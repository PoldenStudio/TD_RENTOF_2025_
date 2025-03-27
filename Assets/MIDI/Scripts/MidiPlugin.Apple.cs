#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MIDI Plugin for iOS, macOS
    /// </summary>
    public class AppleMidiPlugin : IMidiPlugin
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string DllName = "MIDIPlugin";
#else
        private const string DllName = "__Internal";
#endif

        [DllImport(DllName)]
        private static extern void midiPluginInitialize();

        [DllImport(DllName)]
        private static extern void midiPluginTerminate();

        [DllImport(DllName)]
        private static extern void sendMidiData(string deviceId, byte[] byteArray, int length);

        [DllImport(DllName)]
        private static extern string getDeviceName(string deviceId);

        [DllImport(DllName)]
        private static extern string getVendorId(string deviceId);

        [DllImport(DllName)]
        private static extern string getProductId(string deviceId);

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [DllImport(DllName)]
        private static extern void midiPluginStartForEditor();

        [DllImport(DllName)]
        private static extern void midiPluginStopForEditor();
#else
        [DllImport(DllName)]
        private static extern void startScanBluetoothMidiDevices();

        [DllImport(DllName)]
        private static extern void stopScanBluetoothMidiDevices();
#endif

        [AOT.MonoPInvokeCallback(typeof(OnMidiInputDeviceAttachedDelegate))]
        private static void OnMidiInputDeviceAttached(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiInputDeviceAttached((string)o), deviceId);
        [AOT.MonoPInvokeCallback(typeof(OnMidiOutputDeviceAttachedDelegate))]
        private static void OnMidiOutputDeviceAttached(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiOutputDeviceAttached((string)o), deviceId);
        [AOT.MonoPInvokeCallback(typeof(OnMidiInputDeviceDetachedDelegate))]
        private static void OnMidiInputDeviceDetached(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiInputDeviceDetached((string)o), deviceId);
        [AOT.MonoPInvokeCallback(typeof(OnMidiOutputDeviceDetachedDelegate))]
        private static void OnMidiOutputDeviceDetached(string deviceId) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiOutputDeviceDetached((string)o), deviceId);

        [AOT.MonoPInvokeCallback(typeof(OnMidiNoteOnDelegate))]
        private static void OnMidiNoteOn(string deviceId, int group, int channel, int note, int velocity) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiNoteOn((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3], (int)((object[])o)[4]), new object[] {deviceId, group, channel, note, velocity});
        [AOT.MonoPInvokeCallback(typeof(OnMidiNoteOffDelegate))]
        private static void OnMidiNoteOff(string deviceId, int group, int channel, int note, int velocity) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiNoteOff((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3], (int)((object[])o)[4]), new object[] {deviceId, group, channel, note, velocity});
        [AOT.MonoPInvokeCallback(typeof(OnMidiPolyphonicAftertouchDelegate))]
        private static void OnMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiPolyphonicAftertouch((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3], (int)((object[])o)[4]), new object[] {deviceId, group, channel, note, pressure});
        [AOT.MonoPInvokeCallback(typeof(OnMidiControlChangeDelegate))]
        private static void OnMidiControlChange(string deviceId, int group, int channel, int function, int value) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiControlChange((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3], (int)((object[])o)[4]), new object[] {deviceId, group, channel, function, value});
        [AOT.MonoPInvokeCallback(typeof(OnMidiProgramChangeDelegate))]
        private static void OnMidiProgramChange(string deviceId, int group, int channel, int program) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiProgramChange((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3]), new object[] {deviceId, group, channel, program});
        [AOT.MonoPInvokeCallback(typeof(OnMidiChannelAftertouchDelegate))]
        private static void OnMidiChannelAftertouch(string deviceId, int group, int channel, int pressure) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiChannelAftertouch((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3]), new object[] {deviceId, group, channel, pressure});
        [AOT.MonoPInvokeCallback(typeof(OnMidiPitchWheelDelegate))]
        private static void OnMidiPitchWheel(string deviceId, int group, int channel, int amount) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiPitchWheel((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3]), new object[] {deviceId, group, channel, amount});
        [AOT.MonoPInvokeCallback(typeof(OnMidiSystemExclusiveDelegate))]
        private static void OnMidiSystemExclusive(string deviceId, int group, IntPtr exclusive, int length) {
            var systemExclusive = new byte[length];
            Marshal.Copy(exclusive, systemExclusive, 0, length);
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiSystemExclusive((string)((object[])o)[0], (int)((object[])o)[1], (byte[])((object[])o)[2]), new object[] {deviceId, group, systemExclusive});
        }
        [AOT.MonoPInvokeCallback(typeof(OnMidiTimeCodeQuarterFrameDelegate))]
        private static void OnMidiTimeCodeQuarterFrame(string deviceId, int group, int timing) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTimeCodeQuarterFrame((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2]), new object[] {deviceId, group, timing});
        [AOT.MonoPInvokeCallback(typeof(OnMidiSongSelectDelegate))]
        private static void OnMidiSongSelect(string deviceId, int group, int song) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiSongSelect((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2]), new object[] {deviceId, group, song});
        [AOT.MonoPInvokeCallback(typeof(OnMidiSongPositionPointerDelegate))]
        private static void OnMidiSongPositionPointer(string deviceId, int group, int position) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiSongPositionPointer((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2]), new object[] {deviceId, group, position});
        [AOT.MonoPInvokeCallback(typeof(OnMidiTuneRequestDelegate))]
        private static void OnMidiTuneRequest(string deviceId, int group) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTuneRequest((string)((object[])o)[0], (int)((object[])o)[1]), new object[] {deviceId, group});
        [AOT.MonoPInvokeCallback(typeof(OnMidiTimingClockDelegate))]
        private static void OnMidiTimingClock(string deviceId, int group) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTimingClock((string)((object[])o)[0], (int)((object[])o)[1]), new object[] {deviceId, group});
        [AOT.MonoPInvokeCallback(typeof(OnMidiStartDelegate))]
        private static void OnMidiStart(string deviceId, int group) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiStart((string)((object[])o)[0], (int)((object[])o)[1]), new object[] {deviceId, group});
        [AOT.MonoPInvokeCallback(typeof(OnMidiContinueDelegate))]
        private static void OnMidiContinue(string deviceId, int group) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiContinue((string)((object[])o)[0], (int)((object[])o)[1]), new object[] {deviceId, group});
        [AOT.MonoPInvokeCallback(typeof(OnMidiStopDelegate))]
        private static void OnMidiStop(string deviceId, int group) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiStop((string)((object[])o)[0], (int)((object[])o)[1]), new object[] {deviceId, group});
        [AOT.MonoPInvokeCallback(typeof(OnMidiActiveSensingDelegate))]
        private static void OnMidiActiveSensing(string deviceId, int group) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiActiveSensing((string)((object[])o)[0], (int)((object[])o)[1]), new object[] {deviceId, group});
        [AOT.MonoPInvokeCallback(typeof(OnMidiResetDelegate))]
        private static void OnMidiReset(string deviceId, int group) =>
            MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiReset((string)((object[])o)[0], (int)((object[])o)[1]), new object[] {deviceId, group});

        private delegate void OnMidiInputDeviceAttachedDelegate(string deviceId);
        private delegate void OnMidiOutputDeviceAttachedDelegate(string deviceId);
        private delegate void OnMidiInputDeviceDetachedDelegate(string deviceId);
        private delegate void OnMidiOutputDeviceDetachedDelegate(string deviceId);
        private delegate void OnMidiNoteOnDelegate(string deviceId, int group, int channel, int note, int velocity);
        private delegate void OnMidiNoteOffDelegate(string deviceId, int group, int channel, int note, int velocity);
        private delegate void OnMidiPolyphonicAftertouchDelegate(string deviceId, int group, int channel, int note, int pressure);
        private delegate void OnMidiControlChangeDelegate(string deviceId, int group, int channel, int function, int value);
        private delegate void OnMidiProgramChangeDelegate(string deviceId, int group, int channel, int program);
        private delegate void OnMidiChannelAftertouchDelegate(string deviceId, int group, int channel, int pressure);
        private delegate void OnMidiPitchWheelDelegate(string deviceId, int group, int channel, int amount);
        private delegate void OnMidiSystemExclusiveDelegate(string deviceId, int group, IntPtr systemExclusive, int length);
        private delegate void OnMidiTimeCodeQuarterFrameDelegate(string deviceId, int group, int timing);
        private delegate void OnMidiSongSelectDelegate(string deviceId, int group, int song);
        private delegate void OnMidiSongPositionPointerDelegate(string deviceId, int group, int position);
        private delegate void OnMidiTuneRequestDelegate(string deviceId, int group);
        private delegate void OnMidiTimingClockDelegate(string deviceId, int group);
        private delegate void OnMidiStartDelegate(string deviceId, int group);
        private delegate void OnMidiContinueDelegate(string deviceId, int group);
        private delegate void OnMidiStopDelegate(string deviceId, int group);
        private delegate void OnMidiActiveSensingDelegate(string deviceId, int group);
        private delegate void OnMidiResetDelegate(string deviceId, int group);

        [DllImport(DllName)]
        private static extern void SetMidiInputDeviceAttachedCallback(OnMidiInputDeviceAttachedDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiOutputDeviceAttachedCallback(OnMidiOutputDeviceAttachedDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiInputDeviceDetachedCallback(OnMidiInputDeviceDetachedDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiOutputDeviceDetachedCallback(OnMidiOutputDeviceDetachedDelegate callback);

        [DllImport(DllName)]
        private static extern void SetMidiNoteOnCallback(OnMidiNoteOnDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiNoteOffCallback(OnMidiNoteOffDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiPolyphonicAftertouchDelegate(OnMidiPolyphonicAftertouchDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiControlChangeDelegate(OnMidiControlChangeDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiProgramChangeDelegate(OnMidiProgramChangeDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiChannelAftertouchDelegate(OnMidiChannelAftertouchDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiPitchWheelDelegate(OnMidiPitchWheelDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiSystemExclusiveDelegate(OnMidiSystemExclusiveDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiTimeCodeQuarterFrameDelegate(OnMidiTimeCodeQuarterFrameDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiSongSelectDelegate(OnMidiSongSelectDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiSongPositionPointerDelegate(OnMidiSongPositionPointerDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiTuneRequestDelegate(OnMidiTuneRequestDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiTimingClockDelegate(OnMidiTimingClockDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiStartDelegate(OnMidiStartDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiContinueDelegate(OnMidiContinueDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiStopDelegate(OnMidiStopDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiActiveSensingDelegate(OnMidiActiveSensingDelegate callback);
        [DllImport(DllName)]
        private static extern void SetMidiResetDelegate(OnMidiResetDelegate callback);

        /// <summary>
        /// Initializes MIDI Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
        public void InitializeMidi(Action initializeCompletedAction)
        {
            SetMidiInputDeviceAttachedCallback(OnMidiInputDeviceAttached);
            SetMidiOutputDeviceAttachedCallback(OnMidiOutputDeviceAttached);
            SetMidiInputDeviceDetachedCallback(OnMidiInputDeviceDetached);
            SetMidiOutputDeviceDetachedCallback(OnMidiOutputDeviceDetached);

            SetMidiNoteOnCallback(OnMidiNoteOn);
            SetMidiNoteOffCallback(OnMidiNoteOff);
            SetMidiPolyphonicAftertouchDelegate(OnMidiPolyphonicAftertouch);
            SetMidiControlChangeDelegate(OnMidiControlChange);
            SetMidiProgramChangeDelegate(OnMidiProgramChange);
            SetMidiChannelAftertouchDelegate(OnMidiChannelAftertouch);
            SetMidiPitchWheelDelegate(OnMidiPitchWheel);
            SetMidiSystemExclusiveDelegate(OnMidiSystemExclusive);
            SetMidiTimeCodeQuarterFrameDelegate(OnMidiTimeCodeQuarterFrame);
            SetMidiSongSelectDelegate(OnMidiSongSelect);
            SetMidiSongPositionPointerDelegate(OnMidiSongPositionPointer);
            SetMidiTuneRequestDelegate(OnMidiTuneRequest);
            SetMidiTimingClockDelegate(OnMidiTimingClock);
            SetMidiStartDelegate(OnMidiStart);
            SetMidiContinueDelegate(OnMidiContinue);
            SetMidiStopDelegate(OnMidiStop);
            SetMidiActiveSensingDelegate(OnMidiActiveSensing);
            SetMidiResetDelegate(OnMidiReset);
            
#if UNITY_EDITOR_OSX
            midiPluginStartForEditor();
#endif
            midiPluginInitialize();

            initializeCompletedAction?.Invoke();
        }

        /// <summary>
        /// Terminates MIDI Plugin system
        /// </summary>
        public void TerminateMidi()
        {
#if UNITY_EDITOR_OSX
            midiPluginStopForEditor();
#else
            midiPluginTerminate();
#endif
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
        /// for Android / iOS devices only
        /// </summary>
        /// <param name="timeout">timeout milliseconds, 0 : no timeout</param>
        public void StartScanBluetoothMidiDevices(int timeout)
        {
#if UNITY_IOS
            startScanBluetoothMidiDevices();
#endif
        }

        /// <summary>
        /// Stops to scan BLE MIDI devices
        /// for Android / iOS devices only
        /// </summary>
        public void StopScanBluetoothMidiDevices()
        {
#if UNITY_IOS
            stopScanBluetoothMidiDevices();
#endif
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

            return string.Empty;
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

            return string.Empty;
        }

        /// <summary>
        /// Obtains device product id for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetProductId(string deviceId)
        {
            var productId = getProductId(deviceId);
            if (!string.IsNullOrEmpty(productId))
            {
                return productId;
            }

            return string.Empty;
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
            sendMidiData(deviceId, new[] {(byte) (0x90 | channel), (byte) note, (byte) velocity}, 3);
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
            sendMidiData(deviceId, new[] {(byte) (0x80 | channel), (byte) note, (byte) velocity}, 3);
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
            sendMidiData(deviceId, new[] {(byte) (0xa0 | channel), (byte) note, (byte) pressure}, 3);
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
            sendMidiData(deviceId, new[] {(byte) (0xb0 | channel), (byte) function, (byte) value}, 3);
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
            sendMidiData(deviceId, new[] {(byte) (0xc0 | channel), (byte) program}, 2);
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
            sendMidiData(deviceId, new[] {(byte) (0xd0 | channel), (byte) pressure}, 2);
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
            sendMidiData(deviceId, new[] {(byte) (0xe0 | channel), (byte) (amount & 0x7f), (byte) ((amount >> 7) & 0x7f)}, 3);
        }

        /// <summary>
        /// Sends a System Exclusive message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="sysEx">byte array starts with F0, ends with F7</param>
        public void SendMidiSystemExclusive(string deviceId, int group, byte[] sysEx)
        {
            sendMidiData(deviceId, sysEx, sysEx.Length);
        }

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        public void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
        {
            sendMidiData(deviceId, new[] {(byte) 0xf1, (byte) timing}, 2);
        }

        /// <summary>
        /// Sends a Song Select message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="song">0-127</param>
        public void SendMidiSongSelect(string deviceId, int group, int song)
        {
            sendMidiData(deviceId, new[] {(byte) 0xf3, (byte) song}, 2);
        }

        /// <summary>
        /// Sends a Song Position Pointer message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="position">0-16383</param>
        public void SendMidiSongPositionPointer(string deviceId, int group, int position)
        {
            sendMidiData(deviceId, new[] {(byte) 0xf2, (byte) (position & 0x7f), (byte) ((position >> 7) & 0x7f)}, 3);
        }

        /// <summary>
        /// Sends a Tune Request message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTuneRequest(string deviceId, int group)
        {
            sendMidiData(deviceId, new[] {(byte) 0xf6}, 1);
        }

        /// <summary>
        /// Sends a Timing Clock message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTimingClock(string deviceId, int group)
        {
            sendMidiData(deviceId, new[] {(byte) 0xf8}, 1);
        }

        /// <summary>
        /// Sends a Start message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStart(string deviceId, int group)
        {
            sendMidiData(deviceId, new[] {(byte) 0xfa}, 1);
        }

        /// <summary>
        /// Sends a Continue message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiContinue(string deviceId, int group)
        {
            sendMidiData(deviceId, new[] {(byte) 0xfb}, 1);
        }

        /// <summary>
        /// Sends a Stop message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStop(string deviceId, int group)
        {
            sendMidiData(deviceId, new[] {(byte) 0xfc}, 1);
        }

        /// <summary>
        /// Sends an Active Sensing message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiActiveSensing(string deviceId, int group)
        {
            sendMidiData(deviceId, new[] {(byte) 0xfe}, 1);
        }

        /// <summary>
        /// Sends a Reset message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiReset(string deviceId, int group)
        {
            sendMidiData(deviceId, new[] {(byte) 0xff}, 1);
        }
    }
}
#endif
