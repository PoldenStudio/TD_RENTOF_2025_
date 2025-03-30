using UnityEngine;
using System;

namespace LEDControl
{
    public class ColorProcessor : MonoBehaviour
    {
        private byte[] gammaTable = new byte[256];

        private void Awake()
        {
            // Инициализация гамма-таблицы
            InitializeGammaTable(2.2f); // Стандартное значение гаммы
        }

        private void InitializeGammaTable(float gammaValue)
        {
            for (int i = 0; i < 256; ++i)
            {
                float normalizedValue = (float)i / 255f;
                float gammaCorrectedValue = Mathf.Pow(normalizedValue, gammaValue);
                gammaTable[i] = (byte)Mathf.RoundToInt(gammaCorrectedValue * 255f);
            }
        }

        public byte ApplyGammaCorrection(byte colorByte, float gammaValue, bool enableGammaCorrection)
        {
            if (enableGammaCorrection)
            {
                if (Math.Abs(gammaValue - 2.2f) > 0.01f)
                {
                    InitializeGammaTable(gammaValue);
                }
                return gammaTable[colorByte];
            }
            return colorByte;
        }

        public byte[] ColorToHexMonochrome(Color32 color, float brightness, float gammaValue, bool enableGammaCorrection)
        {
            float lum = (color.r * 0.299f + color.g * 0.587f + color.b * 0.114f) * brightness;
            byte monoColorByte = ApplyGammaCorrection((byte)Mathf.Clamp(lum, 0f, 255f), gammaValue, enableGammaCorrection);
            return new byte[] { monoColorByte };
        }

        public byte[] ColorToHexRGB(Color32 color, float brightness, float gammaValue, bool enableGammaCorrection)
        {
            byte r = ApplyGammaCorrection((byte)Mathf.Clamp(color.r * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            byte g = ApplyGammaCorrection((byte)Mathf.Clamp(color.g * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            byte b = ApplyGammaCorrection((byte)Mathf.Clamp(color.b * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            return new byte[] { r, g, b };
        }

        public byte[] ColorToHexRGBW(Color32 color, float brightness, float gammaValue, bool enableGammaCorrection)
        {
            byte r = color.r;
            byte g = color.g;
            byte b = color.b;
            byte w = (byte)Mathf.Min(Mathf.Min(r, g), b);

            r = (byte)Mathf.Max(0, r - w);
            g = (byte)Mathf.Max(0, g - w);
            b = (byte)Mathf.Max(0, b - w);

            r = ApplyGammaCorrection((byte)Mathf.Clamp(r * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            g = ApplyGammaCorrection((byte)Mathf.Clamp(g * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            b = ApplyGammaCorrection((byte)Mathf.Clamp(b * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            w = ApplyGammaCorrection((byte)Mathf.Clamp(w * brightness, 0f, 255f), gammaValue, enableGammaCorrection);

            return new byte[] { r, g, b, w };
        }
    }
}