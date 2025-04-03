#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using LEDControl;

namespace LEDControl
{
    public class SunDataPrebakerTool : EditorWindow
    {
        private EffectsManager effectsManager;
        private StripDataManager stripDataManager;
        private SunDataCache sunDataCache;

        [MenuItem("Tools/Sun Data Prebaker")]
        public static void ShowWindow()
        {
            GetWindow<SunDataPrebakerTool>("Sun Data Prebaker");
        }

        private void OnGUI()
        {
            GUILayout.Label("Sun Data Prebaker Tool", EditorStyles.boldLabel);

            effectsManager = (EffectsManager)EditorGUILayout.ObjectField("Effects Manager", effectsManager, typeof(EffectsManager), true);
            stripDataManager = (StripDataManager)EditorGUILayout.ObjectField("Strip Data Manager", stripDataManager, typeof(StripDataManager), true);
            sunDataCache = (SunDataCache)EditorGUILayout.ObjectField("Sun Data Cache", sunDataCache, typeof(SunDataCache), false);

            if (GUILayout.Button("Create New Sun Data Cache"))
            {
                sunDataCache = CreateInstance<SunDataCache>();
                string path = EditorUtility.SaveFilePanelInProject("Save Sun Data Cache", "SunDataCache", "asset", "Введите имя файла для сохранения кэша");
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(sunDataCache, path);
                    AssetDatabase.SaveAssets();
                }
            }

            if (effectsManager == null || stripDataManager == null || sunDataCache == null)
            {
                EditorGUILayout.HelpBox("Пожалуйста, назначьте Effects Manager, Strip Data Manager и Sun Data Cache.", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("Prebake Sun Data"))
            {
                PrebakeAllSunData();
            }
        }

        private void PrebakeAllSunData()
        {
            sunDataCache.preBakedSunData.Clear();

            for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (stripDataManager.currentDisplayModes[stripIndex] != DisplayMode.SunMovement)
                    continue;

                PreBakedSunDataForStrip dataForStrip = new PreBakedSunDataForStrip();
                dataForStrip.stripIndex = stripIndex;

                // Предзапекаем для Idle
                PreBakedSunDataEntry idleEntry = PrebakeSunDataForStripState(stripIndex, StateManager.AppState.Idle);
                if (idleEntry != null)
                    dataForStrip.entries.Add(idleEntry);

                // Предзапекаем для Active
                PreBakedSunDataEntry activeEntry = PrebakeSunDataForStripState(stripIndex, StateManager.AppState.Active);
                if (activeEntry != null)
                    dataForStrip.entries.Add(activeEntry);

                sunDataCache.preBakedSunData.Add(dataForStrip);
            }

            EditorUtility.SetDirty(sunDataCache);
            AssetDatabase.SaveAssets();
            Debug.Log("Предзапекание Sun Data завершено и сохранено в кэше.");
        }

