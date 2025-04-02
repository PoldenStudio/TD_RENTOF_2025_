using UnityEngine;
using DemolitionStudios.DemolitionMedia;
using TMPro;
using static SwipeDetector;
using LEDControl;
using static StateManager;
using System.Collections.Generic;
using System.Linq;

public class VideoPlaybackController : MonoBehaviour
{
    private enum PlaybackState
    {
        Normal,
        Accelerating,
        Decelerating,
        HoldAccelerating
    }

    [SerializeField] private bool restoreSpeedAfterHold = false;
    [SerializeField] private bool returnToZeroOnRelease = true;

    private PlaybackState _state = PlaybackState.Normal;
    private PlaybackState _previousState;
    private IMediaPlayer _mediaPlayer;

    [SerializeField] private Settings settings;


    private float _currentSpeed = 1f;
    private float _immediateTargetSpeed = 1f;
    private float _effectStartSpeed = 1f;
    private float _finalTargetSpeed = 1f;

    private float _accelerationDuration = 0f;
    private float _decelerationDuration = 0f;
    private float _phaseTimer = 0f;

    private float _effectStartTime = 0f;
    private float _accumulatedTimeDelta = 0f;

    private float _holdStartTime = 0f;
    private float _holdDuration = 0f;
    private int _heldPanelIndex = -1;
    private bool _isPanelHoldActive = false;
    private const float HOLD_THRESHOLD = 1f;

    private const float MIN_DECELERATION_DURATION = 1f;
    private const float MAX_DECELERATION_DURATION = 4f;
    private const float MAX_DECELERATION_DURATION_FROM_HOLD = 2f;


    [SerializeField] private TextMeshProUGUI debugText;
    private SwipeData _lastSwipeData;

    private const float maxTargetSpeed = 13f;
    private const float minSlowSpeedAmount = 0.25f;
    private const float skipTime = 0.25f;
    private const float startfromTime = 0.3f;
    private bool _isReversePlayback = false;

    private const float MAX_REVERSE_SPEED = -13f;

    private float _lastPanelReleaseTime = 0f;
    private const float HOLD_SAFETY_TIMEOUT = 0.5f;

    [SerializeField] private SoundManager _SoundManager;
    [SerializeField] private MIDISoundManager _MIDISoundManager;
    [SerializeField] private StateManager stateManager;
    [SerializeField] private CurtainController curtainController;
    [SerializeField] private LEDController _LEDController;
    [SerializeField] private SPItouchPanel _SPItouchPanel;

    private bool _swipeControlEnabled = false;

    private readonly float _transitionDelay = 0.1f;

    private float _velocity = 0f;
    private float _smoothTime = 0.15f;
    private bool _reachedZero = false;
    private float _previousSpeed = 0f;
    private float _holdZeroTime = 0f;
    private bool _isHolding = false;

    [Header("Swipe Timings")]
    [Tooltip("Average time between touches greater than this will be considered a SLOW swipe.")]
    [SerializeField] private float slowSwipeTimeThreshold = 0.1f;
    [Tooltip("Average time between touches less than or equal to this will be considered a FAST swipe.")]
    [SerializeField] private float fastSwipeTimeThreshold = 0.05f;
    [Tooltip("If the target speed magnitude exceeds this value, it will not return to normal speed automatically.")]
    [SerializeField] private float maintainSpeedThreshold = 5f;


    [Header("Accidental Swipe Filtering")]
    [SerializeField] private bool enableSwipeFiltering = true;
    [SerializeField] private int swipeHistoryCapacity = 5;
    [SerializeField][Range(0.5f, 1f)] private float swipeIgnoreThreshold = 0.8f;
    private Queue<int> _swipeDirectionHistory;


    private void Awake()
    {
        _swipeDirectionHistory = new Queue<int>(swipeHistoryCapacity);
    }

    private void FixedUpdate()
    {
        UpdateSynthParameters();
    }

    public void UpdateSynthParameters()
    {
        _SoundManager.UpdateSynthParameters(_currentSpeed);
        _MIDISoundManager.UpdateSynthParameters(_currentSpeed);
        _LEDController.UpdateSynthParameters(_currentSpeed);
        _SPItouchPanel.UpdateSynthParameters(_currentSpeed);
    }


    public void SetSwipeControlEnabled(bool enabled)
    {
        _swipeControlEnabled = enabled;
        Debug.Log($"[VideoPlaybackController] Swipe control enabled: {enabled}");
        if (!enabled)
        {
            curtainController.ResetCurtainProgress();
            ClearSwipeHistory();
        }
    }

