#!/usr/bin/env python3
import cv2
import numpy as np
import json
import os
import logging
import gzip
import configparser
import argparse
import sys
from enum import Enum
from functools import partial
from multiprocessing import Pool, cpu_count
from tqdm import tqdm
from typing import Tuple, List, Dict, Any

# Настройка логгирования
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

class ColorFormat(Enum):
    RGB = "rgb"
    RGBW = "rgbw"
    HSV = "hsv"
    RGBWMix = "rgbwmix"

class PixelSelection(Enum):
    LEFT = "left"
    RIGHT = "right"
    TOP = "top"
    BOTTOM = "bottom"

class VideoConfig:
    """
    Конфигурация процесса обработки видео.
    """
    def __init__(self, config_path: str = "config.ini") -> None:
        self.config = configparser.ConfigParser()
        if os.path.exists(config_path):
            self.config.read(config_path)
        else:
            self._create_default_config(config_path)

        self.target_height: int = self.config.getint('processing', 'target_height', fallback=50)
        self.target_width: int = self.config.getint('processing', 'target_width', fallback=50)
        self.batch_size: int = self.config.getint('processing', 'batch_size', fallback=100)
        self.color_format: ColorFormat = ColorFormat(self.config.get('processing', 'color_format', fallback='rgb'))
        self.compress_output: bool = self.config.getboolean('output', 'compress', fallback=False)
        self.num_processes: int = self.config.getint(
            'processing', 'num_processes', fallback=max(1, cpu_count() - 1)
        )
        self.max_frames: int = self.config.getint('processing', 'max_frames', fallback=0)
        self.frame_skip: int = self.config.getint('processing', 'frame_skip', fallback=0)
        self.pixel_selection: PixelSelection = PixelSelection("left")  # по умолчанию, запросим у пользователя
        self.save_video: bool = False  # по умолчанию, запросим у пользователя
        self.output_color_format: ColorFormat = ColorFormat("rgb")  # по умолчанию, запросим у пользователя

    def _create_default_config(self, config_path: str) -> None:
        self.config['processing'] = {
            'target_height': '50',
            'target_width': '50',
            'batch_size': '100',
            'color_format': 'rgb',
            'num_processes': str(max(1, cpu_count() - 1)),
            'max_frames': '0',
            'frame_skip': '0',
        }
        self.config['output'] = {
            'compress': 'False',
        }
        with open(config_path, 'w') as f:
            self.config.write(f)
        logger.info(f"Создан конфигурационный файл {config_path} со значениями по умолчанию.")

