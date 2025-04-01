using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using jp.kshoji.midisystem;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;

#if !UNITY_WEBGL || UNITY_EDITOR
using System.ComponentModel;
using AsyncOperation = System.ComponentModel.AsyncOperation;
#endif

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MIDI Manager, will be registered as `DontDestroyOnLoad` GameObject
    /// </summary>
    public partial class MidiManager : MonoBehaviour
    {
        private readonly object deviceIdSetLock = new object();
        private readonly HashSet<string> inputDeviceIdSet = new HashSet<string>();
        private readonly HashSet<string> outputDeviceIdSet = new HashSet<string>();

        public readonly List<IMidiPlugin> midiPlugins = new List<IMidiPlugin>()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            new AndroidMidiPlugin(),
#endif
#if UNITY_EDITOR_OSX || ((UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR)
            new AppleMidiPlugin(),
#endif
#if UNITY_EDITOR_WIN || ((UNITY_WSA || UNITY_STANDALONE_WIN) && !UNITY_EDITOR)
            new WindowsMidiPlugin(),
#endif
#if UNITY_EDITOR_LINUX || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
            new LinuxMidiPlugin(),
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
            new WebGlMidiPlugin(),
#endif
#if (!UNITY_IOS && !UNITY_WEBGL) || UNITY_EDITOR
            new RtpMidiPlugin(),
#endif
#if (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || ((UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR)) && ENABLE_NEARBY_CONNECTIONS
            new NearbyMidiPlugin(),
#endif
        };

        /// <summary>
        /// the set of MIDI device ID string.
        /// </summary>
        public HashSet<string> DeviceIdSet
        {
            get
            {
                lock (deviceIdSetLock)
                {
                    var deviceIdSet = new HashSet<string>(inputDeviceIdSet);
                    deviceIdSet.UnionWith(outputDeviceIdSet);
                    return deviceIdSet;
                }
            }
        }

        /// <summary>
        /// the set of MIDI input device ID string.
        /// </summary>
        public HashSet<string> InputDeviceIdSet
        {
            get
            {
                lock (deviceIdSetLock)
                {
                    return new HashSet<string>(inputDeviceIdSet);
                }
            }
        }

        /// <summary>
        /// the set of MIDI output device ID string.
        /// </summary>
        public HashSet<string> OutputDeviceIdSet
        {
            get
            {
                lock (deviceIdSetLock)
                {
                    return new HashSet<string>(outputDeviceIdSet);
                }
            }
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        internal AsyncOperation asyncOperation;
#endif

        /// <summary>
        /// Get an instance<br />
        /// SHOULD be called by Unity's main thread.
        /// </summary>
        public static MidiManager Instance => lazyInstance.Value;

        private static readonly Lazy<MidiManager> lazyInstance = new Lazy<MidiManager>(() =>
        {
            var instance = new GameObject("MidiManager").AddComponent<MidiManager>();

#if !UNITY_WEBGL || UNITY_EDITOR
            instance.asyncOperation = AsyncOperationManager.CreateOperation(null);
#endif

#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                DontDestroyOnLoad(instance);
            }
            else
            {
                Debug.Log("Don't initialize MidiManager while Unity Editor is not playing!");
            }
#else
            DontDestroyOnLoad(instance);
#endif

            return instance;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        private MidiManager()
        {
        }

        ~MidiManager()
        {
            foreach (var midiPlugin in midiPlugins)
            {
                midiPlugin.TerminateMidi();
            }
        }

        /// <summary>
        /// Initializes MIDI Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
        public void InitializeMidi(Action initializeCompletedAction)
        {
            Debug.Log("[MIDI] Starting MIDI initialization...");

            if (EventSystem.current == null)
            {
                gameObject.AddComponent<EventSystem>();
            }

            Debug.Log($"[MIDI] Found {midiPlugins.Count} MIDI plugins to initialize.");

            var initializedPlugins = new Dictionary<IMidiPlugin, bool>();
            foreach (var midiPlugin in midiPlugins)
            {
                Debug.Log($"[MIDI] Initializing plugin: {midiPlugin.GetType().Name}");

                // Настройка порта для RtpMidiPlugin
                if (midiPlugin is RtpMidiPlugin rtpMidiPlugin)
                {
                    rtpMidiPlugin.SetPort(5004); // Установка стандартного порта
                }

                initializedPlugins[midiPlugin] = false;
            }

            IEnumerator InitializeCompletedWatcher()
            {
                while (initializedPlugins.Count(w => w.Value) < midiPlugins.Count)
                {
                    Debug.Log($"[MIDI] Waiting for plugins to initialize. Initialized: {initializedPlugins.Count(w => w.Value)}/{midiPlugins.Count}");
                    yield return null;
                }

                Debug.Log("[MIDI] All plugins initialized. Invoking initializeCompletedAction...");
                initializeCompletedAction?.Invoke();
            }

            StartCoroutine(InitializeCompletedWatcher());

            // Инициализация всех плагинов
            foreach (var midiPlugin in midiPlugins)
            {
                midiPlugin.InitializeMidi(() =>
                {
                    Debug.Log($"[MIDI] Plugin initialized: {midiPlugin.GetType().Name}");
                    initializedPlugins[midiPlugin] = true;
                });
            }
        }

#if UNITY_EDITOR
        private void Awake()
        {
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        void PlayModeStateChanged(PlayModeStateChange stateChange)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                midiPlugin.PlayModeStateChanged(stateChange);
            }

            if (stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            }
        }
#endif

#region RtpMidi
        /// <summary>
        /// Starts RTP MIDI Listener
        /// </summary>
        /// <param name="sessionName">the name of session</param>
        /// <param name="listenPort">UDP port number(0-65534)</param>
        /// <exception cref="NotImplementedException">iOS platform isn't available</exception>
        public void StartRtpMidiServer(string sessionName, int listenPort)
        {
#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
            throw new NotImplementedException("iOS / WebGL platform isn't available");
#else
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is RtpMidiPlugin rtpMidiPlugin)
                {
                    rtpMidiPlugin.StartRtpMidiServer(sessionName, listenPort);
                    break;
                }
            }
