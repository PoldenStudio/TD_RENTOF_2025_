#if (!UNITY_IOS && !UNITY_WEBGL) || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net;
using jp.kshoji.rtpmidi;
using UnityEditor;
using UnityEngine;

namespace jp.kshoji.unity.midi
{
    public class RtpMidiPlugin : IMidiPlugin
    {


        private int port = 5004;
        public void SetPort(int newPort)
        {
            port = newPort;
            Debug.Log($"[MIDI] RtpMidiPlugin port set to: {port}");
        }

        public void InitializeMidi(Action onInitialized)
        {
            try
            {
                Debug.Log("[MIDI] RtpMidiPlugin initialization started...");

                if (!InitializeNetwork(port))
                {
                    Debug.LogError("[MIDI] RtpMidiPlugin failed to initialize network.");
                    return;
                }

                Debug.Log("[MIDI] RtpMidiPlugin initialization completed.");
                onInitialized?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MIDI] RtpMidiPlugin initialization error: {ex.Message}");
            }
        }

        private bool InitializeNetwork(int port)
        {
            Debug.Log($"[MIDI] Attempting to connect to RTP-MIDI network on port: {port}");

            return true;
        }



        private readonly Dictionary<int, RtpMidiServer> rtpMidiServers = new Dictionary<int, RtpMidiServer>();
        private RtpMidiEventHandler rtpMidiEventHandler = new RtpMidiEventHandler();




        private class RtpMidiEventHandler : IRtpMidiEventHandler
        {
            public void OnMidiNoteOn(string deviceId, int channel, int note, int velocity)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiNoteOn((string)((object[])o)[0], 0, (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3]), new object[] {deviceId, channel, note, velocity});
            public void OnMidiNoteOff(string deviceId, int channel, int note, int velocity)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiNoteOff((string)((object[])o)[0], 0, (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3]), new object[] {deviceId, channel, note, velocity});
            public void OnMidiPolyphonicAftertouch(string deviceId, int channel, int note, int pressure)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiPolyphonicAftertouch((string)((object[])o)[0], 0, (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3]), new object[] {deviceId, channel, note, pressure});
            public void OnMidiControlChange(string deviceId, int channel, int function, int value)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiControlChange((string)((object[])o)[0], 0, (int)((object[])o)[1], (int)((object[])o)[2], (int)((object[])o)[3]), new object[] {deviceId, channel, function, value});
            public void OnMidiProgramChange(string deviceId, int channel, int program)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiProgramChange((string)((object[])o)[0], 0, (int)((object[])o)[1], (int)((object[])o)[2]), new object[] {deviceId, channel, program});
            public void OnMidiChannelAftertouch(string deviceId, int channel, int pressure)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiChannelAftertouch((string)((object[])o)[0], 0, (int)((object[])o)[1], (int)((object[])o)[2]), new object[] {deviceId, channel, pressure});
            public void OnMidiPitchWheel(string deviceId, int channel, int amount)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiPitchWheel((string)((object[])o)[0], 0, (int)((object[])o)[1], (int)((object[])o)[2]), new object[] {deviceId, channel, amount});
            public void OnMidiSystemExclusive(string deviceId, byte[] systemExclusive)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiSystemExclusive((string)((object[])o)[0], 0, (byte[])((object[])o)[1]), new object[] {deviceId, systemExclusive});
            public void OnMidiTimeCodeQuarterFrame(string deviceId, int timing)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTimeCodeQuarterFrame((string)((object[])o)[0], 0, (int)((object[])o)[1]), new object[] {deviceId, timing});
            public void OnMidiSongSelect(string deviceId, int song)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiSongSelect((string)((object[])o)[0], 0, (int)((object[])o)[1]), new object[] {deviceId, song});
            public void OnMidiSongPositionPointer(string deviceId, int position)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiSongPositionPointer((string)((object[])o)[0], 0, (int)((object[])o)[1]), new object[] {deviceId, position});
            public void OnMidiTuneRequest(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTuneRequest((string)o, 0), deviceId);
            public void OnMidiTimingClock(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiTimingClock((string)o, 0), deviceId);
            public void OnMidiStart(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiStart((string)o, 0), deviceId);
            public void OnMidiContinue(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiContinue((string)o, 0), deviceId);
            public void OnMidiStop(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiStop((string)o, 0), deviceId);
            public void OnMidiActiveSensing(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiActiveSensing((string)o, 0), deviceId);
            public void OnMidiReset(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o => MidiManager.Instance.OnMidiReset((string)o, 0), deviceId);
        }

        private class RtpMidiDeviceConnectionListener : IRtpMidiDeviceConnectionListener{
            public void OnRtpMidiDeviceAttached(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o =>
                {
                    MidiManager.Instance.OnMidiInputDeviceAttached((string)o);
                    MidiManager.Instance.OnMidiOutputDeviceAttached((string)o);
                }, deviceId);

            public void OnRtpMidiDeviceDetached(string deviceId)
                => MidiManager.Instance.asyncOperation.Post(o =>
                {
                    MidiManager.Instance.OnMidiInputDeviceDetached((string)o);
                    MidiManager.Instance.OnMidiOutputDeviceDetached((string)o);
                }, deviceId);
        }

        private readonly RtpMidiDeviceConnectionListener rtpMidiDeviceConnectionListener = new RtpMidiDeviceConnectionListener();

        /// <summary>
        /// Initializes MIDI Plugin system
        /// </summary>
        /// <param name="initializeCompletedAction"></param>
