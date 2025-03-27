using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;

public class PanelInputService : MonoBehaviour
{
    [Header("COM Port Settings")]
    public string portName = "COM3";
    public int baudRate = 9600;
    public bool simulateInput = true; // Если true, используем тестовый массив данных

    private SerialPort serialPort;
    private Thread readThread;
    private bool isRunning = false;
    private string receivedData = "";

    [Header("Panel Grid Settings")]
    public int numberOfRows = 3;
    public int numberOfColumns = 4;
    public int numberOfPanels;
    private bool[] panelStates;
    private float[] panelPressStartTime;

    [Header("Swipe Settings")]
    public float swipeHoldDuration = 3.0f;
    public float swipeCooldown = 0.5f;
    private float lastSwipeTime;
    private int lastSwipeDirection = 0; // -1: swipe left, 1: swipe right, 0: none

    [Header("Test Data (simulateInput=true)")]
    [Tooltip("Строки должны содержать ровно 12 символов (0 или 1)")]
    public string[] testInputData;
    public float testDataInterval = 2.0f; // каждые 2 сек подается новая строка
    private int testDataIndex = 0;
    private float lastTestDataTime = 0f;

    // События
    public event Action<int> OnPanelPressed;
    public event Action<int> OnPanelReleased;
    public event Action OnSwipeLeft;
    public event Action OnSwipeRight;

    void Start()
    {
        numberOfPanels = numberOfRows * numberOfColumns;
        panelStates = new bool[numberOfPanels];
        panelPressStartTime = new float[numberOfPanels];

        if (!simulateInput)
        {
            try
            {
                serialPort = new SerialPort(portName, baudRate);
                serialPort.Open();
                isRunning = true;

                readThread = new Thread(ReadData);
                readThread.Start();

                Debug.Log(portName + " port opened successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to open COM port: " + e.Message);
            }
        }
    }

    void Update()
    {
        if (simulateInput && testInputData != null && testInputData.Length > 0)
        {
            // По интервалу подаем тестовые данные
            if (Time.time - lastTestDataTime > testDataInterval)
            {
                receivedData = testInputData[testDataIndex];
                testDataIndex = (testDataIndex + 1) % testInputData.Length;
                lastTestDataTime = Time.time;
            }
        }

        ProcessReceivedData();
        HandleSwipe();
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

    void ProcessReceivedData()
    {
        // Ожидаем строку длиной numberOfPanels символов
        if (!string.IsNullOrEmpty(receivedData) && receivedData.Length >= numberOfPanels)
        {
            // Можно брать лишь первые numberOfPanels символов, если данные длиннее
            string data = receivedData.Substring(0, numberOfPanels);
            for (int i = 0; i < numberOfPanels; i++)
            {
                if (data[i] == '1')
                {
                    // Если панель была не нажата, генерируем событие нажатия
                    if (!panelStates[i])
                    {
                        OnPanelPressed?.Invoke(i);
                        Debug.Log($"PanelInputService: Panel {i + 1} Pressed");
                        // Запускаем таймер удержания для крайних панелей
                        if (i == 0 || i == numberOfPanels - 1)
                        {
                            panelPressStartTime[i] = Time.time;
                        }
                    }
                    panelStates[i] = true;
                }
                else if (data[i] == '0')
                {
                    if (panelStates[i])
                    {
                        OnPanelReleased?.Invoke(i);
                        Debug.Log($"PanelInputService: Panel {i + 1} Released");
                        panelPressStartTime[i] = 0; // сброс таймера для данной панели
                    }
                    panelStates[i] = false;
                }
            }
            receivedData = "";
        }
    }

    void HandleSwipe()
    {
        // Проверка задержки между свайпами
        if (Time.time - lastSwipeTime < swipeCooldown)
            return;

        // Определяем свайп справа: крайняя правая панель
        int rightPanelIndex = numberOfPanels - 1;
        if (panelStates[rightPanelIndex])
        {
            if (panelPressStartTime[rightPanelIndex] > 0 &&
                Time.time - panelPressStartTime[rightPanelIndex] > swipeHoldDuration &&
                lastSwipeDirection != 1)
            {
                Debug.Log("PanelInputService: Swipe Right Detected");
                OnSwipeRight?.Invoke();
                lastSwipeTime = Time.time;
                lastSwipeDirection = 1;
            }
        }

        // Определяем свайп слева: первая панель
        if (panelStates[0])
        {
            if (panelPressStartTime[0] > 0 &&
                Time.time - panelPressStartTime[0] > swipeHoldDuration &&
                lastSwipeDirection != -1)
            {
                Debug.Log("PanelInputService: Swipe Left Detected");
                OnSwipeLeft?.Invoke();
                lastSwipeTime = Time.time;
                lastSwipeDirection = -1;
            }
        }

        // Если крайние панели отпущены, сбрасываем направление свайпа
        if (!panelStates[0] && !panelStates[rightPanelIndex])
            lastSwipeDirection = 0;
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