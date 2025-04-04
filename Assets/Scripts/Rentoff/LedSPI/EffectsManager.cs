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
        [Tooltip("Total number of LEDs the sun occupies at its base brightness.")]
        public int pixelCount = 10;
        public float baseCycleLength = 241f;
        public List<TimeInterval> activeIntervals = new();
        [Tooltip("Global brightness multiplier for this sun effect.")]
        public float brightnessMultiplier = 1f;
        [Tooltip("Controls the blurriness of the sun edges. Higher values = more blurry edges.")]
        [Range(1f, 5f)]
        public float gaussianSpreadFactor = 3f;
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
        public string bakedDataFolderPath = "SunData";

        public float currentSpeedRaw = 1f;
        public float MultiplySpeed = 1f;
        private float CurrentSunSpeed => currentSpeedRaw * MultiplySpeed;
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

        private Dictionary<int, Dictionary<int, BakedSunStateData>> preloadedStateData = new();

        [Serializable]
        public class BakedSunStateData
        {
            public int stateKey;
            public List<string> hexFrames;
            public int frameCount;
            public float frameDuration;
        }

        [Serializable]
        public class BakedSunStripData
        {
            public int stripIndex;
            public List<BakedSunStateData> stateData = new();
        }

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

            PreloadBakedSunData();
        }

        private string GetSunDataFilePath(int stripIndex)
        {
            return Path.Combine(Application.streamingAssetsPath, bakedDataFolderPath, $"BakedSunData_Strip_{stripIndex}.json");
        }

        private void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void PreloadBakedSunData()
        {
            string directoryPath = Path.Combine(Application.streamingAssetsPath, bakedDataFolderPath);
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogWarning($"Baked sun data directory not found at: {directoryPath}");
                return;
            }

            string[] filePaths = Directory.GetFiles(directoryPath, "BakedSunData_Strip_*.json");
            if (filePaths.Length == 0)
            {
                Debug.LogWarning($"No baked sun data files found in: {directoryPath}");
                return;
            }

            Debug.Log($"Found {filePaths.Length} baked sun data files. Preloading...");

            foreach (string filePath in filePaths)
            {
                try
                {
                    string jsonData = File.ReadAllText(filePath);
                    BakedSunStripData stripData = JsonUtility.FromJson<BakedSunStripData>(jsonData);

                    if (stripData == null || stripData.stateData == null || stripData.stateData.Count == 0)
                    {
                        Debug.LogWarning($"No pre-baked sun data found in file: {filePath}");
                        continue;
                    }

                    int stripIndex = stripData.stripIndex;

                    if (!preloadedStateData.TryGetValue(stripIndex, out var stateInfoDict))
                    {
                        stateInfoDict = new Dictionary<int, BakedSunStateData>();
                        preloadedStateData[stripIndex] = stateInfoDict;
                    }

                    foreach (var stateData in stripData.stateData)
                    {
                        if (stateData.hexFrames != null && stateData.hexFrames.Count > 0 && stateData.hexFrames.Count == stateData.frameCount)
                        {
                            stateInfoDict[stateData.stateKey] = stateData;
                        }
                        else
                        {
                             Debug.LogWarning($"Inconsistent frame data for strip {stripIndex}, state key {stateData.stateKey} in {filePath}. " +
                                              $"Expected {stateData.frameCount} frames, found {stateData.hexFrames?.Count ?? 0}.");
                        }
                    }
                    Debug.Log($"Successfully preloaded sun data for strip {stripIndex} from {filePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error loading baked sun data from JSON file {filePath}: {e.Message}");
                }
            }
            Debug.Log("Sun data preloading from JSON files complete");
        }

