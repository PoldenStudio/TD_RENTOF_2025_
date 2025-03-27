using UnityEngine;
using System.IO;

namespace LEDControl
{
    [System.Serializable]
    public class LEDStrip
    {
        [Tooltip("Имя светодиодной ленты")]
        public string name = "LED Strip";

        [Tooltip("Общее количестводиодов")]
        public int totalLEDs = 50;


        [Tooltip("Количество диодов в сегменте")]
        public int ledsPerSegment = 1;

        [Tooltip("Смещение DMX-канала")]
        public int dmxChannelOffset = 1;

        [Tooltip("Глобальный цвет")]
        public Color globalColor = Color.white;

        [Tooltip("Массив цветов сегментов")]
        public Color[] segmentColors;

        [Tooltip("Массив активности сегментов")]
        public bool[] segmentActiveStates;

        [Tooltip("Путь к JSON файлу для активного режима (относительно StreamingAssets)")]
        public string normalJsonPath = "";

        [Tooltip("Путь к JSON файлу для idle режима (относительно StreamingAssets)")]
        public string idleJsonPath = "";

        [Tooltip("Предпочтительный формат JSON")]
        public ColorFormat jsonFormat = ColorFormat.RGB;

        [HideInInspector]
        public string fullNormalJsonPath;
        [HideInInspector]
        public string fullIdleJsonPath;

        // Загруженные данные JSON (active и idle)
        [HideInInspector]
        public LEDDataFrame[] normalJsonData;
        [HideInInspector]
        public LEDDataFrame[] idleJsonData;

        // Предрассчитанные DMX кадры (active и idle)
        [HideInInspector]
        public PreCalculatedDmxFrame[] preCalculatedNormalFrames;
        [HideInInspector]
        public PreCalculatedDmxFrame[] preCalculatedIdleFrames;

        // Счётчики кадров для каждого режима
        [HideInInspector]
        public float currentFrameActive = 0;
        [HideInInspector]
        public float currentFrameIdle = 0;

        public int TotalSegments { get { return totalLEDs / ledsPerSegment; } }

        public LEDStrip()
        {
            InitializeSegmentArrays();
        }

        public int GetTotalChannels()
        {
            int channelsPerLed = GetChannelsPerLed(this);
            return totalLEDs * channelsPerLed;
        }

        private int GetChannelsPerLed(LEDStrip strip)
        {
            return strip.jsonFormat switch
            {
                ColorFormat.RGB => 3,
                ColorFormat.RGBW => 4,
                ColorFormat.HSV => 3,
                ColorFormat.RGBWMix => 5,
                _ => 3,
            };
        }

        public void InitializeSegmentArrays()
        {
            int segments = TotalSegments;
            if (segmentColors == null || segmentColors.Length != segments)
            {
                segmentColors = new Color[segments];
                for (int i = 0; i < segments; i++)
                    segmentColors[i] = Color.black;
            }
            if (segmentActiveStates == null || segmentActiveStates.Length != segments)
            {
                segmentActiveStates = new bool[segments];
                for (int i = 0; i < segments; i++)
                    segmentActiveStates[i] = true;
            }
        }

        /// <summary>
        /// Загружает JSON данные для активного и idle режимов.
        /// </summary>
        /// <param name="loadBothModes">Если true – загружаются оба режима.</param>
        public void LoadJsonData(bool loadBothModes = false)
        {
            if (!string.IsNullOrEmpty(normalJsonPath))
                fullNormalJsonPath = Path.Combine(Application.streamingAssetsPath, normalJsonPath);
            if (!string.IsNullOrEmpty(idleJsonPath))
                fullIdleJsonPath = Path.Combine(Application.streamingAssetsPath, idleJsonPath);

            // Всегда загружаем активный режим
            normalJsonData = JsonFrameLoader.LoadJsonFrames(fullNormalJsonPath, jsonFormat);

            // Если требуется, загружаем idle режим
            if (loadBothModes)
            {
                idleJsonData = JsonFrameLoader.LoadJsonFrames(fullIdleJsonPath, jsonFormat);
            }

            int tot = totalLEDs;

            if (normalJsonData != null)
            {
                preCalculatedNormalFrames = new PreCalculatedDmxFrame[normalJsonData.Length];
                for (int i = 0; i < normalJsonData.Length; i++)
                {
                    preCalculatedNormalFrames[i] = DmxFrameCalculator.CalculateDmxFrame(normalJsonData[i], dmxChannelOffset, tot, jsonFormat);
                }
            }

            if (loadBothModes && idleJsonData != null)
            {
                preCalculatedIdleFrames = new PreCalculatedDmxFrame[idleJsonData.Length];
                for (int i = 0; i < idleJsonData.Length; i++)
                {
                    preCalculatedIdleFrames[i] = DmxFrameCalculator.CalculateDmxFrame(idleJsonData[i], dmxChannelOffset, tot, jsonFormat);
                }
            }
        }

        public void ResetFrames()
        {
            currentFrameActive = 0;
            currentFrameIdle = 0;
        }
    }
}