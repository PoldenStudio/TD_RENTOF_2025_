using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;

public class ComPortInput : MonoBehaviour, IInputSource
{
    public string portName = "COM3";
    public int baudRate = 9600;
    private SerialPort serialPort;
    private Thread readThread;
    private bool isRunning = false;
    private string receivedData = "";
    private int numberOfPanels = 20;
    private bool[] panelStates;

    public event Action<int> OnPanelPressed;
    public event Action<int> OnPanelReleased;
    public event Action<Vector2, float> OnSwipeDetected; // COM-порт не поддерживает свайпы
    public bool IsEnabled { get; set; } = true; // ¬ключен по умолчанию

    void Start()
    {
        panelStates = new bool[numberOfPanels];
        if (IsEnabled)
        {
            InitializeComPort();
        }
    }

    void InitializeComPort()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.Open();
            isRunning = true;
            readThread = new Thread(ReadData);
            readThread.Start();
            Debug.Log("COM port opened successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to open COM port: " + e.Message);
            IsEnabled = false; // ќтключаем источник ввода, если не удалось открыть COM-порт
        }
    }

    void ReadData()
    {
        while (isRunning)
        {
            try
            {
                if (serialPort.BytesToRead > 0)
                {
                    receivedData = serialPort.ReadLine();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error reading from COM port: " + e.Message);
            }
        }
    }

    void Update()
    {
        if (IsEnabled)
        {
            ProcessSerialData();
        }
    }

    void ProcessSerialData()
    {
        if (!string.IsNullOrEmpty(receivedData))
        {
            ProcessPanelData(receivedData);
            receivedData = "";
        }
    }

    void ProcessPanelData(string data)
    {
        if (data.Length == numberOfPanels)
        {
            for (int i = 0; i < numberOfPanels; i++)
            {
                bool isPressed = data[i] == '1';
                if (isPressed != panelStates[i])
                {
                    if (isPressed)
                    {
                        OnPanelPressed?.Invoke(i);
                    }
                    else
                    {
                        OnPanelReleased?.Invoke(i);
                    }
                    panelStates[i] = isPressed;
                }
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join();
        }
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
        }
    }
}