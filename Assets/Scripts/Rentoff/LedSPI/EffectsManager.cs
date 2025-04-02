using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LEDControl
{
    public enum SunMode
    {
        Warm,
        Cold
    }

    [Serializable]
    public class SunMovementSettings
    {
        [Tooltip("Number of pixels to show in Sun Movement mode.")]
        public int pixelCount = 10;

        [Tooltip("Total cycle length in seconds for Sun Movement at speed 1.")]
        public float cycleLength = 241f;

        [Tooltip("Start time in seconds for Sun Movement to appear within the cycle.")]
        public float startTime = 38f;

        [Tooltip("End time in seconds for Sun Movement to disappear within the cycle.")]
        public float endTime = 230f;

        [Tooltip("Brightness multiplier for this sun mode.")]
        [Range(0f, 1f)]
        public float brightnessMultiplier = 1f;
    }

    public class Comet
    {
        public float position;
        public Color32 color;
        public float length;
        public float brightness;
        public bool isActive;
        public float startTime;
        public bool isMoving;
        public float direction;

        public int lastLedIndex = -1;
        public int[] affectedLeds = null;
        public float[] brightnessByLed = null;

        public Comet(float position, Color32 color, float length, float brightness, float direction)
        {
            this.position = position;
            this.color = color;
            this.length = length;
            this.brightness = brightness;
            this.isActive = true;
            this.startTime = Time.time;
            this.isMoving = false;
            this.direction = direction;
        }

        // Метод для обновления кэшированных данных
        public void UpdateCache(int totalLEDs, float tailIntensity)
        {
            int dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(length));
            if (affectedLeds == null || affectedLeds.Length != dynamicLedCount)
            {
                affectedLeds = new int[dynamicLedCount];
                brightnessByLed = new float[dynamicLedCount];
            }

            int ledIndex = Mathf.FloorToInt(position);
            lastLedIndex = ledIndex;

            for (int j = 0; j < dynamicLedCount; j++)
            {
                int offset = direction > 0 ? j : -j;
                int currentLedIndex = ledIndex + offset;
                currentLedIndex = Mathf.RoundToInt(Mathf.Repeat(currentLedIndex, totalLEDs));
                affectedLeds[j] = currentLedIndex;

                float tailFalloff = 1f - ((float)j / (dynamicLedCount - 1));
                tailFalloff = Mathf.Clamp01(tailFalloff);
                brightnessByLed[j] = tailFalloff * tailIntensity;
            }
        }
    }

    public class EffectsManager : MonoBehaviour
    {
        [Header("Speed Synth Mode Settings")]
        [Tooltip("Базовое количество диодов в режиме Speed Synth")]
        [SerializeField] public int synthLedCountBase = 5;

        [Tooltip("Множитель длины хвоста в зависимости от скорости")]
        [SerializeField] public float speedLedCountFactor = 0.5f;

        [Tooltip("Множитель яркости в зависимости от скорости")]
        [SerializeField] public float speedBrightnessFactor = 1.5f;

        [Tooltip("Интенсивность эффекта 'хвоста кометы'")]
        [Range(0f, 1f)]
        [SerializeField] public float tailIntensity = 0.7f;

        [Tooltip("Направление движения в режиме Speed Synth")]
        public MoveDirection moveDirection = MoveDirection.Forward;

        [Tooltip("Режим движения комет с конца к началу ленты")]
        [SerializeField] public bool startFromEnd = false;

        [Tooltip("Множитель яркости для неподвижной кометы")]
        [Range(0f, 1f)]
        [SerializeField] public float stationaryBrightnessFactor = 0.5f;

        [Header("Sun Movement Settings")]
        [Tooltip("Настройки для теплого солнца")]
        [SerializeField] private SunMovementSettings warmSunSettings = new SunMovementSettings();

        [Tooltip("Настройки для холодного солнца")]
        [SerializeField] private SunMovementSettings coldSunSettings = new SunMovementSettings();

        [Header("Touch Panel Settings")]
        [Tooltip("Включить режим, при котором касание гасит уже горящий сегмент")]
        [SerializeField] public bool toggleTouchMode = false;

        [Tooltip("Задержка перед началом движения кометы (в секундах)")]
        [SerializeField] private float cometMoveDelay = 0.2f;

        public float currentSpeed = 1f;
        public float MultiplySpeed = 2f;
        private float sunMovementPhase = 0f;
        public Dictionary<int, List<Comet>> stripComets = new Dictionary<int, List<Comet>>();
        private Dictionary<int, float> lastTouchTimes = new Dictionary<int, float>();

        // Кэширование данных для оптимизации
        private Dictionary<int, Color32[]> pixelCache = new Dictionary<int, Color32[]>();
        private Dictionary<int, string> hexCache = new Dictionary<int, string>();
        private Dictionary<int, float> lastUpdateTime = new Dictionary<int, float>();
        private float cacheLifetime = 0.05f; // 50 мс для кэша

        private int previousSynthLedCountBase;
        private float previousSpeedLedCountFactor;
        private float previousSpeedBrightnessFactor;
        private float previousTailIntensity;
        private MoveDirection previousMoveDirection;
        private SunMovementSettings previousWarmSunSettings;
        private SunMovementSettings previousColdSunSettings;
        [SerializeField] private StripDataManager stripDataManager;

        public void UpdateSpeed(float speed)
        {
            currentSpeed = speed * MultiplySpeed;

            ClearCaches();
        }

        private void ClearCaches()
        {
            pixelCache.Clear();
            hexCache.Clear();
            lastUpdateTime.Clear();
        }

        public void UpdateComets(int stripIndex, StripDataManager stripManager)
        {
            if (!stripComets.ContainsKey(stripIndex))
            {
                stripComets[stripIndex] = new List<Comet>();
                return;
            }

            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            float timeSinceLastUpdate = Time.fixedDeltaTime;
            List<Comet> comets = stripComets[stripIndex];

            float lastTouchTime = lastTouchTimes.ContainsKey(stripIndex) ? lastTouchTimes[stripIndex] : 0f;
            bool canMove = Time.time - lastTouchTime >= cometMoveDelay;

            // Оптимизация: обрабатываем только активные кометы
            bool anyActive = false;
            for (int i = comets.Count - 1; i >= 0; i--)
            {
                Comet comet = comets[i];
                if (!comet.isActive) continue;
                anyActive = true;

                // Если прошло достаточно времени, комета начинает двигаться
                if (canMove && !comet.isMoving)
                {
                    comet.isMoving = true;
                }

                // Двигаем комету, если она движется
                if (comet.isMoving)
                {
                    // Обновляем направление на каждый кадр
                    if (startFromEnd)
                    {
                        comet.direction = currentSpeed >= 0 ? -1f : 1f;
                    }
                    else
                    {
                        comet.direction = currentSpeed >= 0 ? 1f : -1f;
                    }

                    float directionMultiplier = comet.direction;
                    float oldPosition = comet.position;
                    comet.position += Mathf.Abs(currentSpeed) * timeSinceLastUpdate * 30f * directionMultiplier;

                    // Обработка выхода за пределы
                    if (comet.position < 0)
                        comet.position += totalLEDs;
                    else if (comet.position >= totalLEDs)
                        comet.position -= totalLEDs;

                    // Обновляем кэш только если комета переместилась
                    if (Mathf.FloorToInt(oldPosition) != Mathf.FloorToInt(comet.position))
                    {
                        comet.UpdateCache(totalLEDs, tailIntensity);
                    }
                }
            }

            if (!anyActive && pixelCache.ContainsKey(stripIndex))
            {
                pixelCache.Remove(stripIndex);
                hexCache.Remove(stripIndex);
                lastUpdateTime.Remove(stripIndex);
            }
        }

        public void ResetComets(int stripIndex)
        {
            if (stripComets.ContainsKey(stripIndex))
            {
                stripComets[stripIndex].Clear();
            }
            lastTouchTimes.Remove(stripIndex);

            // Очищаем кэш для этой полосы
            pixelCache.Remove(stripIndex);
            hexCache.Remove(stripIndex);
            lastUpdateTime.Remove(stripIndex);
        }

        public void AddComet(int stripIndex, float position, Color32 color, float length, float brightness)
        {
            if (!stripComets.ContainsKey(stripIndex))
            {
                stripComets[stripIndex] = new List<Comet>();
            }

            // Если включен режим startFromEnd, начинаем с конца ленты
            if (startFromEnd)
            {
                position = stripDataManager.totalLEDsPerStrip[stripIndex] - 1;
                moveDirection = MoveDirection.Backward;
            }

            // Определяем направление на основе текущей скорости
            float direction = currentSpeed >= 0 ? 1f : -1f;

            Comet newComet = new Comet(position, color, length, brightness, direction);
            stripComets[stripIndex].Add(newComet);
            lastTouchTimes[stripIndex] = Time.time;

            // Очищаем кэш при добавлении новой кометы
            pixelCache.Remove(stripIndex);
            hexCache.Remove(stripIndex);
            lastUpdateTime.Remove(stripIndex);

            // Инициализируем кэш для новой кометы
            newComet.UpdateCache(stripDataManager.totalLEDsPerStrip[stripIndex], tailIntensity);
        }

        public void UpdateLastTouchTime(int stripIndex)
        {
            lastTouchTimes[stripIndex] = Time.time;
        }

        public void UpdateSunMovementPhase()
        {
            SunMovementSettings settings = warmSunSettings;
            float cycleTime = settings.cycleLength / Mathf.Max(0.1f, currentSpeed);
            sunMovementPhase += Time.fixedDeltaTime / cycleTime;
            if (sunMovementPhase >= 1f)
                sunMovementPhase = 0f;

            // Очищаем кэш при изменении фазы солнца
            ClearCaches();
        }

        public bool CheckForChanges()
        {
            bool changed =
                synthLedCountBase != previousSynthLedCountBase ||
                speedLedCountFactor != previousSpeedLedCountFactor ||
                speedBrightnessFactor != previousSpeedBrightnessFactor ||
                tailIntensity != previousTailIntensity ||
                moveDirection != previousMoveDirection ||
                !AreSunSettingsEqual(warmSunSettings, previousWarmSunSettings) ||
                !AreSunSettingsEqual(coldSunSettings, previousColdSunSettings);

            if (changed)
            {
                CachePreviousValues();
                ClearCaches(); // Очищаем кэш при изменении настроек
            }

            return changed;
        }

        private bool AreSunSettingsEqual(SunMovementSettings a, SunMovementSettings b)
        {
            if (a == null || b == null) return a == b;
            return a.pixelCount == b.pixelCount &&
                   a.cycleLength == b.cycleLength &&
                   a.startTime == b.startTime &&
                   a.endTime == b.endTime &&
                   a.brightnessMultiplier == b.brightnessMultiplier;
        }

        private void CachePreviousValues()
        {
            previousSynthLedCountBase = synthLedCountBase;
            previousSpeedLedCountFactor = speedLedCountFactor;
            previousSpeedBrightnessFactor = speedBrightnessFactor;
            previousTailIntensity = tailIntensity;
            previousMoveDirection = moveDirection;
            previousWarmSunSettings = new SunMovementSettings
            {
                pixelCount = warmSunSettings.pixelCount,
                cycleLength = warmSunSettings.cycleLength,
                startTime = warmSunSettings.startTime,
                endTime = warmSunSettings.endTime,
                brightnessMultiplier = warmSunSettings.brightnessMultiplier
            };
            previousColdSunSettings = new SunMovementSettings
            {
                pixelCount = coldSunSettings.pixelCount,
                cycleLength = coldSunSettings.cycleLength,
                startTime = coldSunSettings.startTime,
                endTime = coldSunSettings.endTime,
                brightnessMultiplier = coldSunSettings.brightnessMultiplier
            };
        }

        public string GetHexDataForSpeedSynthMode(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            // Проверяем кэш
            if (hexCache.ContainsKey(stripIndex) &&
                lastUpdateTime.ContainsKey(stripIndex) &&
                Time.time - lastUpdateTime[stripIndex] < cacheLifetime)
            {
                return hexCache[stripIndex];
            }

            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
            Color32 blackColor = new Color32(0, 0, 0, 255);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            if (!stripComets.ContainsKey(stripIndex) || stripComets[stripIndex].Count == 0)
            {
                hexCache[stripIndex] = "";
                lastUpdateTime[stripIndex] = Time.time;
                return "";
            }

            // Используем кэшированный массив цветов или создаем новый
            Color32[] pixelColors;
            if (!pixelCache.ContainsKey(stripIndex))
            {
                pixelColors = new Color32[totalLEDs];
                pixelCache[stripIndex] = pixelColors;
            }
            else
            {
                pixelColors = pixelCache[stripIndex];
            }

            // Обнуляем цвета
            for (int i = 0; i < totalLEDs; i++)
            {
                pixelColors[i] = blackColor;
            }

            // Обрабатываем только активные кометы
            var activeComets = stripComets[stripIndex].Where(c => c.isActive).ToList();
            foreach (Comet comet in activeComets)
            {
                int dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(comet.length));
                float dynamicBrightness = Mathf.Clamp01(comet.brightness * stripBrightness);

                // Учитываем скорость для интенсивности свечения
                float speedBrightnessMultiplier = 1f + Mathf.Abs(currentSpeed) * speedBrightnessFactor;
                dynamicBrightness *= speedBrightnessMultiplier;

                // Если комета неподвижна, уменьшаем яркость
                if (!comet.isMoving)
                {
                    dynamicBrightness *= stationaryBrightnessFactor;
                }

                // Используем кэшированные данные о затронутых LED
                if (comet.affectedLeds != null && comet.brightnessByLed != null)
                {
                    for (int j = 0; j < comet.affectedLeds.Length; j++)
                    {
                        int currentLedIndex = comet.affectedLeds[j];
                        if (currentLedIndex >= 0 && currentLedIndex < totalLEDs)
                        {
                            float brightnessFactor = comet.brightnessByLed[j];
                            byte r = (byte)Mathf.Clamp(comet.color.r * brightnessFactor * dynamicBrightness, 0, 255);
                            byte g = (byte)Mathf.Clamp(comet.color.g * brightnessFactor * dynamicBrightness, 0, 255);
                            byte b = (byte)Mathf.Clamp(comet.color.b * brightnessFactor * dynamicBrightness, 0, 255);

                            // Применяем более яркий цвет, если несколько комет перекрываются
                            Color32 currentColor = pixelColors[currentLedIndex];
                            pixelColors[currentLedIndex] = new Color32(
                                (byte)Mathf.Max(currentColor.r, r),
                                (byte)Mathf.Max(currentColor.g, g),
                                (byte)Mathf.Max(currentColor.b, b),
                                255
                            );
                        }
                    }
                }
                else
                {
                    comet.UpdateCache(totalLEDs, tailIntensity);
                }
            }

            // Формируем строку HEX
            StringBuilder sb = new StringBuilder(totalLEDs * hexPerPixel);
            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = pixelColors[i];
                if (mode == DataMode.RGBW)
                {
                    sb.Append(colorProcessor.ColorToHexRGBW(pixelColor, stripBrightness, stripGamma, stripGammaEnabled));
                }
                else if (mode == DataMode.RGB)
                {
                    sb.Append(colorProcessor.ColorToHexRGB(pixelColor, stripBrightness, stripGamma, stripGammaEnabled));
                }
                else
                {
                    sb.Append(colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled));
                }
            }

            string result = OptimizeHexString(sb.ToString(), new string('0', hexPerPixel), hexPerPixel);

            // Сохраняем результат в кэш
            hexCache[stripIndex] = result;
            lastUpdateTime[stripIndex] = Time.time;

            return result;
        }

        public string GetHexDataForSunMovement(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            // Проверяем кэш
            if (hexCache.ContainsKey(stripIndex) &&
                lastUpdateTime.ContainsKey(stripIndex) &&
                Time.time - lastUpdateTime[stripIndex] < cacheLifetime)
            {
                return hexCache[stripIndex];
            }

            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
            Color32 sunColor = stripManager.GetSunMode(stripIndex) == SunMode.Warm ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 255);
            Color32 blackColor = new Color32(0, 0, 0, 255);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            SunMovementSettings settings = stripManager.GetSunMode(stripIndex) == SunMode.Warm ? warmSunSettings : coldSunSettings;
            float currentCycleTime = sunMovementPhase * settings.cycleLength;
            bool isActive = currentCycleTime >= settings.startTime && currentCycleTime <= settings.endTime;

            // Используем кэшированный массив цветов или создаем новый
            Color32[] pixelColors;
            if (!pixelCache.ContainsKey(stripIndex))
            {
                pixelColors = new Color32[totalLEDs];
                pixelCache[stripIndex] = pixelColors;
            }
            else
            {
                pixelColors = pixelCache[stripIndex];
            }

            // Оптимизация: если солнце неактивно, заполняем все черным и возвращаем пустую строку
            if (!isActive)
            {
                hexCache[stripIndex] = "";
                lastUpdateTime[stripIndex] = Time.time;
                return "";
            }

            float fadeInFactor = 1.0f;
            float fadeOutFactor = 1.0f;
            float fadeTime = 5.0f;

            if (currentCycleTime < settings.startTime + fadeTime)
            {
                fadeInFactor = (currentCycleTime - settings.startTime) / fadeTime;
            }
            if (currentCycleTime > settings.endTime - fadeTime)
            {
                fadeOutFactor = (settings.endTime - currentCycleTime) / fadeTime;
            }

            float activePhase = (currentCycleTime - settings.startTime) / (settings.endTime - settings.startTime);
            float sunPosition = activePhase * totalLEDs;
            float brightnessMultiplier = Mathf.Min(fadeInFactor, fadeOutFactor) * settings.brightnessMultiplier;

            // Оптимизация: вычисляем только те пиксели, которые могут быть затронуты солнцем
            int startPixel = Mathf.Max(0, Mathf.FloorToInt(sunPosition - settings.pixelCount / 2));
            int endPixel = Mathf.Min(totalLEDs - 1, Mathf.CeilToInt(sunPosition + settings.pixelCount / 2));

            // Обнуляем все цвета
            for (int i = 0; i < totalLEDs; i++)
            {
                pixelColors[i] = blackColor;
            }

            // Заполняем только затронутые пиксели
            for (int i = startPixel; i <= endPixel; i++)
            {
                float distance = Mathf.Abs(i - sunPosition);
                float brightnessFactor = Mathf.Clamp01(1f - distance / (settings.pixelCount / 2f));
                brightnessFactor *= brightnessMultiplier;

                if (brightnessFactor > 0)
                {
                    pixelColors[i] = new Color32(
                        (byte)(sunColor.r * brightnessFactor * stripBrightness),
                        (byte)(sunColor.g * brightnessFactor * stripBrightness),
                        (byte)(sunColor.b * brightnessFactor * stripBrightness),
                        255
                    );
                }
            }

            // Формируем строку HEX
            StringBuilder sb = new StringBuilder(totalLEDs * hexPerPixel);
            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = pixelColors[i];
                if (mode == DataMode.RGBW)
                {
                    sb.Append(colorProcessor.ColorToHexRGBW(pixelColor, stripBrightness, stripGamma, stripGammaEnabled));
                }
                else if (mode == DataMode.RGB)
                {
                    sb.Append(colorProcessor.ColorToHexRGB(pixelColor, stripBrightness, stripGamma, stripGammaEnabled));
                }
                else
                {
                    sb.Append(colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled));
                }
            }

            string result = OptimizeHexString(sb.ToString(), new string('0', hexPerPixel), hexPerPixel);

            // Сохраняем результат в кэш
            hexCache[stripIndex] = result;
            lastUpdateTime[stripIndex] = Time.time;

            return result;
        }

        public string GetHexDataForSegmentMode(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            // Проверяем кэш
            if (hexCache.ContainsKey(stripIndex) &&
                lastUpdateTime.ContainsKey(stripIndex) &&
                Time.time - lastUpdateTime[stripIndex] < cacheLifetime)
            {
                return hexCache[stripIndex];
            }

            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int ledsPerSegment = stripManager.ledsPerSegment;
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            // Оптимизация: предварительно выделяем StringBuilder с известным размером
            StringBuilder sb = new StringBuilder(totalLEDs * hexPerPixel);

            // Оптимизация: обрабатываем по сегментам, а не по отдельным светодиодам
            int totalSegments = (totalLEDs + ledsPerSegment - 1) / ledsPerSegment;
            for (int segmentIndex = 0; segmentIndex < totalSegments; segmentIndex++)
            {
                Color32 segmentColor = stripManager.GetSegmentColor(stripIndex, segmentIndex);
                Color32 pixelColor = new Color32(
                    (byte)(segmentColor.r * stripBrightness),
                    (byte)(segmentColor.g * stripBrightness),
                    (byte)(segmentColor.b * stripBrightness),
                    255
                );

                // Оптимизация: вычисляем HEX для сегмента один раз
                string segmentHex;
                if (mode == DataMode.RGBW)
                {
                    segmentHex = colorProcessor.ColorToHexRGBW(pixelColor, stripBrightness, stripGamma, stripGammaEnabled);
                }
                else if (mode == DataMode.RGB)
                {
                    segmentHex = colorProcessor.ColorToHexRGB(pixelColor, stripBrightness, stripGamma, stripGammaEnabled);
                }
                else
                {
                    segmentHex = colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled);
                }

                // Применяем одинаковый HEX ко всем светодиодам в сегменте
                int startLed = segmentIndex * ledsPerSegment;
                int endLed = Mathf.Min(startLed + ledsPerSegment, totalLEDs);
                for (int i = startLed; i < endLed; i++)
                {
                    sb.Append(segmentHex);
                }
            }

            string result = OptimizeHexString(sb.ToString(), new string('0', hexPerPixel), hexPerPixel);

            // Сохраняем результат в кэш
            hexCache[stripIndex] = result;
            lastUpdateTime[stripIndex] = Time.time;

            return result;
        }

        private string OptimizeHexString(string hexString, string blackHex, int hexPerPixel)
        {
            // Оптимизация: используем бинарный поиск для нахождения последнего ненулевого пикселя
            int totalPixels = hexString.Length / hexPerPixel;
            int left = 0;
            int right = totalPixels - 1;

            // Быстрая проверка: если строка пустая или все пиксели черные
            if (totalPixels == 0 || (totalPixels > 0 && hexString.Substring(0, hexPerPixel) == blackHex &&
                                     hexString.Substring((totalPixels - 1) * hexPerPixel, hexPerPixel) == blackHex))
            {
                // Проверяем, все ли пиксели черные
                bool allBlack = true;
                for (int i = 0; i < totalPixels; i++)
                {
                    if (hexString.Substring(i * hexPerPixel, hexPerPixel) != blackHex)
                    {
                        allBlack = false;
                        break;
                    }
                }
                if (allBlack) return "";
            }

            // Находим последний ненулевой пиксель
            while (totalPixels > 0)
            {
                string lastPixel = hexString.Substring((totalPixels - 1) * hexPerPixel, hexPerPixel);
                if (lastPixel.Equals(blackHex, StringComparison.OrdinalIgnoreCase))
                    totalPixels--;
                else
                    break;
            }

            return hexString.Substring(0, totalPixels * hexPerPixel);
        }

        public void HandleTouchInput(int stripIndex, int touchCol, StripDataManager stripManager, StateManager.AppState appState)
        {
            if (appState != StateManager.AppState.Active) return;

            int segmentIndex = touchCol + stripManager.touchPanelOffset;
            if (segmentIndex < 0 || segmentIndex >= stripManager.GetTotalSegments(stripIndex)) return;

            Color32 currentColor = stripManager.GetSegmentColor(stripIndex, segmentIndex);
            Color32 blackColor = new Color32(0, 0, 0, 255);
            Color32 synthColor = stripManager.GetSynthColorForStrip(stripIndex);

            // Очищаем кэш при изменении сегментов
            pixelCache.Remove(stripIndex);
            hexCache.Remove(stripIndex);
            lastUpdateTime.Remove(stripIndex);

            if (toggleTouchMode && !currentColor.Equals(blackColor))
            {
                stripManager.SetSegmentColor(stripIndex, segmentIndex, blackColor);
            }
            else if (currentColor.Equals(blackColor))
            {
                stripManager.SetSegmentColor(stripIndex, segmentIndex, synthColor);

                // Оптимизация: вычисляем параметры кометы только один раз
                float dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(synthLedCountBase + Mathf.Abs(currentSpeed) * speedLedCountFactor));
                float dynamicBrightness = Mathf.Clamp01(stripManager.GetStripBrightness(stripIndex) + Mathf.Abs(currentSpeed) * speedBrightnessFactor);
                AddComet(stripIndex, segmentIndex * stripManager.ledsPerSegment, synthColor, dynamicLedCount, dynamicBrightness);
            }
        }

        public void CleanupInactiveComets()
        {
            foreach (var stripIndex in stripComets.Keys.ToList())
            {
                List<Comet> comets = stripComets[stripIndex];
                bool removed = false;

                // Удаляем неактивные кометы
                for (int i = comets.Count - 1; i >= 0; i--)
                {
                    Comet comet = comets[i];

                    if (!comet.isActive ||
                        (comet.lastLedIndex != -1 &&
                         comet.lastLedIndex < 0 ||
                         comet.lastLedIndex >= stripDataManager.totalLEDsPerStrip[stripIndex]))
                    {
                        comets.RemoveAt(i);
                        removed = true;
                    }
                }

                // Если кометы были удалены, очищаем кэш для этой полосы
                if (removed)
                {
                    pixelCache.Remove(stripIndex);
                    hexCache.Remove(stripIndex);
                    lastUpdateTime.Remove(stripIndex);
                }
            }
        }

        // Метод для установки времени жизни кэша
        public void SetCacheLifetime(float lifetime)
        {
            cacheLifetime = Mathf.Max(0.01f, lifetime);
        }

        // Вспомогательный метод для обновления кэша всех комет
        public void UpdateAllCometCaches()
        {
            foreach (var stripIndex in stripComets.Keys)
            {
                int totalLEDs = stripDataManager.totalLEDsPerStrip[stripIndex];
                foreach (var comet in stripComets[stripIndex])
                {
                    if (comet.isActive)
                    {
                        comet.UpdateCache(totalLEDs, tailIntensity);
                    }
                }
            }
        }
    }
}