using UnityEngine;

namespace DemolitionStudios.DemolitionMedia
{
    public interface IClockProvider
    {
        string Name { get; }
        float GetSpeed();
        float GetPosition();
        bool GetPause();
    }

    public class ClockProviderBase<TClockProvider> : MonoBehaviour
        where TClockProvider : IClockProvider, new()
    {
        public virtual void Awake()
        {
            var server = gameObject.GetComponent(
                typeof(LFOClockNetworkEnetServer)) as LFOClockNetworkEnetServer;

            if (server != null)
                server.ClockProvider = this as IClockProvider;
            else
                Utilities.LogWarning("[ClockProviderBase]: cannot find LFOClockNetworkEnetServer component");
        }
    }
}
