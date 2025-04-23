using UnityEngine;
using DemolitionStudios.DemolitionMedia;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static SwipeDetector;
using static StateManager;

public class MainController : MonoBehaviour
{
    [Header("Input")]
    public bool useSerialInput = true;
    public SerialInputReader serialInputReader;
    public TestInputReader testInputReader;
    public MouseInputReader mouseInputReader; // Добавлено поле для MouseInputReader

    [Header("Modules")]
    public SwipeDetector swipeDetector;
    public VideoPlaybackController videoPlaybackController;
    public PanelGridVisualizer panelGridVisualizer;
    public SoundManager soundManager;
    [SerializeField] private StateManager stateManager;

    [Header("Demolition Media")]
    [SerializeField] private Media _demolitionMedia;

    private IMediaPlayer _mediaPlayer;
    private InputReader _activeInputReader;


    [Header("Port Restart Logic")]
    [SerializeField] private int maxRestartAttempts = 3;
    [SerializeField] private float restartInterval = 5f;
    private int[] _portRestartAttempts;
    private Queue<int> _restartQueue = new();
    private bool _isHandlingRestarts = false;

    [Header("Mouse Input Configuration")]
    [SerializeField] private bool enableMouseInput = true; // Управление включением ввода мыши

    private void Start()
    {
        if (_demolitionMedia == null)
        {
            Debug.LogError("[MainController] Demolition Media is not assigned!");
            return;
        }

        _mediaPlayer = new DemolitionMediaPlayer(_demolitionMedia);

        if (videoPlaybackController == null)
        {
            Debug.LogError("[MainController] VideoPlaybackController is not assigned!");
            return;
        }
        videoPlaybackController.Init(_mediaPlayer);

        if (useSerialInput)
        {
            if (serialInputReader == null)
            {
                Debug.LogError("[MainController] SerialInputReader is not assigned!");
                return;
            }
            _activeInputReader = serialInputReader;

        }
        else
        {
            if (testInputReader == null)
            {
                Debug.LogError("[MainController] TestInputReader is not assigned!");
                return;
            }
            _activeInputReader = testInputReader;

            if (panelGridVisualizer != null)
                panelGridVisualizer.SetTestMode(true);
        }

        _activeInputReader.enabled = true;
        serialInputReader.OnPortDisconnected += HandlePortDisconnected;

        // Корректная подписка на все события SwipeDetector
        swipeDetector.SwipeDetected += videoPlaybackController.OnSwipeDetected;
        swipeDetector.RelativeSwipeDetected += videoPlaybackController.OnRelativeSwipeDetected;
        swipeDetector.PanelPressed += videoPlaybackController.OnPanelPressed;

        // Добавляем подписку на события мыши
        swipeDetector.MouseSwipeDetected += videoPlaybackController.OnMouseSwipeDetected;

        // В активном состоянии сразу включаем управление свайпами
        if (stateManager.CurrentState == AppState.Active)
        {
            videoPlaybackController.SetSwipeControlEnabled(true);
            Debug.Log("[MainController] SwipeControl enabled at start");
        }

        if (useSerialInput)
        {
            _activeInputReader.InputReceived += OnInputReceived;
        }
        else
        {
            if (panelGridVisualizer != null)
            {
                panelGridVisualizer.PanelTouched += TestPanelTouched;
                panelGridVisualizer.MouseDragEnded += TestSwipeDetected;
            }
        }

        if (panelGridVisualizer != null)
            panelGridVisualizer.Init(Settings.Instance.rows, Settings.Instance.cols, Settings.Instance.segments);

        if (stateManager != null)
        {
            stateManager.OnStateChanged += DisableOnState;
            stateManager.OnStateChanged += videoPlaybackController.OnStateChanged;
        }

        _mediaPlayer.Play();

        if (serialInputReader.portNames != null)
        {
            _portRestartAttempts = new int[serialInputReader.portNames.Length];
        }

        // Принудительно включаем свайп-контроль для тестирования
        videoPlaybackController.SetSwipeControlEnabled(true);
        Debug.Log("[MainController] SwipeControl enabled at startup for testing");
    }