    public void Init(IMediaPlayer player)
    {
        _mediaPlayer = player;
        if (_mediaPlayer == null)
        {
            Debug.LogError("[VideoPlaybackController] MediaPlayer is null after Init!");
        }
        ResetState();
    }

    public void OnSwipeDetected(SwipeData swipeData)
    {
        if (_mediaPlayer == null)
            return;

        if (stateManager != null && (stateManager.CurrentState == AppState.Active))
        {
            CancelHoldState();

            if (_state == PlaybackState.HoldAccelerating)
                return;

            int currentSwipeDirection = swipeData.direction.x > 0 ? 1 : -1;

            if (enableSwipeFiltering && IsAccidentalSwipe(currentSwipeDirection))
            {
                Debug.Log($"[VideoPlaybackController] Accidental swipe detected and ignored. Direction: {currentSwipeDirection}");
                return;
            }

            if (_speedChangeCoroutine != null)
            {
                StopCoroutine(_speedChangeCoroutine);
            }

            _lastSwipeData = swipeData;

            _speedChangeCoroutine = StartCoroutine(ApplySpeedChangeWithDelay(swipeData));

            if (enableSwipeFiltering)
            {
                AddSwipeToHistory(currentSwipeDirection);
            }
        }
    }

    private bool IsAccidentalSwipe(int currentDirection)
    {
        if (_swipeDirectionHistory.Count == 0)
            return false;

        int forwardCount = _swipeDirectionHistory.Count(d => d == 1);
        int backwardCount = _swipeDirectionHistory.Count(d => d == -1);
        int totalHistory = _swipeDirectionHistory.Count;

        if (totalHistory < 2) // Not enough history to determine a trend
            return false;

        float forwardRatio = (float)forwardCount / totalHistory;
        float backwardRatio = (float)backwardCount / totalHistory;

        if (currentDirection == 1 && backwardRatio >= swipeIgnoreThreshold)
        {
            return true; // Mostly backward swipes recently, ignore forward
        }

        if (currentDirection == -1 && forwardRatio >= swipeIgnoreThreshold)
        {
            return true; // Mostly forward swipes recently, ignore backward
        }

        return false;
    }

    private void AddSwipeToHistory(int direction)
    {
        if (_swipeDirectionHistory.Count >= swipeHistoryCapacity)
        {
            _swipeDirectionHistory.Dequeue();
        }
        _swipeDirectionHistory.Enqueue(direction);
    }

    private void ClearSwipeHistory()
    {
        _swipeDirectionHistory.Clear();
    }


    public void OnRelativeSwipeDetected(RelativeSwipeData relativeSwipeData)
    {
        if (_mediaPlayer == null)
            return;

        if (stateManager != null && (stateManager.CurrentState == AppState.Transition))
            return;

        if (stateManager != null && (stateManager.CurrentState == AppState.Idle))
        {
            HandleCurtainSwipe(relativeSwipeData);
        }
    }

    private void HandleCurtainSwipe(RelativeSwipeData relativeSwipeData)
    {
        if (curtainController == null)
        {
            return;
        }

        float progressIncrement = relativeSwipeData.shift / (float)settings.cols;
        if (relativeSwipeData.shift < 0)
            progressIncrement = Mathf.Abs(progressIncrement);
        else
            progressIncrement = -Mathf.Abs(progressIncrement);

        Debug.Log($"[VideoPlaybackController] Curtain progress increment: {progressIncrement}");

        curtainController.AddSwipeProgress(progressIncrement);

        if (curtainController.IsCurtainFull)
        {
            Debug.Log("[VideoPlaybackController] Curtain is fully open");
        }
    }

    private Coroutine _speedChangeCoroutine;

    private System.Collections.IEnumerator ApplySpeedChangeWithDelay(SwipeData swipeData)
    {
        yield return new WaitForSeconds(_transitionDelay);
        ApplySpeedChange(swipeData);
        _speedChangeCoroutine = null;
    }

