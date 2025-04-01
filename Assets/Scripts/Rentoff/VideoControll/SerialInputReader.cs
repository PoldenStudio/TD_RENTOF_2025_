using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using InitializationFramework;
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

    [Header("References")]
    [SerializeField] private CurtainController curtainController;
    [SerializeField] private SwipeDetector swipeDetector;
    [SerializeField] private StateManager stateManager;

    private SerialPort[] _serialPorts;
    private Thread[] _readThreads;
    private volatile bool[] _isPortRunning;
    private ConcurrentQueue<(int portIndex, string message)> _messageQueue = new ConcurrentQueue<(int, string)>();

    //private List<int> mainPanelsCode= new() {1001, 1002, 4, 8, 10, 20, 40, 80, 100, 200, 400, 800};

    private List<string> mainPanelsCode = new() { "1001", "1002", "4", "8", "10", "20", "40", "80", "100", "200", "400", "800" };


    private float _lastDataReceivedTime;
    private float _lastNonZeroTouchTime;
    private bool _hasTimedOut = false;
    private bool _expectingData = false;
    private string disableRow = "0";
    private int PanelsPerSegment;
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
        DisableRow();
        TotalPanelsCalculate();
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
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (stateManager != null && stateManager.CurrentState == StateManager.AppState.Idle)
            {
                curtainController.AddSwipeProgress(0.3f);
            }
        }

        if (stateManager != null && stateManager.CurrentState == StateManager.AppState.Active && (Time.time - _lastNonZeroTouchTime > idleTimeout))
        {
            stateManager.StartTransitionToIdle();
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
        // Not used
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
        Debug.Log("DisableRow generated: " + disableRow);
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
        int segments = Settings.Instance.segments;
        int rows = Settings.Instance.rows;
        int cols = Settings.Instance.cols;
        PanelsPerSegment = rows * cols; // Количество панелей в сегменте (должно быть 12, исходя из списка значений)
        TotalPanels = PanelsPerSegment * segments * portNames.Length;
        Debug.Log("total: " + TotalPanels);
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
                Debug.Log($"[SerialInputReader] Unhandled message: {message} from {portNames[messageData.portIndex]}");
                continue;
            }

            string data = message.Substring(touchStatusIndex + 13).Trim();
            int rows = Settings.Instance.rows;
            int cols = Settings.Instance.cols;
            int panelsPerSegment = rows * cols;
            bool[] panelStates = new bool[TotalPanels];

            int startIndex = 0;
            int segmentIndex = 0;
            int spaceIndex;

            while ((spaceIndex = data.IndexOf(' ', startIndex)) >= 0 && segmentIndex < Settings.Instance.segments)
            {
                string touchStatus = data.Substring(startIndex, spaceIndex - startIndex);
                startIndex = spaceIndex + 1;

                if (touchStatus != "0")
                {
                    _lastNonZeroTouchTime = Time.time;

                    if (touchStatusToIndexMap.TryGetValue(touchStatus, out int panelIndex))
                    {
                        int flippedIndex = panelsPerSegment - panelIndex;
                        int globalIndex = segmentIndex * panelsPerSegment + flippedIndex;

                        if (globalIndex < panelStates.Length)
                        {
                            panelStates[globalIndex] = true;

                            if (EnableDebug)
                            {
                                Debug.Log($"touchStatus {globalIndex}");
                            }
                        }
                    }
                    else
                    {
                        return; // Не найдено совпадение с mainPanelsCode
                        //List <int> Panels = DecodeTouchStatus();
                    }
                }

                segmentIndex++;
            }

            // Обработка последнего сегмента, если есть
            if (startIndex < data.Length && segmentIndex < Settings.Instance.segments)
            {
                string touchStatus = data.Substring(startIndex);

                if (touchStatus != "0")
                {
                    _lastNonZeroTouchTime = Time.time;

                    if (touchStatusToIndexMap.TryGetValue(touchStatus, out int panelIndex))
                    {
                        int flippedIndex = panelsPerSegment - panelIndex;
                        int globalIndex = segmentIndex * panelsPerSegment + flippedIndex;

                        if (globalIndex < panelStates.Length)
                        {
                            panelStates[globalIndex] = true;

                            if (EnableDebug)
                            {
                                Debug.Log($"touchStatus {globalIndex}");
                            }
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }

            // Логирование только если включен дебаг
            if (EnableDebug)
            {
                LogPressedPanels(panelStates, panelsPerSegment);
            }

            OnInputReceived(panelStates);
        }
    }

    // Вынесено в отдельный метод для улучшения читаемости
    private void LogPressedPanels(bool[] panelStates, int panelsPerSegment)
    {
        for (int i = 0; i < panelStates.Length; i++)
        {
            if (panelStates[i])
            {
                int portIdx = i / (Settings.Instance.segments * panelsPerSegment);
                int segmentIdx = (i % (Settings.Instance.segments * panelsPerSegment)) / panelsPerSegment;
                int localIdx = i % panelsPerSegment;
                Debug.Log($"[SerialInputReader] Panel pressed at global index: {i}, Port: {portNames[portIdx]}, Segment: {segmentIdx}, Local index: {localIdx}");
            }
        }
    }

    // Добавьте это поле в класс и инициализируйте при старте
    private Dictionary<string, int> touchStatusToIndexMap;

    // Метод для инициализации словаря
    private void InitializeTouchStatusMap()
    {
        touchStatusToIndexMap = new Dictionary<string, int>(Settings.Instance.rows * Settings.Instance.cols);
        for (int i = 0; i < 12; i++)
        {
            touchStatusToIndexMap[mainPanelsCode[i]] = i + 1;
        }
    }

    // Вспомогательная функция для декодирования суммы в индексы нажатых панелей
    private List<int> DecodeTouchStatus(int touchStatus)
    {
        List<int> pressedIndices = new List<int>();

        return pressedIndices;
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

//сейчас также есть ошибка, что при нажатии на одну  панель, почему то выводит и фиксирует нажатия почти всех панелей на этом сегменте,
//тоесть когда должна фиксироваться одна панель пишет что отобразилось много, это неправильно. Пример что отобращается при нажатии на одну первую панель

//смотри я нажал на панель которая на вход дала 800, она должна вбыть на нулевой точке 
//[SerialInputReader] Panel pressed at global index: 0, Port: COM12, Segment: 0, Local index: 0
//[SerialInputReader] Panel pressed at global index: 1, Port: COM12, Segment: 0, Local index: 1
//[SerialInputReader] Panel pressed at global index: 2, Port: COM12, Segment: 0, Local index: 2
//[SerialInputReader] Panel pressed at global index: 3, Port: COM12, Segment: 0, Local index: 3
//[SerialInputReader] Panel pressed at global index: 4, Port: COM12, Segment: 0, Local index: 4
//[SerialInputReader] Panel pressed at global index: 5, Port: COM12, Segment: 0, Local index: 5
//[SerialInputReader] Panel pressed at global index: 6, Port: COM12, Segment: 0, Local index: 6
//[SerialInputReader] Panel pressed at global index: 7, Port: COM12, Segment: 0, Local index: 7
//[SerialInputReader] Panel pressed at global index: 8, Port: COM12, Segment: 0, Local index: 8
//[SerialInputReader] Panel pressed at global index: 9, Port: COM12, Segment: 0, Local index: 9














//сейчас у нас неправильно обрабатываются нажатые индексы, нужно чтобы они корректно вычислялись
//каждый сегмент получает данные о нажатых панелях.Вот индексы панелей по порядку 1001, 1002, 4, 8, 10, 20, 40, 80, 100, 200, 400, 800
// также есть суммы панелей, например если нажата 1 и 2 панель, то выведется 1003, если 11 и 12, то C00, важно правильно вычислять нажатые индексы.
//также помни что они должны быть инвертированы в конце вычислений для кажого сегмента
//нужно чтобы предрасчитывались все возможные значения сум панелей,
//тоесть все возможные полученные  значения были запечаны и было понятно за какие нажатые панели они отвечают
//тоесть презапекать все возможные значения, например сумма 1001, 1002, 4, 8, равна 100F

//сейчас также есть ошибка, что при нажатии на первую панель, почему то выводит и фиксирует нажатия почти всех панелей на этом сегменте
//[SerialInputReader] Panel pressed at global index: 0, Port: COM12, Segment: 0, Local index: 0
//[SerialInputReader] Panel pressed at global index: 1, Port: COM12, Segment: 0, Local index: 1
//[SerialInputReader] Panel pressed at global index: 2, Port: COM12, Segment: 0, Local index: 2
//[SerialInputReader] Panel pressed at global index: 3, Port: COM12, Segment: 0, Local index: 3
//[SerialInputReader] Panel pressed at global index: 4, Port: COM12, Segment: 0, Local index: 4
//[SerialInputReader] Panel pressed at global index: 5, Port: COM12, Segment: 0, Local index: 5
//[SerialInputReader] Panel pressed at global index: 6, Port: COM12, Segment: 0, Local index: 6
//[SerialInputReader] Panel pressed at global index: 7, Port: COM12, Segment: 0, Local index: 7
//[SerialInputReader] Panel pressed at global index: 8, Port: COM12, Segment: 0, Local index: 8
//[SerialInputReader] Panel pressed at global index: 9, Port: COM12, Segment: 0, Local index: 9
