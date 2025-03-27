using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LEDControl
{
    [Serializable]
    public class StripSegmentColors
    {
        [Tooltip("Цвета сегментов для данной ленты")]
        public List<Color32> segments = new();
    }

    public class StripDataManager : MonoBehaviour
    {
        [Header("General Settings")]
        [Tooltip("Общее количество диодов на ленте для каждой ленты")]
        public List<int> totalLEDsPerStrip = new() { 200, 200, 200, 200 };

        [Tooltip("Количество диодов в одном сегменте. Обычно оставляйте 1.")]
        public int ledsPerSegment = 1;

        [Header("Data Mode")]
        [Tooltip("Включить/выключить ленту по индексу")]
        public List<bool> stripEnabled = new() { true, true, true, true };

        [Tooltip("Режим передачи данных для каждой ленты")]
        public List<DataMode> currentDataModes = new() { DataMode.Monochrome1Color, DataMode.RGBW, DataMode.RGB, DataMode.Monochrome1Color };

        [Header("Display Settings")]
        [Tooltip("Режим отображения цвета для каждой ленты")]
        public List<DisplayMode> currentDisplayModes = new() { DisplayMode.GlobalColor, DisplayMode.GlobalColor, DisplayMode.GlobalColor, DisplayMode.GlobalColor };

        [Header("Monochrome Settings")]
        [Tooltip("Глобальные настройки для монохромных лент")]
        public List<MonochromeStripSettings> monochromeStripSettings = new()
        {
            new MonochromeStripSettings() { globalColor = new Color32(255, 255, 255, 255), synthColor = new Color32(255, 255, 255, 255) },
            new MonochromeStripSettings() { globalColor = new Color32(255, 255, 255, 255), synthColor = new Color32(255, 255, 255, 255) }
        };

        [Header("RGBW/RGB Settings")]
        [Tooltip("Глобальные настройки для RGBW/RGB лент")]
        public List<RGBStripSettings> rgbStripSettings = new()
        {
            new RGBStripSettings() { globalColor = new Color32(255, 0, 0, 255), synthColor = new Color32(255, 255, 255, 255) },
            new RGBStripSettings() { globalColor = new Color32(0, 255, 0, 255), synthColor = new Color32(255, 255, 255, 255) }
        };

        [Header("Segment Mode Settings")]
        [Tooltip("Настройки цвета сегментов для каждой ленты. Количество сегментов = totalLEDsPerStrip / ledsPerSegment")]
        public List<StripSegmentColors> stripSegmentColors = new();

        // Кэш предыдущих значений для определения изменений
        private List<MonochromeStripSettings> previousMonochromeStripSettings = new();
        private List<RGBStripSettings> previousRGBStripSettings = new();
        private List<List<Color32>> previousSegmentColors = new();
        private List<DisplayMode> previousDisplayModes = new();
        private List<DataMode> previousDataModes = new();
        private List<bool> previousStripEnabled = new();

        public int GetTotalSegments(int stripIndex) => totalLEDsPerStrip[stripIndex] / ledsPerSegment;

        public void InitializeStripData()
        {
            // Обеспечиваем синхронизацию числа лент
            if (stripSegmentColors.Count != totalLEDsPerStrip.Count)
            {
                stripSegmentColors.Clear();
                for (int i = 0; i < totalLEDsPerStrip.Count; i++)
                {
                    stripSegmentColors.Add(new StripSegmentColors());
                }
            }

            // Для каждой ленты устанавливаем число сегментов равным totalLEDsPerStrip / ledsPerSegment
            // И по умолчанию каждый сегмент – черный (Color.black)
            for (int stripIndex = 0; stripIndex < totalLEDsPerStrip.Count; stripIndex++)
            {
                int requiredSegmentCount = GetTotalSegments(stripIndex);
                if (stripSegmentColors[stripIndex].segments == null || stripSegmentColors[stripIndex].segments.Count != requiredSegmentCount)
                {
                    List<Color32> newSegmentColors = new List<Color32>();
                    for (int j = 0; j < requiredSegmentCount; j++)
                    {
                        newSegmentColors.Add(Color.black);
                    }
                    stripSegmentColors[stripIndex].segments = newSegmentColors;
                }
            }

            // Кэширование текущих значений для проверки изменений
            previousSegmentColors.Clear();
            for (int stripIndex = 0; stripIndex < stripSegmentColors.Count; stripIndex++)
            {
                previousSegmentColors.Add(new List<Color32>(stripSegmentColors[stripIndex].segments));
            }
        }

        public Color32 GetDefaultColor(int stripIndex)
        {
            switch (currentDataModes[stripIndex])
            {
                case DataMode.Monochrome1Color:
                case DataMode.Monochrome2Color:
                    return monochromeStripSettings[stripIndex < monochromeStripSettings.Count ? stripIndex : monochromeStripSettings.Count - 1].globalColor;
                case DataMode.RGBW:
                case DataMode.RGB:
                    int monochromeCount = currentDataModes.Count(x => x == DataMode.Monochrome1Color || x == DataMode.Monochrome2Color);
                    int rgbIndex = stripIndex - monochromeCount;
                    if (rgbIndex < 0 || rgbIndex >= rgbStripSettings.Count)
                    {
                        Debug.LogError($"[StripDataManager] Invalid RGB strip index: {rgbIndex}");
                        return Color.black;
                    }
                    return rgbStripSettings[rgbIndex].globalColor;
                default:
                    return Color.black;
            }
        }
        public void SetSegmentColor(int stripIndex, int segmentIndex, Color32 color, bool debug = false)
        {
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return;
            }
            if (segmentIndex >= 0 && segmentIndex < GetTotalSegments(stripIndex))
            {
                stripSegmentColors[stripIndex].segments[segmentIndex] = color;
                if (debug)
                    Debug.Log($"Strip {stripIndex}, Segment {segmentIndex} color set to {color}");
            }
            else
            {
                Debug.LogError($"[StripDataManager] Invalid segment index: {segmentIndex} for strip {stripIndex}");
            }
        }
        public Color32 GetSegmentColor(int stripIndex, int segmentIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return GetDefaultColor(stripIndex);
            }
            if (segmentIndex >= 0 && segmentIndex < GetTotalSegments(stripIndex))
            {
                return stripSegmentColors[stripIndex].segments[segmentIndex];
            }
            else
            {
                Debug.LogError($"[StripDataManager] Invalid segment index: {segmentIndex} for strip {stripIndex}");
                return GetDefaultColor(stripIndex);
            }
        }

        public List<Color32> GetSegmentColors(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return new List<Color32>();
            }
            return new List<Color32>(stripSegmentColors[stripIndex].segments);
        }

        public void SetGlobalColorMonochrome(int stripIndex, Color32 color, bool debug = false)
        {
            if (stripIndex < 0 || stripIndex >= monochromeStripSettings.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return;
            }
            monochromeStripSettings[stripIndex].globalColor = color;
            if (debug) Debug.Log($"Global color (Monochrome strip {stripIndex}) set to {color}");
        }

        public Color32 GetGlobalColorMonochrome(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= monochromeStripSettings.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return Color.black;
            }
            return monochromeStripSettings[stripIndex].globalColor;
        }

        public void SetGlobalColorRGB(int stripIndex, Color32 color, bool debug = false)
        {
            if (stripIndex < 0 || stripIndex >= totalLEDsPerStrip.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return;
            }
            int monochromeCount = currentDataModes.Count(x => x == DataMode.Monochrome1Color || x == DataMode.Monochrome2Color);
            int rgbIndex = stripIndex - monochromeCount;
            if (rgbIndex < 0 || rgbIndex >= rgbStripSettings.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid RGB strip index: {rgbIndex}");
                return;
            }
            rgbStripSettings[rgbIndex].globalColor = color;
            if (debug) Debug.Log($"Global color (RGB/RGBW strip {stripIndex}) set to {color}");
        }

        public Color32 GetGlobalColorRGB(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= totalLEDsPerStrip.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return Color.black;
            }
            int monochromeCount = currentDataModes.Count(x => x == DataMode.Monochrome1Color || x == DataMode.Monochrome2Color);
            int rgbIndex = stripIndex - monochromeCount;
            if (rgbIndex < 0 || rgbIndex >= rgbStripSettings.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid RGB strip index: {rgbIndex}");
                return Color.black;
            }
            return rgbStripSettings[rgbIndex].globalColor;
        }
        public Color32 GetGlobalColorForStrip(int stripIndex, DataMode mode)
        {
            if (mode == DataMode.Monochrome1Color || mode == DataMode.Monochrome2Color)
            {
                return monochromeStripSettings[stripIndex < monochromeStripSettings.Count ? stripIndex : monochromeStripSettings.Count - 1].globalColor;
            }
            else if (mode == DataMode.RGBW || mode == DataMode.RGB)
            {
                int monochromeCount = currentDataModes.Count(x => x == DataMode.Monochrome1Color || x == DataMode.Monochrome2Color);
                int rgbIndex = stripIndex - monochromeCount;
                if (rgbIndex < 0 || rgbIndex >= rgbStripSettings.Count)
                {
                    Debug.LogError($"[StripDataManager] Invalid RGB strip index: {rgbIndex}");
                    return Color.black;
                }
                return rgbStripSettings[rgbIndex].globalColor;
            }
            return Color.black;
        }

        public void SetDisplayMode(int stripIndex, int modeIndex, bool debug = false)
        {
            if (stripIndex < 0 || stripIndex >= totalLEDsPerStrip.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return;
            }
            if (Enum.IsDefined(typeof(DisplayMode), modeIndex))
            {
                currentDisplayModes[stripIndex] = (DisplayMode)modeIndex;
                InitializeStripData(); // Обновляем цвета сегментов
                if (debug)
                    Debug.Log($"Strip {stripIndex} display mode switched to {currentDisplayModes[stripIndex]}");
            }
            else
            {
                Debug.LogError($"[StripDataManager] Invalid Display Mode Index: {modeIndex}");
            }
        }

        public void SetDataMode(int stripIndex, int modeIndex, bool debug = false)
        {
            if (stripIndex < 0 || stripIndex >= totalLEDsPerStrip.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return;
            }
            if (Enum.IsDefined(typeof(DataMode), modeIndex))
            {
                currentDataModes[stripIndex] = (DataMode)modeIndex;
                InitializeStripData(); // Обновляем цвета сегментов
                if (debug)
                    Debug.Log($"Strip {stripIndex} data mode switched to {currentDataModes[stripIndex]}");
            }
            else
            {
                Debug.LogError($"[StripDataManager] Invalid Data Mode Index: {modeIndex}");
            }
        }

        public void SetStripEnabled(int stripIndex, bool enabled, bool debug = false)
        {
            if (stripIndex < 0 || stripIndex >= totalLEDsPerStrip.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return;
            }
            stripEnabled[stripIndex] = enabled;
            if (debug)
                Debug.Log($"Strip {stripIndex} enabled set to {enabled}");
        }

        public void CachePreviousValues()
        {
            previousMonochromeStripSettings.Clear();
            foreach (var settings in monochromeStripSettings)
            {
                previousMonochromeStripSettings.Add(new MonochromeStripSettings()
                {
                    globalColor = settings.globalColor,
                    synthColor = settings.synthColor
                });
            }
            previousRGBStripSettings.Clear();
            foreach (var settings in rgbStripSettings)
            {
                previousRGBStripSettings.Add(new RGBStripSettings()
                {
                    globalColor = settings.globalColor,
                    synthColor = settings.synthColor
                });
            }
            previousSegmentColors.Clear();
            for (int stripIndex = 0; stripIndex < stripSegmentColors.Count; stripIndex++)
            {
                previousSegmentColors.Add(new List<Color32>(stripSegmentColors[stripIndex].segments));
            }
            previousDisplayModes = new List<DisplayMode>(currentDisplayModes);
            previousDataModes = new List<DataMode>(currentDataModes);
            previousStripEnabled = new List<bool>(stripEnabled);
        }

        public bool CheckForChanges()
        {
            bool colorsChanged = false;
            if (stripSegmentColors.Count != previousSegmentColors.Count ||
                monochromeStripSettings.Count != previousMonochromeStripSettings.Count ||
                rgbStripSettings.Count != previousRGBStripSettings.Count ||
                currentDisplayModes.Count != previousDisplayModes.Count ||
                currentDataModes.Count != previousDataModes.Count ||
                stripEnabled.Count != previousStripEnabled.Count)
            {
                return true;
            }
            for (int stripIndex = 0; stripIndex < stripSegmentColors.Count; stripIndex++)
            {
                if (stripSegmentColors[stripIndex].segments.Count != previousSegmentColors[stripIndex].Count)
                {
                    colorsChanged = true;
                    break;
                }
                for (int i = 0; i < stripSegmentColors[stripIndex].segments.Count; ++i)
                {
                    if (stripSegmentColors[stripIndex].segments[i].IsDifferent(previousSegmentColors[stripIndex][i]))
                    {
                        colorsChanged = true;
                        break;
                    }
                }
                if (colorsChanged)
                    break;
            }
            for (int i = 0; i < monochromeStripSettings.Count; i++)
            {
                if (monochromeStripSettings[i].globalColor.IsDifferent(previousMonochromeStripSettings[i].globalColor) ||
                    monochromeStripSettings[i].synthColor.IsDifferent(previousMonochromeStripSettings[i].synthColor))
                {
                    colorsChanged = true;
                    break;
                }
            }
            for (int i = 0; i < rgbStripSettings.Count; i++)
            {
                if (rgbStripSettings[i].globalColor.IsDifferent(previousRGBStripSettings[i].globalColor) ||
                    rgbStripSettings[i].synthColor.IsDifferent(previousRGBStripSettings[i].synthColor))
                {
                    colorsChanged = true;
                    break;
                }
            }
            if (!currentDisplayModes.SequenceEqual(previousDisplayModes) ||
                !currentDataModes.SequenceEqual(previousDataModes) ||
                !stripEnabled.SequenceEqual(previousStripEnabled))
            {
                colorsChanged = true;
            }
            return colorsChanged;
        }

        /// <summary>
        /// Метод возвращает цвет для режима Speed Synth.
        /// Поле synthColor отвечает за цвет эффекта в режиме Speed Synth:
        /// – Для монохромных лент берется из настроек (monochromeStripSettings.synthColor),
        ///   что позволяет задавать индивидуальный "синтезированный" цвет для движущегося эффекта.
        /// – Для RGB/RGBW лент по умолчанию возвращается белый цвет.
        /// </summary>
        public Color32 GetSynthColorForStrip(int stripIndex)
        {
            switch (currentDataModes[stripIndex])
            {
                case DataMode.Monochrome1Color:
                case DataMode.Monochrome2Color:
                    return monochromeStripSettings[stripIndex < monochromeStripSettings.Count ? stripIndex : monochromeStripSettings.Count - 1].synthColor;
                case DataMode.RGBW:
                case DataMode.RGB:
                    return new Color32(255, 255, 255, 255);
                default:
                    return Color.white;
            }
        }

        public Color32 GetGlobalColorForStrip_OLD(int stripIndex, DataMode mode)
        {
            // Старый вариант (возвращал для цветных лент только черный)
            if (mode == DataMode.Monochrome1Color || mode == DataMode.Monochrome2Color)
            {
                return monochromeStripSettings[stripIndex < monochromeStripSettings.Count ? stripIndex : monochromeStripSettings.Count - 1].globalColor;
            }
            else if (mode == DataMode.RGBW || mode == DataMode.RGB)
            {
                return new Color32(0, 0, 0, 255);
            }
            return Color.black;
        }
    }
}