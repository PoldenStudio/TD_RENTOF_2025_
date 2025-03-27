#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MIDI Plugin for Android
    /// </summary>
    public class AndroidMidiPlugin : IMidiPlugin
    {
        private static Thread mainThread;
        private AndroidJavaObject usbMidiPlugin;
        private AndroidJavaObject bleMidiPlugin;
        private AndroidJavaObject interAppMidiPlugin;

        public AndroidMidiPlugin()
        {
            mainThread = Thread.CurrentThread;

            try
            {
                usbMidiPlugin = new AndroidJavaObject("jp.kshoji.unity.midi.UsbMidiUnityPlugin");
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning(
                    $"Exception thrown while initialize Android USB MIDI. The USB MIDI feature is disabled. Message: {e.Message}");
            }

            try
            {
                bleMidiPlugin = new AndroidJavaObject("jp.kshoji.unity.midi.BleMidiUnityPlugin");
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning(
                    $"Exception thrown while initialize Android Blueooth MIDI. The Bluetooth MIDI feature is disabled. Message: {e.Message}");
            }

            try
            {
                interAppMidiPlugin = new AndroidJavaObject("jp.kshoji.interappmidi.InterAppMidiManager");
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning(
                    $"Exception thrown while initialize Android Inter-App MIDI. The Inter-App MIDI feature is disabled. Message: {e.Message}");
            }
        }

        private const string AndroidLocationPermission = "android.permission.ACCESS_FINE_LOCATION";
        private const string AndroidBluetoothPermission = "android.permission.BLUETOOTH";
        private const string AndroidBluetoothAdminPermission = "android.permission.BLUETOOTH_ADMIN";

        private const string AndroidBluetoothScanPermission = "android.permission.BLUETOOTH_SCAN";
        private const string AndroidBluetoothConnectPermission = "android.permission.BLUETOOTH_CONNECT";
        private const string AndroidBluetoothAdvertisePermission = "android.permission.BLUETOOTH_ADVERTISE";

        private bool androidBluetoothPermissionRequested;
        private bool androidBleMidiPluginInitialized;
        private Action onAndroidBluetoothInitialized;

        /// <summary>
        /// Check and request BLE MIDI permissions for Android M or later.
        /// If all permissions are granted, this method do nothing.
        /// </summary>
        void AndroidCheckBluetoothPermissions()
        {
            androidBluetoothPermissionRequested = false;
            if (!AndroidIsBluetoothEnabled())
            {
                return;
            }

            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }

            var osVersion = new AndroidJavaClass("android.os.Build$VERSION");
            var osVersionInt = osVersion.GetStatic<int>("SDK_INT");

            if (osVersionInt >= 23)
            {
                // Android M or later
                var requestPermissions = AndroidBluetoothRequiredPermissions(osVersionInt);
                if (requestPermissions.Count > 0)
                {
                    // need asking permission
                    androidBluetoothPermissionRequested = true;

                    var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    activity.Call("requestPermissions", requestPermissions.ToArray(), 0);
                }
            }

            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        private List<string> AndroidBluetoothRequiredPermissions(int osVersionInt)
        {
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var requestPermissions = new List<string>();

            if (osVersionInt >= 31)
            {
                // Android 12 or later
                if (activity.Call<int>("checkSelfPermission", AndroidLocationPermission) != 0)
                {
                    requestPermissions.Add(AndroidLocationPermission);
                }

                if (activity.Call<int>("checkSelfPermission", AndroidBluetoothScanPermission) != 0)
                {
                    requestPermissions.Add(AndroidBluetoothScanPermission);
                }

                if (activity.Call<int>("checkSelfPermission", AndroidBluetoothConnectPermission) != 0)
                {
                    requestPermissions.Add(AndroidBluetoothConnectPermission);
                }

                if (activity.Call<int>("checkSelfPermission", AndroidBluetoothAdvertisePermission) != 0)
                {
                    requestPermissions.Add(AndroidBluetoothAdvertisePermission);
                }
            }
            else if (osVersionInt >= 23)
            {
                // Before Android 12
                if (activity.Call<int>("checkSelfPermission", AndroidLocationPermission) != 0)
                {
                    requestPermissions.Add(AndroidLocationPermission);
                }

                if (activity.Call<int>("checkSelfPermission", AndroidBluetoothPermission) != 0)
                {
                    requestPermissions.Add(AndroidBluetoothPermission);
                }

                if (activity.Call<int>("checkSelfPermission", AndroidBluetoothAdminPermission) != 0)
                {
                    requestPermissions.Add(AndroidBluetoothAdminPermission);
                }
            }

            return requestPermissions;
        }

        private bool AndroidIsBluetoothEnabled()
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }

            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var context = activity.Call<AndroidJavaObject>("getApplicationContext");
            var bluetoothManager = context.Call<AndroidJavaObject>("getSystemService", "bluetooth");
            var bluetoothAdapter = bluetoothManager.Call<AndroidJavaObject>("getAdapter");
            if (bluetoothAdapter == null)
            {
                return false;
            }

            var isEnabled = bluetoothAdapter.Call<bool>("isEnabled");

            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }

            return isEnabled;
        }

        private void AndroidBleMidiPluginInitialize()
        {
            // NOTE: call AndroidJNI.AttachCurrentThread(); / AndroidJNI.DetachCurrentThread(); at the caller
            if (!androidBleMidiPluginInitialized)
            {
                if (bleMidiPlugin == null)
                {
                    return;
                }

                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
#if ENABLE_IL2CPP
                bleMidiPlugin.Call("initialize", activity, new BleMidiDeviceConnectionListener(), new BleMidiInputEventListener());
#else
                bleMidiPlugin.Call("initialize", activity);
#endif
                androidBleMidiPluginInitialized = true;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus && androidBluetoothPermissionRequested)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }

                var osVersion = new AndroidJavaClass("android.os.Build$VERSION");
                var osVersionInt = osVersion.GetStatic<int>("SDK_INT");
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }

                if (osVersionInt >= 23)
                {
                    // Android M or later
                    if (Thread.CurrentThread != mainThread)
                    {
                        AndroidJNI.AttachCurrentThread();
                    }

                    var requestPermissions = AndroidBluetoothRequiredPermissions(osVersionInt);
                    if (Thread.CurrentThread != mainThread)
                    {
                        AndroidJNI.DetachCurrentThread();
                    }

                    if (requestPermissions.Count == 0)
                    {
                        androidBluetoothPermissionRequested = false;
                        if (AndroidIsBluetoothEnabled())
                        {
                            // all permissions granted
                            if (Thread.CurrentThread != mainThread)
                            {
                                AndroidJNI.AttachCurrentThread();
                            }

                            AndroidBleMidiPluginInitialize();
                            if (Thread.CurrentThread != mainThread)
                            {
                                AndroidJNI.DetachCurrentThread();
                            }
                        }

                        onAndroidBluetoothInitialized?.Invoke();
                        onAndroidBluetoothInitialized = null;
                    }
                    else
                    {
                        Debug.Log(
                            $"These permissions are not granted: {string.Join(", ", requestPermissions)}. BLE MIDI function doesn't work.");
                    }
                }
                else
                {

                    onAndroidBluetoothInitialized?.Invoke();
                    onAndroidBluetoothInitialized = null;
                }
            }
        }

