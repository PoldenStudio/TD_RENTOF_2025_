using LEDControl;
using UnityEngine;

namespace LEDControl
{
    public static class DmxFrameCalculator
    {
        public static PreCalculatedDmxFrame CalculateDmxFrame(LEDDataFrame frameData, int offset, int totalLEDs, ColorFormat stripFormat)
        {
            PreCalculatedDmxFrame preCalculatedFrame = new();
            preCalculatedFrame.channelValues = new byte[513];

            if (frameData.pixels == null)
            {
                Debug.LogError("frameData.pixels is null");
                return preCalculatedFrame;
            }

            byte[][] pixels = frameData.pixels;
            int pixelIndex = 0;
            int channelsPerLed = GetChannelsForFormat(stripFormat);

            for (int i = 0; i < totalLEDs; i++)
            {
                int baseChannel = i * channelsPerLed + offset;

                if (baseChannel + channelsPerLed - 1 > 512)
                {
                    Debug.LogWarning($"DMX channel overflow detected for LED strip! Offset: {offset}, Total LEDs: {totalLEDs}, Channels per LED: {channelsPerLed}");
                    break; // Прерываем цикл, если данные выходят за пределы DMX
                }

                if (pixelIndex < pixels.Length)
                {
                    ProcessPixelData(frameData.format, stripFormat, pixels[pixelIndex], preCalculatedFrame.channelValues, baseChannel);
                    pixelIndex++;
                }
            }
            return preCalculatedFrame;
        }

        private static int GetChannelsForFormat(ColorFormat format)
        {
            return format switch
            {
                ColorFormat.RGB => 3,
                ColorFormat.RGBW => 4,
                ColorFormat.HSV => 3,
                ColorFormat.RGBWMix => 5,
                _ => 3,
            };
        }

        private static void ProcessPixelData(ColorFormat sourceFormat, ColorFormat targetFormat, byte[] pixelData, byte[] outputBuffer, int baseChannel)
        {
            if (pixelData == null || pixelData.Length == 0)
                return;

            // Сначала преобразуем входные данные в значения RGB
            byte r = 0, g = 0, b = 0;

            switch (sourceFormat)
            {
                case ColorFormat.RGB:
                    if (pixelData.Length >= 3)
                    {
                        r = pixelData[0];
                        g = pixelData[1];
                        b = pixelData[2];
                    }
                    break;

                case ColorFormat.RGBW:
                    if (pixelData.Length >= 3)
                    {
                        r = pixelData[0];
                        g = pixelData[1];
                        b = pixelData[2];
                        byte w = pixelData.Length >= 4 ? pixelData[3] : (byte)0;
                        // Добавляем белый компонент к RGB
                        if (w > 0)
                        {
                            r = (byte)Mathf.Clamp(r + w / 3, 0, 255);
                            g = (byte)Mathf.Clamp(g + w / 3, 0, 255);
                            b = (byte)Mathf.Clamp(b + w / 3, 0, 255);
                        }
                    }
                    break;

                case ColorFormat.RGBWMix:
                    if (pixelData.Length >= 3)
                    {
                        r = pixelData[0];
                        g = pixelData[1];
                        b = pixelData[2];
                        byte warmWhite = pixelData.Length >= 4 ? pixelData[3] : (byte)0;
                        byte coolWhite = pixelData.Length >= 5 ? pixelData[4] : (byte)0;
                        // Добавляем теплый и холодный белый к RGB
                        r = (byte)Mathf.Clamp(r + warmWhite / 2, 0, 255);
                        g = (byte)Mathf.Clamp(g + (warmWhite / 2 + coolWhite / 2), 0, 255);
                        b = (byte)Mathf.Clamp(b + coolWhite / 2, 0, 255);
                    }
                    break;

                case ColorFormat.HSV:
                    if (pixelData.Length >= 3)
                    {
                        float h = pixelData[0] / 255f * 360f;
                        float s = pixelData[1] / 255f;
                        float v = pixelData[2] / 255f;
                        Color rgb = Color.HSVToRGB(h / 360f, s, v);
                        r = (byte)(rgb.r * 255);
                        g = (byte)(rgb.g * 255);
                        b = (byte)(rgb.b * 255);
                    }
                    break;
            }

            // Вывод в зависимости от целевого формата
            switch (targetFormat)
            {
                case ColorFormat.RGB:
                    outputBuffer[baseChannel] = r;
                    outputBuffer[baseChannel + 1] = g;
                    outputBuffer[baseChannel + 2] = b;
                    break;

                case ColorFormat.RGBW:
                    outputBuffer[baseChannel] = r;
                    outputBuffer[baseChannel + 1] = g;
                    outputBuffer[baseChannel + 2] = b;
                    // Вычисляем белый компонент как минимум из RGB
                    byte w = (byte)Mathf.Min(r, g, b);
                    outputBuffer[baseChannel + 3] = w;
                    break;

                case ColorFormat.RGBWMix:
                    outputBuffer[baseChannel] = r;
                    outputBuffer[baseChannel + 1] = g;
                    outputBuffer[baseChannel + 2] = b;
                    // Вычисляем теплый и холодный белый
                    byte warmWhite = (byte)Mathf.Min(r, g);
                    byte coolWhite = (byte)Mathf.Min(b, g);
                    outputBuffer[baseChannel + 3] = warmWhite;
                    outputBuffer[baseChannel + 4] = coolWhite;
                    break;

                case ColorFormat.HSV:
                    // Для вывода в формате HSV преобразуем RGB к HSV,
                    // но поскольку DMX ожидает RGB-значения, используем RGB + белый
                    outputBuffer[baseChannel] = r;
                    outputBuffer[baseChannel + 1] = g;
                    outputBuffer[baseChannel + 2] = b;
                    byte white = (byte)Mathf.Min(r, g, b);
                    outputBuffer[baseChannel + 3] = white;
                    break;
            }
        }
    }
}