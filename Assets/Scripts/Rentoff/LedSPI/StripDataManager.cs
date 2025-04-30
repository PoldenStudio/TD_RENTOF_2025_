using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static LEDControl.SunManager;

namespace LEDControl
{
    [Serializable]
    public class StripSegmentColors
    {
        [Tooltip("Цвета сегментов для данной ленты")]
        public List<Color32> segments = new();
    }

    [Serializable]
    public class StripSettings
    {
        [Tooltip("Яркость ленты (0-1)")]
        [Range(0f, 1f)]
        public float brightness = 1f;

        [Tooltip("Гамма-коррекция для ленты")]
        [Range(0.1f, 5f)]
        public float gammaValue = 2.2f;

        [Tooltip("Включена ли гамма-коррекция для ленты")]
        public bool enableGammaCorrection = false;
    }

    [Serializable]
    public class SunColorSettings
    {
        public Color32 warmColor = new Color32(255, 180, 0, 255);
        public Color32 coldColor = new Color32(0, 150, 255, 255);
        public Color32 gradientStartColor = new Color32(255, 0, 0, 255);
        public Color32 gradientEndColor = new Color32(0, 0, 255, 255);
    }

    [Serializable]
    public class SunStripSettings
    {
        [Tooltip("Number of LEDs the sun occupies for this specific strip.")]
        public int pixelCount = 10;
    }

    [Serializable]
    public class StripPortAssignment
    {
        public int portIndex = 0;
    }

    [ExecuteInEditMode]
    public class StripDataManager : MonoBehaviour
    {
        public List<int> totalLEDsPerStrip = new() { 200, 200, 200, 200 };

        [Header("Segment Settings")]
        [Tooltip("Number of LEDs per segment for each strip.")]
        public List<int> ledsPerSegment = new() { 1, 1, 1, 1 };

        [Header("Virtual Padding Settings")]
        [Tooltip("Number of virtual LEDs to add on each end of the strip for sun movement pre-baking.")]
        public List<int> virtualPaddingsPerStrip = new() { 5, 5, 5, 5 };


        [Header("Data Mode")]
        public List<bool> stripEnabled = new() { true, true, true, true };
        public List<DataMode> currentDataModes = new() { DataMode.Monochrome1Color, DataMode.RGBW, DataMode.RGB, DataMode.Monochrome1Color };

        [Header("Display Settings")]
        public List<DisplayMode> currentDisplayModes = new() { DisplayMode.GlobalColor, DisplayMode.GlobalColor, DisplayMode.GlobalColor, DisplayMode.GlobalColor };
        public List<SunMode> currentSunModes = new() { SunMode.Warm, SunMode.Warm, SunMode.Warm, SunMode.Warm };

        [Header("Strip-Specific Settings")]
        public List<StripSettings> stripSettings = new();
        public List<StripPortAssignment> stripPortAssignments = new();

        [Header("Sun Color Settings (for strips in SunMovement mode)")]
        public List<SunColorSettings> sunColorSettings = new();

        [Header("Sun Strip Settings (for strips in SunMovement mode)")]
        public List<SunStripSettings> sunStripSettings = new();

        [Header("Monochrome Settings")]
        public List<MonochromeStripSettings> monochromeStripSettings = new()
        {
            new MonochromeStripSettings() { globalColor = new Color32(255, 255, 255, 255), synthColor = new Color32(255, 255, 255, 255) },
            new MonochromeStripSettings() { globalColor = new Color32(255, 255, 255, 255), synthColor = new Color32(255, 255, 255, 255) }
        };

        [Header("RGBW/RGB Settings")]
        public List<RGBStripSettings> rgbStripSettings = new()
        {
            new RGBStripSettings() { globalColor = new Color32(255, 0, 0, 255), synthColor = new Color32(255, 255, 255, 255) },
            new RGBStripSettings() { globalColor = new Color32(0, 255, 0, 255), synthColor = new Color32(255, 255, 255, 255) }
        };

        [Header("Segment Mode Settings")]
        public List<StripSegmentColors> stripSegmentColors = new();

        [Header("Touch Panel Settings")]
        public int touchPanelCols = 10;
        public int touchPanelOffset = 0;

