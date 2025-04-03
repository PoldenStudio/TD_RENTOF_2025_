using System;
using System.Collections.Generic;
using UnityEngine;

namespace LEDControl
{
    [Serializable]
    public class PreBakedSunDataEntry
    {
        public int state; // Например: 0 = Idle, 1 = Active
        public float baseCycleLength;
        public int frameCount;
        public float frameDuration; 
        public string hexData;  // Объединённая строка (конкатенация HEX-данных для всех кадров)
    }

    [Serializable]
    public class PreBakedSunDataForStrip
    {
        public int stripIndex;
        public List<PreBakedSunDataEntry> entries = new List<PreBakedSunDataEntry>();
    }

    [CreateAssetMenu(fileName = "SunDataCache", menuName = "LEDControl/SunDataCache", order = 1)]
    public class SunDataCache : ScriptableObject
    {
        public List<PreBakedSunDataForStrip> preBakedSunData = new List<PreBakedSunDataForStrip>();
    }
}