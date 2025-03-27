using System;
using System.Text;
using UnityEngine;

namespace LEDControl
{
    public class EffectsManager : MonoBehaviour
    {
        [Header("Speed Synth Mode Settings")]
        [Tooltip("Базовое количество диодов в режиме Speed Synth")]
        [SerializeField] private int synthLedCountBase = 5;

        [Tooltip("Множитель яркости в зависимости от скорости")]
        [SerializeField] private float speedBrightnessFactor = 1.5f;

        [Tooltip("Множитель количества диодов в зависимости от скорости")]
        [SerializeField] private float speedLedCountFactor = 0.5f;

        [Tooltip("Интенсивность эффекта 'хвоста кометы'")]
        [Range(0f, 1f)]
        [SerializeField] private float tailIntensity = 0.7f;

        [Tooltip("Направление движения в режиме Speed Synth")]
        public MoveDirection moveDirection = MoveDirection.Forward;

        [Header("Sun Movement Settings")]
        [Tooltip("Number of pixels to show in Sun Movement mode.")]
        [SerializeField] private int sunMovementPixelCount = 10;

        [Tooltip("Total cycle length in seconds for Sun Movement at speed 1.")]
        [SerializeField] private float sunMovementCycleLength = 241f;

        [Tooltip("Start time in seconds for Sun Movement to appear within the cycle.")]
        [SerializeField] private float sunMovementStartTime = 38f;

        [Tooltip("End time in seconds for Sun Movement to disappear within the cycle.")]
        [SerializeField] private float sunMovementEndTime = 230f;

        private float currentSpeed = 1f;
        private float synthPosition = 0f;
        private float sunMovementPhase = 0f;
        private float lastSynthPositionUpdateTime = 0f;

        private int previousSynthLedCountBase;
        private float previousSpeedBrightnessFactor;
        private float previousSpeedLedCountFactor;
        private float previousTailIntensity;
        private MoveDirection previousMoveDirection;

        public void UpdateSpeed(float speed)
        {
            currentSpeed = speed * 3f;
        }

        public void UpdateSynthPosition()
        {
            float timeSinceLastUpdate = Time.fixedDeltaTime;
            synthPosition += currentSpeed * timeSinceLastUpdate * 30f;

            if (synthPosition < 0)
            {
                synthPosition = 0;
            }

            lastSynthPositionUpdateTime = Time.time;
        }

        public void UpdateSunMovementPhase()
        {
            float cycleTime = sunMovementCycleLength / Mathf.Max(0.1f, currentSpeed);
            sunMovementPhase += Time.fixedDeltaTime / cycleTime;
            if (sunMovementPhase >= 1f)
                sunMovementPhase = 0f;
        }

        public bool CheckForChanges()
        {
            bool changed =
                synthLedCountBase != previousSynthLedCountBase ||
                speedBrightnessFactor != previousSpeedBrightnessFactor ||
                speedLedCountFactor != previousSpeedLedCountFactor ||
                tailIntensity != previousTailIntensity ||
                moveDirection != previousMoveDirection;

            if (changed)
            {
                CachePreviousValues();
            }

            return changed;
        }

        private void CachePreviousValues()
        {
            previousSynthLedCountBase = synthLedCountBase;
            previousSpeedBrightnessFactor = speedBrightnessFactor;
            previousSpeedLedCountFactor = speedLedCountFactor;
            previousTailIntensity = tailIntensity;
            previousMoveDirection = moveDirection;
        }

        public string GetHexDataForSpeedSynthMode(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            StringBuilder sb = new(totalLEDs * (mode == DataMode.RGBW ? 8 : 6));
            Color32 synthColor = stripManager.GetSynthColorForStrip(stripIndex);
            Color32 blackColor = new(0, 0, 0, 255);

            int dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(synthLedCountBase + Mathf.Abs(currentSpeed) * speedLedCountFactor));
            float dynamicBrightness = Mathf.Clamp01(colorProcessor.GetGlobalBrightness() + Mathf.Abs(currentSpeed) * speedBrightnessFactor);

            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = blackColor;

                float ledPositionNormalized = Mathf.Repeat(synthPosition, totalLEDs);
                int ledIndex = Mathf.FloorToInt(ledPositionNormalized);

