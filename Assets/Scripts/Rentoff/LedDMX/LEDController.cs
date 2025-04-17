using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DemolitionStudios.DemolitionMedia;
using static StateManager;

namespace LEDControl
{
    public class LEDController : MonoBehaviour
    {
        [SerializeField] private StateManager stateManager;

        [Header("DMX Settings")]
        public string comPortName = "COM3";
        public int baudRate = 250000;
        [SerializeField] private bool autoCalculateOffsets = false;

        [Header("Frame Rate Control")]
        public float baseFramesPerSecond = 120f;
        public int frameSkip = 1;
        private int currentFrameSkipCounter = 0;
        private float currentSpeed = 1f;

        [Header("Debug Options")]
        [SerializeField] private Text debugText;
        public bool enableFrameDebug = false;

        [Header("Brightness")]
        [Range(0f, 3f)]
        public float globalBrightness = 1.0f;
        private float defaultGlobalBrightness;

        [Header("Test Mode Settings")]
        [SerializeField] private AnimationCurve testModeCurve;
        [SerializeField] private float videoLength = 241f;

        [Header("Display Mode")]
        public DisplayMode currentMode = DisplayMode.JsonDataSync;
        public enum DisplayMode { GlobalColor, SegmentColor, JsonDataSync, TestMode, JsonMixByte }

        [SerializeField] private List<LEDStrip> ledStrips = new();

        [Header("Kinetic Control Settings")]
        [SerializeField] private AnimationCurve kineticControlCurve;
        [Range(1, 512)] public int kineticHeightDmxChannel1 = 1;
        [Range(1, 512)] public int kineticHeightDmxChannel2 = 2;

        [Header("Second Kinetic Control Settings")]
        [SerializeField] private AnimationCurve secondKineticControlCurve;
        [Range(1, 512)] public int secondKineticHeightDmxChannel1 = 3;
        [Range(1, 512)] public int secondKineticHeightDmxChannel2 = 4;

        public bool idleMode = true;
        public bool wasIdled = false;

        private int relocatedSecondKineticHeightChannel1;
        private int relocatedSecondKineticHeightChannel2;

        private DMXCommunicator dmxCommunicator;
        private bool isDmxInitialized = false;

        private byte[] FrameBuffer = new byte[513];

        private IMediaPlayer mediaPlayer;
        private float kineticStartTime = 0f;

        private int totalLedChannels = 0;
        private int relocatedKineticHeightChannel1;
        private int relocatedKineticHeightChannel2;
        private bool kineticChannelsRelocated = false;

        private bool confirmTime;

        public float DefaultGlobalBrightness => defaultGlobalBrightness;

        // Переменные для хранения текущих значений кинетики
        private float kineticCurrentValue = 0f;
        private float secondKineticCurrentValue = 0f;

        private bool kineticPaused = false;
        private float pausedKineticValue = 0f;
        private float pausedSecondKineticValue = 0f;

        // Новые переменные для JsonMixByte режима
        [SerializeField] private string byteArrayFilePath = "keys.data";
        [SerializeField] private int leftAmbilightIndex = 1;
        [SerializeField] private int rightAmbilightIndex = 140;
        [SerializeField] private int upperAmbilightIndex = 400;
        [SerializeField] private int lowerAmbilightIndex = 410;
        [SerializeField] private int kineticLIndex = 509;
        [SerializeField] private int kineticRIndex = 511;
        private byte[][] byteArrayFrames;

        private void Awake()
        {
            defaultGlobalBrightness = globalBrightness;
            InitializeDMX();

            if (ledStrips.Count == 0)
                ledStrips.Add(new LEDStrip());

            if (autoCalculateOffsets)
                RecalculateOffsets();

            CalculateTotalLedChannels();
            RelocateKineticChannelsIfNeeded();

            foreach (var strip in ledStrips)
            {
                strip.InitializeSegmentArrays();
                strip.LoadJsonData(true);
            }

            if (stateManager != null)
            {
                stateManager.OnStateChanged += HandleStateChanged;
            }

            LoadByteArrayFrames();
        }