    private void ApplySpeedChange(SwipeData swipeData)
    {
        _effectStartSpeed = _currentSpeed;
        int directionFactor = swipeData.direction.x > 0 ? 1 : -1;
        float avgTimeBetween = swipeData.avgTimeBetween;

        float speedChangeAmount = CalculateSpeedChangeAmount(avgTimeBetween);

        _immediateTargetSpeed = _currentSpeed + directionFactor * speedChangeAmount;
        _immediateTargetSpeed = Mathf.Clamp(_immediateTargetSpeed, MAX_REVERSE_SPEED, maxTargetSpeed);

        _accelerationDuration = 4f;
        _phaseTimer = 0f;
        _state = PlaybackState.Accelerating;

        if (Mathf.Abs(_immediateTargetSpeed) > maintainSpeedThreshold)
        {
            _finalTargetSpeed = _immediateTargetSpeed;
            _decelerationDuration = 0f; // No deceleration phase
        }
        else
        {
            if (Mathf.Abs(_immediateTargetSpeed) > 0.1f)
                _finalTargetSpeed = Mathf.Sign(_immediateTargetSpeed) * 1f;
            else
                _finalTargetSpeed = directionFactor > 0 ? 1f : -1f;

            _decelerationDuration = CalculateDecelerationDuration(Mathf.Abs(_immediateTargetSpeed));
        }

        _finalTargetSpeed = Mathf.Clamp(_finalTargetSpeed, MAX_REVERSE_SPEED, maxTargetSpeed);

        _effectStartTime = _mediaPlayer.CurrentTime;
        _accumulatedTimeDelta = 0f;
        _lastSwipeData = swipeData;
        UpdateDebugText();
    }

    private float CalculateSpeedChangeAmount(float avgTimeBetween)
    {
        bool isSlowSwipe = avgTimeBetween > slowSwipeTimeThreshold;
        bool isFastSwipe = avgTimeBetween <= fastSwipeTimeThreshold;

        if (isSlowSwipe)
        {
            return minSlowSpeedAmount;
        }
        else if (isFastSwipe)
        {
            return Mathf.Lerp(2f, 5f, 1f - avgTimeBetween / fastSwipeTimeThreshold);
        }
        else
        {
            // Ensure division by zero doesn't happen if thresholds are equal
            float range = slowSwipeTimeThreshold - fastSwipeTimeThreshold;
            if (range <= 0) return minSlowSpeedAmount;

            float t = (slowSwipeTimeThreshold - avgTimeBetween) / range;
            return Mathf.Lerp(minSlowSpeedAmount, 2f, t);
        }
    }

    public void OnPanelPressed(int panelIndex, bool isPressed)
    {
        if (_mediaPlayer == null || !_swipeControlEnabled)
            return;

        if (isPressed)
        {
            if (Time.time - _lastPanelReleaseTime < HOLD_SAFETY_TIMEOUT)
                return;

            if (_isPanelHoldActive && _heldPanelIndex == panelIndex)
            {
                return;
            }

            Debug.Log($"Panel {panelIndex} Pressed");
            _heldPanelIndex = panelIndex;
            _holdStartTime = Time.time;
            _holdDuration = 0f;
            _isPanelHoldActive = true;
            _reachedZero = false;
            _holdZeroTime = 0f;
            _isHolding = true;
        }
        else
        {
            _lastPanelReleaseTime = Time.time;
            Debug.Log($"Panel {panelIndex} Released");
            HandleReleaseAfterHold();
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            _isHolding = false;
        }
        ClearSwipeHistory(); // Clear history on hold interaction
    }

    private void HandleReleaseAfterHold()
    {
        if (_state == PlaybackState.HoldAccelerating)
        {
            if (Mathf.Abs(_currentSpeed) > skipTime)
            {
                _state = PlaybackState.Decelerating;
                _effectStartSpeed = _currentSpeed;
                _finalTargetSpeed = _currentSpeed > 0 ? 1f : -1f;
                _decelerationDuration = CalculateDecelerationDuration(Mathf.Abs(_currentSpeed));
                _phaseTimer = 0f;
            }
            else
            {
                _state = PlaybackState.Normal;
                _currentSpeed = 0f;
                _immediateTargetSpeed = 0f;
                _finalTargetSpeed = 0f;
                _holdZeroTime = _mediaPlayer.CurrentTime;
            }
        }
    }

    private void CancelHoldState()
    {
        if (_isPanelHoldActive || _heldPanelIndex >= 0)
        {
            HandleReleaseAfterHold();
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            _isHolding = false;
            ClearSwipeHistory();
        }
    }

    private float CalculateDecelerationDuration(float currentSpeedAbs)
    {
        float normalizedSpeed = Mathf.InverseLerp(1f, maxTargetSpeed, currentSpeedAbs);
        return Mathf.Lerp(MIN_DECELERATION_DURATION, MAX_DECELERATION_DURATION, normalizedSpeed);
    }

