#if (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || ((UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR)) && ENABLE_NEARBY_CONNECTIONS
using System;
using System.Collections.Generic;
using jp.kshoji.unity.nearby;
using jp.kshoji.unity.nearby.midi;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace jp.kshoji.unity.midi
{
    /// <summary>
    /// MIDI Plugin for Nearby
    /// </summary>
    /// 
    /// NOTE: To use Nearby Connections MIDI feature, follow these processes below.
    /// Add a package with Unity Package Manager.
    /// Select `Add package from git URL...` and input this URL.
    /// ssh://git@github.com/kshoji/Nearby-Connections-for-Unity.git
    /// or this URL
    /// git+https://github.com/kshoji/Nearby-Connections-for-Unity
    ///
    /// Add a Scripting Define Symbol to Player settings
    /// ENABLE_NEARBY_CONNECTIONS
    public class NearbyMidiPlugin : IMidiPlugin
    {
        private const string NearbyServiceId = "MIDI";

        private static bool isNearbyInitialized = false;
        private static string localEndpointName = Guid.NewGuid().ToString();
        private static Dictionary<string, NearbyMidiInputDevice> nearbyInputDevices = new Dictionary<string, NearbyMidiInputDevice>();
        private static Dictionary<string, NearbyMidiOutputDevice> nearbyOutputDevices = new Dictionary<string, NearbyMidiOutputDevice>();
        private static NearbyMidiEventHandler nearbyMidiEventHandler = new NearbyMidiEventHandler();

        class NearbyMidiEventHandler : IMidiAllEventsHandler
        {
            public void OnMidiNoteOn(string deviceId, int group, int channel, int note, int velocity)
                => MidiManager.Instance.OnMidiNoteOn(deviceId, group, channel, note, velocity);

            public void OnMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
                => MidiManager.Instance.OnMidiNoteOff(deviceId, group, channel, note, velocity);

            public void OnMidiChannelAftertouch(string deviceId, int group, int channel, int pressure)
                => MidiManager.Instance.OnMidiChannelAftertouch(deviceId, group, channel, pressure);

            public void OnMidiPitchWheel(string deviceId, int group, int channel, int amount)
                => MidiManager.Instance.OnMidiPitchWheel(deviceId, group, channel, amount);

            public void OnMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
                => MidiManager.Instance.OnMidiPolyphonicAftertouch(deviceId, group, channel, note, pressure);

            public void OnMidiProgramChange(string deviceId, int group, int channel, int program)
                => MidiManager.Instance.OnMidiProgramChange(deviceId, group, channel, program);

            public void OnMidiControlChange(string deviceId, int group, int channel, int function, int value)
                => MidiManager.Instance.OnMidiControlChange(deviceId, group, channel, function, value);

            public void OnMidiContinue(string deviceId, int group)
                => MidiManager.Instance.OnMidiContinue(deviceId, group);

            public void OnMidiReset(string deviceId, int group)
                => MidiManager.Instance.OnMidiReset(deviceId, group);

            public void OnMidiStart(string deviceId, int group)
                => MidiManager.Instance.OnMidiStart(deviceId, group);

            public void OnMidiStop(string deviceId, int group)
                => MidiManager.Instance.OnMidiStop(deviceId, group);

            public void OnMidiActiveSensing(string deviceId, int group)
                => MidiManager.Instance.OnMidiActiveSensing(deviceId, group);

            public void OnMidiCableEvents(string deviceId, int group, int byte1, int byte2, int byte3)
                => MidiManager.Instance.OnMidiCableEvents(deviceId, group, byte1, byte2, byte3);

            public void OnMidiSongSelect(string deviceId, int group, int song)
                => MidiManager.Instance.OnMidiSongSelect(deviceId, group, song);

            public void OnMidiSongPositionPointer(string deviceId, int group, int position)
                => MidiManager.Instance.OnMidiSongPositionPointer(deviceId, group, position);

            public void OnMidiSingleByte(string deviceId, int group, int byte1)
                => MidiManager.Instance.OnMidiSingleByte(deviceId, group, byte1);

            public void OnMidiSystemExclusive(string deviceId, int group, byte[] systemExclusive)
                => MidiManager.Instance.OnMidiSystemExclusive(deviceId, group, systemExclusive);

            public void OnMidiSystemCommonMessage(string deviceId, int group, byte[] message)
                => MidiManager.Instance.OnMidiSystemCommonMessage(deviceId, group, message);

            public void OnMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
                => MidiManager.Instance.OnMidiTimeCodeQuarterFrame(deviceId, group, timing);

            public void OnMidiTimingClock(string deviceId, int group)
                => MidiManager.Instance.OnMidiTimingClock(deviceId, group);

            public void OnMidiTuneRequest(string deviceId, int group)
                => MidiManager.Instance.OnMidiTuneRequest(deviceId, group);

            public void OnMidiMiscellaneousFunctionCodes(string deviceId, int group, int byte1, int byte2, int byte3)
                => MidiManager.Instance.OnMidiMiscellaneousFunctionCodes(deviceId, group, byte1, byte2, byte3);
        }

        public void InitializeMidi(Action initializeCompletedAction)
        {
            if (!isNearbyInitialized)
            {
                NearbyConnectionsManager.Instance.OnEndpointDiscovered += endpointId =>
                {
                    NearbyConnectionsManager.Instance.Connect(localEndpointName, endpointId);
                };
                NearbyConnectionsManager.Instance.OnConnectionInitiated += (endpointId, endpointName, connection) =>
                {
                    // auto accept connection
                    NearbyConnectionsManager.Instance.AcceptConnection(endpointId);
                };
                NearbyConnectionsManager.Instance.OnEndpointConnected += endpointId =>
                {
                    void PrepareOutputDevice()
                    {
                        var stream = NearbyConnectionsManager.Instance.StartSendStream(endpointId);
                        var outputDevice = new NearbyMidiOutputDevice(stream);
                        NearbyMidiOutputDevice.DeviceDisconnected outputDeviceOnOnDeviceDisconnected = null;
                        outputDeviceOnOnDeviceDisconnected = () =>
                        {
                            outputDevice.OnDeviceDisconnected -= outputDeviceOnOnDeviceDisconnected;
                            outputDevice.Close();
                            MidiManager.Instance.OnMidiOutputDeviceDetached(endpointId);
                            lock (nearbyOutputDevices)
                            {
                                nearbyOutputDevices.Remove(endpointId);
                            }
                            // auto reconnect if endpoint still connected
                            if (NearbyConnectionsManager.Instance.GetEstablishedConnections().Contains(endpointId))
                            {
                                PrepareOutputDevice();
                            }
                        };
                        outputDevice.OnDeviceDisconnected += outputDeviceOnOnDeviceDisconnected;

                        lock (nearbyOutputDevices)
                        {
                            if (nearbyOutputDevices.ContainsKey(endpointId))
                            {
                                nearbyOutputDevices.Remove(endpointId);
                            }

                            nearbyOutputDevices.Add(endpointId, outputDevice);
                        }
                        MidiManager.Instance.OnMidiOutputDeviceAttached(endpointId);
                    }

                    PrepareOutputDevice();
                };

                NearbyConnectionsManager.Instance.OnReceiveStream += (endpointId, payloadId, stream) =>
                {
                    void PrepareInputDevice()
                    {
                        var inputDevice = new NearbyMidiInputDevice(endpointId, stream, nearbyMidiEventHandler);
                        NearbyMidiInputDevice.DeviceDisconnected inputDeviceOnOnDeviceDisconnected = null;
                        inputDeviceOnOnDeviceDisconnected = () =>
                        {
                            inputDevice.OnDeviceDisconnected -= inputDeviceOnOnDeviceDisconnected;
                            inputDevice.Close();
                            MidiManager.Instance.OnMidiInputDeviceDetached(endpointId);
                            lock (nearbyInputDevices)
                            {
                                nearbyInputDevices.Remove(endpointId);
                            }
                            // auto reconnect if endpoint still connected
                            if (NearbyConnectionsManager.Instance.GetEstablishedConnections().Contains(endpointId))
                            {
                                PrepareInputDevice();
                            }
                        };

                        inputDevice.OnDeviceDisconnected += inputDeviceOnOnDeviceDisconnected;

                        lock (nearbyInputDevices)
                        {
                            if (nearbyInputDevices.ContainsKey(endpointId))
                            {
                                nearbyInputDevices.Remove(endpointId);
                            }

                            nearbyInputDevices.Add(endpointId, inputDevice);
                        }
                        MidiManager.Instance.OnMidiInputDeviceAttached(endpointId);
                    }

                    PrepareInputDevice();
                };
            }

            isNearbyInitialized = false;
            NearbyConnectionsManager.Instance.Initialize(() => { isNearbyInitialized = true; });
        }

        public void TerminateMidi()
        {
            NearbyConnectionsManager.Instance.Terminate();
        }

#if UNITY_EDITOR
        public void PlayModeStateChanged(PlayModeStateChange stateChange)
        {
            // do nothing
        }
#endif

        public void Update()
        {
            lock (nearbyInputDevices)
            {
                foreach (var inputDevice in nearbyInputDevices.Values)
                {
                    inputDevice.OnUpdate();
                }
            }
        }

        /// <summary>
        /// Start to scan Nearby MIDI devices
        /// </summary>
        public void StartNearbyDiscovering()
        {
            if (!isNearbyInitialized)
            {
                Debug.LogError("NearbyConnectionsManager initialization is not finished.");
                return;
            }
            NearbyConnectionsManager.Instance.StartDiscovering(NearbyServiceId, NearbyConnectionsManager.Strategy.P2P_CLUSTER);
        }

        /// <summary>
        /// Stop to scan Nearby MIDI devices
        /// </summary>
        public void StopNearbyDiscovering()
        {
            if (!isNearbyInitialized)
            {
                Debug.LogError("NearbyConnectionsManager initialization is not finished.");
                return;
            }
            NearbyConnectionsManager.Instance.StopDiscovering();
        }

        /// <summary>
        /// Start to advertise Nearby MIDI device
        /// </summary>
        public void StartNearbyAdvertising()
        {
            if (!isNearbyInitialized)
            {
                Debug.LogError("NearbyConnectionsManager initialization is not finished.");
                return;
            }
            NearbyConnectionsManager.Instance.StartAdvertising(localEndpointName, NearbyServiceId, NearbyConnectionsManager.Strategy.P2P_CLUSTER);
        }

        /// <summary>
        /// Stop to advertise Nearby MIDI device
        /// </summary>
        public void StopNearbyAdvertising()
        {
            if (!isNearbyInitialized)
            {
                Debug.LogError("NearbyConnectionsManager initialization is not finished.");
            }
            NearbyConnectionsManager.Instance.StopAdvertising();
        }

#if (UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
        /// <summary>
        /// Starts to scan BLE MIDI devices
        /// for Android / iOS / WebGL devices only
        /// </summary>
        /// <param name="timeout">timeout milliseconds, 0 : no timeout</param>
        public void StartScanBluetoothMidiDevices(int timeout)
        {
            // do nothing
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

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Start to advertise BLE MIDI Peripheral device
        /// for Android devices only
        /// </summary>
        /// <exception cref="NotImplementedException">the platform isn't available</exception>
        public void StartAdvertisingBluetoothMidiDevice()
        {
            // do nothing
        }

        /// <summary>
        /// Stop to advertise BLE MIDI Peripheral device
        /// for Android devices only
        /// </summary>
        /// <exception cref="NotImplementedException">the platform isn't available</exception>
        public void StopAdvertisingBluetoothMidiDevice()
        {
            // do nothing
        }
#endif

        public string GetDeviceName(string deviceId)
        {
            // not supported for Nearby
            return null;
        }

        public string GetVendorId(string deviceId)
        {
            // not supported for Nearby
            return null;
        }

        public string GetProductId(string deviceId)
        {
            // not supported for Nearby
            return null;
        }

        public void SendMidiNoteOn(string deviceId, int group, int channel, int note, int velocity)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiNoteOn(channel, note, velocity);
                }
            }
        }

        public void SendMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiNoteOff(channel, note, velocity);
                }
            }
        }

        public void SendMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiPolyphonicAftertouch(channel, note, pressure);
                }
            }
        }

        public void SendMidiControlChange(string deviceId, int group, int channel, int function, int value)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiControlChange(channel, function, value);
                }
            }
        }

        public void SendMidiProgramChange(string deviceId, int group, int channel, int program)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiProgramChange(channel, program);
                }
            }
        }

        public void SendMidiChannelAftertouch(string deviceId, int group, int channel, int pressure)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiChannelAftertouch(channel, pressure);
                }
            }
        }

        public void SendMidiPitchWheel(string deviceId, int group, int channel, int amount)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiPitchWheel(channel, amount);
                }
            }
        }

        public void SendMidiSystemExclusive(string deviceId, int group, byte[] sysEx)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiSystemExclusive(sysEx);
                }
            }
        }

        public void SendMidiSystemCommonMessage(string deviceId, int group, byte[] message)
        {
        }

        public void SendMidiSingleByte(string deviceId, int group, int byte1)
        {
        }

        public void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiTimeCodeQuarterFrame(timing);
                }
            }
        }

        public void SendMidiSongSelect(string deviceId, int group, int song)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiSongSelect(song);
                }
            }
        }

        public void SendMidiSongPositionPointer(string deviceId, int group, int position)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiSongPositionPointer(position);
                }
            }
        }

        public void SendMidiTuneRequest(string deviceId, int group)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiTuneRequest();
                }
            }
        }

        public void SendMidiTimingClock(string deviceId, int group)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiTimingClock();
                }
            }
        }

        public void SendMidiStart(string deviceId, int group)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiStart();
                }
            }
        }

        public void SendMidiContinue(string deviceId, int group)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiContinue();
                }
            }
        }

        public void SendMidiStop(string deviceId, int group)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiStop();
                }
            }
        }

        public void SendMidiActiveSensing(string deviceId, int group)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiActiveSensing();
                }
            }
        }

        public void SendMidiReset(string deviceId, int group)
        {
            lock (nearbyOutputDevices)
            {
                if (nearbyOutputDevices.TryGetValue(deviceId, out var outputDevice))
                {
                    outputDevice.SendMidiReset();
                }
            }
        }

        public void SendMidiMiscellaneousFunctionCodes(string deviceId, int group, int byte1, int byte2, int byte3)
        {
        }

        public void SendMidiCableEvents(string deviceId, int group, int byte1, int byte2, int byte3)
        {
        }
    }
}
#endif