                for (int j = 0; j < dynamicLedCount; j++)
                {
                    int currentLedIndex = ledIndex + j;
                    if (moveDirection == MoveDirection.Backward)
                    {
                        currentLedIndex = ledIndex - j;
                    }
                    currentLedIndex = Mathf.RoundToInt(Mathf.Repeat(currentLedIndex, totalLEDs));

                    if (i == currentLedIndex)
                    {
                        float brightnessFactor = 1f - (float)j / dynamicLedCount * tailIntensity;

                        byte r = (byte)Mathf.Clamp(synthColor.r * brightnessFactor * dynamicBrightness, 0, 255);
                        byte g = (byte)Mathf.Clamp(synthColor.g * brightnessFactor * dynamicBrightness, 0, 255);
                        byte b = (byte)Mathf.Clamp(synthColor.b * brightnessFactor * dynamicBrightness, 0, 255);

                        pixelColor = new Color32(r, g, b, 255);
                        break;
                    }
                }

                if (mode == DataMode.RGBW)
                {
                    sb.Append(colorProcessor.ColorToHexRGBW(pixelColor));
                }
                else
                {
                    sb.Append(colorProcessor.ColorToHexRGB(pixelColor));
                }
            }
            return sb.ToString();
        }

        public string GetHexDataForSunMovement(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            StringBuilder sb = new(totalLEDs * (mode == DataMode.RGBW ? 8 : 6));
            Color32 sunColor = new(255, 255, 255, 255);
            Color32 blackColor = new(0, 0, 0, 255);

            float currentCycleTime = sunMovementPhase * sunMovementCycleLength;
            bool isActive = currentCycleTime >= sunMovementStartTime && currentCycleTime <= sunMovementEndTime;

            float fadeInFactor = 1.0f;
            float fadeOutFactor = 1.0f;
            float fadeTime = 5.0f;

            if (isActive)
            {
                if (currentCycleTime < sunMovementStartTime + fadeTime)
                {
                    fadeInFactor = (currentCycleTime - sunMovementStartTime) / fadeTime;
                }
                if (currentCycleTime > sunMovementEndTime - fadeTime)
                {
                    fadeOutFactor = (sunMovementEndTime - currentCycleTime) / fadeTime;
                }
            }

            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = blackColor;

                if (isActive)
                {
                    float activePhase = (currentCycleTime - sunMovementStartTime) / (sunMovementEndTime - sunMovementStartTime);
                    float sunPosition = activePhase * totalLEDs;
                    float distance = Mathf.Abs(i - sunPosition);
                    float brightnessFactor = Mathf.Clamp01(1f - distance / (sunMovementPixelCount / 2f));
                    brightnessFactor *= Mathf.Min(fadeInFactor, fadeOutFactor);

                    if (brightnessFactor > 0)
                    {
                        pixelColor = new Color32(
                            (byte)(sunColor.r * brightnessFactor * colorProcessor.GetGlobalBrightness()),
                            (byte)(sunColor.g * brightnessFactor * colorProcessor.GetGlobalBrightness()),
                            (byte)(sunColor.b * brightnessFactor * colorProcessor.GetGlobalBrightness()),
                            255
                        );
                    }
                }

                if (mode == DataMode.RGBW)
                {
                    sb.Append(colorProcessor.ColorToHexRGBW(pixelColor));
                }
                else
                {
                    sb.Append(colorProcessor.ColorToHexRGB(pixelColor));
                }
            }

            return sb.ToString();
        }

        public string GetHexDataForSegmentMode(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int ledsPerSegment = stripManager.ledsPerSegment;
            StringBuilder sb = new(totalLEDs * (mode == DataMode.RGBW ? 8 : 6));

            for (int i = 0; i < totalLEDs; ++i)
            {
                int segmentIndex = i / ledsPerSegment;
                Color32 segmentColor = stripManager.GetSegmentColor(stripIndex, segmentIndex);
                float globalBrightness = colorProcessor.GetGlobalBrightness();
                Color32 pixelColor = new(
                    (byte)(segmentColor.r * globalBrightness),
                    (byte)(segmentColor.g * globalBrightness),
                    (byte)(segmentColor.b * globalBrightness),
                    255
                );

                if (mode == DataMode.RGBW)
                {
                    sb.Append(colorProcessor.ColorToHexRGBW(pixelColor));
                }
                else
                {
                    sb.Append(colorProcessor.ColorToHexRGB(pixelColor));
                }
            }
            return sb.ToString();
        }
    }
}