    private float CalculateDecelerationDurationFromHold(float currentSpeedAbs)
    {
        float normalizedSpeed = Mathf.InverseLerp(1f, maxTargetSpeed, currentSpeedAbs);
        return Mathf.Lerp(MIN_DECELERATION_DURATION, MAX_DECELERATION_DURATION_FROM_HOLD, normalizedSpeed);
    }

    private void Update()
    {
        UpdateMediaPlayer();
        if (_mediaPlayer == null || (!_swipeControlEnabled && _state == PlaybackState.Normal))
            return;

        _previousSpeed = _currentSpeed;
        HandleHoldAcceleration();
        UpdatePlaybackState();
        UpdateDebugText();
    }

    private void HandleHoldAcceleration()
    {
        if (_isPanelHoldActive && _heldPanelIndex >= 0)
        {
            _holdDuration = Time.time - _holdStartTime;
            if (_holdDuration >= HOLD_THRESHOLD && _state != PlaybackState.HoldAccelerating)
            {
                Debug.Log("Transitioning to HoldAccelerating state");
                _previousState = _state;
                _state = PlaybackState.HoldAccelerating;
                _effectStartSpeed = _currentSpeed;
                _immediateTargetSpeed = 0f;
                _finalTargetSpeed = 0f;
                _decelerationDuration = CalculateDecelerationDurationFromHold(Mathf.Abs(_currentSpeed));
                _phaseTimer = 0f;
                _reachedZero = false;
                _holdZeroTime = 0f;
                ClearSwipeHistory();
            }
        }
    }

    private void UpdatePlaybackState()
    {
        _previousState = _state;
        float deltaTime = Time.deltaTime;

        switch (_state)
        {
            case PlaybackState.Accelerating:
                _phaseTimer += deltaTime;
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _immediateTargetSpeed, ref _velocity, _smoothTime, maxTargetSpeed, deltaTime);

                if (_phaseTimer >= _accelerationDuration)
                {
                    _currentSpeed = _immediateTargetSpeed;
                    if (_decelerationDuration > 0f && !Mathf.Approximately(_immediateTargetSpeed, _finalTargetSpeed))
                    {
                        _state = PlaybackState.Decelerating;
                        _effectStartSpeed = _currentSpeed;
                        _phaseTimer = 0f;
                    }
                    else
                    {
                        _state = PlaybackState.Normal;
                    }
                }
                break;

            case PlaybackState.Decelerating:
                _phaseTimer += deltaTime;
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _finalTargetSpeed, ref _velocity, _smoothTime, maxTargetSpeed, deltaTime);

                if (_phaseTimer >= _decelerationDuration)
                {
                    _currentSpeed = _finalTargetSpeed;
                    _state = PlaybackState.Normal;
                }
                break;

            case PlaybackState.HoldAccelerating:
                _phaseTimer += deltaTime;
                float holdProgress = Mathf.Clamp01(_phaseTimer / _decelerationDuration);
                float holdTargetDecelSpeed = QuadraticEaseOut(_effectStartSpeed, 0f, holdProgress);

