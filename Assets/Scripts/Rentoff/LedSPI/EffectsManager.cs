using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

namespace LEDControl
{
    public enum SunMode
    {
        Warm,
        Cold
    }

    [Serializable]
    public class SunMovementSettings
    {
        public int pixelCount = 10;
        public float cycleLength = 241f;
        public float startTime = 38f;
        public float endTime = 230f;
        [Range(0f, 1f)]
        public float brightnessMultiplier = 1f;

        // Предварительно рассчитанные значения
        public float fadeTime = 5.0f;
        public float invCycleLength;
        public float invFadeTime;

        public void Initialize()
        {
            invCycleLength = 1f / cycleLength;
            invFadeTime = 1f / fadeTime;
        }
    }

    public struct Comet
    {
        public float position;
        public Color32 color;
        public float length;
        public float brightness;
        public bool isActive;
        public float startTime;
        public bool isMoving;
        public float direction;

        public Comet(float position, Color32 color, float length, float brightness, float direction)
        {
            this.position = position;
            this.color = color;
            this.length = length;
            this.brightness = brightness;
            this.isActive = true;
            this.startTime = Time.time;
            this.isMoving = false;
            this.direction = direction;
        }
    }

    [BurstCompile]
    public struct UpdateCometsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Comet> comets;
        public NativeArray<Comet> updatedComets;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float currentSpeed;
        [ReadOnly] public float cometMoveDelay;
        [ReadOnly] public bool startFromEnd;
        [ReadOnly] public NativeArray<float> lastTouchTimes;
        public NativeArray<bool> canMoveArray;
        public float time;

