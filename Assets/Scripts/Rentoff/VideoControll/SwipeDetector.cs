using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static StateManager;

public class SwipeDetector : MonoBehaviour
{
    public struct SwipeData
    {
        public Vector2 direction;
        public float speed;
        public int panelsCount;
        public float avgTimeBetween;
        public bool isSmoothDrag;
    }

    public struct RelativeSwipeData
    {
        public int startIndex;
        public int shift;
    }

    public struct MouseSwipeData
    {
        public Vector2 startPosition;
        public Vector2 endPosition;
        public Vector2 direction;
        public float speed;
        public float duration;
        public float distance;
        public bool isFinalSwipe;
    }

    public struct MouseHoldData
    {
        public Vector2 position;
        public Vector2 endPosition;
        public float duration;
        public bool isStart;
    }

    public event Action<SwipeData> SwipeDetected;
    public event Action<int, bool> PanelPressed;
    public event Action<RelativeSwipeData> RelativeSwipeDetected;
    public event Action<MouseSwipeData> MouseSwipeDetected;
    public event Action<MouseHoldData> MouseHoldDetected;

    private class PanelActivation
    {
        public int index;
        public float time;
    }

    private List<PanelActivation> _activationHistory = new();
    private Dictionary<int, float> _panelHoldStartTimes = new();
    private HashSet<int> _currentlyPressedPanels = new();

    [SerializeField] private float swipeFinishDelay = 0.3f;
    [SerializeField] private float maxSwipeDuration = 2.0f;
    [SerializeField] private float maxTimeBetweenPresses = 0.5f;

    [SerializeField] private VideoPlaybackController _playbackController;
    [SerializeField] private StateManager stateManager;
    [SerializeField] private SerialInputReader _serialInputReader;

    private float _lastTouchTime;
    private bool _isSwipeInProgress = false;
    private bool _isSmoothDragSequence = false;
    private List<int> _processedIndices = new();

    private void Awake()
    {
        if (stateManager == null)
        {
            stateManager = FindObjectOfType<StateManager>();
            if (stateManager == null)
            {
                Debug.LogError("[SwipeDetector] StateManager not assigned and not found in scene!");
            }
        }
    }

    public void OnInputReceived(bool[] panelStates)
    {
        float currentTime = Time.time;
        bool anyPressedThisFrame = false;

        for (int globalIndex = 0; globalIndex < panelStates.Length; globalIndex++)
        {
            bool isPressed = panelStates[globalIndex];

            if (isPressed)
            {
                anyPressedThisFrame = true;
                if (!_currentlyPressedPanels.Contains(globalIndex))
                {
                    PanelPressed?.Invoke(globalIndex, true);
                    _playbackController?.OnPanelPressed(globalIndex, true);
                }

                if (!_currentlyPressedPanels.Contains(globalIndex))
                {
                    _currentlyPressedPanels.Add(globalIndex);
                    _panelHoldStartTimes[globalIndex] = currentTime;

                    if (_activationHistory.Count > 0 &&
                        (currentTime - _activationHistory.Last().time) > maxTimeBetweenPresses)
                    {
                        ResetSwipeData();
                        Debug.Log("[SwipeDetector] Resetting Swipe Data due to time gap");
                    }

                    _activationHistory.Add(new PanelActivation { index = globalIndex, time = currentTime });
                    _lastTouchTime = currentTime;
                    _isSwipeInProgress = true;

                    if (stateManager != null)
                    {
                        stateManager.ResetIdleTimer();
                    }

                    if (stateManager != null && stateManager.CurrentState == AppState.Active)
                    {
                        TryDetectSwipe(false);
                    }
                    else if (stateManager != null && stateManager.CurrentState == AppState.Idle)
                    {
                        TryDetectRelativeSwipe();
                    }
                }
            }
            else
            {
                if (_currentlyPressedPanels.Contains(globalIndex))
                {
                    _currentlyPressedPanels.Remove(globalIndex);
                    _panelHoldStartTimes.Remove(globalIndex);
                    PanelPressed?.Invoke(globalIndex, false);
                    _playbackController?.OnPanelPressed(globalIndex, false);
                }
            }
        }

        if (!anyPressedThisFrame && _isSmoothDragSequence && _isSwipeInProgress)
        {
            _isSwipeInProgress = false;
            _isSmoothDragSequence = false;
        }
    }

    private void FixedUpdate()
    {
        float currentTime = Time.time;

        if (_isSwipeInProgress && _activationHistory.Count > 0)
        {
            if ((currentTime - _lastTouchTime) >= swipeFinishDelay)
            {
                if (stateManager != null && stateManager.CurrentState == AppState.Active)
                {
                    TryDetectSwipe(false);
                }
                else if (stateManager != null && stateManager.CurrentState == AppState.Idle)
                {
                    TryDetectRelativeSwipe();
                }
                _isSwipeInProgress = false;
                ResetSwipeData();
                Debug.Log("[SwipeDetector] Swipe finalized due to inactivity timeout.");
            }
            else if ((currentTime - _activationHistory[0].time) > maxSwipeDuration)
            {
                if (stateManager != null && stateManager.CurrentState == AppState.Active)
                {
                    TryDetectSwipe(false);
                }
                else if (stateManager != null && stateManager.CurrentState == AppState.Idle)
                {
                    TryDetectRelativeSwipe();
                }
                _isSwipeInProgress = false;
                ResetSwipeData();
                Debug.Log("[SwipeDetector] Swipe finalized due to max duration timeout.");
            }
        }
    }

    private void ResetSwipeData()
    {
        _activationHistory.Clear();
        _processedIndices.Clear();
        _isSmoothDragSequence = false;
    }

