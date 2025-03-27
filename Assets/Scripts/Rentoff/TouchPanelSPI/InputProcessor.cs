/*using System;
using UnityEngine;

public class InputProcessor : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float dataTimeout = 10.0f;
    [SerializeField] private float idleTimeout = 20.0f;

    [Header("References")]
    [SerializeField] private SerialInputReader serialInputReader;
    [SerializeField] private CurtainController curtainController;
    [SerializeField] private SwipeDetector swipeDetector;
    [SerializeField] private StateManager stateManager;

    private float _lastDataReceivedTime;
    private float _lastNonZeroTouchTime;
    private bool _hasTimedOut = false;
    private bool _expectingData = false;

    public event Action OnDataTimeout;

    private void Awake()
    {
        _lastDataReceivedTime = Time.time;
        _lastNonZeroTouchTime = Time.time;
    }

    private void OnEnable()
    {
        if (serialInputReader != null)
        {
            serialInputReader.OnTouchDataReceived += ProcessTouchData;
        }

        if (swipeDetector != null)
        {
            swipeDetector.SwipeDetected += OnSwipeDetected;
        }
    }

    private void OnDisable()
    {
        if (serialInputReader != null)
        {
            serialInputReader.OnTouchDataReceived -= ProcessTouchData;
        }

        if (swipeDetector != null)
        {
            swipeDetector.SwipeDetected -= OnSwipeDetected;
        }
    }

    private void Update()
    {
        // Для тестирования: нажатие Return имитирует свайп в режиме Idle
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (stateManager != null && stateManager.CurrentState == StateManager.AppState.Idle)
            {
                curtainController.AddSwipeProgress(0.3f);
            }
        }

        // Проверка таймаута простоя в режиме Active
        if (stateManager != null &&
            stateManager.CurrentState == StateManager.AppState.Active &&
            (Time.time - _lastNonZeroTouchTime > idleTimeout))
        {
            stateManager.StartTransitionToIdle();
        }

        // Проверка таймаута данных
        if (_expectingData && (Time.time - _lastDataReceivedTime > dataTimeout) && !_hasTimedOut)
        {
            _hasTimedOut = true;
            OnDataTimeout?.Invoke();
            if (stateManager != null && stateManager.CurrentState != StateManager.AppState.Transition)
            {
                serialInputReader.RestartSerialPorts(false);
            }
        }
    }

    private void ProcessTouchData(bool[] panelStates)
    {
        _lastDataReceivedTime = Time.time;
        _expectingData = true;

        if (_hasTimedOut)
        {
            _hasTimedOut = false;
        }

        // Проверяем, есть ли ненулевые касания
        bool hasNonZeroTouch = false;
        foreach (bool state in panelStates)
        {
            if (state)
            {
                hasNonZeroTouch = true;
                break;
            }
        }

        if (hasNonZeroTouch)
        {
            _lastNonZeroTouchTime = Time.time;
        }

        // Передаем данные в зависимости от текущего состояния
        if (stateManager != null && stateManager.CurrentState != StateManager.AppState.Transition)
        {
            serialInputReader.OnInputReceived(panelStates);
        }
        else
        {
            Debug.Log("[InputProcessor] Received input while transitioning: " + stateManager.CurrentState);
        }
    }

    private void OnSwipeDetected(SwipeDetector.SwipeData data)
    {
        // Обрабатываем свайпы только в режиме Idle
        if (stateManager != null &&
            (stateManager.CurrentState == StateManager.AppState.Active || stateManager.CurrentState == StateManager.AppState.Transition))
        {
            Debug.Log("[InputProcessor] Ignoring swipe in current state: " + stateManager.CurrentState);
            return;
        }

        if (data.direction.x <= 0)
        {
            Debug.Log("[InputProcessor] Ignoring backward swipe in Idle mode.");
            return;
        }

        float increment = (data.avgTimeBetween < 0.15f) ? 2.5f :
                          (data.avgTimeBetween < 0.3f) ? 1.0f : 0.5f;

        Debug.Log($"[InputProcessor] Idle swipe detected: avgTimeBetween = {data.avgTimeBetween:F2}s, increment = {increment:F2}");
        curtainController.AddSwipeProgress(increment);
    }
}*/