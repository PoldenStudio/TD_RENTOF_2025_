using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using InitializationFramework;

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
        // Пример проверок
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (stateManager != null && stateManager.CurrentState == StateManager.AppState.Idle)
            {
                curtainController.AddSwipeProgress(0.3f);
            }
        }

        if (stateManager != null &&
            stateManager.CurrentState == StateManager.AppState.Active &&
            (Time.time - _lastNonZeroTouchTime > idleTimeout))
        {
            stateManager.StartTransitionToIdle();
        }

        // Если порты не открыты, не обрабатываем вход
        if (!IsConnected())
        {
            return;
        }

        // Если ожидали данные, но вышел таймаут — сигнализируем
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
        // Заглушка для тестирования
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
            // Сигнализируем о том, что порт «не открылся»
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
            // Сигнализируем, что порт упал / не открылся
            OnPortDisconnected?.Invoke(portIndex);
        }
    }

    private void StartReadThread(int portIndex)
    {
        _isPortRunning[portIndex] = true;
        _readThreads[portIndex] = new Thread(() => ReadSerialPort(portIndex)) { IsBackground = true };
        _readThreads[portIndex].Start();
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
                        if (rawData != "touch_status: 0" && EnableDebug == true)
                        {
                            Debug.Log($"[SerialInputReader] Data from {portName}: {rawData}");
                        }
                    }
                }
                Thread.Sleep(10);
            }
            catch (TimeoutException)
            {
                // Можем просто игнорировать timeout
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SerialInputReader] Error reading {portName}: {ex.Message}");
                _isPortRunning[portIndex] = false;
                // Сообщаем о падении порта
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

            Match statusMatch = Regex.Match(message, @"touch_status:?\s*(\d+)");
            if (statusMatch.Success && int.TryParse(statusMatch.Groups[1].Value, out int touchStatus))
            {
                if (touchStatus != 0)
                {
                    _lastNonZeroTouchTime = Time.time;
                }

                if (stateManager == null || stateManager.CurrentState != StateManager.AppState.Transition)
                {
                    ProcessActiveInput(message, portIndex);
                }
                else
                {
                    Debug.Log("Received input while transitioning: " + stateManager.CurrentState);
                }
            }
            else if (!string.IsNullOrWhiteSpace(message))
            {
                Debug.Log($"[SerialInputReader] Unhandled message: {message} from {portNames[portIndex]}");
            }
        }
    }

    private void ProcessActiveInput(string message, int portIndex)
    {
        Match match = Regex.Match(message, @"touch_status:?\s*(\d+)");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int touchStatus))
            {
                int numPanelsPerPort = Settings.Instance.rows * Settings.Instance.cols;
                int totalPanels = numPanelsPerPort * portNames.Length;
                bool[] panelStates = new bool[totalPanels];
                Array.Clear(panelStates, 0, panelStates.Length);

                string touchStatusStr = touchStatus.ToString();
                for (int i = 0; i < touchStatusStr.Length; i++)
                {
                    int digit = int.Parse(touchStatusStr[touchStatusStr.Length - 1 - i].ToString());
                    if (digit != 0)
                    {
                        for (int bit = 0; bit < 4; bit++)
                        {
                            if (((digit >> bit) & 1) == 1)
                            {
                                int panelIndex = i * 4 + bit;
                                if (panelIndex < numPanelsPerPort)
                                {
                                    int globalPanelIndex = portIndex * numPanelsPerPort + panelIndex;
                                    panelStates[globalPanelIndex] = true;
                                }
                            }
                        }
                    }
                }
                OnInputReceived(panelStates);
            }
        }
        else if (!string.IsNullOrWhiteSpace(message))
        {
            Debug.Log($"[SerialInputReader] Unhandled message: {message} from {portNames[portIndex]}");
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
        Debug.Log($"[SerialInputReader] Attempting to restart port index {portIndex} ({portNames[portIndex]})");

        // Останавливаем поток
        if (_readThreads != null &&
            _readThreads[portIndex] != null &&
            _readThreads[portIndex].IsAlive)
        {
            _isPortRunning[portIndex] = false;
            _readThreads[portIndex].Join(100);
        }

        // Закрываем и чистим порт
        if (_serialPorts != null &&
            _serialPorts[portIndex] != null &&
            _serialPorts[portIndex].IsOpen)
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

        // Пробуем заново
        OpenSerialPort(portIndex);
        StartReadThread(portIndex);

        Debug.Log($"[SerialInputReader] Port {portNames[portIndex]} restarted.");
    }

    public bool IsPortOpen(int portIndex)
    {
        if (_serialPorts == null ||
            portIndex < 0 ||
            portIndex >= _serialPorts.Length)
        {
            return false;
        }

        var port = _serialPorts[portIndex];
        return (port != null && port.IsOpen);
    }
}