using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using UnityEngine;
using static StateManager;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;

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

        private const int MaxPacketSize = 2048; // Максимальный размер пакета для отправки

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
                    if (serialPort != null && serialPort.IsOpen)
                    {
                        if (sendQueue.TryDequeue(out byte[] data))
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
                    else
                    {
                        Thread.Sleep(100); // Пауза, если порт закрыт
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DataSender][Thread] Serial port exception: {e.Message}");
                    TryReconnectSerialPort();
                }
            }
        }

        private void TryReconnectSerialPort()
        {
            try
            {
                serialPort?.Close();
                Thread.Sleep(500);
                serialPort?.Open();
                if (debugMode)
                    Debug.Log($"[DataSender][Thread] Serial port {portName} reconnected successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataSender][Thread] Failed to reconnect serial port {portName}: {e.Message}");
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
                        for (int i = 0; i < stripDataManager.totalLEDsPerStrip.Count; i++)
                        {
                            serialPort.Write(Encoding.ASCII.GetBytes(i + ":clear\r\n"), 0, Encoding.ASCII.GetBytes(i + ":clear\r\n").Length);
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

        private byte[] OptimizeHexData(byte[] hexData, byte[] blackHex)
        {
            int hexPerPixel = blackHex.Length;
            int totalPixels = hexData.Length / hexPerPixel;
            int lastSentPixel = totalPixels;
            while (lastSentPixel > 0)
            {
                int startIndex = (lastSentPixel - 1) * hexPerPixel;
                if (new Span<byte>(hexData, startIndex, hexPerPixel).SequenceEqual(blackHex))
                {
                    lastSentPixel--;
                }
                else
                {
                    break;
                }
            }
            byte[] optimizedData = new byte[lastSentPixel * hexPerPixel];
            Buffer.BlockCopy(hexData, 0, optimizedData, 0, optimizedData.Length);
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
            byte[] pixelHex = new byte[hexPerPixel];
            Span<byte> pixelHexSpan = pixelHex.AsSpan();
            switch (mode)
            {
                case DataMode.Monochrome1Color:
                case DataMode.Monochrome2Color:
                    colorProcessor.ColorToHexMonochrome(globalColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHexSpan);
                    break;
                case DataMode.RGB:
                    colorProcessor.ColorToHexRGB(globalColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHexSpan);
                    break;
                case DataMode.RGBW:
                    colorProcessor.ColorToHexRGBW(globalColor, stripBrightness, stripGamma, stripGammaEnabled, pixelHexSpan);
                    break;
            }

            byte[] hexData = new byte[pixelsToGenerate * hexPerPixel];
            for (int i = 0; i < pixelsToGenerate; i++)
            {
                Buffer.BlockCopy(pixelHex, 0, hexData, i * hexPerPixel, hexPerPixel);
            }

            byte[] optimizedHex = OptimizeHexData(hexData, new byte[hexPerPixel]);

            if (previousGlobalData.TryGetValue(stripIndex, out byte[] prevHex) && optimizedHex.AsSpan().SequenceEqual(prevHex))
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
                byte[] pixelHex = new byte[hexPerPixel];
                Span<byte> pixelHexSpan = pixelHex.AsSpan();
                switch (mode)
                {
                    case DataMode.Monochrome1Color:
                    case DataMode.Monochrome2Color:
                        colorProcessor.ColorToHexMonochrome(color, stripBrightness, stripGamma, stripGammaEnabled, pixelHexSpan);
                        break;
                    case DataMode.RGB:
                        colorProcessor.ColorToHexRGB(color, stripBrightness, stripGamma, stripGammaEnabled, pixelHexSpan);
                        break;
                    case DataMode.RGBW:
                        colorProcessor.ColorToHexRGBW(color, stripBrightness, stripGamma, stripGammaEnabled, pixelHexSpan);
                        break;
                }

                for (int j = 0; j < ledsPerSegment; j++)
                {
                    Buffer.BlockCopy(pixelHex, 0, hexData, (i * ledsPerSegment + j) * hexPerPixel, hexPerPixel);
                }
            }

            byte[] optimizedHex = OptimizeHexData(hexData, new byte[hexPerPixel]);

            if (previousSegmentData.TryGetValue(stripIndex, out byte[] prevHex) && optimizedHex.AsSpan().SequenceEqual(prevHex))
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
            byte[] colorData = null;
            switch (displayMode)
            {
                case DisplayMode.GlobalColor:
                    colorData = GetHexDataForGlobalColor(stripIndex, dataMode, stripManager, colorProcessor);
                    break;
                case DisplayMode.SegmentColor:
                    colorData = GetHexDataForSegmentColors(stripIndex, dataMode, stripManager, colorProcessor);
                    break;
                case DisplayMode.SpeedSynthMode when dataMode is DataMode.RGB or DataMode.RGBW:
                    colorData = effectsManager.GetHexDataForSpeedSynthMode(stripIndex, dataMode, stripManager, colorProcessor);
                    break;
                case DisplayMode.SunMovement:
                    colorData = effectsManager.GetHexDataForSunMovement(stripIndex, dataMode, stripManager, colorProcessor);
                    break;
                default:
                    colorData = GetHexDataForGlobalColor(stripIndex, dataMode, stripManager, colorProcessor);
                    break;
            }


            if (colorData == null || colorData.Length == 0)
                return null;

            byte[] dataString = new byte[prefix.Length + colorData.Length + 2]; // +2 для \r\n
            Buffer.BlockCopy(prefix, 0, dataString, 0, prefix.Length);
            Buffer.BlockCopy(colorData, 0, dataString, prefix.Length, colorData.Length);
            dataString[dataString.Length - 2] = (byte)'\r';
            dataString[dataString.Length - 1] = (byte)'\n';

            return dataString;
        }

        public void SendAllData(StripDataManager stripManager, EffectsManager effectsManager, ColorProcessor colorProcessor, AppState appState)
        {
            if (ShouldSendData())
            {
                int totalLength = 0;
                List<byte[]> dataPackets = new List<byte[]>();
                for (int i = 0; i < stripManager.totalLEDsPerStrip.Count; ++i)
                {
                    if (!stripManager.stripEnabled[i]) continue;
                    byte[] stripData = GenerateDataString(i, stripManager, effectsManager, colorProcessor, appState);
                    if (stripData != null && stripData.Length > 0)
                    {
                        dataPackets.Add(stripData);
                        totalLength += stripData.Length;
                    }
                }

                if (totalLength > 0)
                {
                    byte[] combinedData = new byte[Mathf.Min(totalLength, MaxPacketSize)];
                    int offset = 0;
                    foreach (byte[] packet in dataPackets)
                    {
                        int bytesToCopy = Mathf.Min(packet.Length, combinedData.Length - offset);
                        if (bytesToCopy > 0)
                        {
                            Buffer.BlockCopy(packet, 0, combinedData, offset, bytesToCopy);
                            offset += bytesToCopy;
                            if (offset >= combinedData.Length)
                            {
                                EnqueueData(combinedData);
                                combinedData = new byte[Mathf.Min(totalLength - offset, MaxPacketSize)];
                                offset = 0;
                            }
                        }
                    }
                    if (offset > 0)
                    {
                        byte[] finalPacket = new byte[offset];
                        Buffer.BlockCopy(combinedData, 0, finalPacket, 0, offset);
                        EnqueueData(finalPacket);
                    }
                }
            }
        }
    }
}