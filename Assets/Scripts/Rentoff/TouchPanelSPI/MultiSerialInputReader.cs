/*using InitializationFramework;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Video;

public class MultiSerialInputReader : MonoBehaviour
{
    [Header("Serial Port Settings")]
    [SerializeField] private List<string> portNames = new List<string> { "COM3", "COM4" }; // List of COM ports
    [SerializeField] private int baudRate = 115200;
    [SerializeField] private float dataTimeout = 10.0f;
    [SerializeField] private float idleTimeout = 20.0f;

    private Dictionary<string, SerialPortReader> _portReaders = new Dictionary<string, SerialPortReader>();
    private bool[] _aggregatedPanelStates;
    private float _lastNonZeroTouchTime;
    private bool _isTransitioning = false;

    public event Action<bool[]> InputReceived;
    public event Action OnEnterActiveMode;
    public event Action OnEnterIdleMode;

    private enum Mode { Idle, Active }
    private Mode _currentMode = Mode.Idle;

    private void Awake()
    {
        int numPanels = Settings.Instance.rows * Settings.Instance.cols;
        _aggregatedPanelStates = new bool[numPanels];
        _lastNonZeroTouchTime = Time.time;

        foreach (var portName in portNames)
        {
            var reader = new SerialPortReader(portName, baudRate, dataTimeout);
            reader.DataReceived += OnDataReceivedFromPort;
            _portReaders[portName] = reader;
        }
    }

    private void OnEnable()
    {
        foreach (var reader in _portReaders.Values)
        {
            reader.Start();
        }
        SetMode(Mode.Idle); // Start in idle mode
    }

    private void OnDisable()
    {
        foreach (var reader in _portReaders.Values)
        {
            reader.Stop();
        }
    }

    private void OnDestroy()
    {
        foreach (var reader in _portReaders.Values)
        {
            reader.Dispose();
        }
    }

    private void Update()
    {
        // Idle timeout check when in Active mode and not transitioning
        if (_currentMode == Mode.Active && !_isTransitioning && Time.time - _lastNonZeroTouchTime > idleTimeout)
        {
            Debug.Log($"[MultiSerialInputReader] Switching to Idle due to inactivity for {idleTimeout} seconds.");
            SetMode(Mode.Idle);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ToggleModeViaKey();
        }
    }

    private void SetMode(Mode newMode)
    {
        if (_currentMode == newMode || _isTransitioning) return;

        _isTransitioning = true;

        if (newMode == Mode.Active)
        {
            StartCoroutine(SwitchToActiveModeRoutine());
        }
        else
        {
            StartCoroutine(SwitchToIdleModeRoutine());
        }
    }

    private System.Collections.IEnumerator SwitchToIdleModeRoutine()
    {
        OnEnterIdleMode?.Invoke();
        Debug.Log("[MultiSerialInputReader] Switching to Idle Mode");

        if (fadeScreen != null) yield return StartCoroutine(fadeScreen.FadeOut());
        if (videoPlayer != null) yield return StartCoroutine(videoPlayer.SwitchToIdleMode());
        if (fadeScreen != null) yield return StartCoroutine(fadeScreen.FadeIn());

        _currentMode = Mode.Idle;
        _isTransitioning = false;
    }

    private System.Collections.IEnumerator SwitchToActiveModeRoutine()
    {
        OnEnterActiveMode?.Invoke();
        Debug.Log("[MultiSerialInputReader] Switching to Active Mode");

        if (fadeScreen != null) yield return StartCoroutine(fadeScreen.FadeOut());
        if (videoPlayer != null) yield return StartCoroutine(videoPlayer.SwitchToActiveMode());
        if (fadeScreen != null) yield return StartCoroutine(fadeScreen.FadeIn());

        _currentMode = Mode.Active;
        _isTransitioning = false;
    }

    private void OnDataReceivedFromPort(string portName, bool[] panelStates)
    {
        // Aggregate panel states from all ports
        for (int i = 0; i < panelStates.Length; i++)
        {
            _aggregatedPanelStates[i] |= panelStates[i];
        }

        bool hasNonZeroTouch = Array.Exists(_aggregatedPanelStates, state => state);
        if (hasNonZeroTouch)
        {
            _lastNonZeroTouchTime = Time.time;
        }

        if (_currentMode == Mode.Idle && hasNonZeroTouch)
        {
            SetMode(Mode.Active);
        }

        InputReceived?.Invoke(_aggregatedPanelStates.Clone() as bool[]);
    }

    private void ToggleModeViaKey()
    {
        if (_currentMode == Mode.Active)
        {
            SetMode(Mode.Idle);
        }
        else
        {
            SetMode(Mode.Active);
        }
    }
}

public class SerialPortReader : IDisposable
{
    private SerialPort _serialPort;
    private Thread _readThread;
    private bool _stopThread = false;
    private readonly float _dataTimeout;
    private float _lastDataReceivedTime;

    public event Action<string, bool[]> DataReceived;

    public SerialPortReader(string portName, int baudRate, float dataTimeout)
    {
        _serialPort = new SerialPort(portName, baudRate)
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

        _dataTimeout = dataTimeout;
        _lastDataReceivedTime = Time.time;
    }

    public void Start()
    {
        try
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
                Debug.Log($"[SerialPortReader] Port {_serialPort.PortName} opened.");
            }

            _stopThread = false;
            _readThread = new Thread(ReadSerialData) { IsBackground = true };
            _readThread.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SerialPortReader] Failed to start port {_serialPort.PortName}: {ex.Message}");
        }
    }

    public void Stop()
    {
        _stopThread = true;
        if (_readThread != null && _readThread.IsAlive)
        {
            _readThread.Join(1000); // Wait up to 1 second for thread to stop
        }
    }

    public void Dispose()
    {
        Stop();
        _serialPort?.Dispose();
    }

    private void ReadSerialData()
    {
        while (!_stopThread)
        {
            try
            {
                if (_serialPort.IsOpen && _serialPort.BytesToRead > 0)
                {
                    string rawData = _serialPort.ReadLine().Trim();
                    _lastDataReceivedTime = Time.time;

                    bool[] panelStates = ProcessMessage(rawData);
                    UnityMainThreadDispatcher.Instance.Enqueue(() => DataReceived?.Invoke(_serialPort.PortName, panelStates));
                }

                if (Time.time - _lastDataReceivedTime > _dataTimeout)
                {
                    Debug.LogWarning($"[SerialPortReader] Data timeout on port {_serialPort.PortName}.");
                    // Handle timeout (e.g., restart port)
                }

                Thread.Sleep(10); // Reduce CPU usage
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SerialPortReader] Error on port {_serialPort.PortName}: {ex.Message}");
            }
        }
    }

    private bool[] ProcessMessage(string message)
    {
        int numPanels = Settings.Instance.rows * Settings.Instance.cols;
        bool[] panelStates = new bool[numPanels];

        Match match = Regex.Match(message, @"touch_status:?\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int touchStatus))
        {
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
                            if (panelIndex < numPanels)
                            {
                                panelStates[panelIndex] = true;
                            }
                        }
                    }
                }
            }
        }

        return panelStates;
    }
}

public static class UnityMainThreadDispatcher
{
    private static readonly Queue<Action> _actionQueue = new Queue<Action>();

    public static void Enqueue(Action action)
    {
        lock (_actionQueue)
        {
            _actionQueue.Enqueue(action);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
        UnityEngine.Object.DontDestroyOnLoad(dispatcherObject);
        dispatcherObject.AddComponent<DispatcherHandler>();
    }

    private class DispatcherHandler : MonoBehaviour
    {
        private void Update()
        {
            while (_actionQueue.Count > 0)
            {
                Action action;
                lock (_actionQueue)
                {
                    action = _actionQueue.Dequeue();
                }
                action?.Invoke();
            }
        }
    }
}*/