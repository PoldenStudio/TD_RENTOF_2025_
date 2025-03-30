using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        [Tooltip("Number of pixels to show in Sun Movement mode.")]
        public int pixelCount = 10;

        [Tooltip("Total cycle length in seconds for Sun Movement at speed 1.")]
        public float cycleLength = 241f;

        [Tooltip("Start time in seconds for Sun Movement to appear within the cycle.")]
        public float startTime = 38f;

        [Tooltip("End time in seconds for Sun Movement to disappear within the cycle.")]
        public float endTime = 230f;

        [Tooltip("Brightness multiplier for this sun mode.")]
        [Range(0f, 1f)]
        public float brightnessMultiplier = 1f;
    }

    public class Comet
    {
        public float position;
        public Color32 color;
        public float length;
        public float brightness;
        public bool isActive;
        public float startTime; // Время создания кометы
        public bool isMoving; // Флаг, указывающий, движется ли комета
        public float direction; // Текущее направление движения (1 для Forward, -1 для Backward)

        public Comet(float position, Color32 color, float length, float brightness, float direction)
        {
            this.position = position;
            this.color = color;
            this.length = length;
            this.brightness = brightness;
            this.isActive = true;
            this.startTime = Time.time;
            this.isMoving = false; // Комета неподвижна при создании
            this.direction = direction;
        }
    }

    public class EffectsManager : MonoBehaviour
    {
        [Header("Speed Synth Mode Settings")]
        [Tooltip("Базовое количество диодов в режиме Speed Synth")]
        [SerializeField] public int synthLedCountBase = 5;

        [Tooltip("Множитель длины хвоста в зависимости от скорости")]
        [SerializeField] public float speedLedCountFactor = 0.5f;

        [Tooltip("Множитель яркости в зависимости от скорости")]
        [SerializeField] public float speedBrightnessFactor = 1.5f;

        [Tooltip("Интенсивность эффекта 'хвоста кометы'")]
        [Range(0f, 1f)]
        [SerializeField] public float tailIntensity = 0.7f;

        [Tooltip("Направление движения в режиме Speed Synth")]
        public MoveDirection moveDirection = MoveDirection.Forward;

        [Tooltip("Режим движения комет с конца к началу ленты")]
        [SerializeField] public bool startFromEnd = false;

        [Tooltip("Множитель яркости для неподвижной кометы")]
        [Range(0f, 1f)]
        [SerializeField] public float stationaryBrightnessFactor = 0.5f;

        [Header("Sun Movement Settings")]
        [Tooltip("Настройки для теплого солнца")]
        [SerializeField] private SunMovementSettings warmSunSettings = new SunMovementSettings();

        [Tooltip("Настройки для холодного солнца")]
        [SerializeField] private SunMovementSettings coldSunSettings = new SunMovementSettings();

        [Header("Touch Panel Settings")]
        [Tooltip("Включить режим, при котором касание гасит уже горящий сегмент")]
        [SerializeField] public bool toggleTouchMode = false;

        [Tooltip("Задержка перед началом движения кометы (в секундах)")]
        [SerializeField] private float cometMoveDelay = 0.2f;

        public float currentSpeed = 1f;
        public float MultiplySpeed = 2f;
        private float sunMovementPhase = 0f;
        public Dictionary<int, List<Comet>> stripComets = new Dictionary<int, List<Comet>>();
        private Dictionary<int, float> lastTouchTimes = new Dictionary<int, float>(); // Время последнего касания для каждой ленты

        private int previousSynthLedCountBase;
        private float previousSpeedLedCountFactor;
        private float previousSpeedBrightnessFactor;
        private float previousTailIntensity;
        private MoveDirection previousMoveDirection;
        private SunMovementSettings previousWarmSunSettings;
        private SunMovementSettings previousColdSunSettings;
        [SerializeField] private StripDataManager stripDataManager;

        private Dictionary<int, Queue<Comet>> cometPools = new Dictionary<int, Queue<Comet>>();
        private int poolCapacity = 20; // Примерная максимальная потребность в кометах

        private Dictionary<int, byte[]> previousSpeedSynthData = new Dictionary<int, byte[]>();
        private Dictionary<int, byte[]> previousSunMovementData = new Dictionary<int, byte[]>();

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
            float timeSinceLastUpdate = Time.fixedDeltaTime;
            List<Comet> comets = stripComets[stripIndex];

            // Проверяем, прошло ли достаточно времени с последнего касания
            float lastTouchTime = lastTouchTimes.ContainsKey(stripIndex) ? lastTouchTimes[stripIndex] : 0f;
            bool canMove = Time.time - lastTouchTime >= cometMoveDelay;

            for (int i = comets.Count - 1; i >= 0; i--)
            {
                Comet comet = comets[i];
                if (!comet.isActive)
                {
                    stripComets[stripIndex].RemoveAt(i);
                    ReturnCometToPool(stripIndex, comet);
                    continue;
                }

                // Если прошло достаточно времени, комета начинает двигаться
                if (canMove && !comet.isMoving)
                {
                    comet.isMoving = true;
                }

                // Двигаем комету, если она движется
                if (comet.isMoving)
                {
                    // Обновляем направление на каждый кадр
                    if (startFromEnd)
                    {
                        comet.direction = currentSpeed >= 0 ? -1f : 1f;
                    }
                    else
                    {
                        comet.direction = currentSpeed >= 0 ? 1f : -1f;
                    }

                    float directionMultiplier = comet.direction;
                    comet.position += Mathf.Abs(currentSpeed) * timeSinceLastUpdate * 30f * directionMultiplier;

                    // Обработка выхода за пределы
                    if (comet.position < 0)
                        comet.position += totalLEDs;
                    else if (comet.position >= totalLEDs)
                        comet.position -= totalLEDs;
                }
            }
        }

        public void ResetComets(int stripIndex)
        {
            if (stripComets.ContainsKey(stripIndex))
            {
                foreach (var comet in stripComets[stripIndex])
                {
                    ReturnCometToPool(stripIndex, comet);
                }
                stripComets[stripIndex].Clear();
                previousSpeedSynthData.Remove(stripIndex);
            }
            lastTouchTimes.Remove(stripIndex); // Сбрасываем время последнего касания
        }

        public Comet GetCometFromPool(int stripIndex, float position, Color32 color, float length, float brightness, float direction)
        {
            if (!cometPools.ContainsKey(stripIndex))
            {
                cometPools[stripIndex] = new Queue<Comet>(poolCapacity);
            }

            if (cometPools[stripIndex].Count > 0)
            {
                var comet = cometPools[stripIndex].Dequeue();
                comet.position = position;
                comet.color = color;
                comet.length = length;
                comet.brightness = brightness;
                comet.direction = direction;
                comet.isActive = true;
                comet.startTime = Time.time;
                comet.isMoving = false;
                return comet;
            }
            else
            {
                return new Comet(position, color, length, brightness, direction);
            }
        }

        public void ReturnCometToPool(int stripIndex, Comet comet)
        {
            if (!cometPools.ContainsKey(stripIndex))
            {
                cometPools[stripIndex] = new Queue<Comet>(poolCapacity);
            }
            comet.isActive = false;
            if (cometPools[stripIndex].Count < poolCapacity)
            {
                cometPools[stripIndex].Enqueue(comet);
            }
        }

        public void AddComet(int stripIndex, float position, Color32 color, float length, float brightness)
        {
            if (!stripComets.ContainsKey(stripIndex))
            {
                stripComets[stripIndex] = new List<Comet>();
            }

            // Если включен режим startFromEnd, начинаем с конца ленты
            if (startFromEnd)
            {
                position = stripDataManager.totalLEDsPerStrip[stripIndex] - 1;
                moveDirection = MoveDirection.Backward;
            }

            // Определяем направление на основе текущей скорости
            float direction = currentSpeed >= 0 ? 1f : -1f;

            stripComets[stripIndex].Add(GetCometFromPool(stripIndex, position, color, length, brightness, direction));
            lastTouchTimes[stripIndex] = Time.time; // Обновляем время последнего касания
        }

        public void UpdateLastTouchTime(int stripIndex)
        {
            lastTouchTimes[stripIndex] = Time.time; // Обновляем время последнего касания без создания кометы
        }

        public void UpdateSunMovementPhase()
        {
            SunMovementSettings settings = warmSunSettings; // По умолчанию, можно использовать coldSunSettings
            float cycleTime = settings.cycleLength / Mathf.Max(0.1f, currentSpeed);
            sunMovementPhase += Time.fixedDeltaTime / cycleTime;
            if (sunMovementPhase >= 1f)
                sunMovementPhase = 0f;
        }

        public bool CheckForChanges()
        {
            bool changed =
                synthLedCountBase != previousSynthLedCountBase ||
                speedLedCountFactor != previousSpeedLedCountFactor ||
                speedBrightnessFactor != previousSpeedBrightnessFactor ||
                tailIntensity != previousTailIntensity ||
                moveDirection != previousMoveDirection ||
                !AreSunSettingsEqual(warmSunSettings, previousWarmSunSettings) ||
                !AreSunSettingsEqual(coldSunSettings, previousColdSunSettings);

            if (changed)
            {
                CachePreviousValues();
            }

            return changed;
        }

        private bool AreSunSettingsEqual(SunMovementSettings a, SunMovementSettings b)
        {
            if (a == null || b == null) return a == b;
            return a.pixelCount == b.pixelCount &&
                   a.cycleLength == b.cycleLength &&
                   a.startTime == b.startTime &&
                   a.endTime == b.endTime &&
                   a.brightnessMultiplier == b.brightnessMultiplier;
        }

        private void CachePreviousValues()
        {
            previousSynthLedCountBase = synthLedCountBase;
            previousSpeedLedCountFactor = speedLedCountFactor;
            previousSpeedBrightnessFactor = speedBrightnessFactor;
            previousTailIntensity = tailIntensity;
            previousMoveDirection = moveDirection;
            previousWarmSunSettings = new SunMovementSettings
            {
                pixelCount = warmSunSettings.pixelCount,
                cycleLength = warmSunSettings.cycleLength,
                startTime = warmSunSettings.startTime,
                endTime = warmSunSettings.endTime,
                brightnessMultiplier = warmSunSettings.brightnessMultiplier
            };
            previousColdSunSettings = new SunMovementSettings
            {
                pixelCount = coldSunSettings.pixelCount,
                cycleLength = coldSunSettings.cycleLength,
                startTime = coldSunSettings.startTime,
                endTime = coldSunSettings.endTime,
                brightnessMultiplier = coldSunSettings.brightnessMultiplier
            };
        }

        public byte[] GetHexDataForSpeedSynthMode(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            if (!stripComets.ContainsKey(stripIndex) || stripComets[stripIndex].Count == 0)
            {
                if (previousSpeedSynthData.ContainsKey(stripIndex))
                {
                    previousSpeedSynthData.Remove(stripIndex);
                }
                return null;
            }

            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int hexPerPixel = (mode == DataMode.RGBW ? 4 : mode == DataMode.RGB ? 3 : 1);
            byte[] hexData = new byte[totalLEDs * hexPerPixel];
            Color32 blackColor = new Color32(0, 0, 0, 255);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            Span<Color32> pixelColors = new Span<Color32>(new Color32[totalLEDs]);
            pixelColors.Fill(blackColor);

            foreach (Comet comet in stripComets[stripIndex])
            {
                if (!comet.isActive) continue;

                int dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(comet.length));
                float dynamicBrightness = Mathf.Clamp01(comet.brightness * stripBrightness);

                // Учитываем скорость для интенсивности свечения
                float speedBrightnessMultiplier = 1f + Mathf.Abs(currentSpeed) * speedBrightnessFactor;
                dynamicBrightness *= speedBrightnessMultiplier;

                // Если комета неподвижна, уменьшаем яркость
                if (!comet.isMoving)
                {
                    dynamicBrightness *= stationaryBrightnessFactor;
                }

                int ledIndex = Mathf.FloorToInt(comet.position);
                for (int j = 0; j < dynamicLedCount; j++)
                {
                    // Голова кометы (самая яркая часть) должна быть в направлении движения
                    int offset = comet.direction > 0 ? j : -j;
                    int currentLedIndex = ledIndex + offset;
                    currentLedIndex = Mathf.RoundToInt(Mathf.Repeat(currentLedIndex, totalLEDs));

                    if (currentLedIndex >= 0 && currentLedIndex < totalLEDs)
                    {
                        float tailFalloff = 1f - ((float)j / (dynamicLedCount - 1));
                        tailFalloff = Mathf.Clamp01(tailFalloff); // безопасная защита
                        float brightnessFactor = tailFalloff * tailIntensity;

                        // Яркость уменьшается от головы к хвосту
                        byte r = (byte)Mathf.Clamp(comet.color.r * brightnessFactor * dynamicBrightness, 0, 255);
                        byte g = (byte)Mathf.Clamp(comet.color.g * brightnessFactor * dynamicBrightness, 0, 255);
                        byte b = (byte)Mathf.Clamp(comet.color.b * brightnessFactor * dynamicBrightness, 0, 255);

                        pixelColors[currentLedIndex] = new Color32(r, g, b, 255);
                    }
                }
            }

            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = pixelColors[i];
                Span<byte> pixelHex = stackalloc byte[hexPerPixel];
                if (mode == DataMode.RGBW)
                {
                    colorProcessor.ColorToHexRGBW(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                else if (mode == DataMode.RGB)
                {
                    colorProcessor.ColorToHexRGB(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                else
                {
                    colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                pixelHex.CopyTo(new Span<byte>(hexData, i * hexPerPixel, hexPerPixel));
            }

            byte[] optimizedHex = OptimizeHexData(hexData, new byte[hexPerPixel]);
            if (previousSpeedSynthData.TryGetValue(stripIndex, out byte[] prevHex) && optimizedHex.AsSpan().SequenceEqual(prevHex))
            {
                return null;
            }
            previousSpeedSynthData[stripIndex] = optimizedHex;
            return optimizedHex;
        }

        public byte[] GetHexDataForSunMovement(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int hexPerPixel = (mode == DataMode.RGBW ? 4 : mode == DataMode.RGB ? 3 : 1);
            byte[] hexData = new byte[totalLEDs * hexPerPixel];
            Color32 sunColor = stripManager.GetSunMode(stripIndex) == SunMode.Warm ? new Color32(255, 147, 41, 255) : new Color32(173, 216, 230, 255);
            Color32 blackColor = new Color32(0, 0, 0, 255);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            SunMovementSettings settings = stripManager.GetSunMode(stripIndex) == SunMode.Warm ? warmSunSettings : coldSunSettings;
            float currentCycleTime = sunMovementPhase * settings.cycleLength;
            bool isActive = currentCycleTime >= settings.startTime && currentCycleTime <= settings.endTime;

            float fadeInFactor = 1.0f;
            float fadeOutFactor = 1.0f;
            float fadeTime = 5.0f;

            if (isActive)
            {
                if (currentCycleTime < settings.startTime + fadeTime)
                {
                    fadeInFactor = (currentCycleTime - settings.startTime) / fadeTime;
                }
                if (currentCycleTime > settings.endTime - fadeTime)
                {
                    fadeOutFactor = (settings.endTime - currentCycleTime) / fadeTime;
                }
            }

            for (int i = 0; i < totalLEDs; ++i)
            {
                Color32 pixelColor = blackColor;

                if (isActive)
                {
                    float activePhase = (currentCycleTime - settings.startTime) / (settings.endTime - settings.startTime);
                    float sunPosition = activePhase * totalLEDs;
                    float distance = Mathf.Abs(i - sunPosition);
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
                Span<byte> pixelHex = stackalloc byte[hexPerPixel];
                if (mode == DataMode.RGBW)
                {
                    colorProcessor.ColorToHexRGBW(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                else if (mode == DataMode.RGB)
                {
                    colorProcessor.ColorToHexRGB(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                else
                {
                    colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                pixelHex.CopyTo(new Span<byte>(hexData, i * hexPerPixel, hexPerPixel));
            }

            byte[] optimizedHex = OptimizeHexData(hexData, new byte[hexPerPixel]);
            if (previousSunMovementData.TryGetValue(stripIndex, out byte[] prevHex) && optimizedHex.AsSpan().SequenceEqual(prevHex))
            {
                return null;
            }
            previousSunMovementData[stripIndex] = optimizedHex;
            return optimizedHex;
        }

        public byte[] GetHexDataForSegmentMode(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int ledsPerSegment = stripManager.ledsPerSegment;
            int hexPerPixel = (mode == DataMode.RGBW ? 4 : mode == DataMode.RGB ? 3 : 1);
            byte[] hexData = new byte[totalLEDs * hexPerPixel];

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            for (int i = 0; i < totalLEDs; ++i)
            {
                int segmentIndex = i / ledsPerSegment;
                Color32 segmentColor = stripManager.GetSegmentColor(stripIndex, segmentIndex);
                Color32 pixelColor = new Color32(
                    (byte)(segmentColor.r * stripBrightness),
                    (byte)(segmentColor.g * stripBrightness),
                    (byte)(segmentColor.b * stripBrightness),
                    255
                );
                Span<byte> pixelHex = stackalloc byte[hexPerPixel];
                if (mode == DataMode.RGBW)
                {
                    colorProcessor.ColorToHexRGBW(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                else if (mode == DataMode.RGB)
                {
                    colorProcessor.ColorToHexRGB(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                else
                {
                    colorProcessor.ColorToHexMonochrome(pixelColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHex);
                }
                pixelHex.CopyTo(new Span<byte>(hexData, i * hexPerPixel, hexPerPixel));
            }

            return OptimizeHexData(hexData, new byte[hexPerPixel]);
        }

        private byte[] OptimizeHexData(byte[] hexData, byte[] blackHex)
        {
            int hexPerPixel = blackHex.Length;
            int totalPixels = hexData.Length / hexPerPixel;
            int lastSentPixel = totalPixels;
            while (lastSentPixel > 0)
            {
                int startIndex = (lastSentPixel - 1) * hexPerPixel;
                if (new Span<byte>(hexData, startIndex, hexPerPixel).SequenceEqual(blackHex))
                {
                    lastSentPixel--;
                }
                else
                {
                    break;
                }
            }
            byte[] optimizedData = new byte[lastSentPixel * hexPerPixel];
            Buffer.BlockCopy(hexData, 0, optimizedData, 0, optimizedData.Length);
            return optimizedData;
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
    }
}