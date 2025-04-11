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
        private float _sunFadeDuration = 1f;
        private float _sunFadeStartTime;
        private float _currentSunFadeFactor = 1f;

        [SerializeField] private StripDataManager stripDataManager;
        [SerializeField] private ColorProcessor colorProcessor;

        private Dictionary<int, Dictionary<int, BakedSunStateData>> preloadedStateData = new();

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
            public SunMode sunMode;
        }

        [Serializable]
        public class SunMovementSettings
        {
            // Removed pixelCount from here
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

        private float GetWrappedDistance(float p1, float p2, int wrap)
        {
            float diffNormal = Mathf.Abs(p1 - p2);
            float diffWrap = wrap - diffNormal;
            return Mathf.Min(diffNormal, diffWrap);
        }

        private void Awake()
        {
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
                            Debug.LogWarning($"Inconsistent frame data for strip {stripIndex}, state key {stateData.stateKey} in {filePath}. Expected {stateData.frameCount} frames, found {stateData.hexFrames?.Count ?? 0}.");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error loading baked sun data from JSON file {filePath}: {e.Message}");
                }
            }
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

            preBakeFrameRate = (dataSender != null && dataSender.SendInterval > 0) ? Mathf.Max(1f, 1f / dataSender.SendInterval) : 35f;

            bool bakedSomething = false;
            if (stripDataManager.totalLEDsPerStrip == null || stripDataManager.currentDisplayModes == null)
            {
                Debug.LogError("StripDataManager lists are not initialized.");
                return;
            }

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
                AssetDatabase.Refresh();
                Debug.Log("Finished baking sun data to JSON files.");
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
            int totalLEDs = stripDataManager.totalLEDsPerStrip[stripIndex];

            int hexPerPixel = dataMode switch
            {
                DataMode.Monochrome1Color or DataMode.Monochrome2Color => 2,
                DataMode.RGB => 6,
                DataMode.RGBW => 8,
                _ => 2
            };
            string blackHexValue = new string('0', hexPerPixel);

            BakedSunStripData stripData = new BakedSunStripData { stripIndex = stripIndex };

            stripData.stateData.Add(PreBakeSunDataForState(stripIndex, StateManager.AppState.Idle, totalLEDs, hexPerPixel, dataMode, blackHexValue));
            stripData.stateData.Add(PreBakeSunDataForState(stripIndex, StateManager.AppState.Active, totalLEDs, hexPerPixel, dataMode, blackHexValue));

            stripData.stateData.RemoveAll(sd => sd.hexFrames == null || sd.hexFrames.Count == 0);

            return stripData;
        }

        private BakedSunStateData PreBakeSunDataForState(
            int stripIndex,
            StateManager.AppState appState,
            int totalLEDs,
            int hexPerPixel,
            DataMode dataMode,
            string blackHexValue)
        {
            int stateKey = GenerateStateKey(appState);
            var stateData = new BakedSunStateData { stateKey = stateKey };

            SunMovementSettings settingsRef = GetCurrentSunSettingsForRuntime(appState);
            if (settingsRef == null || settingsRef.baseCycleLength <= 0f)
            {
                Debug.LogWarning($"Skipping bake for state {appState}: Invalid settings or cycle length.");
                return stateData;
            }

            float totalCycleLength = settingsRef.baseCycleLength;
            int frameCount = Mathf.Max(1, Mathf.CeilToInt(totalCycleLength * preBakeFrameRate));
            float frameDuration = totalCycleLength / frameCount;

            stateData.frameCount = frameCount;
            stateData.frameDuration = frameDuration;
            stateData.hexFrames = new List<string>(frameCount);

            byte[] pixelBrightness = new byte[totalLEDs];

            int sunPixelCount = stripDataManager.GetSunPixelCount(stripIndex);

            for (int frame = 0; frame < frameCount; frame++)
            {
                float currentTime = frame * frameDuration;
                Array.Clear(pixelBrightness, 0, totalLEDs);
                StringBuilder currentFrameHex = new StringBuilder(totalLEDs * hexPerPixel);

                SunMode currentSunMode = SunMode.Warm;
                bool foundInterval = false;

                foreach (var interval in settingsRef.intervals)
                {
                    if (interval.endTime <= interval.startTime) continue;

                    if (currentTime >= interval.startTime && currentTime < interval.endTime)
                    {
                        foundInterval = true;
                        currentSunMode = interval.sunMode;
                        float intervalDuration = interval.endTime - interval.startTime;
                        float activeTime = currentTime - interval.startTime;
                        float progress = Mathf.Clamp01(activeTime / intervalDuration);

                        float sunRadius = Mathf.Max(1f, sunPixelCount) * 1f; // Use per-strip pixel count
                        float virtualLength = totalLEDs + 2 * sunRadius;
                        float sunPositionContinuous = (virtualLength * (1f - progress)) - sunRadius;
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
                        break;
                    }
                }

                StringBuilder frameHexBuilder = new StringBuilder(totalLEDs * hexPerPixel);
                Color32 sunColor = stripDataManager.GetSunColorForStrip(stripIndex, currentSunMode);

                for (int i = 0; i < totalLEDs; i++)
                {
                    Color32 pixelColor = Color.black;
                    if (foundInterval) // Only apply sun color if inside an interval
                    {
                        pixelColor.r = (byte)(sunColor.r * pixelBrightness[i] / 255f);
                        pixelColor.g = (byte)(sunColor.g * pixelBrightness[i] / 255f);
                        pixelColor.b = (byte)(sunColor.b * pixelBrightness[i] / 255f);
                    }

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
                            pixelHex = "00";
                            break;
                    }
                    frameHexBuilder.Append(pixelHex);
                }

                string optimizedHex = OptimizeHexStringForBaking(frameHexBuilder.ToString(), blackHexValue, hexPerPixel);
                stateData.hexFrames.Add(optimizedHex);
            }

            return stateData;
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
                    InvalidateCache(CacheInvalidationReason.AppStateChange);
                }
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

            int stateKey = GenerateStateKey(_currentAppState);

            if (preloadedStateData.TryGetValue(stripIndex, out var stateInfoDict) &&
                stateInfoDict.TryGetValue(stateKey, out BakedSunStateData stateData))
            {
                int frameCount = stateData.frameCount;
                float frameDuration = stateData.frameDuration;

                if (frameCount <= 0 || frameDuration <= 0f || stateData.hexFrames == null || stateData.hexFrames.Count != frameCount)
                {
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

        private StringBuilder GetStringBuilderForStrip(int stripIndex)
        {
            if (!stringBuilderCache.ContainsKey(stripIndex))
            {
                stringBuilderCache[stripIndex] = new StringBuilder();
            }
            return stringBuilderCache[stripIndex];
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
                base.OnInspectorGUI();

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