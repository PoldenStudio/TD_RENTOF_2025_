using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;

namespace LEDControl
{
    public class JsonFrameLoader
    {
        private class LEDDataFrameRaw
        {
            public int frame;
            public int[][] pixels;
        }

        private class LEDDataWithFormat
        {
            public string color_format;
            public LEDDataFrameRaw[] frames;
        }

        public static LEDDataFrame[] LoadJsonFrames(string fullJsonPath, ColorFormat preferredFormat)
        {
            if (string.IsNullOrEmpty(fullJsonPath) || !File.Exists(fullJsonPath))
            {
                Debug.LogWarning($"JSON path invalid: {fullJsonPath}");
                return null;
            }
            try
            {
                string jsonString = File.ReadAllText(fullJsonPath);
                ColorFormat detectedFormat = preferredFormat;
                LEDDataFrameRaw[] rawData = null;

                try
                {
                    LEDDataWithFormat formattedData = JsonConvert.DeserializeObject<LEDDataWithFormat>(jsonString);
                    if (formattedData != null && formattedData.frames != null)
                    {
                        rawData = formattedData.frames;
                        if (!string.IsNullOrEmpty(formattedData.color_format))
                        {
                            string cf = formattedData.color_format.ToLower();
                            if (cf == "rgb")
                                detectedFormat = ColorFormat.RGB;
                            else if (cf == "rgbw")
                                detectedFormat = ColorFormat.RGBW;
                            else if (cf == "hsv")
                                detectedFormat = ColorFormat.HSV;
                            else if (cf == "rgbwmix")
                                detectedFormat = ColorFormat.RGBWMix;
                        }
                    }
                }
                catch
                {
                    rawData = JsonConvert.DeserializeObject<LEDDataFrameRaw[]>(jsonString);
                }

                if (rawData == null)
                {
                    Debug.LogError($"Failed to parse JSON: {fullJsonPath}");
                    return null;
                }

                LEDDataFrame[] frames = new LEDDataFrame[rawData.Length];
                for (int i = 0; i < rawData.Length; i++)
                {
                    frames[i].frame = rawData[i].frame;
                    frames[i].format = detectedFormat;
                    frames[i].pixels = new byte[rawData[i].pixels.Length][];

                    for (int j = 0; j < rawData[i].pixels.Length; j++)
                    {
                        // Определяем размер массива в зависимости от формата
                        int pixelSize;
                        switch (detectedFormat)
                        {
                            case ColorFormat.RGB:
                                pixelSize = 3;
                                break;
                            case ColorFormat.RGBW:
                                pixelSize = 4;
                                break;
                            case ColorFormat.RGBWMix:
                                pixelSize = 5;
                                break;
                            case ColorFormat.HSV:
                                pixelSize = 3;
                                break;
                            default:
                                pixelSize = 3;
                                break;
                        }

                        frames[i].pixels[j] = new byte[pixelSize];
                        int len = Math.Min(pixelSize, rawData[i].pixels[j].Length);
                        for (int k = 0; k < len; k++)
                        {
                            frames[i].pixels[j][k] = (byte)Mathf.Clamp(rawData[i].pixels[j][k], 0, 255);
                        }

                        // Дополнение недостающих данных для различных форматов
                        if (detectedFormat == ColorFormat.RGBWMix && rawData[i].pixels[j].Length < 5 && len >= 3)
                        {
                            byte r = frames[i].pixels[j][0],
                                 g = frames[i].pixels[j][1],
                                 b = frames[i].pixels[j][2];
                            byte warmWhite = (byte)Mathf.Min(r, g);
                            byte coolWhite = (byte)Mathf.Min(b, g);
                            if (pixelSize > 3) frames[i].pixels[j][3] = warmWhite;
                            if (pixelSize > 4) frames[i].pixels[j][4] = coolWhite;
                        }
                        else if (detectedFormat == ColorFormat.RGBW && rawData[i].pixels[j].Length < 4 && len >= 3)
                        {
                            byte r = frames[i].pixels[j][0],
                                 g = frames[i].pixels[j][1],
                                 b = frames[i].pixels[j][2];
                            byte white = (byte)Mathf.Min(Mathf.Min(r, g), b);
                            frames[i].pixels[j][3] = white;
                        }
                    }
                }

                Debug.Log($"Loaded {frames.Length} frames from {fullJsonPath} with format {detectedFormat}");
                return frames;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading JSON from {fullJsonPath}: {e.Message}");
                return null;
            }
        }
    }
}