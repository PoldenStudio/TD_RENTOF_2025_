using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LEDControl;
using System.Linq;
using static StateManager;
using System.Threading;
using System.Collections.Concurrent;
using System.IO.Ports;

namespace LEDControl
{
    public class DataSender : MonoBehaviour
    {
        [Header("Serial Port Settings")]
        [SerializeField] private string portName = "COM6";
        [SerializeField] private int baudRate = 115200;
        [SerializeField] private bool debugMode = false;
        [SerializeField] private StripDataManager stripManager;

        private SerialPort serialPort;
        private float lastSendTime = 0f;
        [Tooltip("Minimum time in seconds between sending data.  Adjust to prevent overwhelming the serial connection.")]
        [SerializeField] private float sendInterval = 0.028f;

        private Dictionary<int, string> previousGlobalData = new();
        private Dictionary<int, string> previousSegmentData = new();

        private Thread portThread;
        private volatile bool threadRunning = false;
        private ConcurrentQueue<string> sendQueue = new ConcurrentQueue<string>();
        private bool transfer = true;

        private object serialLock = new object();

        private readonly StringBuilder globalDataBuilder = new StringBuilder(2048);
        private readonly StringBuilder segmentDataBuilder = new StringBuilder(2048);
        private readonly StringBuilder allDataBuilder = new StringBuilder(4096); 

        private readonly string[] dataModePrefixes = new string[4];
        private string clearCommand;

        void Awake()
        {
            portName = Settings.Instance.dataSenderPortName;
            baudRate = Settings.Instance.dataSenderBaudRate;

            // Pre-format the data mode prefixes
            for (int i = 0; i < dataModePrefixes.Length; i++)
            {
                dataModePrefixes[i] = i.ToString() + ":";
            }

            clearCommand = "0:clear\r\n";

            Initialize();
        }

        void OnDestroy()
        {
            CloseSerialPort();
        }

        public void Initialize()
        {
            try
            {
                if (serialPort == null)
                {
                    serialPort = new SerialPort(portName, baudRate)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };

                    lock (serialLock)
                    {
                        serialPort.Open();
                    }


                    if (debugMode)
                        Debug.Log($"[DataSender] Serial port {portName} opened successfully.");

                    threadRunning = true;
                    portThread = new Thread(SerialThreadLoop);
                    portThread.IsBackground = true;
                    portThread.Start();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataSender] Failed to open serial port {portName}: {e.Message}");
            }
        }

        private void SerialThreadLoop()
        {
            while (threadRunning)
            {
                try
                {
                    if (serialPort != null && serialPort.IsOpen && sendQueue.TryDequeue(out string dataString))
                    {
                        lock (serialLock)
                        {
                            serialPort.Write(dataString);
                        }
                        if (debugMode)
                            Debug.Log($"[DataSender][Thread] Sent data: {dataString.Replace("\r\n", "\\r\\n")}");
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DataSender][Thread] Serial port exception: {e.Message}");
                    lock (serialLock)
                    {
                        try { serialPort?.Close(); } catch { }
                    }


                    Thread.Sleep(500);

                    lock (serialLock)
                    {
                        try { serialPort?.Open(); } catch { }
                    }

                }
            }
        }

        public void SendString(string row)
        {
            if (IsPortOpen())
            {
                lock (serialLock)
                {
                    serialPort.Write(row);
                }
            }
            if (debugMode)
            {
                Debug.Log($"[DataSender] SendString: {row}");
            }
        }

        public void CloseSerialPort()
        {
            threadRunning = false;

            if (portThread != null && portThread.IsAlive)
            {
                portThread.Join(500);
            }

            if (serialPort != null)
            {
                try
                {
                    if (serialPort.IsOpen)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            SendString(dataModePrefixes[i] + "clear\r\n");
                        }

                        lock (serialLock)
                        {
                            serialPort.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataSender] Error closing port: {e.Message}");
                }

                lock (serialLock)
                {
                    serialPort.Dispose();
                }
                serialPort = null;
            }