    public void DisableOnState(AppState state)
    {
        // Более четкое управление подписками в зависимости от состояния
        if (state == AppState.Active)
        {
            // В активном состоянии нам нужны все обработчики
                swipeDetector.SwipeDetected += videoPlaybackController.OnSwipeDetected;

                swipeDetector.MouseSwipeDetected += videoPlaybackController.OnMouseSwipeDetected;

                swipeDetector.PanelPressed += videoPlaybackController.OnPanelPressed;

            // В активном состоянии не нужен RelativeSwipe (для шторки)
            swipeDetector.RelativeSwipeDetected -= videoPlaybackController.OnRelativeSwipeDetected;

            Debug.Log("[MainController] State ACTIVE: Regular swipes, mouse swipes and panel presses enabled");
        }
        else if (state == AppState.Idle)
        {
            // В режиме ожидания нам нужны только события для управления шторкой
            swipeDetector.SwipeDetected -= videoPlaybackController.OnSwipeDetected;

                swipeDetector.MouseSwipeDetected += videoPlaybackController.OnMouseSwipeDetected;

                swipeDetector.RelativeSwipeDetected += videoPlaybackController.OnRelativeSwipeDetected;

            swipeDetector.PanelPressed -= videoPlaybackController.OnPanelPressed;

            Debug.Log("[MainController] State IDLE: Mouse swipes and relative swipes enabled for curtain control");
        }
        else if (state == AppState.Transition)
        {
            // В переходном состоянии отключаем все события
            swipeDetector.SwipeDetected -= videoPlaybackController.OnSwipeDetected;
            swipeDetector.RelativeSwipeDetected -= videoPlaybackController.OnRelativeSwipeDetected;
            swipeDetector.MouseSwipeDetected -= videoPlaybackController.OnMouseSwipeDetected;
            swipeDetector.PanelPressed -= videoPlaybackController.OnPanelPressed;

            Debug.Log("[MainController] State TRANSITION: All swipe detection disabled");
        }

        // Принудительно включаем свайп-контроль для тестирования
        if (state == AppState.Active)
        {
            videoPlaybackController.SetSwipeControlEnabled(true);
            Debug.Log("[MainController] SwipeControl enabled for Active state");
        }
    }

    private void HandlePortDisconnected(int portIndex)
    {
        if (!useSerialInput || _activeInputReader != serialInputReader)
            return;

        if (_portRestartAttempts == null || portIndex < 0 || portIndex >= _portRestartAttempts.Length)
            return;


        if (_portRestartAttempts[portIndex] < maxRestartAttempts)
        {
            _restartQueue.Enqueue(portIndex);

            Debug.Log($"[MainController] Порт {portIndex} планируется к рестарту. " +
                      $"Попытка #{_portRestartAttempts[portIndex] + 1}.");


            if (!_isHandlingRestarts)
            {
                StartCoroutine(HandlePortRestarts());
            }
        }
        else
        {
            Debug.LogWarning($"[MainController] Порт {portIndex} превысил лимит рестартов (>= {maxRestartAttempts}).");
        }
    }

    private IEnumerator HandlePortRestarts()
    {
        _isHandlingRestarts = true;
        Debug.Log("[MainController] HandlePortRestarts coroutine started.");

        while (_restartQueue.Count > 0)
        {
            int portIndex = _restartQueue.Dequeue();


            _portRestartAttempts[portIndex]++;

            Debug.Log($"[MainController] Попытка #{_portRestartAttempts[portIndex]} из {maxRestartAttempts} " +
                      $"для порта {portIndex}.");
            serialInputReader.RestartPort(portIndex);

            yield return new WaitForSeconds(restartInterval);

            if (serialInputReader.IsPortOpen(portIndex))
            {
                Debug.Log($"[MainController] Порт {portIndex} успешно открылся " +
                          $"после {_portRestartAttempts[portIndex]} попыток.");

                _portRestartAttempts[portIndex] = 0;
            }
            else
            {
                if (_portRestartAttempts[portIndex] < maxRestartAttempts)
                {
                    Debug.LogWarning($"[MainController] Порт {portIndex} всё ещё не открыт, " +
                                     $"пытаемся снова (попытка #{_portRestartAttempts[portIndex] + 1}).");
                    _restartQueue.Enqueue(portIndex);
                }
                else
                {
                    Debug.LogError($"[MainController] Порт {portIndex} достиг лимита попыток ({maxRestartAttempts}) " +
                                   "и остаётся закрытым.");
                }
            }
        }

        _isHandlingRestarts = false;
    }

