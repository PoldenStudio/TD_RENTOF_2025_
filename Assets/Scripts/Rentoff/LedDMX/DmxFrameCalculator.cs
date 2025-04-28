using LEDControl;
using UnityEngine;
using System;

namespace LEDControl
{
    public static class DmxFrameCalculator
    {
        public static PreCalculatedDmxFrame CalculateDmxFrame(LEDDataFrame frameData, int offset, int totalLEDs, ColorFormat stripFormat)
        {
            PreCalculatedDmxFrame preCalculatedFrame = new();
            preCalculatedFrame.channelValues = new byte[513]; // DMX channels 1-512

            if (frameData.pixels == null)
            {
                //Debug.LogError("frameData.pixels is null");
                return preCalculatedFrame;
            }

            byte[][] pixels = frameData.pixels;
            int pixelIndex = 0;
            int channelsPerLed = GetChannelsForFormat(stripFormat);

            for (int i = 0; i < totalLEDs; i++)
            {
                int baseDmxChannel = i * channelsPerLed + offset;

                if (baseDmxChannel + channelsPerLed - 1 > 512)
                {
                    // Debug.LogWarning($"DMX channel overflow detected for LED strip! Offset: {offset}, Total LEDs: {totalLEDs}, Channels per LED: {channelsPerLed}, LED index: {i}");
                    break;
                }

                if (pixelIndex < pixels.Length)
                {
                    ProcessPixelData(frameData.format, stripFormat, pixels[pixelIndex], preCalculatedFrame.channelValues, baseDmxChannel);
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

        private static void ProcessPixelData(ColorFormat sourceFormat, ColorFormat targetFormat, byte[] pixelData, byte[] outputBuffer, int baseDmxChannel)
        {
            if (pixelData == null || pixelData.Length == 0)
                return;

            byte r = 0, g = 0, b = 0;

            switch (sourceFormat)
            {
                case ColorFormat.RGB:
                    if (pixelData.Length >= 3) { r = pixelData[0]; g = pixelData[1]; b = pixelData[2]; }
                    break;
                case ColorFormat.RGBW:
                    if (pixelData.Length >= 3)
                    {
                        r = pixelData[0]; g = pixelData[1]; b = pixelData[2];
                        byte w = pixelData.Length >= 4 ? pixelData[3] : (byte)0;
                        r = (byte)Mathf.Clamp(r + w / 3, 0, 255);
                        g = (byte)Mathf.Clamp(g + w / 3, 0, 255);
                        b = (byte)Mathf.Clamp(b + w / 3, 0, 255);
                    }
                    break;
                case ColorFormat.RGBWMix:
                    if (pixelData.Length >= 3)
                    {
                        r = pixelData[0]; g = pixelData[1]; b = pixelData[2];
                        byte warmWhite = pixelData.Length >= 4 ? pixelData[3] : (byte)0;
                        byte coolWhite = pixelData.Length >= 5 ? pixelData[4] : (byte)0;
                        r = (byte)Mathf.Clamp(r + warmWhite / 2, 0, 255);
                        g = (byte)Mathf.Clamp(g + (warmWhite / 2 + coolWhite / 2), 0, 255);
                        b = (byte)Mathf.Clamp(b + coolWhite / 2, 0, 255);
                    }
                    break;
                case ColorFormat.HSV:
                    if (pixelData.Length >= 3)
                    {
                        float h = pixelData[0] / 255f;
                        float s = pixelData[1] / 255f;
                        float v = pixelData[2] / 255f;
                        Color rgb = Color.HSVToRGB(h, s, v);
                        r = (byte)(rgb.r * 255); g = (byte)(rgb.g * 255); b = (byte)(rgb.b * 255);
                    }
                    break;
            }

            Action<int, byte> writeToBuffer = (channel, value) => {
                if (channel >= 1 && channel <= 512) outputBuffer[channel] = value;
            };


            switch (targetFormat)
            {
                case ColorFormat.RGB:
                    writeToBuffer(baseDmxChannel, r);
                    writeToBuffer(baseDmxChannel + 1, g);
                    writeToBuffer(baseDmxChannel + 2, b);
                    break;
                case ColorFormat.RGBW:
                    writeToBuffer(baseDmxChannel, r);
                    writeToBuffer(baseDmxChannel + 1, g);
                    writeToBuffer(baseDmxChannel + 2, b);
                    writeToBuffer(baseDmxChannel + 3, (byte)Mathf.Min(r, g, b));
                    break;
                case ColorFormat.RGBWMix:
                    writeToBuffer(baseDmxChannel, r);
                    writeToBuffer(baseDmxChannel + 1, g);
                    writeToBuffer(baseDmxChannel + 2, b);
                    writeToBuffer(baseDmxChannel + 3, (byte)Mathf.Min(r, g));
                    writeToBuffer(baseDmxChannel + 4, (byte)Mathf.Min(b, g));
                    break;
                case ColorFormat.HSV:
                    writeToBuffer(baseDmxChannel, r);
                    writeToBuffer(baseDmxChannel + 1, g);
                    writeToBuffer(baseDmxChannel + 2, b);
                    if (GetChannelsForFormat(targetFormat) >= 4)
                        writeToBuffer(baseDmxChannel + 3, (byte)Mathf.Min(r, g, b));
                    break;
            }
        }
    }
}