#endif
        }

        /// <summary>
        /// Check RTP MIDI Listener is running
        /// </summary>
        /// <param name="listenPort">UDP port number(0-65534)</param>
        public bool IsRtpMidiRunning(int listenPort)
        {
#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
            throw new NotImplementedException("iOS / WebGL platform isn't available");
#else
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is RtpMidiPlugin rtpMidiPlugin)
                {
                    return rtpMidiPlugin.IsRtpMidiRunning(listenPort);
                }
            }

            return false;
#endif
        }

        /// <summary>
        /// Stops RTP MIDI Listener with the specified port
        /// </summary>
        public void StopRtpMidi(int listenPort)
        {
#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
            throw new NotImplementedException("iOS / WebGL platform isn't available");
#else
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is RtpMidiPlugin rtpMidiPlugin)
                {
                    rtpMidiPlugin.StopRtpMidi(listenPort);
                    break;
                }
            }
#endif
        }

        /// <summary>
        /// Stops all RTP MIDI servers
        /// </summary>
        public void StopAllRtpMidi()
        {
#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
            throw new NotImplementedException("iOS / WebGL platform isn't available");
#else
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is RtpMidiPlugin rtpMidiPlugin)
                {
                    rtpMidiPlugin.StopAllRtpMidi();
                    break;
                }
            }
#endif
        }
        
        /// <summary>
        /// Initiate RTP MIDI Connection with specified IPEndPoint
        /// </summary>
        /// <param name="sessionName">the name of session</param>
        /// <param name="listenPort">port to listen</param>
        /// <param name="ipEndPoint">IP address and port to connect with</param>
        public void ConnectToRtpMidiServer(string sessionName, int listenPort, System.Net.IPEndPoint ipEndPoint)
        {
#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
            throw new NotImplementedException("iOS / WebGL platform isn't available");
#else
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is RtpMidiPlugin rtpMidiPlugin)
                {
                    rtpMidiPlugin.ConnectToRtpMidiServer(sessionName, listenPort, ipEndPoint);
                    break;
                }
            }
#endif
        }
#endregion

#region BluetoothMidi
        /// <summary>
        /// Starts to scan BLE MIDI devices
        /// for Android / iOS / WebGL devices only
        /// </summary>
        /// <param name="timeout">timeout milliseconds, 0 : no timeout</param>
        public void StartScanBluetoothMidiDevices(int timeout)
        {
#if (UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            foreach (var midiPlugin in midiPlugins)
            {
                midiPlugin.StartScanBluetoothMidiDevices(timeout);
            }
#else
            throw new NotImplementedException("this platform isn't available");
#endif
        }

        /// <summary>
        /// Stops to scan BLE MIDI devices
        /// for Android / iOS / WebGL devices only
        /// </summary>
        public void StopScanBluetoothMidiDevices()
        {
#if (UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            foreach (var midiPlugin in midiPlugins)
            {
                midiPlugin.StopScanBluetoothMidiDevices();
            }
#else
            throw new NotImplementedException("this platform isn't available");
#endif
        }

        /// <summary>
        /// Start to advertise BLE MIDI Peripheral device
        /// for Android devices only
        /// </summary>
        /// <exception cref="NotImplementedException">the platform isn't available</exception>
        public void StartAdvertisingBluetoothMidiDevice()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var midiPlugin in midiPlugins)
            {
                midiPlugin.StartAdvertisingBluetoothMidiDevice();
            }
#else
            throw new NotImplementedException("this platform isn't available");
#endif
        }

        /// <summary>
        /// Stop to advertise BLE MIDI Peripheral device
        /// for Android devices only
        /// </summary>
        /// <exception cref="NotImplementedException">the platform isn't available</exception>
        public void StopAdvertisingBluetoothMidiDevice()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var midiPlugin in midiPlugins)
            {
                midiPlugin.StopAdvertisingBluetoothMidiDevice();
            }
#else
            throw new NotImplementedException("this platform isn't available");
#endif
        }
#endregion

#region NearbyConnectionsMidi
#if (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || ((UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR)) && ENABLE_NEARBY_CONNECTIONS
        private void Update()
        {
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is NearbyMidiPlugin nearbyMidiPlugin)
                {
                    nearbyMidiPlugin.Update();
                    break;
                }
            }
        }

        /// <summary>
        /// Start to scan Nearby MIDI devices
        /// </summary>
        public void StartNearbyDiscovering()
        {
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is NearbyMidiPlugin nearbyMidiPlugin)
                {
                    nearbyMidiPlugin.StartNearbyDiscovering();
                    break;
                }
            }
        }

        /// <summary>
        /// Stop to scan Nearby MIDI devices
        /// </summary>
        public void StopNearbyDiscovering()
        {
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is NearbyMidiPlugin nearbyMidiPlugin)
                {
                    nearbyMidiPlugin.StopNearbyDiscovering();
                    break;
                }
            }
        }

        /// <summary>
        /// Start to advertise Nearby MIDI device
        /// </summary>
        public void StartNearbyAdvertising()
        {
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is NearbyMidiPlugin nearbyMidiPlugin)
                {
                    nearbyMidiPlugin.StartNearbyAdvertising();
                    break;
                }
            }
        }

        /// <summary>
        /// Stop to advertise Nearby MIDI device
        /// </summary>
        public void StopNearbyAdvertising()
        {
            foreach (var midiPlugin in midiPlugins)
            {
                if (midiPlugin is NearbyMidiPlugin nearbyMidiPlugin)
                {
                    nearbyMidiPlugin.StopNearbyAdvertising();
                    break;
                }
            }
        }