    private void OnInputReceived(bool[] panelStates)
    {
        if (panelGridVisualizer == null) return;
        for (int i = 0; i < panelStates.Length; i++)
        {
            panelGridVisualizer.SetPanelState(i, panelStates[i]);
        }
    }

    private void TestPanelTouched(int index)
    {
        testInputReader.SetPanelState(index, true);

        bool[] currentStates = new bool[Settings.Instance.rows * Settings.Instance.cols];
        System.Array.Copy(testInputReader.testDataArray[0].panelStates, currentStates, currentStates.Length);
        swipeDetector.OnInputReceived(currentStates);
    }

    private void TestSwipeDetected()
    {
        testInputReader.ApplyTestData();
        swipeDetector.OnInputReceived(testInputReader.testDataArray[0].panelStates);
    }

    public void TogglePanelVisualization(bool enabled)
    {
        if (panelGridVisualizer != null)
            panelGridVisualizer.gameObject.SetActive(enabled);
    }

    public void SetInputMode(bool serial)
    {
        if (_activeInputReader != null)
        {
            _activeInputReader.InputReceived -= OnInputReceived;
            _activeInputReader.InputReceived -= swipeDetector.OnInputReceived;
        }

        if (!useSerialInput && panelGridVisualizer != null)
        {
            panelGridVisualizer.PanelTouched -= TestPanelTouched;
            panelGridVisualizer.MouseDragEnded -= TestSwipeDetected;
        }

        if (useSerialInput && serialInputReader != null)
        {
            serialInputReader.OnPortDisconnected -= HandlePortDisconnected;
        }

        useSerialInput = serial;
        if (useSerialInput)
        {
            _activeInputReader = serialInputReader;
            if (panelGridVisualizer != null)
                panelGridVisualizer.SetTestMode(false);
            serialInputReader.OnPortDisconnected += HandlePortDisconnected;
        }
        else
        {
            _activeInputReader = testInputReader;
            if (panelGridVisualizer != null)
                panelGridVisualizer.SetTestMode(true);
        }
        _activeInputReader.enabled = true;

        if (useSerialInput)
        {
            _activeInputReader.InputReceived += OnInputReceived;
            _activeInputReader.InputReceived += swipeDetector.OnInputReceived;
        }
        else
        {
            _activeInputReader.InputReceived += swipeDetector.OnInputReceived;
            if (panelGridVisualizer != null)
            {
                panelGridVisualizer.PanelTouched += TestPanelTouched;
                panelGridVisualizer.MouseDragEnded += TestSwipeDetected;
            }
        }
    }

    // Метод для переключения ввода мыши
    public void ToggleMouseInput(bool enable)
    {
        enableMouseInput = enable;
        if (mouseInputReader != null)
        {
            mouseInputReader.enabled = enable;
            Debug.Log($"[MainController] Mouse input {(enable ? "enabled" : "disabled")}");
        }
    }

    private void OnDestroy()
    {
        if (_mediaPlayer != null)
        {
            videoPlaybackController.SetNormalSpeed();
            _mediaPlayer.Close();
        }

        if (useSerialInput && serialInputReader != null)
        {
            serialInputReader.OnPortDisconnected -= HandlePortDisconnected;
        }

        if (_activeInputReader != null)
        {
            _activeInputReader.InputReceived -= OnInputReceived;
            _activeInputReader.InputReceived -= swipeDetector.OnInputReceived;
        }

        if (!useSerialInput && panelGridVisualizer != null)
        {
            panelGridVisualizer.PanelTouched -= TestPanelTouched;
            panelGridVisualizer.MouseDragEnded -= TestSwipeDetected;
        }

        // Отписка от всех событий

        if (swipeDetector != null && videoPlaybackController != null)
        {
            swipeDetector.SwipeDetected -= videoPlaybackController.OnSwipeDetected;
            swipeDetector.RelativeSwipeDetected -= videoPlaybackController.OnRelativeSwipeDetected;
            swipeDetector.PanelPressed -= videoPlaybackController.OnPanelPressed;
            swipeDetector.MouseSwipeDetected -= videoPlaybackController.OnMouseSwipeDetected;
        }

        if (stateManager != null)
        {
            stateManager.OnStateChanged -= videoPlaybackController.OnStateChanged;
            stateManager.OnStateChanged -= DisableOnState;
        }
    }
}