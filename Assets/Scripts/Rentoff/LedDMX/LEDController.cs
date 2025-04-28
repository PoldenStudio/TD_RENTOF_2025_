using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DemolitionStudios.DemolitionMedia;
using static StateManager;
using System.IO;

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

        [Header("Transition Curves and Durations")]
        [SerializeField] private AnimationCurve StartFadeIn_R;
        [SerializeField] private AnimationCurve StartFadeIn_G;
        [SerializeField] private AnimationCurve StartFadeIn_B;
        [SerializeField] private AnimationCurve StartFadeInGlobalBrightness;
        [SerializeField] private float StartFadeInDuration = 2f;

        [SerializeField] private AnimationCurve StartFadeOut_R;
        [SerializeField] private AnimationCurve StartFadeOut_G;
        [SerializeField] private AnimationCurve StartFadeOut_B;
        [SerializeField] private AnimationCurve StartFadeOutBrightness;
        [SerializeField] private float StartFadeOutDuration = 2f;

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

        private float kineticCurrentValue = 0f;
        private float secondKineticCurrentValue = 0f;

        private bool kineticPaused = false;
        private float pausedKineticValue = 0f;
        private float pausedSecondKineticValue = 0f;

        [Serializable]
        public class Offset_Settings
        {
            public int leftAmbilightIndex = 0;
            public int rightAmbilightIndex = 140;
            public int upperAmbilightIndex = 400;
            public int lowerAmbilightIndex = 410;
            public int kineticLIndex = 509;
            public int kineticRIndex = 511;
        }

        [Header("JsonMixByte Settings")]
        [SerializeField] private string byteArrayFilePath = "keys.data";
        [SerializeField] private Offset_Settings st;
        private byte[][] byteArrayFrames;
        private byte[][] preCalculatedMixByteDMXFrames;


        [Header("External Light Settings")]
        [Range(1, 512)] public int externalLightChannel = 500;
        [Range(0, 255)] public byte externalLightValueIdle = 0;
        [Range(0, 255)] public byte externalLightValueActive = 255;

        private float currentRFactor = 1f;
        private float currentGFactor = 1f;
        private float currentBFactor = 1f;
        private float currentGlobalBrightnessFactor = 1f;

        private Coroutine currentTransitionCoroutine = null;

        private void Awake()
        {
            if (Settings.Instance != null)
            {
                comPortName = Settings.Instance.dmxComPortName;
                baudRate = Settings.Instance.dmxBaudRate;
            }

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
            PreCalculateMixByteFrames();

            currentRFactor = 1f;
            currentGFactor = 1f;
            currentBFactor = 1f;
            currentGlobalBrightnessFactor = 1f;
        }

        void CalculateTotalLedChannels()
        {
            totalLedChannels = 0;
            foreach (var strip in ledStrips)
            {
                int stripEndChannel = strip.dmxChannelOffset - 1 + strip.GetTotalChannels();
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
                    if (mediaPlayer != null && !mediaPlayer.IsPlaying)
                    {
                        kineticStartTime = Time.time;
                    }
                }
            }
        }

        private void HandleStateChanged(AppState newState)
        {
        }

        void FixedUpdate()
        {
            if (!isDmxInitialized) InitializeDMX();
            if (!isDmxInitialized) return;


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
            ClearLEDChannels();
            switch (currentMode)
            {
                case DisplayMode.GlobalColor:
                    BuildGlobalColorBuffer();
                    break;
                case DisplayMode.SegmentColor:
                    BuildSegmentColorBuffer();
                    break;
                case DisplayMode.JsonDataSync:
                    BuildJsonDataSyncBuffer();
                    break;
                case DisplayMode.TestMode:
                    BuildTestModeBuffer();
                    break;
                case DisplayMode.JsonMixByte:
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

            if (!confirmTime && mediaPlayer != null && mediaPlayer.DurationSeconds > 0)
            {
                videoLength = mediaPlayer.DurationSeconds;
                kineticStartTime = Time.time;
                kineticCurrentValue = 0f;
                secondKineticCurrentValue = 0f;
                confirmTime = true;
            }
            else if (mediaPlayer == null || mediaPlayer.DurationSeconds <= 0)
            {
                confirmTime = false; // Wait for valid media player
            }


            if (kineticPaused)
            {
                SetKineticDMXChannels(pausedKineticValue, pausedSecondKineticValue);
                return;
            }

            if (!confirmTime) return; // Don't proceed if videoLength is not set

            float elapsedTime = (Time.time - kineticStartTime) * currentSpeed;
            float normalizedTime = (elapsedTime % videoLength) / videoLength;

            float newKineticValue = kineticControlCurve.Evaluate(normalizedTime);
            float newSecondKineticValue = secondKineticControlCurve.Evaluate(normalizedTime);

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
                Color baseColor = strip.globalColor;

                Color color = new Color(
                    baseColor.r * currentRFactor,
                    baseColor.g * currentGFactor,
                    baseColor.b * currentBFactor,
                    baseColor.a);


                float brightness = strip.brightness * currentGlobalBrightnessFactor * globalBrightness;


                byte r = (byte)(color.r * brightness * 255);
                byte g = (byte)(color.g * brightness * 255);
                byte b = (byte)(color.b * brightness * 255);

                for (int i = 0; i < totLEDs; i++)
                {
                    int baseChannel = offset + i * channelsPerLed;
                    if (baseChannel + channelsPerLed - 1 > 512) break;


                    for (int j = 0; j < channelsPerLed; j++)
                    {
                        int absChannel = baseChannel + j;
                        if (IsKineticChannel(absChannel)) continue;

                        switch (j)
                        {
                            case 0: WriteToDMXChannel(FrameBuffer, absChannel, r); break;
                            case 1: WriteToDMXChannel(FrameBuffer, absChannel, g); break;
                            case 2: WriteToDMXChannel(FrameBuffer, absChannel, b); break;
                            case 3: // W for RGBW or RGBWMix
                                byte w = (byte)(Mathf.Min(r, g, b) * 0.8f * brightness);
                                WriteToDMXChannel(FrameBuffer, absChannel, w);
                                break;
                            case 4: // MixW for RGBWMix
                                byte cw = (byte)(Mathf.Min(b, g) * 0.8f * brightness);
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

                float currentStripBrightness = strip.brightness * currentGlobalBrightnessFactor * globalBrightness;


                for (int seg = 0; seg < segments; seg++)
                {
                    byte r = 0, g = 0, b = 0, w = 0, cw = 0;
                    if (seg < strip.segmentColors.Length && strip.segmentActiveStates[seg])
                    {
                        Color baseCol = strip.segmentColors[seg];
                        Color col = new Color(
                            baseCol.r * currentRFactor,
                            baseCol.g * currentGFactor,
                            baseCol.b * currentBFactor,
                            baseCol.a);

                        r = (byte)(col.r * currentStripBrightness * 255);
                        g = (byte)(col.g * currentStripBrightness * 255);
                        b = (byte)(col.b * currentStripBrightness * 255);
                        if (channelsPerLed >= 4)
                            w = (byte)(Mathf.Min(r, g, b) * 0.8f * currentStripBrightness);
                        if (channelsPerLed >= 5)
                            cw = (byte)(Mathf.Min(b, g) * 0.8f * currentStripBrightness);

                    }

                    for (int ledInSeg = 0; ledInSeg < strip.ledsPerSegment; ledInSeg++)
                    {
                        int ledIndex = seg * strip.ledsPerSegment + ledInSeg;
                        if (ledIndex >= strip.totalLEDs) break;

                        int baseChannel = offset + ledIndex * channelsPerLed;
                        if (baseChannel + channelsPerLed - 1 > 512) break;


                        for (int j = 0; j < channelsPerLed; j++)
                        {
                            int absChannel = baseChannel + j;
                            if (IsKineticChannel(absChannel)) continue;

                            switch (j)
                            {
                                case 0: WriteToDMXChannel(FrameBuffer, absChannel, r); break;
                                case 1: WriteToDMXChannel(FrameBuffer, absChannel, g); break;
                                case 2: WriteToDMXChannel(FrameBuffer, absChannel, b); break;
                                case 3: WriteToDMXChannel(FrameBuffer, absChannel, w); break;
                                case 4: WriteToDMXChannel(FrameBuffer, absChannel, cw); break;
                            }
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

                if (frames == null || frames.Length == 0) continue;

                int frameIndex = Mathf.FloorToInt(currentFrame);
                frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);

                byte[] precalculatedStripDmxValues = frames[frameIndex].channelValues;
                float brightness = strip.brightness * currentGlobalBrightnessFactor * globalBrightness;
                int channelsPerLed = GetChannelsPerLed(strip);

                for (int ledIndex = 0; ledIndex < strip.totalLEDs; ledIndex++)
                {
                    for (int chInLed = 0; chInLed < channelsPerLed; chInLed++)
                    {
                        int globalChannel = strip.dmxChannelOffset + ledIndex * channelsPerLed + chInLed;

                        if (globalChannel >= 1 && globalChannel <= 512 && !IsKineticChannel(globalChannel))
                        {
                            byte originalValue = precalculatedStripDmxValues[globalChannel];
                            byte valueWithBrightness = (byte)(originalValue * brightness);
                            byte finalValue = valueWithBrightness;

                            if (chInLed == 0) // R
                                finalValue = (byte)(valueWithBrightness * currentRFactor);
                            else if (chInLed == 1) // G
                                finalValue = (byte)(valueWithBrightness * currentGFactor);
                            else if (chInLed == 2) // B
                                finalValue = (byte)(valueWithBrightness * currentBFactor);

                            byte currentValueInBuffer = FrameBuffer[globalChannel];
                            FrameBuffer[globalChannel] = (byte)Mathf.Clamp(currentValueInBuffer + finalValue, 0, 255);
                        }
                    }
                }
            }
            WriteToDMXChannel(FrameBuffer, externalLightChannel, (byte)(externalLightValueIdle));
        }


        void BuildTestModeBuffer()
        {
            mediaPlayer ??= new DemolitionMediaPlayer(Media.Instance);

            if (mediaPlayer == null || mediaPlayer.DurationSeconds <= 0)
            {
                if (videoLength <= 0) videoLength = 1f; // Fallback
            }
            else
            {
                videoLength = mediaPlayer.DurationSeconds;
            }


            float currentTime = Time.time - kineticStartTime;
            float curveValue = testModeCurve.Evaluate(currentTime / videoLength);

            foreach (var strip in ledStrips)
            {
                int totLEDs = strip.totalLEDs;
                int offset = strip.dmxChannelOffset;
                int channelsPerLed = GetChannelsPerLed(strip);

                float brightness = strip.brightness * currentGlobalBrightnessFactor * globalBrightness;
                byte intensity = (byte)(curveValue * brightness * 255);


                for (int i = 0; i < totLEDs; i++)
                {
                    int baseChannel = offset + i * channelsPerLed;
                    if (baseChannel + channelsPerLed - 1 > 512) break;

                    for (int j = 0; j < channelsPerLed; j++)
                    {
                        int absChannel = baseChannel + j;
                        if (absChannel > 512) break;
                        if (IsKineticChannel(absChannel)) continue;
                        WriteToDMXChannel(FrameBuffer, absChannel, intensity);
                    }
                }
            }
        }

        void PreCalculateMixByteFrames()
        {
            if (byteArrayFrames == null || byteArrayFrames.Length == 0)
            {
                preCalculatedMixByteDMXFrames = null;
                return;
            }

            preCalculatedMixByteDMXFrames = new byte[byteArrayFrames.Length][];

            for (int f = 0; f < byteArrayFrames.Length; f++)
            {
                byte[] frameIn = byteArrayFrames[f];
                byte[] dmxFrame = new byte[513];
                int offset = 0;

                void CopyBytesToDmx(int dmxStartIndex, int byteCount)
                {
                    for (int i = 0; i < byteCount && offset < frameIn.Length; i++)
                    {
                        int dmxChannel = dmxStartIndex + i;
                        if (dmxChannel >= 1 && dmxChannel <= 512)
                        {
                            dmxFrame[dmxChannel] = frameIn[offset++];
                        }
                        else
                        {
                            offset++; // consume byte even if out of DMX range
                        }
                    }
                }

                CopyBytesToDmx(st.leftAmbilightIndex, 140);
                CopyBytesToDmx(st.rightAmbilightIndex, 140);
                CopyBytesToDmx(st.upperAmbilightIndex, 5);
                CopyBytesToDmx(st.lowerAmbilightIndex, 5);
                CopyBytesToDmx(st.kineticLIndex, 2);
                CopyBytesToDmx(st.kineticRIndex, 2);

                preCalculatedMixByteDMXFrames[f] = dmxFrame;
            }
        }

        void ApplyFadeToDmxRange(byte[] buffer, int startIndex, int count, ColorFormat format)
        {
            int channelsPerPixel = format switch
            {
                ColorFormat.RGB => 3,
                ColorFormat.RGBW => 4,
                ColorFormat.RGBWMix => 5,
                _ => 3
            };

            for (int i = 0; i < count; i++)
            {
                int currentDmxChannel = startIndex + i;
                if (currentDmxChannel >= buffer.Length || currentDmxChannel < 1) continue;
                if (IsKineticChannel(currentDmxChannel)) continue;

                int channelInPixel = i % channelsPerPixel;
                float effectiveBrightness = globalBrightness * currentGlobalBrightnessFactor;


                if (channelInPixel == 0) // R
                    buffer[currentDmxChannel] = (byte)(buffer[currentDmxChannel] * effectiveBrightness * currentRFactor);
                else if (channelInPixel == 1) // G
                    buffer[currentDmxChannel] = (byte)(buffer[currentDmxChannel] * effectiveBrightness * currentGFactor);
                else if (channelInPixel == 2) // B
                    buffer[currentDmxChannel] = (byte)(buffer[currentDmxChannel] * effectiveBrightness * currentBFactor);
                else // W or MixW
                    buffer[currentDmxChannel] = (byte)(buffer[currentDmxChannel] * effectiveBrightness);
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
                if (preCalculatedMixByteDMXFrames != null && preCalculatedMixByteDMXFrames.Length > 0)
                {
                    mediaPlayer ??= new DemolitionMediaPlayer(Media.Instance);
                    int videoFrameIndex = (mediaPlayer != null) ? mediaPlayer.VideoCurrentFrame : 0;

                    int byteArrayFrameIndex = videoFrameIndex % preCalculatedMixByteDMXFrames.Length;

                    Array.Copy(preCalculatedMixByteDMXFrames[byteArrayFrameIndex], 1, FrameBuffer, 1, 512);


/*                    if (currentTransitionCoroutine != null || currentGlobalBrightnessFactor != 1.0f || currentRFactor != 1.0f || currentGFactor != 1.0f || currentBFactor != 1.0f || globalBrightness != defaultGlobalBrightness)
                    {
                        ApplyFadeToDmxRange(FrameBuffer, st.leftAmbilightIndex, 140, ColorFormat.RGB);
                        ApplyFadeToDmxRange(FrameBuffer, st.rightAmbilightIndex, 140, ColorFormat.RGB);
                        ApplyFadeToDmxRange(FrameBuffer, st.upperAmbilightIndex, 5, ColorFormat.RGBWMix);
                        ApplyFadeToDmxRange(FrameBuffer, st.lowerAmbilightIndex, 5, ColorFormat.RGBWMix);
                    }*/

                    if (enableFrameDebug && debugText != null)
                    {
                        StringBuilder sb = new();
                        sb.Append($"JsonMixByte Frame Index: {byteArrayFrameIndex} | Data: ");
                        int bytesToLog = Mathf.Min(512, FrameBuffer.Length - 1);
                        for (int i = 1; i <= bytesToLog; i++)
                        {
                            sb.Append(FrameBuffer[i]).Append(" ");
                        }
                        debugText.text = sb.ToString();
                    }
                    WriteToDMXChannel(FrameBuffer, externalLightChannel, (byte)(externalLightValueActive));
                }
                else
                {
                    // ClearLEDChannels(); // Already called in UpdateFrameBuffer
                }
            }
        }


        void LoadByteArrayFrames()
        {
            string absPath = Path.Combine(Application.streamingAssetsPath, byteArrayFilePath);

            try
            {
                if (File.Exists(absPath))
                {
                    using (var fl = File.OpenRead(absPath))
                    using (var str = new BinaryReader(fl))
                    {
                        if (str.BaseStream.Length == 0)
                        {
                            byteArrayFrames = null;
                            return;
                        }
                        int frameSize = 294;
                        int frameCount = (int)(str.BaseStream.Length / frameSize);
                        byteArrayFrames = new byte[frameCount][];

                        for (int i = 0; i < frameCount; i++)
                        {
                            byteArrayFrames[i] = str.ReadBytes(frameSize);
                        }
                    }
                }
                else
                {
                    byteArrayFrames = null;
                }
            }
            catch (Exception err)
            {
                Debug.LogError($"Error loading byte array frames: {err.Message}");
                byteArrayFrames = null;
            }
        }

        void OnDestroy()
        {
            if (dmxCommunicator != null)
            {
                TurnOffAllLEDs();
                dmxCommunicator.Stop();
                dmxCommunicator.Dispose();
            }

            if (stateManager != null)
            {
                stateManager.OnStateChanged -= HandleStateChanged;
            }
        }

        private void TurnOffAllLEDs()
        {
            if (dmxCommunicator == null || !dmxCommunicator.IsActive) return;

            Array.Clear(FrameBuffer, 1, 512);
            dmxCommunicator.SendFrame(FrameBuffer);
        }

        public void StartFadeIn()
        {
            if (currentTransitionCoroutine != null) StopCoroutine(currentTransitionCoroutine);
            currentTransitionCoroutine = StartCoroutine(TransitionCoroutine(
                StartFadeIn_R, StartFadeIn_G, StartFadeIn_B, StartFadeInGlobalBrightness,
                StartFadeInDuration, true));
        }

        public void StartFadeOut()
        {
            if (currentTransitionCoroutine != null) StopCoroutine(currentTransitionCoroutine);
            currentTransitionCoroutine = StartCoroutine(TransitionCoroutine(
                StartFadeOut_R, StartFadeOut_G, StartFadeOut_B, StartFadeOutBrightness,
                StartFadeOutDuration, false));
        }

        private IEnumerator TransitionCoroutine(
            AnimationCurve rCurve, AnimationCurve gCurve, AnimationCurve bCurve,
            AnimationCurve brightnessCurve, float duration, bool persistFactors)
        {
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                float t = duration > 0 ? elapsedTime / duration : 1f;
                currentRFactor = rCurve.Evaluate(t);
                currentGFactor = gCurve.Evaluate(t);
                currentBFactor = bCurve.Evaluate(t);
                currentGlobalBrightnessFactor = brightnessCurve.Evaluate(t);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            currentRFactor = rCurve.Evaluate(1f);
            currentGFactor = gCurve.Evaluate(1f);
            currentBFactor = bCurve.Evaluate(1f);
            currentGlobalBrightnessFactor = brightnessCurve.Evaluate(1f);

            if (!persistFactors)
            {
                currentRFactor = 1f;
                currentGFactor = 1f;
                currentBFactor = 1f;
                currentGlobalBrightnessFactor = 1f;
            }
            currentTransitionCoroutine = null;
        }


        public void SwitchToActiveJSON()
        {
            idleMode = false;
            foreach (var strip in ledStrips) strip.ResetFrames();
            confirmTime = false;
            kineticStartTime = Time.time;
        }
        public void SwitchToIdleJSON()
        {
            idleMode = true;
            foreach (var strip in ledStrips) strip.ResetFrames();
            wasIdled = true;
            confirmTime = false;
        }

        public void ReloadJsonData()
        {
            foreach (var strip in ledStrips)
            {
                strip.LoadJsonData(true);
            }
            PreCalculateMixByteFrames();
        }
    }
}