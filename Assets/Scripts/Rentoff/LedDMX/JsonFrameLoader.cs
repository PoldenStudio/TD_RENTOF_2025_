using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Linq;

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
                // Debug.LogWarning($"JSON path invalid or file not found: {fullJsonPath}");
                return null;
            }
            try
            {
                string jsonString = File.ReadAllText(fullJsonPath);
                ColorFormat detectedFormat = preferredFormat;
                LEDDataFrameRaw[] rawData = null;

                try
                {
                    // Try parsing with format hint first
                    LEDDataWithFormat formattedData = JsonConvert.DeserializeObject<LEDDataWithFormat>(jsonString);
                    if (formattedData != null && formattedData.frames != null)
                    {
                        rawData = formattedData.frames;
                        if (!string.IsNullOrEmpty(formattedData.color_format))
                        {
                            string cf = formattedData.color_format.Trim().ToLower();
                            if (cf == "rgb") detectedFormat = ColorFormat.RGB;
                            else if (cf == "rgbw") detectedFormat = ColorFormat.RGBW;
                            else if (cf == "hsv") detectedFormat = ColorFormat.HSV;
                            else if (cf == "rgbwmix") detectedFormat = ColorFormat.RGBWMix;
                        }
                    }
                    else
                    {
                        rawData = JsonConvert.DeserializeObject<LEDDataFrameRaw[]>(jsonString);
                    }
                }
                catch
                {
                    try
                    {
                        rawData = JsonConvert.DeserializeObject<LEDDataFrameRaw[]>(jsonString);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to parse JSON (both attempts): {fullJsonPath}. Error: {ex.Message}");
                        return null;
                    }
                }


                if (rawData == null)
                {
                    Debug.LogError($"Failed to parse JSON data from: {fullJsonPath}");
                    return null;
                }

                LEDDataFrame[] frames = new LEDDataFrame[rawData.Length];
                for (int i = 0; i < rawData.Length; i++)
                {
                    frames[i].frame = rawData[i].frame;
                    frames[i].format = detectedFormat;

                    if (rawData[i].pixels == null)
                    {
                        frames[i].pixels = Array.Empty<byte[]>();
                        continue;
                    }

                    frames[i].pixels = new byte[rawData[i].pixels.Length][];

                    for (int j = 0; j < rawData[i].pixels.Length; j++)
                    {
                        int[] pixelIntArray = rawData[i].pixels[j];
                        if (pixelIntArray == null)
                        {
                            frames[i].pixels[j] = Array.Empty<byte>();
                            continue;
                        }

                        int pixelSize = pixelIntArray.Length;
                        frames[i].pixels[j] = new byte[pixelSize];
                        for (int k = 0; k < pixelSize; k++)
                        {
                            frames[i].pixels[j][k] = (byte)Mathf.Clamp(pixelIntArray[k], 0, 255);
                        }
                    }
                }
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