        void CalculateTotalLedChannels()
        {
            totalLedChannels = 0;
            foreach (var strip in ledStrips)
            {
                int stripEndChannel = strip.dmxChannelOffset + strip.GetTotalChannels();
                if (stripEndChannel > totalLedChannels)
                    totalLedChannels = stripEndChannel;
            }
        }

        void RelocateKineticChannelsIfNeeded()
        {
            relocatedKineticHeightChannel1 = kineticHeightDmxChannel1;
            relocatedKineticHeightChannel2 = kineticHeightDmxChannel2;
            relocatedSecondKineticHeightChannel1 = secondKineticHeightDmxChannel1;
            relocatedSecondKineticHeightChannel2 = secondKineticHeightDmxChannel2;
            kineticChannelsRelocated = false;

            int maxUsedChannel = totalLedChannels;

            if (kineticHeightDmxChannel1 <= totalLedChannels || kineticHeightDmxChannel2 <= totalLedChannels)
            {
                relocatedKineticHeightChannel1 = maxUsedChannel + 1;
                relocatedKineticHeightChannel2 = maxUsedChannel + 2;
                kineticChannelsRelocated = true;
                maxUsedChannel += 2;
            }

            if (secondKineticHeightDmxChannel1 <= maxUsedChannel || secondKineticHeightDmxChannel2 <= maxUsedChannel)
            {
                relocatedSecondKineticHeightChannel1 = maxUsedChannel + 1;
                relocatedSecondKineticHeightChannel2 = maxUsedChannel + 2;
                kineticChannelsRelocated = true;
            }
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
                Debug.LogError("Error initializing DMX: " + e.Message);
                isDmxInitialized = false;
            }
        }

        public void RecalculateOffsets()
        {
            int currentOffset = 1;

            for (int i = 0; i < ledStrips.Count; i++)
            {
                LEDStrip strip = ledStrips[i];
                strip.dmxChannelOffset = currentOffset;
                currentOffset += strip.GetTotalChannels();
                Debug.Log($"Strip {i}: {strip.name} - Offset: {strip.dmxChannelOffset}");
            }

            CalculateTotalLedChannels();
            RelocateKineticChannelsIfNeeded();
        }

        public void UpdateSynthParameters(float speed)
        {
            if (!Mathf.Approximately(currentSpeed, speed))
            {
                currentSpeed = speed;

                if (Mathf.Approximately(currentSpeed, 0f))
                {
                    kineticPaused = true;
                    pausedKineticValue = kineticCurrentValue;
                    pausedSecondKineticValue = secondKineticCurrentValue;
                }
                else
                {
                    kineticPaused = false;
                    // При выходе из паузы сбрасываем время, чтобы избежать прыжка
                    kineticStartTime = Time.time;
                }
            }
        }

        private void HandleStateChanged(AppState newState)
        {
            Debug.Log("[LEDController] State changed to " + newState);
        }

