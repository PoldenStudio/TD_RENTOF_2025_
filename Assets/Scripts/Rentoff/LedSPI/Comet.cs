using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Comet
{
    public float position;         // Float position for smooth movement
    public Color32 color;
    public float length;           // Fixed length
    public float brightness;
    public bool isActive;
    public float startTime;
    public bool isMoving;
    public float direction;        // 1 for forward, -1 for backward

    // Cached values to avoid recalculations
    private int totalLEDs;
    private float tailIntensity;
    private float[] brightnessProfile; // Pre-calculated brightness profile

    public Comet(float position, Color32 color, float length, float brightness, float direction, int totalLEDs, float tailIntensity)
    {
        this.position = position;
        this.color = color;
        this.length = Mathf.Max(1f, length); // Ensure minimum length
        this.brightness = brightness;
        this.isActive = true;
        this.startTime = Time.time;
        this.isMoving = false;
        this.direction = direction;
        this.totalLEDs = totalLEDs;
        this.tailIntensity = tailIntensity;

        // Pre-calculate brightness profile
        InitializeBrightnessProfile();
    }

    private void InitializeBrightnessProfile()
    {
        int ledCount = Mathf.CeilToInt(length);
        brightnessProfile = new float[ledCount];

        // Create a smooth falloff for the tail
        for (int i = 0; i < ledCount; i++)
        {
            float normalizedPosition = (float)i / (ledCount - 1);
            // Smoother falloff using a cosine curve
            float falloff = Mathf.Cos(normalizedPosition * Mathf.PI * 0.5f);
            brightnessProfile[i] = falloff * tailIntensity;
        }
    }

    public void UpdatePosition(float deltaTime, float speed, int newTotalLEDs)
    {
        // Update total LEDs if changed
        if (totalLEDs != newTotalLEDs)
        {
            totalLEDs = newTotalLEDs;
        }

        position += speed * deltaTime * direction;

        // Wrap position around the loop properly
        position = WrapPosition(position, totalLEDs);
    }

    // Get a smooth, interpolated color at a specific LED position
    public Color32 GetColorAtLed(int ledIndex, float globalBrightness)
    {
        float distance = GetDistanceOnCircle(ledIndex, position, totalLEDs);

        // If the LED is too far from the comet center, don't light it
        if (distance >= length)
            return new Color32(0, 0, 0, 0);

        // Find the proper brightness index from our profile
        int profileIndex = Mathf.FloorToInt(distance / length * brightnessProfile.Length);
        profileIndex = Mathf.Clamp(profileIndex, 0, brightnessProfile.Length - 1);

        float ledBrightness = brightnessProfile[profileIndex] * brightness * globalBrightness;

        return new Color32(
            (byte)(color.r * ledBrightness),
            (byte)(color.g * ledBrightness),
            (byte)(color.b * ledBrightness),
            color.a
        );
    }

    // Helper method to calculate the shortest distance on a circular strip
    private float GetDistanceOnCircle(int ledIndex, float centerPosition, int totalLeds)
    {
        float directDistance = Mathf.Abs(ledIndex - centerPosition);
        float wrapDistance = totalLeds - directDistance;
        return Mathf.Min(directDistance, wrapDistance);
    }

    // Properly wrap a position around the circular strip
    private float WrapPosition(float pos, int totalLeds)
    {
        // Handle both positive and negative positions correctly
        return ((pos % totalLeds) + totalLeds) % totalLeds;
    }

    // Update the tail intensity if needed
    public void UpdateTailIntensity(float newTailIntensity)
    {
        if (Mathf.Approximately(tailIntensity, newTailIntensity))
            return;

        tailIntensity = newTailIntensity;
        InitializeBrightnessProfile();
    }
}