class FrameProcessor:
    """
    Класс для обработки отдельных кадров.
    """
    @staticmethod
    def resize_frame(frame: np.ndarray, pixel_selection: PixelSelection, original_width: int, original_height: int, target_height: int = 50, target_width: int = 50) -> np.ndarray:
        if pixel_selection == PixelSelection.LEFT or pixel_selection == PixelSelection.RIGHT:
            aspect_ratio = original_width / original_height
            _target_width = int(target_height * aspect_ratio)
            return cv2.resize(frame, (_target_width, target_height), interpolation=cv2.INTER_AREA)
        elif pixel_selection == PixelSelection.TOP or pixel_selection == PixelSelection.BOTTOM:
            aspect_ratio = original_height / original_width
            _target_height = int(target_width * aspect_ratio)
            return cv2.resize(frame, (target_width, _target_height), interpolation=cv2.INTER_AREA)
        else:
            raise ValueError(f"Неверный выбор селекции для ресайза: {pixel_selection}")

    @staticmethod
    def convert_color_format(frame: np.ndarray, color_format: ColorFormat) -> np.ndarray:
        if color_format == ColorFormat.RGB:
            return cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        elif color_format == ColorFormat.HSV:
            return cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)
        elif color_format == ColorFormat.RGBW:
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            w_channel = np.min(rgb_frame, axis=2, keepdims=True)
            rgbw_frame = np.concatenate((rgb_frame, w_channel), axis=2)
            return rgbw_frame
        elif color_format == ColorFormat.RGBWMix:
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            w_channel = np.min(rgb_frame, axis=2, keepdims=True)
            cw_channel = np.max(rgb_frame, axis=2, keepdims=True)
            rgbwmix_frame = np.concatenate((rgb_frame, w_channel, cw_channel), axis=2)
            return rgbwmix_frame
        else:
            raise ValueError(f"Неподдерживаемый цветовой формат: {color_format}")

    @staticmethod
    def sigmoid_transform(value, midpoint=0.5, steepness=10):
        """Применяет S-образное преобразование к значению цвета."""
        normalized = value / 255.0
        transformed = 1 / (1 + np.exp(-steepness * (normalized - midpoint)))
        return int(transformed * 255.0)

    @staticmethod
    def limit_brightness(pixel, max_brightness=220):
        """Ограничивает суммарную яркость пикселя."""
        if len(pixel) == 3:
            r, g, b = pixel
            total_brightness = r + g + b
            if total_brightness > max_brightness:
                scale = max_brightness / total_brightness
                r = int(r * scale)
                g = int(g * scale)
                b = int(b * scale)
            return [r, g, b]
        elif len(pixel) == 4:
            r, g, b, w = pixel
            total_brightness = r + g + b + w
            if total_brightness > max_brightness:
                scale = max_brightness / total_brightness
                r = int(r * scale)
                g = int(g * scale)
                b = int(b * scale)
                w = int(w * scale)
            return [r, g, b, w]
        elif len(pixel) == 5:
            r, g, b, w, cw = pixel
            total_brightness = r + g + b + w + cw
            if total_brightness > max_brightness:
                scale = max_brightness / total_brightness
                r = int(r * scale)
                g = int(g * scale)
                b = int(b * scale)
                w = int(w * scale)
                cw = int(cw * scale)
            return [r, g, b, w, cw]
        else:
            return pixel

    @staticmethod
    def enhance_saturation(pixel, factor=1.2):
        """Увеличивает насыщенность цвета (только для RGB)."""
        if len(pixel) == 3:
            h, s, v = cv2.cvtColor(np.uint8([[pixel]]), cv2.COLOR_RGB2HSV)[0][0]
            s = int(np.clip(s * factor, 0, 255))
            return cv2.cvtColor(np.uint8([[[h, s, v]]]), cv2.COLOR_HSV2RGB)[0][0].tolist()
        else:
            return pixel

    @staticmethod
    def extract_colors(frame: np.ndarray, pixel_selection: PixelSelection, output_color_format: ColorFormat, target_count: int) -> List[List[int]]:
        height, width, channels = frame.shape
        pixels = []

        if pixel_selection == PixelSelection.LEFT:
            if height != target_count:
                resized_frame = cv2.resize(frame, (1, target_count), interpolation=cv2.INTER_AREA)
                column = resized_frame[:, 0, :]
            else:
                column = frame[:, 0, :]
            for i in range(target_count):
                pixel = column[i].astype(np.float32)
                pixels.append(pixel)
            pixels = pixels[::-1]  # от нижнего к верхнему
        elif pixel_selection == PixelSelection.RIGHT:
            if height != target_count:
                resized_frame = cv2.resize(frame, (1, target_count), interpolation=cv2.INTER_AREA)
                column = resized_frame[:, 0, :]
            else:
                column = frame[:, -1, :]
            for i in range(target_count):
                pixel = column[i].astype(np.float32)
                pixels.append(pixel)
            pixels = pixels[::-1]
        elif pixel_selection == PixelSelection.TOP:
            if width != target_count:
                resized_frame = cv2.resize(frame, (target_count, 1), interpolation=cv2.INTER_AREA)
                row = resized_frame[0, :, :]
            else:
                row = frame[0, :, :]
            for i in range(target_count):
                pixel = row[i].astype(np.float32)
                pixels.append(pixel)
            pixels = pixels[::-1]
        elif pixel_selection == PixelSelection.BOTTOM:
            if width != target_count:
                resized_frame = cv2.resize(frame, (target_count, 1), interpolation=cv2.INTER_AREA)
                row = resized_frame[0, :, :]
            else:
                row = frame[-1, :, :]
            for i in range(target_count):
                pixel = row[i].astype(np.float32)
                pixels.append(pixel)
            pixels = pixels[::-1]
        else:
            raise ValueError(f"Неверный выбор селекции: {pixel_selection}")

        processed_pixels = []
        for pixel in pixels:
            pixel_list = pixel.tolist()
            if output_color_format != ColorFormat.RGBW and len(pixel_list) == 4:
                pixel_list = pixel_list[:3]  # если не RGBW, отбрасываем W
            elif output_color_format == ColorFormat.RGBW and len(pixel_list) == 3:
                w_channel = min(pixel_list)
                pixel_list.append(w_channel)
            elif output_color_format == ColorFormat.RGBWMix and len(pixel_list) == 3:
                w_channel = min(pixel_list)
                cw_channel = max(pixel_list)
                pixel_list.extend([w_channel, cw_channel])
            transformed_pixel = [FrameProcessor.sigmoid_transform(c) for c in pixel_list]
            transformed_pixel = FrameProcessor.limit_brightness(transformed_pixel)
            if output_color_format in [ColorFormat.RGB, ColorFormat.RGBW, ColorFormat.RGBWMix]:
                transformed_pixel = FrameProcessor.enhance_saturation(transformed_pixel)
            processed_pixels.append(transformed_pixel)

        return processed_pixels

