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
        public List<Color32> segments = new();
    }

    [Serializable]
    public class StripSettings
    {
        [Range(0f, 1f)]
        public float brightness = 1f;

        [Range(0.1f, 5f)]
        public float gammaValue = 2.2f;

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
        public List<int> ledsPerSegment = new() { 1, 1, 1, 1 };
        public List<bool> stripEnabled = new() { true, true, true, true };
        public List<DataMode> currentDataModes = new() { DataMode.Monochrome1Color, DataMode.RGBW, DataMode.RGB, DataMode.Monochrome1Color };
        public List<DisplayMode> currentDisplayModes = new() { DisplayMode.GlobalColor, DisplayMode.GlobalColor, DisplayMode.GlobalColor, DisplayMode.GlobalColor };
        public List<SunMode> currentSunModes = new() { SunMode.Warm, SunMode.Warm, SunMode.Warm, SunMode.Warm };
        public List<StripSettings> stripSettings = new();
        public List<StripPortAssignment> stripPortAssignments = new();
        public List<SunColorSettings> sunColorSettings = new();
        public List<SunStripSettings> sunStripSettings = new();
        public List<MonochromeStripSettings> monochromeStripSettings = new()
        {
            new MonochromeStripSettings() { globalColor = new Color32(255, 255, 255, 255), synthColor = new Color32(255, 255, 255, 255) },
            new MonochromeStripSettings() { globalColor = new Color32(255, 255, 255, 255), synthColor = new Color32(255, 255, 255, 255) }
        };
        public List<RGBStripSettings> rgbStripSettings = new()
        {
            new RGBStripSettings() { globalColor = new Color32(255, 0, 0, 255), synthColor = new Color32(255, 255, 255, 255) },
            new RGBStripSettings() { globalColor = new Color32(0, 255, 0, 255), synthColor = new Color32(255, 255, 255, 255) }
        };
        public List<StripSegmentColors> stripSegmentColors = new();
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

        private void OnValidate()
        {
            InitializeStripData();
        }

        private void OnEnable()
        {
            InitializeStripData();
        }

        public int GetTotalSegments(int stripIndex) => totalLEDsPerStrip[stripIndex] / ledsPerSegment[stripIndex];

        public void InitializeStripData()
        {
            ResizeList(ref stripEnabled, totalLEDsPerStrip.Count, true);
            ResizeList(ref currentDataModes, totalLEDsPerStrip.Count, DataMode.Monochrome1Color);
            ResizeList(ref currentDisplayModes, totalLEDsPerStrip.Count, DisplayMode.GlobalColor);
            ResizeList(ref currentSunModes, totalLEDsPerStrip.Count, SunMode.Warm);
            ResizeList(ref stripSettings, totalLEDsPerStrip.Count, new StripSettings());
            ResizeList(ref stripPortAssignments, totalLEDsPerStrip.Count, new StripPortAssignment());
            ResizeList(ref sunColorSettings, totalLEDsPerStrip.Count, new SunColorSettings());
            ResizeList(ref sunStripSettings, totalLEDsPerStrip.Count, new SunStripSettings());
            ResizeList(ref ledsPerSegment, totalLEDsPerStrip.Count, 1);
            ResizeList(ref stripSegmentColors, totalLEDsPerStrip.Count, new StripSegmentColors());

            for (int stripIndex = 0; stripIndex < totalLEDsPerStrip.Count; stripIndex++)
            {
                int requiredSegmentCount = GetTotalSegments(stripIndex);
                if (stripSegmentColors[stripIndex].segments == null || stripSegmentColors[stripIndex].segments.Count != requiredSegmentCount)
                {
                    ResizeList(ref stripSegmentColors[stripIndex].segments, requiredSegmentCount, Color.black);
                }
            }

            int monochromeCount = currentDataModes.Count(x => x == DataMode.Monochrome1Color || x == DataMode.Monochrome2Color);
            while (monochromeStripSettings.Count < monochromeCount)
                monochromeStripSettings.Add(new MonochromeStripSettings() { globalColor = new Color32(255, 255, 255, 255), synthColor = new Color32(255, 255, 255, 255) });
            while (monochromeStripSettings.Count > monochromeCount)
                monochromeStripSettings.RemoveAt(monochromeStripSettings.Count - 1);

            int rgbCount = currentDataModes.Count(x => x == DataMode.RGB || x == DataMode.RGBW);
            while (rgbStripSettings.Count < rgbCount)
                rgbStripSettings.Add(new RGBStripSettings() { globalColor = new Color32(255, 0, 0, 255), synthColor = new Color32(255, 255, 255, 255) });
            while (rgbStripSettings.Count > rgbCount)
                rgbStripSettings.RemoveAt(rgbStripSettings.Count - 1);

            CachePreviousValues();
        }

        private void ResizeList<T>(ref List<T> list, int newSize, T defaultValue) where T : new()
        {
            if (list == null) list = new List<T>();
            while (list.Count < newSize) list.Add(defaultValue is Color ? (T)(object)Color.black : defaultValue);
            while (list.Count > newSize) list.RemoveAt(list.Count - 1);
        }

        public int GetPortIndexForStrip(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripPortAssignments.Count) return 0;
            return stripPortAssignments[stripIndex].portIndex;
        }

        public Color32 GetDefaultColor(int stripIndex)
        {
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
            for (int i = 0; i <= stripIndex; i++)
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
            for (int i = 0; i <= stripIndex; i++)
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
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count) return;
            if (segmentIndex >= 0 && segmentIndex < GetTotalSegments(stripIndex))
            {
                if (currentDataModes[stripIndex] == DataMode.Monochrome1Color || currentDataModes[stripIndex] == DataMode.Monochrome2Color)
                {
                    float lum = (color.r * 0.299f + color.g * 0.587f + color.b * 0.114f);
                    color = new Color32((byte)lum, (byte)lum, (byte)lum, 255);
                }
                stripSegmentColors[stripIndex].segments[segmentIndex] = color;
                if (debug) Debug.Log($"Strip {stripIndex}, Segment {segmentIndex} color set to {color}");
            }
        }

        public Color32 GetSegmentColor(int stripIndex, int segmentIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count) return GetDefaultColor(stripIndex);
            if (segmentIndex >= 0 && segmentIndex < GetTotalSegments(stripIndex)) return stripSegmentColors[stripIndex].segments[segmentIndex];
            return GetDefaultColor(stripIndex);
        }

        public List<Color32> GetSegmentColors(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSegmentColors.Count) return new List<Color32>();
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
                return sunMode == SunMode.Warm ? new Color32(255, 180, 0, 255) : new Color32(0, 150, 255, 255);
            }

            return sunMode switch
            {
                SunMode.Warm => sunColorSettings[stripIndex].warmColor,
                SunMode.Cold => sunColorSettings[stripIndex].coldColor,
                SunMode.Gradient => sunColorSettings[stripIndex].gradientStartColor,
                SunMode.BackGradient => sunColorSettings[stripIndex].gradientStartColor,
                _ => Color.white

            };
        }

        public Color32 GetSunEndColorForStrip(int stripIndex, SunMode sunMode)
        {
            if (stripIndex < 0 || stripIndex >= sunColorSettings.Count)
            {
                return Color.white;
            }

            return sunMode switch
            {
                SunMode.Gradient => sunColorSettings[stripIndex].gradientEndColor,
                SunMode.BackGradient => sunColorSettings[stripIndex].gradientEndColor,
                _ => GetSunColorForStrip(stripIndex, sunMode)
            };
        }

        public int GetSunPixelCountForStrip(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= sunStripSettings.Count)
            {
                return 10;
            }
            return sunStripSettings[stripIndex].pixelCount;
        }

        public void CachePreviousValues()
        {
            previousMonochromeStripSettings.Clear();
            foreach (var settings in monochromeStripSettings) previousMonochromeStripSettings.Add(new MonochromeStripSettings() { globalColor = settings.globalColor, synthColor = settings.synthColor });

            previousRGBStripSettings.Clear();
            foreach (var settings in rgbStripSettings) previousRGBStripSettings.Add(new RGBStripSettings() { globalColor = settings.globalColor, synthColor = settings.synthColor });

            previousSegmentColors.Clear();
            for (int stripIndex = 0; stripIndex < stripSegmentColors.Count; stripIndex++) previousSegmentColors.Add(new List<Color32>(stripSegmentColors[stripIndex].segments));

            previousDisplayModes = new List<DisplayMode>(currentDisplayModes);
            previousDataModes = new List<DataMode>(currentDataModes);
            previousStripEnabled = new List<bool>(stripEnabled);
            previousStripSettings = new List<StripSettings>(stripSettings);
            previousSunModes = new List<SunMode>(currentSunModes);
            previousSunColorSettings = new List<SunColorSettings>(sunColorSettings);
            previousSunStripSettings = new List<SunStripSettings>(sunStripSettings);
            previousLedsPerSegment = new List<int>(ledsPerSegment);
        }

        public bool CheckForChanges()
        {
            bool colorsChanged = false;
            if (stripSegmentColors.Count != previousSegmentColors.Count || monochromeStripSettings.Count != previousMonochromeStripSettings.Count ||
                rgbStripSettings.Count != previousRGBStripSettings.Count || currentDisplayModes.Count != previousDisplayModes.Count ||
                currentDataModes.Count != previousDataModes.Count || stripEnabled.Count != previousStripEnabled.Count || stripSettings.Count != previousStripSettings.Count ||
                sunColorSettings.Count != previousSunColorSettings.Count || sunStripSettings.Count != previousSunStripSettings.Count ||
                ledsPerSegment.Count != previousLedsPerSegment.Count)
                return true;

            for (int stripIndex = 0; stripIndex < stripSegmentColors.Count; stripIndex++)
            {
                if (stripSegmentColors[stripIndex].segments.Count != previousSegmentColors[stripIndex].Count) { colorsChanged = true; break; }
                for (int i = 0; i < stripSegmentColors[stripIndex].segments.Count; ++i)
                {
                    if (stripSegmentColors[stripIndex].segments[i].IsDifferent(previousSegmentColors[stripIndex][i])) { colorsChanged = true; break; }
                }
                if (colorsChanged) break;
            }

            for (int i = 0; i < monochromeStripSettings.Count; i++)
            {
                if (monochromeStripSettings[i].globalColor.IsDifferent(previousMonochromeStripSettings[i].globalColor) ||
                    monochromeStripSettings[i].synthColor.IsDifferent(previousMonochromeStripSettings[i].synthColor)) { colorsChanged = true; break; }
            }

            for (int i = 0; i < rgbStripSettings.Count; i++)
            {
                if (rgbStripSettings[i].globalColor.IsDifferent(previousRGBStripSettings[i].globalColor) ||
                    rgbStripSettings[i].synthColor.IsDifferent(previousRGBStripSettings[i].synthColor)) { colorsChanged = true; break; }
            }

            for (int i = 0; i < sunColorSettings.Count; i++)
            {
                if (sunColorSettings[i].warmColor.IsDifferent(previousSunColorSettings[i].warmColor) ||
                    sunColorSettings[i].coldColor.IsDifferent(previousSunColorSettings[i].coldColor) ||
                    sunColorSettings[i].gradientStartColor.IsDifferent(previousSunColorSettings[i].gradientStartColor) ||
                    sunColorSettings[i].gradientEndColor.IsDifferent(previousSunColorSettings[i].gradientEndColor)) { colorsChanged = true; break; }
            }

            for (int i = 0; i < sunStripSettings.Count; i++)
            {
                if (sunStripSettings[i].pixelCount != previousSunStripSettings[i].pixelCount) { colorsChanged = true; break; }
            }

            for (int i = 0; i < ledsPerSegment.Count; i++)
            {
                if (ledsPerSegment[i] != previousLedsPerSegment[i]) { colorsChanged = true; break; }
            }

            if (!currentDisplayModes.SequenceEqual(previousDisplayModes) || !currentDataModes.SequenceEqual(previousDataModes) ||
                !stripEnabled.SequenceEqual(previousStripEnabled) || !currentSunModes.SequenceEqual(previousSunModes)) colorsChanged = true;

            for (int i = 0; i < stripSettings.Count; i++)
            {
                if (stripSettings[i].brightness != previousStripSettings[i].brightness || stripSettings[i].gammaValue != previousStripSettings[i].gammaValue ||
                    stripSettings[i].enableGammaCorrection != previousStripSettings[i].enableGammaCorrection)
                {
                    colorsChanged = true;
                    break;
                }
            }
            return colorsChanged;
        }

        public float GetStripBrightness(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSettings.Count) return 1f;
            return stripSettings[stripIndex].brightness;
        }

        public float GetStripGamma(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSettings.Count) return 2.2f;
            return stripSettings[stripIndex].gammaValue;
        }

        public bool IsGammaCorrectionEnabled(int stripIndex)
        {
            if (stripIndex < 0 || stripIndex >= stripSettings.Count) return true;
            return stripSettings[stripIndex].enableGammaCorrection;
        }

        public Color32 GetSynthColorForStrip(int stripIndex)
        {
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
            if (stripIndex < 0 || stripIndex >= totalLEDsPerStrip.Count) return SunMode.Warm;
            return currentSunModes[stripIndex];
        }

        public int GetVirtualPadding(int stripIndex)
        {
            switch (currentDataModes[stripIndex])
            {
                case DataMode.Monochrome1Color:
                case DataMode.Monochrome2Color:
                    return 5;
                case DataMode.RGB:
                case DataMode.RGBW:
                    return 5;
                default:
                    return 5;
            }
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
        int stripCount = manager.totalLEDsPerStrip.Count;
        stripFoldouts = new bool[stripCount];
        for (int i = 0; i < stripCount; i++) stripFoldouts[i] = false;
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
        SerializedProperty ledsPerSegmentProperty = serializedObject.FindProperty("ledsPerSegment");
        EditorGUILayout.PropertyField(ledsPerSegmentProperty, new GUIContent("Диодов в сегменте (на ленту)"));
        SerializedProperty touchColsProperty = serializedObject.FindProperty("touchPanelCols");
        EditorGUILayout.PropertyField(touchColsProperty, new GUIContent("Панелей на тач-панели"));
        SerializedProperty touchOffsetProperty = serializedObject.FindProperty("touchPanelOffset");
        EditorGUILayout.PropertyField(touchOffsetProperty, new GUIContent("Смещение тач-панели"));
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Настройки лент", headerStyle);
        int stripCount = manager.totalLEDsPerStrip.Count;
        SerializedProperty stripEnabledProperty = serializedObject.FindProperty("stripEnabled");
        SerializedProperty dataModesProperty = serializedObject.FindProperty("currentDataModes");
        SerializedProperty displayModesProperty = serializedObject.FindProperty("currentDisplayModes");
        SerializedProperty sunModesProperty = serializedObject.FindProperty("currentSunModes");
        SerializedProperty stripSettingsProperty = serializedObject.FindProperty("stripSettings");
        SerializedProperty sunColorSettingsProperty = serializedObject.FindProperty("sunColorSettings");
        SerializedProperty sunStripSettingsProperty = serializedObject.FindProperty("sunStripSettings");
        SerializedProperty segmentColorsProperty = serializedObject.FindProperty("stripSegmentColors");

        for (int stripIndex = 0; stripIndex < stripCount; stripIndex++)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.BeginHorizontal();
            stripFoldouts[stripIndex] = EditorGUILayout.Foldout(stripFoldouts[stripIndex], $" Лента #{stripIndex + 1}", true, stripHeaderStyle);
            SerializedProperty enabledProp = stripEnabledProperty.GetArrayElementAtIndex(stripIndex);
            EditorGUILayout.PropertyField(enabledProp, GUIContent.none, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            if (stripFoldouts[stripIndex])
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                SerializedProperty dataModeProp = dataModesProperty.GetArrayElementAtIndex(stripIndex);
                EditorGUILayout.PropertyField(dataModeProp, new GUIContent("Тип ленты"));
                SerializedProperty displayModeProp = displayModesProperty.GetArrayElementAtIndex(stripIndex);
                EditorGUILayout.PropertyField(displayModeProp, new GUIContent("Режим отображения"));

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    manager.InitializeStripData();
                    serializedObject.Update();
                }

                LEDControl.DisplayMode displayMode = (LEDControl.DisplayMode)displayModeProp.enumValueIndex;
                LEDControl.DataMode dataMode = (LEDControl.DataMode)dataModeProp.enumValueIndex;

                SerializedProperty stripSettingsProp = stripSettingsProperty.GetArrayElementAtIndex(stripIndex);
                SerializedProperty brightnessProp = stripSettingsProp.FindPropertyRelative("brightness");
                SerializedProperty gammaValueProp = stripSettingsProp.FindPropertyRelative("gammaValue");
                SerializedProperty enableGammaProp = stripSettingsProp.FindPropertyRelative("enableGammaCorrection");

                EditorGUILayout.PropertyField(brightnessProp, new GUIContent("Яркость ленты"));
                EditorGUILayout.PropertyField(enableGammaProp, new GUIContent("Включить гамма-коррекцию"));
                if (enableGammaProp.boolValue) EditorGUILayout.PropertyField(gammaValueProp, new GUIContent("Значение гаммы"));

                SerializedProperty segmentSettingProp = ledsPerSegmentProperty.GetArrayElementAtIndex(stripIndex);
                EditorGUILayout.PropertyField(segmentSettingProp, new GUIContent("Диодов в сегменте"));

                if (displayMode == LEDControl.DisplayMode.GlobalColor)
                {
                    if (dataMode == LEDControl.DataMode.Monochrome1Color || dataMode == LEDControl.DataMode.Monochrome2Color)
                    {
                        int monoIndex = manager.GetMonochromeStripIndex(stripIndex);
                        if (monoIndex >= 0 && monoIndex < serializedObject.FindProperty("monochromeStripSettings").arraySize)
                        {
                            SerializedProperty monoProp = serializedObject.FindProperty("monochromeStripSettings").GetArrayElementAtIndex(monoIndex);
                            SerializedProperty colorProp = monoProp.FindPropertyRelative("globalColor");
                            EditorGUILayout.PropertyField(colorProp, new GUIContent("Глобальный цвет (монохром)"));
                        }
                    }
                    else if (dataMode == LEDControl.DataMode.RGB || dataMode == LEDControl.DataMode.RGBW)
                    {
                        int rgbIndex = manager.GetRGBStripIndex(stripIndex);
                        if (rgbIndex >= 0 && rgbIndex < serializedObject.FindProperty("rgbStripSettings").arraySize)
                        {
                            SerializedProperty rgbProp = serializedObject.FindProperty("rgbStripSettings").GetArrayElementAtIndex(rgbIndex);
                            SerializedProperty colorProp = rgbProp.FindPropertyRelative("globalColor");
                            EditorGUILayout.PropertyField(colorProp, new GUIContent("Глобальный цвет (RGB)"));
                        }
                    }
                }
                else if (displayMode == LEDControl.DisplayMode.SunMovement)
                {
                    SerializedProperty sunModeProp = sunModesProperty.GetArrayElementAtIndex(stripIndex);
                    EditorGUILayout.PropertyField(sunModeProp, new GUIContent("Режим солнца (Warm/Cold/Gradient)"));

                    SerializedProperty sunColorSettingsProp = sunColorSettingsProperty.GetArrayElementAtIndex(stripIndex);
                    SerializedProperty warmColorProp = sunColorSettingsProp.FindPropertyRelative("warmColor");
                    SerializedProperty coldColorProp = sunColorSettingsProp.FindPropertyRelative("coldColor");
                    SerializedProperty gradientStartColorProp = sunColorSettingsProp.FindPropertyRelative("gradientStartColor");
                    SerializedProperty gradientEndColorProp = sunColorSettingsProp.FindPropertyRelative("gradientEndColor");

                    EditorGUILayout.PropertyField(warmColorProp, new GUIContent("Цвет тёплого солнца"));
                    EditorGUILayout.PropertyField(coldColorProp, new GUIContent("Цвет холодного солнца"));

                    EditorGUILayout.PropertyField(gradientStartColorProp, new GUIContent("Начальный цвет градиента"));
                    EditorGUILayout.PropertyField(gradientEndColorProp, new GUIContent("Конечный цвет градиента"));


                    SerializedProperty sunStripSettingsProp = sunStripSettingsProperty.GetArrayElementAtIndex(stripIndex);
                    SerializedProperty pixelCountProp = sunStripSettingsProp.FindPropertyRelative("pixelCount");
                    EditorGUILayout.PropertyField(pixelCountProp, new GUIContent("Длина солнца (пикселей)"));
                }
                else if (displayMode == LEDControl.DisplayMode.SegmentColors)
                {
                    SerializedProperty segmentProp = segmentColorsProperty.GetArrayElementAtIndex(stripIndex);
                    EditorGUILayout.PropertyField(segmentProp, new GUIContent("Цвета сегментов"));
                }
                else if (displayMode == LEDControl.DisplayMode.SpeedSynthMode)
                {
                    if (dataMode == LEDControl.DataMode.Monochrome1Color || dataMode == LEDControl.DataMode.Monochrome2Color)
                    {
                        int monoIndex = manager.GetMonochromeStripIndex(stripIndex);
                        if (monoIndex >= 0 && monoIndex < serializedObject.FindProperty("monochromeStripSettings").arraySize)
                        {
                            SerializedProperty monoProp = serializedObject.FindProperty("monochromeStripSettings").GetArrayElementAtIndex(monoIndex);
                            SerializedProperty synthColorProp = monoProp.FindPropertyRelative("synthColor");
                            EditorGUILayout.PropertyField(synthColorProp, new GUIContent("Цвет кометы (монохром)"));
                        }
                    }
                    else if (dataMode == LEDControl.DataMode.RGB || dataMode == LEDControl.DataMode.RGBW)
                    {
                        int rgbIndex = manager.GetRGBStripIndex(stripIndex);
                        if (rgbIndex >= 0 && rgbIndex < serializedObject.FindProperty("rgbStripSettings").arraySize)
                        {
                            SerializedProperty rgbProp = serializedObject.FindProperty("rgbStripSettings").GetArrayElementAtIndex(rgbIndex);
                            SerializedProperty synthColorProp = rgbProp.FindPropertyRelative("synthColor");
                            EditorGUILayout.PropertyField(synthColorProp, new GUIContent("Цвет кометы (RGB)"));
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
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