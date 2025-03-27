#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MIDI Plugin for Linux
    /// </summary>
    public class LinuxMidiPlugin : IMidiPlugin
    {
        private delegate void OnSendMessageDelegate(string method, string message);

        [DllImport("MIDIPlugin")]
        private static extern void SetSendMessageCallback(OnSendMessageDelegate callback);

        [AOT.MonoPInvokeCallback(typeof(OnSendMessageDelegate))]
        private static void LinuxOnSendMessageDelegate(string method, string message) =>
            MidiManager.Instance.asyncOperation.Post(o => {
                if (MidiManager.Instance != null) {
                    MidiManager.Instance.gameObject.SendMessage((string)((object[])o)[0], (string)((object[])o)[1]);
                }
            }, new object[] {method, message});

        [DllImport("MIDIPlugin")]
        private static extern void InitializeMidiLinux();

        [DllImport("MIDIPlugin")]
        private static extern void TerminateMidiLinux();

        [DllImport("MIDIPlugin")]
        private static extern string GetDeviceNameLinux(string deviceId);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiNoteOff(string deviceId, byte channel, byte note, byte velocity);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiNoteOn(string deviceId, byte channel, byte note, byte velocity);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiPolyphonicAftertouch(string deviceId, byte channel, byte note, byte pressure);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiControlChange(string deviceId, byte channel, byte func, byte value);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiProgramChange(string deviceId, byte channel, byte program);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiChannelAftertouch(string deviceId, byte channel, byte pressure);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiPitchWheel(string deviceId, byte channel, short amount);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiSystemExclusive(string deviceId, byte[] data, int length);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiTimeCodeQuarterFrame(string deviceId, byte value);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiSongPositionPointer(string deviceId, short position);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiSongSelect(string deviceId, byte song);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiTuneRequest(string deviceId);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiTimingClock(string deviceId);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiStart(string deviceId);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiContinue(string deviceId);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiStop(string deviceId);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiActiveSensing(string deviceId);

        [DllImport("MIDIPlugin")]
        private static extern void SendMidiReset(string deviceId);

        /// <summary>
        /// Initializes MIDI Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
        public void InitializeMidi(Action initializeCompletedAction)
        {
            SetSendMessageCallback(LinuxOnSendMessageDelegate);
            InitializeMidiLinux();
            initializeCompletedAction?.Invoke();
        }

        /// <summary>
        /// Terminates MIDI Plugin system
        /// </summary>
        public void TerminateMidi()
        {
            TerminateMidiLinux();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Called when Unity Editor play mode changed
        /// </summary>
        /// <param name="stateChange"></param>
        public void PlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                TerminateMidiLinux();
            }
            else if (stateChange == PlayModeStateChange.EnteredPlayMode)
            {
                InitializeMidiLinux();
            }
        }
#endif

        /// <summary>
        /// Obtains device name for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetDeviceName(string deviceId)
        {
            var deviceName = GetDeviceNameLinux(deviceId);
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
            // not supported for linux
            return null;
        }

        /// <summary>
        /// Obtains device product id for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetProductId(string deviceId)
        {
            // not supported for linux
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
            => SendMidiNoteOn(deviceId, (byte)channel, (byte)note, (byte)velocity);

        /// <summary>
        /// Sends a Note Off message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        public void SendMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
            => SendMidiNoteOff(deviceId, (byte)channel, (byte)note, (byte)velocity);

        /// <summary>
        /// Sends a Polyphonic Aftertouch message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="pressure">0-127</param>
        public void SendMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
            => SendMidiPolyphonicAftertouch(deviceId, (byte)channel, (byte)note, (byte)pressure);

        /// <summary>
        /// Sends a Control Change message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="function">0-127</param>
        /// <param name="value">0-127</param>
        public void SendMidiControlChange(string deviceId, int group, int channel, int function, int value)
            => SendMidiControlChange(deviceId, (byte)channel, (byte)function, (byte)value);

        /// <summary>
        /// Sends a Program Change message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="program">0-127</param>
        public void SendMidiProgramChange(string deviceId, int group, int channel, int program)
            => SendMidiProgramChange(deviceId, (byte)channel, (byte)program);

        /// <summary>
        /// Sends a Channel Aftertouch message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="pressure">0-127</param>
        public void SendMidiChannelAftertouch(string deviceId, int group, int channel, int pressure)
            => SendMidiChannelAftertouch(deviceId, (byte)channel, (byte)pressure);

        /// <summary>
        /// Sends a Pitch Wheel message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="amount">0-16383</param>
        public void SendMidiPitchWheel(string deviceId, int group, int channel, int amount)
            => SendMidiPitchWheel(deviceId, (byte)channel, (short)amount);

        /// <summary>
        /// Sends a System Exclusive message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="sysEx">byte array starts with F0, ends with F7</param>
        public void SendMidiSystemExclusive(string deviceId, int group, byte[] sysEx)
            => SendMidiSystemExclusive(deviceId, sysEx, sysEx.Length);

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        public void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
            => SendMidiTimeCodeQuarterFrame(deviceId, (byte)timing);

        /// <summary>
        /// Sends a Song Select message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="song">0-127</param>
        public void SendMidiSongSelect(string deviceId, int group, int song)
            => SendMidiSongSelect(deviceId, (byte)song);

        /// <summary>
        /// Sends a Song Position Pointer message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="position">0-16383</param>
        public void SendMidiSongPositionPointer(string deviceId, int group, int position)
            => SendMidiSongPositionPointer(deviceId, (short)position);

        /// <summary>
        /// Sends a Tune Request message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTuneRequest(string deviceId, int group)
            => SendMidiTuneRequest(deviceId);

        /// <summary>
        /// Sends a Timing Clock message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTimingClock(string deviceId, int group)
            => SendMidiTimingClock(deviceId);

        /// <summary>
        /// Sends a Start message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStart(string deviceId, int group)
            => SendMidiStart(deviceId);

        /// <summary>
        /// Sends a Continue message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiContinue(string deviceId, int group)
            => SendMidiContinue(deviceId);

        /// <summary>
        /// Sends a Stop message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStop(string deviceId, int group)
            => SendMidiStop(deviceId);

        /// <summary>
        /// Sends an Active Sensing message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiActiveSensing(string deviceId, int group)
            => SendMidiActiveSensing(deviceId);

        /// <summary>
        /// Sends a Reset message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiReset(string deviceId, int group)
            => SendMidiReset(deviceId);
    }
}
#endif