def process_frame_batch_worker(
    frame_batch: List[Tuple[int, np.ndarray]],
    original_width: int,
    original_height: int,
    target_height: int,
    target_width: int,
    color_format: ColorFormat,
    frame_skip: int,
    pixel_selection: PixelSelection,
    output_color_format: ColorFormat
) -> Tuple[List[Dict[str, Any]], List[np.ndarray]]:
    results = []
    processed_frames = []
    target_count = target_height if pixel_selection in [PixelSelection.LEFT, PixelSelection.RIGHT] else target_width
    for frame_number, frame in frame_batch:
        try:
            resized_for_display = FrameProcessor.resize_frame(
                frame,
                pixel_selection,
                original_width,
                original_height,
                target_height,
                target_width
            )
            converted = FrameProcessor.convert_color_format(frame, color_format)
            resized_for_extraction = FrameProcessor.resize_frame(
                converted,
                pixel_selection,
                original_width,
                original_height,
                target_height,
                target_width
            )
            pixels = FrameProcessor.extract_colors(resized_for_extraction, pixel_selection, output_color_format, target_count)
            results.append({
                "frame": frame_number + 1,
                "pixels": pixels
            })
            processed_frames.append(resized_for_display)
        except Exception as e:
            logger.error(f"Ошибка при обработке кадра {frame_number}: {e}")
    return results, processed_frames

