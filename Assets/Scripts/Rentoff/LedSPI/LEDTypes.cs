using System;
using UnityEngine;
using System.Collections.Generic;

namespace LEDControl
{
    public enum DisplayMode
    {
        GlobalColor,
        SegmentColor,
        SpeedSynthMode,
        SunMovement
    }

    public enum DataMode
    {
        Monochrome1Color = 0,
        Monochrome2Color = 1,
        RGBW = 2,
        RGB = 3
    }

    public enum MoveDirection
    {
        Forward,
        Backward
    }

    [Serializable]
    public class MonochromeStripSettings
    {
        public Color32 globalColor = new(255, 255, 255, 255);
        public Color32 synthColor = new(255, 255, 255, 255);
    }

    [Serializable]
    public class RGBStripSettings
    {
        public Color32 globalColor = new(0, 0, 0, 255);
        public Color32 synthColor = new(255, 255, 255, 255);
    }

    public static class Color32Extensions
    {
        public static bool IsDifferent(this Color32 c1, Color32 c2)
        {
            return c1.r != c2.r || c1.g != c2.g || c1.b != c2.b || c1.a != c2.a;
        }
    }
}