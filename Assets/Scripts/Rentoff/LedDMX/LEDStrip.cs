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

        public float brightness = 1.0f;

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

        [HideInInspector]
        public LEDDataFrame[] normalJsonData;
        [HideInInspector]
        public LEDDataFrame[] idleJsonData;

        [HideInInspector]
        public PreCalculatedDmxFrame[] preCalculatedNormalFrames;
        [HideInInspector]
        public PreCalculatedDmxFrame[] preCalculatedIdleFrames;

        [HideInInspector]
        public float currentFrameActive = 0;
        [HideInInspector]
        public float currentFrameIdle = 0;

        public int TotalSegments { get { return totalLEDs > 0 && ledsPerSegment > 0 ? totalLEDs / ledsPerSegment : 0; } }

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

        public void LoadJsonData(bool loadBothModes = false)
        {
            if (!string.IsNullOrEmpty(normalJsonPath))
                fullNormalJsonPath = Path.Combine(Application.streamingAssetsPath, normalJsonPath);
            else
                fullNormalJsonPath = string.Empty;

            if (!string.IsNullOrEmpty(idleJsonPath))
                fullIdleJsonPath = Path.Combine(Application.streamingAssetsPath, idleJsonPath);
            else
                fullIdleJsonPath = string.Empty;

            normalJsonData = JsonFrameLoader.LoadJsonFrames(fullNormalJsonPath, jsonFormat);

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
            else
            {
                preCalculatedNormalFrames = null;
            }

            if (loadBothModes && idleJsonData != null)
            {
                preCalculatedIdleFrames = new PreCalculatedDmxFrame[idleJsonData.Length];
                for (int i = 0; i < idleJsonData.Length; i++)
                {
                    preCalculatedIdleFrames[i] = DmxFrameCalculator.CalculateDmxFrame(idleJsonData[i], dmxChannelOffset, tot, jsonFormat);
                }
            }
            else if (loadBothModes)
            {
                preCalculatedIdleFrames = null;
            }
        }

        public void ResetFrames()
        {
            currentFrameActive = 0;
            currentFrameIdle = 0;
        }
    }
}