        void FixedUpdate()
        {
            if (!isDmxInitialized)
                InitializeDMX();

            float framesToAdvance = baseFramesPerSecond * currentSpeed * Time.fixedDeltaTime;
            currentFrameSkipCounter++;
            if (currentFrameSkipCounter < frameSkip)
                return;

            foreach (var strip in ledStrips)
            {
                if (idleMode)
                {
                    if (strip.preCalculatedIdleFrames != null && strip.preCalculatedIdleFrames.Length > 0)
                    {
                        strip.currentFrameIdle += framesToAdvance;
                        while (strip.currentFrameIdle >= strip.preCalculatedIdleFrames.Length)
                            strip.currentFrameIdle -= strip.preCalculatedIdleFrames.Length;
                        while (strip.currentFrameIdle < 0)
                            strip.currentFrameIdle += strip.preCalculatedIdleFrames.Length;
                    }
                }
                else
                {
                    if (strip.preCalculatedNormalFrames != null && strip.preCalculatedNormalFrames.Length > 0)
                    {
                        strip.currentFrameActive += framesToAdvance;
                        while (strip.currentFrameActive >= strip.preCalculatedNormalFrames.Length)
                            strip.currentFrameActive -= strip.preCalculatedNormalFrames.Length;
                        while (strip.currentFrameActive < 0)
                            strip.currentFrameActive += strip.preCalculatedNormalFrames.Length;
                    }
                }
            }

            UpdateFrameBuffer();

            if (dmxCommunicator != null && dmxCommunicator.IsActive)
            {
                dmxCommunicator.SendFrame(FrameBuffer);
            }

            if (enableFrameDebug)
            {
                StringBuilder sb = new StringBuilder();
                int bytesToLog = 512;
                for (int i = 1; i < FrameBuffer.Length && i <= bytesToLog; i++)
                {
                    sb.Append(FrameBuffer[i]).Append(" ");
                }
                Debug.Log($"Sent DMX Frame (first {bytesToLog} bytes): {sb.ToString()}");
                if (debugText != null)
                    debugText.text = sb.ToString();
            }

            currentFrameSkipCounter = 0;
        }

        private bool IsKineticChannel(int channel)
        {
            return (channel == relocatedKineticHeightChannel1 || channel == relocatedKineticHeightChannel2
                 || channel == relocatedSecondKineticHeightChannel1 || channel == relocatedSecondKineticHeightChannel2);
        }

        private void WriteToDMXChannel(byte[] buffer, int channel, byte value)
        {
            if (channel >= 1 && channel <= 512)
                buffer[channel] = value;
        }

        void UpdateFrameBuffer()
        {
            switch (currentMode)
            {
                case DisplayMode.GlobalColor:
                    ClearLEDChannels();
                    BuildGlobalColorBuffer();
                    break;
                case DisplayMode.SegmentColor:
                    ClearLEDChannels();
                    BuildSegmentColorBuffer();
                    break;
                case DisplayMode.JsonDataSync:
                    ClearLEDChannels();
                    BuildJsonDataSyncBuffer();
                    break;
                case DisplayMode.TestMode:
                    ClearLEDChannels();
                    BuildTestModeBuffer();
                    break;
                case DisplayMode.JsonMixByte:
                    ClearLEDChannels();
                    BuildJsonMixByteBuffer();
                    break;
            }

            if (currentMode != DisplayMode.JsonMixByte)
            {
            UpdateKineticControl();

            }
        }

        private void ClearLEDChannels()
        {
            for (int ch = 1; ch <= 512; ch++)
            {
                if (!IsKineticChannel(ch))
                    FrameBuffer[ch] = 0;
            }
        }

        void UpdateKineticControl()
        {
            if (idleMode)
            {
                if (wasIdled)
                {
                    WriteToDMXChannel(FrameBuffer, relocatedKineticHeightChannel1, 0);
                    WriteToDMXChannel(FrameBuffer, relocatedKineticHeightChannel2, 0);
                    WriteToDMXChannel(FrameBuffer, relocatedSecondKineticHeightChannel1, 0);
                    WriteToDMXChannel(FrameBuffer, relocatedSecondKineticHeightChannel2, 0);
                    wasIdled = false;
                }
                return;
            }

            mediaPlayer ??= new DemolitionMediaPlayer(Media.Instance);

            if (!confirmTime)
            {
                videoLength = mediaPlayer.DurationSeconds;
                // Если длительность недопустима – задаём значение по умолчанию
                if (videoLength <= 0f)
                    videoLength = 1f;
                kineticStartTime = Time.time;
                // Сбрасываем кинетические значения при старте
                kineticCurrentValue = 0f;
                secondKineticCurrentValue = 0f;
                confirmTime = true;
            }

            if (kineticPaused)
            {
                SetKineticDMXChannels(pausedKineticValue, pausedSecondKineticValue);
                return;
            }

            // Вычисляем время с учётом currentSpeed и нормализуем по длительности видео для цикличности
            float elapsedTime = (Time.time - kineticStartTime) * currentSpeed;
            float normalizedTime = (elapsedTime % videoLength) / videoLength;

            // Получаем новые значения из кривых от 0 до 1
            float newKineticValue = kineticControlCurve.Evaluate(normalizedTime);
            float newSecondKineticValue = secondKineticControlCurve.Evaluate(normalizedTime);

            // Плавное приближение (сглаживание) текущих значений
            kineticCurrentValue = SmoothApproach(kineticCurrentValue, newKineticValue, 2f);
            secondKineticCurrentValue = SmoothApproach(secondKineticCurrentValue, newSecondKineticValue, 2f);

            SetKineticDMXChannels(kineticCurrentValue, secondKineticCurrentValue);
        }