                if (_isHolding && Mathf.Abs(holdTargetDecelSpeed) <= skipTime)
                {
                    holdTargetDecelSpeed = 0f;
                }
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, holdTargetDecelSpeed, ref _velocity, _smoothTime, maxTargetSpeed, deltaTime);

                if (Mathf.Abs(_currentSpeed) <= skipTime)
                {
                    if (!_reachedZero)
                    {
                        _holdZeroTime = _mediaPlayer.CurrentTime;
                        _currentSpeed = 0;
                        _effectStartTime = _mediaPlayer.CurrentTime;
                        _accumulatedTimeDelta = 0f;
                        _velocity = 0f;
                    }
                    _reachedZero = true;
                }

                if ((_previousSpeed < 0 && _currentSpeed > 0) || (_previousSpeed > 0 && _currentSpeed < 0))
                {
                    _currentSpeed = 0;
                    _effectStartTime = _mediaPlayer.CurrentTime;
                    _accumulatedTimeDelta = 0f;
                }

                if (_phaseTimer >= _decelerationDuration && !_isHolding)
                {
                    _state = PlaybackState.Normal;
                    _currentSpeed = 0f;
                }
                break;

            case PlaybackState.Normal:
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _finalTargetSpeed, ref _velocity, _smoothTime, maxTargetSpeed, deltaTime);
                break;
        }
    }

    private float QuadraticEaseOut(float start, float end, float value)
    {
        end -= start;
        return -end * value * (value - 2) + start;
    }

    private void UpdateMediaPlayer()
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.PlaybackSpeed = _currentSpeed;

            if (!(_state == PlaybackState.HoldAccelerating && _reachedZero))
            {
                _accumulatedTimeDelta += _currentSpeed * Time.deltaTime;
                float newTime = _effectStartTime + _accumulatedTimeDelta;
                newTime = WrapTime(newTime, _mediaPlayer.DurationSeconds);
                _mediaPlayer.SeekToTime(newTime);
            }
            else if (_state == PlaybackState.HoldAccelerating && _reachedZero)
            {
                _mediaPlayer.SeekToTime(_holdZeroTime);
            }
        }
    }

    private float WrapTime(float time, float duration)
    {
        if (duration <= 0f) return 0f;
        return ((time % duration) + duration) % duration;
    }

    private void UpdateDebugText()
    {
        if (debugText == null) return;

        string stateInfo = _state switch
        {
            PlaybackState.Accelerating => $"Acc (target: {_immediateTargetSpeed:F2}×, final: {_finalTargetSpeed:F2}×, elapsed: {_phaseTimer:F1}/{_accelerationDuration:F1})",
            PlaybackState.Decelerating => $"Dec (current: {_currentSpeed:F2}×, final: {_finalTargetSpeed:F2}×, rem: {_decelerationDuration - _phaseTimer:F1}/{_decelerationDuration:F1})",
            PlaybackState.HoldAccelerating => $"Hold Stop (speed: {_currentSpeed:F2}×)",
            _ => $"Normal (final: {_finalTargetSpeed:F2}×)"
        };

        string panelInfo = _swipeControlEnabled ? $"Panels: {_lastSwipeData.panelsCount}, Avg Time: {_lastSwipeData.avgTimeBetween:F3}s" : "Swipe Disabled";
        string holdInfo = $"Hold: idx={_heldPanelIndex}, act={_isPanelHoldActive}, dur={_holdDuration:F1}s, reachedZero={_reachedZero}";
        string swipeHistoryInfo = $"SwipeHist: [{string.Join(",", _swipeDirectionHistory)}]";
        string swipeTimings = $"SlowT: {slowSwipeTimeThreshold:F2}, FastT: {fastSwipeTimeThreshold:F2}";

        debugText.text = $"Current Speed: {_currentSpeed:F2}×\nState: {stateInfo}\n{panelInfo}\n{holdInfo}\n{swipeHistoryInfo}\n{swipeTimings}";
    }

    public void SetNormalSpeed()
    {
        if (_mediaPlayer != null)
        {
            _currentSpeed = 1f;
            _immediateTargetSpeed = 1f;
            _finalTargetSpeed = 1f;
            _isReversePlayback = false;
            _state = PlaybackState.Normal;
            _mediaPlayer.PlaybackSpeed = 1f;
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            ClearSwipeHistory();
            Debug.Log("[VideoPlaybackController] Reset to normal speed");
        }
    }

    public void OnStateChanged(AppState newState)
    {
        if (newState == AppState.Idle)
        {
            ResetToIdleMode();
        }
    }

    private void ResetToIdleMode()
    {
        if (_mediaPlayer != null)
        {
            _currentSpeed = 1f;
            _immediateTargetSpeed = 1f;
            _finalTargetSpeed = 1f;
            _isReversePlayback = false;
            _state = PlaybackState.Normal;
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            _mediaPlayer.PlaybackSpeed = 1f;
            _mediaPlayer.SeekToTime(0f);
            _mediaPlayer.SeekToFrame(0);
            _mediaPlayer.Play();
            ClearSwipeHistory();
            Debug.Log("[VideoPlaybackController] Скорость видео 1");
        }
    }


    void OnEnable()
    {
        ResetState();
    }

    private void ResetState()
    {
        _currentSpeed = 1f;
        _immediateTargetSpeed = 1f;
        _finalTargetSpeed = 1f;
        _isReversePlayback = false;
        _state = PlaybackState.Normal;
        _heldPanelIndex = -1;
        _isPanelHoldActive = false;
        _lastPanelReleaseTime = 0f;
        _swipeControlEnabled = false;
        _reachedZero = false;
        _previousSpeed = 0f;
        _holdZeroTime = 0f;
        _isHolding = false;
        ClearSwipeHistory();

        if (_mediaPlayer != null)
        {
            _mediaPlayer.PlaybackSpeed = 1f;
        }
    }

    private void OnValidate()
    {
        // Ensure fast swipe threshold is not greater than slow swipe threshold
        if (fastSwipeTimeThreshold > slowSwipeTimeThreshold)
        {
            fastSwipeTimeThreshold = slowSwipeTimeThreshold;
        }
    }
}