#if UNITY_WEBGL
using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MIDI Plugin for WebGL
    /// </summary>
    public class WebGlMidiPlugin : IMidiPlugin
    {
        [DllImport("__Internal")]
        private static extern void midiPluginInitialize();
        [DllImport("__Internal")]
        private static extern string getDeviceName(string deviceId);
        [DllImport("__Internal")]
        private static extern string getVendorId(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendMidiNoteOff(string deviceId, byte channel, byte note, byte velocity);
        [DllImport("__Internal")]
        private static extern void sendMidiNoteOn(string deviceId, byte channel, byte note, byte velocity);
        [DllImport("__Internal")]
        private static extern void sendMidiPolyphonicAftertouch(string deviceId, byte channel, byte note, byte pressure);
        [DllImport("__Internal")]
        private static extern void sendMidiControlChange(string deviceId, byte channel, byte function, byte value);
        [DllImport("__Internal")]
        private static extern void sendMidiProgramChange(string deviceId, byte channel, byte program);
        [DllImport("__Internal")]
        private static extern void sendMidiChannelAftertouch(string deviceId, byte channel, byte pressure);
        [DllImport("__Internal")]
        private static extern void sendMidiPitchWheel(string deviceId, byte channel, int amount);
        [DllImport("__Internal")]
        private static extern void sendMidiSystemExclusive(string deviceId, byte[] data);
        [DllImport("__Internal")]
        private static extern void sendMidiTimeCodeQuarterFrame(string deviceId, int value);
        [DllImport("__Internal")]
        private static extern void sendMidiSongPositionPointer(string deviceId, int position);
        [DllImport("__Internal")]
        private static extern void sendMidiSongSelect(string deviceId, byte song);
        [DllImport("__Internal")]
        private static extern void sendMidiTuneRequest(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendMidiTimingClock(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendMidiStart(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendMidiContinue(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendMidiStop(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendMidiActiveSensing(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendMidiReset(string deviceId);

        // BLE MIDI for WebGL
        [DllImport("__Internal")]
        private static extern void bleMidiPluginInitialize();
        [DllImport("__Internal")]
        private static extern void startScanBluetoothMidiDevices();
        [DllImport("__Internal")]
        private static extern string getBleDeviceName(string deviceId);
        [DllImport("__Internal")]
        private static extern string getBleVendorId(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendBleMidiNoteOff(string deviceId, byte channel, byte note, byte velocity);
        [DllImport("__Internal")]
        private static extern void sendBleMidiNoteOn(string deviceId, byte channel, byte note, byte velocity);
        [DllImport("__Internal")]
        private static extern void sendBleMidiPolyphonicAftertouch(string deviceId, byte channel, byte note, byte pressure);
        [DllImport("__Internal")]
        private static extern void sendBleMidiControlChange(string deviceId, byte channel, byte function, byte value);
        [DllImport("__Internal")]
        private static extern void sendBleMidiProgramChange(string deviceId, byte channel, byte program);
        [DllImport("__Internal")]
        private static extern void sendBleMidiChannelAftertouch(string deviceId, byte channel, byte pressure);
        [DllImport("__Internal")]
        private static extern void sendBleMidiPitchWheel(string deviceId, byte channel, int amount);
        [DllImport("__Internal")]
        private static extern void sendBleMidiSystemExclusive(string deviceId, byte[] data);
        [DllImport("__Internal")]
        private static extern void sendBleMidiTimeCodeQuarterFrame(string deviceId, int value);
        [DllImport("__Internal")]
        private static extern void sendBleMidiSongPositionPointer(string deviceId, int position);
        [DllImport("__Internal")]
        private static extern void sendBleMidiSongSelect(string deviceId, byte song);
        [DllImport("__Internal")]
        private static extern void sendBleMidiTuneRequest(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendBleMidiTimingClock(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendBleMidiStart(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendBleMidiContinue(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendBleMidiStop(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendBleMidiActiveSensing(string deviceId);
        [DllImport("__Internal")]
        private static extern void sendBleMidiReset(string deviceId);

        /// <summary>
        /// Initializes MIDI Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
        public void InitializeMidi(Action initializeCompletedAction)
        {
            midiPluginInitialize();
            bleMidiPluginInitialize();
            initializeCompletedAction?.Invoke();
        }

        /// <summary>
        /// Terminates MIDI Plugin system
        /// </summary>
        public void TerminateMidi()
        {
            // do nothing
        }

#if UNITY_EDITOR
        /// <summary>
        /// Called when Unity Editor play mode changed
        /// </summary>
        /// <param name="stateChange"></param>
        public void PlayModeStateChanged(PlayModeStateChange stateChange)
        {
            // do nothing
        }
#endif

#if !UNITY_EDITOR
        /// <summary>
        /// Starts to scan BLE MIDI devices
        /// for Android / iOS / WebGL devices only
        /// </summary>
        /// <param name="timeout">timeout milliseconds, 0 : no timeout</param>
        public void StartScanBluetoothMidiDevices(int timeout)
        {
            startScanBluetoothMidiDevices();
        }

        /// <summary>
        /// Stops to scan BLE MIDI devices
        /// for Android / iOS devices only
        /// </summary>
        public void StopScanBluetoothMidiDevices()
        {
            // do nothing
        }
#endif

        /// <summary>
        /// Obtains device name for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetDeviceName(string deviceId)
        {
            var deviceName = getDeviceName(deviceId);
            if (!string.IsNullOrEmpty(deviceName))
            {
                return deviceName;
            }

            deviceName = getBleDeviceName(deviceId);
            if (!string.IsNullOrEmpty(deviceName))
            {
                return deviceName;
            }

            return null;
        }

        /// <summary>
        /// Obtains device vendor id for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetVendorId(string deviceId)
        {
            var vendorId = getVendorId(deviceId);
            if (!string.IsNullOrEmpty(vendorId))
            {
                return vendorId;
            }
            vendorId = getBleVendorId(deviceId);
            if (!string.IsNullOrEmpty(vendorId))
            {
                return vendorId;
            }

            return null;
        }

        /// <summary>
        /// Obtains device product id for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetProductId(string deviceId)
        {
            // not supported for WebGL
            return null;
        }

        /// <summary>
        /// Sends a Note On message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        public void SendMidiNoteOn(string deviceId, int group, int channel, int note, int velocity)
        {
            sendMidiNoteOn(deviceId, (byte)channel, (byte)note, (byte)velocity);
            sendBleMidiNoteOn(deviceId, (byte)channel, (byte)note, (byte)velocity);
        }

        /// <summary>
        /// Sends a Note Off message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        public void SendMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
        {
            sendMidiNoteOff(deviceId, (byte)channel, (byte)note, (byte)velocity);
            sendBleMidiNoteOff(deviceId, (byte)channel, (byte)note, (byte)velocity);
        }

        /// <summary>
        /// Sends a Polyphonic Aftertouch message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="pressure">0-127</param>
        public void SendMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
        {
            sendMidiPolyphonicAftertouch(deviceId, (byte)channel, (byte)note, (byte)pressure);
            sendBleMidiPolyphonicAftertouch(deviceId, (byte)channel, (byte)note, (byte)pressure);
        }

        /// <summary>
        /// Sends a Control Change message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="function">0-127</param>
        /// <param name="value">0-127</param>
        public void SendMidiControlChange(string deviceId, int group, int channel, int function, int value)
        {
            sendMidiControlChange(deviceId, (byte)channel, (byte)function, (byte)value);
            sendBleMidiControlChange(deviceId, (byte)channel, (byte)function, (byte)value);
        }

        /// <summary>
        /// Sends a Program Change message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="program">0-127</param>
        public void SendMidiProgramChange(string deviceId, int group, int channel, int program)
        {
            sendMidiProgramChange(deviceId, (byte)channel, (byte)program);
            sendBleMidiProgramChange(deviceId, (byte)channel, (byte)program);
        }

        /// <summary>
        /// Sends a Channel Aftertouch message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="pressure">0-127</param>
        public void SendMidiChannelAftertouch(string deviceId, int group, int channel, int pressure)
        {
            sendMidiChannelAftertouch(deviceId, (byte)channel, (byte)pressure);
            sendBleMidiChannelAftertouch(deviceId, (byte)channel, (byte)pressure);
        }

        /// <summary>
        /// Sends a Pitch Wheel message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="amount">0-16383</param>
        public void SendMidiPitchWheel(string deviceId, int group, int channel, int amount)
        {
            sendMidiPitchWheel(deviceId, (byte)channel, (byte)amount);
            sendBleMidiPitchWheel(deviceId, (byte)channel, (byte)amount);
        }

        /// <summary>
        /// Sends a System Exclusive message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="sysEx">byte array starts with F0, ends with F7</param>
        public void SendMidiSystemExclusive(string deviceId, int group, byte[] sysEx)
        {
            sendMidiSystemExclusive(deviceId, sysEx);
            sendBleMidiSystemExclusive(deviceId, sysEx);
        }

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        public void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
        {
            sendMidiTimeCodeQuarterFrame(deviceId, timing);
            sendBleMidiTimeCodeQuarterFrame(deviceId, timing);
        }

        /// <summary>
        /// Sends a Song Select message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="song">0-127</param>
        public void SendMidiSongSelect(string deviceId, int group, int song)
        {
            sendMidiSongSelect(deviceId, (byte)song);
            sendBleMidiSongSelect(deviceId, (byte)song);
        }

        /// <summary>
        /// Sends a Song Position Pointer message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="position">0-16383</param>
        public void SendMidiSongPositionPointer(string deviceId, int group, int position)
        {
            sendMidiSongPositionPointer(deviceId, position);
            sendBleMidiSongPositionPointer(deviceId, position);
        }

        /// <summary>
        /// Sends a Tune Request message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTuneRequest(string deviceId, int group)
        {
            sendMidiTuneRequest(deviceId);
            sendBleMidiTuneRequest(deviceId);
        }

        /// <summary>
        /// Sends a Timing Clock message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTimingClock(string deviceId, int group)
        {
            sendMidiTimingClock(deviceId);
            sendBleMidiTimingClock(deviceId);
        }

        /// <summary>
        /// Sends a Start message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStart(string deviceId, int group)
        {
            sendMidiStart(deviceId);
            sendBleMidiStart(deviceId);
        }

        /// <summary>
        /// Sends a Continue message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiContinue(string deviceId, int group)
        {
            sendMidiContinue(deviceId);
            sendBleMidiContinue(deviceId);
        }

        /// <summary>
        /// Sends a Stop message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStop(string deviceId, int group)
        {
            sendMidiStop(deviceId);
            sendBleMidiStop(deviceId);
        }

        /// <summary>
        /// Sends an Active Sensing message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiActiveSensing(string deviceId, int group)
        {
            sendMidiActiveSensing(deviceId);
            sendBleMidiActiveSensing(deviceId);
        }

        /// <summary>
        /// Sends a Reset message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiReset(string deviceId, int group)
        {
            sendMidiReset(deviceId);
            sendBleMidiReset(deviceId);
        }
    }
}
#endif
