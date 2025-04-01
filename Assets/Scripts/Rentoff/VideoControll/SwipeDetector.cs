using System;
using System.Collections.Generic;
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

    public event Action<SwipeData> SwipeDetected;
    public event Action<int, bool> PanelPressed;
    public event Action<RelativeSwipeData> RelativeSwipeDetected;

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

    [SerializeField] private VideoPlaybackController _playbackController;
    [SerializeField] private StateManager stateManager;

    private float _lastTouchTime;
    private bool _isSwipeInProgress = false;
    private bool _isSmoothDragSequence = false;
    private List<int> _processedIndices = new();

    public void OnInputReceived(bool[] panelStates)
    {
        float currentTime = Time.time;
        bool anyPressed = false;

        int segmentsCount = Settings.Instance.segments;
        int rows = Settings.Instance.rows;
        int cols = Settings.Instance.cols;
        int panelsPerSegment = rows * cols;

        int segmentsPerPort = segmentsCount;
        int segmentSize = panelsPerSegment;

        int panelsPerPort = segmentsPerPort * segmentSize;
        int portsCount = panelStates.Length / panelsPerPort;

        int virtualPanelsCount = portsCount * panelsPerSegment;

        // Создаем массив для агрегированной информации о виртуальных панелях
        bool[] virtualPanelStates = new bool[virtualPanelsCount];

        // Проходим по всем портам
        for (int port = 0; port < portsCount; port++)
        {
            // Для каждой виртуальной панели
            for (int panelIdx = 0; panelIdx < panelsPerSegment; panelIdx++)
            {
                bool isPressed = false;

                // Для каждого сегмента в порту
                for (int segment = 0; segment < segmentsPerPort; segment++)
                {
                    int segmentOffset = port * panelsPerPort + segment * segmentSize;
                    int idx = segmentOffset + panelIdx;

                    if (panelStates[idx])
                    {
                        isPressed = true;
                        break; // хотя бы один сегмент нажат — вся виртуальная панель считается нажатой
                    }
                }

                int virtualPanelIndex = port * panelsPerSegment + panelIdx;
                virtualPanelStates[virtualPanelIndex] = isPressed;
            }
        }

        // Теперь работаем с виртуальными панелями
        for (int virtualIndex = 0; virtualIndex < virtualPanelsCount; virtualIndex++)
        {
            bool isPressed = virtualPanelStates[virtualIndex];

            if (isPressed)
            {
                anyPressed = true;
                _playbackController.OnPanelPressed(virtualIndex, true);

                if (!_currentlyPressedPanels.Contains(virtualIndex))
                {
                    _currentlyPressedPanels.Add(virtualIndex);
                    _panelHoldStartTimes[virtualIndex] = currentTime;

                    if (_activationHistory.Count > 0 &&
                        _activationHistory[_activationHistory.Count - 1].index == virtualIndex)
                    {
                        continue;
                    }

                    _activationHistory.Add(new PanelActivation { index = virtualIndex, time = currentTime });
                    _lastTouchTime = currentTime;

                    if (_activationHistory.Count >= 2 && !_isSwipeInProgress)
                    {
                        float dt = _activationHistory[1].time - _activationHistory[0].time;
                        _isSmoothDragSequence = dt > 0.2f;
                        _isSwipeInProgress = true;

                        if (stateManager.CurrentState == AppState.Active)
                            TryDetectSwipe(true);
                        else if (stateManager.CurrentState == AppState.Idle)
                            TryDetectRelativeSwipe();
                    }
                    else if (_isSwipeInProgress && _activationHistory.Count > 2)
                    {
                        if (stateManager.CurrentState == AppState.Active)
                            TryDetectSwipe(true);
                        else if (stateManager.CurrentState == AppState.Idle)
                            TryDetectRelativeSwipe();
                    }
                }
            }
            else
            {
                if (_currentlyPressedPanels.Contains(virtualIndex))
                {
                    _currentlyPressedPanels.Remove(virtualIndex);
                    _panelHoldStartTimes.Remove(virtualIndex);
                    _playbackController.OnPanelPressed(virtualIndex, false);
                }
            }
        }

        if (!anyPressed && _isSmoothDragSequence && _isSwipeInProgress)
        {
            _isSwipeInProgress = false;
            _isSmoothDragSequence = false;
            ResetSwipeData();
        }
    }

    private void FixedUpdate()
    {
        float currentTime = Time.time;

        if (_activationHistory.Count > 0 && (currentTime - _lastTouchTime) >= swipeFinishDelay)
        {
            if (_isSwipeInProgress)
            {
                if (stateManager.CurrentState == AppState.Active)
                {
                    TryDetectSwipe(false);
                }
                else if (stateManager.CurrentState == AppState.Idle)
                {
                    TryDetectRelativeSwipe();
                }
                _isSwipeInProgress = false;
            }
            ResetSwipeData();
        }
        else if (_activationHistory.Count > 0 && (currentTime - _activationHistory[0].time) > maxSwipeDuration)
        {
            if (_isSwipeInProgress)
            {
                if (stateManager.CurrentState == AppState.Active)
                {
                    TryDetectSwipe(false);
                }
                else if (stateManager.CurrentState == AppState.Idle)
                {
                    TryDetectRelativeSwipe();
                }
                _isSwipeInProgress = false;
            }
            ResetSwipeData();
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
        if (_activationHistory.Count < 3)
            return;

        _activationHistory.Sort((a, b) => a.time.CompareTo(b.time));

        var first = _activationHistory[0];
        var last = _activationHistory[_activationHistory.Count - 1];

        if (first.index == last.index && _activationHistory.Count <= 2)
            return;

        Vector2 posFirst = GetPanelPos(first.index);
        Vector2 posLast = GetPanelPos(last.index);
        Vector2 swipeDir = (posLast - posFirst).normalized;

        float totalDt = 0f;
        int steps = 0;
        for (int i = 1; i < _activationHistory.Count; i++)
        {
            float dt = _activationHistory[i].time - _activationHistory[i - 1].time;
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
            panelsCount = _activationHistory.Count,
            avgTimeBetween = avgDt,
            isSmoothDrag = _isSmoothDragSequence && avgDt > 0.2f
        };

        SwipeDetected?.Invoke(data);
        _playbackController.OnSwipeDetected(data);
    }

    private void TryDetectRelativeSwipe()
    {
        if (_activationHistory.Count < 2) return;

        _activationHistory.Sort((a, b) => a.time.CompareTo(b.time));

        List<int> uniqueIndices = new();
        foreach (var a in _activationHistory)
        {
            if (!uniqueIndices.Contains(a.index))
                uniqueIndices.Add(a.index);
        }

        if (uniqueIndices.Count < 2) return;

        for (int i = 0; i < uniqueIndices.Count - 1; i++)
        {
            int startIndex = uniqueIndices[i];
            int endIndex = uniqueIndices[i + 1];

            if (_processedIndices.Contains(startIndex))
                continue;

            int shift = endIndex - startIndex;
            shift = shift > 0 ? 1 : -1;

            var data = new RelativeSwipeData
            {
                startIndex = startIndex,
                shift = shift
            };

            Debug.Log($"Relative Swipe: Start={startIndex}, Shift={shift}");
            RelativeSwipeDetected?.Invoke(data);
            _playbackController.OnRelativeSwipeDetected(data);

            _processedIndices.Add(startIndex);
        }

        for (int i = uniqueIndices.Count - 1; i > 0; i--)
        {
            int startIndex = uniqueIndices[i];
            int endIndex = uniqueIndices[i - 1];

            if (_processedIndices.Contains(endIndex))
                continue;

            int shift = endIndex - startIndex;
            shift = shift > 0 ? 1 : -1;

            var data = new RelativeSwipeData
            {
                startIndex = endIndex,
                shift = shift
            };

            Debug.Log($"Relative Swipe (Reverse): Start={endIndex}, Shift={shift}");
            RelativeSwipeDetected?.Invoke(data);
            _playbackController.OnRelativeSwipeDetected(data);
            _processedIndices.Add(endIndex);
        }
    }

    private Vector2 GetPanelPos(int virtualIndex)
    {
        int cols = Settings.Instance.cols;
        int rows = Settings.Instance.rows;

        int totalPanelsPerPort = rows * cols;
        int portCount = (60 / (Settings.Instance.segments * rows * cols));
        int panelsPerPort = totalPanelsPerPort;

        int portIndex = virtualIndex / panelsPerPort;
        int panelIndexInPort = virtualIndex % panelsPerPort;

        int y = panelIndexInPort / cols;
        int x = panelIndexInPort % cols;

        return new Vector2(x + portIndex * cols, -y);
    }
}