        public void Execute(int index)
        {
            Comet comet = comets[index];
            if (!comet.isActive)
            {
                updatedComets[index] = comet;
                return;
            }

            bool canMove = time - lastTouchTimes[0] >= cometMoveDelay;
            canMoveArray[0] = canMove;

            if (canMove && !comet.isMoving)
            {
                comet.isMoving = true;
            }

            if (comet.isMoving)
            {
                float directionMultiplier = startFromEnd ?
                    (currentSpeed >= 0 ? -1f : 1f) :
                    (currentSpeed >= 0 ? 1f : -1f);
                comet.position += Mathf.Abs(currentSpeed) * deltaTime * 30f * directionMultiplier;

                if (comet.position < 0)
                    comet.position += updatedComets.Length;
                else if (comet.position >= updatedComets.Length)
                    comet.position -= updatedComets.Length;
            }

            updatedComets[index] = comet;
        }
    }

    [BurstCompile]
    public struct GenerateSpeedSynthHexJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Comet> comets;
        [ReadOnly] public int totalLEDs;
        [ReadOnly] public float stripBrightness;
        [ReadOnly] public float stripGamma;
        [ReadOnly] public bool stripGammaEnabled;
        [ReadOnly] public float currentSpeed;
        [ReadOnly] public float speedBrightnessFactor;
        [ReadOnly] public float tailIntensity;
        [ReadOnly] public float stationaryBrightnessFactor;
        public NativeArray<byte> hexData;

        public void Execute(int index)
        {
            int hexPerPixel = 6; // Для RGB
            int pixelIndex = index / hexPerPixel;
            Color32 pixelColor = new Color32(0, 0, 0, 255);

            foreach (var comet in comets)
            {
                if (!comet.isActive) continue;

                int dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(comet.length));
                float dynamicBrightness = Mathf.Clamp01(comet.brightness * stripBrightness);
                float speedBrightnessMultiplier = 1f + Mathf.Abs(currentSpeed) * speedBrightnessFactor;
                dynamicBrightness *= speedBrightnessMultiplier;

                if (!comet.isMoving)
                    dynamicBrightness *= stationaryBrightnessFactor;

                int ledIndex = Mathf.FloorToInt(comet.position);
                for (int j = 0; j < dynamicLedCount; j++)
                {
                    int offset = comet.direction > 0 ? j : -j;
                    int currentLedIndex = ledIndex + offset;
                    currentLedIndex = Mathf.RoundToInt(Mathf.Repeat(currentLedIndex, totalLEDs));

                    if (currentLedIndex == pixelIndex)
                    {
                        float tailFalloff = 1f - ((float)j / (dynamicLedCount - 1));
                        tailFalloff = Mathf.Clamp01(tailFalloff);
                        float brightnessFactor = tailFalloff * tailIntensity;

                        byte r = (byte)Mathf.Clamp(comet.color.r * brightnessFactor * dynamicBrightness, 0, 255);
                        byte g = (byte)Mathf.Clamp(comet.color.g * brightnessFactor * dynamicBrightness, 0, 255);
                        byte b = (byte)Mathf.Clamp(comet.color.b * brightnessFactor * dynamicBrightness, 0, 255);

                        pixelColor = new Color32(r, g, b, 255);
                        break;
                    }
                }
            }

            // Запись HEX-значений
            hexData[index] = (byte)(pixelColor.r >> 4 | 0x30);
            hexData[index + 1] = (byte)(pixelColor.r & 0x0F | 0x30);
            hexData[index + 2] = (byte)(pixelColor.g >> 4 | 0x30);
            hexData[index + 3] = (byte)(pixelColor.g & 0x0F | 0x30);
            hexData[index + 4] = (byte)(pixelColor.b >> 4 | 0x30);
            hexData[index + 5] = (byte)(pixelColor.b & 0x0F | 0x30);
        }
    }

    [BurstCompile]
    public struct GenerateSunMovementHexJob : IJobParallelFor
    {
        [ReadOnly] public SunMovementSettings settings;
        [ReadOnly] public int totalLEDs;
        [ReadOnly] public float sunMovementPhase;
        [ReadOnly] public float stripBrightness;
        [ReadOnly] public float stripGamma;
        [ReadOnly] public bool stripGammaEnabled;
        [ReadOnly] public Color32 sunColor;
        public NativeArray<byte> hexData;

        public void Execute(int index)
        {
            int hexPerPixel = 6; // Для RGB
            int pixelIndex = index / hexPerPixel;
            Color32 pixelColor = new Color32(0, 0, 0, 255);

            float currentCycleTime = sunMovementPhase * settings.cycleLength;
            bool isActive = currentCycleTime >= settings.startTime && currentCycleTime <= settings.endTime;

            float fadeInFactor = 1.0f;
            float fadeOutFactor = 1.0f;

            if (isActive)
            {
                if (currentCycleTime < settings.startTime + settings.fadeTime)
                {
                    fadeInFactor = (currentCycleTime - settings.startTime) * settings.invFadeTime;
                }
                if (currentCycleTime > settings.endTime - settings.fadeTime)
                {
                    fadeOutFactor = (settings.endTime - currentCycleTime) * settings.invFadeTime;
                }
            }

            if (isActive)
            {
                float activePhase = (currentCycleTime - settings.startTime) * (1f / (settings.endTime - settings.startTime));
                float sunPosition = activePhase * totalLEDs;
                float distance = Mathf.Abs(pixelIndex - sunPosition);
                float brightnessFactor = Mathf.Clamp01(1f - distance / (settings.pixelCount / 2f));
                brightnessFactor *= Mathf.Min(fadeInFactor, fadeOutFactor) * settings.brightnessMultiplier;

                if (brightnessFactor > 0)
                {
                    pixelColor = new Color32(
                        (byte)(sunColor.r * brightnessFactor * stripBrightness),
                        (byte)(sunColor.g * brightnessFactor * stripBrightness),
                        (byte)(sunColor.b * brightnessFactor * stripBrightness),
                        255
                    );
                }
            }

            // Запись HEX-значений
            hexData[index] = (byte)(pixelColor.r >> 4 | 0x30);
            hexData[index + 1] = (byte)(pixelColor.r & 0x0F | 0x30);
            hexData[index + 2] = (byte)(pixelColor.g >> 4 | 0x30);
            hexData[index + 3] = (byte)(pixelColor.g & 0x0F | 0x30);
            hexData[index + 4] = (byte)(pixelColor.b >> 4 | 0x30);
            hexData[index + 5] = (byte)(pixelColor.b & 0x0F | 0x30);
        }
    }

    public class EffectsManager : MonoBehaviour
    {
        [Header("Speed Synth Mode Settings")]
        [SerializeField] public int synthLedCountBase = 5;
        [SerializeField] public float speedLedCountFactor = 0.5f;
        [SerializeField] public float speedBrightnessFactor = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] public float tailIntensity = 0.7f;
        public MoveDirection moveDirection = MoveDirection.Forward;
        [SerializeField] public bool startFromEnd = false;
        [Range(0f, 1f)]
        [SerializeField] public float stationaryBrightnessFactor = 0.5f;

        [Header("Sun Movement Settings")]
        [SerializeField] private SunMovementSettings warmSunSettings = new SunMovementSettings();
        [SerializeField] private SunMovementSettings coldSunSettings = new SunMovementSettings();
        [SerializeField] private StripDataManager stripDataManager;
        [Header("Touch Panel Settings")]
        [SerializeField] public bool toggleTouchMode = false;
        [SerializeField] private float cometMoveDelay = 0.2f;

        public float currentSpeed = 1f;
        public float MultiplySpeed = 2f;
        private float sunMovementPhase = 0f;
        public Dictionary<int, List<Comet>> stripComets = new Dictionary<int, List<Comet>>();
        private Dictionary<int, float> lastTouchTimes = new Dictionary<int, float>();

        private void Start()
        {
            warmSunSettings.Initialize();
            coldSunSettings.Initialize();
        }

        public void UpdateSpeed(float speed)
        {
            currentSpeed = speed * MultiplySpeed;
        }

        public void UpdateComets(int stripIndex, StripDataManager stripManager)
        {
            if (!stripComets.ContainsKey(stripIndex))
            {
                stripComets[stripIndex] = new List<Comet>();
            }

            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            float timeSinceLastUpdate = Time.deltaTime;
            List<Comet> comets = stripComets[stripIndex];

            NativeArray<Comet> nativeComets = new NativeArray<Comet>(comets.ToArray(), Allocator.TempJob);
            NativeArray<Comet> updatedComets = new NativeArray<Comet>(comets.Count, Allocator.TempJob);
            NativeArray<float> lastTouchTimesArray = new NativeArray<float>(1, Allocator.TempJob);
            NativeArray<bool> canMoveArray = new NativeArray<bool>(1, Allocator.TempJob);

            lastTouchTimesArray[0] = lastTouchTimes.ContainsKey(stripIndex) ? lastTouchTimes[stripIndex] : 0f;

            UpdateCometsJob job = new UpdateCometsJob
            {
                comets = nativeComets,
                updatedComets = updatedComets,
                deltaTime = timeSinceLastUpdate,
                currentSpeed = currentSpeed,
                cometMoveDelay = cometMoveDelay,
                startFromEnd = startFromEnd,
                lastTouchTimes = lastTouchTimesArray,
                canMoveArray = canMoveArray,
                time = Time.time
            };

            JobHandle handle = job.Schedule(comets.Count, 64);
            handle.Complete();

            comets.Clear();
            comets.AddRange(updatedComets.ToArray());

            if (canMoveArray[0])
            {
                lastTouchTimes[stripIndex] = Time.time;
            }

            nativeComets.Dispose();
            updatedComets.Dispose();
            lastTouchTimesArray.Dispose();
            canMoveArray.Dispose();
        }

        public void ResetComets(int stripIndex)
        {
            if (stripComets.ContainsKey(stripIndex))
            {
                stripComets[stripIndex].Clear();
            }
            lastTouchTimes.Remove(stripIndex);
        }

        public void AddComet(int stripIndex, float position, Color32 color, float length, float brightness)
        {
            if (!stripComets.ContainsKey(stripIndex))
            {
                stripComets[stripIndex] = new List<Comet>();
            }

            if (startFromEnd)
            {
                position = stripDataManager.totalLEDsPerStrip[stripIndex] - 1;
                moveDirection = MoveDirection.Backward;
            }

            float direction = currentSpeed >= 0 ? 1f : -1f;
            stripComets[stripIndex].Add(new Comet(position, color, length, brightness, direction));
            lastTouchTimes[stripIndex] = Time.time;
        }

        public void UpdateLastTouchTime(int stripIndex)
        {
            lastTouchTimes[stripIndex] = Time.time;
        }

        public void UpdateSunMovementPhase()
        {
            SunMovementSettings settings = warmSunSettings; // Можно использовать coldSunSettings
            float cycleTime = settings.cycleLength / Mathf.Max(0.1f, currentSpeed);
            sunMovementPhase += Time.deltaTime * settings.invCycleLength;
            if (sunMovementPhase >= 1f)
                sunMovementPhase = 0f;
        }

        public bool CheckForChanges()
        {
            // Эта проверка теперь не нужна, так как все параметры предрассчитаны
            return false;
        }

        public string GetHexDataForSpeedSynthMode(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
            NativeArray<byte> hexData = new NativeArray<byte>(totalLEDs * hexPerPixel, Allocator.TempJob);

            if (!stripComets.ContainsKey(stripIndex) || stripComets[stripIndex].Count == 0)
            {
                hexData.Dispose();
                return "";
            }

            List<Comet> comets = stripComets[stripIndex];
            NativeArray<Comet> nativeComets = new NativeArray<Comet>(comets.ToArray(), Allocator.TempJob);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            GenerateSpeedSynthHexJob job = new GenerateSpeedSynthHexJob
            {
                comets = nativeComets,
                totalLEDs = totalLEDs,
                stripBrightness = stripBrightness,
                stripGamma = stripGamma,
                stripGammaEnabled = stripGammaEnabled,
                currentSpeed = currentSpeed,
                speedBrightnessFactor = speedBrightnessFactor,
                tailIntensity = tailIntensity,
                stationaryBrightnessFactor = stationaryBrightnessFactor,
                hexData = hexData
            };

            JobHandle handle = job.Schedule(totalLEDs * hexPerPixel, 64);
            handle.Complete();

            string hexString = System.Text.Encoding.ASCII.GetString(hexData.ToArray());
            hexData.Dispose();
            nativeComets.Dispose();

            return OptimizeHexString(hexString, new string('0', hexPerPixel), hexPerPixel);
        }

        public string GetHexDataForSunMovement(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
            NativeArray<byte> hexData = new NativeArray<byte>(totalLEDs * hexPerPixel, Allocator.TempJob);

            SunMovementSettings settings = stripManager.GetSunMode(stripIndex) == SunMode.Warm ? warmSunSettings : coldSunSettings;
            Color32 sunColor = stripManager.GetSunMode(stripIndex) == SunMode.Warm ?
                new Color32(255, 147, 41, 255) :
                new Color32(173, 216, 230, 255);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            GenerateSunMovementHexJob job = new GenerateSunMovementHexJob
            {
                settings = settings,
                totalLEDs = totalLEDs,
                sunMovementPhase = sunMovementPhase,
                stripBrightness = stripBrightness,
                stripGamma = stripGamma,
                stripGammaEnabled = stripGammaEnabled,
                sunColor = sunColor,
                hexData = hexData
            };

            JobHandle handle = job.Schedule(totalLEDs * hexPerPixel, 64);
            handle.Complete();

            string hexString = System.Text.Encoding.ASCII.GetString(hexData.ToArray());
            hexData.Dispose();

            return OptimizeHexString(hexString, new string('0', hexPerPixel), hexPerPixel);
        }

        private string OptimizeHexString(string hexString, string blackHex, int hexPerPixel)
        {
            int totalPixels = hexString.Length / hexPerPixel;
            while (totalPixels > 0)
            {
                string lastPixel = hexString.Substring((totalPixels - 1) * hexPerPixel, hexPerPixel);
                if (lastPixel.Equals(blackHex, StringComparison.OrdinalIgnoreCase))
                    totalPixels--;
                else
                    break;
            }
            return hexString.Substring(0, totalPixels * hexPerPixel);
        }

        public void HandleTouchInput(int stripIndex, int touchCol, StripDataManager stripManager, StateManager.AppState appState)
        {
            if (appState != StateManager.AppState.Active) return;

            int segmentIndex = touchCol + stripManager.touchPanelOffset;
            if (segmentIndex < 0 || segmentIndex >= stripManager.GetTotalSegments(stripIndex)) return;

            Color32 currentColor = stripManager.GetSegmentColor(stripIndex, segmentIndex);
            Color32 blackColor = new Color32(0, 0, 0, 255);
            Color32 synthColor = stripManager.GetSynthColorForStrip(stripIndex);

            if (toggleTouchMode && !currentColor.Equals(blackColor))
            {
                stripManager.SetSegmentColor(stripIndex, segmentIndex, blackColor);
            }
            else if (currentColor.Equals(blackColor))
            {
                stripManager.SetSegmentColor(stripIndex, segmentIndex, synthColor);
                float dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(synthLedCountBase + Mathf.Abs(currentSpeed) * speedLedCountFactor));
                float dynamicBrightness = Mathf.Clamp01(stripManager.GetStripBrightness(stripIndex) + Mathf.Abs(currentSpeed) * speedBrightnessFactor);
                AddComet(stripIndex, segmentIndex * stripManager.ledsPerSegment, synthColor, dynamicLedCount, dynamicBrightness);
            }
        }

        private void OnDestroy()
        {
            foreach (var pair in stripComets)
            {
                pair.Value.Clear();
            }
            stripComets.Clear();
            lastTouchTimes.Clear();
        }
    }
}