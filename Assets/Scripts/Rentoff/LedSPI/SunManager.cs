using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;

namespace LEDControl
{
    public class SunManager : MonoBehaviour
    {
        [Header("Sun Movement Settings")]
        public SunMovementSettings idleSunSettings = new();
        public SunMovementSettings activeSunSettings = new();

        [Header("Transition Settings")]
        public float sunFadeDuration = 1.0f;

        [Header("Synchronization Settings")]
        public DataSender dataSender;

        [Header("Pre-Bake Settings")]
        public float preBakeFrameRate = 35f;
        public string bakedDataFolderPath = "SunData";

        [Header("Virtual Strip Settings")]
        [Tooltip("Number of virtual LEDs to add on each end of the strip")]
        public int virtualPadding = 5;

        public float currentSpeedRaw = 1f;
        public float MultiplySpeed = 1f;
        private float CurrentSunSpeed => currentSpeedRaw * MultiplySpeed;

        private float _sunMovementPhase = 0f;

        private Dictionary<int, string> hexCache = new();
        private Dictionary<int, float> lastUpdateTime = new();
        private float cacheLifetime = 0.05f;

        private Dictionary<int, StringBuilder> stringBuilderCache = new();

        private StateManager.AppState _currentAppState = StateManager.AppState.Idle;
        private StateManager.AppState _targetAppState = StateManager.AppState.Idle;

        private bool _isSunFading = false;
        private float _sunFadeDurationInternal = 1f; // Renamed to avoid conflict with public field
        private float _sunFadeStartTime;
        private float _currentSunFadeFactor = 1f;

        [SerializeField] private StripDataManager stripDataManager;
        [SerializeField] private ColorProcessor colorProcessor;

        private Dictionary<int, Dictionary<int, BakedSunStateData>> preloadedStateData = new();

        public enum SunMode
        {
            Warm,
            Cold,
            Gradient
        }

        [Serializable]
        public struct TimeInterval
        {
            public float startTime;
            public float endTime;
            public SunMode sunMode;
            [Range(0f, 1f)] public float startPosition;
            [Range(0f, 1f)] public float endPosition;
        }

        [Serializable]
        public class SunMovementSettings
        {
            public float baseCycleLength = 241f;
            public List<TimeInterval> intervals = new();
            [Tooltip("Global brightness multiplier for this sun effect.")]
            public float brightnessMultiplier = 1f;
            [Tooltip("Controls the blurriness of the sun edges. Higher values = more blurry edges.")]
            [Range(1f, 5f)]
            public float gaussianSpreadFactor = 3f;

            public override string ToString()
            {
                return $"CycleLength: {baseCycleLength}, Brightness: {brightnessMultiplier}, Gaussian: {gaussianSpreadFactor}";
            }
        }

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

