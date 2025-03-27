using UnityEngine.EventSystems;

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MIDI Note On event handler
    /// </summary>
    public interface IMidiNoteOnEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Note On received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        void OnMidiNoteOn(string deviceId, int group, int channel, int note, int velocity);
    }

    /// <summary>
    /// MIDI Note Off event handler
    /// </summary>
    public interface IMidiNoteOffEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Note Off received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        void OnMidiNoteOff(string deviceId, int group, int channel, int note, int velocity);
    }

    /// <summary>
    /// MIDI Polyphonic Aftertouch event handler
    /// </summary>
    public interface IMidiPolyphonicAftertouchEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Polyphonic Aftertouch received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="pressure">0-127</param>
        void OnMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure);
    }

    /// <summary>
    /// MIDI Control Change event handler
    /// </summary>
    public interface IMidiControlChangeEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Control Change received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="function">0-127</param>
        /// <param name="value">0-127</param>
        void OnMidiControlChange(string deviceId, int group, int channel, int function, int value);
    }

    /// <summary>
    /// MIDI Program Change event handler
    /// </summary>
    public interface IMidiProgramChangeEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Program Change received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="program">0-127</param>
        void OnMidiProgramChange(string deviceId, int group, int channel, int program);
    }

    /// <summary>
    /// MIDI Channel Aftertouch event handler
    /// </summary>
    public interface IMidiChannelAftertouchEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Channel Aftertouch received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="pressure">0-127</param>
        void OnMidiChannelAftertouch(string deviceId, int group, int channel, int pressure);
    }

    /// <summary>
    /// MIDI Pitch Wheel event handler
    /// </summary>
    public interface IMidiPitchWheelEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Pitch Wheel received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="amount">0-16383</param>
        void OnMidiPitchWheel(string deviceId, int group, int channel, int amount);
    }

    /// <summary>
    /// MIDI System Exclusive event handler
    /// </summary>
    public interface IMidiSystemExclusiveEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// System Exclusive received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="systemExclusive">the system exclusive</param>
        void OnMidiSystemExclusive(string deviceId, int group, byte[] systemExclusive);
    }

    /// <summary>
    /// MIDI System Common event handler
    /// </summary>
    public interface IMidiSystemCommonMessageEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// System Common received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="message">the message</param>
        void OnMidiSystemCommonMessage(string deviceId, int group, byte[] message);
    }

    /// <summary>
    /// MIDI Single Byte event handler
    /// </summary>
    public interface IMidiSingleByteEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Single Byte received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        void OnMidiSingleByte(string deviceId, int group, int byte1);
    }

    /// <summary>
    /// MIDI Time Code Quarter Frame event handler
    /// </summary>
    public interface IMidiTimeCodeQuarterFrameEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Time Code Quarter Frame received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        void OnMidiTimeCodeQuarterFrame(string deviceId, int group, int timing);
    }

    /// <summary>
    /// MIDI Song Select event handler
    /// </summary>
    public interface IMidiSongSelectEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Song Select received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="song">0-127</param>
        void OnMidiSongSelect(string deviceId, int group, int song);
    }

    /// <summary>
    /// MIDI Song Position Pointer event handler
    /// </summary>
    public interface IMidiSongPositionPointerEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Song Position Pointer received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="position">0-16383</param>
        void OnMidiSongPositionPointer(string deviceId, int group, int position);
    }

    /// <summary>
    /// MIDI Tune Request event handler
    /// </summary>
    public interface IMidiTuneRequestEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Tune Request received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void OnMidiTuneRequest(string deviceId, int group);
    }

    /// <summary>
    /// MIDI Timing Clock event handler
    /// </summary>
    public interface IMidiTimingClockEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Timing Clock received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void OnMidiTimingClock(string deviceId, int group);
    }

    /// <summary>
    /// MIDI Start event handler
    /// </summary>
    public interface IMidiStartEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Start received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void OnMidiStart(string deviceId, int group);
    }

    /// <summary>
    /// MIDI Continue event handler
    /// </summary>
    public interface IMidiContinueEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Continue received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void OnMidiContinue(string deviceId, int group);
    }

    /// <summary>
    /// MIDI Stop event handler
    /// </summary>
    public interface IMidiStopEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Stop received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void OnMidiStop(string deviceId, int group);
    }

    /// <summary>
    /// MIDI Active Sensing event handler
    /// </summary>
    public interface IMidiActiveSensingEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Active Sensing received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void OnMidiActiveSensing(string deviceId, int group);
    }

    /// <summary>
    /// MIDI Reset event handler
    /// </summary>
    public interface IMidiResetEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Reset received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void OnMidiReset(string deviceId, int group);
    }

    /// <summary>
    /// MIDI Miscellaneous Function Codes event handler
    /// </summary>
    public interface IMidiMiscellaneousFunctionCodesEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Miscellaneous Function Codes received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        /// <param name="byte2">0-255</param>
        /// <param name="byte3">0-255</param>
        void OnMidiMiscellaneousFunctionCodes(string deviceId, int group, int byte1, int byte2, int byte3);
    }

    /// <summary>
    /// MIDI Cable Events event handler
    /// </summary>
    public interface IMidiCableEventsEventHandler : IEventSystemHandler
    {
        /// <summary>
        /// Cable Events received
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        /// <param name="byte2">0-255</param>
        /// <param name="byte3">0-255</param>
        void OnMidiCableEvents(string deviceId, int group, int byte1, int byte2, int byte3);
    }

    /// <summary>
    /// MIDI Playing events handler
    /// </summary>
    public interface IMidiPlayingEventsHandler : IMidiNoteOnEventHandler, IMidiNoteOffEventHandler,
        IMidiChannelAftertouchEventHandler, IMidiPitchWheelEventHandler, IMidiPolyphonicAftertouchEventHandler,
        IMidiProgramChangeEventHandler, IMidiControlChangeEventHandler
    {
    }

    /// <summary>
    /// MIDI System events handler
    /// </summary>
    public interface IMidiSystemEventsHandler : IMidiContinueEventHandler, IMidiResetEventHandler,
        IMidiStartEventHandler, IMidiStopEventHandler, IMidiActiveSensingEventHandler, IMidiCableEventsEventHandler,
        IMidiSongSelectEventHandler, IMidiSongPositionPointerEventHandler, IMidiSingleByteEventHandler,
        IMidiSystemExclusiveEventHandler, IMidiSystemCommonMessageEventHandler, IMidiTimeCodeQuarterFrameEventHandler,
        IMidiTimingClockEventHandler, IMidiTuneRequestEventHandler, IMidiMiscellaneousFunctionCodesEventHandler
    {
    }

    /// <summary>
    /// MIDI All events handler
    /// </summary>
    public interface IMidiAllEventsHandler : IMidiPlayingEventsHandler, IMidiSystemEventsHandler
    {
    }
}