#if UNITY_EDITOR
        [ContextMenu("Pre-Bake Sun Data")]
        public void PreBakeSunDataEditor()
        {
            if (!stripDataManager)
            {
                Debug.LogError("StripDataManager is not assigned.");
                return;
            }

            if (dataSender != null && dataSender.SendInterval > 0)
            {
                preBakeFrameRate = Mathf.Max(1f, 1f / dataSender.SendInterval);
            }
            else
            {
                preBakeFrameRate = 35f;
            }

            bool bakedSomething = false;
            for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (stripDataManager.currentDisplayModes.Count > stripIndex &&
                    stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SunMovement)
                {
                    BakedSunStripData stripData = PreBakeSingleSunStrip(stripIndex);
                    if (stripData != null && stripData.stateData.Count > 0)
                    {
                        SaveBakedStripDataToJson(stripData);
                        bakedSomething = true;
                    }
                }
            }

            if (bakedSomething)
            {
                Debug.Log("Finished baking sun data to JSON files.");
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.Log("No strips found with SunMovement mode to bake.");
            }
        }

        private void SaveBakedStripDataToJson(BakedSunStripData stripData)
        {
            if (stripData == null || stripData.stateData == null || stripData.stateData.Count == 0)
            {
                return;
            }

            string filePath = GetSunDataFilePath(stripData.stripIndex);
            EnsureDirectoryExists(filePath);

            try
            {
                string jsonData = JsonUtility.ToJson(stripData, true);
                File.WriteAllText(filePath, jsonData);
                Debug.Log($"Saved baked sun data for strip {stripData.stripIndex} to: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving baked sun data for strip {stripData.stripIndex} to {filePath}: {e.Message}");
            }
        }