/*        public void InitializeMidi(Action initializeCompletedAction)
        {
        }*/

        /// <summary>
        /// Terminates MIDI Plugin system
        /// </summary>
        public void TerminateMidi()
        {
            StopAllRtpMidi();
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
        /// Starts RTP MIDI Listener
        /// </summary>
        /// <param name="sessionName">the name of session</param>
        /// <param name="listenPort">UDP port number(0-65534)</param>
        public void StartRtpMidiServer(string sessionName, int listenPort)
        {
            lock (rtpMidiServers)
            {
                RtpMidiServer rtpMidiServer = null;
                if (rtpMidiServers.TryGetValue(listenPort, out var server))
                {
                    rtpMidiServer = server;
                }

                if (rtpMidiServer == null)
                {
                    // starts RTP MIDI server with UDP specified control port
                    rtpMidiServer = new RtpMidiServer(sessionName, listenPort, rtpMidiDeviceConnectionListener, rtpMidiEventHandler);
                    rtpMidiServer.Start();
                    rtpMidiServers[listenPort] = rtpMidiServer;
                }
            }
        }

        /// <summary>
        /// Check RTP MIDI Listener is running
        /// </summary>
        /// <param name="listenPort">UDP port number(0-65534)</param>
        public bool IsRtpMidiRunning(int listenPort)
        {
            lock (rtpMidiServers)
            {
                if (rtpMidiServers.TryGetValue(listenPort, out var rtpMidiServer))
                {
                    return rtpMidiServer != null && rtpMidiServer.IsStarted();
                }

                return false;
            }
        }

        /// <summary>
        /// Stops RTP MIDI Listener with the specified port
        /// </summary>
        public void StopRtpMidi(int listenPort)
        {
            lock (rtpMidiServers)
            {
                if (rtpMidiServers.TryGetValue(listenPort, out var rtpMidiServer))
                {
                    rtpMidiServer?.Stop();
                    rtpMidiServers.Remove(listenPort);
                }
            }
        }

        /// <summary>
        /// Stops all RTP MIDI servers
        /// </summary>
        public void StopAllRtpMidi()
        {
            lock (rtpMidiServers)
            {
                foreach (var rtpMidiServer in rtpMidiServers.Values)
                {
                    rtpMidiServer?.Stop();
                }
                rtpMidiServers.Clear();
            }
        }

        /// <summary>
        /// Initiate RTP MIDI Connection with specified IPEndPoint
        /// </summary>
        /// <param name="sessionName">the name of session</param>
        /// <param name="listenPort">port to listen</param>
        /// <param name="ipEndPoint">IP address and port to connect with</param>
        public void ConnectToRtpMidiServer(string sessionName, int listenPort, IPEndPoint ipEndPoint)
        {
            lock (rtpMidiServers)
            {
                RtpMidiServer rtpMidiServer = null;
                if (rtpMidiServers.TryGetValue(listenPort, out var server))
                {
                    rtpMidiServer = server;
                }

                if (rtpMidiServer == null)
                {
                    StartRtpMidiServer(sessionName, listenPort);
                    rtpMidiServer = rtpMidiServers[listenPort];
                }

                rtpMidiServer.ConnectToListener(ipEndPoint);
            }
        }

        /// <summary>
        /// Obtains device name for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetDeviceName(string deviceId)
        {
            lock (rtpMidiServers)
            {
                foreach (var rtpMidiServer in rtpMidiServers)
                {
                    var deviceName = rtpMidiServer.Value.GetDeviceName(deviceId);
                    if (!string.IsNullOrEmpty(deviceName))
                    {
                        return deviceName;
                    }
                }
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
            // not supported for RtpMIDI
            return null;
        }

        /// <summary>
        /// Obtains device product id for deviceId
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public string GetProductId(string deviceId)
        {
            // not supported for RtpMIDI
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiNoteOn(deviceId, channel, note, velocity);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiNoteOff(deviceId, channel, note, velocity);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiPolyphonicAftertouch(deviceId, channel, note, pressure);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiControlChange(deviceId, channel, function, value);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var server))
                {
                    server.SendMidiProgramChange(deviceId, channel, program);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiChannelAftertouch(deviceId, channel, pressure);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiPitchWheel(deviceId, channel, amount);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiSystemExclusive(deviceId, sysEx);
                }
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Sends a System Common message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="message">byte array</param>
        public void SendMidiSystemCommonMessage(string deviceId, int group, byte[] message)
        {
            // do nothing
        }

        /// <summary>
        /// Sends a Single Byte message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="byte1">0-255</param>
        public void SendMidiSingleByte(string deviceId, int group, int byte1)
        {
            // do nothing
        }
#endif

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the Device Id</param>
        /// <param name="group">0-15</param>
        /// <param name="timing">0-127</param>
        public void SendMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
        {
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiTimeCodeQuarterFrame(deviceId, timing);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiSongSelect(deviceId, song);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiSongPositionPointer(deviceId, position);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiTuneRequest(deviceId);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiTimingClock(deviceId);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiStart(deviceId);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiContinue(deviceId);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiStop(deviceId);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiActiveSensing(deviceId);
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
            lock (rtpMidiServers)
            {
                var port = RtpMidiSession.GetPortFromDeviceId(deviceId);
                if (rtpMidiServers.TryGetValue(port, out var rtpMidiServer))
                {
                    rtpMidiServer.SendMidiReset(deviceId);
                }
            }
        }
        
#if UNITY_ANDROID && !UNITY_EDITOR
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
            // do nothing
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
            // do nothing
        }
#endif
    }
}
#endif