#endif
#endregion

        private void OnApplicationQuit()
        {
            // terminates MIDI system if not terminated
            TerminateMidi();
        }

        /// <summary>
        /// Terminates MIDI Plugin system
        /// </summary>
        public void TerminateMidi()
        {
            // close all sequencer threads
            SequencerImpl.CloseAllSequencers();

            foreach (var midiPlugin in midiPlugins)
            {
                midiPlugin.TerminateMidi();
            }
        }

        internal readonly HashSet<GameObject> midiDeviceEventHandlers = new HashSet<GameObject>();

        private readonly MpeInputEventHandler mpeInputEventHandler = new MpeInputEventHandler();

        /// <summary>
        /// Registers Unity GameObject to receive MIDI events, and Connection events
        /// </summary>
        /// <param name="eventHandler"></param>
        public void RegisterEventHandleObject(GameObject eventHandler)
        {
            midiDeviceEventHandlers.Add(eventHandler);
        }

        private readonly Dictionary<string, string> deviceNameCache = new Dictionary<string, string>();
        private readonly Dictionary<string, string> vendorIdCache = new Dictionary<string, string>();
        private readonly Dictionary<string, string> productIdCache = new Dictionary<string, string>();

        /// <summary>
        /// Obtains device name for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetDeviceName(string deviceId)
        {
            lock (deviceNameCache)
            {
                if (deviceNameCache.TryGetValue(deviceId, out var deviceName))
                {
                    return deviceName;
                }
            }

            foreach (var midiPlugin in midiPlugins)
            {
                var deviceName = midiPlugin.GetDeviceName(deviceId);
                if (!string.IsNullOrEmpty(deviceName))
                {
                    lock (deviceNameCache)
                    {
                        deviceNameCache[deviceId] = deviceName;
                    }
                    return deviceName;
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
            lock (vendorIdCache)
            {
                if (vendorIdCache.TryGetValue(deviceId, out var vendorId))
                {
                    return vendorId;
                }
            }

            foreach (var midiPlugin in midiPlugins)
            {
                var vendorId = midiPlugin.GetVendorId(deviceId);
                if (!string.IsNullOrEmpty(vendorId))
                {
                    lock (vendorIdCache)
                    {
                        vendorIdCache[deviceId] = vendorId;
                    }
                    return vendorId;
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
            lock (productIdCache)
            {
                if (productIdCache.TryGetValue(deviceId, out var productId))
                {
                    return productId;
                }
            }

            foreach (var midiPlugin in midiPlugins)
            {
                var productId = midiPlugin.GetProductId(deviceId);
                if (!string.IsNullOrEmpty(productId))
                {
                    lock (productIdCache)
                    {
                        productIdCache[deviceId] = productId;
                    }
                    return productId;
                }
            }

            return string.Empty;
        }

        internal class MidiMessage
        {
            internal string DeviceId;
            internal int Group;
            internal decimal[] Messages;
            internal byte[] SystemExclusive;
        }

        private static MidiMessage DeserializeMidiMessage(string midiMessage, bool isSysEx = false)
        {
            var split = midiMessage.Split(',');
            decimal[] midiMessageArray = null;
            byte[] systemExclusive = null;
            if (isSysEx)
            {
                systemExclusive = new byte[split.Length - 2];
                for (var i = 2; i < split.Length; i++)
                {
                    systemExclusive[i - 2] = byte.Parse(split[i]);
                }
            }
            else
            {
                midiMessageArray = new decimal[split.Length - 2];
                for (var i = 2; i < split.Length; i++)
                {
                    midiMessageArray[i - 2] = int.Parse(split[i]);
                }
            }

            return new MidiMessage
            {
                Messages = midiMessageArray,
                DeviceId = split[0],
                Group = int.Parse(split[1]),
                SystemExclusive = systemExclusive,
            };
        }

        internal class MidiEventData : BaseEventData
        {
            internal readonly MidiMessage Message;

            public MidiEventData(MidiMessage message, EventSystem eventSystem) : base(eventSystem)
            {
                Message = message;
            }
        }

        private class MidiDeviceEventData : BaseEventData
        {
            internal readonly string DeviceId;

            public MidiDeviceEventData(string deviceId, EventSystem eventSystem) : base(eventSystem)
            {
                DeviceId = deviceId;
            }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiNoteOn(deviceId, group, channel, note, velocity);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiNoteOff(deviceId, group, channel, note, velocity);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiPolyphonicAftertouch(deviceId, group, channel, note, pressure);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiControlChange(deviceId, group, channel, function, value);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiProgramChange(deviceId, group, channel, program);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiChannelAftertouch(deviceId, group, channel, pressure);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiPitchWheel(deviceId, group, channel, amount);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiSystemExclusive(deviceId, group, sysEx);
                }
                catch (Exception)
                {
                    // ignored
                }
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
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiSystemCommonMessage(deviceId, group, message);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
#endif
        }

        /// <summary>
        /// Sends a Single Byte message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        public void SendMidiSingleByte(string deviceId, int group, int byte1)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiSingleByte(deviceId, group, byte1);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
#endif
        }

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        public void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiTimeCodeQuarterFrame(deviceId, group, timing);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiSongSelect(deviceId, group, song);
                }
                catch (Exception)
                {
                    // ignored
                }
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
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiSongPositionPointer(deviceId, group, position);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Sends a Tune Request message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTuneRequest(string deviceId, int group)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiTuneRequest(deviceId, group);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Sends a Timing Clock message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiTimingClock(string deviceId, int group)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiTimingClock(deviceId, group);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Sends a Start message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStart(string deviceId, int group)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiStart(deviceId, group);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Sends a Continue message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiContinue(string deviceId, int group)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiContinue(deviceId, group);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Sends a Stop message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiStop(string deviceId, int group)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiStop(deviceId, group);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Sends an Active Sensing message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiActiveSensing(string deviceId, int group)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiActiveSensing(deviceId, group);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Sends a Reset message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        public void SendMidiReset(string deviceId, int group)
        {
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiReset(deviceId, group);
                }
                catch (Exception)
                {
                    // ignored
                }
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
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiMiscellaneousFunctionCodes(deviceId, group, byte1, byte2, byte3);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
#endif
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
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var midiPlugin in midiPlugins)
            {
                try
                {
                    midiPlugin.SendMidiCableEvents(deviceId, group, byte1, byte2, byte3);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
#endif
        }

        internal void OnMidiInputDeviceAttached(string deviceId)
        {
            lock (deviceIdSetLock)
            {
                inputDeviceIdSet.Add(deviceId);
            }

            MidiSystem.AddTransmitter(deviceId, new TransmitterImpl(deviceId));

            foreach (var midiDeviceEventHandler in midiDeviceEventHandlers)
            {
                if (!ExecuteEvents.CanHandleEvent<IMidiDeviceEventHandler>(midiDeviceEventHandler))
                {
                    continue;
                }

                ExecuteEvents.Execute<IMidiDeviceEventHandler>(midiDeviceEventHandler,
                    new MidiDeviceEventData(deviceId, EventSystem.current), (eventHandler, baseEventData) =>
                    {
                        if (baseEventData is MidiDeviceEventData midiDeviceEventData)
                        {
                            eventHandler.OnMidiInputDeviceAttached(midiDeviceEventData.DeviceId);
                        }
                    });
            }
        }

        internal void OnMidiOutputDeviceAttached(string deviceId)
        {
            lock (deviceIdSetLock)
            {
                outputDeviceIdSet.Add(deviceId);
            }

            MidiSystem.AddReceiver(deviceId, new ReceiverImpl(deviceId));

            foreach (var midiDeviceEventHandler in midiDeviceEventHandlers)
            {
                if (!ExecuteEvents.CanHandleEvent<IMidiDeviceEventHandler>(midiDeviceEventHandler))
                {
                    continue;
                }

                ExecuteEvents.Execute<IMidiDeviceEventHandler>(midiDeviceEventHandler,
                    new MidiDeviceEventData(deviceId, EventSystem.current), (eventHandler, baseEventData) =>
                    {
                        if (baseEventData is MidiDeviceEventData midiDeviceEventData)
                        {
                            eventHandler.OnMidiOutputDeviceAttached(midiDeviceEventData.DeviceId);
                        }
                    });
            }
        }

        internal void OnMidiInputDeviceDetached(string deviceId)
        {
            lock (deviceIdSetLock)
            {
                inputDeviceIdSet.Remove(deviceId);
            }

            lock (deviceNameCache)
            {
                if (deviceNameCache.ContainsKey(deviceId))
                {
                    deviceNameCache.Remove(deviceId);
                }
            }
            lock (vendorIdCache)
            {
                if (vendorIdCache.ContainsKey(deviceId))
                {
                    vendorIdCache.Remove(deviceId);
                }
            }
            lock (productIdCache)
            {
                if (productIdCache.ContainsKey(deviceId))
                {
                    productIdCache.Remove(deviceId);
                }
            }

            MidiSystem.RemoveTransmitter(deviceId);

            foreach (var midiDeviceEventHandler in midiDeviceEventHandlers)
            {
                if (!ExecuteEvents.CanHandleEvent<IMidiDeviceEventHandler>(midiDeviceEventHandler))
                {
                    continue;
                }

                ExecuteEvents.Execute<IMidiDeviceEventHandler>(midiDeviceEventHandler,
                    new MidiDeviceEventData(deviceId, EventSystem.current), (eventHandler, baseEventData) =>
                    {
                        if (baseEventData is MidiDeviceEventData midiDeviceEventData)
                        {
                            eventHandler.OnMidiInputDeviceDetached(midiDeviceEventData.DeviceId);
                        }
                    });
            }
        }

        internal void OnMidiOutputDeviceDetached(string deviceId)
        {
            lock (deviceIdSetLock)
            {
                outputDeviceIdSet.Remove(deviceId);
            }

            lock (deviceNameCache)
            {
                if (deviceNameCache.ContainsKey(deviceId))
                {
                    deviceNameCache.Remove(deviceId);
                }
            }
            lock (vendorIdCache)
            {
                if (vendorIdCache.ContainsKey(deviceId))
                {
                    vendorIdCache.Remove(deviceId);
                }
            }
            lock (productIdCache)
            {
                if (productIdCache.ContainsKey(deviceId))
                {
                    productIdCache.Remove(deviceId);
                }
            }

            MidiSystem.RemoveReceiver(deviceId);

            foreach (var midiDeviceEventHandler in midiDeviceEventHandlers)
            {
                if (!ExecuteEvents.CanHandleEvent<IMidiDeviceEventHandler>(midiDeviceEventHandler))
                {
                    continue;
                }

                ExecuteEvents.Execute<IMidiDeviceEventHandler>(midiDeviceEventHandler,
                    new MidiDeviceEventData(deviceId, EventSystem.current), (eventHandler, baseEventData) =>
                    {
                        if (baseEventData is MidiDeviceEventData midiDeviceEventData)
                        {
                            eventHandler.OnMidiOutputDeviceDetached(midiDeviceEventData.DeviceId);
                        }
                    });
            }
        }

        private static void SendMidiMessageToTransmitters(MidiMessage parsed, int status)
        {
            var transmitters = MidiSystem.GetTransmitters();
            try
            {
                ShortMessage message;
                if (parsed.Messages == null)
                {
                    message = new ShortMessage(status, 0, 0);
                }
                else
                {
                    message = new ShortMessage(
                        status | (parsed.Messages.Length > 0 ? (int)parsed.Messages[0] : 0),
                        parsed.Messages.Length > 1 ? (int)parsed.Messages[1] : 0,
                        parsed.Messages.Length > 2 ? (int)parsed.Messages[2] : 0);
                }

                foreach (var transmitter in transmitters)
                {
                    transmitter.GetReceiver()?.Send(message, 0);
                }
            }
            catch (InvalidMidiDataException)
            {
                // ignore invalid message
            }
        }

        internal void ExecuteMidiEvent<T>(MidiMessage midiMessage, Action<T, MidiMessage> callback) where T : IEventSystemHandler
        {
            var eventData = new MidiEventData(midiMessage, EventSystem.current);
            foreach (var midiDeviceEventHandler in midiDeviceEventHandlers)
            {
                if (!ExecuteEvents.CanHandleEvent<T>(midiDeviceEventHandler))
                {
                    continue;
                }

                ExecuteEvents.Execute<T>(midiDeviceEventHandler,
                    eventData, (eventHandler, baseEventData) =>
                    {
                        if (baseEventData is MidiEventData midiEventData)
                        {
                            callback(eventHandler, midiEventData.Message);
                        }
                    });
            }
        }
        
        private void OnMidiNoteOn(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiNoteOn(deserializedMidiMessage);
        }
        internal void OnMidiNoteOn(string deviceId, int group, int channel, int note, int velocity)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {channel, note, velocity},
            };
            OnMidiNoteOn(midiMessage);
        }
        private void OnMidiNoteOn(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiNoteOnEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiNoteOn(message.DeviceId, message.Group,
                    (int)message.Messages[0], (int)message.Messages[1], (int)message.Messages[2]);
            });

            mpeInputEventHandler.OnMidiNoteOn(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0], (int)midiMessage.Messages[1], (int)midiMessage.Messages[2]);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.NoteOn);
        }

        private void OnMidiNoteOff(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiNoteOff(deserializedMidiMessage);
        }
        internal void OnMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {channel, note, velocity},
            };
            OnMidiNoteOff(midiMessage);
        }
        private void OnMidiNoteOff(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiNoteOffEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiNoteOff(message.DeviceId, message.Group,
                    (int)message.Messages[0], (int)message.Messages[1], (int)message.Messages[2]);
            });

            mpeInputEventHandler.OnMidiNoteOff(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0], (int)midiMessage.Messages[1], (int)midiMessage.Messages[2]);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.NoteOff);
        }

        private void OnMidiPolyphonicAftertouch(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiPolyphonicAftertouch(deserializedMidiMessage);
        }
        internal void OnMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {channel, note, pressure},
            };
            OnMidiPolyphonicAftertouch(midiMessage);
        }
        private void OnMidiPolyphonicAftertouch(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiPolyphonicAftertouchEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiPolyphonicAftertouch(message.DeviceId, message.Group,
                    (int)message.Messages[0], (int)message.Messages[1], (int)message.Messages[2]);
            });

            mpeInputEventHandler.OnMidiPolyphonicAftertouch(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0], (int)midiMessage.Messages[1], (int)midiMessage.Messages[2]);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.PolyPressure);
        }

        private void OnMidiControlChange(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiControlChange(deserializedMidiMessage);
        }
        internal void OnMidiControlChange(string deviceId, int group, int channel, int function, int value)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {channel, function, value},
            };
            OnMidiControlChange(midiMessage);
        }
        private void OnMidiControlChange(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiControlChangeEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiControlChange(message.DeviceId, message.Group,
                    (int)message.Messages[0], (int)message.Messages[1], (int)message.Messages[2]);
            });

            mpeInputEventHandler.OnMidiControlChange(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0], (int)midiMessage.Messages[1], (int)midiMessage.Messages[2]);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.ControlChange);
        }

        private void OnMidiProgramChange(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiProgramChange(deserializedMidiMessage);
        }
        internal void OnMidiProgramChange(string deviceId, int group, int channel, int program)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {channel, program},
            };
            OnMidiProgramChange(midiMessage);
        }
        private void OnMidiProgramChange(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiProgramChangeEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiProgramChange(message.DeviceId, message.Group,
                    (int)message.Messages[0], (int)message.Messages[1]);
            });

            mpeInputEventHandler.OnMidiProgramChange(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0], (int)midiMessage.Messages[1]);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.ProgramChange);
        }

        private void OnMidiChannelAftertouch(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiChannelAftertouch(deserializedMidiMessage);
        }
        internal void OnMidiChannelAftertouch(string deviceId, int group, int channel, int pressure)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {channel, pressure},
            };
            OnMidiChannelAftertouch(midiMessage);
        }
        private void OnMidiChannelAftertouch(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiChannelAftertouchEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiChannelAftertouch(message.DeviceId, message.Group,
                    (int)message.Messages[0], (int)message.Messages[1]);
            });

            mpeInputEventHandler.OnMidiChannelAftertouch(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0], (int)midiMessage.Messages[1]);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.ChannelPressure);
        }

        private void OnMidiPitchWheel(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiPitchWheel(deserializedMidiMessage);
        }
        internal void OnMidiPitchWheel(string deviceId, int group, int channel, int amount)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {channel, amount},
            };
            OnMidiPitchWheel(midiMessage);
        }
        private void OnMidiPitchWheel(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiPitchWheelEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiPitchWheel(message.DeviceId, message.Group,
                    (int)message.Messages[0], (int)message.Messages[1]);
            });

            mpeInputEventHandler.OnMidiPitchWheel(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0], (int)midiMessage.Messages[1]);

            {
                var transmitters = MidiSystem.GetTransmitters();
                var message = new ShortMessage(ShortMessage.PitchBend | ((int)midiMessage.Messages[0] & ShortMessage.MaskChannel),
                    (int)midiMessage.Messages[1] & 0x7f,
                    ((int)midiMessage.Messages[1] >> 7) & 0x7f);
                foreach (var transmitter in transmitters)
                {
                    transmitter.GetReceiver()?.Send(message, 0);
                }
            }
        }

        private void OnMidiSystemExclusive(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage, true);
            OnMidiSystemExclusive(deserializedMidiMessage);
        }
        internal void OnMidiSystemExclusive(string deviceId, int group, byte[] systemExclusive)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                SystemExclusive = systemExclusive,
            };
            OnMidiSystemExclusive(midiMessage);
        }
        private void OnMidiSystemExclusive(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiSystemExclusiveEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiSystemExclusive(message.DeviceId, message.Group, message.SystemExclusive);
            });

            mpeInputEventHandler.OnMidiSystemExclusive(midiMessage.DeviceId, midiMessage.Group,
                midiMessage.SystemExclusive);

            {
                var transmitters = MidiSystem.GetTransmitters();
                var message = new SysexMessage(ShortMessage.StartOfExclusive, midiMessage.SystemExclusive);
                foreach (var transmitter in transmitters)
                {
                    transmitter.GetReceiver()?.Send(message, 0);
                }
            }
        }

        private void OnMidiSystemCommonMessage(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage, true);
            OnMidiSystemCommonMessage(deserializedMidiMessage);
        }
        internal void OnMidiSystemCommonMessage(string deviceId, int group, byte[] bytes)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                SystemExclusive = bytes,
            };
            OnMidiSystemCommonMessage(midiMessage);
        }
        private void OnMidiSystemCommonMessage(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiSystemCommonMessageEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiSystemCommonMessage(message.DeviceId, message.Group, message.SystemExclusive);
            });

            {
                var transmitters = MidiSystem.GetTransmitters();
                var message = new SysexMessage(ShortMessage.StartOfExclusive, midiMessage.SystemExclusive);
                foreach (var transmitter in transmitters)
                {
                    transmitter.GetReceiver()?.Send(message, 0);
                }
            }
        }

        private void OnMidiSingleByte(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiSingleByte(deserializedMidiMessage);
        }
        internal void OnMidiSingleByte(string deviceId, int group, int byte1)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {byte1},
            };
            OnMidiSingleByte(midiMessage);
        }
        private void OnMidiSingleByte(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiSingleByteEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiSingleByte(message.DeviceId, message.Group, (int)message.Messages[0]);
            });

            SendMidiMessageToTransmitters(midiMessage, 0);
        }

        private void OnMidiTimeCodeQuarterFrame(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiTimeCodeQuarterFrame(deserializedMidiMessage);
        }
        internal void OnMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {timing},
            };
            OnMidiTimeCodeQuarterFrame(midiMessage);
        }
        private void OnMidiTimeCodeQuarterFrame(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiTimeCodeQuarterFrameEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiTimeCodeQuarterFrame(message.DeviceId, message.Group, (int)message.Messages[0]);
            });

            mpeInputEventHandler.OnMidiTimeCodeQuarterFrame(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0]);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.MidiTimeCode);
        }

        private void OnMidiSongSelect(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiSongSelect(deserializedMidiMessage);
        }
        internal void OnMidiSongSelect(string deviceId, int group, int song)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {song},
            };
            OnMidiSongSelect(midiMessage);
        }
        private void OnMidiSongSelect(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiSongSelectEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiSongSelect(message.DeviceId, message.Group, (int)message.Messages[0]);
            });

            mpeInputEventHandler.OnMidiSongSelect(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0]);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.SongSelect);
        }

        private void OnMidiSongPositionPointer(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiSongPositionPointer(deserializedMidiMessage);
        }
        internal void OnMidiSongPositionPointer(string deviceId, int group, int position)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {position},
            };
            OnMidiSongPositionPointer(midiMessage);
        }
        private void OnMidiSongPositionPointer(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiSongPositionPointerEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiSongPositionPointer(message.DeviceId, message.Group, (int)message.Messages[0]);
            });

            mpeInputEventHandler.OnMidiSongPositionPointer(midiMessage.DeviceId, midiMessage.Group,
                (int)midiMessage.Messages[0]);

            {
                var transmitters = MidiSystem.GetTransmitters();
                var message = new ShortMessage(ShortMessage.SongPositionPointer,
                    (int)midiMessage.Messages[0] & 0x7f,
                    ((int)midiMessage.Messages[0] >> 7) & 0x7f);
                foreach (var transmitter in transmitters)
                {
                    transmitter.GetReceiver()?.Send(message, 0);
                }
            }
        }

        private void OnMidiTuneRequest(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiTuneRequest(deserializedMidiMessage);
        }
        internal void OnMidiTuneRequest(string deviceId, int group)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
            };
            OnMidiTuneRequest(midiMessage);
        }
        private void OnMidiTuneRequest(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiTuneRequestEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiTuneRequest(message.DeviceId, message.Group);
            });

            mpeInputEventHandler.OnMidiTuneRequest(midiMessage.DeviceId, midiMessage.Group);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.TuneRequest);
        }

        private void OnMidiTimingClock(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiTimingClock(deserializedMidiMessage);
        }
        internal void OnMidiTimingClock(string deviceId, int group)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
            };
            OnMidiTimingClock(midiMessage);
        }
        private void OnMidiTimingClock(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiTimingClockEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiTimingClock(message.DeviceId, message.Group);
            });

            mpeInputEventHandler.OnMidiTuneRequest(midiMessage.DeviceId, midiMessage.Group);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.TimingClock);
        }

        private void OnMidiStart(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiStart(deserializedMidiMessage);
        }
        internal void OnMidiStart(string deviceId, int group)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
            };
            OnMidiStart(midiMessage);
        }
        private void OnMidiStart(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiStartEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiStart(message.DeviceId, message.Group);
            });

            mpeInputEventHandler.OnMidiStart(midiMessage.DeviceId, midiMessage.Group);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.Start);
        }

        private void OnMidiContinue(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiContinue(deserializedMidiMessage);
        }
        internal void OnMidiContinue(string deviceId, int group)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
            };
            OnMidiContinue(midiMessage);
        }
        private void OnMidiContinue(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiContinueEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiContinue(message.DeviceId, message.Group);
            });

            mpeInputEventHandler.OnMidiContinue(midiMessage.DeviceId, midiMessage.Group);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.Continue);
        }

        private void OnMidiStop(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiStop(deserializedMidiMessage);
        }
        internal void OnMidiStop(string deviceId, int group)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
            };
            OnMidiStop(midiMessage);
        }
        private void OnMidiStop(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiStopEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiStop(message.DeviceId, message.Group);
            });

            mpeInputEventHandler.OnMidiStop(midiMessage.DeviceId, midiMessage.Group);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.Stop);
        }

        private void OnMidiActiveSensing(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiActiveSensing(deserializedMidiMessage);
        }
        internal void OnMidiActiveSensing(string deviceId, int group)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
            };
            OnMidiActiveSensing(midiMessage);
        }
        private void OnMidiActiveSensing(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiActiveSensingEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiActiveSensing(message.DeviceId, message.Group);
            });

            mpeInputEventHandler.OnMidiActiveSensing(midiMessage.DeviceId, midiMessage.Group);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.ActiveSensing);
        }

        private void OnMidiReset(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiReset(deserializedMidiMessage);
        }
        internal void OnMidiReset(string deviceId, int group)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
            };
            OnMidiReset(midiMessage);
        }
        private void OnMidiReset(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiResetEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiReset(message.DeviceId, message.Group);
            });

            mpeInputEventHandler.OnMidiReset(midiMessage.DeviceId, midiMessage.Group);

            SendMidiMessageToTransmitters(midiMessage, ShortMessage.SystemReset);
        }

        private void OnMidiMiscellaneousFunctionCodes(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiMiscellaneousFunctionCodes(deserializedMidiMessage);
        }
        internal void OnMidiMiscellaneousFunctionCodes(string deviceId, int group, int byte1, int byte2, int byte3)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {byte1, byte2, byte3},
            };
            OnMidiMiscellaneousFunctionCodes(midiMessage);
        }
        private void OnMidiMiscellaneousFunctionCodes(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiMiscellaneousFunctionCodesEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiMiscellaneousFunctionCodes(message.DeviceId, message.Group,
                    (int)(message.Messages.Length > 0 ? message.Messages[0] : 0),
                    (int)(message.Messages.Length > 1 ? message.Messages[1] : 0),
                    (int)(message.Messages.Length > 2 ? message.Messages[2] : 0));
            });
 
            SendMidiMessageToTransmitters(midiMessage, 0);
        }

        private void OnMidiCableEvents(string midiMessage)
        {
            var deserializedMidiMessage = DeserializeMidiMessage(midiMessage);
            OnMidiCableEvents(deserializedMidiMessage);
        }
        internal void OnMidiCableEvents(string deviceId, int group, int byte1, int byte2, int byte3)
        {
            var midiMessage = new MidiMessage
            {
                DeviceId = deviceId,
                Group = group,
                Messages = new decimal[] {byte1, byte2, byte3},
            };
            OnMidiCableEvents(midiMessage);
        }
        private void OnMidiCableEvents(MidiMessage midiMessage)
        {
            ExecuteMidiEvent<IMidiCableEventsEventHandler>(midiMessage, (handler, message) =>
            {
                handler.OnMidiCableEvents(message.DeviceId, message.Group,
                    (int)(message.Messages.Length > 0 ? message.Messages[0] : 0),
                    (int)(message.Messages.Length > 1 ? message.Messages[1] : 0),
                    (int)(message.Messages.Length > 2 ? message.Messages[2] : 0));
            });
 
            SendMidiMessageToTransmitters(midiMessage, 0);
        }
    }

    /// <summary>
    /// Receiver
    /// </summary>
    internal class ReceiverImpl : IReceiver
    {
        private readonly string deviceId;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        internal ReceiverImpl(string deviceId)
        {
            this.deviceId = deviceId;
        }

        /// <inheritdoc cref="object.ToString"/>
        public override string ToString()
        {
            return deviceId;
        }

        /// <inheritdoc cref="IReceiver.Send"/>
        public void Send(MidiMessage message, long timeStamp)
        {
            if (message is ShortMessage shortMessage)
            {
                var midiMessage = shortMessage.GetMessage();
                switch (shortMessage.GetStatus() & ShortMessage.MaskEvent)
                {
                    case ShortMessage.NoteOff:
                        MidiManager.Instance.SendMidiNoteOff(deviceId, 0,
                            midiMessage[0] & ShortMessage.MaskChannel, midiMessage[1], midiMessage[2]);
                        break;
                    case ShortMessage.NoteOn:
                        MidiManager.Instance.SendMidiNoteOn(deviceId, 0,
                            midiMessage[0] & ShortMessage.MaskChannel, midiMessage[1], midiMessage[2]);
                        break;
                    case ShortMessage.PolyPressure:
                        MidiManager.Instance.SendMidiPolyphonicAftertouch(deviceId, 0,
                            midiMessage[0] & ShortMessage.MaskChannel, midiMessage[1], midiMessage[2]);
                        break;
                    case ShortMessage.ControlChange:
                        MidiManager.Instance.SendMidiControlChange(deviceId, 0,
                            midiMessage[0] & ShortMessage.MaskChannel, midiMessage[1], midiMessage[2]);
                        break;
                    case ShortMessage.ProgramChange:
                        MidiManager.Instance.SendMidiProgramChange(deviceId, 0,
                            midiMessage[0] & ShortMessage.MaskChannel, midiMessage[1]);
                        break;
                    case ShortMessage.ChannelPressure:
                        MidiManager.Instance.SendMidiChannelAftertouch(deviceId, 0,
                            midiMessage[0] & ShortMessage.MaskChannel, midiMessage[1]);
                        break;
                    case ShortMessage.PitchBend:
                        MidiManager.Instance.SendMidiPitchWheel(deviceId, 0,
                            midiMessage[0] & ShortMessage.MaskChannel, midiMessage[1] | (midiMessage[2] << 7));
                        break;
                    case ShortMessage.MidiTimeCode:
                        MidiManager.Instance.SendMidiTimeCodeQuarterFrame(deviceId, 0,
                            midiMessage[1]);
                        break;
                    case ShortMessage.SongPositionPointer:
                        MidiManager.Instance.SendMidiSongPositionPointer(deviceId, 0,
                            midiMessage[1] | (midiMessage[2] << 7));
                        break;
                    case ShortMessage.SongSelect:
                        MidiManager.Instance.SendMidiSongSelect(deviceId, 0,
                            midiMessage[1]);
                        break;
                    case ShortMessage.TuneRequest:
                        MidiManager.Instance.SendMidiTuneRequest(deviceId, 0);
                        break;
                    case ShortMessage.TimingClock:
                        MidiManager.Instance.SendMidiTimingClock(deviceId, 0);
                        break;
                    case ShortMessage.Start:
                        MidiManager.Instance.SendMidiStart(deviceId, 0);
                        break;
                    case ShortMessage.Continue:
                        MidiManager.Instance.SendMidiContinue(deviceId, 0);
                        break;
                    case ShortMessage.Stop:
                        MidiManager.Instance.SendMidiStop(deviceId, 0);
                        break;
                    case ShortMessage.ActiveSensing:
                        MidiManager.Instance.SendMidiActiveSensing(deviceId, 0);
                        break;
                    case ShortMessage.SystemReset:
                        MidiManager.Instance.SendMidiReset(deviceId, 0);
                        break;
                }
            }
            else if (message is MetaMessage)
            {
                // ignore meta messages
            }
            else if (message is SysexMessage sysexMessage)
            {
                MidiManager.Instance.SendMidiSystemExclusive(deviceId, 0, sysexMessage.GetData());
            }
        }

        /// <inheritdoc cref="IReceiver.Close"/>
        public void Close()
        {
            // do nothing
        }
    }

    /// <summary>
    /// Transmitter
    /// </summary>
    internal class TransmitterImpl : ITransmitter
    {
        private readonly string deviceId;
        private IReceiver receiver;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="deviceId">the device ID</param>
        internal TransmitterImpl(string deviceId)
        {
            this.deviceId = deviceId;
        }

        /// <inheritdoc cref="object.ToString"/>
        public override string ToString()
        {
            return deviceId;
        }

        /// <inheritdoc cref="ITransmitter.SetReceiver"/>
        public void SetReceiver(IReceiver theReceiver)
        {
            receiver = theReceiver;
        }

        /// <inheritdoc cref="ITransmitter.GetReceiver"/>
        public IReceiver GetReceiver()
        {
            return receiver;
        }

        /// <inheritdoc cref="ITransmitter.Close"/>
        public void Close()
        {
            receiver.Close();
        }
    }
}