#if ENABLE_IL2CPP
        /// <summary>
        /// USB MIDI device connection listener
        /// </summary>
        internal class UsbMidiDeviceConnectionListener : AndroidJavaProxy
        {
            public UsbMidiDeviceConnectionListener() : base("jp.kshoji.unity.midi.OnUsbMidiDeviceConnectionListener")
            {
            }

            public void onMidiInputDeviceAttached(string midiInputDevice)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiInputDeviceAttached((string)o), midiInputDevice);

            public void onMidiOutputDeviceAttached(string midiOutputDevice)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiOutputDeviceAttached((string)o), midiOutputDevice);

            public void onMidiInputDeviceDetached(string midiInputDevice)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiInputDeviceDetached((string)o), midiInputDevice);

            public void onMidiOutputDeviceDetached(string midiOutputDevice)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiOutputDeviceDetached((string)o), midiOutputDevice);
        }

        /// <summary>
        /// USB MIDI Event listener
        /// </summary>
        internal class UsbMidiInputEventListener : AndroidJavaProxy
        {
            public UsbMidiInputEventListener() : base("jp.kshoji.unity.midi.OnUsbMidiInputEventListener")
            {
            }

            public void onMidiMiscellaneousFunctionCodes(string sender, int cable, int byte1, int byte2, int byte3)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiMiscellaneousFunctionCodes((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3], (int)((object[])o)[4]),
                    new object[] { sender, cable, byte1, byte2, byte3 });

            public void onMidiCableEvents(string sender, int cable, int byte1, int byte2, int byte3)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiCableEvents((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3], (int)((object[])o)[4]),
                    new object[] { sender, cable, byte1, byte2, byte3 });

            public void onMidiSystemCommonMessage(string sender, int cable, byte[] bytes)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSystemCommonMessage((string)((object[])o)[0], (int)((object[])o)[1],
                        (byte[])((object[])o)[2]), new object[] { sender, cable, bytes });

            public void onMidiSystemExclusive(string sender, int cable, byte[] systemExclusive)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSystemExclusive((string)((object[])o)[0], (int)((object[])o)[1],
                        (byte[])((object[])o)[2]), new object[] { sender, cable, systemExclusive });

            public void onMidiNoteOff(string sender, int cable, int channel, int note, int velocity)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiNoteOff((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2],
                        (int)((object[])o)[3], (int)((object[])o)[4]),
                    new object[] { sender, cable, channel, note, velocity });

            public void onMidiNoteOn(string sender, int cable, int channel, int note, int velocity)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiNoteOn((string)((object[])o)[0], (int)((object[])o)[1], (int)((object[])o)[2],
                        (int)((object[])o)[3], (int)((object[])o)[4]),
                    new object[] { sender, cable, channel, note, velocity });

            public void onMidiPolyphonicAftertouch(string sender, int cable, int channel, int note, int pressure)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiPolyphonicAftertouch((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3], (int)((object[])o)[4]),
                    new object[] { sender, cable, channel, note, pressure });

            public void onMidiControlChange(string sender, int cable, int channel, int function, int value)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiControlChange((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3], (int)((object[])o)[4]),
                    new object[] { sender, cable, channel, function, value });

            public void onMidiProgramChange(string sender, int cable, int channel, int program)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiProgramChange((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3]),
                    new object[] { sender, cable, channel, program });

            public void onMidiChannelAftertouch(string sender, int cable, int channel, int pressure)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiChannelAftertouch((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3]),
                    new object[] { sender, cable, channel, pressure });

            public void onMidiPitchWheel(string sender, int cable, int channel, int amount)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiPitchWheel((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3]), new object[] { sender, cable, channel, amount });

            public void onMidiSingleByte(string sender, int cable, int byte1)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSingleByte((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2]), new object[] { sender, cable, byte1 });

            public void onMidiTimeCodeQuarterFrame(string sender, int cable, int timing)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiTimeCodeQuarterFrame((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2]), new object[] { sender, cable, timing });

            public void onMidiSongSelect(string sender, int cable, int song)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSongSelect((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2]), new object[] { sender, cable, song });

            public void onMidiSongPositionPointer(string sender, int cable, int position)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSongPositionPointer((string)((object[])o)[0], (int)((object[])o)[1],
                        (int)((object[])o)[2]), new object[] { sender, cable, position });

            public void onMidiTuneRequest(string sender, int cable)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiTuneRequest((string)((object[])o)[0], (int)((object[])o)[1]),
                    new object[] { sender, cable });

            public void onMidiTimingClock(string sender, int cable)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiTimingClock((string)((object[])o)[0], (int)((object[])o)[1]),
                    new object[] { sender, cable });

            public void onMidiStart(string sender, int cable)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiStart((string)((object[])o)[0], (int)((object[])o)[1]),
                    new object[] { sender, cable });

            public void onMidiContinue(string sender, int cable)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiContinue((string)((object[])o)[0], (int)((object[])o)[1]),
                    new object[] { sender, cable });

            public void onMidiStop(string sender, int cable)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiStop((string)((object[])o)[0], (int)((object[])o)[1]),
                    new object[] { sender, cable });

            public void onMidiActiveSensing(string sender, int cable)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiActiveSensing((string)((object[])o)[0], (int)((object[])o)[1]),
                    new object[] { sender, cable });

            public void onMidiReset(string sender, int cable)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiReset((string)((object[])o)[0], (int)((object[])o)[1]),
                    new object[] { sender, cable });
        }

        /// <summary>
        /// Bluetooth LE MIDI device attached listener
        /// </summary>
        internal class BleMidiDeviceConnectionListener : AndroidJavaProxy
        {
            public BleMidiDeviceConnectionListener() : base("jp.kshoji.unity.midi.OnBleMidiDeviceConnectionListener")
            {
            }

            public void onMidiInputDeviceAttached(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiInputDeviceAttached((string)o), deviceId);

            public void onMidiOutputDeviceAttached(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiOutputDeviceAttached((string)o), deviceId);

            public void onMidiInputDeviceDetached(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiInputDeviceDetached((string)o), deviceId);

            public void onMidiOutputDeviceDetached(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiOutputDeviceDetached((string)o), deviceId);
        }

        /// <summary>
        /// Bluetooth LE MIDI Event listener
        /// </summary>
        internal class BleMidiInputEventListener : AndroidJavaProxy
        {
            public BleMidiInputEventListener() : base("jp.kshoji.unity.midi.OnBleMidiInputEventListener")
            {
            }

            public void onMidiSystemExclusive(string deviceId, byte[] systemExclusive)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSystemExclusive((string)((object[])o)[0], 0, (byte[])((object[])o)[1]),
                    new object[] { deviceId, systemExclusive });

            public void onMidiNoteOff(string deviceId, int channel, int note, int velocity)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiNoteOff((string)((object[])o)[0], 0, (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3]),
                    new object[] { deviceId, channel, note, velocity });

            public void onMidiNoteOn(string deviceId, int channel, int note, int velocity)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiNoteOn((string)((object[])o)[0], 0, (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3]),
                    new object[] { deviceId, channel, note, velocity });

            public void onMidiPolyphonicAftertouch(string deviceId, int channel, int note, int pressure)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiPolyphonicAftertouch((string)((object[])o)[0], 0, (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3]),
                    new object[] { deviceId, channel, note, pressure });

            public void onMidiControlChange(string deviceId, int channel, int function, int value)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiControlChange((string)((object[])o)[0], 0, (int)((object[])o)[1],
                        (int)((object[])o)[2], (int)((object[])o)[3]),
                    new object[] { deviceId, channel, function, value });

            public void onMidiProgramChange(string deviceId, int channel, int program)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiProgramChange((string)((object[])o)[0], 0, (int)((object[])o)[1],
                        (int)((object[])o)[2]), new object[] { deviceId, channel, program });

            public void onMidiChannelAftertouch(string deviceId, int channel, int pressure)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiChannelAftertouch((string)((object[])o)[0], 0, (int)((object[])o)[1],
                        (int)((object[])o)[2]), new object[] { deviceId, channel, pressure });

            public void onMidiPitchWheel(string deviceId, int channel, int amount)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiPitchWheel((string)((object[])o)[0], 0, (int)((object[])o)[1],
                        (int)((object[])o)[2]), new object[] { deviceId, channel, amount });

            public void onMidiSingleByte(string deviceId, int byte1)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSingleByte((string)((object[])o)[0], 0, (int)((object[])o)[1]),
                    new object[] { deviceId, byte1 });

            public void onMidiTimeCodeQuarterFrame(string deviceId, int timing)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiTimeCodeQuarterFrame((string)((object[])o)[0], 0, (int)((object[])o)[1]),
                    new object[] { deviceId, timing });

            public void onMidiSongSelect(string deviceId, int song)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSongSelect((string)((object[])o)[0], 0, (int)((object[])o)[1]),
                    new object[] { deviceId, song });

            public void onMidiSongPositionPointer(string deviceId, int position)
                => MidiManager.Instance.asyncOperation.Post(
                    o => MidiManager.Instance.OnMidiSongPositionPointer((string)((object[])o)[0], 0, (int)((object[])o)[1]),
                    new object[] { deviceId, position });

            public void onMidiTuneRequest(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTuneRequest((string)o, 0), deviceId);

            public void onMidiTimingClock(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTimingClock((string)o, 0), deviceId);

            public void onMidiStart(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiStart((string)o, 0), deviceId);

            public void onMidiContinue(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiContinue((string)o, 0), deviceId);

            public void onMidiStop(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiStop((string)o, 0), deviceId);

            public void onMidiActiveSensing(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiActiveSensing((string)o, 0), deviceId);

            public void onMidiReset(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiReset((string)o, 0), deviceId);
        }