        private void SetKineticDMXChannels(float kineticValue1, float kineticValue2)
        {
            SetKineticDMXChannel(kineticValue1, relocatedKineticHeightChannel1, relocatedKineticHeightChannel2);
            SetKineticDMXChannel(kineticValue2, relocatedSecondKineticHeightChannel1, relocatedSecondKineticHeightChannel2);
        }

        private void SetKineticDMXChannel(float curveValue, int channel1, int channel2)
        {
            float max = 255f;
            float min = 0f;

            float minCombinedValue = 0f;
            float maxCombinedValue = (max - min) * 256f + (max - min);

            float combinedValue = Mathf.Lerp(minCombinedValue, maxCombinedValue, Mathf.Clamp01(curveValue));

            byte firstByte = (byte)Mathf.Clamp(Mathf.Floor(combinedValue / 256f) + min, min, max);
            byte secondByte = (byte)Mathf.Clamp((combinedValue % 256f) + min, min, max);

            WriteToDMXChannel(FrameBuffer, channel1, firstByte);
            WriteToDMXChannel(FrameBuffer, channel2, secondByte);
        }

        private float SmoothApproach(float current, float target, float speed)
        {
            return Mathf.MoveTowards(current, target, speed * Time.fixedDeltaTime);
        }

        int GetChannelsPerLed(LEDStrip strip)
        {
            return strip.jsonFormat switch
            {
                ColorFormat.RGB => 3,
                ColorFormat.RGBW => 4,
                ColorFormat.HSV => 3,
                ColorFormat.RGBWMix => 5,
                _ => 3,
            };
        }

        void BuildGlobalColorBuffer()
        {
            foreach (var strip in ledStrips)
            {
                int channelsPerLed = GetChannelsPerLed(strip);
                int totLEDs = strip.totalLEDs;
                int offset = strip.dmxChannelOffset;
                Color color = strip.globalColor;
                float brightness = strip.brightness;
                byte r = (byte)(color.r * brightness * 255);
                byte g = (byte)(color.g * brightness * 255);
                byte b = (byte)(color.b * brightness * 255);

                for (int i = 0; i < totLEDs; i++)
                {
                    int baseChannel = offset + i * channelsPerLed;
                    if (baseChannel + channelsPerLed - 1 > 512)
                        break;

                    for (int j = 0; j < channelsPerLed; j++)
                    {
                        int absChannel = baseChannel + j;
                        if (IsKineticChannel(absChannel)) continue;

                        switch (j)
                        {
                            case 0:
                                WriteToDMXChannel(FrameBuffer, absChannel, r);
                                break;
                            case 1:
                                WriteToDMXChannel(FrameBuffer, absChannel, g);
                                break;
                            case 2:
                                WriteToDMXChannel(FrameBuffer, absChannel, b);
                                break;
                            case 3:
                                byte w = (byte)(Mathf.Min(r, g, b) * 0.8f);
                                WriteToDMXChannel(FrameBuffer, absChannel, w);
                                break;
                            case 4:
                                byte cw = (byte)(Mathf.Min(b, g) * 0.8f);
                                WriteToDMXChannel(FrameBuffer, absChannel, cw);
                                break;
                        }
                    }
                }
            }
        }