#endif

        private BakedSunStripData PreBakeSingleSunStrip(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripDataManager.totalLEDsPerStrip.Count)
            {
                Debug.LogError($"Invalid strip index {stripIndex} for pre-baking.");
                return null;
            }
            if (stripIndex >= stripDataManager.currentDataModes.Count)
            {
                Debug.LogError($"Data modes not configured for strip index {stripIndex}.");
                return null;
            }

            int totalLEDs = stripDataManager.totalLEDsPerStrip[stripIndex];
            Debug.Log($"Pre-baking SunMovement data for Strip {stripIndex} ({totalLEDs} LEDs)...");

            int hexPerPixel = 2; // Monochrome brightness

            BakedSunStripData stripData = new BakedSunStripData { stripIndex = stripIndex };

            stripData.stateData.Add(PreBakeSunDataForStateAndMode(SunMode.Warm, warmSunSettingsIdle, StateManager.AppState.Idle, totalLEDs, hexPerPixel));
            stripData.stateData.Add(PreBakeSunDataForStateAndMode(SunMode.Cold, coldSunSettingsIdle, StateManager.AppState.Idle, totalLEDs, hexPerPixel));
            stripData.stateData.Add(PreBakeSunDataForStateAndMode(SunMode.Warm, warmSunSettingsActive, StateManager.AppState.Active, totalLEDs, hexPerPixel));
            stripData.stateData.Add(PreBakeSunDataForStateAndMode(SunMode.Cold, coldSunSettingsActive, StateManager.AppState.Active, totalLEDs, hexPerPixel));

            stripData.stateData.RemoveAll(sd => sd.hexFrames == null || sd.hexFrames.Count == 0);

            Debug.Log($"[EffectsManager] Finished pre-baking data for Strip {stripIndex}. {stripData.stateData.Count} states baked.");
            return stripData;
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
                Debug.LogWarning($"Skipping bake for state {appState}, mode {sunMode}: Invalid settings or cycle length.");
                return stateData;
            }

            Debug.Log($"Baking state {appState}, mode {sunMode}, key {stateKey}...");

            float totalCycleLength = settings.baseCycleLength;
            int frameCount = Mathf.Max(1, Mathf.CeilToInt(totalCycleLength * preBakeFrameRate));
            float frameDuration = totalCycleLength / frameCount;

            stateData.frameCount = frameCount;
            stateData.frameDuration = frameDuration;
            stateData.hexFrames = new List<string>(frameCount);

            byte[] pixelBrightness = new byte[totalLEDs];

            for (int frame = 0; frame < frameCount; frame++)
            {
                float currentTime = frame * frameDuration;
                Array.Clear(pixelBrightness, 0, totalLEDs);
                StringBuilder currentFrameHex = new StringBuilder(totalLEDs * hexPerPixel);

                bool isActiveTime = false;

                if (settings.activeIntervals != null)
                {
                    foreach (var interval in settings.activeIntervals)
                    {
                        if (interval.endTime <= interval.startTime) continue;

                        float adjustedStartTime = interval.startTime;
                        float adjustedEndTime = interval.endTime;

                        if (currentTime >= adjustedStartTime && currentTime <= adjustedEndTime)
                        {
                            isActiveTime = true;
                            float intervalDuration = adjustedEndTime - adjustedStartTime;
                            float activeTime = currentTime - adjustedStartTime;
                            float progress = Mathf.Clamp01(activeTime / intervalDuration);

                            float sunPositionContinuous = totalLEDs * (1f - progress);
                            float sunRadius = Mathf.Max(1f, settings.pixelCount) * 0.5f;
                            float sigma = sunRadius / Mathf.Max(0.1f, settings.gaussianSpreadFactor);
                            float denominator = 2f * sigma * sigma;
                            if (denominator <= 0f) denominator = 0.001f;

                            for (int i = 0; i < totalLEDs; i++)
                            {
                                float distance = GetWrappedDistance(i, sunPositionContinuous, totalLEDs);
                                float brightnessFactor = Mathf.Exp(-(distance * distance) / denominator);
                                byte brightnessValue = (byte)Mathf.Clamp(255f * brightnessFactor, 0, 255);
                                pixelBrightness[i] = Math.Max(pixelBrightness[i], brightnessValue);
                            }
                            break;
                        }
                    }
                }
                if (!isActiveTime)
                {
                    Array.Clear(pixelBrightness, 0, totalLEDs);
                }

                for (int i = 0; i < totalLEDs; i++)
                {
                    currentFrameHex.Append(pixelBrightness[i].ToString("X2"));
                }
                stateData.hexFrames.Add(currentFrameHex.ToString());
            }

            Debug.Log($"Baked state {appState}, mode {sunMode}: {frameCount} frames, duration {frameDuration:F3}s");
            return stateData;
        }

        private float GetWrappedDistance(float p1, float p2, int wrap)
        {
            float diffNormal = Mathf.Abs(p1 - p2);
            float diffWrap = wrap - diffNormal;
            return Mathf.Min(diffNormal, diffWrap);
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

            _sunMovementPhase += Time.deltaTime / currentCycleDuration;
            _sunMovementPhase %= 1.0f;

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
                float dynamicBrightness = comet.brightness; // Base brightness of the comet
                float speedBrightnessMultiplier = 1f + Mathf.Abs(CurrentCometSpeed) * speedBrightnessFactor;
                dynamicBrightness *= speedBrightnessMultiplier;

                if (!comet.isMoving)
                {
                    dynamicBrightness *= stationaryBrightnessFactor;
                }

                dynamicBrightness = Mathf.Clamp01(dynamicBrightness * stripBrightness); // Apply strip brightness

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

            string result = GenerateOptimizedHexString(pixelColors, mode, colorProcessor, 1f, stripGamma, stripGammaEnabled, hexPerPixel); // Brightness already applied
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
            if (_currentAppState == StateManager.AppState.Transition)
            {
                return "";
            }

            if (hexCache.TryGetValue(stripIndex, out string cachedHex) &&
                lastUpdateTime.TryGetValue(stripIndex, out float lastUpdate) &&
                Time.time - lastUpdate < cacheLifetime)
            {
                return cachedHex;
            }

            if (stripIndex < 0 || stripIndex >= stripManager.currentSunModes.Count) return "";
            SunMode sunMode = stripManager.GetSunMode(stripIndex);
            int stateKey = GenerateStateKey(_currentAppState, sunMode);

            if (preloadedStateData.TryGetValue(stripIndex, out var stateInfoDict) &&
                stateInfoDict.TryGetValue(stateKey, out BakedSunStateData stateData))
            {
                int frameCount = stateData.frameCount;
                float frameDuration = stateData.frameDuration;

                if (frameCount <= 0 || frameDuration <= 0f || stateData.hexFrames == null || stateData.hexFrames.Count != frameCount)
                {
                    Debug.LogError($"Inconsistent baked data for strip {stripIndex}, state {stateKey}");
                    return "";
                }

                SunMovementSettings currentSettings = GetCurrentSunSettingsForRuntime(stripIndex, _currentAppState);
                if (currentSettings == null || currentSettings.baseCycleLength <= 0f) return "";

                float currentCycleTime = _sunMovementPhase * currentSettings.baseCycleLength;
                int currentFrame = Mathf.FloorToInt(currentCycleTime / frameDuration);
                currentFrame = currentFrame % frameCount;

                int hexPerPixelTarget = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
                int hexPerPixelSource = 2;

                if (stripIndex < 0 || stripIndex >= stripDataManager.totalLEDsPerStrip.Count) return "";
                int ledsPerStrip = stripDataManager.totalLEDsPerStrip[stripIndex];
                int expectedFrameHexLength = ledsPerStrip * hexPerPixelSource;

                if (currentFrame < 0 || currentFrame >= stateData.hexFrames.Count)
                {
                    Debug.LogWarning($"Calculated frame index {currentFrame} out of bounds for strip {stripIndex}, state {stateKey}. " +
                                     $"Frame count: {stateData.hexFrames.Count}");
                    return "";
                }

                string frameHex = stateData.hexFrames[currentFrame];

                if (frameHex == null || frameHex.Length != expectedFrameHexLength)
                {
                    Debug.LogWarning($"Invalid baked frame data at index {currentFrame} for strip {stripIndex}, state {stateKey}. " +
                                     $"Expected length {expectedFrameHexLength}, got {frameHex?.Length ?? 0}");
                    return "";
                }

                float stripBrightness = stripManager.GetStripBrightness(stripIndex);
                float effectBrightness = currentSettings.brightnessMultiplier;
                float totalBrightnessMultiplier = stripBrightness * effectBrightness;

                float stripGamma = stripManager.GetStripGamma(stripIndex);
                bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

                StringBuilder convertedHex = GetStringBuilder(ledsPerStrip * hexPerPixelTarget);
                for (int i = 0; i < ledsPerStrip; i++)
                {
                    int pixelStart = i * hexPerPixelSource;
                    if (pixelStart + hexPerPixelSource <= frameHex.Length)
                    {
                        string brightnessHex = frameHex.Substring(pixelStart, hexPerPixelSource);
                        if (byte.TryParse(brightnessHex, System.Globalization.NumberStyles.HexNumber, null, out byte bakedBrightness))
                        {
                            byte finalBrightness = (byte)Mathf.Clamp(bakedBrightness * totalBrightnessMultiplier, 0, 255);
                            Color32 pixelColor = new Color32(finalBrightness, finalBrightness, finalBrightness, 255);
                            string hexColor = mode switch
                            {
                                DataMode.RGBW => colorProcessor.ColorToHexRGBW(pixelColor, 1f, stripGamma, stripGammaEnabled),
                                DataMode.RGB => colorProcessor.ColorToHexRGB(pixelColor, 1f, stripGamma, stripGammaEnabled),
                                _ => colorProcessor.ColorToHexMonochrome(pixelColor, 1f, stripGamma, stripGammaEnabled),
                            };
                            convertedHex.Append(hexColor);
                        }
                        else
                        {
                            convertedHex.Append(new string('0', hexPerPixelTarget));
                        }
                    }
                    else
                    {
                        convertedHex.Append(new string('0', hexPerPixelTarget));
                    }
                }

                string optimizedHex = OptimizeHexString(convertedHex.ToString(), new string('0', hexPerPixelTarget), hexPerPixelTarget);
                hexCache[stripIndex] = optimizedHex;
                lastUpdateTime[stripIndex] = Time.time;
                return optimizedHex;
            }

            Debug.LogWarning($"No preloaded sun data found for strip {stripIndex}, state key {stateKey}");
            return "";
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


        private string GenerateOptimizedHexString(Color32[] pixelColors, DataMode mode, ColorProcessor colorProcessor, float brightness, float gamma, bool gammaEnabled, int hexPerPixel)
        {
            int totalLEDs = pixelColors.Length;
            StringBuilder sb = GetStringBuilder(totalLEDs * hexPerPixel);

            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = pixelColors[i];
                string hexColor = mode switch
                {
                    DataMode.RGBW => colorProcessor.ColorToHexRGBW(pixelColor, brightness, gamma, gammaEnabled),
                    DataMode.RGB => colorProcessor.ColorToHexRGB(pixelColor, brightness, gamma, gammaEnabled),
                    _ => colorProcessor.ColorToHexMonochrome(pixelColor, brightness, gamma, gammaEnabled),
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