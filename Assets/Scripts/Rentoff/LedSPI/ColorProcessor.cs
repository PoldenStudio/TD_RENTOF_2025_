using UnityEngine;
using System;

namespace LEDControl
{
    public class ColorProcessor : MonoBehaviour
    {
        private byte[] gammaTable = new byte[256];

        private void Awake()
        {
            // Инициализация гамма-таблицы не нужна здесь, так как она будет создаваться для каждой ленты
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
                InitializeGammaTable(gammaValue);
                return gammaTable[colorByte];
            }
            return colorByte;
        }

        public string ColorToHexMonochrome(Color32 color, float brightness, float gammaValue, bool enableGammaCorrection)
        {
            float lum = (color.r * 0.299f + color.g * 0.587f + color.b * 0.114f) * brightness;
            byte monoColorByte = ApplyGammaCorrection((byte)Mathf.Clamp(lum, 0f, 255f), gammaValue, enableGammaCorrection);
            return monoColorByte.ToString("X2");
        }

        public string ColorToHexRGB(Color32 monoColor, Color32 color, Color32 color2, float brightness, float gammaValue, bool enableGammaCorrection)
        {
            byte r = ApplyGammaCorrection((byte)Mathf.Clamp(color.r * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            byte g = ApplyGammaCorrection((byte)Mathf.Clamp(color2.g * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            byte b = ApplyGammaCorrection((byte)Mathf.Clamp(monoColor.b * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        public string ColorToHexRGBW(Color32 monoColor, Color32 color, Color32 color2, float brightness, float gammaValue, bool enableGammaCorrection)
        {
            byte r = color.r;
            byte g = color2.g;
            byte b = monoColor.b;
            byte w = (byte)Mathf.Min(Mathf.Min(r, g), b);

            r = (byte)Mathf.Max(0, r - w);
            g = (byte)Mathf.Max(0, g - w);
            b = (byte)Mathf.Max(0, b - w);

            r = ApplyGammaCorrection((byte)Mathf.Clamp(r * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            g = ApplyGammaCorrection((byte)Mathf.Clamp(g * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            b = ApplyGammaCorrection((byte)Mathf.Clamp(b * brightness, 0f, 255f), gammaValue, enableGammaCorrection);
            w = ApplyGammaCorrection((byte)Mathf.Clamp(w * brightness, 0f, 255f), gammaValue, enableGammaCorrection);

            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2") + w.ToString("X2");
        }
    }
}