#endif

        /// <summary>
        /// Initializes MIDI Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
        public void InitializeMidi(Action initializeCompletedAction)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }

            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var context = activity.Call<AndroidJavaObject>("getApplicationContext");
#if ENABLE_IL2CPP
            usbMidiPlugin?.Call("initialize", context, new UsbMidiDeviceConnectionListener(), new UsbMidiInputEventListener());
#else
            usbMidiPlugin?.Call("initialize", context);
#endif
            interAppMidiPlugin?.Call("initialize", activity);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }

            AndroidCheckBluetoothPermissions();
            if (androidBluetoothPermissionRequested)
            {
                onAndroidBluetoothInitialized = initializeCompletedAction;
            }
            else
            {
                if (AndroidIsBluetoothEnabled())
                {
                    if (Thread.CurrentThread != mainThread)
                    {
                        AndroidJNI.AttachCurrentThread();
                    }

                    AndroidBleMidiPluginInitialize();
                    if (Thread.CurrentThread != mainThread)
                    {
                        AndroidJNI.DetachCurrentThread();
                    }
                }

                initializeCompletedAction?.Invoke();
            }
        }

        /// <summary>
        /// Starts to scan BLE MIDI devices
        /// </summary>
        /// <param name="timeout">timeout milliseconds, 0 : no timeout</param>
        public void StartScanBluetoothMidiDevices(int timeout)
        {
            AndroidCheckBluetoothPermissions();
            if (androidBluetoothPermissionRequested)
            {
                onAndroidBluetoothInitialized = () =>
                {
                    if (AndroidIsBluetoothEnabled())
                    {
                        if (Thread.CurrentThread != mainThread)
                        {
                            AndroidJNI.AttachCurrentThread();
                        }

                        AndroidBleMidiPluginInitialize();
                        bleMidiPlugin.Call("startScanDevice", timeout);
                        if (Thread.CurrentThread != mainThread)
                        {
                            AndroidJNI.DetachCurrentThread();
                        }
                    }
                };
            }
            else
            {
                if (AndroidIsBluetoothEnabled())
                {
                    if (Thread.CurrentThread != mainThread)
                    {
                        AndroidJNI.AttachCurrentThread();
                    }

                    AndroidBleMidiPluginInitialize();
                    bleMidiPlugin.Call("startScanDevice", timeout);
                    if (Thread.CurrentThread != mainThread)
                    {
                        AndroidJNI.DetachCurrentThread();
                    }
                }
            }
        }

        /// <summary>
        /// Stops to scan BLE MIDI devices
        /// </summary>
        public void StopScanBluetoothMidiDevices()
        {
            if (AndroidIsBluetoothEnabled())
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }

                AndroidBleMidiPluginInitialize();
                bleMidiPlugin.Call("stopScanDevice");
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }
            }
        }

        /// <summary>
        /// Start to advertise BLE MIDI Peripheral device
        /// </summary>
        public void StartAdvertisingBluetoothMidiDevice()
        {
            AndroidCheckBluetoothPermissions();
            if (androidBluetoothPermissionRequested)
            {
                onAndroidBluetoothInitialized = () =>
                {
                    if (AndroidIsBluetoothEnabled())
                    {
                        if (Thread.CurrentThread != mainThread)
                        {
                            AndroidJNI.AttachCurrentThread();
                        }

                        AndroidBleMidiPluginInitialize();
                        bleMidiPlugin.Call("startAdvertising");
                        if (Thread.CurrentThread != mainThread)
                        {
                            AndroidJNI.DetachCurrentThread();
                        }
                    }
                };
            }
            else
            {
                if (AndroidIsBluetoothEnabled())
                {
                    if (Thread.CurrentThread != mainThread)
                    {
                        AndroidJNI.AttachCurrentThread();
                    }

                    AndroidBleMidiPluginInitialize();
                    bleMidiPlugin.Call("startAdvertising");
                    if (Thread.CurrentThread != mainThread)
                    {
                        AndroidJNI.DetachCurrentThread();
                    }
                }
            }
        }

        /// <summary>
        /// Stop to advertise BLE MIDI Peripheral device
        /// </summary>
        public void StopAdvertisingBluetoothMidiDevice()
        {
            if (AndroidIsBluetoothEnabled())
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }

                AndroidBleMidiPluginInitialize();
                bleMidiPlugin.Call("stopAdvertising");
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }
            }
        }

        /// <summary>
        /// Terminates MIDI Plugin system
        /// </summary>
        public void TerminateMidi()
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }

            if (usbMidiPlugin != null)
            {
                usbMidiPlugin.Call("terminate");
                usbMidiPlugin = null;
            }

            if (bleMidiPlugin != null)
            {
                bleMidiPlugin.Call("terminate");
                bleMidiPlugin = null;
                androidBleMidiPluginInitialized = false;

            }

            if (interAppMidiPlugin != null)
            {
                interAppMidiPlugin.Call("terminate");
                interAppMidiPlugin = null;
            }

            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
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

        /// <summary>
        /// Obtains device name for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetDeviceName(string deviceId)
        {
            if (usbMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }

                var result = usbMidiPlugin.Call<string>("getDeviceName", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }

                if (result != null)
                {
                    return result;
                }
            }

            if (bleMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }

                var result = bleMidiPlugin.Call<string>("getDeviceName", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }

                if (result != null)
                {
                    return result;
                }
            }

            if (interAppMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }

                var result = interAppMidiPlugin.Call<string>("getDeviceName", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }

                if (result != null)
                {
                    return result;
                }
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
            if (usbMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }
                var result = usbMidiPlugin.Call<string>("getVendorId", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }
                if (result != null)
                {
                    return result;
                }
            }

            if (bleMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }
                var result = bleMidiPlugin.Call<string>("getVendorId", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }
                if (result != null)
                {
                    return result;
                }
            }

            if (interAppMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }
                var result = interAppMidiPlugin.Call<string>("getVendorId", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }
                if (result != null)
                {
                    return result;
                }
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
            if (usbMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }
                var result = usbMidiPlugin.Call<string>("getProductId", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }
                if (result != null)
                {
                    return result;
                }
            }

            if (bleMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }
                var result = bleMidiPlugin.Call<string>("getProductId", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }
                if (result != null)
                {
                    return result;
                }
            }

            if (interAppMidiPlugin != null)
            {
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.AttachCurrentThread();
                }
                var result = interAppMidiPlugin.Call<string>("getProductId", deviceId);
                if (Thread.CurrentThread != mainThread)
                {
                    AndroidJNI.DetachCurrentThread();
                }
                if (result != null)
                {
                    return result;
                }
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
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiNoteOn", deviceId, group, channel, note, velocity);
            bleMidiPlugin?.Call("sendMidiNoteOn", deviceId, channel, note, velocity);
            interAppMidiPlugin?.Call("sendMidiNoteOn", deviceId, channel, note, velocity);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
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
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiNoteOff", deviceId, group, channel, note, velocity);
            bleMidiPlugin?.Call("sendMidiNoteOff", deviceId, channel, note, velocity);
            interAppMidiPlugin?.Call("sendMidiNoteOff", deviceId, channel, note, velocity);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
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
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiPolyphonicAftertouch", deviceId, group, channel, note, pressure);
            bleMidiPlugin?.Call("sendMidiPolyphonicAftertouch", deviceId, channel, note, pressure);
            interAppMidiPlugin?.Call("sendMidiPolyphonicAftertouch", deviceId, channel, note, pressure);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
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
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiControlChange", deviceId, group, channel, function, value);
            bleMidiPlugin?.Call("sendMidiControlChange", deviceId, channel, function, value);
            interAppMidiPlugin?.Call("sendMidiControlChange", deviceId, channel, function, value);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
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
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiProgramChange", deviceId, group, channel, program);
            bleMidiPlugin?.Call("sendMidiProgramChange", deviceId, channel, program);
            interAppMidiPlugin?.Call("sendMidiProgramChange", deviceId, channel, program);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
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
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiChannelAftertouch", deviceId, group, channel, pressure);
            bleMidiPlugin?.Call("sendMidiChannelAftertouch", deviceId, channel, pressure);
            interAppMidiPlugin?.Call("sendMidiChannelAftertouch", deviceId, channel, pressure);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
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
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiPitchWheel", deviceId, group, channel, amount);
            bleMidiPlugin?.Call("sendMidiPitchWheel", deviceId, channel, amount);
            interAppMidiPlugin?.Call("sendMidiPitchWheel", deviceId, channel, amount);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a System Exclusive message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="sysEx">byte array starts with F0, ends with F7</param>
        public void SendMidiSystemExclusive(string deviceId, int group, byte[] sysEx)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiSystemExclusive", deviceId, group, Array.ConvertAll(sysEx, b => unchecked((sbyte)b)));
            bleMidiPlugin?.Call("sendMidiSystemExclusive", deviceId, Array.ConvertAll(sysEx, b => unchecked((sbyte)b)));
            interAppMidiPlugin?.Call("sendMidiSystemExclusive", deviceId, Array.ConvertAll(sysEx, b => unchecked((sbyte)b)));
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a System Common message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="message">byte array</param>
        public void SendMidiSystemCommonMessage(string deviceId, int group, byte[] message)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiSystemCommonMessage", deviceId, group, Array.ConvertAll(message, b => unchecked((sbyte)b)));
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Single Byte message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        public void SendMidiSingleByte(string deviceId, int group, int byte1)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiSingleByte", deviceId, group, byte1);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        public void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiTimeCodeQuarterFrame", deviceId, group, timing);
            bleMidiPlugin?.Call("sendMidiTimeCodeQuarterFrame", deviceId, timing);
            interAppMidiPlugin?.Call("sendMidiTimeCodeQuarterFrame", deviceId, timing);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Song Select message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="song">0-127</param>
        public void SendMidiSongSelect(string deviceId, int group, int song)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiSongSelect", deviceId, group, song);
            bleMidiPlugin?.Call("sendMidiSongSelect", deviceId, song);
            interAppMidiPlugin?.Call("sendMidiSongSelect", deviceId, song);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Song Position Pointer message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="position">0-16383</param>
        public void SendMidiSongPositionPointer(string deviceId, int group, int position)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiSongPositionPointer", deviceId, group, position);
            bleMidiPlugin?.Call("sendMidiSongPositionPointer", deviceId, position);
            interAppMidiPlugin?.Call("sendMidiSongPositionPointer", deviceId, position);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Tune Request message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTuneRequest(string deviceId, int group)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiTuneRequest", deviceId, group);
            bleMidiPlugin?.Call("sendMidiTuneRequest", deviceId);
            interAppMidiPlugin?.Call("sendMidiTuneRequest", deviceId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Timing Clock message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTimingClock(string deviceId, int group)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiTimingClock", deviceId, group);
            bleMidiPlugin?.Call("sendMidiTimingClock", deviceId);
            interAppMidiPlugin?.Call("sendMidiTimingClock", deviceId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Start message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStart(string deviceId, int group)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiStart", deviceId, group);
            bleMidiPlugin?.Call("sendMidiStart", deviceId);
            interAppMidiPlugin?.Call("sendMidiStart", deviceId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Continue message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiContinue(string deviceId, int group)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiContinue", deviceId, group);
            bleMidiPlugin?.Call("sendMidiContinue", deviceId);
            interAppMidiPlugin?.Call("sendMidiContinue", deviceId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Stop message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStop(string deviceId, int group)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiStop", deviceId, group);
            bleMidiPlugin?.Call("sendMidiStop", deviceId);
            interAppMidiPlugin?.Call("sendMidiStop", deviceId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends an Active Sensing message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiActiveSensing(string deviceId, int group)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiActiveSensing", deviceId, group);
            bleMidiPlugin?.Call("sendMidiActiveSensing", deviceId);
            interAppMidiPlugin?.Call("sendMidiActiveSensing", deviceId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Reset message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiReset(string deviceId, int group)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiReset", deviceId, group);
            bleMidiPlugin?.Call("sendMidiReset", deviceId);
            interAppMidiPlugin?.Call("sendMidiReset", deviceId);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Miscellaneous Function Codes message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1"></param>
        /// <param name="byte2"></param>
        /// <param name="byte3"></param>
        public void SendMidiMiscellaneousFunctionCodes(string deviceId, int group, int byte1, int byte2, int byte3)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiMiscellaneousFunctionCodes", deviceId, group, byte1, byte2, byte3);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }

        /// <summary>
        /// Sends a Cable Events message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        /// <param name="byte2">0-255</param>
        /// <param name="byte3">0-255</param>
        public void SendMidiCableEvents(string deviceId, int group, int byte1, int byte2, int byte3)
        {
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.AttachCurrentThread();
            }
            usbMidiPlugin?.Call("sendMidiCableEvents", deviceId, group, byte1, byte2, byte3);
            if (Thread.CurrentThread != mainThread)
            {
                AndroidJNI.DetachCurrentThread();
            }
        }
    }
}
#endif
