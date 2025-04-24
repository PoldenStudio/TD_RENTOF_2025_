using UnityEngine;

public class Comet
{
    public float position;
    public Color32 color;
    public float length;
    public float brightness;
    public bool isActive;
    public float startTime;
    public bool isMoving;
    public float direction;

    private int totalLEDs;
    private float tailIntensity;
    private float[] brightnessProfile;

    public Comet(float position, Color32 color, float length, float brightness, float direction, int totalLEDs, float tailIntensity)
    {
        this.position = position;
        this.color = new Color32(
            (byte)(color.r * brightness),
            (byte)(color.g * brightness),
            (byte)(color.b * brightness),
            255
        );
        this.length = Mathf.Max(1f, length);
        this.direction = direction;
        this.totalLEDs = totalLEDs;
        this.tailIntensity = tailIntensity;

        InitializeBrightnessProfile();
    }

    private void InitializeBrightnessProfile()
    {
        int ledCount = Mathf.CeilToInt(length);
        brightnessProfile = new float[ledCount];

        for (int i = 0; i < ledCount; i++)
        {
            float normalizedPosition = (float)i / (ledCount - 1);
            float falloff = Mathf.Pow(Mathf.Cos(normalizedPosition * Mathf.PI * 0.5f), 0.5f);
            brightnessProfile[i] = falloff * (1 + tailIntensity);
        }
    }

    public void UpdatePosition(float deltaTime, float speed, int newTotalLEDs)
    {
        if (totalLEDs != newTotalLEDs)
        {
            totalLEDs = newTotalLEDs;
        }

        position += speed * deltaTime * direction;
        position = WrapPosition(position, totalLEDs);
    }

    public Color32 GetColorAtLed(int ledIndex, float globalBrightness)
    {
        float distance = GetDistanceOnCircle(ledIndex, position, totalLEDs);
        if (distance >= length) return new Color32(0, 0, 0, 0);

        int profileIndex = Mathf.FloorToInt(distance / length * (brightnessProfile.Length - 1));
        float ledBrightness = brightnessProfile[profileIndex] * globalBrightness;

        return new Color32(
            (byte)(color.r * ledBrightness),
            (byte)(color.g * ledBrightness),
            (byte)(color.b * ledBrightness),
            255
        );
    }

    private float GetDistanceOnCircle(int ledIndex, float centerPosition, int totalLeds)
    {
        float directDistance = Mathf.Abs(ledIndex - centerPosition);
        float wrapDistance = totalLeds - directDistance;
        return Mathf.Min(directDistance, wrapDistance);
    }

    private float WrapPosition(float pos, int totalLeds)
    {
        return ((pos % totalLeds) + totalLeds) % totalLeds;
    }

    public void UpdateTailIntensity(float newTailIntensity)
    {
        if (Mathf.Approximately(tailIntensity, newTailIntensity))
            return;

        tailIntensity = newTailIntensity;
        InitializeBrightnessProfile();
    }
}