    private void TryDetectSwipe(bool isInProgress = false)
    {
        if (_activationHistory.Count < 2)
            return;

        _activationHistory.Sort((a, b) => a.time.CompareTo(b.time));

        var validActivations = _activationHistory
            .Where(a => !_processedIndices.Contains(a.index))
            .ToList();

        if (validActivations.Count < 2) return;

        var first = validActivations[0];
        var last = validActivations.Last();

        if (first.index == last.index)
            return;

        Vector2 posFirst = GetPanelPos(first.index);
        Vector2 posLast = GetPanelPos(last.index);
        Vector2 swipeDir = (posLast - posFirst).normalized;

        float totalDt = 0f;
        int steps = 0;
        for (int i = 1; i < validActivations.Count; i++)
        {
            float dt = validActivations[i].time - validActivations[i - 1].time;
            if (dt <= swipeFinishDelay && dt > 0f)
            {
                totalDt += dt;
                steps++;
            }
        }

        float avgDt = steps == 0 ? 0f : totalDt / steps;
        float speed = avgDt <= 0f ? 0f : 1f / avgDt;

        SwipeData data = new SwipeData
        {
            direction = swipeDir,
            speed = speed,
            panelsCount = validActivations.Count,
            avgTimeBetween = avgDt,
            isSmoothDrag = _isSmoothDragSequence
        };

        SwipeDetected?.Invoke(data);
        _playbackController?.OnSwipeDetected(data);
    }

    private void TryDetectRelativeSwipe()
    {
        if (_activationHistory.Count < 2) return;

        _activationHistory.Sort((a, b) => a.time.CompareTo(b.time));

        List<int> uniqueIndices = _activationHistory.Select(a => a.index).Distinct().ToList();
        if (uniqueIndices.Count < 2) return;

        for (int i = 0; i < uniqueIndices.Count - 1; i++)
        {
            int startIndex = uniqueIndices[i];
            int endIndex = uniqueIndices[i + 1];

            int combinedHash = startIndex ^ endIndex;
            if (_processedIndices.Contains(combinedHash))
                continue;

            int shift = endIndex > startIndex ? 1 : -1;

            var data = new RelativeSwipeData
            {
                startIndex = startIndex,
                shift = shift
            };

            Debug.Log($"Relative Swipe: Start={startIndex}, Shift={shift}");
            RelativeSwipeDetected?.Invoke(data);
            _playbackController?.OnRelativeSwipeDetected(data);

            _processedIndices.Add(combinedHash);
        }
    }

    public void ProcessMouseSwipe(Vector2 startPos, Vector2 endPos, float duration, float speed, Vector2 direction, bool isFinal)
    {
        // Теперь обрабатываем все свайпы, и финальные, и промежуточные,
        // сразу генерируя событие SwipeDetected.
        MouseSwipeData data = new()
        {
            startPosition = startPos,
            endPosition = endPos,
            direction = direction,
            speed = speed,
            duration = duration,
            distance = Vector2.Distance(startPos, endPos),
            isFinalSwipe = isFinal
        };

        MouseSwipeDetected?.Invoke(data);

        if (stateManager != null)
        {
            stateManager.ResetIdleTimer();
        }

        // Формируем универсальные данные свайпа
        SwipeData standardSwipe = new()
        {
            direction = direction,
            speed = speed / 1000f,  // поправочный коэффициент
            panelsCount = 2,       // условная "длина" свайпа
            avgTimeBetween = duration / 2f,
            isSmoothDrag = true
        };

        SwipeDetected?.Invoke(standardSwipe);
        _playbackController?.OnSwipeDetected(standardSwipe);
    }

    public void ProcessMouseHold(Vector2 position, Vector2 endPosition, float duration, bool isStart)
    {
        MouseHoldData holdData = new MouseHoldData
        {
            position = position,
            endPosition = endPosition,
            duration = duration,
            isStart = isStart
        };

        MouseHoldDetected?.Invoke(holdData);

        if (isStart && stateManager != null)
        {
            stateManager.ResetIdleTimer();
        }

        if (_playbackController != null)
        {
            _playbackController.OnMouseHoldDetected(holdData);
        }

        Debug.Log($"[SwipeDetector] Mouse hold {(isStart ? "started" : "ended")}: position={position}, duration={duration:F2}s");
    }

    private Vector2 GetPanelPos(int globalIndex)
    {
        if (Settings.Instance == null)
        {
            Debug.LogError("Settings.Instance is null!");
            return Vector2.zero;
        }

        int cols = Settings.Instance.cols;
        int rows = Settings.Instance.rows;
        int segmentsPerPort = Settings.Instance.segments;

        if (cols <= 0 || rows <= 0 || segmentsPerPort <= 0)
        {
            Debug.LogError($"Invalid settings: cols={cols}, rows={rows}, segments={segmentsPerPort}");
            return Vector2.zero;
        }

        int panelsPerSegment = cols * rows;
        int panelsPerPort = panelsPerSegment * segmentsPerPort;
        int numPorts = (_serialInputReader != null && _serialInputReader.portNames != null) ? _serialInputReader.portNames.Length : 1;

        if (panelsPerSegment <= 0 || panelsPerPort <= 0)
        {
            Debug.LogError($"panelsPerSegment={panelsPerSegment} or panelsPerPort={panelsPerPort} is zero or negative, cannot calculate panel position.");
            return Vector2.zero;
        }

        int portIndex = globalIndex / panelsPerPort;
        int indexInPort = globalIndex % panelsPerPort;
        int segmentIndex = indexInPort / panelsPerSegment;
        int indexInSegment = indexInPort % panelsPerSegment;

        int localY = indexInSegment / cols;
        int localX = indexInSegment % cols;

        float globalX = localX + segmentIndex * cols + portIndex * cols * segmentsPerPort;
        float globalY = -localY;

        return new Vector2(globalX, globalY);
    }
}