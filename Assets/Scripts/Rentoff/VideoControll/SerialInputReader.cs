using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

public class SerialInputReader : InputReader
{
    [Header("Serial Port Settings")]
    [SerializeField] public string[] portNames = { "COM5", "COM6" };
    [SerializeField] private int baudRate = 115200;
    [SerializeField] private float dataTimeout = 10.0f;
    [SerializeField] private float idleTimeout = 20.0f;
    [SerializeField] private float restartDelay = 1.0f;
    [SerializeField] private bool EnableDebug = false;
    [SerializeField] private bool MultipleTouchPerSegmentAsHexBitmask = false;

    [Header("References")]
    [SerializeField] private SwipeDetector swipeDetector;
    [SerializeField] private StateManager stateManager;

    private SerialPort[] _serialPorts;
    private Thread[] _readThreads;
    private volatile bool[] _isPortRunning;
    private ConcurrentQueue<(int portIndex, string message)> _messageQueue = new ConcurrentQueue<(int, string)>();

    //private readonly List<int> singleTouchPanelCodes = new() { 1001, 1002, 4, 8, 10, 20, 40, 80, 100, 200, 400, 800 };
    private readonly List<int> singleTouchPanelCodes = new() { 1001, 1003, 1002, 1006, 4, 12, 8, 18, 10, 30, 20, 60, 40, 120, 80, 180, 100, 300, 200, 600, 400, 1200, 800 };

    private Dictionary<int, int> touchCodeToIndexMap;

    private float _lastDataReceivedTime;
    private float _lastNonZeroTouchTime;
    private bool _hasTimedOut = false;
    private bool _expectingData = false;
    private string disableRow = "0";
    private int PanelsPerSegment;
    private int SegmentsPerPort;
    private int PanelsPerPort;
    private int TotalPanels;

    public event Action<int> OnPortDisconnected;

    public override bool IsConnected()
    {
        return _serialPorts != null && _serialPorts.All(port => port != null && port.IsOpen);
    }

    private void Awake()
    {
        _lastDataReceivedTime = Time.time;
        _lastNonZeroTouchTime = Time.time;
    }

    private void Start()
    {
        TotalPanelsCalculate();
        DisableRow();
        InitializeTouchStatusMap();
    }

    private void OnEnable()
    {
        InitializeSerialPorts();
        _lastDataReceivedTime = Time.time;
        _lastNonZeroTouchTime = Time.time;
    }

    private void OnDisable()
    {
        CloseSerialPorts();
    }

    private void OnDestroy()
    {
        CloseSerialPorts();
    }

    private void Update()
    {

        if (stateManager != null && stateManager.CurrentState == StateManager.AppState.Active && (Time.time - _lastNonZeroTouchTime > idleTimeout))
        {
            //stateManager.StartTransitionToIdle();
        }

        if (!IsConnected())
        {
            return;
        }

        if (_expectingData && (Time.time - _lastDataReceivedTime > dataTimeout) && !_hasTimedOut)
        {
            _hasTimedOut = true;
            if (stateManager != null && stateManager.CurrentState != StateManager.AppState.Transition)
            {
                for (int i = 0; i < portNames.Length; i++)
                {
                    if (!_isPortRunning[i])
                    {
                        OnPortDisconnected?.Invoke(i);
                    }
                }
            }
            return;
        }

        ProcessMessageQueue();
    }

    protected override void ReadInput()
    {
    }

    private void InitializeSerialPorts()
    {
        if (portNames == null || portNames.Length == 0)
        {
            Debug.LogError("[SerialInputReader] No port names specified!");
            return;
        }

        _serialPorts = new SerialPort[portNames.Length];
        _readThreads = new Thread[portNames.Length];
        _isPortRunning = new bool[portNames.Length];

        for (int i = 0; i < portNames.Length; i++)
        {
            OpenSerialPort(i);
            StartReadThread(i);
        }
    }