class VideoColorProcessor:
    """
    Основной класс для обработки видео.
    """
    def __init__(
        self,
        video_path: str,
        output_json_path: str,
        config: VideoConfig
    ) -> None:
        self.video_path: str = video_path
        self.output_json_path: str = output_json_path
        self.config: VideoConfig = config
        self.cap = None
        self.video_info: Dict[str, Any] = {}
        self._validate_inputs()
        self.output_video_path: str = ""

    def _validate_inputs(self) -> None:
        if not os.path.exists(self.video_path):
            raise FileNotFoundError(f"Видео файл не найден: {self.video_path}")
        if self.config.target_height <= 0 or self.config.target_width <= 0:
            raise ValueError("Высота и ширина должны быть положительными")
        if self.config.batch_size <= 0:
            raise ValueError("Размер батча должен быть положительным")

    def initialize_video(self) -> Dict[str, Any]:
        self.cap = cv2.VideoCapture(self.video_path)
        if not self.cap.isOpened():
            raise RuntimeError("Не удалось открыть видео файл")

        self.video_info = {
            "frame_count": int(self.cap.get(cv2.CAP_PROP_FRAME_COUNT)),
            "fps": self.cap.get(cv2.CAP_PROP_FPS),
            "original_width": int(self.cap.get(cv2.CAP_PROP_FRAME_WIDTH)),
            "original_height": int(self.cap.get(cv2.CAP_PROP_FRAME_HEIGHT)),
            "fourcc": int(self.cap.get(cv2.CAP_PROP_FOURCC)),
        }
        logger.info(f"Открыто видео: {self.video_path} "
                    f"({self.video_info['original_width']}x{self.video_info['original_height']}, {self.video_info['frame_count']} кадров)")

        base_name = os.path.basename(self.video_path)
        name, ext = os.path.splitext(base_name)
        output_dir = os.path.dirname(self.output_json_path)
        self.output_video_path = os.path.join(output_dir, f"{name}_resized{ext}")

        return self.video_info

    def _generate_batches(self):
        frame_number = 0
        processed_frames = 0
        batch = []
        total_frames = self.video_info.get("frame_count", 0)
        max_frames = self.config.max_frames if self.config.max_frames > 0 else total_frames
        frame_skip = self.config.frame_skip

        with tqdm(total=min(total_frames, max_frames), desc="Чтение кадров") as pbar:
            while processed_frames < max_frames:
                ret, frame = self.cap.read()
                if not ret:
                    break

                if frame_skip > 0 and (frame_number % (frame_skip + 1)) != 0:
                    frame_number += 1
                    continue

                batch.append((frame_number, frame))
                processed_frames += 1
                frame_number += 1
                pbar.update(1)

                if len(batch) >= self.config.batch_size:
                    yield batch
                    batch = []

            if batch:
                yield batch

    def process_video(self) -> List[Dict[str, Any]]:
        self.initialize_video()
        results: List[Dict[str, Any]] = []
        all_processed_frames: List[np.ndarray] = []

        total_frames = self.video_info.get("frame_count", 0)
        max_frames = self.config.max_frames if self.config.max_frames > 0 else total_frames
        total_batches = (max_frames // self.config.batch_size) + (1 if max_frames % self.config.batch_size != 0 else 0)

        worker = partial(
            process_frame_batch_worker,
            original_width=self.video_info['original_width'],
            original_height=self.video_info['original_height'],
            target_height=self.config.target_height,
            target_width=self.config.target_width,
            color_format=self.config.color_format,
            frame_skip=self.config.frame_skip,
            pixel_selection=self.config.pixel_selection,
            output_color_format=self.config.output_color_format
        )

        try:
            with Pool(processes=self.config.num_processes) as pool:
                for batch_result, processed_frames_batch in tqdm(
                    pool.imap(worker, self._generate_batches()),
                    total=total_batches,
                    desc="Обработка батчей"
                ):
                    results.extend(batch_result)
                    all_processed_frames.extend(processed_frames_batch)

            if self.config.save_video and all_processed_frames:
                if self.config.pixel_selection in [PixelSelection.LEFT, PixelSelection.RIGHT]:
                    output_height = self.config.target_height
                    aspect_ratio = self.video_info['original_width'] / self.video_info['original_height']
                    output_width = int(output_height * aspect_ratio)
                else:
                    output_width = self.config.target_width
                    aspect_ratio = self.video_info['original_height'] / self.video_info['original_width']
                    output_height = int(output_width * aspect_ratio)

                fourcc = cv2.VideoWriter_fourcc(*'mp4v')
                out = cv2.VideoWriter(self.output_video_path, fourcc, self.video_info['fps'], (output_width, output_height))
                for frame in all_processed_frames:
                    out.write(frame)
                out.release()
                logger.info(f"Обработанное видео сохранено в {self.output_video_path}")

            logger.info("Обработка видео завершена.")
            return results

        except Exception as e:
            logger.error(f"Ошибка при обработке видео: {e}")
            raise
        finally:
            if self.cap:
                self.cap.release()
                logger.info("Закрыт видеопоток.")

    def save_to_json(self, data: List[Dict[str, Any]]) -> None:
        try:
            if os.path.isdir(self.output_json_path):
                self.output_json_path = os.path.join(self.output_json_path, "output.json")

            output_dir = os.path.dirname(self.output_json_path)
            if output_dir:
                os.makedirs(output_dir, exist_ok=True)

            if self.config.compress_output:
                output_path = self.output_json_path + ".gz"
                with gzip.open(output_path, 'wt', encoding='utf-8') as f:
                    json.dump(data, f, ensure_ascii=False, indent=2)
                logger.info(f"Данные сохранены с сжатием в {output_path}")
            else:
                with open(self.output_json_path, 'w', encoding='utf-8') as f:
                    json.dump(data, f, ensure_ascii=False, indent=2)
                logger.info(f"Данные сохранены в {self.output_json_path}")

        except Exception as e:
            logger.error(f"Ошибка при сохранении JSON: {e}")
            raise

def apply_temporal_smoothing(frames_data: List[Dict[str, Any]], alpha: float) -> List[Dict[str, Any]]:
    """
    Применяет экспоненциальное временное сглаживание к последовательности кадров.
    Для каждого кадра новый цвет рассчитывается как:
       new_val = alpha * current + (1 - alpha) * previous_smoothed,
    где для первого кадра previous_smoothed = current.
    """
    # Сортируем по номеру кадра на случай, если порядок изменён
    sorted_data = sorted(frames_data, key=lambda d: d["frame"])
    prev_smoothed = None
    for item in sorted_data:
        current_pixels = item["pixels"]
        if prev_smoothed is None:
            # Для первого кадра сглаживание не применяется
            smoothed_pixels = current_pixels
        else:
            smoothed_pixels = []
            # Применяем сглаживание для каждого пикселя по позиции
            for curr, prev in zip(current_pixels, prev_smoothed):
                smoothed_pixel = []
                for c_val, p_val in zip(curr, prev):
                    # Экспоненциальное сглаживание. Приводим к int после расчёта.
                    new_val = int(alpha * c_val + (1 - alpha) * p_val)
                    smoothed_pixel.append(new_val)
                smoothed_pixels.append(smoothed_pixel)
        item["pixels"] = smoothed_pixels  # заменяем исходное значение сглаженными
        prev_smoothed = smoothed_pixels
    return sorted_data

def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Обработка видео: извлечение цветов и сохранение уменьшенной копии",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    parser.add_argument("--video-path", type=str, help="Путь к входному видео файлу")
    parser.add_argument("--output-path", type=str, help="Путь для выходного JSON файла")
    parser.add_argument("--target-height", type=int, default=50, help="Целевая высота для боковых пикселей")
    parser.add_argument("--target-width", type=int, default=50, help="Целевая ширина для верхних/нижних пикселей")
    parser.add_argument("--color-format", type=str, default="rgb", choices=[cf.value for cf in ColorFormat], help="Цветовой формат для обработки видео")
    parser.add_argument("--max-frames", type=int, default=0, help="Максимальное количество кадров (0 - все)")
    parser.add_argument("--frame-skip", type=int, default=0, help="Количество пропускаемых кадров")
    parser.add_argument("--midpoint", type=float, default=0.5, help="Точка перегиба S-образной кривой (0-1)")
    parser.add_argument("--steepness", type=float, default=10.0, help="Крутизна S-образной кривой")
    parser.add_argument("--max-brightness", type=int, default=220, help="Максимальная суммарная яркость пикселя")
    parser.add_argument("--saturation-factor", type=float, default=1.2, help="Коэффициент усиления насыщенности")
    parser.add_argument("--temporal-alpha", type=float, default=0.3, help="Коэффициент сглаживания для временной фильтрации (0-1, где 1 = без сглаживания)")
    return parser.parse_args()

def main() -> None:
    try:
        args = parse_arguments()
        config = VideoConfig()

        # Применение аргументов командной строки, если они предоставлены
        if args.color_format:
            config.color_format = ColorFormat(args.color_format)
        if args.max_frames:
            config.max_frames = args.max_frames
        if args.frame_skip:
            config.frame_skip = args.frame_skip
        if args.target_height:
            config.target_height = args.target_height
        if args.target_width:
            config.target_width = args.target_width

        # Запрашиваем у пользователя pixel_selection
        while True:
            pixel_selection_input = input("Выберите область пикселей (left/right/top/bottom, по умолчанию left): ").strip().lower()
            if not pixel_selection_input or pixel_selection_input in [ps.value for ps in PixelSelection]:
                config.pixel_selection = PixelSelection(pixel_selection_input or "left")
                break
            else:
                print("Ошибка: Пожалуйста, выберите 'left', 'right', 'top' или 'bottom'.")

        # Запрашиваем у пользователя output_color_format
        while True:
            output_color_format_input = input(f"Выберите цветовой формат для записи в JSON ({', '.join([cf.value for cf in ColorFormat])}, по умолчанию rgb): ").strip().lower()
            if not output_color_format_input or output_color_format_input in [cf.value for cf in ColorFormat]:
                config.output_color_format = ColorFormat(output_color_format_input or "rgb")
                break
            else:
                print(f"Ошибка: Пожалуйста, выберите из '{', '.join([cf.value for cf in ColorFormat])}'.")

        # Запрашиваем у пользователя save_video
        while True:
            save_video_input = input("Сохранить обработанное видео? (yes/no, по умолчанию no): ").strip().lower()
            if not save_video_input or save_video_input == 'no':
                config.save_video = False
                break
            elif save_video_input == 'yes':
                config.save_video = True
                break
            else:
                print("Ошибка: Пожалуйста, выберите 'yes' или 'no'.")

        # Если пути к видео и/или выходному файлу не предоставлены, запрашиваем у пользователя
        video_path = args.video_path
        output_json_path = args.output_path

        if not video_path:
            while True:
                video_path = input("Введите путь к видео файлу (или нажмите Enter для 'input_video.mp4'): ").strip()
                if not video_path:
                    video_path = "input_video.mp4"
                if os.path.exists(video_path):
                    break
                print("Ошибка: Файл не найден. Пожалуйста, введите корректный путь.")

        if not output_json_path:
            output_json_path = input("Введите путь для выходного JSON файла (или нажмите Enter для 'output/colors.json'): ").strip()
            if not output_json_path:
                output_json_path = "output/colors.json"

        # Параметры S-образной кривой, ограничения яркости и усиления насыщенности (из аргументов)
        midpoint = args.midpoint
        steepness = args.steepness
        max_brightness = args.max_brightness
        saturation_factor = args.saturation_factor

        # Используем partial для передачи параметров в методы класса
        FrameProcessor.sigmoid_transform = partial(FrameProcessor.sigmoid_transform, midpoint=midpoint, steepness=steepness)
        FrameProcessor.limit_brightness = partial(FrameProcessor.limit_brightness, max_brightness=max_brightness)
        FrameProcessor.enhance_saturation = partial(FrameProcessor.enhance_saturation, factor=saturation_factor)

        processor = VideoColorProcessor(
            video_path=video_path,
            output_json_path=output_json_path,
            config=config
        )

        frames_data = processor.process_video()
        # Применяем временное сглаживание для имитации эффекта ambilight
        frames_data = apply_temporal_smoothing(frames_data, args.temporal_alpha)
        processor.save_to_json(frames_data)
        logger.info("Скрипт завершен успешно.")
    except Exception as e:
        logger.error(f"Произошла критическая ошибка: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()