        void BuildSegmentColorBuffer()
        {
            foreach (var strip in ledStrips)
            {
                int channelsPerLed = GetChannelsPerLed(strip);
                int segments = strip.TotalSegments;
                int offset = strip.dmxChannelOffset;
                float brightness = strip.brightness;

                for (int seg = 0; seg < segments; seg++)
                {
                    byte r = 0, g = 0, b = 0, w = 0, cw = 0;
                    if (seg < strip.segmentColors.Length && strip.segmentActiveStates[seg])
                    {
                        Color col = strip.segmentColors[seg];
                        r = (byte)(col.r * brightness * 255);
                        g = (byte)(col.g * brightness * 255);
                        b = (byte)(col.b * brightness * 255);
                        if (channelsPerLed >= 4)
                            w = (byte)(Mathf.Min(r, g, b) * 0.8f);
                        if (channelsPerLed >= 5)
                            cw = (byte)(Mathf.Min(b, g) * 0.8f);
                    }

                    int i = seg * strip.ledsPerSegment;
                    int baseChannel = offset + i * channelsPerLed;
                    if (baseChannel + channelsPerLed - 1 > 512)
                        break;

                    for (int j = 0; j < channelsPerLed; j++)
                    {
                        int absChannel = baseChannel + j;
                        if (IsKineticChannel(absChannel)) continue;

                        switch (j)
                        {
                            case 0:
                                WriteToDMXChannel(FrameBuffer, absChannel, r);
                                break;
                            case 1:
                                WriteToDMXChannel(FrameBuffer, absChannel, g);
                                break;
                            case 2:
                                WriteToDMXChannel(FrameBuffer, absChannel, b);
                                break;
                            case 3:
                                WriteToDMXChannel(FrameBuffer, absChannel, w);
                                break;
                            case 4:
                                WriteToDMXChannel(FrameBuffer, absChannel, cw);
                                break;
                        }
                    }
                }
            }
        }

