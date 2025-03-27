namespace LEDControl
{
    public enum ColorFormat
    {
        RGB,
        RGBW,
        HSV,
        RGBWMix
    }

    public struct LEDDataFrame
    {
        public int frame;
        public byte[][] pixels;
        public ColorFormat format;
    }

    public struct PreCalculatedDmxFrame
    {
        public byte[] channelValues;
    }
}
