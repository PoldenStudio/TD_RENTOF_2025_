using System.Collections;
using System.Collections.Generic;
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

    public int lastLedIndex = -1;
    public int[] affectedLeds = null;
    public float[] brightnessByLed = null;

    public Comet(float position, Color32 color, float length, float brightness, float direction)
    {
        this.position = position;
        this.color = color;
        this.length = length;
        this.brightness = brightness;
        this.isActive = true;
        this.startTime = Time.time;
        this.isMoving = false;
        this.direction = direction;
    }

    public void UpdateCache(int totalLEDs, float tailIntensity)
    {
        int dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(length));
        if (affectedLeds == null || affectedLeds.Length != dynamicLedCount)
        {
            affectedLeds = new int[dynamicLedCount];
            brightnessByLed = new float[dynamicLedCount];
        }

        int ledIndex = Mathf.FloorToInt(position);
        lastLedIndex = ledIndex;

        for (int j = 0; j < dynamicLedCount; j++)
        {
            int offset = direction > 0 ? j : -j;
            int currentLedIndex = ledIndex + offset;
            currentLedIndex = Mathf.RoundToInt(Mathf.Repeat(currentLedIndex, totalLEDs));
            affectedLeds[j] = currentLedIndex;

            float tailFalloff = 1f - ((float)j / (dynamicLedCount - 1));
            tailFalloff = Mathf.Clamp01(tailFalloff);
            brightnessByLed[j] = tailFalloff * tailIntensity;
        }
    }
}