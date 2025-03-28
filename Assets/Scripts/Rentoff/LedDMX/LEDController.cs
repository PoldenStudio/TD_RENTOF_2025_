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
        private IMediaPlayer _mediaPlayer;

        [Header("Frame Rate Control")]
        public float baseFramesPerSecond = 120f;
        public int frameSkip = 1;
        private int currentFrameSkipCounter = 0;
        private float currentSpeed = 1f;

        [Range(0f, 3f)]
        [Tooltip("Глобальная яркость для всех режимов")]
        [SerializeField] public float globalBrightness = 1.0f;
        private float defaultGlobalBrightness;

        [Header("Test Mode Settings")]
        [SerializeField] private AnimationCurve testModeCurve;
        [SerializeField] private float videoLength = 241f;

        [Header("Display Mode")]
        public DisplayMode currentMode = DisplayMode.JsonDataSync;
        public enum DisplayMode { GlobalColor, SegmentColor, JsonDataSync, TestMode }

        [Tooltip("Список LED лент")]
        [SerializeField] private List<LEDStrip> ledStrips = new();

        [Tooltip("Текст отладки (необязательно)")]
        [SerializeField] private Text debugText;

        [Tooltip("Автоматически рассчитывать DMX-смещения")]
        [SerializeField] private bool autoCalculateOffsets = false;

        [Header("Debug Options")]
        [Tooltip("Включить вывод данных отправленного DMX кадра в консоль")]
        public bool enableFrameDebug = false;

        [Header("Kinetic Control Settings")]
        [SerializeField] private AnimationCurve kineticControlCurve;
        [Tooltip("DMX канал для кинетической скорости (исключительно для передачи первого байта высоты)")]
        [Range(1, 512)]
        public int kineticHeightDmxChannel1 = 1;
        [Tooltip("DMX канал для кинетической высоты (в продолжении первого байта)")]
        [Range(1, 512)]
        public int kineticHeightDmxChannel2 = 2;

        private DMXCommunicator dmxCommunicator;
        private bool isDmxInitialized = false;

        private byte[] FrameBuffer = new byte[513];

        public bool idleMode = true;

        private IMediaPlayer mediaPlayer;
        // Фиксируем время старта кинетики при переходе в active, чтобы данные всегда отправлялись с начала
        private float kineticStartTime = 0f;

        private int totalLedChannels = 0;
        private int relocatedKineticHeightChannel1;
        private int relocatedKineticHeightChannel2;
        private bool kineticChannelsRelocated = false;
        private bool confirmTime;

        public float DefaultGlobalBrightness => defaultGlobalBrightness;

        private void Awake()
        {
            defaultGlobalBrightness = globalBrightness;
            InitializeDMX();

            if (ledStrips.Count == 0)
                ledStrips.Add(new LEDStrip());

            // Пересчитываем смещения только если включен автоматический режим
            if (autoCalculateOffsets)
                RecalculateOffsets();

            CalculateTotalLedChannels();
            RelocateKineticChannelsIfNeeded();

            // Предзагрузка JSON для всех лент сразу (active и idle режимы)
            foreach (var strip in ledStrips)
            {
                strip.InitializeSegmentArrays();
                strip.LoadJsonData(true);
            }

            if (stateManager != null)
            {
                stateManager.OnStateChanged += HandleStateChanged;
            }
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
            kineticChannelsRelocated = false;

            if (kineticHeightDmxChannel1 <= totalLedChannels || kineticHeightDmxChannel2 <= totalLedChannels)
            {
                relocatedKineticHeightChannel1 = totalLedChannels + 1;
                relocatedKineticHeightChannel2 = totalLedChannels + 2;
                kineticHeightDmxChannel1 = relocatedKineticHeightChannel1;
                kineticHeightDmxChannel2 = relocatedKineticHeightChannel2;
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
            currentSpeed = speed;
        }

        private void HandleStateChanged(AppState newState)
        {
            Debug.Log("[LEDController] State changed to " + newState);
        }

        void FixedUpdate()
        {
            if (!isDmxInitialized)
            {
                InitializeDMX();
            }

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
            else
            {
                // DMX communicator is not active. Frame buffer updated but not sent.
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
            return (channel == relocatedKineticHeightChannel1 || channel == relocatedKineticHeightChannel2);
        }

        private void WriteToDMXChannel(byte[] buffer, int channel, byte value)
        {
            if (channel >= 1 && channel <= 512)
            {
                buffer[channel] = value;
            }
        }

        void UpdateFrameBuffer()
        {
            Array.Clear(FrameBuffer, 1, FrameBuffer.Length - 1);
            switch (currentMode)
            {
                case DisplayMode.GlobalColor:
                    BuildGlobalColorBuffer(globalBrightness);
                    break;
                case DisplayMode.SegmentColor:
                    BuildSegmentColorBuffer(globalBrightness);
                    break;
                case DisplayMode.JsonDataSync:
                    BuildJsonDataSyncBuffer(globalBrightness);
                    break;
                case DisplayMode.TestMode:
                    BuildTestModeBuffer(globalBrightness);
                    break;
            }
            UpdateKineticControl();
        }


        //исправление логики вычисления кинетики
        void UpdateKineticControl()
        {
            if (idleMode)
            {
                WriteToDMXChannel(FrameBuffer, relocatedKineticHeightChannel1, 0);
                WriteToDMXChannel(FrameBuffer, relocatedKineticHeightChannel2, 0);
            }
            else
            {
                mediaPlayer ??= new DemolitionMediaPlayer(Media.Instance);

                if (!confirmTime)
                {
                    videoLength = mediaPlayer.DurationSeconds;
                    kineticStartTime = Time.time;  //  Сбрасываем стартовое время при инициализации
                    confirmTime = true;
                }

                // Вычисляем нормализованное время с учетом скорости.
                float elapsedTime = (Time.time - kineticStartTime) * currentSpeed;  // Учитываем currentSpeed
                float normalizedTime = elapsedTime / videoLength;

                //  Обрезаем нормализованное время, чтобы оно всегда было в пределах [0, 1] и циклично.
                normalizedTime = normalizedTime % 1f;

                // Получаем значение кривой по высоте.
                float yValue = kineticControlCurve.Evaluate(normalizedTime);
                yValue = Mathf.Clamp01(yValue); //  Ограничение необходимо, но, вероятно, избыточно, т.к. Evaluate возвращает [0,1]

                byte highByte;
                byte lowByte;

                // Разделение значения на два байта
                float combinedValue = yValue * 65535f; // Максимальное значение для 2 байт (2^16 -1)
                highByte = (byte)(combinedValue / 256f); // Старший байт
                lowByte = (byte)(combinedValue % 256f);   // Младший байт

                WriteToDMXChannel(FrameBuffer, relocatedKineticHeightChannel1, highByte);
                WriteToDMXChannel(FrameBuffer, relocatedKineticHeightChannel2, lowByte);
            }
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

        void BuildGlobalColorBuffer(float brightness)
        {
            foreach (var strip in ledStrips)
            {
                int channelsPerLed = GetChannelsPerLed(strip);
                int totLEDs = strip.totalLEDs;
                int offset = strip.dmxChannelOffset;  // Используем ручной offset
                Color color = strip.globalColor;
                byte r = (byte)(color.r * brightness * 255);
                byte g = (byte)(color.g * brightness * 255);
                byte b = (byte)(color.b * brightness * 255);

                for (int i = 0; i < totLEDs; i++)
                {
                    int baseChannel = offset + i * channelsPerLed;

                    if (baseChannel + channelsPerLed - 1 > 512)
                        break;

                    WriteToDMXChannel(FrameBuffer, baseChannel, r);
                    WriteToDMXChannel(FrameBuffer, baseChannel + 1, g);
                    WriteToDMXChannel(FrameBuffer, baseChannel + 2, b);

                    if (channelsPerLed >= 4)
                    {
                        byte w = (byte)(Mathf.Min(r, g, b) * 0.8f);
                        WriteToDMXChannel(FrameBuffer, baseChannel + 3, w);
                    }
                    if (channelsPerLed >= 5)
                    {
                        byte cw = (byte)(Mathf.Min(b, g) * 0.8f);
                        WriteToDMXChannel(FrameBuffer, baseChannel + 4, cw);
                    }
                }
            }
        }

        void BuildSegmentColorBuffer(float brightness)
        {
            foreach (var strip in ledStrips)
            {
                int channelsPerLed = GetChannelsPerLed(strip);
                int totLEDs = strip.totalLEDs;
                int offset = strip.dmxChannelOffset;  // Используем ручной offset
                int segments = strip.TotalSegments;

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

                    WriteToDMXChannel(FrameBuffer, baseChannel, r);
                    WriteToDMXChannel(FrameBuffer, baseChannel + 1, g);
                    WriteToDMXChannel(FrameBuffer, baseChannel + 2, b);
                    if (channelsPerLed >= 4)
                        WriteToDMXChannel(FrameBuffer, baseChannel + 3, w);
                    if (channelsPerLed >= 5)
                        WriteToDMXChannel(FrameBuffer, baseChannel + 4, cw);
                }
            }
        }

        void BuildJsonDataSyncBuffer(float brightness)
        {
            Array.Clear(FrameBuffer, 1, FrameBuffer.Length - 1);

            // Перебираем каждую ленту
            foreach (var strip in ledStrips)
            {
                PreCalculatedDmxFrame[] frames = idleMode ? strip.preCalculatedIdleFrames : strip.preCalculatedNormalFrames;
                float currentFrame = idleMode ? strip.currentFrameIdle : strip.currentFrameActive;

                if (frames == null || frames.Length == 0)
                    continue;

                int frameIndex = Mathf.FloorToInt(currentFrame);
                frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);

                byte[] stripValues = frames[frameIndex].channelValues;
                int offset = strip.dmxChannelOffset;  // Используем ручной offset

                // Перебираем каналы DMX для этой ленты
                for (int i = 0; i < stripValues.Length; i++)
                {
                    int globalChannel = offset + i; // Глобальный канал DMX

                    // Проверяем, что канал в допустимом диапазоне и не является кинетическим
                    if (globalChannel >= 1 && globalChannel <= 512 && !IsKineticChannel(globalChannel))
                    {
                        // Применяем яркость
                        byte newValue = (byte)(stripValues[i] * brightness);
                        // Записываем значение в буфер.  Перезаписываем напрямую, т.к. данные JSON уже с учетом яркости.
                        WriteToDMXChannel(FrameBuffer, globalChannel, newValue);
                    }
                }
            }
        }

        void BuildTestModeBuffer(float brightness)
        {
            if (mediaPlayer == null)
                mediaPlayer = new DemolitionMediaPlayer(Media.Instance);

            float currentTime = Time.time - kineticStartTime;
            float curveValue = testModeCurve.Evaluate(currentTime / videoLength);
            byte intensity = (byte)(curveValue * brightness * 255);

            foreach (var strip in ledStrips)
            {
                int totLEDs = strip.totalLEDs;
                int offset = strip.dmxChannelOffset;  // Используем ручной offset
                int channelsPerLed = GetChannelsPerLed(strip);

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
            Array.Clear(FrameBuffer, 1, FrameBuffer.Length - 1);
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
            // При переходе в active сбрасываем время для кинетики, чтобы начинать отправку данных с начала
            kineticStartTime = Time.time;
        }

        public void SwitchToIdleJSON()
        {
            idleMode = true;
            foreach (var strip in ledStrips)
            {
                strip.ResetFrames();
            }
        }

        // Метод для перезагрузки JSON данных после изменения смещений
        public void ReloadJsonData()
        {
            foreach (var strip in ledStrips)
            {
                strip.LoadJsonData(true);
            }
        }
    }
}