        private PreBakedSunDataEntry PrebakeSunDataForStripState(int stripIndex, StateManager.AppState state)
        {
            // Получаем настройки движения солнца в зависимости от состояния и sunMode
            SunMode sunMode = stripDataManager.GetSunMode(stripIndex);
            SunMovementSettings settings = GetSunSettingsForState(state, sunMode);
            if (settings == null || settings.baseCycleLength <= 0 || settings.activeIntervals == null || settings.activeIntervals.Count == 0)
            {
                Debug.LogWarning($"Нет корректных SunMovementSettings для канала {stripIndex} состояние {state}");
                return null;
            }

            int totalLEDs = stripDataManager.totalLEDsPerStrip[stripIndex];
            LEDControl.DataMode dataMode = stripDataManager.currentDataModes[stripIndex]; // Полностью квалифицированный тип
            int hexPerPixel = (dataMode == LEDControl.DataMode.RGBW ? 8 : dataMode == LEDControl.DataMode.RGB ? 6 : 2);

            int frameCount = Mathf.CeilToInt(settings.baseCycleLength * 120f);
            if (frameCount <= 0) frameCount = 1;
            float frameDuration = settings.baseCycleLength / frameCount;

            // Подготавливаем буфер для пикселей и словарь для хранения HEX-строк каждого кадра
            Color32[] pixelColors = new Color32[totalLEDs];
            Dictionary<int, string> bakedData = new Dictionary<int, string>();

            // Предварительно вычисляем базовый цвет для солнца
            Color32 sunColorBase = GetSunColorBase(sunMode);

            for (int frame = 0; frame < frameCount; frame++)
            {
                float currentTime = frame * frameDuration;
                // Обнуляем весь массив – чёрный цвет
                for (int i = 0; i < totalLEDs; i++)
                {
                    pixelColors[i] = new Color32(0, 0, 0, 255);
                }

                // Перебираем все активные интервалы
                foreach (var interval in settings.activeIntervals)
                {
                    if (currentTime >= interval.startTime && currentTime <= interval.endTime)
                    {
                        float intervalDuration = interval.endTime - interval.startTime;
                        if (intervalDuration > 0)
                        {
                            float activePhase = (currentTime - interval.startTime) / intervalDuration;
                            float sunPosition = (1f - activePhase) * totalLEDs;

                            int centerPixel = Mathf.RoundToInt(sunPosition);
                            int halfPixels = settings.pixelCount / 2;

                            for (int i = 0; i < settings.pixelCount; i++)
                            {
                                int pixelIndex = centerPixel - halfPixels + i;
                                float distance = Mathf.Abs(pixelIndex - sunPosition);
                                float brightnessFactor = Mathf.Clamp01(1f - distance / (float)halfPixels) * settings.brightnessMultiplier;
                                if (brightnessFactor > 0)
                                {
                                    int wrappedIndex = ((pixelIndex % totalLEDs) + totalLEDs) % totalLEDs;
                                    byte r = (byte)Mathf.Clamp(sunColorBase.r * brightnessFactor, 0, 255);
                                    byte g = (byte)Mathf.Clamp(sunColorBase.g * brightnessFactor, 0, 255);
                                    byte b = (byte)Mathf.Clamp(sunColorBase.b * brightnessFactor, 0, 255);
                                    pixelColors[wrappedIndex] = new Color32(r, g, b, 255);
                                }
                            }
                        }
                    }
                }

                // Генерируем HEX-строку для данного кадра
                string frameHex = GenerateHexString(pixelColors, dataMode, hexPerPixel);
                // Оптимизация: удаляем концевые "нули"
                frameHex = OptimizeHexString(frameHex, new string('0', hexPerPixel), hexPerPixel);
                bakedData[frame] = string.IsNullOrEmpty(frameHex) ? "" : frameHex;
            }

            // Объединяем все кадры в одну строку
            StringBuilder combined = new StringBuilder();
            for (int frame = 0; frame < frameCount; frame++)
            {
                combined.Append(bakedData[frame]);
            }
            string combinedHex = combined.ToString();
            if (string.IsNullOrEmpty(combinedHex))
                return null;

            PreBakedSunDataEntry entry = new PreBakedSunDataEntry
            {
                state = (int)state,
                baseCycleLength = settings.baseCycleLength,
                frameCount = frameCount,
                frameDuration = frameDuration,
                hexData = combinedHex
            };

            return entry;
        }

        private SunMovementSettings GetSunSettingsForState(StateManager.AppState state, SunMode sunMode)
        {
            // Настройки берём из EffectsManager
            if (state == StateManager.AppState.Idle)
            {
                return sunMode == SunMode.Warm ? effectsManager.warmSunSettingsIdle : effectsManager.coldSunSettingsIdle;
            }
            else
            {
                return sunMode == SunMode.Warm ? effectsManager.warmSunSettingsActive : effectsManager.coldSunSettingsActive;
            }
        }

        private Color32 GetSunColorBase(SunMode sunMode)
        {
            return sunMode == SunMode.Warm
                ? new Color32(255, 180, 100, 255)
                : new Color32(200, 200, 255, 255);
        }

        private string GenerateHexString(Color32[] pixelColors, LEDControl.DataMode mode, int hexPerPixel)
        {
            StringBuilder sb = new StringBuilder(pixelColors.Length * hexPerPixel);
            foreach (Color32 color in pixelColors)
            {
                string hexColor = mode switch
                {
                    LEDControl.DataMode.RGBW => ColorToHexRGBW(color),
                    LEDControl.DataMode.RGB => ColorToHexRGB(color),
                    _ => ColorToHexMonochrome(color)
                };
                sb.Append(hexColor);
            }
            return sb.ToString();
        }

        private string ColorToHexRGBW(Color32 color)
        {
            return $"{color.r:X2}{color.g:X2}{color.b:X2}{color.a:X2}";
        }

        private string ColorToHexRGB(Color32 color)
        {
            return $"{color.r:X2}{color.g:X2}{color.b:X2}";
        }

        private string ColorToHexMonochrome(Color32 color)
        {
            byte mono = (byte)((color.r * 0.299f + color.g * 0.587f + color.b * 0.114f));
            return $"{mono:X2}";
        }

        private string OptimizeHexString(string hexString, string blackHex, int hexPerPixel)
        {
            int totalPixels = hexString.Length / hexPerPixel;
            if (totalPixels == 0) return "";
            int lastNonBlack = -1;
            for (int i = totalPixels - 1; i >= 0; i--)
            {
                string pixelHex = hexString.Substring(i * hexPerPixel, hexPerPixel);
                if (!pixelHex.Equals(blackHex, StringComparison.OrdinalIgnoreCase))
                {
                    lastNonBlack = i;
                    break;
                }
            }
            if (lastNonBlack == -1) return "";
            return hexString.Substring(0, (lastNonBlack + 1) * hexPerPixel);
        }
    }
}
#endif