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

    private List<PanelActivation> _activationHistory = new List<PanelActivation>();
    [SerializeField] private float swipeFinishDelay = 0.3f;
    [SerializeField] private float maxSwipeDuration = 2.0f;
    private float _lastTouchTime = 0f;
    private bool _isSwipeInProgress = false;

    private Dictionary<int, float> _panelHoldStartTimes = new Dictionary<int, float>();
    public HashSet<int> _currentlyPressedPanels = new HashSet<int>();
    [SerializeField] private VideoPlaybackController _playbackController;
    [SerializeField] private StateManager stateManager;

    private bool _isSmoothDragSequence = false;
    private List<int> _processedIndices = new List<int>(); // Keep track of processed indices

    public void OnInputReceived(bool[] panelStates)
    {
        float currentTime = Time.time;
        bool anyPressed = false;

        for (int i = 0; i < panelStates.Length; i++)
        {
            if (panelStates[i])
            {
                anyPressed = true;

                _playbackController.OnPanelPressed(i, true);

                if (!_currentlyPressedPanels.Contains(i))
                {
                    _currentlyPressedPanels.Add(i);
                    _panelHoldStartTimes[i] = currentTime;

                    if (_activationHistory.Count > 0 &&
                        _activationHistory[_activationHistory.Count - 1].index == i)
                    {
                        continue;
                    }

                    _activationHistory.Add(new PanelActivation { index = i, time = currentTime });
                    _lastTouchTime = currentTime;

                    if (_activationHistory.Count >= 2 && !_isSwipeInProgress)
                    {
                        float timeDiffBetweenFirstTwo = _activationHistory[1].time - _activationHistory[0].time;
                        _isSmoothDragSequence = timeDiffBetweenFirstTwo > 0.2f;
                        _isSwipeInProgress = true;
                        if (stateManager.CurrentState == AppState.Active)
                        {
                            TryDetectSwipe(true);
                        }
                        else if (stateManager.CurrentState == AppState.Idle)
                        {
                            TryDetectRelativeSwipe();
                        }
                    }
                    else if (_isSwipeInProgress && _activationHistory.Count > 2)
                    {
                        if (stateManager.CurrentState == AppState.Active)
                        {
                            TryDetectSwipe(true);
                        }
                        else if (stateManager.CurrentState == AppState.Idle)
                        {
                            TryDetectRelativeSwipe();
                        }
                    }
                }
            }
            else
            {
                if (_currentlyPressedPanels.Contains(i))
                {
                    _currentlyPressedPanels.Remove(i);
                    _panelHoldStartTimes.Remove(i);
                    _playbackController.OnPanelPressed(i, false);
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

        PanelActivation first = _activationHistory[0];
        PanelActivation last = _activationHistory[_activationHistory.Count - 1];

        if (first.index == last.index && _activationHistory.Count <= 2)
            return;

        Vector2 posFirst = GetPanelPos(first.index);
        Vector2 posLast = GetPanelPos(last.index);
        Vector2 swipeDirection = (posLast - posFirst).normalized;

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
        float computedSpeed = avgDt <= 0f ? 0f : 1f / avgDt;

        SwipeData data = new SwipeData
        {
            direction = swipeDirection,
            speed = computedSpeed,
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

        List<int> uniqueIndices = new List<int>();
        foreach (var activation in _activationHistory)
        {
            if (!uniqueIndices.Contains(activation.index))
            {
                uniqueIndices.Add(activation.index);
            }
        }

        if (uniqueIndices.Count < 2) return; // Need at least two *different* panels

        // --- Forward Swipe Detection (same as before) ---
        for (int i = 0; i < uniqueIndices.Count - 1; i++)
        {
            int startIndex = uniqueIndices[i];
            int endIndex = uniqueIndices[i + 1];

            if (_processedIndices.Contains(startIndex))
            {
                continue;
            }

            int shift = endIndex - startIndex;
            shift = shift > 0 ? 1 : -1;

            RelativeSwipeData data = new RelativeSwipeData
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
            {
                continue; 
            }

            int shift = endIndex - startIndex;
            shift = shift > 0 ? 1 : -1;

            RelativeSwipeData data = new RelativeSwipeData
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
    private Vector2 GetPanelPos(int index)
    {
        int cols = Settings.Instance.cols;
        int row = index / cols;
        int col = index % cols;
        return new Vector2(col, -row);
    }
}