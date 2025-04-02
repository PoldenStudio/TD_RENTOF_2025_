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
    [SerializeField] private float maxTimeBetweenPresses = 0.5f; // NEW: Max time between presses to consider it a swipe

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

        int segmentsPerPort = Settings.Instance.segments;
        int rows = Settings.Instance.rows;
        int cols = Settings.Instance.cols;
        int panelsPerSegment = rows * cols; // Panels in one hardware segment
        int panelsPerPort = panelsPerSegment * segmentsPerPort;
        int portsCount = panelStates.Length / panelsPerPort;

        // Virtual panels aggregate touches across segments at the same relative position.
        // Layout: All panels of one segment type from all ports side-by-side.
        int virtualPanelsCount = cols * rows * portsCount;
        bool[] virtualPanelStates = new bool[virtualPanelsCount];

        for (int prt = 0; prt < portsCount; prt++)
        {
            for (int pIdx = 0; pIdx < panelsPerSegment; pIdx++) // Local index within a segment
            {
                bool isPressedOnAnySegment = false;
                for (int seg = 0; seg < segmentsPerPort; seg++)
                {
                    int globalIndex = prt * panelsPerPort + seg * panelsPerSegment + pIdx;
                    if (globalIndex < panelStates.Length && panelStates[globalIndex])
                    {
                        isPressedOnAnySegment = true;
                        break;
                    }
                }

                int virtualPanelIndex = prt * panelsPerSegment + pIdx;
                if (virtualPanelIndex < virtualPanelStates.Length)
                {
                    virtualPanelStates[virtualPanelIndex] = isPressedOnAnySegment;
                }
            }
        }

        for (int virtualIndex = 0; virtualIndex < virtualPanelStates.Length; virtualIndex++)
        {
            bool isPressed = virtualPanelStates[virtualIndex];

            if (isPressed)
            {
                anyPressed = true;
                PanelPressed?.Invoke(virtualIndex, true);
                _playbackController?.OnPanelPressed(virtualIndex, true);

                if (!_currentlyPressedPanels.Contains(virtualIndex))
                {
                    _currentlyPressedPanels.Add(virtualIndex);
                    _panelHoldStartTimes[virtualIndex] = currentTime;

                    //Important change here: only add to history if the time difference is small enough
                    if (_activationHistory.Count > 0 &&
                        (currentTime - _activationHistory.Last().time) > maxTimeBetweenPresses)
                    {
                        //If the time difference is too large, reset the swipe data to start fresh
                        ResetSwipeData();
                        Debug.Log("Resetting Swipe Data due to time gap");
                    }

                    // Add the activation to the history
                    _activationHistory.Add(new PanelActivation { index = virtualIndex, time = currentTime });
                    _lastTouchTime = currentTime;

                    _isSwipeInProgress = true;  // Immediately consider it a swipe in progress

                    if (stateManager.CurrentState == AppState.Active)
                    {
                        TryDetectSwipe(false);
                    }
                    else if (stateManager.CurrentState == AppState.Idle)
                    {
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
                    PanelPressed?.Invoke(virtualIndex, false);
                    _playbackController?.OnPanelPressed(virtualIndex, false);
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

        //Check for timeout to finish swipe
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
        //Check for swipe duration timeout
        else if (_activationHistory.Count > 0 && (currentTime - _activationHistory[0].time) > maxSwipeDuration)
        {
            if (_isSwipeInProgress)
            {
                TryDetectSwipe(false);  // Detect and send the swipe one last time
                TryDetectRelativeSwipe();  // Detect and send relative swipe one last time
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
            isSmoothDrag = _isSmoothDragSequence && avgDt > 0.2f
        };

        SwipeDetected?.Invoke(data);
        _playbackController?.OnSwipeDetected(data);

        if (!isInProgress)
        {
            foreach (var act in validActivations)
                _processedIndices.Add(act.index);
        }
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

    private Vector2 GetPanelPos(int virtualIndex)
    {
        int cols = Settings.Instance.cols;
        int rows = Settings.Instance.rows;
        int panelsPerSegment = cols * rows;

        int portIndex = virtualIndex / panelsPerSegment;
        int localIndex = virtualIndex % panelsPerSegment;

        int y = localIndex / cols;
        int x = localIndex % cols;

        Vector2 pos = new Vector2(x + portIndex * cols, -y);
        Debug.Log($"GetPanelPos: virtualIndex={virtualIndex}, portIndex={portIndex}, localIndex={localIndex}, x_local={x}, y_local={y}, calculated_pos={pos}");
        return pos;
    }
}