            if (debugMode)
                Debug.Log("[DataSender] Serial port closed and thread stopped.");
        }

        public bool IsPortOpen()
        {
            return serialPort != null && serialPort.IsOpen;
        }

        public bool ShouldSendData()
        {
            return Time.time - lastSendTime >= sendInterval;
        }

        public void EnqueueData(string dataString)
        {
            if (string.IsNullOrEmpty(dataString))
                return;

            if (debugMode)
                Debug.Log($"[DataSender] Enqueuing data: {dataString.Replace("\r\n", "\\r\\n")}");

            if (IsPortOpen())
            {
                sendQueue.Enqueue(dataString);
                lastSendTime = Time.time;
            }

        }

        private string GetPrefixForDataMode(DataMode mode)
        {
            return dataModePrefixes[(int)mode];
        }

        private string OptimizeHexString(string hexString, string blackHex, int hexPerPixel, int totalPixels, ref int lastSentPixel)
        {
            int changedPixels = totalPixels;
            for (int i = totalPixels - 1; i >= lastSentPixel; i--)
            {
                string pixelHex = hexString.Substring(i * hexPerPixel, hexPerPixel);
                if (!pixelHex.Equals(blackHex, StringComparison.OrdinalIgnoreCase))
                {
                    changedPixels = i + 1;
                    break;
                }
            }
            lastSentPixel = changedPixels;
            return hexString.Substring(0, changedPixels * hexPerPixel);
        }

        public string GetHexDataForGlobalColor(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int pixelsToGenerate = Mathf.Max(stripManager.GetTotalSegments(stripIndex), 1);
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            Color32 globalColor = stripManager.GetGlobalColorForStrip(stripIndex, mode);
            string pixelHex = mode switch
            {
                DataMode.Monochrome1Color or DataMode.Monochrome2Color => colorProcessor.ColorToHexMonochrome(globalColor, stripBrightness, stripGamma, stripGammaEnabled),
                DataMode.RGB => colorProcessor.ColorToHexRGB(globalColor, globalColor, globalColor, stripBrightness, stripGamma, stripGammaEnabled),
                DataMode.RGBW => colorProcessor.ColorToHexRGBW(globalColor, globalColor, globalColor, stripBrightness, stripGamma, stripGammaEnabled),
                _ => ""
            };

            globalDataBuilder.Clear();
            for (int i = 0; i < pixelsToGenerate; i++)
                globalDataBuilder.Append(pixelHex);

            int lastSentPixel = 0;
            string optimizedHex = OptimizeHexString(globalDataBuilder.ToString(), new string('0', hexPerPixel), hexPerPixel, pixelsToGenerate, ref lastSentPixel);

            if (previousGlobalData.TryGetValue(stripIndex, out string prevHex) && prevHex == optimizedHex)
                return "";

            previousGlobalData[stripIndex] = optimizedHex;
            return optimizedHex;
        }

        public string GetHexDataForSegmentColors(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int ledsPerSegment = stripManager.ledsPerSegment[stripIndex];
            int hexPerPixel = (mode == DataMode.RGBW ? 8 : mode == DataMode.RGB ? 6 : 2);
            var segmentColors = stripManager.GetSegmentColors(stripIndex);
            int totalPixels = segmentColors.Count * ledsPerSegment;

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            segmentDataBuilder.Clear();
            foreach (Color32 color in segmentColors)
            {
                string pixelHex = mode switch
                {
                    DataMode.Monochrome1Color or DataMode.Monochrome2Color => colorProcessor.ColorToHexMonochrome(color, stripBrightness, stripGamma, stripGammaEnabled),
                    DataMode.RGB => colorProcessor.ColorToHexRGB(color, color, color, stripBrightness, stripGamma, stripGammaEnabled),
                    DataMode.RGBW => colorProcessor.ColorToHexRGBW(color, color, color, stripBrightness, stripGamma, stripGammaEnabled),
                    _ => ""
                };

                for (int j = 0; j < ledsPerSegment; j++)
                    segmentDataBuilder.Append(pixelHex);
            }

            int lastSentPixel = 0;
            string optimizedHex = OptimizeHexString(segmentDataBuilder.ToString(), new string('0', hexPerPixel), hexPerPixel, totalPixels, ref lastSentPixel);

            if (previousSegmentData.TryGetValue(stripIndex, out string prevHex) && prevHex == optimizedHex)
                return "";

            previousSegmentData[stripIndex] = optimizedHex;
            return optimizedHex;
        }

        public string GenerateDataString(int stripIndex, StripDataManager stripManager, SunManager SunManager, EffectsManager effectsManager, ColorProcessor colorProcessor, AppState appState)
        {
            if (stripIndex < 0 || stripIndex >= stripManager.totalLEDsPerStrip.Count)
            {
                Debug.LogError($"[DataSender] Invalid strip index: {stripIndex}");
                return "";
            }

            DataMode dataMode = stripManager.currentDataModes[stripIndex];
            DisplayMode displayMode = stripManager.currentDisplayModes[stripIndex];

            if (appState == AppState.Idle && displayMode == DisplayMode.SpeedSynthMode)
            {
                return "";
            }

            if (appState == AppState.Transition)
            {
                if (transfer)
                {
                    transfer = false;
                    for (int i = 0; i < 4; i++)
                    {
                        SendString(dataModePrefixes[i] + "clear\r\n");
                    }
                }
            }
            else
            {
                transfer = true;
            }

            string prefix = GetPrefixForDataMode(dataMode);
            string colorData = displayMode switch
            {
                DisplayMode.GlobalColor => GetHexDataForGlobalColor(stripIndex, dataMode, stripManager, colorProcessor),
                DisplayMode.SegmentColors => GetHexDataForSegmentColors(stripIndex, dataMode, stripManager, colorProcessor),
                DisplayMode.SpeedSynthMode when dataMode is DataMode.RGB or DataMode.RGBW
                    => effectsManager.GetHexDataForSpeedSynthMode(stripIndex, dataMode, stripManager, colorProcessor),
                DisplayMode.SunMovement => SunManager.GetHexDataForSunMovement(stripIndex, dataMode, stripManager, colorProcessor),
                _ => GetHexDataForGlobalColor(stripIndex, dataMode, stripManager, colorProcessor)
            };

            if (string.IsNullOrEmpty(colorData))
            {
                return "";
            }

            globalDataBuilder.Clear();
            globalDataBuilder.Append(prefix);
            globalDataBuilder.Append(colorData);
            globalDataBuilder.Append("\r\n");

            return globalDataBuilder.ToString();

        }

        public string GenerateAllDataString(StripDataManager stripManager, SunManager SunManager, EffectsManager effectsManager, ColorProcessor colorProcessor, AppState appState)
        {
            allDataBuilder.Clear();
            for (int i = 0; i < stripManager.totalLEDsPerStrip.Count; i++)
            {
                string stripData = GenerateDataString(i, stripManager, SunManager, effectsManager, colorProcessor, appState);
                if (!string.IsNullOrEmpty(stripData))
                    allDataBuilder.Append(stripData);
            }
            return allDataBuilder.Length > 0 ? allDataBuilder.ToString() : "";
        }

        public float SendInterval => sendInterval;
    }
}