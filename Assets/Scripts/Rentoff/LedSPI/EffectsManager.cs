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
        public int synthLedCountBase = 5;
        public float speedLedCountFactor = 0.5f;
        public float speedBrightnessFactor = 1.5f;
        [Range(0f, 1f)] public float tailIntensity = 0.7f;
        public MoveDirection moveDirection = MoveDirection.Forward;
        public bool toggleTouchMode = false;
        public bool startFromEnd = false;
        [Range(0f, 1f)] public float stationaryBrightnessFactor = 0.5f;
        public float currentSpeedRaw = 1f;
        public float MultiplySpeed = 1f;

        public float CurrentCometSpeed => currentSpeedRaw * MultiplySpeed;

        public DataSender dataSender;

        private Dictionary<int, Color32[]> pixelCache = new();
        private Dictionary<int, string> hexCache = new();
        private Dictionary<int, float> lastUpdateTime = new();
        private float cacheLifetime = 0.05f;

        private Dictionary<int, StringBuilder> stringBuilderCache = new();


        private Dictionary<int, List<Comet>> stripComets = new();
        private Dictionary<int, float> lastTouchTimes = new();

        [SerializeField] private StripDataManager stripDataManager;
        [SerializeField] private ColorProcessor colorProcessor;

        public void UpdateSpeed(float speed)
        {
            currentSpeedRaw = speed;
            ClearCaches();
        }

        private void ClearCaches()
        {
            //реализовать
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
            float timeSinceLastUpdate = Time.fixedDeltaTime;

            float lastTouchTime = lastTouchTimes.TryGetValue(stripIndex, out float value) ? value : 0f;
            bool canMove = Time.time - lastTouchTime >= 0.2f;

            bool anyActive = false;
            for (int i = comets.Count - 1; i >= 0; i--)
            {
                Comet comet = comets[i];
                if (!comet.isActive) continue;
                anyActive = true;

                if (canMove && !comet.isMoving)
                {
                    comet.isMoving = true;
                }

                if (comet.isMoving)
                {
                    float speed = CurrentCometSpeed;
                    if (startFromEnd)
                    {
                        comet.direction = speed >= 0 ? -1f : 1f;
                    }
                    else
                    {
                        comet.direction = speed >= 0 ? 1f : -1f;
                    }

                    float directionMultiplier = comet.direction;
                    float oldPosition = comet.position;
                    comet.position += Mathf.Abs(speed) * timeSinceLastUpdate * 30f * directionMultiplier;

                    comet.position = Mathf.Repeat(comet.position, totalLEDs);

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

            float startPos = position;
            if (startFromEnd)
            {
                startPos = totalLeds - 1;
                moveDirection = MoveDirection.Backward;
            }

            float direction = CurrentCometSpeed >= 0 ? 1f : -1f;
            if (startFromEnd) direction *= -1;

            Comet newComet = new Comet(startPos, color, length, brightness, direction);
            comets.Add(newComet);
            lastTouchTimes[stripIndex] = Time.time;

            pixelCache.Remove(stripIndex);
            hexCache.Remove(stripIndex);
            lastUpdateTime.Remove(stripIndex);

            newComet.UpdateCache(totalLeds, tailIntensity);
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
                float speedBrightnessMultiplier = 1f + Mathf.Abs(CurrentCometSpeed) * speedBrightnessFactor;
                dynamicBrightness *= speedBrightnessMultiplier;

                if (!comet.isMoving)
                {
                    dynamicBrightness *= stationaryBrightnessFactor;
                }

                dynamicBrightness = Mathf.Clamp01(dynamicBrightness * stripBrightness);

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

            string result = GenerateOptimizedHexString(pixelColors, mode, colorProcessor, stripIndex);
            hexCache[stripIndex] = result;
            lastUpdateTime[stripIndex] = Time.time;
            return result;
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