        private void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void Awake()
        {
            _sunFadeDurationInternal = sunFadeDuration; // Initialize internal variable
            PreloadBakedSunData();
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
            if (!colorProcessor)
            {
                Debug.LogError("ColorProcessor is not assigned.");
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

        private string GetSunDataFilePath(int stripIndex)
        {
            return Path.Combine(Application.streamingAssetsPath, bakedDataFolderPath, $"BakedSunData_Strip_{stripIndex}.json");
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

            DataMode dataMode = stripDataManager.currentDataModes[stripIndex];
            int actualLEDs = stripDataManager.totalLEDsPerStrip[stripIndex];
            int totalLEDs = actualLEDs + virtualPadding * 2;

            Debug.Log($"Pre-baking SunMovement data for Strip {stripIndex} ({actualLEDs} LEDs + {virtualPadding * 2} virtual LEDs) in {dataMode} mode...");

            int hexPerPixel = dataMode switch
            {
                DataMode.Monochrome1Color or DataMode.Monochrome2Color => 2,
                DataMode.RGB => 6,
                DataMode.RGBW => 8,
                _ => 2
            };
            string blackHexValue = new string('0', hexPerPixel);

            BakedSunStripData stripData = new BakedSunStripData { stripIndex = stripIndex };

            stripData.stateData.Add(PreBakeSunDataForStateAndMode(stripIndex, StateManager.AppState.Idle, totalLEDs, actualLEDs, hexPerPixel, dataMode, blackHexValue, idleSunSettings));
            stripData.stateData.Add(PreBakeSunDataForStateAndMode(stripIndex, StateManager.AppState.Active, totalLEDs, actualLEDs, hexPerPixel, dataMode, blackHexValue, activeSunSettings));

            stripData.stateData.RemoveAll(sd => sd.hexFrames == null || sd.hexFrames.Count == 0);

            Debug.Log($"[SunManager] Finished pre-baking data for Strip {stripIndex}. {stripData.stateData.Count} states baked.");
            return stripData;
        }

        private BakedSunStateData PreBakeSunDataForStateAndMode(
            int stripIndex,
            StateManager.AppState appState,
            int totalLEDs,
            int actualLEDs,
            int hexPerPixel,
            DataMode dataMode,
            string blackHexValue,
            SunMovementSettings settingsRef)
        {
            int stateKey = GenerateStateKey(appState);
            var stateData = new BakedSunStateData { stateKey = stateKey };

            if (settingsRef == null || settingsRef.baseCycleLength <= 0f)
            {
                Debug.LogWarning($"Skipping bake for state {appState}: Settings invalid or cycle length zero.");
                return stateData;
            }

            Debug.Log($"Baking state {appState}, key {stateKey} for strip {stripIndex}");

            float totalCycleLength = settingsRef.baseCycleLength;
            int frameCount = Mathf.Max(1, Mathf.CeilToInt(totalCycleLength * preBakeFrameRate));
            float frameDuration = totalCycleLength / frameCount;

            stateData.frameCount = frameCount;
            stateData.frameDuration = frameDuration;
            stateData.hexFrames = new List<string>(frameCount);

            byte[] pixelBrightness = new byte[totalLEDs];

            bool previouslyActive = false; // Track if the *previous* frame was active

            for (int frame = 0; frame < frameCount; frame++)
            {
                float currentTime = (frame * frameDuration) % totalCycleLength;

                bool isActiveTime = false;
                SunMode currentSunMode = SunMode.Warm;
                float currentIntervalStartPosition = 0f;
                float currentIntervalEndPosition = 0f;
                float currentIntervalStartTime = 0f;
                float currentIntervalDuration = 1f;


                // Determine if current frame is active and get interval details
                foreach (var interval in settingsRef.intervals)
                {
                    if (interval.endTime <= interval.startTime) continue;

                    if (currentTime >= interval.startTime && currentTime < interval.endTime)
                    {
                        isActiveTime = true;
                        currentSunMode = interval.sunMode;
                        currentIntervalStartPosition = interval.startPosition;
                        currentIntervalEndPosition = interval.endPosition;
                        currentIntervalStartTime = interval.startTime;
                        currentIntervalDuration = interval.endTime - interval.startTime;
                        break;
                    }
                }

                string frameDataToAdd;

                if (isActiveTime)
                {
                    // Logic for ACTIVE frame
                    Array.Clear(pixelBrightness, 0, totalLEDs);

                    float activeTime = currentTime - currentIntervalStartTime;
                    float intervalTimeProgress = Mathf.Clamp01(activeTime / currentIntervalDuration);

                    float currentNormalizedStripPos = Mathf.Lerp(currentIntervalStartPosition, currentIntervalEndPosition, intervalTimeProgress);

                    int stripPixelCount = stripDataManager.GetSunPixelCountForStrip(stripIndex);
                    float sunRadius = Mathf.Max(1f, stripPixelCount) * 1f;
                    float virtualLength = totalLEDs + 2 * sunRadius;

                    float sunPositionContinuous = (virtualLength * (1f - currentNormalizedStripPos)) - sunRadius;

                    float sigma = sunRadius / Mathf.Max(0.1f, settingsRef.gaussianSpreadFactor);
                    float denominator = 2f * sigma * sigma;
                    if (denominator <= 0f) denominator = 0.001f;

                    for (int i = 0; i < totalLEDs; i++)
                    {
                        float distance = Mathf.Abs(i - sunPositionContinuous);
                        float brightnessFactor = Mathf.Exp(-(distance * distance) / denominator);
                        byte brightnessValue = (byte)Mathf.Clamp(255f * brightnessFactor, 0, 255);
                        pixelBrightness[i] = Math.Max(pixelBrightness[i], brightnessValue);
                    }

                    StringBuilder frameHexBuilder = new(totalLEDs * hexPerPixel);
                    Color32 sunColor = stripDataManager.GetSunColorForStrip(stripIndex, currentSunMode);
                    Color32 sunEndColor = stripDataManager.GetSunEndColorForStrip(stripIndex, currentSunMode);
                    Color32 pixelColor = Color.black;

                    for (int i = 0; i < totalLEDs; i++)
                    {
                        float normalizedPos = (float)i / totalLEDs;
                        Color32 blendedColor = currentSunMode == SunMode.Gradient
                            ? Color32.Lerp(sunColor, sunEndColor, normalizedPos)
                            : sunColor;

                        pixelColor.r = (byte)(blendedColor.r * pixelBrightness[i] / 255f);
                        pixelColor.g = (byte)(blendedColor.g * pixelBrightness[i] / 255f);
                        pixelColor.b = (byte)(blendedColor.b * pixelBrightness[i] / 255f);

                        string pixelHex = "";

                        float stripBrightness = stripDataManager.GetStripBrightness(stripIndex);
                        float stripGamma = stripDataManager.GetStripGamma(stripIndex);
                        bool stripGammaEnabled = stripDataManager.IsGammaCorrectionEnabled(stripIndex);

                        switch (dataMode)
                        {
                            case DataMode.Monochrome1Color:
                            case DataMode.Monochrome2Color:
                                pixelHex = colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled);
                                break;
                            case DataMode.RGB:
                                pixelHex = colorProcessor.ColorToHexRGB(pixelColor, pixelColor, pixelColor, stripBrightness, stripGamma, stripGammaEnabled);
                                break;
                            case DataMode.RGBW:
                                pixelHex = colorProcessor.ColorToHexRGBW(pixelColor, pixelColor, pixelColor, stripBrightness, stripGamma, stripGammaEnabled);
                                break;
                            default:
                                pixelHex = blackHexValue;
                                break;
                        }
                        frameHexBuilder.Append(pixelHex);
                    }

                    string fullHexString = frameHexBuilder.ToString();
                    string trimmedHexString = TrimVirtualPadding(fullHexString, hexPerPixel, virtualPadding, actualLEDs);
                    frameDataToAdd = OptimizeHexStringForBaking(trimmedHexString, blackHexValue, hexPerPixel);
                    previouslyActive = true;
                }
                else // Not active time for this frame
                {
                    if (previouslyActive)
                    {
                        frameDataToAdd = "clear";
                    }
                    else
                    {
                        frameDataToAdd = "";
                    }
                    previouslyActive = false;
                }

                stateData.hexFrames.Add(frameDataToAdd);
            }

            Debug.Log($"Baked state {appState} for strip {stripIndex}: {frameCount} frames, duration {frameDuration:F3}s");
            return stateData;
        }

        private string TrimVirtualPadding(string fullHexString, int hexPerPixel, int padding, int actualLEDs)
        {
            int startIndex = padding * hexPerPixel;
            int length = actualLEDs * hexPerPixel;

            if (startIndex + length > fullHexString.Length)
            {
                length = Mathf.Max(0, fullHexString.Length - startIndex);
            }

            if (startIndex >= fullHexString.Length || length <= 0)
            {
                return "";
            }

            return fullHexString.Substring(startIndex, length);
        }

        private string OptimizeHexStringForBaking(string hexString, string blackHex, int hexPerPixel)
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
                    InvalidateCache(CacheInvalidationReason.AppStateChange);
                }
            }
        }

        private void ResetSunMovementPhase()
        {
            _sunMovementPhase = 0f;
        }

        public void StartSunFadeOut(float duration)
        {
            _sunFadeDurationInternal = duration;
            _sunFadeStartTime = Time.time;
            _isSunFading = true;
            _currentSunFadeFactor = 1f;
        }

        private void UpdateSunFade()
        {
            if (_isSunFading)
            {
                float elapsed = Time.time - _sunFadeStartTime;
                _currentSunFadeFactor = Mathf.Clamp01(1f - (elapsed / _sunFadeDurationInternal));

                if (_currentSunFadeFactor <= 0f)
                {
                    _isSunFading = false;
                    _currentSunFadeFactor = 1f;

                    _currentAppState = _targetAppState;

                    ResetSunMovementPhase();
                    InvalidateCache(CacheInvalidationReason.AppStateChange);
                }

                ClearSunCache();
            }
        }

        private void ClearSunCache()
        {
            hexCache.Clear();
            lastUpdateTime.Clear();
        }

        public void UpdateSpeed(float speed)
        {
            if (Mathf.Abs(currentSpeedRaw - speed) > Mathf.Epsilon)
            {
                currentSpeedRaw = speed;
                InvalidateCache(CacheInvalidationReason.SpeedChange);
            }
        }

        public void UpdateSunMovementPhase()
        {
            UpdateSunFade();

            SunMovementSettings settingsRef = GetCurrentSunSettingsForRuntime(_currentAppState);
            if (settingsRef == null || settingsRef.baseCycleLength <= 0f) return;

            float currentSpeed = CurrentSunSpeed;
            if (Mathf.Approximately(currentSpeed, 0f))
            {
                if (hexCache.Count > 0)
                {
                    ClearSunCache();
                }
                return;
            }

            float currentCycleDuration = settingsRef.baseCycleLength / (currentSpeed);
            if (Mathf.Approximately(currentCycleDuration, 0f)) return;

            _sunMovementPhase += Time.deltaTime / currentCycleDuration;
            _sunMovementPhase %= 1.0f;

            ClearSunCache();
        }

        private SunMovementSettings GetCurrentSunSettingsForRuntime(StateManager.AppState state)
        {
            return state switch
            {
                StateManager.AppState.Idle => idleSunSettings,
                _ => activeSunSettings
            };
        }

        private enum CacheInvalidationReason
        {
            AppStateChange,
            SpeedChange,
            SunSettingsChange
        }

        private void InvalidateCache(CacheInvalidationReason reason)
        {
            switch (reason)
            {
                case CacheInvalidationReason.AppStateChange:
                    Debug.Log("Cache invalidated due to AppState change.");
                    break;
                case CacheInvalidationReason.SpeedChange:
                    Debug.Log("Cache invalidated due to Speed change.");
                    break;
                case CacheInvalidationReason.SunSettingsChange:
                    Debug.Log("Cache invalidated due to SunSettings change.");
                    break;
            }
            ClearSunCache();
        }

        public string GetHexDataForSunMovement(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            if (_currentAppState == StateManager.AppState.Transition)
            {
                return "";
            }

            int cacheKey = GetCacheKey(stripIndex, _currentAppState);

            if (hexCache.TryGetValue(cacheKey, out string cachedHex) &&
                lastUpdateTime.TryGetValue(cacheKey, out float lastUpdate) &&
                Time.time - lastUpdate < cacheLifetime)
            {
                return cachedHex;
            }

            if (stripIndex < 0 || stripIndex >= stripManager.currentSunModes.Count) return "";
            int stateKey = GenerateStateKey(_currentAppState);

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

                SunMovementSettings currentSettings = GetCurrentSunSettingsForRuntime(_currentAppState);
                if (currentSettings == null || currentSettings.baseCycleLength <= 0f) return "";

                float currentCycleTime = _sunMovementPhase * currentSettings.baseCycleLength;
                int currentFrame = Mathf.FloorToInt(currentCycleTime / frameDuration);
                currentFrame = Mathf.Clamp(currentFrame, 0, frameCount - 1);

                string hexData = stateData.hexFrames[currentFrame];

                hexCache[cacheKey] = hexData;
                lastUpdateTime[cacheKey] = Time.time;

                return hexData;
            }
            else
            {
                return "";
            }
        }

        private int GenerateStateKey(StateManager.AppState state)
        {
            return (int)state;
        }

        private int GetCacheKey(int stripIndex, StateManager.AppState appState)
        {
            return HashCode.Combine(stripIndex, appState);
        }

        public void OnSunSettingsChanged()
        {
            InvalidateCache(CacheInvalidationReason.SunSettingsChange);
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(SunManager))]
        public class SunManagerEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                SunManager sunManager = (SunManager)target;

                if (GUILayout.Button("Invalidate Cache"))
                {
                    sunManager.OnSunSettingsChanged();
                }
                if (GUILayout.Button("Pre-Bake Sun Data"))
                {
                    sunManager.PreBakeSunDataEditor();
                }
            }
        }
#endif
    }
}