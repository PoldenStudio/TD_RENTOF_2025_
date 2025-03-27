#if UNITY_WSA || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using jp.kshoji.unity.midi.win32;
#else
using jp.kshoji.unity.midi.uwp;
#endif

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MIDI Plugin for UWP, Windows
    /// </summary>
    public class WindowsMidiPlugin : IMidiPlugin
    {
        private void MidiPlugin_OnMidiInputDeviceAttached(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiInputDeviceAttached((string)o), deviceId);

        private void MidiPlugin_OnMidiInputDeviceDetached(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiInputDeviceDetached((string)o), deviceId);

        private void MidiPlugin_OnMidiOutputDeviceAttached(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiOutputDeviceAttached((string)o), deviceId);

        private void MidiPlugin_OnMidiOutputDeviceDetached(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiOutputDeviceDetached((string)o), deviceId);

        private void MidiPlugin_OnMidiNoteOn(string deviceId, byte channel, byte note, byte velocity) =>
            MidiManager.Instance.asyncOperation.Post(
                o => MidiManager.Instance.OnMidiNoteOn((string)((object[])o)[0], 0, (byte)((object[])o)[1], (byte)((object[])o)[2],
                    (byte)((object[])o)[3]), new object[] { deviceId, channel, note, velocity });

        private void MidiPlugin_OnMidiNoteOff(string deviceId, byte channel, byte note, byte velocity) =>
            MidiManager.Instance.asyncOperation.Post(
                o => MidiManager.Instance.OnMidiNoteOff((string)((object[])o)[0], 0, (byte)((object[])o)[1], (byte)((object[])o)[2],
                    (byte)((object[])o)[3]), new object[] { deviceId, channel, note, velocity });

        private void MidiPlugin_OnMidiPolyphonicKeyPressure(string deviceId, byte channel, byte note, byte velocity) =>
            MidiManager.Instance.asyncOperation.Post(
                o => MidiManager.Instance.OnMidiPolyphonicAftertouch((string)((object[])o)[0], 0, (byte)((object[])o)[1],
                    (byte)((object[])o)[2], (byte)((object[])o)[3]),
                new object[] { deviceId, channel, note, velocity });

        private void
            MidiPlugin_OnMidiControlChange(string deviceId, byte channel, byte controller, byte controllerValue) =>
            MidiManager.Instance.asyncOperation.Post(
                o => MidiManager.Instance.OnMidiControlChange((string)((object[])o)[0], 0, (byte)((object[])o)[1], (byte)((object[])o)[2],
                    (byte)((object[])o)[3]), new object[] { deviceId, channel, controller, controllerValue });

        private void MidiPlugin_OnMidiProgramChange(string deviceId, byte channel, byte program) => MidiManager.Instance.asyncOperation.Post(
            o => MidiManager.Instance.OnMidiProgramChange((string)((object[])o)[0], 0, (byte)((object[])o)[1], (byte)((object[])o)[2]),
            new object[] { deviceId, channel, program });

        private void MidiPlugin_OnMidiChannelPressure(string deviceId, byte channel, byte pressure) =>
            MidiManager.Instance.asyncOperation.Post(
                o => MidiManager.Instance.OnMidiChannelAftertouch((string)((object[])o)[0], 0, (byte)((object[])o)[1],
                    (byte)((object[])o)[2]), new object[] { deviceId, channel, pressure });

        private void MidiPlugin_OnMidiPitchBendChange(string deviceId, byte channel, ushort bend) =>
            MidiManager.Instance.asyncOperation.Post(
                o => MidiManager.Instance.OnMidiPitchWheel((string)((object[])o)[0], 0, (byte)((object[])o)[1], (ushort)((object[])o)[2]),
                new object[] { deviceId, channel, bend });

        private void MidiPlugin_OnMidiSystemExclusive(string deviceId, byte[] systemExclusive) => MidiManager.Instance.asyncOperation.Post(
            o => MidiManager.Instance.OnMidiSystemExclusive((string)((object[])o)[0], 0, (byte[])((object[])o)[1]),
            new object[] { deviceId, systemExclusive });

        private void MidiPlugin_OnMidiTimeCode(string deviceId, byte frameType, byte values) => MidiManager.Instance.asyncOperation.Post(
            o => MidiManager.Instance.OnMidiTimeCodeQuarterFrame((string)((object[])o)[0], 0,
                (((byte)((object[])o)[1] & 0x7) << 4) | ((byte)((object[])o)[2] & 0xf)),
            new object[] { deviceId, frameType, values });

        private void MidiPlugin_OnMidiSongPositionPointer(string deviceId, ushort beats) => MidiManager.Instance.asyncOperation.Post(
            o => MidiManager.Instance.OnMidiSongPositionPointer((string)((object[])o)[0], 0, (ushort)((object[])o)[1]),
            new object[] { deviceId, beats });

        private void MidiPlugin_OnMidiSongSelect(string deviceId, byte song) => MidiManager.Instance.asyncOperation.Post(
            o => MidiManager.Instance.OnMidiSongSelect((string)((object[])o)[0], 0, (byte)((object[])o)[1]),
            new object[] { deviceId, song });

        private void MidiPlugin_OnMidiTuneRequest(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTuneRequest((string)o, 0), deviceId);

        private void MidiPlugin_OnMidiTimingClock(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTimingClock((string)o, 0), deviceId);

        private void MidiPlugin_OnMidiStart(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiStart((string)o, 0), deviceId);

        private void MidiPlugin_OnMidiContinue(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiContinue((string)o, 0), deviceId);

        private void MidiPlugin_OnMidiStop(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiStop((string)o, 0), deviceId);

        private void MidiPlugin_OnMidiActiveSensing(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiActiveSensing((string)o, 0), deviceId);

        private void MidiPlugin_OnMidiSystemReset(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiReset((string)o, 0), deviceId);

        /// <summary>
        /// Initializes MIDI Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
        public void InitializeMidi(Action initializeCompletedAction)
        {
            MidiPlugin.Instance.OnMidiInputDeviceAttached += MidiPlugin_OnMidiInputDeviceAttached;
            MidiPlugin.Instance.OnMidiInputDeviceDetached += MidiPlugin_OnMidiInputDeviceDetached;
            MidiPlugin.Instance.OnMidiOutputDeviceAttached += MidiPlugin_OnMidiOutputDeviceAttached;
            MidiPlugin.Instance.OnMidiOutputDeviceDetached += MidiPlugin_OnMidiOutputDeviceDetached;

            MidiPlugin.Instance.OnMidiNoteOn += MidiPlugin_OnMidiNoteOn;
            MidiPlugin.Instance.OnMidiNoteOff += MidiPlugin_OnMidiNoteOff;
            MidiPlugin.Instance.OnMidiPolyphonicKeyPressure += MidiPlugin_OnMidiPolyphonicKeyPressure;
            MidiPlugin.Instance.OnMidiControlChange += MidiPlugin_OnMidiControlChange;
            MidiPlugin.Instance.OnMidiProgramChange += MidiPlugin_OnMidiProgramChange;
            MidiPlugin.Instance.OnMidiChannelPressure += MidiPlugin_OnMidiChannelPressure;
            MidiPlugin.Instance.OnMidiPitchBendChange += MidiPlugin_OnMidiPitchBendChange;
            MidiPlugin.Instance.OnMidiSystemExclusive += MidiPlugin_OnMidiSystemExclusive;
            MidiPlugin.Instance.OnMidiTimeCode += MidiPlugin_OnMidiTimeCode;
            MidiPlugin.Instance.OnMidiSongPositionPointer += MidiPlugin_OnMidiSongPositionPointer;
            MidiPlugin.Instance.OnMidiSongSelect += MidiPlugin_OnMidiSongSelect;
            MidiPlugin.Instance.OnMidiTuneRequest += MidiPlugin_OnMidiTuneRequest;
            MidiPlugin.Instance.OnMidiTimingClock += MidiPlugin_OnMidiTimingClock;
            MidiPlugin.Instance.OnMidiStart += MidiPlugin_OnMidiStart;
            MidiPlugin.Instance.OnMidiContinue += MidiPlugin_OnMidiContinue;
            MidiPlugin.Instance.OnMidiStop += MidiPlugin_OnMidiStop;
            MidiPlugin.Instance.OnMidiActiveSensing += MidiPlugin_OnMidiActiveSensing;
            MidiPlugin.Instance.OnMidiSystemReset += MidiPlugin_OnMidiSystemReset;

            initializeCompletedAction?.Invoke();
        }

        /// <summary>
        /// Terminates MIDI Plugin system
        /// </summary>
        public void TerminateMidi()
        {
            MidiPlugin.Instance.OnMidiInputDeviceAttached -= MidiPlugin_OnMidiInputDeviceAttached;
            MidiPlugin.Instance.OnMidiInputDeviceDetached -= MidiPlugin_OnMidiInputDeviceDetached;
            MidiPlugin.Instance.OnMidiOutputDeviceAttached -= MidiPlugin_OnMidiOutputDeviceAttached;
            MidiPlugin.Instance.OnMidiOutputDeviceDetached -= MidiPlugin_OnMidiOutputDeviceDetached;

            MidiPlugin.Instance.OnMidiNoteOn -= MidiPlugin_OnMidiNoteOn;
            MidiPlugin.Instance.OnMidiNoteOff -= MidiPlugin_OnMidiNoteOff;
            MidiPlugin.Instance.OnMidiPolyphonicKeyPressure -= MidiPlugin_OnMidiPolyphonicKeyPressure;
            MidiPlugin.Instance.OnMidiControlChange -= MidiPlugin_OnMidiControlChange;
            MidiPlugin.Instance.OnMidiProgramChange -= MidiPlugin_OnMidiProgramChange;
            MidiPlugin.Instance.OnMidiChannelPressure -= MidiPlugin_OnMidiChannelPressure;
            MidiPlugin.Instance.OnMidiPitchBendChange -= MidiPlugin_OnMidiPitchBendChange;
            MidiPlugin.Instance.OnMidiSystemExclusive -= MidiPlugin_OnMidiSystemExclusive;
            MidiPlugin.Instance.OnMidiTimeCode -= MidiPlugin_OnMidiTimeCode;
            MidiPlugin.Instance.OnMidiSongPositionPointer -= MidiPlugin_OnMidiSongPositionPointer;
            MidiPlugin.Instance.OnMidiSongSelect -= MidiPlugin_OnMidiSongSelect;
            MidiPlugin.Instance.OnMidiTuneRequest -= MidiPlugin_OnMidiTuneRequest;
            MidiPlugin.Instance.OnMidiTimingClock -= MidiPlugin_OnMidiTimingClock;
            MidiPlugin.Instance.OnMidiStart -= MidiPlugin_OnMidiStart;
            MidiPlugin.Instance.OnMidiContinue -= MidiPlugin_OnMidiContinue;
            MidiPlugin.Instance.OnMidiStop -= MidiPlugin_OnMidiStop;
            MidiPlugin.Instance.OnMidiActiveSensing -= MidiPlugin_OnMidiActiveSensing;
            MidiPlugin.Instance.OnMidiSystemReset -= MidiPlugin_OnMidiSystemReset;
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
                MidiPlugin.Instance.Stop();
            }
            else if (stateChange == PlayModeStateChange.EnteredPlayMode)
            {
                MidiPlugin.Instance.Start();
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
            var deviceName = MidiPlugin.Instance.GetDeviceName(deviceId);
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
            var deviceName = MidiPlugin.Instance.GetVendorId(deviceId);
            if (!string.IsNullOrEmpty(deviceName))
            {
                return deviceName;
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
            var deviceName = MidiPlugin.Instance.GetProductId(deviceId);
            if (!string.IsNullOrEmpty(deviceName))
            {
                return deviceName;
            }

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
            => MidiPlugin.Instance.SendMidiNoteOn(deviceId, (byte)channel, (byte)note, (byte)velocity);

        /// <summary>
        /// Sends a Note Off message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        public void SendMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
            => MidiPlugin.Instance.SendMidiNoteOff(deviceId, (byte)channel, (byte)note, (byte)velocity);

        /// <summary>
        /// Sends a Polyphonic Aftertouch message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="note">0-127</param>
        /// <param name="pressure">0-127</param>
        public void SendMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
            => MidiPlugin.Instance.SendMidiPolyphonicKeyPressure(deviceId, (byte)channel, (byte)note, (byte)pressure);

        /// <summary>
        /// Sends a Control Change message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="function">0-127</param>
        /// <param name="value">0-127</param>
        public void SendMidiControlChange(string deviceId, int group, int channel, int function, int value)
            => MidiPlugin.Instance.SendMidiControlChange(deviceId, (byte)channel, (byte)function, (byte)value);

        /// <summary>
        /// Sends a Program Change message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="program">0-127</param>
        public void SendMidiProgramChange(string deviceId, int group, int channel, int program)
            => MidiPlugin.Instance.SendMidiProgramChange(deviceId, (byte)channel, (byte)program);

        /// <summary>
        /// Sends a Channel Aftertouch message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="pressure">0-127</param>
        public void SendMidiChannelAftertouch(string deviceId, int group, int channel, int pressure)
            => MidiPlugin.Instance.SendMidiChannelPressure(deviceId, (byte)channel, (byte)pressure);

        /// <summary>
        /// Sends a Pitch Wheel message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="channel">0-15</param>
        /// <param name="amount">0-16383</param>
        public void SendMidiPitchWheel(string deviceId, int group, int channel, int amount)
            => MidiPlugin.Instance.SendMidiPitchBendChange(deviceId, (byte)channel, (ushort)amount);

        /// <summary>
        /// Sends a System Exclusive message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="sysEx">byte array starts with F0, ends with F7</param>
        public void SendMidiSystemExclusive(string deviceId, int group, byte[] sysEx)
            => MidiPlugin.Instance.SendMidiSystemExclusive(deviceId, sysEx);

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        public void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
            => MidiPlugin.Instance.SendMidiTimeCode(deviceId, (byte)(timing >> 4), (byte)(timing & 0xf));

        /// <summary>
        /// Sends a Song Select message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="song">0-127</param>
        public void SendMidiSongSelect(string deviceId, int group, int song)
            => MidiPlugin.Instance.SendMidiSongSelect(deviceId, (byte)song);

        /// <summary>
        /// Sends a Song Position Pointer message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="position">0-16383</param>
        public void SendMidiSongPositionPointer(string deviceId, int group, int position)
            => MidiPlugin.Instance.SendMidiSongPositionPointer(deviceId, (ushort)position);

        /// <summary>
        /// Sends a Tune Request message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTuneRequest(string deviceId, int group)
            => MidiPlugin.Instance.SendMidiTuneRequest(deviceId);

        /// <summary>
        /// Sends a Timing Clock message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTimingClock(string deviceId, int group)
            => MidiPlugin.Instance.SendMidiTimingClock(deviceId);

        /// <summary>
        /// Sends a Start message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStart(string deviceId, int group)
            => MidiPlugin.Instance.SendMidiStart(deviceId);

        /// <summary>
        /// Sends a Continue message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiContinue(string deviceId, int group)
            => MidiPlugin.Instance.SendMidiContinue(deviceId);

        /// <summary>
        /// Sends a Stop message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStop(string deviceId, int group)
            => MidiPlugin.Instance.SendMidiStop(deviceId);

        /// <summary>
        /// Sends an Active Sensing message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiActiveSensing(string deviceId, int group)
            => MidiPlugin.Instance.SendMidiActiveSensing(deviceId);

        /// <summary>
        /// Sends a Reset message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiReset(string deviceId, int group)
            => MidiPlugin.Instance.SendMidiSystemReset(deviceId);
    }
}
#endif
