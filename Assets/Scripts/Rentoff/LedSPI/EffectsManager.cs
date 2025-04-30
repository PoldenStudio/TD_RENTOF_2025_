using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using LEDControl;
using System.Threading.Tasks;

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
        public float baseMovementSpeed = 10f;

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

        private void FixedUpdate()
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

            float baseBrightness = 0.8f;

            Comet newComet = new Comet(startPos, color, dynamicLength, baseBrightness, direction, totalLeds, tailIntensity);
            comets.Add(newComet);
            lastTouchTimes[stripIndex] = Time.time;

            pixelCache.Remove(stripIndex);
            hexCache.Remove(stripIndex);
            lastUpdateTime.Remove(stripIndex);

            Debug.LogFormat("AddComet: stripIndex={0}, position={1}, color=({2},{3},{4},{5}), length={6}, brightness={7}, direction={8}, totalLeds={9}, tailIntensity={10}",
                stripIndex, startPos, color.r, color.g, color.b, color.a, dynamicLength, baseBrightness, direction, totalLeds, tailIntensity);
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
                int hexLength = totalLEDs * hexPerPixel;
                string emptyHex = new string('0', hexLength);
                hexCache[stripIndex] = emptyHex;
                lastUpdateTime[stripIndex] = Time.time;
                return emptyHex;
            }

            Color32[] pixelColors = GetPixelBuffer(stripIndex, totalLEDs, blackColor);

            float speedDifference = Mathf.Abs(CurrentCometSpeed);
            float dynamicBrightnessBase = stripBrightness + speedDifference * 0.5f;

            // Кэш для dynamicBrightness
            Dictionary<float, float> dynamicBrightnessCache = new Dictionary<float, float>();

            Parallel.ForEach(comets, comet =>
            {
                if (!comet.isActive) return;

                float dynamicBrightness;
                if (!dynamicBrightnessCache.TryGetValue(comet.brightness, out dynamicBrightness))
                {
                    dynamicBrightness = Mathf.Clamp01(dynamicBrightnessBase * comet.brightness);
                    dynamicBrightnessCache[comet.brightness] = dynamicBrightness;
                }

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
            });

            string result = GenerateRawHexString(pixelColors, mode, colorProcessor, stripIndex);
            hexCache[stripIndex] = result;
            lastUpdateTime[stripIndex] = Time.time;
            return result;
        }

        private string GenerateRawHexString(Color32[] pixelColors, DataMode mode, ColorProcessor colorProcessor, int stripIndex)
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
            return sb.ToString();
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
    }
}