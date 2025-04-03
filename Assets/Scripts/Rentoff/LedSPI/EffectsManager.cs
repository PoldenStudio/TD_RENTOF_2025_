using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LEDControl
{
    public enum SunMode
    {
        Warm,
        Cold
    }

    [Serializable]
    public struct TimeInterval
    {
        public float startTime;
        public float endTime;
    }

    [Serializable]
    public class SunMovementSettings
    {
        public int pixelCount = 10;
        public float baseCycleLength = 241f;
        public List<TimeInterval> activeIntervals = new();
        public float brightnessMultiplier = 1f;
    }

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

        [Header("Sun Movement Settings")]
        public SunMovementSettings warmSunSettingsIdle = new();
        public SunMovementSettings coldSunSettingsIdle = new();
        public SunMovementSettings warmSunSettingsActive = new();
        public SunMovementSettings coldSunSettingsActive = new();

        [Header("Transition Settings")]
        public float sunFadeDuration = 1.0f;

        [Header("Synchronization Settings")]
        public DataSender dataSender;

        [Header("Pre-Bake Settings")]
        public float preBakeFrameRate = 35f;

        public float currentSpeedRaw = 1f;
        public float MultiplySpeed = 1f;
        private float CurrentSunSpeed => currentSpeedRaw;
        public float CurrentCometSpeed => currentSpeedRaw * MultiplySpeed;

        private float _sunMovementPhase = 0f;
        private Dictionary<int, List<Comet>> stripComets = new();
        private Dictionary<int, float> lastTouchTimes = new();

        private Dictionary<int, Color32[]> pixelCache = new();
        private Dictionary<int, string> hexCache = new();
        private Dictionary<int, float> lastUpdateTime = new();
        private float cacheLifetime = 0.05f;

        private Dictionary<int, StringBuilder> stringBuilderCache = new();

        private StateManager.AppState _currentAppState = StateManager.AppState.Idle;
        private StateManager.AppState _targetAppState = StateManager.AppState.Idle;

        private bool _isSunFading = false;
        private float _sunFadeDuration = 1f;
        private float _sunFadeStartTime;
        private float _currentSunFadeFactor = 1f;

        [SerializeField] private StripDataManager stripDataManager;

        [Serializable]
        public class BakedSunStateData
        {
            public int stateKey;
            [TextArea(3, 10)]
            public string concatenatedHexFrames;
            public int frameCount;
            public float frameDuration;
        }

        [Serializable]
        public class BakedSunStripData
        {
            public int stripIndex;
            public List<BakedSunStateData> stateData = new();
        }

        [SerializeField, HideInInspector]
        private List<BakedSunStripData> allPreBakedSunData = new();

        private void Awake()
        {
            if (stripDataManager == null)
            {
                stripDataManager = GetComponent<StripDataManager>();
            }

            if (dataSender == null)
            {
                dataSender = GetComponent<DataSender>();
            }
        }

        private void Start()
        {
            LogPreBakedDataStatus();
        }

        private void LogPreBakedDataStatus()
        {
            if (allPreBakedSunData == null || allPreBakedSunData.Count == 0)
            {
                return;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Pre-Bake Sun Data")]
        public void PreBakeSunDataEditor()
        {
            if (!stripDataManager)
            {
                return;
            }

            Undo.RecordObject(this, "Pre-Bake Sun Data");
            bool wasDirty = EditorUtility.IsDirty(this);

            allPreBakedSunData.Clear();

            if (dataSender != null)
            {
                if (dataSender.SendInterval > 0)
                {
                    preBakeFrameRate = 1f / dataSender.SendInterval;
                }
            }

            bool bakedSomething = false;
            for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (stripDataManager.currentDisplayModes.Count > stripIndex &&
                    stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SunMovement)
                {
                    PreBakeSingleSunStrip(stripIndex);
                    bakedSomething = true;
                }
            }

            if (bakedSomething)
            {
                if (!wasDirty)
                {
                    EditorUtility.SetDirty(this);
                }
            }
        }
#endif

        private void PreBakeSingleSunStrip(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripDataManager.totalLEDsPerStrip.Count)
            {
                return;
            }
            if (stripIndex >= stripDataManager.currentDataModes.Count)
            {
                return;
            }

            int totalLEDs = stripDataManager.totalLEDsPerStrip[stripIndex];

            int hexPerPixel = 2;

            BakedSunStripData stripData = new BakedSunStripData { stripIndex = stripIndex };

            // Четыре набора: (IdleWarm, IdleCold, ActiveWarm, ActiveCold)
            stripData.stateData.Add(PreBakeSunDataForStateAndMode(SunMode.Warm, warmSunSettingsIdle, StateManager.AppState.Idle, totalLEDs, hexPerPixel));
            stripData.stateData.Add(PreBakeSunDataForStateAndMode(SunMode.Cold, coldSunSettingsIdle, StateManager.AppState.Idle, totalLEDs, hexPerPixel));

            stripData.stateData.Add(PreBakeSunDataForStateAndMode(SunMode.Warm, warmSunSettingsActive, StateManager.AppState.Active, totalLEDs, hexPerPixel));
            stripData.stateData.Add(PreBakeSunDataForStateAndMode(SunMode.Cold, coldSunSettingsActive, StateManager.AppState.Active, totalLEDs, hexPerPixel));

            allPreBakedSunData.Add(stripData);
            Debug.Log($"[EffectsManager] Pre-baked data for Strip {stripIndex}.");
        }

        private BakedSunStateData PreBakeSunDataForStateAndMode(
            SunMode sunMode,
            SunMovementSettings settings,
            StateManager.AppState appState,
            int totalLEDs,
            int hexPerPixel)
        {
            int stateKey = GenerateStateKey(appState, sunMode);
            var stateData = new BakedSunStateData { stateKey = stateKey };

            if (settings == null || settings.baseCycleLength <= 0f)
            {
                return stateData;
            }

            float totalCycleLength = settings.baseCycleLength;
            int frameCount = Mathf.Max(1, Mathf.CeilToInt(totalCycleLength * preBakeFrameRate));
            float frameDuration = totalCycleLength / frameCount;

            stateData.frameCount = frameCount;
            stateData.frameDuration = frameDuration;

            byte[] pixelBrightness = new byte[totalLEDs];
            StringBuilder allFramesHex = new(frameCount * totalLEDs * hexPerPixel);

            for (int frame = 0; frame < frameCount; frame++)
            {
                float currentTime = frame * frameDuration;
                Array.Clear(pixelBrightness, 0, totalLEDs);

                bool isActive = false;

                if (settings.activeIntervals != null)
                {
                    foreach (var interval in settings.activeIntervals)
                    {
                        if (interval.endTime <= interval.startTime) continue;

                        float adjustedStartTime = interval.startTime;
                        float adjustedEndTime = interval.endTime;

                        if (currentTime >= adjustedStartTime && currentTime <= adjustedEndTime)
                        {
                            isActive = true;
                            float intervalDuration = adjustedEndTime - adjustedStartTime;
                            float activeTime = currentTime - adjustedStartTime;
                            float progress = Mathf.Clamp01(activeTime / intervalDuration);

                            float fadeFactor = 1f;

                            float sunPosition = totalLEDs * (1f - progress);

                            int centerPixel = Mathf.RoundToInt(sunPosition);
                            int halfPixels = Mathf.Max(1, settings.pixelCount) / 2;
                            float halfPixelsF = Mathf.Max(1f, settings.pixelCount / 2f);

                            for (int i = 0; i < settings.pixelCount; i++)
                            {
                                int pixelIndexOffset = centerPixel - halfPixels + i;
                                int wrappedIndex = pixelIndexOffset % totalLEDs;
                                if (wrappedIndex < 0) wrappedIndex += totalLEDs;

                                float distance = Mathf.Abs(pixelIndexOffset - sunPosition);
                                float normalizedBrightness = Mathf.Clamp01(1f - distance / halfPixelsF);

                                byte baseValue = 255;
                                byte newValue = (byte)(baseValue * normalizedBrightness * fadeFactor);
                                pixelBrightness[wrappedIndex] = Math.Max(pixelBrightness[wrappedIndex], newValue);
                            }
                        }
                    }
                }
                if (!isActive)
                {
                    Array.Clear(pixelBrightness, 0, totalLEDs);
                }

                for (int i = 0; i < totalLEDs; i++)
                {
                    allFramesHex.Append(pixelBrightness[i].ToString("X2"));
                }
            }

            stateData.concatenatedHexFrames = allFramesHex.ToString();
            return stateData;
        }

        public void SetAppState(StateManager.AppState state)
        {
            if (_currentAppState != state)
            {
                _targetAppState = state;

                if (_currentAppState != StateManager.AppState.Idle && state != _currentAppState)
                {
                    StartSunFadeOut(sunFadeDuration);
                }
                else
                {
                    _currentAppState = state;
                    ResetSunMovementPhase();
                    ClearCaches();
                }
            }
        }
        private void ResetSunMovementPhase()
        {
            _sunMovementPhase = 0f;
        }

        public void StartSunFadeOut(float duration)
        {
            _sunFadeDuration = duration;
            _sunFadeStartTime = Time.time;
            _isSunFading = true;
            _currentSunFadeFactor = 1f;
        }

        private void UpdateSunFade()
        {
            if (_isSunFading)
            {
                float elapsed = Time.time - _sunFadeStartTime;
                _currentSunFadeFactor = Mathf.Clamp01(1f - (elapsed / _sunFadeDuration));

                if (_currentSunFadeFactor <= 0f)
                {
                    _isSunFading = false;
                    _currentSunFadeFactor = 1f;
                    _currentAppState = _targetAppState;
                    ResetSunMovementPhase();
                }

                ClearSunCache();
            }
        }

        public void UpdateSpeed(float speed)
        {
            currentSpeedRaw = speed;
            ClearCaches();
        }

        private void ClearCaches()
        {
            pixelCache.Clear();
            hexCache.Clear();
            lastUpdateTime.Clear();
        }

        private void ClearSunCache()
        {
            for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (stripDataManager.currentDisplayModes.Count > stripIndex &&
                    stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SunMovement)
                {
                    hexCache.Remove(stripIndex);
                    lastUpdateTime.Remove(stripIndex);
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

        public void UpdateSunMovementPhase()
        {
            UpdateSunFade();

            SunMovementSettings settingsRef = GetCurrentSunSettingsForRuntime(0, _currentAppState);
            if (settingsRef == null || settingsRef.baseCycleLength <= 0f) return;

            float currentSpeed = CurrentSunSpeed;
            if (Mathf.Approximately(currentSpeed, 0f))
            {
                ClearSunCache();
                return;
            }


            float currentCycleDuration = settingsRef.baseCycleLength / Mathf.Abs(currentSpeed);
            if (Mathf.Approximately(currentCycleDuration, 0f)) return;

            _sunMovementPhase += dataSender.SendInterval / currentCycleDuration;

            if (_sunMovementPhase >= 1.0f)
            {
                _sunMovementPhase = 0f;
            }

            ClearSunCache();
        }

        private SunMovementSettings GetCurrentSunSettingsForRuntime(int stripIndex, StateManager.AppState state)
        {
            if (stripDataManager == null || stripIndex < 0 || stripIndex >= stripDataManager.currentSunModes.Count)
                return null;

            SunMode sunMode = stripDataManager.GetSunMode(stripIndex);
            return state switch
            {
                StateManager.AppState.Idle => sunMode == SunMode.Warm ? warmSunSettingsIdle : coldSunSettingsIdle,
                _ => sunMode == SunMode.Warm ? warmSunSettingsActive : coldSunSettingsActive
            };
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

            if (!stripComets.TryGetValue(stripIndex, out List<Comet> comets) || comets.Count == 0)
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
                float dynamicBrightness = Mathf.Clamp01(comet.brightness * stripBrightness);
                float speedBrightnessMultiplier = 1f + Mathf.Abs(CurrentCometSpeed) * speedBrightnessFactor;
                dynamicBrightness *= speedBrightnessMultiplier;

                if (!comet.isMoving)
                {
                    dynamicBrightness *= stationaryBrightnessFactor;
                }

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

            string result = GenerateOptimizedHexString(pixelColors, mode, colorProcessor, stripBrightness, stripGamma, stripGammaEnabled, hexPerPixel);
            hexCache[stripIndex] = result;
            lastUpdateTime[stripIndex] = Time.time;
            return result;
        }

        private int GenerateStateKey(StateManager.AppState appState, SunMode sunMode)
        {
            return ((int)appState * 10) + (int)sunMode;
        }

        public string GetHexDataForSunMovement(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            if (allPreBakedSunData == null) return "";

            BakedSunStripData stripData = allPreBakedSunData.FirstOrDefault(d => d.stripIndex == stripIndex);
            if (stripData == null)
            {
                return "";
            }

            if (stripIndex < 0 || stripIndex >= stripManager.currentSunModes.Count) return "";
            SunMode sunMode = stripManager.GetSunMode(stripIndex);
            int stateKey = GenerateStateKey(_currentAppState, sunMode);

            BakedSunStateData stateData = stripData.stateData.FirstOrDefault(s => s.stateKey == stateKey);
            if (stateData == null || string.IsNullOrEmpty(stateData.concatenatedHexFrames))
            {
                return "";
            }

            string bakedHexAnimation = stateData.concatenatedHexFrames;
            int frameCount = stateData.frameCount;
            float frameDuration = stateData.frameDuration;

            if (frameCount <= 0 || frameDuration <= 0f) return "";

            SunMovementSettings currentSettings = GetCurrentSunSettingsForRuntime(stripIndex, _currentAppState);
            if (currentSettings == null || currentSettings.baseCycleLength <= 0f) return "";

            float currentCycleTime = _sunMovementPhase * currentSettings.baseCycleLength;
            int currentFrame = Mathf.FloorToInt(currentCycleTime / frameDuration);

            if (currentFrame >= frameCount)
            {
                return "";
            }

            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
            if (stripIndex < 0 || stripIndex >= stripDataManager.totalLEDsPerStrip.Count) return "";
            int ledsPerStrip = stripDataManager.totalLEDsPerStrip[stripIndex];
            int frameHexLength = ledsPerStrip * hexPerPixel;
            int startIndex = currentFrame * frameHexLength;

            string rawHex = bakedHexAnimation.Substring(startIndex, frameHexLength);
            string optimizedHex = OptimizeHexString(rawHex, new string('0', hexPerPixel), hexPerPixel);
            return optimizedHex;
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

        private string GenerateOptimizedHexString(Color32[] pixelColors, DataMode mode, ColorProcessor colorProcessor, float stripBrightness, float stripGamma, bool stripGammaEnabled, int hexPerPixel)
        {
            int totalLEDs = pixelColors.Length;
            int sbKey = totalLEDs * 100 + (int)mode;

            if (!stringBuilderCache.TryGetValue(sbKey, out StringBuilder sb))
            {
                sb = new StringBuilder(totalLEDs * hexPerPixel);
                stringBuilderCache[sbKey] = sb;
            }
            else
            {
                sb.Clear();
            }

            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = pixelColors[i];
                string hexColor = mode switch
                {
                    DataMode.RGBW => colorProcessor.ColorToHexRGBW(pixelColor, stripBrightness, stripGamma, stripGammaEnabled),
                    DataMode.RGB => colorProcessor.ColorToHexRGB(pixelColor, stripBrightness, stripGamma, stripGammaEnabled),
                    _ => colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled),
                };
                sb.Append(hexColor);
            }
            return OptimizeHexString(sb.ToString(), new string('0', hexPerPixel), hexPerPixel);
        }

        private string OptimizeHexString(string hexString, string blackHex, int hexPerPixel)
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