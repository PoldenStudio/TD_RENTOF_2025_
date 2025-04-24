using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LEDControl
{
    public class EffectsManager : MonoBehaviour
    {
        [Header("Speed Synth Mode Settings")]
        [Tooltip("Base length of the comet in LEDs")]
        public int synthLedCountBase = 5;
        [Tooltip("How much speed affects the comet length")]
        public float speedLedCountFactor = 0.5f;
        [Tooltip("How much speed affects brightness")]
        public float speedBrightnessFactor = 1.5f;
        [Range(0f, 1f)]
        [Tooltip("Intensity of the comet tail")]
        public float tailIntensity = 0.7f;
        [Tooltip("Default direction of movement")]
        public MoveDirection moveDirection = MoveDirection.Forward;
        [Tooltip("Whether to toggle touch mode")]
        public bool toggleTouchMode = false;
        [Tooltip("Whether comets start from the end of the strip")]
        public bool startFromEnd = false;
        [Range(0f, 1f)]
        [Tooltip("Brightness factor when comet is stationary")]
        public float stationaryBrightnessFactor = 0.5f;
        [Tooltip("Raw speed value")]
        public float currentSpeedRaw = 1f;
        [Tooltip("Speed multiplier")]
        public float MultiplySpeed = 1f;
        [Tooltip("Base movement speed")]
        public float baseMovementSpeed = 10f;

        public float CurrentCometSpeed => currentSpeedRaw * MultiplySpeed;

        public DataSender dataSender;

        private Dictionary<int, Color32[]> pixelCache = new();
        private Dictionary<int, string> hexCache = new();
        private Dictionary<int, float> lastUpdateTime = new();
        private float cacheLifetime = 0.02f; // Уменьшили время жизни кэша

        private Dictionary<int, StringBuilder> stringBuilderCache = new();
        private Dictionary<int, List<Comet>> stripComets = new();
        private Dictionary<int, float> lastTouchTimes = new();

        [SerializeField] private StripDataManager stripDataManager;
        [SerializeField] private ColorProcessor colorProcessor;

        private void Awake()
        {
            ClearCaches();
        }

        public void UpdateSpeed(float speed)
        {
            if (Mathf.Abs(currentSpeedRaw - speed) > 0.01f)
            {
                currentSpeedRaw = speed;
                ClearCaches();
            }
        }

        private void ClearCaches()
        {
            pixelCache.Clear();
            hexCache.Clear();
            lastUpdateTime.Clear();
        }

        private void Update()
        {
            foreach (var stripIndex in stripComets.Keys)
            {
                if (stripDataManager != null && stripIndex < stripDataManager.totalLEDsPerStrip.Count)
                {
                    UpdateComets(stripIndex, stripDataManager);
                }
            }
        }

        public void UpdateComets(int stripIndex, StripDataManager stripManager)
        {
            if (!stripComets.TryGetValue(stripIndex, out List<Comet> comets))
            {
                comets = new List<Comet>();
                stripComets[stripIndex] = comets;
                return;
            }

            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            float deltaTime = Time.deltaTime;

            float lastTouchTime = lastTouchTimes.TryGetValue(stripIndex, out float value) ? value : 0f;
            bool canMove = Time.time - lastTouchTime >= 0.2f;

            bool anyActive = false;
            for (int i = comets.Count - 1; i >= 0; i--)
            {
                Comet comet = comets[i];
                if (!comet.isActive) continue;
                anyActive = true;

                comet.UpdateTailIntensity(tailIntensity);

                if (canMove && !comet.isMoving)
                {
                    comet.isMoving = true;
                }

                if (comet.isMoving)
                {
                    float speed = Mathf.Abs(CurrentCometSpeed) * baseMovementSpeed;

                    if (startFromEnd)
                    {
                        comet.direction = CurrentCometSpeed >= 0 ? -1f : 1f;
                    }
                    else
                    {
                        comet.direction = CurrentCometSpeed >= 0 ? 1f : -1f;
                    }

                    comet.UpdatePosition(deltaTime, speed, totalLEDs);
                }
            }

            if (!anyActive && pixelCache.ContainsKey(stripIndex))
            {
                pixelCache.Remove(stripIndex);
                hexCache.Remove(stripIndex);
                lastUpdateTime.Remove(stripIndex);
            }
            else if (anyActive)
            {
                hexCache.Remove(stripIndex);
                lastUpdateTime.Remove(stripIndex);
            }
        }

        public void ResetComets(int stripIndex)
        {
            if (stripComets.TryGetValue(stripIndex, out List<Comet> comets))
            {
                comets.Clear();
            }
            lastTouchTimes.Remove(stripIndex);

            pixelCache.Remove(stripIndex);
            hexCache.Remove(stripIndex);
            lastUpdateTime.Remove(stripIndex);
        }

        public void AddComet(int stripIndex, float position, Color32 color, float length, float brightness)
        {
            if (!stripComets.TryGetValue(stripIndex, out List<Comet> comets))
            {
                comets = new List<Comet>();
                stripComets[stripIndex] = comets;
            }

            if (stripIndex < 0 || stripIndex >= stripDataManager.totalLEDsPerStrip.Count) return;
            int totalLeds = stripDataManager.totalLEDsPerStrip[stripIndex];

            float dynamicLength = synthLedCountBase + Mathf.Abs(CurrentCometSpeed) * speedLedCountFactor;

            float startPos = position;
            if (startFromEnd)
            {
                startPos = totalLeds - 1;
                moveDirection = MoveDirection.Backward;
            }

            float direction = CurrentCometSpeed >= 0 ? 1f : -1f;
            if (startFromEnd) direction *= -1;

            float baseBrightness = 1.0f; // Увеличили базовую яркость

            Comet newComet = new Comet(startPos, color, dynamicLength, baseBrightness, direction, totalLeds, tailIntensity);
            comets.Add(newComet);
            lastTouchTimes[stripIndex] = Time.time;

            pixelCache.Remove(stripIndex);
            hexCache.Remove(stripIndex);
            lastUpdateTime.Remove(stripIndex);
        }

        public void UpdateLastTouchTime(int stripIndex)
        {
            lastTouchTimes[stripIndex] = Time.time;
        }

        public bool CheckForChanges()
        {
            return true;
        }

        public string GetHexDataForSpeedSynthMode(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            if (hexCache.TryGetValue(stripIndex, out string cachedHex) &&
                lastUpdateTime.TryGetValue(stripIndex, out float lastUpdate) &&
                Time.time - lastUpdate < cacheLifetime)
            {
                return cachedHex;
            }

            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
            Color32 blackColor = new Color32(0, 0, 0, 255);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            if (!stripComets.TryGetValue(stripIndex, out List<Comet> comets) || comets.All(c => !c.isActive))
            {
                string emptyHex = OptimizeHexString("", new string('0', hexPerPixel), hexPerPixel);
                hexCache[stripIndex] = emptyHex;
                lastUpdateTime[stripIndex] = Time.time;
                return emptyHex;
            }

            Color32[] pixelColors = GetPixelBuffer(stripIndex, totalLEDs, blackColor);

            var activeComets = comets.Where(c => c.isActive).ToList();

            foreach (Comet comet in activeComets)
            {
                float dynamicBrightness = comet.brightness;

                float speedDifference = Mathf.Abs(CurrentCometSpeed);
                dynamicBrightness += speedDifference * 1.5f; // Увеличили коэффициент для speedDifference

                dynamicBrightness = Mathf.Clamp01(dynamicBrightness * stripBrightness);

                for (int ledIndex = 0; ledIndex < totalLEDs; ledIndex++)
                {
                    Color32 cometColor = comet.GetColorAtLed(ledIndex, dynamicBrightness);

                    if (cometColor.r > 0 || cometColor.g > 0 || cometColor.b > 0)
                    {
                        Color32 currentColor = pixelColors[ledIndex];
                        pixelColors[ledIndex] = new Color32(
                            (byte)Mathf.Max(currentColor.r, cometColor.r),
                            (byte)Mathf.Max(currentColor.g, cometColor.g),
                            (byte)Mathf.Max(currentColor.b, cometColor.b),
                            255
                        );
                    }
                }
            }

            string result = GenerateOptimizedHexString(pixelColors, mode, colorProcessor, stripIndex);
            hexCache[stripIndex] = result;
            lastUpdateTime[stripIndex] = Time.time;
            return result;
        }

        private StringBuilder GetStringBuilderForStrip(int stripIndex)
        {
            if (!stringBuilderCache.ContainsKey(stripIndex))
            {
                stringBuilderCache[stripIndex] = new StringBuilder();
            }
            return stringBuilderCache[stripIndex];
        }

        private Color32[] GetPixelBuffer(int stripIndex, int totalLEDs, Color32 defaultColor)
        {
            if (!pixelCache.TryGetValue(stripIndex, out Color32[] pixelColors) || pixelColors.Length != totalLEDs)
            {
                pixelColors = new Color32[totalLEDs];
                pixelCache[stripIndex] = pixelColors;
            }

            for (int i = 0; i < totalLEDs; i++)
            {
                pixelColors[i] = defaultColor;
            }
            return pixelColors;
        }

        private StringBuilder GetStringBuilder(int capacity)
        {
            if (!stringBuilderCache.TryGetValue(capacity, out StringBuilder sb))
            {
                sb = new StringBuilder(capacity);
                stringBuilderCache[capacity] = sb;
            }
            else
            {
                sb.Clear();
            }
            return sb;
        }

        private string GenerateOptimizedHexString(Color32[] pixelColors, DataMode mode, ColorProcessor colorProcessor, int stripIndex)
        {
            int totalLEDs = pixelColors.Length;
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
            StringBuilder sb = GetStringBuilder(totalLEDs * hexPerPixel);

            float stripBrightness = stripDataManager.GetStripBrightness(stripIndex);
            float stripGamma = stripDataManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripDataManager.IsGammaCorrectionEnabled(stripIndex);

            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = pixelColors[i];
                string hexColor = mode switch
                {
                    DataMode.RGBW => colorProcessor.ColorToHexRGBW(pixelColor, pixelColor, pixelColor, stripBrightness, stripGamma, stripGammaEnabled),
                    DataMode.RGB => colorProcessor.ColorToHexRGB(pixelColor, pixelColor, pixelColor, stripBrightness, stripGamma, stripGammaEnabled),
                    _ => colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled),
                };
                sb.Append(hexColor);
            }
            return OptimizeHexString(sb.ToString(), new string('0', hexPerPixel), hexPerPixel);
        }

        public string OptimizeHexString(string hexString, string blackHex, int hexPerPixel)
        {
            if (string.IsNullOrEmpty(hexString)) return "";
            if (hexPerPixel <= 0) return hexString;
            int totalPixels = hexString.Length / hexPerPixel;
            if (totalPixels == 0) return "";

            int lastNonBlack = -1;
            for (int i = totalPixels - 1; i >= 0; i--)
            {
                int startIndex = i * hexPerPixel;
                if (startIndex + hexPerPixel <= hexString.Length &&
                    hexString.Substring(startIndex, hexPerPixel) != blackHex)
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