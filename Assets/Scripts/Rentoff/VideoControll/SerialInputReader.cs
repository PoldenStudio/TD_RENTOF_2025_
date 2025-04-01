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

    private float _lastDataReceivedTime;
    private float _lastNonZeroTouchTime;
    private bool _hasTimedOut = false;
    private bool _expectingData = false;
    private string disableRow = "0";

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

    private void ProcessMessageQueue()
    {
        while (_messageQueue.TryDequeue(out var messageData))
        {
            int portIndex = messageData.portIndex;
            string message = messageData.message;

            _lastDataReceivedTime = Time.time;
            _expectingData = true;

            if (_hasTimedOut)
            {
                _hasTimedOut = false;
            }

            Match match = Regex.Match(message, @"touch_status:?\s*(.*)");
            if (match.Success)
            {
                string data = match.Groups[1].Value.Trim();
                string[] segmentValues = data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                int segments = Settings.Instance.segments;
                int rows = Settings.Instance.rows;
                int cols = Settings.Instance.cols;
                int panelsPerSegment = rows * cols; // Количество панелей в сегменте (должно быть 12, исходя из списка значений)
                int totalPanels = panelsPerSegment * segments * portNames.Length;

                bool[] panelStates = new bool[totalPanels];

                for (int segmentIndex = 0; segmentIndex < segmentValues.Length; segmentIndex++)
                {
                    if (int.TryParse(segmentValues[segmentIndex], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int touchStatus))
                    {
                        if (touchStatus != 0)
                        {
                            _lastNonZeroTouchTime = Time.time;
                        }

                        // Декодируем сумму в индексы нажатых панелей
                        List<int> pressedPanelIndices = DecodeTouchStatus(touchStatus);

                        // Обрабатываем каждый индекс нажатой панели
                        foreach (int localPanelIndex in pressedPanelIndices)
                        {
                            // Инвертируем индекс внутри сегмента
                            int flippedIndex = panelsPerSegment - 1 - localPanelIndex;

                            if (flippedIndex >= 0 && flippedIndex < panelsPerSegment)
                            {
                                // Вычисляем глобальный индекс
                                int globalIndex = portIndex * segments * panelsPerSegment
                                                + segmentIndex * panelsPerSegment
                                                + flippedIndex;

                                if (globalIndex < panelStates.Length)
                                {
                                    panelStates[globalIndex] = true;
                                }
                            }
                        }
                    }
                }

                // Логирование индексов нажатых панелей
                if (EnableDebug)
                {
                    for (int i = 0; i < panelStates.Length; i++)
                    {
                        if (panelStates[i])
                        {
                            int portIdx = i / (segments * panelsPerSegment); // Вычисляем индекс порта
                            int segmentIdx = (i % (segments * panelsPerSegment)) / panelsPerSegment; // Вычисляем индекс сегмента
                            int localIdx = i % panelsPerSegment; // Локальный индекс внутри сегмента
                            Debug.Log($"[SerialInputReader] Panel pressed at global index: {i}, Port: {portNames[portIdx]}, Segment: {segmentIdx}, Local index: {localIdx}");
                        }
                    }
                }

                OnInputReceived(panelStates);
            }
            else
            {
                Debug.Log($"[SerialInputReader] Unhandled message: {message} from {portNames[portIndex]}");
            }
        }
    }

    // Вспомогательная функция для декодирования суммы в индексы нажатых панелей
    private List<int> DecodeTouchStatus(int touchStatus)
    {
        // Список значений, соответствующих индексам панелей
        int[] panelValues = { 1001, 1002, 4, 8, 10, 20, 40, 80, 100, 200, 400, 800 };
        List<int> pressedIndices = new List<int>();

        // Если touchStatus равен 0, значит ничего не нажато
        if (touchStatus == 0)
        {
            return pressedIndices;
        }

        // Проверяем каждую панель, начиная с конца (чтобы правильно обрабатывать большие значения)
        for (int i = panelValues.Length - 1; i >= 0; i--)
        {
            if (touchStatus >= panelValues[i])
            {
                pressedIndices.Add(i); // Добавляем индекс панели
                touchStatus -= panelValues[i]; // Вычитаем значение панели из суммы
            }
        }

        // Если остаток не равен 0, значит данные некорректны
        if (touchStatus != 0)
        {
            Debug.LogWarning($"[SerialInputReader] Unable to fully decode touch status. Remaining value: {touchStatus}");
        }

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


//сейчас у нас неправильно обрабатываются нажатые индексы, нужно чтобы они корректно вычислялись
//каждый сегмент получает данные о нажатых панелях.Вот индексы панелей по порядку 1001, 1002, 4, 8, 10, 20, 40, 80, 100, 200, 400, 800
// также есть суммы панелей, например если нажата 1 и 2 панель, то выведется 1003, если 11 и 12, то C00, важно правильно вычислять нажатые индексы.
//также помни что они должны быть инвертированы в конце вычислений для кажого сегмента

//сейчас принажатии на первую панель, почему то выводит и фиксирует нажатия
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
