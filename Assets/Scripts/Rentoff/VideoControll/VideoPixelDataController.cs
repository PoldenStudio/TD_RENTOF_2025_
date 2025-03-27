/*using UnityEngine;
using System.IO;
using System;

public class VideoPixelDataController : MonoBehaviour
{
    [Header("DMX Settings")]
    [Tooltip("Имя COM порта для DMX")]
    public string comPortName = "COM3";
    [Tooltip("Скорость передачи данных для DMX")]
    public int baudRate = 250000;


    [Header("LED Strip Configuration")]
    [Tooltip("Общее количество диодов на ленте")]
    public int totalLEDs = 206;
    [Tooltip("Количество диодов в одном сегменте")]
    public int ledsPerSegment = 1;
    private int totalSegments => totalLEDs / ledsPerSegment;

    [Header("JSON Configuration")]
    [Tooltip("Имя JSON файла в StreamingAssets")]
    public string jsonFileName = "pixelData.json";
    private string jsonFilePath => Path.Combine(Application.streamingAssetsPath, jsonFileName);

    public enum DisplayMode
    {
        GlobalColor,
        SegmentColor,
        JsonData
    }
    [Tooltip("Режим отображения цвета")]
    public DisplayMode currentMode = DisplayMode.JsonData;

    [Header("Global Color Mode")]
    [Tooltip("Цвет для всей ленты (RGBW)")]
    public Color globalColor = Color.white;
    [Tooltip("Уровень белого для всей ленты")]
    [Range(0, 255)]
    public int globalWhiteLevel = 255;

    [Header("Segment Color Mode")]
    [Tooltip("Массив цветов для каждого сегмента (RGBW)")]
    public Color[] segmentColors;
    [Tooltip("Массив уровней белого для каждого сегмента")]
    [Range(0, 255)]
    public int[] segmentWhiteLevels;
    [Tooltip("Массив активности сегментов (true - сегмент активен, false - выключен)")]
    public bool[] segmentActiveStates;

    [Serializable]
    private class PixelData
    {
        public int[] pixel;
    }

    [Serializable]
    private class FrameData
    {
        public int frame;
        public PixelData[] pixels;
    }

    [Serializable]
    private class JsonData
    {
        public FrameData[] frames;
    }

    private DMXCommunicator dmxCommunicator;
    private bool isDmxInitialized = false;
    private bool isJsonLoaded = false;
    private int[][] firstFramePixels;

    void Start()
    {
        InitializeSegmentArrays();
        LoadJsonData();
        InitializeDMX();
    }

    void InitializeDMX()
    {
        try
        {
            dmxCommunicator = new DMXCommunicator(comPortName, baudRate);
            isDmxInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VideoPixelDataController] Error initializing DMX: {e.Message}");
            isDmxInitialized = false;
        }
    }

    void InitializeSegmentArrays()
    {
        int segments = totalSegments;

        if (segmentColors == null || segmentColors.Length != segments)
        {
            segmentColors = new Color[segments];
            for (int i = 0; i < segments; i++)
            {
                segmentColors[i] = Color.black;
            }
        }

        if (segmentWhiteLevels == null || segmentWhiteLevels.Length != segments)
        {
            segmentWhiteLevels = new int[segments];
            for (int i = 0; i < segments; i++)
            {
                segmentWhiteLevels[i] = 0;
            }
        }

        if (segmentActiveStates == null || segmentActiveStates.Length != segments)
        {
            segmentActiveStates = new bool[segments];
            for (int i = 0; i < segments; i++)
            {
                segmentActiveStates[i] = true;
            }
        }
    }

    void LoadJsonData()
    {
        try
        {
            if (!File.Exists(jsonFilePath))
            {
                Debug.LogError($"[VideoPixelDataController] JSON file not found at: {jsonFilePath}");
                isJsonLoaded = false;
                return;
            }

            string jsonContent = File.ReadAllText(jsonFilePath);
            JsonData jsonData = JsonUtility.FromJson<JsonData>("{\"frames\":" + jsonContent + "}");

            if (jsonData == null || jsonData.frames == null || jsonData.frames.Length == 0)
            {
                Debug.LogError("[VideoPixelDataController] No frames found in JSON data");
                isJsonLoaded = false;
                return;
            }

            FrameData firstFrame = jsonData.frames[0];
            if (firstFrame.pixels == null || firstFrame.pixels.Length == 0)
            {
                Debug.LogError("[VideoPixelDataController] No pixels found in first frame");
                isJsonLoaded = false;
                return;
            }

            firstFramePixels = new int[firstFrame.pixels.Length][];
            for (int i = 0; i < firstFrame.pixels.Length; i++)
            {
                if (firstFrame.pixels[i].pixel == null || firstFrame.pixels[i].pixel.Length < 4)
                {
                    Debug.LogWarning($"[VideoPixelDataController] Invalid pixel data at index {i}");
                    firstFramePixels[i] = new int[] { 0, 0, 0, 0 };
                    continue;
                }
                firstFramePixels[i] = firstFrame.pixels[i].pixel;
            }

            LoadJsonToSegmentArrays();
            isJsonLoaded = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VideoPixelDataController] Error loading JSON: {e.Message}");
            isJsonLoaded = false;
        }
    }

    void LoadJsonToSegmentArrays()
    {
        for (int i = 0; i < totalSegments; i++)
        {
            if (i >= firstFramePixels.Length)
            {
                segmentColors[i] = Color.black;
                segmentWhiteLevels[i] = 0;
                continue;
            }

            int[] pixelData = firstFramePixels[i];
            float r = pixelData[0] / 255f;
            float g = pixelData[1] / 255f;
            float b = pixelData[2] / 255f;
            int w = pixelData[3];

            segmentColors[i] = new Color(r, g, b);
            segmentWhiteLevels[i] = Mathf.Clamp(w, 0, 255);
        }
    }

    void FixedUpdate()
    {
        if (!isDmxInitialized || dmxCommunicator == null || !dmxCommunicator.IsActive)
        {
            if (!isDmxInitialized)
            {
                InitializeDMX();
            }
            return;
        }

        switch (currentMode)
        {
            case DisplayMode.GlobalColor:
                SetGlobalColor();
                break;
            case DisplayMode.SegmentColor:
                SetSegmentColors("Test");
                break;
            case DisplayMode.JsonData:
                if (isJsonLoaded)
                {
                    SetSegmentColors("JSON");
                }
                else
                {
                    Debug.LogWarning("[VideoPixelDataController] JSON data not loaded, falling back to black");
                    //SetGlobalColor(Color.black, 0);
                }
                break;
        }
    }

    void SetGlobalColor()
    {
        byte r = (byte)(globalColor.r * 255);
        byte g = (byte)(globalColor.g * 255);
        byte b = (byte)(globalColor.b * 255);
        byte w = (byte)Mathf.Clamp(globalWhiteLevel, 0, 255);

        SetGlobalColor(r, g, b, w);
    }

    void SetGlobalColor(byte r, byte g, byte b, byte w)
    {
        for (int segment = 0; segment < totalSegments; segment++)
        {
            SetSegmentColor(segment, r, g, b, w, "Global");
        }
        dmxCommunicator.SendFrame();
    }

    void SetSegmentColors(string mode)
    {
        for (int segment = 0; segment < totalSegments; segment++)
        {
            if (segment < segmentColors.Length &&
                segment < segmentWhiteLevels.Length &&
                segment < segmentActiveStates.Length)
            {
                if (!segmentActiveStates[segment])
                {
                    SetSegmentColor(segment, 0, 0, 0, 0, mode);
                    continue;
                }

                byte r = (byte)(segmentColors[segment].r * 255);
                byte g = (byte)(segmentColors[segment].g * 255);
                byte b = (byte)(segmentColors[segment].b * 255);
                byte w = (byte)Mathf.Clamp(segmentWhiteLevels[segment], 0, 255);

                SetSegmentColor(segment, r, g, b, w, mode);
            }
        }
        dmxCommunicator.SendFrame();
    }

    void SetSegmentColor(int segmentIndex, byte r, byte g, byte b, byte w, string mode)
    {
        for (int ledIndex = 0; ledIndex < ledsPerSegment; ledIndex++)
        {
            int globalLedIndex = segmentIndex * ledsPerSegment + ledIndex;
            int baseChannel = globalLedIndex * 4 + 1;

            if (baseChannel + 3 > 512)
            {
                break;
            }

            Debug.Log($"{mode} Segment {segmentIndex} LED {ledIndex} Channels {baseChannel}-{baseChannel + 3}: {r},{g},{b},{w}");

            dmxCommunicator.SetChannel(baseChannel, r);
            dmxCommunicator.SetChannel(baseChannel + 1, g);
            dmxCommunicator.SetChannel(baseChannel + 2, b);
            dmxCommunicator.SetChannel(baseChannel + 3, w);
        }
    }

    private void OnDestroy()
    {
        if (dmxCommunicator != null)
        {
            dmxCommunicator.Dispose();
        }
    }

    public void SetSegmentActive(int segmentIndex, bool isActive)
    {
        if (segmentIndex >= 0 && segmentIndex < segmentActiveStates.Length)
        {
            segmentActiveStates[segmentIndex] = isActive;
        }
    }

    public void SetSegmentColor(int segmentIndex, Color color, int whiteLevel)
    {
        if (segmentIndex >= 0 && segmentIndex < segmentColors.Length &&
            segmentIndex < segmentWhiteLevels.Length)
        {
            segmentColors[segmentIndex] = color;
            segmentWhiteLevels[segmentIndex] = Mathf.Clamp(whiteLevel, 0, 255);
        }
    }
}*/