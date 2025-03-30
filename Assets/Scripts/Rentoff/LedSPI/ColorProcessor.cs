using UnityEngine;
using System;
using System.Runtime.CompilerServices;

namespace LEDControl
{
    public class ColorProcessor : MonoBehaviour
    {
        private byte[] gammaTable = new byte[256];
        private float currentGammaValue = -1f;

        private void Awake()
        {
            // Инициализация гамма-таблицы
            InitializeGammaTable(2.2f); // Стандартное значение гаммы
        }

        private void InitializeGammaTable(float gammaValue)
        {
            if (Mathf.Abs(currentGammaValue - gammaValue) > 0.01f)
            {
                currentGammaValue = gammaValue;
                for (int i = 0; i < 256; ++i)
                {
                    float normalizedValue = (float)i / 255f;
                    float gammaCorrectedValue = Mathf.Pow(normalizedValue, gammaValue);
                    gammaTable[i] = (byte)Mathf.RoundToInt(gammaCorrectedValue * 255f);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ApplyGammaCorrection(byte colorByte, float gammaValue, bool enableGammaCorrection)
        {
            if (enableGammaCorrection)
            {
                InitializeGammaTable(gammaValue);
                return gammaTable[colorByte];
            }
            return colorByte;
        }

        public void ColorToHexMonochrome(Color32 color, float brightness, float gammaValue, bool enableGammaCorrection, Span<byte> output)
        {
            float lum = (color.r * 0.299f + color.g * 0.587f + color.b * 0.114f) * brightness;
            byte monoColorByte = ApplyGammaCorrection((byte)Mathf.Clamp(lum, 0f, 255f), gammaValue, enableGammaCorrection);
            output[0] = monoColorByte;
        }

        public void ColorToHexRGB(Color32 color, float brightness, float gammaValue, bool enableGammaCorrection, Span<byte> output)
        {
            output[0] = ApplyGammaCorrection((byte)Mathf.Clamp(color.r * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            output[1] = ApplyGammaCorrection((byte)Mathf.Clamp(color.g * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            output[2] = ApplyGammaCorrection((byte)Mathf.Clamp(color.b * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
        }

        public void ColorToHexRGBW(Color32 color, float brightness, float gammaValue, bool enableGammaCorrection, Span<byte> output)
        {
            byte r = color.r;
            byte g = color.g;
            byte b = color.b;
            byte w = (byte)Mathf.Min(Mathf.Min(r, g), b);

            r = (byte)Mathf.Max(0, r - w);
            g = (byte)Mathf.Max(0, g - w);
            b = (byte)Mathf.Max(0, b - w);

            output[0] = ApplyGammaCorrection((byte)Mathf.Clamp(r * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            output[1] = ApplyGammaCorrection((byte)Mathf.Clamp(g * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            output[2] = ApplyGammaCorrection((byte)Mathf.Clamp(b * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            output[3] = ApplyGammaCorrection((byte)Mathf.Clamp(w * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
        }
    }
}