        void BuildJsonDataSyncBuffer()
        {
            foreach (var strip in ledStrips)
            {
                PreCalculatedDmxFrame[] frames = idleMode ? strip.preCalculatedIdleFrames : strip.preCalculatedNormalFrames;
                float currentFrame = idleMode ? strip.currentFrameIdle : strip.currentFrameActive;

                if (frames == null || frames.Length == 0)
                    continue;

                int frameIndex = Mathf.FloorToInt(currentFrame);
                frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);

                byte[] stripValues = frames[frameIndex].channelValues;
                float brightness = strip.brightness;

                for (int i = 0; i < stripValues.Length; i++)
                {
                    int globalChannel = i + 1; // DMX-каналы от 1 до 512

                    if (globalChannel >= 1 && globalChannel <= 512 && !IsKineticChannel(globalChannel))
                    {
                        byte currentValue = FrameBuffer[globalChannel];
                        byte newValue = (byte)(stripValues[i] * brightness);
                        FrameBuffer[globalChannel] = (byte)Mathf.Min(currentValue + newValue, 255);
                    }
                }
            }
        }

        void BuildTestModeBuffer()
        {
            if (mediaPlayer == null)
                mediaPlayer = new DemolitionMediaPlayer(Media.Instance);

            float currentTime = Time.time - kineticStartTime;
            float curveValue = testModeCurve.Evaluate(currentTime / videoLength);

            foreach (var strip in ledStrips)
            {
                int totLEDs = strip.totalLEDs;
                int offset = strip.dmxChannelOffset;
                int channelsPerLed = GetChannelsPerLed(strip);
                float brightness = strip.brightness;
                byte intensity = (byte)(curveValue * brightness * 255);

                for (int i = 0; i < totLEDs; i++)
                {
                    int baseChannel = offset + i * channelsPerLed;
                    if (baseChannel + channelsPerLed - 1 > 512)
                        break;

                    for (int j = 0; j < channelsPerLed; j++)
                    {
                        int absChannel = baseChannel + j;
                        if (absChannel > 512)
                            break;
                        if (IsKineticChannel(absChannel))
                            continue;
                        WriteToDMXChannel(FrameBuffer, absChannel, intensity);
                    }
                }
            }
        }

        void BuildJsonMixByteBuffer()
        {
            if (idleMode)
            {
                BuildJsonDataSyncBuffer();
            }
            else
            {
                int frameIndex = Mathf.FloorToInt(Time.time * 60); 
                frameIndex = Mathf.Clamp(frameIndex, 0, byteArrayFrames.Length - 1);

                byte[] frameData = byteArrayFrames[frameIndex];

                Array.Copy(frameData, 0, FrameBuffer, 1, frameData.Length);

                if (enableFrameDebug)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 1; i < FrameBuffer.Length; i++)
                    {
                        sb.Append(FrameBuffer[i]).Append(" ");
                    }
                    Debug.Log($"Sent DMX Frame from byte array: {sb.ToString()}");
                    if (debugText != null)
                        debugText.text = sb.ToString();
                }
            }
        }

        void LoadByteArrayFrames()
        {
            var absPath = Application.streamingAssetsPath + System.IO.Path.AltDirectorySeparatorChar + byteArrayFilePath;

            try
            {
                if (System.IO.File.Exists(absPath))
                {
                    var fl = System.IO.File.OpenRead(absPath);

                    using (var str = new System.IO.BinaryReader(fl))
                    {
                        int frameCount = (int)(str.BaseStream.Length / 512);
                        byteArrayFrames = new byte[frameCount][];

                        for (int i = 0; i < frameCount; i++)
                        {
                            byteArrayFrames[i] = str.ReadBytes(512);
                        }
                    }
                }
            }
            catch (System.Exception err)
            {
                Debug.LogError($"Error loading byte array frames: {err.Message}");
            }
        }

        void OnDestroy()
        {
            if (dmxCommunicator != null)
            {
                dmxCommunicator.Stop();
                TurnOffAllLEDs();
                dmxCommunicator.Dispose();
            }

            if (stateManager != null)
            {
                stateManager.OnStateChanged -= HandleStateChanged;
            }
        }

        private void TurnOffAllLEDs()
        {
            if (dmxCommunicator == null || !dmxCommunicator.IsActive)
                return;

            ClearLEDChannels();
            dmxCommunicator.SendFrame(FrameBuffer);
        }

        public IEnumerator FadeOut(float duration)
        {
            float startBrightness = globalBrightness;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                globalBrightness = Mathf.Lerp(startBrightness, 0f, elapsed / duration);
                yield return null;
                elapsed += Time.deltaTime;
            }
            globalBrightness = 0f;
        }

        public IEnumerator FadeIn(float duration, float targetBrightness)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                globalBrightness = Mathf.Lerp(0f, targetBrightness, elapsed / duration);
                yield return null;
                elapsed += Time.deltaTime;
            }
            globalBrightness = targetBrightness;
        }

        public void SwitchToActiveJSON()
        {
            idleMode = false;
            foreach (var strip in ledStrips)
            {
                strip.ResetFrames();
            }

            kineticStartTime = Time.time;
            confirmTime = false;
        }

        public void SwitchToIdleJSON()
        {
            idleMode = true;
            foreach (var strip in ledStrips)
            {
                strip.ResetFrames();
            }
            wasIdled = true;
            confirmTime = false;
        }

        public void ReloadJsonData()
        {
            foreach (var strip in ledStrips)
            {
                strip.LoadJsonData(true);
            }
        }
    }
}