using UnityEngine;

namespace DemolitionStudios.DemolitionMedia
{
    public class SampleClockProvider : ClockProviderBase<SampleClockProvider>, IClockProvider
    {
        public string Name { get { return "Sample Clock"; } }
        public float GetSpeed() { return 1.0f; }
        public float GetPosition() { return Time.time; }
        public bool GetPause() { return false; }
    }
}