    private void OpenSerialPort(int portIndex)
    {
        string portName = portNames[portIndex];
        if (string.IsNullOrEmpty(portName) ||
            !Regex.IsMatch(portName, @"^COM\d+$", RegexOptions.IgnoreCase) ||
            !SerialPort.GetPortNames().Contains(portName))
        {
            Debug.LogError($"[SerialInputReader] Invalid or non-existent port: {portName}");
            _isPortRunning[portIndex] = false;
            OnPortDisconnected?.Invoke(portIndex);
            return;
        }

        try
        {
            _serialPorts[portIndex] = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                NewLine = "\n",
                DtrEnable = true,
                RtsEnable = true,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None
            };
            _serialPorts[portIndex].Open();
            _serialPorts[portIndex].DiscardInBuffer();
            _serialPorts[portIndex].DiscardOutBuffer();
            _lastDataReceivedTime = Time.time;
            _expectingData = (stateManager != null && stateManager.CurrentState == StateManager.AppState.Active);
            Debug.Log($"[SerialInputReader] Port {portName} opened.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SerialInputReader] Failed to open {portName}: {ex.Message}");
            _isPortRunning[portIndex] = false;
            OnPortDisconnected?.Invoke(portIndex);
        }
    }

    private void StartReadThread(int portIndex)
    {
        _isPortRunning[portIndex] = true;
        _readThreads[portIndex] = new Thread(() => ReadSerialPort(portIndex)) { IsBackground = true };
        _readThreads[portIndex].Start();
    }

    private void DisableRow()
    {
        int segments = Settings.Instance.segments;
        string[] zeros = Enumerable.Repeat("0", segments).ToArray();
        disableRow = "touch_status: " + string.Join(" ", zeros);
        if (EnableDebug)
            Debug.Log("[SerialInputReader] DisableRow generated: " + disableRow);
    }

    private void ReadSerialPort(int portIndex)
    {
        SerialPort port = _serialPorts[portIndex];
        string portName = portNames[portIndex];
        while (_isPortRunning[portIndex] && port != null && port.IsOpen)
        {
            try
            {
                if (port.BytesToRead > 0)
                {
                    string rawData = port.ReadLine().Trim();
                    if (!string.IsNullOrWhiteSpace(rawData))
                    {
                        _messageQueue.Enqueue((portIndex, rawData));

                        if (rawData != disableRow && EnableDebug)
                        {
                            Debug.Log($"[SerialInputReader] Data from {portName}: {rawData}");
                        }
                    }
                }
                Thread.Sleep(10);
            }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[SerialInputReader] Error reading {portName}: {ex.Message}");
                _isPortRunning[portIndex] = false;
                OnPortDisconnected?.Invoke(portIndex);
                break;
            }
        }
    }

    private void TotalPanelsCalculate()
    {
        SegmentsPerPort = Settings.Instance.segments;
        int rows = Settings.Instance.rows;
        int cols = Settings.Instance.cols;
        PanelsPerSegment = rows * cols;
        PanelsPerPort = PanelsPerSegment * SegmentsPerPort;
        TotalPanels = PanelsPerPort * portNames.Length;
        if (EnableDebug)
            Debug.Log($"[SerialInputReader] PanelsPerSegment: {PanelsPerSegment}, SegmentsPerPort: {SegmentsPerPort}, PanelsPerPort: {PanelsPerPort}, TotalPanels: {TotalPanels}");
    }

    private void ProcessMessageQueue()
    {
        while (_messageQueue.TryDequeue(out var messageData))
        {
            _lastDataReceivedTime = Time.time;
            _expectingData = true;
            _hasTimedOut = false;

            string message = messageData.message;
            int touchStatusIndex = message.IndexOf("touch_status:");

            if (touchStatusIndex < 0)
            {
                if (EnableDebug)
                    Debug.Log($"[SerialInputReader] Unhandled message: {message} from {portNames[messageData.portIndex]}");
                continue;
            }

            string data = message.Substring(touchStatusIndex + "touch_status:".Length).Trim();
            bool[] panelStates = new bool[TotalPanels];
            string[] segmentValues = data.Split(' ');

            for (int segIndex = 0; segIndex < Math.Min(segmentValues.Length, SegmentsPerPort); segIndex++)
            {
                string touchStatusStr = segmentValues[segIndex];
                if (touchStatusStr != "0")
                {
                    _lastNonZeroTouchTime = Time.time;
                    if (MultipleTouchPerSegmentAsHexBitmask)
                    {
                        ProcessMultiTouchStatus(touchStatusStr, messageData.portIndex, segIndex, panelStates);
                    }
                    else
                    {
                        if (int.TryParse(touchStatusStr, out int touchStatus))
                        {
                            ProcessSingleTouchStatus(touchStatus, messageData.portIndex, segIndex, panelStates);
                        }
                    }
                }
            }

            if (EnableDebug)
            {
                LogPressedPanels(panelStates);
            }

            OnInputReceived(panelStates);
        }
    }

    private void ProcessSingleTouchStatus(int touchStatus, int portIndex, int segmentIndex, bool[] panelStates)
    {
        if (touchCodeToIndexMap.TryGetValue(touchStatus, out int panelLocalIdZeroBased))
        {
            // The flip handles hardware reporting order vs internal representation
            int flippedLocalIndex = (PanelsPerSegment - 1) - panelLocalIdZeroBased;

            int globalIndex = portIndex * PanelsPerPort +
                              segmentIndex * PanelsPerSegment +
                              flippedLocalIndex;

            if (globalIndex >= 0 && globalIndex < panelStates.Length)
            {
                panelStates[globalIndex] = true;
                if (EnableDebug)
                {
                    Debug.Log($"[SerialInputReader] Port {portIndex}, Seg {segmentIndex}: Code {touchStatus} -> LocalId {panelLocalIdZeroBased}, FlippedLocal {flippedLocalIndex}, Global {globalIndex}");
                }
            }
            else
            {
                Debug.LogWarning($"[SerialInputReader] GlobalIndex {globalIndex} out of range. TotalPanels: {TotalPanels}");
            }
        }
        else
        {
            //Debug.LogWarning($"[SerialInputReader] Unknown touch status value: {touchStatus}");
        }
    }

    private void ProcessMultiTouchStatus(string touchStatusStr, int portIndex, int segmentIndex, bool[] panelStates)
    {
        if (int.TryParse(touchStatusStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int touchStatusInt))
        {
            List<int> pressed = DecodeTouchBitmask(touchStatusInt, PanelsPerSegment);
            foreach (int zeroBasedLocalIndex in pressed)
            {
                int flippedLocalIndex = (PanelsPerSegment - 1) - zeroBasedLocalIndex;
                int globalIndex = portIndex * PanelsPerPort +
                                  segmentIndex * PanelsPerSegment +
                                  flippedLocalIndex;

                if (globalIndex >= 0 && globalIndex < panelStates.Length)
                {
                    panelStates[globalIndex] = true;
                    if (EnableDebug)
                    {
                        Debug.Log($"[SerialInputReader] Port {portIndex}, Seg {segmentIndex}: Code {touchStatusStr} -> LocalId {zeroBasedLocalIndex}, FlippedLocal {flippedLocalIndex}, Global {globalIndex}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[SerialInputReader] GlobalIndex {globalIndex} out of range. TotalPanels: {TotalPanels}");
                }
            }
        }
        else
        {
            //Debug.LogWarning($"[SerialInputReader] Invalid touch status string: {touchStatusStr}");
        }
    }

    private List<int> DecodeTouchBitmask(int bitmask, int numPanels)
    {
        List<int> pressedIndices = new List<int>();
        for (int i = 0; i < numPanels; i++)
        {
            if ((bitmask & (1 << i)) != 0)
            {
                pressedIndices.Add(i);
            }
        }
        return pressedIndices;
    }

    private void LogPressedPanels(bool[] panelStates)
    {
        for (int i = 0; i < panelStates.Length; i++)
        {
            if (panelStates[i])
            {
                int portIdx = i / PanelsPerPort;
                int withinPortIndex = i % PanelsPerPort;
                int segmentIdx = withinPortIndex / PanelsPerSegment;
                int localIdx = withinPortIndex % PanelsPerSegment;
                Debug.Log($"[SerialInputReader] Panel pressed: GlobalIdx={i}, Port={portIdx}({portNames[portIdx]}), Seg={segmentIdx}, LocalInSeg={localIdx}");
            }
        }
    }

    private void InitializeTouchStatusMap()
    {
        touchCodeToIndexMap = new Dictionary<int, int>(singleTouchPanelCodes.Count);
        for (int i = 0; i < singleTouchPanelCodes.Count; i++)
        {
            // Assuming codes directly map to a 0-based logical order
            touchCodeToIndexMap[singleTouchPanelCodes[i]] = i;
        }
    }

    private void CloseSerialPorts()
    {
        if (_readThreads != null)
        {
            for (int i = 0; i < _readThreads.Length; i++)
            {
                _isPortRunning[i] = false;
                _readThreads[i]?.Join(100);
            }
        }

        if (_serialPorts != null)
        {
            for (int i = 0; i < _serialPorts.Length; i++)
            {
                if (_serialPorts[i] != null)
                {
                    try
                    {
                        if (_serialPorts[i].IsOpen)
                        {
                            _serialPorts[i].DiscardInBuffer();
                            _serialPorts[i].DiscardOutBuffer();
                            _serialPorts[i].Close();
                        }
                        if (EnableDebug)
                            Debug.Log($"[SerialInputReader] Port {portNames[i]} closed.");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[SerialInputReader] Error closing {portNames[i]}: {ex.Message}");
                    }
                    finally
                    {
                        _serialPorts[i]?.Dispose();
                        _serialPorts[i] = null;
                    }
                }
            }
        }
        _expectingData = false;
    }

    public void RestartPort(int portIndex)
    {
        Debug.Log($"[SerialInputReader] Restarting port index {portIndex} ({portNames[portIndex]})");

        if (_readThreads != null && _readThreads[portIndex] != null && _readThreads[portIndex].IsAlive)
        {
            _isPortRunning[portIndex] = false;
            _readThreads[portIndex].Join(100);
        }

        if (_serialPorts != null && _serialPorts[portIndex] != null && _serialPorts[portIndex].IsOpen)
        {
            try
            {
                _serialPorts[portIndex].DiscardInBuffer();
                _serialPorts[portIndex].DiscardOutBuffer();
                _serialPorts[portIndex].Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SerialInputReader] Error closing port {portNames[portIndex]}: {ex.Message}");
            }

            _serialPorts[portIndex].Dispose();
            _serialPorts[portIndex] = null;
        }

        OpenSerialPort(portIndex);
        StartReadThread(portIndex);
        Debug.Log($"[SerialInputReader] Port {portNames[portIndex]} restarted.");
    }

    public bool IsPortOpen(int portIndex)
    {
        if (_serialPorts == null || portIndex < 0 || portIndex >= _serialPorts.Length)
        {
            return false;
        }

        var port = _serialPorts[portIndex];
        return (port != null && port.IsOpen);
    }
    private new void OnInputReceived(bool[] panelStates)
    {
        swipeDetector?.OnInputReceived(panelStates);
    }
}