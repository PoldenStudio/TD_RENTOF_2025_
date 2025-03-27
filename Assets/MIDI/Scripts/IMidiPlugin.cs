using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// Common interface for MIDI Plugins
    /// </summary>
    public interface IMidiPlugin
    {
        /// <summary>
        /// Initializes MIDI Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
        void InitializeMidi(Action initializeCompletedAction);

        /// <summary>
        /// Terminates MIDI Plugin system
        /// </summary>
        void TerminateMidi();

#if UNITY_EDITOR
        /// <summary>
        /// Called when Unity Editor play mode changed
        /// </summary>
        /// <param name="stateChange"></param>
        void PlayModeStateChanged(PlayModeStateChange stateChange);
#endif

#if (UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
        /// <summary>
        /// Starts to scan BLE MIDI devices
        /// for Android / iOS / WebGL devices only
        /// </summary>
        /// <param name="timeout">timeout milliseconds, 0 : no timeout</param>
        void StartScanBluetoothMidiDevices(int timeout);

        /// <summary>
        /// Stops to scan BLE MIDI devices
        /// for Android / iOS / WebGL devices only
        /// </summary>
        void StopScanBluetoothMidiDevices();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Start to advertise BLE MIDI Peripheral device
        /// for Android devices only
        /// </summary>
        /// <exception cref="NotImplementedException">the platform isn't available</exception>
        void StartAdvertisingBluetoothMidiDevice();

        /// <summary>
        /// Stop to advertise BLE MIDI Peripheral device
        /// for Android devices only
        /// </summary>
        /// <exception cref="NotImplementedException">the platform isn't available</exception>
        void StopAdvertisingBluetoothMidiDevice();
#endif

        /// <summary>
        /// Obtains device name for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        string GetDeviceName(string deviceId);

        /// <summary>
        /// Obtains device vendor id for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        string GetVendorId(string deviceId);

        /// <summary>
        /// Obtains device product id for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        string GetProductId(string deviceId);

        /// <summary>
        /// Sends a Note On message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        void SendMidiNoteOn(string deviceId, int group, int channel, int note, int velocity);

        /// <summary>
        /// Sends a Note Off message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        void SendMidiNoteOff(string deviceId, int group, int channel, int note, int velocity);

        /// <summary>
        /// Sends a Polyphonic Aftertouch message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="pressure">0-127</param>
        void SendMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure);

        /// <summary>
        /// Sends a Control Change message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="function">0-127</param>
        /// <param name="value">0-127</param>
        void SendMidiControlChange(string deviceId, int group, int channel, int function, int value);

        /// <summary>
        /// Sends a Program Change message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="program">0-127</param>
        void SendMidiProgramChange(string deviceId, int group, int channel, int program);

        /// <summary>
        /// Sends a Channel Aftertouch message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="pressure">0-127</param>
        void SendMidiChannelAftertouch(string deviceId, int group, int channel, int pressure);

        /// <summary>
        /// Sends a Pitch Wheel message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="amount">0-16383</param>
        void SendMidiPitchWheel(string deviceId, int group, int channel, int amount);

        /// <summary>
        /// Sends a System Exclusive message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="sysEx">byte array starts with F0, ends with F7</param>
        void SendMidiSystemExclusive(string deviceId, int group, byte[] sysEx);

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Sends a System Common message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="message">byte array</param>
        void SendMidiSystemCommonMessage(string deviceId, int group, byte[] message);

        /// <summary>
        /// Sends a Single Byte message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        void SendMidiSingleByte(string deviceId, int group, int byte1);
#endif

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing);

        /// <summary>
        /// Sends a Song Select message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="song">0-127</param>
        void SendMidiSongSelect(string deviceId, int group, int song);

        /// <summary>
        /// Sends a Song Position Pointer message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="position">0-16383</param>
        void SendMidiSongPositionPointer(string deviceId, int group, int position);

        /// <summary>
        /// Sends a Tune Request message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void SendMidiTuneRequest(string deviceId, int group);

        /// <summary>
        /// Sends a Timing Clock message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void SendMidiTimingClock(string deviceId, int group);

        /// <summary>
        /// Sends a Start message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void SendMidiStart(string deviceId, int group);

        /// <summary>
        /// Sends a Continue message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void SendMidiContinue(string deviceId, int group);

        /// <summary>
        /// Sends a Stop message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void SendMidiStop(string deviceId, int group);

        /// <summary>
        /// Sends an Active Sensing message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void SendMidiActiveSensing(string deviceId, int group);

        /// <summary>
        /// Sends a Reset message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        void SendMidiReset(string deviceId, int group);

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Sends a Miscellaneous Function Codes message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1"></param>
        /// <param name="byte2"></param>
        /// <param name="byte3"></param>
        void SendMidiMiscellaneousFunctionCodes(string deviceId, int group, int byte1, int byte2, int byte3);

        /// <summary>
        /// Sends a Cable Events message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        /// <param name="byte2">0-255</param>
        /// <param name="byte3">0-255</param>
        void SendMidiCableEvents(string deviceId, int group, int byte1, int byte2, int byte3);
#endif
    }
}