        private List<MonochromeStripSettings> previousMonochromeStripSettings = new();
        private List<RGBStripSettings> previousRGBStripSettings = new();
        private List<List<Color32>> previousSegmentColors = new();
        private List<DisplayMode> previousDisplayModes = new();
        private List<DataMode> previousDataModes = new();
        private List<bool> previousStripEnabled = new();
        private List<StripSettings> previousStripSettings = new();
        private List<SunMode> previousSunModes = new();
        private List<SunColorSettings> previousSunColorSettings = new();
        private List<SunStripSettings> previousSunStripSettings = new();
        private List<int> previousLedsPerSegment = new();
        private List<StripPortAssignment> previousStripPortAssignments = new();
        private List<int> previousVirtualPaddingsPerStrip = new();


        private void OnValidate()
        {
            InitializeStripData();
        }

        private void OnEnable()
        {
            InitializeStripData();
        }

        public int GetTotalSegments(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= totalLEDsPerStrip.Count || stripIndex >= ledsPerSegment.Count || ledsPerSegment[stripIndex] <= 0)
                return 0;
            return totalLEDsPerStrip[stripIndex] / ledsPerSegment[stripIndex];
        }


        public void InitializeStripData()
        {
            int stripCount = totalLEDsPerStrip.Count;

            stripEnabled = EnsureListSize(stripEnabled, stripCount, true);
            currentDataModes = EnsureListSize(currentDataModes, stripCount, DataMode.Monochrome1Color);
            currentDisplayModes = EnsureListSize(currentDisplayModes, stripCount, DisplayMode.GlobalColor);
            currentSunModes = EnsureListSize(currentSunModes, stripCount, SunMode.Warm);
            stripSettings = EnsureListSize(stripSettings, stripCount, () => new StripSettings());
            stripPortAssignments = EnsureListSize(stripPortAssignments, stripCount, () => new StripPortAssignment());
            sunColorSettings = EnsureListSize(sunColorSettings, stripCount, () => new SunColorSettings());
            sunStripSettings = EnsureListSize(sunStripSettings, stripCount, () => new SunStripSettings());
            ledsPerSegment = EnsureListSize(ledsPerSegment, stripCount, 1);
            virtualPaddingsPerStrip = EnsureListSize(virtualPaddingsPerStrip, stripCount, 5);
            stripSegmentColors = EnsureListSize(stripSegmentColors, stripCount, () => new StripSegmentColors());

            for (int stripIndex = 0; stripIndex < stripCount; stripIndex++)
            {
                int requiredSegmentCount = GetTotalSegments(stripIndex);
                if (stripSegmentColors[stripIndex].segments == null || stripSegmentColors[stripIndex].segments.Count != requiredSegmentCount)
                {
                    List<Color32> newSegmentColors = new List<Color32>(new Color32[requiredSegmentCount]);
                    stripSegmentColors[stripIndex].segments = newSegmentColors;
                }
            }

            int monochromeCount = currentDataModes.Count(x => x == DataMode.Monochrome1Color || x == DataMode.Monochrome2Color);
            monochromeStripSettings = EnsureListSize(monochromeStripSettings, monochromeCount, () => new MonochromeStripSettings { globalColor = Color.white, synthColor = Color.white });

            int rgbCount = currentDataModes.Count(x => x == DataMode.RGB || x == DataMode.RGBW);
            rgbStripSettings = EnsureListSize(rgbStripSettings, rgbCount, () => new RGBStripSettings { globalColor = Color.red, synthColor = Color.white });

            CachePreviousValues();
        }

        private List<T> EnsureListSize<T>(List<T> list, int targetSize, T defaultValue)
        {
            while (list.Count < targetSize) list.Add(defaultValue);
            while (list.Count > targetSize) list.RemoveAt(list.Count - 1);
            return list;
        }

        private List<T> EnsureListSize<T>(List<T> list, int targetSize, Func<T> creator) where T : class
        {
            while (list.Count < targetSize) list.Add(creator());
            while (list.Count > targetSize) list.RemoveAt(list.Count - 1);
            return list;
        }


