using UnityEngine;
using System;

namespace LEDControl
{
    public class ColorProcessor : MonoBehaviour
    {
        [Header("Gamma Correction")]
        [Tooltip("Enable Gamma Correction for non-linear brightness adjustment.")]
        public bool enableGammaCorrection = true;

        [Tooltip("Gamma value. Typical value is 2.2 or 2.5.")]
        [Range(0.1f, 5.0f)]
        [SerializeField] private float gammaValue = 2.2f;

        [Range(0f, 1f)]
        [SerializeField] private float globalBrightness = 1.0f;

        private byte[] gammaTable = new byte[256];

        private void Awake()
        {
            InitializeGammaTable();
        }

        public void InitializeGammaTable()
        {
            for (int i = 0; i < 256; ++i)
            {
                float normalizedValue = (float)i / 255f;
                float gammaCorrectedValue = Mathf.Pow(normalizedValue, gammaValue);
                gammaTable[i] = (byte)Mathf.RoundToInt(gammaCorrectedValue * 255f);
            }
        }

        public byte ApplyGammaCorrection(byte colorByte)
        {
            if (enableGammaCorrection)
            {
                return gammaTable[colorByte];
            }
            return colorByte;
        }

        public string ColorToHexMonochrome(Color32 color)
        {
            float lum = (color.r * 0.299f + color.g * 0.587f + color.b * 0.114f) * globalBrightness;
            byte monoColorByte = ApplyGammaCorrection((byte)Mathf.Clamp(lum, 0f, 255f));
            return monoColorByte.ToString("X2");
        }

        public string ColorToHexRGB(Color32 color)
        {
            byte r = ApplyGammaCorrection((byte)Mathf.Clamp(color.r * globalBrightness, 0f, 255f));
            byte g = ApplyGammaCorrection((byte)Mathf.Clamp(color.g * globalBrightness, 0f, 255f));
            byte b = ApplyGammaCorrection((byte)Mathf.Clamp(color.b * globalBrightness, 0f, 255f));
            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        public string ColorToHexRGBW(Color32 color)
        {
            byte r = color.r;
            byte g = color.g;
            byte b = color.b;
            byte w = (byte)Mathf.Min(Mathf.Min(r, g), b);

            r = (byte)Mathf.Max(0, r - w);
            g = (byte)Mathf.Max(0, g - w);
            b = (byte)Mathf.Max(0, b - w);

            r = ApplyGammaCorrection((byte)Mathf.Clamp(r * globalBrightness, 0f, 255f));
            g = ApplyGammaCorrection((byte)Mathf.Clamp(g * globalBrightness, 0f, 255f));
            b = ApplyGammaCorrection((byte)Mathf.Clamp(b * globalBrightness, 0f, 255f));
            w = ApplyGammaCorrection((byte)Mathf.Clamp(w * globalBrightness, 0f, 255f));

            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2") + w.ToString("X2");
        }

        public void SetGlobalBrightness(float brightness)
        {
            globalBrightness = brightness;
        }

        public float GetGlobalBrightness()
        {
            return globalBrightness;
        }

        public void SetGammaCorrection(bool enabled, float gamma)
        {
            enableGammaCorrection = enabled;

            if (gammaValue != gamma)
            {
                gammaValue = gamma;
                InitializeGammaTable();
            }
        }
    }
}