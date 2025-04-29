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
    [Serializable]
    public class SerialPortConfig
    {
        public string portName = "COM6";
        public int baudRate = 115200;
    }

    public class DataSender : MonoBehaviour
    {
        private static DataSender instance;

        public static DataSender Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<DataSender>();
                    if (instance == null)
                    {
                        GameObject obj = new GameObject("DataSender");
                        instance = obj.AddComponent<DataSender>();
                    }
                }
                return instance;
            }
        }

        [Header("Serial Port Settings")]
        public List<SerialPortConfig> portConfigs = new List<SerialPortConfig>()
        {
            new SerialPortConfig(),
            new SerialPortConfig(),
            new SerialPortConfig(),
            new SerialPortConfig()
        };

        [SerializeField] private bool debugMode = false;
        [SerializeField] private StripDataManager stripManager;

        private List<SerialPort> serialPorts = new List<SerialPort>();
        private float lastSendTime = 0f;
        [Tooltip("Minimum time in seconds between sending data.  Adjust to prevent overwhelming the serial connection.")]
        [SerializeField] public float sendInterval = 0.028f;

        private Dictionary<int, Dictionary<int, string>> previousGlobalData = new();
        private Dictionary<int, Dictionary<int, string>> previousSegmentData = new();

        private Thread[] portThreads;
        private volatile bool[] threadRunning;
        private ConcurrentQueue<string>[] sendQueues;
        private bool transfer = true;

        private object[] serialLocks;

        private readonly StringBuilder globalDataBuilder = new StringBuilder(2048);
        private readonly StringBuilder segmentDataBuilder = new StringBuilder(2048);
        private readonly StringBuilder allDataBuilder = new StringBuilder(4096);

        private readonly string[] dataModePrefixes = new string[4];
        private string clearCommand;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);

                portConfigs.Clear();
                foreach (var config in Settings.Instance.dataSenderPortConfigs)
                {
                    portConfigs.Add(new SerialPortConfig
                    {
                        portName = config.portName,
                        baudRate = config.baudRate
                    });
                }

                portConfigs = portConfigs.GroupBy(pc => pc.portName).Select(g => g.First()).ToList();

                for (int i = 0; i < dataModePrefixes.Length; i++)
                {
                    dataModePrefixes[i] = i.ToString() + ":";
                }

                clearCommand = "0:clear\r\n";

                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            CloseSerialPorts();
        }

        public void Initialize()
        {
            if (serialPorts.Count > 0)
            {
                return;
            }

            var uniqueConfigs = portConfigs;

            int portCount = uniqueConfigs.Count;
            serialPorts.Clear();
            sendQueues = new ConcurrentQueue<string>[portCount];
            threadRunning = new bool[portCount];
            portThreads = new Thread[portCount];
            serialLocks = new object[portCount];
            previousGlobalData.Clear();
            previousSegmentData.Clear();

            for (int i = 0; i < portCount; i++)
            {
                try
                {
                    SerialPort serialPort = new SerialPort(uniqueConfigs[i].portName, uniqueConfigs[i].baudRate)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };

                    serialPort.Open();
                    serialPorts.Add(serialPort);
                    sendQueues[i] = new ConcurrentQueue<string>();
                    serialLocks[i] = new object();

                    if (debugMode)
                        Debug.Log($"[DataSender] Serial port {uniqueConfigs[i].portName} opened successfully.");

                    threadRunning[i] = true;
                    int index = i;
                    portThreads[i] = new Thread(() => SerialThreadLoop(index));
                    portThreads[i].IsBackground = true;
                    portThreads[i].Start();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DataSender] Failed to open serial port {uniqueConfigs[i].portName}: {e.Message}");
                }
            }
        }

        private void SerialThreadLoop(int portIndex)
        {
            while (threadRunning[portIndex])
            {
                try
                {
                    if (serialPorts[portIndex] != null && serialPorts[portIndex].IsOpen && sendQueues[portIndex].TryDequeue(out string dataString))
                    {
                        lock (serialLocks[portIndex])
                        {
                            serialPorts[portIndex].Write(dataString);
                        }
                        if (debugMode)
                            Debug.Log($"[DataSender][Thread] Sent data to {portConfigs[portIndex].portName}: {dataString.Replace("\r\n", "\\r\\n")}");
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DataSender][Thread] Serial port {portConfigs[portIndex].portName} exception: {e.Message}");
                    lock (serialLocks[portIndex])
                    {
                        try { serialPorts[portIndex]?.Close(); } catch { }
                    }

                    Thread.Sleep(500);

                    lock (serialLocks[portIndex])
                    {
                        try { serialPorts[portIndex]?.Open(); } catch { }
                    }
                }
            }
        }


        public void SendString(int portIndex, string row)
        {
            if (portIndex >= 0 && portIndex < serialPorts.Count && IsPortOpen(portIndex))
            {
                lock (serialLocks[portIndex])
                {
                    serialPorts[portIndex].Write(row);
                }
            }
            if (debugMode)
            {
                Debug.Log($"[DataSender] SendString to {portConfigs[portIndex].portName}: {row}");
            }
        }

        public void CloseSerialPorts()
        {
            for (int i = 0; i < threadRunning.Length; i++)
            {
                threadRunning[i] = false;
            }

            for (int i = 0; i < portThreads.Length; i++)
            {
                if (portThreads[i] != null && portThreads[i].IsAlive)
                {
                    portThreads[i].Join(500);
                }
            }

            for (int i = 0; i < serialPorts.Count; i++)
            {
                try
                {
                    if (serialPorts[i] != null && serialPorts[i].IsOpen)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            SendString(i, dataModePrefixes[j] + "clear\r\n");
                        }

                        lock (serialLocks[i])
                        {
                            serialPorts[i].Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataSender] Error closing port {portConfigs[i].portName}: {e.Message}");
                }

                lock (serialLocks[i])
                {
                    if (serialPorts[i] != null)
                    {
                        serialPorts[i].Dispose();
                    }
                }
            }
            serialPorts.Clear();
        }

        public bool IsPortOpen(int portIndex)
        {
            return portIndex >= 0 && portIndex < serialPorts.Count && serialPorts[portIndex] != null && serialPorts[portIndex].IsOpen;
        }

        public bool ShouldSendData()
        {
            return Time.time - lastSendTime >= sendInterval;
        }

        public void EnqueueData(int portIndex, string dataString)
        {
            if (string.IsNullOrEmpty(dataString))
                return;

            if (debugMode)
                Debug.Log($"[DataSender] Enqueuing data to {portConfigs[portIndex].portName}: {dataString.Replace("\r\n", "\\r\\n")}");

            if (IsPortOpen(portIndex))
            {
                sendQueues[portIndex].Enqueue(dataString);
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

            if (!previousGlobalData.TryGetValue(stripIndex, out Dictionary<int, string> stripData))
            {
                stripData = new Dictionary<int, string>();
                previousGlobalData[stripIndex] = stripData;
            }

            int portIndex = stripManager.GetPortIndexForStrip(stripIndex);
            int lastSentPixel = 0;
            string optimizedHex = OptimizeHexString(globalDataBuilder.ToString(), new string('0', hexPerPixel), hexPerPixel, pixelsToGenerate, ref lastSentPixel);

            if (stripData.TryGetValue(portIndex, out string prevHex) && prevHex == optimizedHex)
                return "";

            stripData[portIndex] = optimizedHex;
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

            if (!previousSegmentData.TryGetValue(stripIndex, out Dictionary<int, string> stripData))
            {
                stripData = new Dictionary<int, string>();
                previousSegmentData[stripIndex] = stripData;
            }

            int portIndex = stripManager.GetPortIndexForStrip(stripIndex);
            int lastSentPixel = 0;
            string optimizedHex = OptimizeHexString(segmentDataBuilder.ToString(), new string('0', hexPerPixel), hexPerPixel, totalPixels, ref lastSentPixel);

            if (stripData.TryGetValue(portIndex, out string prevHex) && prevHex == optimizedHex)
                return "";

            stripData[portIndex] = optimizedHex;
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
                        int portIndex = stripManager.GetPortIndexForStrip(i);
                        SendString(portIndex, dataModePrefixes[i] + "clear\r\n");
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

        public void SendAllData(StripDataManager stripManager, SunManager SunManager, EffectsManager effectsManager, ColorProcessor colorProcessor, AppState appState)
        {
            allDataBuilder.Clear();
            for (int i = 0; i < stripManager.totalLEDsPerStrip.Count; i++)
            {
                string stripData = GenerateDataString(i, stripManager, SunManager, effectsManager, colorProcessor, appState);
                if (!string.IsNullOrEmpty(stripData))
                {
                    int portIndex = stripManager.GetPortIndexForStrip(i);
                    EnqueueData(portIndex, stripData);
                }
            }
        }

        public float SendInterval => sendInterval;
    }
}