        public int GetPortIndexForStrip(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripPortAssignments.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}");
                return 0;
            }
            return stripPortAssignments[stripIndex].portIndex;
        }

        public int GetVirtualPaddingForStrip(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= virtualPaddingsPerStrip.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index for virtual padding: {stripIndex}");
                return 5; // Default fallback
            }
            return virtualPaddingsPerStrip[stripIndex];
        }


        public Color32 GetDefaultColor(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= currentDataModes.Count) return Color.black;
            switch (currentDataModes[stripIndex])
            {
                case DataMode.Monochrome1Color:
                case DataMode.Monochrome2Color:
                    int monoIndex = GetMonochromeStripIndex(stripIndex);
                    if (monoIndex >= 0 && monoIndex < monochromeStripSettings.Count) return monochromeStripSettings[monoIndex].globalColor;
                    return Color.white;
                case DataMode.RGBW:
                case DataMode.RGB:
                    int rgbIndex = GetRGBStripIndex(stripIndex);
                    if (rgbIndex >= 0 && rgbIndex < rgbStripSettings.Count) return rgbStripSettings[rgbIndex].globalColor;
                    return Color.red;
                default: return Color.black;
            }
        }

        public int GetMonochromeStripIndex(int stripIndex)
        {
            int monoCount = 0;
            for (int i = 0; i < currentDataModes.Count && i <= stripIndex; i++)
            {
                if (currentDataModes[i] == DataMode.Monochrome1Color || currentDataModes[i] == DataMode.Monochrome2Color)
                {
                    if (i == stripIndex) return monoCount;
                    monoCount++;
                }
            }
            return -1;
        }

        public int GetRGBStripIndex(int stripIndex)
        {
            int rgbCount = 0;
            for (int i = 0; i < currentDataModes.Count && i <= stripIndex; i++)
            {
                if (currentDataModes[i] == DataMode.RGB || currentDataModes[i] == DataMode.RGBW)
                {
                    if (i == stripIndex) return rgbCount;
                    rgbCount++;
                }
            }
            return -1;
        }

        public void SetSegmentColor(int stripIndex, int segmentIndex, Color32 color, bool debug = false)
        {
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count) { Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}"); return; }
            if (segmentIndex >= 0 && segmentIndex < GetTotalSegments(stripIndex))
            {
                if (stripIndex < currentDataModes.Count && (currentDataModes[stripIndex] == DataMode.Monochrome1Color || currentDataModes[stripIndex] == DataMode.Monochrome2Color))
                {
                    float lum = (color.r * 0.299f + color.g * 0.587f + color.b * 0.114f);
                    color = new Color32((byte)lum, (byte)lum, (byte)lum, 255);
                }
                stripSegmentColors[stripIndex].segments[segmentIndex] = color;
                if (debug) Debug.Log($"Strip {stripIndex}, Segment {segmentIndex} color set to {color}");
            }
            else Debug.LogError($"[StripDataManager] Invalid segment index: {segmentIndex} for strip {stripIndex}");
        }

        public Color32 GetSegmentColor(int stripIndex, int segmentIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count) { Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}"); return GetDefaultColor(stripIndex); }
            if (segmentIndex >= 0 && segmentIndex < GetTotalSegments(stripIndex)) return stripSegmentColors[stripIndex].segments[segmentIndex];
            else { Debug.LogError($"[StripDataManager] Invalid segment index: {segmentIndex} for strip {stripIndex}"); return GetDefaultColor(stripIndex); }
        }

        public List<Color32> GetSegmentColors(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count) { Debug.LogError($"[StripDataManager] Invalid strip index: {stripIndex}"); return new List<Color32>(); }
            return new List<Color32>(stripSegmentColors[stripIndex].segments);
        }

        public Color32 GetGlobalColorForStrip(int stripIndex, DataMode mode)
        {
            if (mode == DataMode.Monochrome1Color || mode == DataMode.Monochrome2Color)
            {
                int monoIndex = GetMonochromeStripIndex(stripIndex);
                if (monoIndex >= 0 && monoIndex < monochromeStripSettings.Count) return monochromeStripSettings[monoIndex].globalColor;
                return Color.white;
            }
            else if (mode == DataMode.RGBW || mode == DataMode.RGB)
            {
                int rgbIndex = GetRGBStripIndex(stripIndex);
                if (rgbIndex >= 0 && rgbIndex < rgbStripSettings.Count) return rgbStripSettings[rgbIndex].globalColor;
                return Color.red;
            }
            return Color.black;
        }

        public Color32 GetSunColorForStrip(int stripIndex, SunMode sunMode)
        {
            if (stripIndex < 0 || stripIndex >= sunColorSettings.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index for SunColor: {stripIndex}");
                return sunMode == SunMode.Warm ? new Color32(255, 180, 0, 255) : new Color32(0, 150, 255, 255);
            }
            var settings = sunColorSettings[stripIndex];
            return sunMode switch
            {
                SunMode.Warm => settings.warmColor,
                SunMode.Cold => settings.coldColor,
                SunMode.Gradient => settings.gradientStartColor,
                SunMode.BackGradient => settings.gradientEndColor,
                _ => settings.warmColor
            };
        }

        public Color32 GetSunEndColorForStrip(int stripIndex, SunMode sunMode)
        {
            if (stripIndex < 0 || stripIndex >= sunColorSettings.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index for SunEndColor: {stripIndex}");
                return sunMode == SunMode.Warm ? new Color32(255, 180, 0, 255) : new Color32(0, 150, 255, 255);
            }
            var settings = sunColorSettings[stripIndex];
            return sunMode switch
            {
                SunMode.Warm => settings.warmColor,
                SunMode.Cold => settings.coldColor,
                SunMode.Gradient => settings.gradientEndColor,
                SunMode.BackGradient => settings.gradientStartColor,
                _ => GetSunColorForStrip(stripIndex, sunMode) 
            };
        }


        public int GetSunPixelCountForStrip(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= sunStripSettings.Count)
            {
                Debug.LogError($"[StripDataManager] Invalid strip index for SunPixelCount: {stripIndex}");
                return 10;
            }
            return sunStripSettings[stripIndex].pixelCount;
        }

        public void CachePreviousValues()
        {
            previousMonochromeStripSettings = monochromeStripSettings.Select(s => new MonochromeStripSettings { globalColor = s.globalColor, synthColor = s.synthColor }).ToList();
            previousRGBStripSettings = rgbStripSettings.Select(s => new RGBStripSettings { globalColor = s.globalColor, synthColor = s.synthColor }).ToList();
            previousSegmentColors = stripSegmentColors.Select(sc => new List<Color32>(sc.segments)).ToList();
            previousDisplayModes = new List<DisplayMode>(currentDisplayModes);
            previousDataModes = new List<DataMode>(currentDataModes);
            previousStripEnabled = new List<bool>(stripEnabled);
            previousStripSettings = stripSettings.Select(s => new StripSettings { brightness = s.brightness, gammaValue = s.gammaValue, enableGammaCorrection = s.enableGammaCorrection }).ToList();
            previousSunModes = new List<SunMode>(currentSunModes);
            previousSunColorSettings = sunColorSettings.Select(s => new SunColorSettings { warmColor = s.warmColor, coldColor = s.coldColor, gradientStartColor = s.gradientStartColor, gradientEndColor = s.gradientEndColor }).ToList();
            previousSunStripSettings = sunStripSettings.Select(s => new SunStripSettings { pixelCount = s.pixelCount }).ToList();
            previousLedsPerSegment = new List<int>(ledsPerSegment);
            previousVirtualPaddingsPerStrip = new List<int>(virtualPaddingsPerStrip);
            previousStripPortAssignments = stripPortAssignments.Select(p => new StripPortAssignment { portIndex = p.portIndex }).ToList();
        }

        public bool CheckForChanges()
        {
            if (stripSegmentColors.Count != previousSegmentColors.Count ||
                monochromeStripSettings.Count != previousMonochromeStripSettings.Count ||
                rgbStripSettings.Count != previousRGBStripSettings.Count ||
                currentDisplayModes.Count != previousDisplayModes.Count ||
                currentDataModes.Count != previousDataModes.Count ||
                stripEnabled.Count != previousStripEnabled.Count ||
                stripSettings.Count != previousStripSettings.Count ||
                sunColorSettings.Count != previousSunColorSettings.Count ||
                sunStripSettings.Count != previousSunStripSettings.Count ||
                ledsPerSegment.Count != previousLedsPerSegment.Count ||
                virtualPaddingsPerStrip.Count != previousVirtualPaddingsPerStrip.Count ||
                stripPortAssignments.Count != previousStripPortAssignments.Count)
                return true;

            for (int stripIndex = 0; stripIndex < stripSegmentColors.Count; stripIndex++)
            {
                if (stripSegmentColors[stripIndex].segments.Count != previousSegmentColors[stripIndex].Count ||
                    !stripSegmentColors[stripIndex].segments.SequenceEqual(previousSegmentColors[stripIndex])) return true;
            }

            for (int i = 0; i < monochromeStripSettings.Count; i++)
            {
                if (monochromeStripSettings[i].globalColor.IsDifferent(previousMonochromeStripSettings[i].globalColor) ||
                    monochromeStripSettings[i].synthColor.IsDifferent(previousMonochromeStripSettings[i].synthColor)) return true;
            }

            for (int i = 0; i < rgbStripSettings.Count; i++)
            {
                if (rgbStripSettings[i].globalColor.IsDifferent(previousRGBStripSettings[i].globalColor) ||
                    rgbStripSettings[i].synthColor.IsDifferent(previousRGBStripSettings[i].synthColor)) return true;
            }

            for (int i = 0; i < sunColorSettings.Count; i++)
            {
                if (sunColorSettings[i].warmColor.IsDifferent(previousSunColorSettings[i].warmColor) ||
                    sunColorSettings[i].coldColor.IsDifferent(previousSunColorSettings[i].coldColor) ||
                    sunColorSettings[i].gradientStartColor.IsDifferent(previousSunColorSettings[i].gradientStartColor) ||
                    sunColorSettings[i].gradientEndColor.IsDifferent(previousSunColorSettings[i].gradientEndColor)) return true;
            }

            for (int i = 0; i < sunStripSettings.Count; i++)
            {
                if (sunStripSettings[i].pixelCount != previousSunStripSettings[i].pixelCount) return true;
            }

            if (!ledsPerSegment.SequenceEqual(previousLedsPerSegment) ||
                !virtualPaddingsPerStrip.SequenceEqual(previousVirtualPaddingsPerStrip) ||
                !currentDisplayModes.SequenceEqual(previousDisplayModes) ||
                !currentDataModes.SequenceEqual(previousDataModes) ||
                !stripEnabled.SequenceEqual(previousStripEnabled) ||
                !currentSunModes.SequenceEqual(previousSunModes)) return true;

            for (int i = 0; i < stripPortAssignments.Count; i++)
            {
                if (stripPortAssignments[i].portIndex != previousStripPortAssignments[i].portIndex) return true;
            }

            for (int i = 0; i < stripSettings.Count; i++)
            {
                if (stripSettings[i].brightness != previousStripSettings[i].brightness ||
                    stripSettings[i].gammaValue != previousStripSettings[i].gammaValue ||
                    stripSettings[i].enableGammaCorrection != previousStripSettings[i].enableGammaCorrection) return true;
            }
            return false;
        }

        public float GetStripBrightness(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSettings.Count) { Debug.LogError($"[StripDataManager] Invalid strip index for Brightness: {stripIndex}"); return 1f; }
            return stripSettings[stripIndex].brightness;
        }

        public float GetStripGamma(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSettings.Count) { Debug.LogError($"[StripDataManager] Invalid strip index for Gamma: {stripIndex}"); return 2.2f; }
            return stripSettings[stripIndex].gammaValue;
        }

        public bool IsGammaCorrectionEnabled(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSettings.Count) { Debug.LogError($"[StripDataManager] Invalid strip index for GammaEnabled: {stripIndex}"); return true; }
            return stripSettings[stripIndex].enableGammaCorrection;
        }

        public Color32 GetSynthColorForStrip(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= currentDataModes.Count) return Color.white;
            switch (currentDataModes[stripIndex])
            {
                case DataMode.Monochrome1Color:
                case DataMode.Monochrome2Color:
                    int monoIndex = GetMonochromeStripIndex(stripIndex);
                    if (monoIndex >= 0 && monoIndex < monochromeStripSettings.Count) return monochromeStripSettings[monoIndex].synthColor;
                    return Color.white;
                case DataMode.RGBW:
                case DataMode.RGB:
                    int rgbIndex = GetRGBStripIndex(stripIndex);
                    if (rgbIndex >= 0 && rgbIndex < rgbStripSettings.Count) return rgbStripSettings[rgbIndex].synthColor;
                    return Color.white;
                default: return Color.white;
            }
        }

        public SunMode GetSunMode(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= currentSunModes.Count) { Debug.LogError($"[StripDataManager] Invalid strip index for SunMode: {stripIndex}"); return SunMode.Warm; }
            return currentSunModes[stripIndex];
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LEDControl.StripDataManager))]
public class StripDataManagerEditor : Editor
{
    private bool[] stripFoldouts;
    private GUIStyle headerStyle;
    private GUIStyle stripHeaderStyle;
    private GUIStyle boxStyle;

