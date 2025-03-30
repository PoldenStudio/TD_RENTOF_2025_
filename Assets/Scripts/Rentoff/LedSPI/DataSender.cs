using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using UnityEngine;
using static StateManager;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;

namespace LEDControl
{
    public class DataSender : MonoBehaviour
    {
        [Header("Serial Port Settings")]
        [SerializeField] private string portName = "COM6";
        [SerializeField] private int baudRate = 115200;
        [SerializeField] private bool debugMode = false;
        [SerializeField] private StripDataManager stripDataManager;

        private SerialPort serialPort;

        private float lastSendTime = 0f;
        private float sendInterval = 0.028f; // 28 мс

        private Dictionary<int, byte[]> previousGlobalData = new();
        private Dictionary<int, byte[]> previousSegmentData = new();

        private Thread portThread;
        private volatile bool threadRunning = false;
        private ConcurrentQueue<byte[]> sendQueue = new ConcurrentQueue<byte[]>();

        private byte[] tempBuffer = new byte[1024]; // Временный буфер для формирования данных

        void Awake()
        {
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
                    if (serialPort != null && serialPort.IsOpen && sendQueue.TryDequeue(out byte[] data))
                    {
                        serialPort.Write(data, 0, data.Length);
                        if (debugMode)
                            Debug.Log($"[DataSender][Thread] Sent data: {BitConverter.ToString(data).Replace("-", "")}");
                    }
                    else
                    {
                        Thread.Sleep(1); // чтобы не грузить процессор
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

        public void EnqueueData(byte[] data)
        {
            if (data == null || data.Length == 0 || !IsPortOpen())
                return;

            sendQueue.Enqueue(data);
            lastSendTime = Time.time;
        }

        private byte[] GetPrefixForDataMode(DataMode mode)
        {
            return Encoding.ASCII.GetBytes(((int)mode).ToString() + ":");
        }

        private byte[] OptimizeHexData(byte[] hexData, byte[] blackHex, int hexPerPixel, int totalPixels, ref int lastSentPixel)
        {
            int changedPixels = totalPixels;
            for (int i = totalPixels - 1; i >= lastSentPixel; i--)
            {
                int startIndex = i * hexPerPixel;
                bool isBlack = true;
                for (int j = 0; j < hexPerPixel; j++)
                {
                    if (hexData[startIndex + j] != blackHex[j])
                    {
                        isBlack = false;
                        break;
                    }
                }
                if (!isBlack)
                {
                    changedPixels = i + 1;
                    break;
                }
            }
            lastSentPixel = changedPixels;
            byte[] optimizedData = new byte[changedPixels * hexPerPixel];
            Array.Copy(hexData, optimizedData, optimizedData.Length);
            return optimizedData;
        }

        public byte[] GetHexDataForGlobalColor(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int pixelsToGenerate = Mathf.Max(stripManager.GetTotalSegments(stripIndex), 1);
            int hexPerPixel = (mode == DataMode.RGBW ? 4 : mode == DataMode.RGB ? 3 : 1);

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            Color32 globalColor = stripManager.GetGlobalColorForStrip(stripIndex, mode);
            byte[] pixelHex = mode switch
            {
                DataMode.Monochrome1Color or DataMode.Monochrome2Color => colorProcessor.ColorToHexMonochrome(globalColor, stripBrightness, stripGamma, stripGammaEnabled),
                DataMode.RGB => colorProcessor.ColorToHexRGB(globalColor, stripBrightness, stripGamma, stripGammaEnabled),
                DataMode.RGBW => colorProcessor.ColorToHexRGBW(globalColor, stripBrightness, stripGamma, stripGammaEnabled),
                _ => new byte[hexPerPixel]
            };

            byte[] hexData = new byte[pixelsToGenerate * hexPerPixel];
            for (int i = 0; i < pixelsToGenerate; i++)
            {
                Array.Copy(pixelHex, 0, hexData, i * hexPerPixel, hexPerPixel);
            }

            int lastSentPixel = 0;
            byte[] optimizedHex = OptimizeHexData(hexData, new byte[hexPerPixel], hexPerPixel, pixelsToGenerate, ref lastSentPixel);

            if (previousGlobalData.TryGetValue(stripIndex, out byte[] prevHex) && optimizedHex.SequenceEqual(prevHex))
                return null;

            previousGlobalData[stripIndex] = optimizedHex;
            return optimizedHex;
        }

        public byte[] GetHexDataForSegmentColors(int stripIndex, DataMode mode, StripDataManager stripManager, ColorProcessor colorProcessor)
        {
            int totalLEDs = stripManager.totalLEDsPerStrip[stripIndex];
            int ledsPerSegment = stripManager.ledsPerSegment;
            int hexPerPixel = (mode == DataMode.RGBW ? 4 : mode == DataMode.RGB ? 3 : 1);
            var segmentColors = stripManager.GetSegmentColors(stripIndex);
            int totalPixels = segmentColors.Count * ledsPerSegment;

            float stripBrightness = stripManager.GetStripBrightness(stripIndex);
            float stripGamma = stripManager.GetStripGamma(stripIndex);
            bool stripGammaEnabled = stripManager.IsGammaCorrectionEnabled(stripIndex);

            byte[] hexData = new byte[totalPixels * hexPerPixel];

            for (int i = 0; i < segmentColors.Count; i++)
            {
                Color32 color = segmentColors[i];
                byte[] pixelHex = mode switch
                {
                    DataMode.Monochrome1Color or DataMode.Monochrome2Color => colorProcessor.ColorToHexMonochrome(color, stripBrightness, stripGamma, stripGammaEnabled),
                    DataMode.RGB => colorProcessor.ColorToHexRGB(color, stripBrightness, stripGamma, stripGammaEnabled),
                    DataMode.RGBW => colorProcessor.ColorToHexRGBW(color, stripBrightness, stripGamma, stripGammaEnabled),
                    _ => new byte[hexPerPixel]
                };

                for (int j = 0; j < ledsPerSegment; j++)
                {
                    Array.Copy(pixelHex, 0, hexData, (i * ledsPerSegment + j) * hexPerPixel, hexPerPixel);
                }
            }

            int lastSentPixel = 0;
            byte[] optimizedHex = OptimizeHexData(hexData, new byte[hexPerPixel], hexPerPixel, totalPixels, ref lastSentPixel);

            if (previousSegmentData.TryGetValue(stripIndex, out byte[] prevHex) && optimizedHex.SequenceEqual(prevHex))
                return null;

            previousSegmentData[stripIndex] = optimizedHex;
            return optimizedHex;
        }

        public byte[] GenerateDataString(int stripIndex, StripDataManager stripManager, EffectsManager effectsManager, ColorProcessor colorProcessor, AppState appState)
        {
            if (stripIndex < 0 || stripIndex >= stripManager.totalLEDsPerStrip.Count)
            {
                Debug.LogError($"[DataSender] Invalid strip index: {stripIndex}");
                return null;
            }

            DataMode dataMode = stripManager.currentDataModes[stripIndex];
            DisplayMode displayMode = stripManager.currentDisplayModes[stripIndex];

            // В режиме Idle и SpeedSynthMode ничего не отправляем
            if (appState == AppState.Idle && displayMode == DisplayMode.SpeedSynthMode)
            {
                return null;
            }

            byte[] prefix = GetPrefixForDataMode(dataMode);
            byte[] colorData = displayMode switch
            {
                DisplayMode.GlobalColor => GetHexDataForGlobalColor(stripIndex, dataMode, stripManager, colorProcessor),
                DisplayMode.SegmentColor => GetHexDataForSegmentColors(stripIndex, dataMode, stripManager, colorProcessor),
                DisplayMode.SpeedSynthMode when dataMode is DataMode.RGB or DataMode.RGBW
                    => effectsManager.GetHexDataForSpeedSynthMode(stripIndex, dataMode, stripManager, colorProcessor),
                DisplayMode.SunMovement => effectsManager.GetHexDataForSunMovement(stripIndex, dataMode, stripManager, colorProcessor),
                _ => GetHexDataForGlobalColor(stripIndex, dataMode, stripManager, colorProcessor)
            };

            if (colorData == null || colorData.Length == 0)
                return null;

            byte[] dataString = new byte[prefix.Length + colorData.Length + 2]; // +2 для \r\n
            Array.Copy(prefix, 0, dataString, 0, prefix.Length);
            Array.Copy(colorData, 0, dataString, prefix.Length, colorData.Length);
            dataString[dataString.Length - 2] = (byte)'\r';
            dataString[dataString.Length - 1] = (byte)'\n';

            return dataString;
        }

        public byte[] GenerateAllDataString(StripDataManager stripManager, EffectsManager effectsManager, ColorProcessor colorProcessor, AppState appState)
        {
            int totalLength = 0;
            foreach (int stripIndex in stripDataManager.totalLEDsPerStrip) // Исправлено обращение к элементам списка
            {
                byte[] stripData = GenerateDataString(stripIndex, stripManager, effectsManager, colorProcessor, appState);
                if (stripData != null)
                {
                    totalLength += stripData.Length;
                }
            }

            if (totalLength == 0)
                return null;

            byte[] fullData = new byte[totalLength];
            int offset = 0;
            foreach (int stripIndex in stripDataManager.totalLEDsPerStrip) // Исправлено обращение к элементам списка
            {
                byte[] stripData = GenerateDataString(stripIndex, stripManager, effectsManager, colorProcessor, appState);
                if (stripData != null)
                {
                    Array.Copy(stripData, 0, fullData, offset, stripData.Length);
                    offset += stripData.Length;
                }
            }

            return fullData;
        }

        public void SendAllData(StripDataManager stripManager, EffectsManager effectsManager, ColorProcessor colorProcessor, AppState appState)
        {
            if (ShouldSendData())
            {
                byte[] totalData = GenerateAllDataString(stripManager, effectsManager, colorProcessor, appState);
                if (totalData != null && totalData.Length > 0)
                    EnqueueData(totalData);
            }
        }
    }
}