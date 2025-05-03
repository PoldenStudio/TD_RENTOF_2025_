using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using System.Collections;

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

        private Coroutine _currentFadeCoroutine = null;
        private float _currentFadeBrightness = 1f;

        [SerializeField] private StripDataManager stripDataManager;
        [SerializeField] private ColorProcessor colorProcessor;

        private Dictionary<int, Dictionary<int, BakedSunStateData>> preloadedStateData = new();

        public enum SunMode
        {
            Warm,
            Cold,
            Gradient,
            BackGradient
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
            [Range(0f, 2f)]
            public float brightnessMultiplier = 1f;
            [Tooltip("Controls the blurriness of the sun edges. Higher values = more blurry edges.")]
            [Range(1f, 10f)]
            public float gaussianSpreadFactor = 3f;

            public override string ToString()
            {
                return $"CycleLength: {baseCycleLength}, Brightness: {brightnessMultiplier}, Gaussian: {gaussianSpreadFactor}, Intervals: {intervals.Count}";
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
            int currentStripVirtualPadding = stripDataManager.GetVirtualPaddingForStrip(stripIndex);
            int totalLEDsWithPadding = actualLEDs + currentStripVirtualPadding * 2;

            Debug.Log($"Pre-baking SunMovement data for Strip {stripIndex} ({actualLEDs} LEDs + {currentStripVirtualPadding * 2} virtual LEDs = {totalLEDsWithPadding} total) in {dataMode} mode...");

            int hexPerPixel = dataMode switch
            {
                DataMode.Monochrome1Color or DataMode.Monochrome2Color => 2,
                DataMode.RGB => 6,
                DataMode.RGBW => 8,
                _ => 2
            };
            string blackHexValue = new string('0', hexPerPixel);

            BakedSunStripData stripData = new BakedSunStripData { stripIndex = stripIndex };

            stripData.stateData.Add(PreBakeSunDataForStateAndMode(stripIndex, StateManager.AppState.Idle, totalLEDsWithPadding, actualLEDs, currentStripVirtualPadding, hexPerPixel, dataMode, blackHexValue, idleSunSettings));
            stripData.stateData.Add(PreBakeSunDataForStateAndMode(stripIndex, StateManager.AppState.Active, totalLEDsWithPadding, actualLEDs, currentStripVirtualPadding, hexPerPixel, dataMode, blackHexValue, activeSunSettings));

            stripData.stateData.RemoveAll(sd => sd.hexFrames == null || sd.hexFrames.Count == 0);

            Debug.Log($"[SunManager] Finished pre-baking data for Strip {stripIndex}. {stripData.stateData.Count} states baked.");
            return stripData;
        }

        private BakedSunStateData PreBakeSunDataForStateAndMode(
            int stripIndex,
            StateManager.AppState appState,
            int totalLEDsWithPadding,
            int actualLEDs,
            int virtualPaddingForThisStrip,
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

            byte[] pixelBrightness = new byte[totalLEDsWithPadding];
            bool previouslyActive = false;

            for (int frame = 0; frame < frameCount; frame++)
            {
                float currentTime = (frame * frameDuration) % totalCycleLength;

                bool isActiveTime = false;
                SunMode currentSunMode = SunMode.Warm;
                float currentIntervalStartPosition = 0f;
                float currentIntervalEndPosition = 0f;
                float currentIntervalStartTime = 0f;
                float currentIntervalDuration = 1f;

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
                    Array.Clear(pixelBrightness, 0, totalLEDsWithPadding);

                    float activeTime = currentTime - currentIntervalStartTime;
                    float intervalTimeProgress = Mathf.Clamp01(activeTime / currentIntervalDuration);
                    float currentNormalizedStripPos = Mathf.Lerp(currentIntervalStartPosition, currentIntervalEndPosition, intervalTimeProgress);

                    int stripPixelCount = stripDataManager.GetSunPixelCountForStrip(stripIndex);
                    float sunRadius = Mathf.Max(1f, stripPixelCount) * 0.5f;
                    float virtualLength = totalLEDsWithPadding + 2 * sunRadius;
                    float sunPositionContinuous = (virtualLength * (1f - currentNormalizedStripPos)) - sunRadius;

                    float sigma = sunRadius / Mathf.Max(0.1f, settingsRef.gaussianSpreadFactor);
                    float denominator = 2f * sigma * sigma;
                    if (denominator <= 0f) denominator = 0.001f;

                    for (int i = 0; i < totalLEDsWithPadding; i++)
                    {
                        float distance = Mathf.Abs(i - sunPositionContinuous);
                        float brightnessFactor = Mathf.Exp(-(distance * distance) / denominator);
                        float finalBrightnessFactor = brightnessFactor * settingsRef.brightnessMultiplier;
                        byte brightnessValue = (byte)Mathf.Clamp(255f * finalBrightnessFactor, 0, 255);
                        pixelBrightness[i] = Math.Max(pixelBrightness[i], brightnessValue);
                    }

                    Color32 intervalPrimaryColor = stripDataManager.GetSunColorForStrip(stripIndex, currentSunMode);
                    Color32 intervalSecondaryColor = stripDataManager.GetSunEndColorForStrip(stripIndex, currentSunMode);
                    Color32 baseSunColorForThisFrame;

                    if (currentSunMode == SunMode.Gradient || currentSunMode == SunMode.BackGradient)
                    {
                        baseSunColorForThisFrame = Color32.Lerp(intervalPrimaryColor, intervalSecondaryColor, intervalTimeProgress);
                    }
                    else
                    {
                        baseSunColorForThisFrame = intervalPrimaryColor;
                    }

                    StringBuilder frameHexBuilder = new(totalLEDsWithPadding * hexPerPixel);
                    Color32 pixelColor = new Color32();

                    for (int i = 0; i < totalLEDsWithPadding; i++)
                    {
                        pixelColor.r = (byte)(baseSunColorForThisFrame.r * pixelBrightness[i] / 255f);
                        pixelColor.g = (byte)(baseSunColorForThisFrame.g * pixelBrightness[i] / 255f);
                        pixelColor.b = (byte)(baseSunColorForThisFrame.b * pixelBrightness[i] / 255f);
                        pixelColor.a = baseSunColorForThisFrame.a;

                        string pixelHex;
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
                    string trimmedHexString = TrimVirtualPadding(fullHexString, hexPerPixel, virtualPaddingForThisStrip, actualLEDs);
                    frameDataToAdd = OptimizeHexStringForBaking(trimmedHexString, blackHexValue, hexPerPixel);
                    previouslyActive = true;
                }
                else
                {
                    if (previouslyActive) frameDataToAdd = "clear";
                    else frameDataToAdd = "";
                    previouslyActive = false;
                }
                stateData.hexFrames.Add(frameDataToAdd);
            }
            Debug.Log($"Baked state {appState} for strip {stripIndex}: {frameCount} frames, duration {frameDuration:F3}s");
            return stateData;
        }

        private string TrimVirtualPadding(string fullHexString, int hexPerPixel, int paddingSize, int actualLEDs)
        {
            int startIndex = paddingSize * hexPerPixel;
            int length = actualLEDs * hexPerPixel;

            if (startIndex + length > fullHexString.Length)
            {
                length = Mathf.Max(0, fullHexString.Length - startIndex);
            }

            if (startIndex < 0 || length < 0 || startIndex > fullHexString.Length)
            {
                return "";
            }
            return fullHexString.Substring(startIndex, length);
        }

        private string OptimizeHexStringForBaking(string hexString, string blackHex, int hexPerPixel)
        {
            return hexString;
        }

        public void SetAppState(StateManager.AppState state)
        {
            if (_currentAppState != state)
            {

                _currentAppState = state; // Смена состояния
                ResetSunMovementPhase();  // Сброс фазы
                InvalidateCache(CacheInvalidationReason.AppStateChange); // Инвалидация кэша
            }
        }

        private void ResetSunMovementPhase()
        {
            _sunMovementPhase = 0f;
        }

        public void StartSunFadeOut(float duration)
        {
            if (_currentFadeCoroutine != null)
            {
                StopCoroutine(_currentFadeCoroutine);
            }
            _currentFadeCoroutine = StartCoroutine(FadeOutRoutine(duration));
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            float startTime = Time.time;
            float initialBrightness = _currentFadeBrightness;
            while (Time.time - startTime < duration)
            {
                _currentFadeBrightness = Mathf.Lerp(initialBrightness, 0f, (Time.time - startTime) / duration);
                yield return null;
            }
            _currentFadeBrightness = 0f;
            _currentFadeCoroutine = null;
        }

        public void StartSunFadeIn(float duration)
        {
            if (_currentFadeCoroutine != null)
            {
                StopCoroutine(_currentFadeCoroutine);
            }
            _currentFadeCoroutine = StartCoroutine(FadeInRoutine(duration));
        }

        private IEnumerator FadeInRoutine(float duration)
        {
            float startTime = Time.time;
            float initialBrightness = _currentFadeBrightness;
            while (Time.time - startTime < duration)
            {
                _currentFadeBrightness = Mathf.Lerp(initialBrightness, 1f, (Time.time - startTime) / duration);
                yield return null;
            }
            _currentFadeBrightness = 1f;
            _currentFadeCoroutine = null;
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
            SunMovementSettings settingsRef = GetCurrentSunSettingsForRuntime(_currentAppState);
            if (settingsRef == null || settingsRef.baseCycleLength <= 0f) return;

            float currentSpeed = CurrentSunSpeed;
            if (Mathf.Approximately(currentSpeed, 0f))
            {
                return;
            }

            _sunMovementPhase += (Time.deltaTime * currentSpeed) / settingsRef.baseCycleLength;
            _sunMovementPhase = (_sunMovementPhase % 1.0f + 1.0f) % 1.0f;

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
            int cacheKey = GetCacheKey(stripIndex, _currentAppState);
            if (hexCache.TryGetValue(cacheKey, out string cachedHex) &&
                lastUpdateTime.TryGetValue(cacheKey, out float lastUpdate) &&
                Time.time - lastUpdate < cacheLifetime &&
                !Mathf.Approximately(CurrentSunSpeed, 0f))
            {
                return ApplyFadeBrightness(cachedHex);
            }

            if (Mathf.Approximately(CurrentSunSpeed, 0f) && hexCache.TryGetValue(cacheKey, out string staticCachedHex))
            {
                return ApplyFadeBrightness(staticCachedHex);
            }

            if (stripIndex < 0 || stripIndex >= stripManager.currentSunModes.Count) return "";
            int stateKey = GenerateStateKey(_currentAppState);

            if (preloadedStateData.TryGetValue(stripIndex, out var stateInfoDict) &&
                stateInfoDict.TryGetValue(stateKey, out BakedSunStateData stateData))
            {
                if (stateData.frameCount <= 0 || stateData.frameDuration <= 0f || stateData.hexFrames == null || stateData.hexFrames.Count != stateData.frameCount)
                {
                    Debug.LogError($"Inconsistent baked data for strip {stripIndex}, state {stateKey}. FrameCount: {stateData.frameCount}, FrameDuration: {stateData.frameDuration}, HexFrames: {stateData.hexFrames?.Count}");
                    return "";
                }

                SunMovementSettings currentSettings = GetCurrentSunSettingsForRuntime(_currentAppState);
                if (currentSettings == null || currentSettings.baseCycleLength <= 0f) return "";

                float currentCycleTime = _sunMovementPhase * currentSettings.baseCycleLength;
                if (currentCycleTime < 0) currentCycleTime = 0;

                int currentFrame = Mathf.FloorToInt(currentCycleTime / stateData.frameDuration);
                currentFrame = Mathf.Clamp(currentFrame, 0, stateData.frameCount - 1);

                string hexData = stateData.hexFrames[currentFrame];
                hexCache[cacheKey] = hexData;
                lastUpdateTime[cacheKey] = Time.time;
                return ApplyFadeBrightness(hexData);
            }
            return "";
        }

        private string ApplyFadeBrightness(string hexData)
        {
            if (string.IsNullOrEmpty(hexData) || _currentFadeBrightness >= 0.99f)
                return hexData;

            if (hexData == "clear")
                return hexData;

            int hexPerPixel = hexData.Length % 2 == 0 ? 2 : 6;
            StringBuilder fadedHex = new StringBuilder(hexData.Length);

            for (int i = 0; i < hexData.Length; i += hexPerPixel)
            {
                string pixelHex = hexData.Substring(i, hexPerPixel);
                int colorValue = Convert.ToInt32(pixelHex, 16);
                int fadedValue = (int)(colorValue * _currentFadeBrightness);
                fadedValue = Mathf.Clamp(fadedValue, 0, colorValue);
                string fadedHexStr = fadedValue.ToString("X" + hexPerPixel.ToString());
                fadedHex.Append(fadedHexStr.PadLeft(hexPerPixel, '0'));
            }
            return fadedHex.ToString();
        }

        private int GenerateStateKey(StateManager.AppState state) => (int)state;
        private int GetCacheKey(int stripIndex, StateManager.AppState appState) => HashCode.Combine(stripIndex, appState);

        public void OnSunSettingsChanged()
        {
            InvalidateCache(CacheInvalidationReason.SunSettingsChange);
        }

        private void ClearSunCache()
        {
            hexCache.Clear();
            lastUpdateTime.Clear();
        }


#if UNITY_EDITOR
[CustomEditor(typeof(SunManager))]
    public class SunManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            SunManager sunManager = (SunManager)target;
            if (GUILayout.Button("Invalidate Cache & Reset Phase"))
            {
                sunManager.ResetSunMovementPhase();
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