    private void OnEnable()
    {
        LEDControl.StripDataManager manager = (LEDControl.StripDataManager)target;
        if (manager != null && manager.totalLEDsPerStrip != null)
        {
            int stripCount = manager.totalLEDsPerStrip.Count;
            stripFoldouts = new bool[stripCount];
        }
        else
        {
            stripFoldouts = Array.Empty<bool>();
        }
    }

    public override void OnInspectorGUI()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, margin = new RectOffset(0, 0, 10, 5) };
            stripHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, margin = new RectOffset(0, 0, 5, 0) };
            boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10), margin = new RectOffset(0, 0, 5, 5) };
        }

        LEDControl.StripDataManager manager = (LEDControl.StripDataManager)target;
        serializedObject.Update();

        EditorGUILayout.LabelField("Общие настройки", headerStyle);
        EditorGUI.indentLevel++;
        SerializedProperty ledsPerStripProperty = serializedObject.FindProperty("totalLEDsPerStrip");
        EditorGUILayout.PropertyField(ledsPerStripProperty, new GUIContent("Количество диодов на лентах"));

        if (ledsPerStripProperty.arraySize != (stripFoldouts?.Length ?? 0))
        {
            manager.InitializeStripData();
            serializedObject.Update();
            if (stripFoldouts == null || stripFoldouts.Length != ledsPerStripProperty.arraySize)
            {
                Array.Resize(ref stripFoldouts, ledsPerStripProperty.arraySize);
            }
        }

        SerializedProperty virtualPaddingsProperty = serializedObject.FindProperty("virtualPaddingsPerStrip");
        EditorGUILayout.PropertyField(virtualPaddingsProperty, new GUIContent("Виртуальные отступы (на ленту)"));

        SerializedProperty ledsPerSegmentProperty = serializedObject.FindProperty("ledsPerSegment");
        EditorGUILayout.PropertyField(ledsPerSegmentProperty, new GUIContent("Диодов в сегменте (на ленту)"));

        SerializedProperty touchColsProperty = serializedObject.FindProperty("touchPanelCols");
        EditorGUILayout.PropertyField(touchColsProperty, new GUIContent("Панелей на тач-панели"));
        SerializedProperty touchOffsetProperty = serializedObject.FindProperty("touchPanelOffset");
        EditorGUILayout.PropertyField(touchOffsetProperty, new GUIContent("Смещение тач-панели"));
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Настройки лент", headerStyle);
        int stripCount = ledsPerStripProperty.arraySize; // Use property's arraySize for consistency

        SerializedProperty stripEnabledProperty = serializedObject.FindProperty("stripEnabled");
        SerializedProperty dataModesProperty = serializedObject.FindProperty("currentDataModes");
        SerializedProperty displayModesProperty = serializedObject.FindProperty("currentDisplayModes");
        SerializedProperty sunModesProperty = serializedObject.FindProperty("currentSunModes");
        SerializedProperty stripSettingsProperty = serializedObject.FindProperty("stripSettings");
        SerializedProperty sunColorSettingsProperty = serializedObject.FindProperty("sunColorSettings");
        SerializedProperty sunStripSettingsProperty = serializedObject.FindProperty("sunStripSettings");
        SerializedProperty segmentColorsProperty = serializedObject.FindProperty("stripSegmentColors");
        SerializedProperty stripPortAssignmentsProperty = serializedObject.FindProperty("stripPortAssignments");


        for (int stripIndex = 0; stripIndex < stripCount; stripIndex++)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.BeginHorizontal();
            if (stripFoldouts.Length > stripIndex) // Ensure foldout array is large enough
            {
                stripFoldouts[stripIndex] = EditorGUILayout.Foldout(stripFoldouts[stripIndex], $" Лента #{stripIndex + 1}", true, stripHeaderStyle);
            }
            else
            {
                EditorGUILayout.LabelField($" Лента #{stripIndex + 1}", stripHeaderStyle); // Fallback if array is too small
            }

            if (stripEnabledProperty.arraySize > stripIndex)
            {
                SerializedProperty enabledProp = stripEnabledProperty.GetArrayElementAtIndex(stripIndex);
                EditorGUILayout.PropertyField(enabledProp, GUIContent.none, GUILayout.Width(20));
            }
            EditorGUILayout.EndHorizontal();

            if (stripFoldouts.Length > stripIndex && stripFoldouts[stripIndex])
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck(); // Start checking for changes

                if (dataModesProperty.arraySize > stripIndex)
                {
                    SerializedProperty dataModeProp = dataModesProperty.GetArrayElementAtIndex(stripIndex);
                    EditorGUILayout.PropertyField(dataModeProp, new GUIContent("Тип ленты"));
                }
                if (displayModesProperty.arraySize > stripIndex)
                {
                    SerializedProperty displayModeProp = displayModesProperty.GetArrayElementAtIndex(stripIndex);
                    EditorGUILayout.PropertyField(displayModeProp, new GUIContent("Режим отображения"));
                }
                if (stripPortAssignmentsProperty.arraySize > stripIndex)
                {
                    SerializedProperty portAssignmentProp = stripPortAssignmentsProperty.GetArrayElementAtIndex(stripIndex).FindPropertyRelative("portIndex");
                    EditorGUILayout.PropertyField(portAssignmentProp, new GUIContent("Порт"));
                }


                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    manager.InitializeStripData();
                    serializedObject.Update();
                }

                SerializedProperty currentDataModeProp = dataModesProperty.arraySize > stripIndex ? dataModesProperty.GetArrayElementAtIndex(stripIndex) : null;
                SerializedProperty currentDisplayModeProp = displayModesProperty.arraySize > stripIndex ? displayModesProperty.GetArrayElementAtIndex(stripIndex) : null;

                LEDControl.DisplayMode displayMode = currentDisplayModeProp != null ? (LEDControl.DisplayMode)currentDisplayModeProp.enumValueIndex : default;
                LEDControl.DataMode dataMode = currentDataModeProp != null ? (LEDControl.DataMode)currentDataModeProp.enumValueIndex : default;


                if (stripSettingsProperty.arraySize > stripIndex)
                {
                    SerializedProperty stripSettingsProp = stripSettingsProperty.GetArrayElementAtIndex(stripIndex);
                    SerializedProperty brightnessProp = stripSettingsProp.FindPropertyRelative("brightness");
                    SerializedProperty gammaValueProp = stripSettingsProp.FindPropertyRelative("gammaValue");
                    SerializedProperty enableGammaProp = stripSettingsProp.FindPropertyRelative("enableGammaCorrection");

                    EditorGUILayout.PropertyField(brightnessProp, new GUIContent("Яркость ленты"));
                    EditorGUILayout.PropertyField(enableGammaProp, new GUIContent("Включить гамма-коррекцию"));
                    if (enableGammaProp.boolValue) EditorGUILayout.PropertyField(gammaValueProp, new GUIContent("Значение гаммы"));
                }

                if (ledsPerSegmentProperty.arraySize > stripIndex)
                {
                    SerializedProperty segmentSettingProp = ledsPerSegmentProperty.GetArrayElementAtIndex(stripIndex);
                    EditorGUILayout.PropertyField(segmentSettingProp, new GUIContent("Диодов в сегменте"));
                }
                if (virtualPaddingsProperty.arraySize > stripIndex)
                {
                    SerializedProperty virtualPaddingProp = virtualPaddingsProperty.GetArrayElementAtIndex(stripIndex);
                    EditorGUILayout.PropertyField(virtualPaddingProp, new GUIContent("Виртуальные отступы"));
                }


                if (displayMode == LEDControl.DisplayMode.GlobalColor)
                {
                    if (dataMode == LEDControl.DataMode.Monochrome1Color || dataMode == LEDControl.DataMode.Monochrome2Color)
                    {
                        int monoIndex = manager.GetMonochromeStripIndex(stripIndex);
                        SerializedProperty monochromeSettingsArray = serializedObject.FindProperty("monochromeStripSettings");
                        if (monoIndex >= 0 && monoIndex < monochromeSettingsArray.arraySize)
                        {
                            SerializedProperty monoProp = monochromeSettingsArray.GetArrayElementAtIndex(monoIndex);
                            SerializedProperty colorProp = monoProp.FindPropertyRelative("globalColor");
                            EditorGUILayout.PropertyField(colorProp, new GUIContent("Глобальный цвет (монохром)"));
                        }
                    }
                    else if (dataMode == LEDControl.DataMode.RGB || dataMode == LEDControl.DataMode.RGBW)
                    {
                        int rgbIndex = manager.GetRGBStripIndex(stripIndex);
                        SerializedProperty rgbSettingsArray = serializedObject.FindProperty("rgbStripSettings");
                        if (rgbIndex >= 0 && rgbIndex < rgbSettingsArray.arraySize)
                        {
                            SerializedProperty rgbProp = rgbSettingsArray.GetArrayElementAtIndex(rgbIndex);
                            SerializedProperty colorProp = rgbProp.FindPropertyRelative("globalColor");
                            EditorGUILayout.PropertyField(colorProp, new GUIContent("Глобальный цвет (RGB)"));
                        }
                    }
                }
                else if (displayMode == LEDControl.DisplayMode.SunMovement)
                {
                    if (sunModesProperty.arraySize > stripIndex)
                    {
                        SerializedProperty sunModeProp = sunModesProperty.GetArrayElementAtIndex(stripIndex);
                        EditorGUILayout.PropertyField(sunModeProp, new GUIContent("Режим солнца"));
                    }

                    if (sunColorSettingsProperty.arraySize > stripIndex)
                    {
                        SerializedProperty sunColorSettingsElementProp = sunColorSettingsProperty.GetArrayElementAtIndex(stripIndex);
                        SerializedProperty warmColorProp = sunColorSettingsElementProp.FindPropertyRelative("warmColor");
                        SerializedProperty coldColorProp = sunColorSettingsElementProp.FindPropertyRelative("coldColor");
                        SerializedProperty gradientStartColorProp = sunColorSettingsElementProp.FindPropertyRelative("gradientStartColor");
                        SerializedProperty gradientEndColorProp = sunColorSettingsElementProp.FindPropertyRelative("gradientEndColor");

                        EditorGUILayout.PropertyField(warmColorProp, new GUIContent("Цвет тёплого солнца"));
                        EditorGUILayout.PropertyField(coldColorProp, new GUIContent("Цвет холодного солнца"));
                        EditorGUILayout.PropertyField(gradientStartColorProp, new GUIContent("Начальный цвет градиента"));
                        EditorGUILayout.PropertyField(gradientEndColorProp, new GUIContent("Конечный цвет градиента"));
                    }

                    if (sunStripSettingsProperty.arraySize > stripIndex)
                    {
                        SerializedProperty sunStripSettingsElementProp = sunStripSettingsProperty.GetArrayElementAtIndex(stripIndex);
                        SerializedProperty pixelCountProp = sunStripSettingsElementProp.FindPropertyRelative("pixelCount");
                        EditorGUILayout.PropertyField(pixelCountProp, new GUIContent("Длина солнца (пикселей)"));
                    }
                }
                else if (displayMode == LEDControl.DisplayMode.SegmentColors)
                {
                    if (segmentColorsProperty.arraySize > stripIndex)
                    {
                        SerializedProperty segmentProp = segmentColorsProperty.GetArrayElementAtIndex(stripIndex);
                        EditorGUILayout.PropertyField(segmentProp, new GUIContent("Цвета сегментов"), true); // true for children
                    }
                }
                else if (displayMode == LEDControl.DisplayMode.SpeedSynthMode)
                {
                    if (dataMode == LEDControl.DataMode.Monochrome1Color || dataMode == LEDControl.DataMode.Monochrome2Color)
                    {
                        int monoIndex = manager.GetMonochromeStripIndex(stripIndex);
                        SerializedProperty monochromeSettingsArray = serializedObject.FindProperty("monochromeStripSettings");
                        if (monoIndex >= 0 && monoIndex < monochromeSettingsArray.arraySize)
                        {
                            SerializedProperty monoProp = monochromeSettingsArray.GetArrayElementAtIndex(monoIndex);
                            SerializedProperty synthColorProp = monoProp.FindPropertyRelative("synthColor");
                            EditorGUILayout.PropertyField(synthColorProp, new GUIContent("Цвет кометы (монохром)"));
                        }
                    }
                    else if (dataMode == LEDControl.DataMode.RGB || dataMode == LEDControl.DataMode.RGBW)
                    {
                        int rgbIndex = manager.GetRGBStripIndex(stripIndex);
                        SerializedProperty rgbSettingsArray = serializedObject.FindProperty("rgbStripSettings");
                        if (rgbIndex >= 0 && rgbIndex < rgbSettingsArray.arraySize)
                        {
                            SerializedProperty rgbProp = rgbSettingsArray.GetArrayElementAtIndex(rgbIndex);
                            SerializedProperty synthColorProp = rgbProp.FindPropertyRelative("synthColor");
                            EditorGUILayout.PropertyField(synthColorProp, new GUIContent("Цвет кометы (RGB)"));
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        if (GUI.changed)
        {
            manager.InitializeStripData();
        }
        serializedObject.ApplyModifiedProperties();
    }
}
#endif

public static class ColorExtensions
{
    public static bool IsDifferent(this Color32 color1, Color32 color2)
    {
        return color1.r != color2.r || color1.g != color2.g || color1.b != color2.b || color1.a != color2.a;
    }
}