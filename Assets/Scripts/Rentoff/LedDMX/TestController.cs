/*using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using JetBrains.Annotations;
using UnityEditor.VersionControl;
using InitializationFramework;

using DemolitionStudios.DemolitionMedia;

public class TestController : MonoBehaviour
{

    Media player;

    [SerializeField]
    byte[] dmxData;*//**//*


    [Header("DMX Settings")]
    [Tooltip("Имя COM порта для DMX")]
    public string comPortName = "COM3";
    [Tooltip("Скорость передачи данных для DMX")]
    public int baudRate = 250000;

    [Header("Frame Rate Control")]
    [Tooltip("Базовая частота кадров (кадров в секунду) при скорости 1x")]
    public float baseFramesPerSecond = 120f;
    [Tooltip("Пропускать кадры (1 = каждый кадр, 2 = каждый второй, и т.д.)")]
    public int frameSkip = 1;
    private int currentFrameSkipCounter = 0;
    private float currentSpeed = 1f; // текущая скорость воспроизведения
    public Text Value;

    [Range(0f, 1f)]
    [SerializeField] private float globalBrightness = 1.0f;


    [SerializeField]
    AnimationCurve testModeCurve;

    private float VideoLength = 241f;

    public enum DisplayMode
    {
        GlobalColor,
        SegmentColor,
        JsonData,
        JsonDataSync,
        TestMode
    }
    [Tooltip("Режим отображения цвета")]
    public DisplayMode currentMode = DisplayMode.JsonData;

    // Перечисление форматов цвета
    public enum ColorFormat
    {
        RGB,
        RGBW,
        HSV,
        RGBWMix
    }

    // Структуры данных - вынесены за пределы класса для обеспечения публичной доступности
    public struct LEDDataFrame
    {
        public int frame;
        public byte[][] pixels;
        public ColorFormat format;
    }

    // Структура для хранения предварительно рассчитанных DMX значений
    public struct PreCalculatedDmxFrame
    {
        public byte[] channelValues;

    }

    private byte[] FrameBuffer;
    
    [System.Serializable]
    public class LEDStrip
    {
        [Tooltip("Название светодиодной ленты")]
        public string name = "LED Strip";

        [Tooltip("Общее количество диодов на ленте")]
        public int totalLEDs = 206;

        [Tooltip("Количество диодов в одном сегменте. Обычно оставляйте 1.")]
        public int ledsPerSegment = 1;

        [Tooltip("Смещение DMX канала для этой ленты")]
        public int dmxChannelOffset = 0;

        [Tooltip("Цвет для всей ленты (RGB)")]
        public Color globalColor = Color.white;

        [Tooltip("Массив цветов для каждого сегмента (RGB)")]
        public Color[] segmentColors;

        [Tooltip("Массив активности сегментов (true - сегмент активен, false - выключен)")]
        public bool[] segmentActiveStates;

        [Tooltip("Путь к JSON файлу (относительно StreamingAssets)")]
        public string jsonFilePath = "";

        [Tooltip("Формат цвета для JSON файла")]
        public ColorFormat jsonFormat = ColorFormat.RGB;

        [HideInInspector]
        public string fullJsonPath;

        [HideInInspector]
        public LEDDataFrame[] jsonData;

        [HideInInspector]
        public PreCalculatedDmxFrame[] preCalculatedDmxFrames;

        [SerializeField] byte[] dmxData;

        [HideInInspector]
        public float currentFrame = 0;

        public int TotalSegments => totalLEDs / ledsPerSegment;

        public void InitializeSegmentArrays()
        {
            int segments = TotalSegments;
            if (segmentColors == null || segmentColors.Length != segments)
            {
                segmentColors = new Color[segments];
                for (int i = 0; i < segments; i++)
                    segmentColors[i] = Color.black;
            }
            if (segmentActiveStates == null || segmentActiveStates.Length != segments)
            {
                segmentActiveStates = new bool[segments];
                for (int i = 0; i < segments; i++)
                    segmentActiveStates[i] = true;
            }
        }
    }

    [Tooltip("Светодиодные ленты")]
    public List<LEDStrip> ledStrips = new List<LEDStrip>();

    // DMX communicator
    private DMXCommunicator dmxCommunicator;
    private bool isDmxInitialized = false;
    private bool idlemode;

    // Структура для десериализации raw JSON данных
    private class LEDDataFrameRaw
    {
        public int frame;
        public int[][] pixels;
    }

    // Класс для десериализации JSON с информацией о формате
    private class LEDDataWithFormat
    {
        public string color_format;
        public LEDDataFrameRaw[] frames;
    }

    void Awake()
    {

        dmxData = new byte[512];


        for (int i =0; i < 512; ++i)
        {
            float fc = (float)i / 512.0f;

            byte vl = (byte)Mathf.Ceil(Mathf.Sin(fc * 3.14f) * 255.0f);


            dmxData[i] = vl;

            //print(dmxData[i]);
        }
        


        *//**//*

        // Если список лент пуст, добавим одну по умолчанию
        if (ledStrips.Count == 0)
        {
            ledStrips.Add(new LEDStrip());
        }

        // Инициализация для каждой ленты
        foreach (var strip in ledStrips)
        {
            strip.fullJsonPath = Path.Combine(Application.streamingAssetsPath, strip.jsonFilePath);
            strip.InitializeSegmentArrays();
        }

        InitializeDMX();

        LoadAllJsonData();
    }

    void InitializeDMX()
    {
        try
        {
            dmxCommunicator = new DMXCommunicator(comPortName, baudRate);
            isDmxInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TestController] Error initializing DMX: {e.Message}\n{e.StackTrace}");
            isDmxInitialized = false;
        }
    }

    void LoadAllJsonData()
    {
        foreach (var strip in ledStrips)
        {
            LoadJsonDataForStrip(strip);
        }
    }

    void LoadJsonDataForStrip(LEDStrip strip)
    {
        if (string.IsNullOrEmpty(strip.jsonFilePath) || !File.Exists(strip.fullJsonPath))
        {
            strip.jsonData = null;
            strip.preCalculatedDmxFrames = null;
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(strip.fullJsonPath);
            ColorFormat detectedFormat = strip.jsonFormat;
            LEDDataFrameRaw[] rawData = null;

            // Пробуем сначала прочитать как структуру с информацией о формате
            try
            {
                LEDDataWithFormat formattedData = JsonConvert.DeserializeObject<LEDDataWithFormat>(jsonString);
                if (formattedData != null && formattedData.frames != null)
                {
                    rawData = formattedData.frames;

                    // Определяем формат из JSON, если он указан
                    if (!string.IsNullOrEmpty(formattedData.color_format))
                    {
                        if (formattedData.color_format.ToLower() == "rgb")
                            detectedFormat = ColorFormat.RGB;
                        else if (formattedData.color_format.ToLower() == "rgbw")
                            detectedFormat = ColorFormat.RGBW;
                        else if (formattedData.color_format.ToLower() == "hsv")
                            detectedFormat = ColorFormat.HSV;
                        else if (formattedData.color_format.ToLower() == "rgbwmix")
                            detectedFormat = ColorFormat.RGBWMix;

                        Debug.Log($"[{strip.name}] Detected color format from JSON: {detectedFormat}");
                    }
                }
            }
            catch
            {
                // Если не удалось распарсить как структуру с форматом, 
                // пробуем прочитать как простой массив кадров
                rawData = JsonConvert.DeserializeObject<LEDDataFrameRaw[]>(jsonString);
                Debug.Log($"[{strip.name}] Using preferred format: {strip.jsonFormat}");
            }

            if (rawData == null)
            {
                Debug.LogError($"[TestController] Не удалось распарсить JSON: {strip.fullJsonPath}");
                strip.jsonData = null;
                strip.preCalculatedDmxFrames = null;
                return;
            }

            strip.jsonData = new LEDDataFrame[rawData.Length];
            for (int i = 0; i < rawData.Length; i++)
            {
                strip.jsonData[i].frame = rawData[i].frame;
                strip.jsonData[i].format = detectedFormat;
                strip.jsonData[i].pixels = new byte[rawData[i].pixels.Length][];

                for (int j = 0; j < rawData[i].pixels.Length; j++)
                {
                    // Определяем размер массива в зависимости от формата
                    int pixelSize;
                    switch (detectedFormat)
                    {
                        case ColorFormat.RGB:
                            pixelSize = 3;
                            break;
                        case ColorFormat.RGBW:
                            pixelSize = 4;
                            break;
                        case ColorFormat.RGBWMix:
                            pixelSize = 5; // R, G, B, Warm White, Cool White
                            break;
                        case ColorFormat.HSV:
                            pixelSize = 3;
                            break;
                        default:
                            pixelSize = 3;
                            break;
                    }

                    strip.jsonData[i].pixels[j] = new byte[pixelSize];

                    // Копируем данные из JSON в массив байтов
                    for (int k = 0; k < Math.Min(pixelSize, rawData[i].pixels[j].Length); k++)
                    {
                        strip.jsonData[i].pixels[j][k] = (byte)Mathf.Clamp(rawData[i].pixels[j][k], 0, 255);
                    }

                    // Если у нас RGBWMix, но в JSON только RGB данные, генерируем теплый и холодный белый
                    if (detectedFormat == ColorFormat.RGBWMix && rawData[i].pixels[j].Length < 5)
                    {
                        if (rawData[i].pixels[j].Length >= 3)
                        {
                            // Вычисляем теплый и холодный белый из RGB
                            byte r = strip.jsonData[i].pixels[j][0];
                            byte g = strip.jsonData[i].pixels[j][1];
                            byte b = strip.jsonData[i].pixels[j][2];

                            // Теплый белый - на основе минимума красного и зеленого
                            byte warmWhite = (byte)Mathf.Min(r, g);

                            // Холодный белый - на основе минимума синего и зеленого
                            byte coolWhite = (byte)Mathf.Min(b, g);

                            // Добавляем значения в массив
                            if (pixelSize > 3) strip.jsonData[i].pixels[j][3] = warmWhite;
                            if (pixelSize > 4) strip.jsonData[i].pixels[j][4] = coolWhite;
                        }
                    }
                    // Если у нас RGBW, но в JSON только RGB данные, генерируем белый канал
                    else if (detectedFormat == ColorFormat.RGBW && rawData[i].pixels[j].Length < 4)
                    {
                        if (rawData[i].pixels[j].Length >= 3)
                        {
                            // Вычисляем белый канал как минимум из RGB
                            byte r = strip.jsonData[i].pixels[j][0];
                            byte g = strip.jsonData[i].pixels[j][1];
                            byte b = strip.jsonData[i].pixels[j][2];

                            // Белый канал - минимальное значение из RGB
                            byte white = (byte)Mathf.Min(Mathf.Min(r, g), b);

                            // Добавляем значение в массив
                            strip.jsonData[i].pixels[j][3] = white;
                        }
                    }
                }
            }

            // Предрасчитываем DMX кадры
            strip.preCalculatedDmxFrames = new PreCalculatedDmxFrame[strip.jsonData.Length];
            for (int i = 0; i < strip.jsonData.Length; i++)
            {
                strip.preCalculatedDmxFrames[i] = CalculateDmxDataForFrame(strip.jsonData[i], strip.dmxChannelOffset, strip.totalLEDs);
            }

            Debug.Log($"[{strip.name}] Loaded {strip.jsonData.Length} frames from {strip.fullJsonPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TestController] Ошибка загрузки/parsing JSON для {strip.name}: {e.Message}\n{e.StackTrace}");
            strip.jsonData = null;
            strip.preCalculatedDmxFrames = null;
        }
    }



    // Обновленный метод расчета DMX данных с учетом формата
    private PreCalculatedDmxFrame CalculateDmxDataForFrame(LEDDataFrame frameData, int offset, int totalLEDs)
    {
        PreCalculatedDmxFrame preCalculatedFrame = new PreCalculatedDmxFrame();
        preCalculatedFrame.channelValues = new byte[513];
        //Debug.Log(preCalculatedFrame.channelValues);
        //Value = channelValues;
        if (frameData.pixels == null)
        {
            Debug.LogError("frameData.pixels is null");
            return preCalculatedFrame;
        }

        int pixelIndex = 0;
        for (int i = 0; i < totalLEDs; i++)
        {
            int baseChannel = i * 3 + 1 + offset;
            if (baseChannel + 2 > 512)
                break;

            if (pixelIndex < frameData.pixels.Length)
            {
                switch (frameData.format)
                {
                    case ColorFormat.RGB:
                        // Стандартный RGB формат - 3 канала
                        preCalculatedFrame.channelValues[baseChannel] = frameData.pixels[pixelIndex][0];     // R
                        preCalculatedFrame.channelValues[baseChannel + 1] = frameData.pixels[pixelIndex][1]; // G
                        preCalculatedFrame.channelValues[baseChannel + 2] = frameData.pixels[pixelIndex][2]; // B
                        break;

                    case ColorFormat.RGBW:
                        // RGBW формат - используем 4 канала (RGB + W)
                        // Для DMX используем только RGB компоненты, но учитываем W для 
                        // улучшения цветопередачи
                        byte r_rgbw = frameData.pixels[pixelIndex][0];
                        byte g_rgbw = frameData.pixels[pixelIndex][1];
                        byte b_rgbw = frameData.pixels[pixelIndex][2];
                        byte w_rgbw = 0;

                        if (frameData.pixels[pixelIndex].Length >= 4)
                        {
                            w_rgbw = frameData.pixels[pixelIndex][3];

                            // Добавляем белый компонент к RGB для улучшения яркости
                            r_rgbw = (byte)Mathf.Clamp(r_rgbw + w_rgbw / 3, 0, 255);
                            g_rgbw = (byte)Mathf.Clamp(g_rgbw + w_rgbw / 3, 0, 255);
                            b_rgbw = (byte)Mathf.Clamp(b_rgbw + w_rgbw / 3, 0, 255);
                        }

                        preCalculatedFrame.channelValues[baseChannel] = r_rgbw;
                        preCalculatedFrame.channelValues[baseChannel + 1] = g_rgbw;
                        preCalculatedFrame.channelValues[baseChannel + 2] = b_rgbw;
                        break;

                    case ColorFormat.RGBWMix:
                        // RGBWMix формат - преобразуем 5 каналов (R,G,B,WW,CW) в RGB для DMX
                        // Извлекаем компоненты
                        byte r = frameData.pixels[pixelIndex][0];
                        byte g = frameData.pixels[pixelIndex][1];
                        byte b = frameData.pixels[pixelIndex][2];

                        byte warmWhite = 0;
                        byte coolWhite = 0;

                        if (frameData.pixels[pixelIndex].Length >= 5)
                        {
                            warmWhite = frameData.pixels[pixelIndex][3];
                            coolWhite = frameData.pixels[pixelIndex][4];
                        }

                        // Добавляем теплый белый к красному и зеленому
                        r = (byte)Mathf.Clamp(r + warmWhite / 2, 0, 255);
                        g = (byte)Mathf.Clamp(g + warmWhite / 2, 0, 255);

                        // Добавляем холодный белый к синему и зеленому
                        b = (byte)Mathf.Clamp(b + coolWhite / 2, 0, 255);
                        g = (byte)Mathf.Clamp(g + coolWhite / 2, 0, 255);

                        preCalculatedFrame.channelValues[baseChannel] = r;
                        preCalculatedFrame.channelValues[baseChannel + 1] = g;
                        preCalculatedFrame.channelValues[baseChannel + 2] = b;
                        break;

                    case ColorFormat.HSV:
                        // Преобразуем HSV в RGB
                        float h = frameData.pixels[pixelIndex][0] / 255f * 360f;
                        float s = frameData.pixels[pixelIndex][1] / 255f;
                        float v = frameData.pixels[pixelIndex][2] / 255f;

                        Color rgbColor = Color.HSVToRGB(h / 360f, s, v);

                        preCalculatedFrame.channelValues[baseChannel] = (byte)(rgbColor.r * 255);
                        preCalculatedFrame.channelValues[baseChannel + 1] = (byte)(rgbColor.g * 255);
                        preCalculatedFrame.channelValues[baseChannel + 2] = (byte)(rgbColor.b * 255);
                        break;

                    default:
                        // По умолчанию используем RGB
                        preCalculatedFrame.channelValues[baseChannel] = frameData.pixels[pixelIndex][0];
                        preCalculatedFrame.channelValues[baseChannel + 1] = frameData.pixels[pixelIndex][1];
                        preCalculatedFrame.channelValues[baseChannel + 2] = frameData.pixels[pixelIndex][2];
                        break;
                }
                pixelIndex++;
            }
        }
        return preCalculatedFrame;
    }

    // Функция для обновления параметров синтезатора
    public void UpdateSynthParameters(float speed)
    {
        currentSpeed = speed;
    }

    void FixedUpdate()
    {

        if (dmxCommunicator == null || !isDmxInitialized || !dmxCommunicator.IsActive)
        {
            if (!isDmxInitialized)
                InitializeDMX();
            return;
        }


            //dmxCommunicator.SetChannel(1, dmxData[0]);

            float framesToAdvance = baseFramesPerSecond * currentSpeed * Time.fixedDeltaTime;
        currentFrameSkipCounter++;
        if (currentFrameSkipCounter >= frameSkip)
        {
            // Обновляем счетчики кадров для всех лент
            foreach (var strip in ledStrips)
            {
                if (strip.preCalculatedDmxFrames != null && strip.preCalculatedDmxFrames.Length > 0)
                {
                    strip.currentFrame += framesToAdvance;
                    while (strip.currentFrame >= strip.preCalculatedDmxFrames.Length)
                        strip.currentFrame -= strip.preCalculatedDmxFrames.Length;
                    while (strip.currentFrame < 0)
                        strip.currentFrame += strip.preCalculatedDmxFrames.Length;
                }
            }

            UpdateFrame();
            currentFrameSkipCounter = 0;

        }

    }

    void UpdateFrame()
    {
        //ClearDmxBuffer();

        switch (currentMode)
        {
            case DisplayMode.GlobalColor:
                SetGlobalColors();
                break;
            case DisplayMode.SegmentColor:
                SetSegmentColors();
                break;
            case DisplayMode.JsonData:
                if (idlemode == false)
                {
                    SetJsonData();
                }
                else
                {
                    //Берем данные для idle 
                }
                    break;
            case DisplayMode.JsonDataSync:
                if (idlemode == false)
                {
                    SetJsonDataSync();
                }
                else
                {
                    //Берем данные для idle 
                }
                break;
            case DisplayMode.TestMode:
                if (idlemode == false)
                {
                    TestMode();
                }
                break;
        }

        dmxCommunicator.SendFrame(FrameBuffer);
    }

    void ClearDmxBuffer()
    {
        // Очищаем буфер DMX
        for (int ch = 1; ch <= 512; ch++)
        {
            dmxCommunicator.SetChannel(ch, 0);
        }
    }

    void SetGlobalColors()
    {
        foreach (var strip in ledStrips)
        {
            byte r = (byte)(strip.globalColor.r * 255);
            byte g = (byte)(strip.globalColor.g * 255);
            byte b = (byte)(strip.globalColor.b * 255);

            for (int i = 0; i < strip.totalLEDs; i++)
            {
                SetStripLedColor(strip, i, r, g, b);
            }
        }
    }

    void SetSegmentColors()
    {
        foreach (var strip in ledStrips)
        {
            for (int segment = 0; segment < strip.TotalSegments; segment++)
            {
                if (segment < strip.segmentColors.Length && segment < strip.segmentActiveStates.Length)
                {
                    if (!strip.segmentActiveStates[segment])
                    {
                        SetStripSegmentColor(strip, segment, 0, 0, 0);
                        continue;
                    }

                    byte r = (byte)(strip.segmentColors[segment].r * 255);
                    byte g = (byte)(strip.segmentColors[segment].g * 255);
                    byte b = (byte)(strip.segmentColors[segment].b * 255);

                    SetStripSegmentColor(strip, segment, r, g, b);
                }
            }
        }
    }

    public void ModeState(bool state)
    {
        idlemode = state;
    }

    void SetJsonData()
    {
        foreach (var strip in ledStrips)
        {
            if (strip.preCalculatedDmxFrames == null || strip.preCalculatedDmxFrames.Length == 0)
                continue;

            int frameIndex = Mathf.FloorToInt(strip.currentFrame);
            frameIndex = Mathf.Clamp(frameIndex, 0, strip.preCalculatedDmxFrames.Length - 1);

            // Копируем данные из предрассчитанного кадра в DMX буфер
            for (int ch = 1; ch <= 512; ch++)
            {
                byte value = strip.preCalculatedDmxFrames[frameIndex].channelValues[ch];
                dmxCommunicator.SetChannel(ch, value);
            }
        }
    }

    *//*
        void TestMode()
        {
            if (idlemode == false)
            {
                player = (player == null) ? Media.Instance : player;

                float time = Time.time * currentSpeed;
                float videoPosition = time % (player.CurrentTime * baseFramesPerSecond);
                float curveValue = testModeCurve.Evaluate(videoPosition / VideoLength);

                        FrameBuffer[bufferIndex++] = (byte)Mathf.Clamp(intensity * globalBrightness, 0f, 255f);
                        FrameBuffer[bufferIndex++] = (byte)Mathf.Clamp(intensity * globalBrightness, 0f, 255f);
                        FrameBuffer[bufferIndex++] = (byte)Mathf.Clamp(intensity * globalBrightness, 0f, 255f);

                foreach (var strip in ledStrips)
                {
                    byte intensity = (byte)(curveValue * 255);

                    for (int i = 0; i < strip.totalLEDs; i++)
                    {
                        SetStripSegmentColor(strip, i, intensity, intensity, intensity);
                        Debug.Log("Пакет" + i + "Данные" + intensity);
                    }
                }
            }
            else
            {
                foreach (var strip in ledStrips)
                {
                    for (int i = 0; i < strip.totalLEDs; i++)
                    {
                        SetStripSegmentColor(strip, i, 0, 0, 0);
                    }
                }
            }
        }
    *//*

    void TestMode()
    {

        int totalLEDsCount = 0;
        foreach (var strip in ledStrips)
        {
            totalLEDsCount += strip.totalLEDs;
        }

        FrameBuffer = new byte[totalLEDsCount * 3];

        int bufferIndex = 0;

        if (!idlemode)
        {

            player = (player == null) ? Media.Instance : player;


           //float time = Time.time * currentSpeed;
           // float videoPosition = time % (player.CurrentTime * baseFramesPerSecond);
            float curveValue = testModeCurve.Evaluate(player.CurrentTime / VideoLength);


            byte intensity = (byte)(curveValue * globalBrightness * 255);

            foreach (var strip in ledStrips)
            {
                for (int i = 0; i < strip.totalLEDs; i++)
                {
                    SetStripSegmentColor(strip, i, intensity, intensity, intensity);
                    Debug.Log("Пакет " + i + " Данные " + intensity);


                    FrameBuffer[bufferIndex++] = intensity;
                    FrameBuffer[bufferIndex++] = intensity;
                    FrameBuffer[bufferIndex++] = intensity;
                }
            }
        }
        else
        {

            foreach (var strip in ledStrips)
            {
                for (int i = 0; i < strip.totalLEDs; i++)
                {
                    SetStripSegmentColor(strip, i, 0, 0, 0);

                    FrameBuffer[bufferIndex++] = 0;
                    FrameBuffer[bufferIndex++] = 0;
                    FrameBuffer[bufferIndex++] = 0;
                }
            }
        }


    }



    *//*    void TestMode()
        {

            player = (player == null) ? Media.Instance : player;

            float curveValue = testModeCurve.Evaluate(player.CurrentTime/ VideoLength);


            int index = (int)Mathf.Lerp(0f,(float)(dmxData.Length - 1) , curveValue );

            //print("::: " + curveValue );
            print("::: " + index);





            dmxCommunicator.SetChannel(1, dmxData[index]);

            dmxCommunicator.SetChannel(2, dmxData[index]);

            dmxCommunicator.SetChannel(3, dmxData[index]);

            dmxCommunicator.SetChannel(4, dmxData[index]);
            *//**//*
        }
    *//*

    void SetJsonDataSync()
    {
        byte[] dmxBuffer = new byte[513];

        foreach (var strip in ledStrips)
        {
            if (strip.preCalculatedDmxFrames == null || strip.preCalculatedDmxFrames.Length == 0)
                continue;

            int frameIndex = Mathf.FloorToInt(strip.currentFrame);
            frameIndex = Mathf.Clamp(frameIndex, 0, strip.preCalculatedDmxFrames.Length - 1);

            // Копируем данные из предрассчитанного кадра в DMX буфер с учетом смещения
            for (int i = 0; i < strip.totalLEDs; i++)
            {
                int baseChannel = i * 3 + 1 + strip.dmxChannelOffset;
                if (baseChannel + 2 > 512)
                    break;

                dmxBuffer[baseChannel] = strip.preCalculatedDmxFrames[frameIndex].channelValues[baseChannel];
                dmxBuffer[baseChannel + 1] = strip.preCalculatedDmxFrames[frameIndex].channelValues[baseChannel + 1];
                dmxBuffer[baseChannel + 2] = strip.preCalculatedDmxFrames[frameIndex].channelValues[baseChannel + 2];
            }
        }



        // Отправляем данные в DMX
        for (int ch = 1; ch <= 512; ch++)
        {
            dmxCommunicator.SetChannel(ch, dmxBuffer[ch]);
        }
    }

    void SetStripLedColor(LEDStrip strip, int ledIndex, byte r, byte g, byte b)
    {
        int baseChannel = ledIndex * 3 + 1 + strip.dmxChannelOffset;
        if (baseChannel + 2 > 512)
        {
            Debug.LogWarning($"[{strip.name}] Попытка записи за пределы DMX-буфера.");
            return;
        }

        dmxCommunicator.SetChannel(baseChannel, r);
        dmxCommunicator.SetChannel(baseChannel + 1, g);
        dmxCommunicator.SetChannel(baseChannel + 2, b);
    }

    //
    void SampleCurve( double time )
    { 
    


    
    }


    //

    void TestLedColor(LEDStrip strip, int ledIndex, byte a, byte r, byte g, byte b)
    {
        int baseChannel = ledIndex * 3 + 1 + strip.dmxChannelOffset;
        if (baseChannel + 2 > 5)
        {
            Debug.LogWarning($"[{strip.name}] Попытка записи за пределы DMX-буфера.");
            return;
        }




        dmxCommunicator.SetChannel(baseChannel, a);
        dmxCommunicator.SetChannel(baseChannel, r);
        dmxCommunicator.SetChannel(baseChannel + 1, g);
        dmxCommunicator.SetChannel(baseChannel + 2, b);
    }


    void SetStripSegmentColor(LEDStrip strip, int segmentIndex, byte r, byte g, byte b)
    {
        int globalLedIndex = segmentIndex * strip.ledsPerSegment;
        SetStripLedColor(strip, globalLedIndex, r, g, b);
    }

    private void OnDestroy()
    {
        if (dmxCommunicator != null)
        {
            dmxCommunicator.Stop();
            TurnOffAllLEDs();

            //dmxData[1] = 0;

            dmxCommunicator.Dispose();
        }
    }

    private void TurnOffAllLEDs()
    {
        if (dmxCommunicator == null || !dmxCommunicator.IsActive) return;

        foreach (var strip in ledStrips)
        {
            for (int i = 0; i < strip.totalLEDs; i++)
            {
                SetStripLedColor(strip, i, 0, 0, 0);
            }
        }


        dmxCommunicator.SendFrame(FrameBuffer);
    }

    // Публичные методы для управления лентами

    public void SetStripGlobalColor(int stripIndex, Color color)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            ledStrips[stripIndex].globalColor = color;
        }
    }

    public void SetStripSegmentColor(int stripIndex, int segmentIndex, Color color)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            LEDStrip strip = ledStrips[stripIndex];
            if (segmentIndex >= 0 && segmentIndex < strip.segmentColors.Length)
            {
                strip.segmentColors[segmentIndex] = color;
            }
        }
    }

    public void SetStripSegmentActive(int stripIndex, int segmentIndex, bool isActive)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            LEDStrip strip = ledStrips[stripIndex];
            if (segmentIndex >= 0 && segmentIndex < strip.segmentActiveStates.Length)
            {
                strip.segmentActiveStates[segmentIndex] = isActive;
            }
        }
    }

    public void ChangeDisplayMode(DisplayMode newMode)
    {
        currentMode = newMode;

        // Сбрасываем счетчики кадров для всех лент
        if (newMode == DisplayMode.JsonData || newMode == DisplayMode.JsonDataSync)
        {
            foreach (var strip in ledStrips)
            {
                strip.currentFrame = 0;
            }
        }
    }

    public void ChangeStripJsonFile(int stripIndex, string newFileName, ColorFormat format = ColorFormat.RGB)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            LEDStrip strip = ledStrips[stripIndex];
            strip.jsonFilePath = newFileName;
            strip.jsonFormat = format;
            strip.fullJsonPath = Path.Combine(Application.streamingAssetsPath, strip.jsonFilePath);
            LoadJsonDataForStrip(strip);
            strip.currentFrame = 0;
        }
    }

    public void ChangeStripJsonFormat(int stripIndex, ColorFormat newFormat)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            LEDStrip strip = ledStrips[stripIndex];
            strip.jsonFormat = newFormat;
            LoadJsonDataForStrip(strip);
        }
    }

    // Методы для добавления и удаления лент

    public void AddLEDStrip(string name = "New LED Strip", int totalLEDs = 206, int dmxOffset = 0)
    {
        LEDStrip newStrip = new LEDStrip
        {
            name = name,
            totalLEDs = totalLEDs,
            dmxChannelOffset = dmxOffset
        };

        newStrip.InitializeSegmentArrays();
        newStrip.fullJsonPath = string.IsNullOrEmpty(newStrip.jsonFilePath) ? "" :
                                Path.Combine(Application.streamingAssetsPath, newStrip.jsonFilePath);

        ledStrips.Add(newStrip);
    }

    public void RemoveLEDStrip(int stripIndex)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            ledStrips.RemoveAt(stripIndex);
        }
    }

    // Метод для получения информации о ленте
    public LEDStrip GetLEDStrip(int stripIndex)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            return ledStrips[stripIndex];
        }
        return null;
    }

    // Метод для получения количества лент
    public int GetLEDStripCount()
    {
        return ledStrips.Count;
    }

    // Метод для обработки цвета в соответствии с форматом
    public Color[] ProcessColorsByFormat(Color[] colors, ColorFormat format)
    {
        Color[] processedColors = new Color[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            switch (format)
            {
                case ColorFormat.RGB:
                    // Просто копируем цвет
                    processedColors[i] = colors[i];
                    break;

                case ColorFormat.RGBW:
                    // Вычисляем белый канал и добавляем его к RGB
                    float r = colors[i].r;
                    float g = colors[i].g;
                    float b = colors[i].b;
                    float w = Mathf.Min(r, Mathf.Min(g, b));

                    // Увеличиваем яркость RGB с учетом белого канала
                    r = Mathf.Clamp01(r + w / 3);
                    g = Mathf.Clamp01(g + w / 3);
                    b = Mathf.Clamp01(b + w / 3);

                    processedColors[i] = new Color(r, g, b);
                    break;

                case ColorFormat.RGBWMix:
                    // Вычисляем теплый и холодный белый каналы и добавляем их к RGB
                    float r2 = colors[i].r;
                    float g2 = colors[i].g;
                    float b2 = colors[i].b;

                    // Теплый белый (влияет на красный и зеленый)
                    float warmWhite = Mathf.Min(r2, g2);

                    // Холодный белый (влияет на синий и зеленый)
                    float coolWhite = Mathf.Min(b2, g2);

                    // Увеличиваем яркость RGB с учетом белых каналов
                    r2 = Mathf.Clamp01(r2 + warmWhite / 2);
                    g2 = Mathf.Clamp01(g2 + (warmWhite + coolWhite) / 2);
                    b2 = Mathf.Clamp01(b2 + coolWhite / 2);

                    processedColors[i] = new Color(r2, g2, b2);
                    break;

                case ColorFormat.HSV:
                    // Преобразуем RGB в HSV, возможно модифицируем, и обратно в RGB
                    float h, s, v;
                    Color.RGBToHSV(colors[i], out h, out s, out v);

                    // Здесь можно модифицировать HSV если нужно
                    // Например, увеличить насыщенность:
                    s = Mathf.Clamp01(s * 1.2f);

                    processedColors[i] = Color.HSVToRGB(h, s, v);
                    break;

                default:
                    processedColors[i] = colors[i];
                    break;
            }
        }

        return processedColors;
    }

    // Метод для установки всех цветов ленты в соответствии с форматом
    public void SetStripColors(int stripIndex, Color[] colors)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count && colors != null)
        {
            LEDStrip strip = ledStrips[stripIndex];

            // Обрабатываем цвета в соответствии с форматом
            Color[] processedColors = ProcessColorsByFormat(colors, strip.jsonFormat);

            // Устанавливаем цвета для сегментов
            int segmentCount = Mathf.Min(processedColors.Length, strip.TotalSegments);
            for (int i = 0; i < segmentCount; i++)
            {
                if (i < strip.segmentColors.Length)
                {
                    strip.segmentColors[i] = processedColors[i];
                }
            }
        }
    }

    // Метод для получения текущего кадра ленты
    public int GetCurrentFrame(int stripIndex)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            return Mathf.FloorToInt(ledStrips[stripIndex].currentFrame);
        }
        return 0;
    }

    // Метод для установки конкретного кадра
    public void SetFrame(int stripIndex, int frameIndex)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            LEDStrip strip = ledStrips[stripIndex];
            if (strip.preCalculatedDmxFrames != null && strip.preCalculatedDmxFrames.Length > 0)
            {
                strip.currentFrame = Mathf.Clamp(frameIndex, 0, strip.preCalculatedDmxFrames.Length - 1);
            }
        }
    }

    // Метод для получения общего количества кадров
    public int GetTotalFrames(int stripIndex)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            LEDStrip strip = ledStrips[stripIndex];
            if (strip.preCalculatedDmxFrames != null)
            {
                return strip.preCalculatedDmxFrames.Length;
            }
        }
        return 0;
    }

    // Метод для сброса всех лент
    public void ResetAllStrips()
    {
        foreach (var strip in ledStrips)
        {
            strip.currentFrame = 0;
        }
    }

    // Метод для обновления смещения DMX канала
    public void UpdateDmxOffset(int stripIndex, int newOffset)
    {
        if (stripIndex >= 0 && stripIndex < ledStrips.Count)
        {
            LEDStrip strip = ledStrips[stripIndex];
            strip.dmxChannelOffset = newOffset;

            // Если есть загруженные данные, пересчитываем DMX кадры с новым смещением
            if (strip.jsonData != null && strip.jsonData.Length > 0)
            {
                strip.preCalculatedDmxFrames = new PreCalculatedDmxFrame[strip.jsonData.Length];
                for (int i = 0; i < strip.jsonData.Length; i++)
                {
                    strip.preCalculatedDmxFrames[i] = CalculateDmxDataForFrame(strip.jsonData[i], strip.dmxChannelOffset, strip.totalLEDs);
                }
            }
        }
    }
}*/