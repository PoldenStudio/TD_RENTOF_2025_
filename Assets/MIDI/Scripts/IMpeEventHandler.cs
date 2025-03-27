using UnityEngine.EventSystems;

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MPE Zone defined event handler
    /// </summary>
    public interface IMpeZoneDefinedEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// MPE Zone defined
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="memberChannelCount">member channels count, 0: zone removed, 1-15: zone defined</param>
        void OnMpeZoneDefined(string deviceId, int managerChannel, int memberChannelCount);
    }

    /// <summary>
    /// MPE Note On event handler
    /// </summary>
    public interface IMpeNoteOnEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Note On received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="note"></param>
        /// <param name="velocity"></param>
        void OnMpeNoteOn(string deviceId, int managerChannel, int note, int velocity);
    }

    /// <summary>
    /// MPE Note Off event handler
    /// </summary>
    public interface IMpeNoteOffEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Note Off received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="note"></param>
        /// <param name="velocity"></param>
        void OnMpeNoteOff(string deviceId, int managerChannel, int note, int velocity);
    }

    /// <summary>
    /// MPE Polyphonic Aftertouch event handler
    /// </summary>
    public interface IMpePolyphonicAftertouchEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Polyphonic Aftertouch received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="note"></param>
        /// <param name="pressure"></param>
        void OnMpePolyphonicAftertouch(string deviceId, int managerChannel, int note, int pressure);
    }

    /// <summary>
    /// MPE Control Change event handler
    /// </summary>
    public interface IMpeControlChangeEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Control Change received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="function"></param>
        /// <param name="value"></param>
        void OnMpeControlChange(string deviceId, int managerChannel, int function, int value);
    }

    /// <summary>
    /// MPE Program Change event handler
    /// </summary>
    public interface IMpeProgramChangeEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Program Change received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="program"></param>
        void OnMpeProgramChange(string deviceId, int managerChannel, int program);
    }

    /// <summary>
    /// MPE Channel Aftertouch event handler
    /// </summary>
    public interface IMpeChannelAftertouchEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Channel Aftertouch received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="pressure"></param>
        void OnMpeChannelAftertouch(string deviceId, int managerChannel, int pressure);
    }

    /// <summary>
    /// MPE Pitch Wheel event handler
    /// </summary>
    public interface IMpePitchWheelEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Pitch Wheel received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="amount"></param>
        void OnMpePitchWheel(string deviceId, int managerChannel, int amount);
    }

    /// <summary>
    /// MPE System Exclusive event handler
    /// </summary>
    public interface IMpeSystemExclusiveEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// System Exclusive received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="systemExclusive"></param>
        void OnMpeSystemExclusive(string deviceId, byte[] systemExclusive);
    }

    /// <summary>
    /// MPE Time Code Quarter Frame event handler
    /// </summary>
    public interface IMpeTimeCodeQuarterFrameEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Time Code Quarter Frame received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="timing"></param>
        void OnMpeTimeCodeQuarterFrame(string deviceId, int timing);
    }

    /// <summary>
    /// MPE Song Select Event event handler
    /// </summary>
    public interface IMpeSongSelectEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Song Select Event received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="song"></param>
        void OnMpeSongSelect(string deviceId, int song);
    }

    /// <summary>
    /// MPE Song Position Pointer event handler
    /// </summary>
    public interface IMpeSongPositionPointerEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Song Position Pointer received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        /// <param name="position"></param>
        void OnMpeSongPositionPointer(string deviceId, int position);
    }

    /// <summary>
    /// MPE Tune Request event handler
    /// </summary>
    public interface IMpeTuneRequestEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Tune Request received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        void OnMpeTuneRequest(string deviceId);
    }

    /// <summary>
    /// MPE Timing Clock event handler
    /// </summary>
    public interface IMpeTimingClockEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Timing Clock received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        void OnMpeTimingClock(string deviceId);
    }

    /// <summary>
    /// MPE Start event handler
    /// </summary>
    public interface IMpeStartEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Start received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        void OnMpeStart(string deviceId);
    }

    /// <summary>
    /// MPE Continue event handler
    /// </summary>
    public interface IMpeContinueEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Continue received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        void OnMpeContinue(string deviceId);
    }

    /// <summary>
    /// MPE Stop event handler
    /// </summary>
    public interface IMpeStopEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Stop received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        void OnMpeStop(string deviceId);
    }

    /// <summary>
    /// MPE Active Sensing event handler
    /// </summary>
    public interface IMpeActiveSensingEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Active Sensing received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        void OnMpeActiveSensing(string deviceId);
    }

    /// <summary>
    /// MPE Reset event handler
    /// </summary>
    public interface IMpeResetEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Reset received
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        void OnMpeReset(string deviceId);
    }

    /// <summary>
    /// MPE Playing events handler
    /// </summary>
    public interface IMpePlayingEventsHandler : IMpeNoteOnEventHandler, IMpeNoteOffEventHandler,
        IMpeChannelAftertouchEventHandler, IMpePitchWheelEventHandler, IMpePolyphonicAftertouchEventHandler,
        IMpeProgramChangeEventHandler, IMpeControlChangeEventHandler
    {
    }

    /// <summary>
    /// MPE System events handler
    /// </summary>
    public interface IMpeSystemEventsHandler : IMpeZoneDefinedEventHandler, IMpeContinueEventHandler, IMpeResetEventHandler,
        IMpeStartEventHandler, IMpeStopEventHandler, IMpeActiveSensingEventHandler, IMpeSongSelectEventHandler,
        IMpeSongPositionPointerEventHandler, IMpeSystemExclusiveEventHandler, IMpeTimeCodeQuarterFrameEventHandler,
        IMpeTimingClockEventHandler, IMpeTuneRequestEventHandler
    {
    }

    /// <summary>
    /// MPE All events handler
    /// </summary>
    public interface IMpeAllEventsHandler : IMpePlayingEventsHandler, IMpeSystemEventsHandler
    {
    }
}