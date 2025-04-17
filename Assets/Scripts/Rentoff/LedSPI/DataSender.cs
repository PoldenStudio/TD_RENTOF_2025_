using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using UnityEngine;
using static StateManager;
using System.Threading;
using System.Collections.Concurrent;

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
        // Интервал отправки (например, 28 мс)
        private float sendInterval = 0.028f;

        private Dictionary<int, string> previousGlobalData = new();
        private Dictionary<int, string> previousSegmentData = new();

        // Поля для многопоточности
        private Thread portThread;
        private volatile bool threadRunning = false;
        private ConcurrentQueue<string> sendQueue = new ConcurrentQueue<string>();
        private bool transfer = true;

        void Awake()
        {
            portName = Settings.Instance.dataSenderPortName;
            baudRate = Settings.Instance.dataSenderBaudRate;

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
                    serialPort.Open();

                    if (debugMode)
                        Debug.Log($"[DataSender] Serial port {portName} opened successfully.");

                    // Запускаем поток для отправки
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
                        serialPort.Write(dataString);
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
                    try { serialPort?.Close(); } catch { }

                    Thread.Sleep(500);
                    try { serialPort?.Open(); } catch { }
                }
            }
        }

        public void SendString(string row)
        {
            serialPort.Write(row);
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
                            serialPort.Write(i + ":clear\r\n");
                        }
                        serialPort.Close();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataSender] Error closing port: {e.Message}");
                }
                serialPort.Dispose();
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
            return Time.time - lastSendTime > sendInterval;
        }

        public void EnqueueData(string dataString)
        {
            if (string.IsNullOrEmpty(dataString) || !IsPortOpen())
                return;

            sendQueue.Enqueue(dataString);
            lastSendTime = Time.time;
        }

        private string GetPrefixForDataMode(DataMode mode)
        {
            return ((int)mode).ToString() + ":";
        }

        /// <summary>
        /// Оптимизирует hex-строку, обрезая пиксели, у которых данные равны значению blackHex (например, "00").
        /// </summary>
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

            StringBuilder sb = new StringBuilder(pixelsToGenerate * hexPerPixel);
            for (int i = 0; i < pixelsToGenerate; i++)
                sb.Append(pixelHex);

            int lastSentPixel = 0;
            string optimizedHex = OptimizeHexString(sb.ToString(), new string('0', hexPerPixel), hexPerPixel, pixelsToGenerate, ref lastSentPixel);

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
            StringBuilder sb = new StringBuilder(totalPixels * hexPerPixel);

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
                    sb.Append(pixelHex);
            }

            int lastSentPixel = 0;
            string optimizedHex = OptimizeHexString(sb.ToString(), new string('0', hexPerPixel), hexPerPixel, totalPixels, ref lastSentPixel);

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

                if (transfer == true)
                {
                    transfer = false;
                    for (int i = 0; i < 4; i++)
                    {
                        serialPort.Write(i + ":clear\r\n");
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

            return string.IsNullOrEmpty(colorData) ? "" : $"{prefix}{colorData}\r\n";
        }

        public string GenerateAllDataString(StripDataManager stripManager, SunManager SunManager, EffectsManager effectsManager, ColorProcessor colorProcessor, AppState appState)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < stripManager.totalLEDsPerStrip.Count; i++)
            {
                string stripData = GenerateDataString(i, stripManager, SunManager, effectsManager, colorProcessor, appState);
                if (!string.IsNullOrEmpty(stripData))
                    sb.Append(stripData);
            }
            return sb.Length > 0 ? sb.ToString() : "";
        }
        public float SendInterval
        {
            get { return sendInterval; }
        }
    }
}