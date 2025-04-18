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
        Decelerating,
        HoldAccelerating
    }

    private PlaybackState _state = PlaybackState.Normal;
    private PlaybackState _previousState;
    private IMediaPlayer _mediaPlayer;

    [SerializeField] private Settings settings;

    private float _currentSpeed = 1f;
    private float _finalTargetSpeed = 1f;

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

    private float _velocity = 0f;

    [Header("Mouse swipe Timings")]
    [SerializeField] private float _smoothTimeNormal = 0.15f;
    [SerializeField] private float _smoothTimeSwipeMin = 0.1f;
    [SerializeField] private float _smoothTimeSwipeMax = 0.3f;
    [SerializeField] private float _smoothTimeHoldMin = 0.1f;
    [SerializeField] private float _smoothTimeHoldMax = 0.4f;
    private float _phaseTimer;
    private bool _reachedZero = false;
    private float _previousSpeed = 0f;
    private float _holdZeroTime = 0f;
    private bool _isHolding = false;

    [Header("Swipe Timings")]
    [Tooltip("Average time between touches greater than this will be considered a SLOW swipe")]
    [SerializeField] private float slowSwipeTimeThreshold = 0.1f;
    [Tooltip("Average time between touches less than or equal to this will be considered a FAST swipe")]
    [SerializeField] private float fastSwipeTimeThreshold = 0.05f;
    [Tooltip("If the target speed magnitude exceeds this value, it will NOT return to normal speed automatically")]
    [SerializeField] private float maintainSpeedThreshold = 5f;

    [Header("Mouse Swipe Settings")]
    [SerializeField] private float mouseSensitivity = 0.002f;
    [SerializeField] private float mouseCurtainSensitivity = 0.5f;
    [SerializeField] private float mouseMinimumImpact = 1.0f;
    [SerializeField] private float mouseMaximumImpact = 8.0f;
    [SerializeField] private AnimationCurve mouseResponseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool preferMouseHorizontalDirection = true;
    [SerializeField] private bool enableMouseSwipeContinuousUpdate = true;

    [Header("Mouse Hold Settings")]
    [SerializeField] private bool enableMouseHold = true;
    [SerializeField] private float mouseHoldThreshold = 0.5f;
    [SerializeField] private float mouseHoldMovementThreshold = 5.0f;

    [Header("Accidental Swipe Filtering")]
    [SerializeField] private bool enableSwipeFiltering = true;
    [SerializeField] private int swipeHistoryCapacity = 5;
    [SerializeField][Range(0.5f, 1f)] private float swipeIgnoreThreshold = 0.8f;

    private Queue<int> _swipeDirectionHistory;

    private Vector2 _lastMouseSwipeDir;
    private float _lastMouseSwipeSpeed;
    private float _lastMouseSwipeTime;
    private bool _waitingForMouseSwipeRelease = false;
    private bool _isMouseHolding = false;
    private float _mouseHoldStartTime = 0f;
    private Vector2 _mouseHoldPosition;
    private bool _processedMouseHold = false;
    private Vector2 _mouseHoldStartPosition;
    private bool _mouseHasMovedTooMuch = false;

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
        _SoundManager?.UpdateSynthParameters(_currentSpeed);
        _MIDISoundManager?.UpdateSynthParameters(_currentSpeed);
        _LEDController?.UpdateSynthParameters(_currentSpeed);
        _SPItouchPanel?.UpdateSynthParameters(_currentSpeed);
    }

    public void SetSwipeControlEnabled(bool enabled)
    {
        _swipeControlEnabled = enabled;
        Debug.Log($"[VideoPlaybackController] Swipe control enabled: {enabled}");
        if (!enabled)
        {
            curtainController?.ResetCurtainProgress();
            ClearSwipeHistory();
            SetNormalSpeed();
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

    public void OnMouseSwipeDetected(MouseSwipeData mouseSwipeData)
    {
        if (_mediaPlayer == null || stateManager == null)
            return;

        if (Time.time - _lastMouseSwipeTime < 0.01f)
            return;

        _lastMouseSwipeTime = Time.time;

        Vector2 direction = mouseSwipeData.direction;
        Vector2 significantDir = direction;
        if (preferMouseHorizontalDirection)
        {
            if (Mathf.Abs(direction.x) > 0.3f)
            {
                significantDir = new Vector2(Mathf.Sign(direction.x), 0).normalized;
            }
        }
        _lastMouseSwipeDir = significantDir;
        _lastMouseSwipeSpeed = mouseSwipeData.speed;

        if (stateManager.CurrentState == AppState.Active)
        {
            if (!_swipeControlEnabled)
            {
                Debug.Log("[VideoPlaybackController] Swipe control disabled => ignoring mouse swipe");
                return;
            }

            if (enableMouseHold && !mouseSwipeData.isFinalSwipe)
            {
                if (!_isMouseHolding)
                {
                    _isMouseHolding = true;
                    _mouseHoldStartTime = Time.time;
                    _mouseHoldPosition = mouseSwipeData.startPosition;
                    _mouseHoldStartPosition = mouseSwipeData.startPosition;
                    _processedMouseHold = false;
                    _mouseHasMovedTooMuch = false;
                }
                else
                {
                    float distanceMoved = Vector2.Distance(_mouseHoldStartPosition, mouseSwipeData.endPosition);
                    if (distanceMoved > mouseHoldMovementThreshold)
                    {
                        _mouseHasMovedTooMuch = true;
                    }
                }
                return;
            }
            else if (mouseSwipeData.isFinalSwipe && _isMouseHolding)
            {
                HandleMouseHoldRelease();
                if (_processedMouseHold && !_mouseHasMovedTooMuch)
                {
                    return;
                }
            }

            if (mouseSwipeData.isFinalSwipe || !enableMouseHold)
            {
                CancelHoldState();
                int currentSwipeDirection = significantDir.x > 0 ? 1 : -1;
                if (enableSwipeFiltering && IsAccidentalSwipe(currentSwipeDirection))
                {
                    Debug.Log($"[VideoPlaybackController] Mouse swipe ignored as accidental. Dir={currentSwipeDirection}");
                    return;
                }

                float swipeImpact = CalculateMouseSwipeImpact(mouseSwipeData.speed, mouseSwipeData.duration);

                Vector2 correctedDirection = new Vector2(-significantDir.x, significantDir.y);
                SwipeData convertedSwipeData = new SwipeData
                {
                    direction = correctedDirection,
                    speed = swipeImpact,
                    panelsCount = 2,
                    avgTimeBetween = mouseSwipeData.duration > 0 ? mouseSwipeData.duration / 2f : 0.08f,
                    isSmoothDrag = true
                };

                ApplySpeedChange(convertedSwipeData);

                if (enableSwipeFiltering)
                {
                    AddSwipeToHistory(currentSwipeDirection);
                }

                _waitingForMouseSwipeRelease = false;
            }

        }
        else if (stateManager.CurrentState == AppState.Idle)
        {
            HandleMouseCurtainSwipe(mouseSwipeData);
        }
    }

    private void HandleMouseCurtainSwipe(MouseSwipeData msd)
    {
        if (curtainController == null || !msd.isFinalSwipe)
            return;

        float horizFactor = msd.direction.x;
        float progress = horizFactor * (msd.distance / Screen.width) * mouseCurtainSensitivity;
        progress = -progress;
        curtainController.AddSwipeProgress(progress);
    }

    public void OnMouseHoldDetected(MouseHoldData holdData)
    {
        if (_mediaPlayer == null || !_swipeControlEnabled) return;

        if (holdData.isStart)
        {
            if (_state == PlaybackState.HoldAccelerating)
                return;

            _isMouseHolding = true;
            _mouseHoldStartTime = Time.time;
            _processedMouseHold = false;
            _mouseHoldPosition = holdData.position;

            _previousState = _state;
            _state = PlaybackState.HoldAccelerating;
            _finalTargetSpeed = 0f;

            float smoothTimeHold = Mathf.Lerp(_smoothTimeHoldMin, _smoothTimeHoldMax, Mathf.InverseLerp(1f, maxTargetSpeed, Mathf.Abs(_currentSpeed)));
            _phaseTimer = 0f;
            _reachedZero = false;
            _holdZeroTime = 0f;
            _isHolding = true;
            _velocity = 0f;

            ClearSwipeHistory();
            Debug.Log("[VideoPlaybackController] Mouse Hold Started");
        }
        else
        {
            _isMouseHolding = false;
            if (_state == PlaybackState.HoldAccelerating)
            {
                HandleMouseHoldRelease();
            }
        }
    }

    private void HandleMouseHoldRelease()
    {
        if (_state == PlaybackState.HoldAccelerating && _isHolding)
        {
            _isHolding = false;
            _processedMouseHold = true;

            if (Mathf.Abs(_currentSpeed) > skipTime)
            {
                _state = PlaybackState.Decelerating;
                _finalTargetSpeed = Mathf.Sign(_currentSpeed) * 1f;

                float smoothTimeRelease = Mathf.Lerp(_smoothTimeHoldMin, _smoothTimeNormal, 0.5f);
                _velocity = 0f;
                Debug.Log("[VideoPlaybackController] Mouse hold release => resuming smoothly");
            }
            else
            {
                _state = PlaybackState.Normal;
                _currentSpeed = 0f;
                _finalTargetSpeed = 0f;
                _holdZeroTime = _mediaPlayer?.CurrentTime ?? 0f;
                Debug.Log("[VideoPlaybackController] Mouse hold release => stopped");
            }
        }
        _isMouseHolding = false;
    }

    private float CalculateMouseSwipeImpact(float speed, float duration)
    {
        float normSpeed = Mathf.Min(1.0f, speed * mouseSensitivity);
        float curved = mouseResponseCurve.Evaluate(normSpeed);
        float durFactor = Mathf.Lerp(1.0f, 0.5f, Mathf.Min(1.0f, duration / 0.5f));
        float impact = Mathf.Lerp(mouseMinimumImpact, mouseMaximumImpact, curved) * durFactor;
        return impact;
    }

    public void OnSwipeDetected(SwipeData swipeData)
    {
        if (_mediaPlayer == null || stateManager == null || stateManager.CurrentState != AppState.Active) return;
        if (!_swipeControlEnabled) return;

        CancelHoldState();

        int dir = swipeData.direction.x > 0 ? 1 : -1;
        if (enableSwipeFiltering && IsAccidentalSwipe(dir))
        {
            Debug.Log($"[VideoPlaybackController] Accidental swipe ignored. Dir={dir}");
            return;
        }

        ApplySpeedChange(swipeData);

        if (enableSwipeFiltering)
        {
            AddSwipeToHistory(dir);
        }
    }

    public void OnRelativeSwipeDetected(RelativeSwipeData relativeSwipeData)
    {
        if (_mediaPlayer == null || stateManager == null) return;

        if (stateManager.CurrentState == AppState.Transition)
            return;

        if (stateManager.CurrentState == AppState.Idle)
        {
            HandleCurtainSwipe(relativeSwipeData);
        }
    }

    private void HandleCurtainSwipe(RelativeSwipeData rsd)
    {
        if (curtainController == null) return;

        float progressIncrement = rsd.shift / (float)settings.cols;
        progressIncrement = rsd.shift < 0 ? Mathf.Abs(progressIncrement) : -Mathf.Abs(progressIncrement);

        curtainController.AddSwipeProgress(progressIncrement);
    }

    private void ApplySpeedChange(SwipeData swipeData)
    {
        _lastSwipeData = swipeData;

        int directionFactor = swipeData.direction.x > 0 ? 1 : -1;
        directionFactor *= -1; 

        float avgTime = swipeData.avgTimeBetween;
        int pCount = swipeData.panelsCount;

        float speedChangeAmount = CalculateSpeedChangeAmount(avgTime);
        if (avgTime <= fastSwipeTimeThreshold)
        {
            float panelsMultiplier = Mathf.Lerp(1f, 2f, Mathf.InverseLerp(1, settings.cols, pCount));
            speedChangeAmount *= panelsMultiplier;
        }

        float newPotentialTargetSpeed = _currentSpeed + directionFactor * speedChangeAmount;
        newPotentialTargetSpeed = Mathf.Clamp(newPotentialTargetSpeed, MAX_REVERSE_SPEED, maxTargetSpeed);

        if (Mathf.Abs(newPotentialTargetSpeed) > maintainSpeedThreshold)
        {
            _finalTargetSpeed = newPotentialTargetSpeed;
        }
        else
        {
            if (Mathf.Abs(newPotentialTargetSpeed) < skipTime)
            {
                _finalTargetSpeed = 0f;
            }
            else if (Mathf.Abs(newPotentialTargetSpeed) < 1f)
            {
                _finalTargetSpeed = Mathf.Sign(newPotentialTargetSpeed) * 1f;
            }
            else
            {
                _finalTargetSpeed = newPotentialTargetSpeed;
            }
        }
        _finalTargetSpeed = Mathf.Clamp(_finalTargetSpeed, MAX_REVERSE_SPEED, maxTargetSpeed);

        if (_state != PlaybackState.HoldAccelerating)
        {
            _state = PlaybackState.Decelerating;
        }

        _velocity = 0f;

        Debug.Log($"[VideoPlaybackController] ApplySpeedChange => dir={directionFactor}, potentialTarget={newPotentialTargetSpeed:F2}, finalTarget={_finalTargetSpeed:F2}");
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
            float t = 1f - avgTimeBetween / fastSwipeTimeThreshold;
            t = Mathf.Clamp01(t);
            return Mathf.Lerp(2f, 4f, t);
        }
        else
        {
            float range = slowSwipeTimeThreshold - fastSwipeTimeThreshold;
            if (range <= 0f) return minSlowSpeedAmount;

            float t = (slowSwipeTimeThreshold - avgTimeBetween) / range;
            t = Mathf.Clamp01(t);
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
            HandleReleaseAfterHold();
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            _isHolding = false;
            ClearSwipeHistory();
        }
    }

    private void HandleReleaseAfterHold()
    {
        if (_state == PlaybackState.HoldAccelerating)
        {
            if (Mathf.Abs(_currentSpeed) > skipTime)
            {
                _state = PlaybackState.Decelerating;
                _finalTargetSpeed = Mathf.Sign(_currentSpeed) * 1f;
                _velocity = 0f;
                Debug.Log("[VideoPlaybackController] Panel hold release => resuming smoothly");
            }
            else
            {
                _state = PlaybackState.Normal;
                _currentSpeed = 0f;
                _finalTargetSpeed = 0f;
                _holdZeroTime = _mediaPlayer?.CurrentTime ?? 0f;
                Debug.Log("[VideoPlaybackController] Panel hold release => stopped");
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
            Debug.Log("[VideoPlaybackController] Panel hold cancelled by swipe");
        }

        if (_isMouseHolding)
        {
            HandleMouseHoldRelease();
            Debug.Log("[VideoPlaybackController] Mouse hold cancelled by swipe");
        }
    }

    private void Update()
    {
        UpdateMediaPlayer();
        if (_mediaPlayer == null)
            return;

        _previousSpeed = _currentSpeed;
        HandlePanelHoldActivation();
        UpdatePlaybackState();
        UpdateDebugText();
    }

    private void HandlePanelHoldActivation()
    {
        if (_isPanelHoldActive && _heldPanelIndex >= 0 && !_isHolding)
        {
            _holdDuration = Time.time - _holdStartTime;
            if (_holdDuration >= HOLD_THRESHOLD)
            {
                _isHolding = true;
                _previousState = _state;
                _state = PlaybackState.HoldAccelerating;
                _finalTargetSpeed = 0f;
                _phaseTimer = 0f;
                _reachedZero = false;
                _holdZeroTime = 0f;
                _velocity = 0f;
                ClearSwipeHistory();
                Debug.Log("[VideoPlaybackController] Panel Hold Activated");
            }
        }
    }

    private void UpdatePlaybackState()
    {
        _previousSpeed = _currentSpeed;
        float dt = Time.deltaTime;
        float currentSmoothTime = 0;

        switch (_state)
        {
            case PlaybackState.Decelerating:
                float targetSpeedMagnitude = Mathf.Abs(_finalTargetSpeed);
                currentSmoothTime = Mathf.Lerp(_smoothTimeSwipeMin, _smoothTimeSwipeMax, Mathf.InverseLerp(1f, maxTargetSpeed, targetSpeedMagnitude));
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _finalTargetSpeed, ref _velocity, currentSmoothTime, maxTargetSpeed, dt);

                if (Mathf.Abs(_currentSpeed - _finalTargetSpeed) < 0.01f)
                {
                    _currentSpeed = _finalTargetSpeed;
                    _state = PlaybackState.Normal;
                    _velocity = 0f; 
                    Debug.Log($"[VideoPlaybackController] Deceleration complete. Final speed: {_currentSpeed:F2}");
                }

                break;

            case PlaybackState.HoldAccelerating:
                currentSmoothTime = Mathf.Lerp(_smoothTimeHoldMin, _smoothTimeHoldMax, Mathf.InverseLerp(1f, maxTargetSpeed, Mathf.Abs(_previousSpeed)));
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, 0f, ref _velocity, currentSmoothTime, maxTargetSpeed, dt);

                if (Mathf.Abs(_currentSpeed) <= skipTime)
                {
                    if (!_reachedZero)
                    {
                        _holdZeroTime = _mediaPlayer?.CurrentTime ?? 0f;
                        _currentSpeed = 0f;
                        _effectStartTime = _mediaPlayer?.CurrentTime ?? 0f;
                        _accumulatedTimeDelta = 0f;
                    }
                    _reachedZero = true;
                }
                else
                {
                    _reachedZero = false;
                }

                if ((_previousSpeed < 0 && _currentSpeed >= 0) || (_previousSpeed > 0 && _currentSpeed <= 0))
                {
                    if (!_reachedZero)
                    {
                        _currentSpeed = 0f;
                        _holdZeroTime = _mediaPlayer?.CurrentTime ?? 0f;
                        _effectStartTime = _mediaPlayer?.CurrentTime ?? 0f;
                        _accumulatedTimeDelta = 0f;
                        _reachedZero = true;
                        Debug.Log("[VideoPlaybackController] Speed crossed zero during hold");
                    }
                }

                if (!_isHolding && !_isMouseHolding)
                {
                    HandleReleaseAfterHold();
                }
                break;

            case PlaybackState.Normal:
                currentSmoothTime = _smoothTimeNormal;
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _finalTargetSpeed, ref _velocity, currentSmoothTime, maxTargetSpeed, dt);
                break;
        }
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

                if (Mathf.Abs(_currentSpeed) < 0.01f)
                { 
                    _mediaPlayer.SeekToTime(_holdZeroTime);
                }
            }
        }
    }

    private float WrapTime(float time, float duration)
    {
        if (duration <= 0f) return 0f;

        float wrappedTime = time % duration;
        if (wrappedTime < 0)
        {
            wrappedTime += duration;
        }
        return wrappedTime;
    }

    private void UpdateDebugText()
    {
        if (debugText == null) return;

        string stateInfo = _state switch
        {
            PlaybackState.Decelerating =>
                $"Dec (speed={_currentSpeed:F2}, final={_finalTargetSpeed:F2})",
            PlaybackState.HoldAccelerating =>
                $"Hold Stop (speed={_currentSpeed:F2}, reachedZero={_reachedZero})",
            _ => $"Normal (speed={_currentSpeed:F2}, target={_finalTargetSpeed:F2})"
        };

        string panelText = _swipeControlEnabled
            ? $"Panels={(_lastSwipeData.panelsCount > 0 ? _lastSwipeData.panelsCount : 0)}, avgT={(_lastSwipeData.avgTimeBetween > 0 ? _lastSwipeData.avgTimeBetween : 0):F3}s"
            : "Swipe Disabled";
        string holdText = $"Hold: panel={_heldPanelIndex}, active={_isPanelHoldActive}, mouse={_isMouseHolding}, general={_isHolding}";
        string histText = $"SwipeHist: [{string.Join(",", _swipeDirectionHistory?.ToArray() ?? new int[0])}]";
        string mouseText = $"Mouse dir=({_lastMouseSwipeDir.x:F2},{_lastMouseSwipeDir.y:F2}), spd={_lastMouseSwipeSpeed:F0}";

        debugText.text =
            $"Current Speed: {_currentSpeed:F2}×\n" +
            $"State: {stateInfo}\n" +
            $"{panelText}\n" +
            $"{holdText}\n" +
            $"{histText}\n" +
            $"{mouseText}";
    }

    public void SetNormalSpeed()
    {
        if (_mediaPlayer != null)
        {
            _currentSpeed = 1f;
            _finalTargetSpeed = 1f;
            _isReversePlayback = false;
            _state = PlaybackState.Normal;
            _mediaPlayer.PlaybackSpeed = 1f;
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            _isMouseHolding = false;
            _processedMouseHold = false;
            _mouseHasMovedTooMuch = false;
            _velocity = 0f;
            _isHolding = false;
            ClearSwipeHistory();

            Debug.Log("[VideoPlaybackController] SetNormalSpeed => speed=1×");
        }
    }

    public void OnStateChanged(AppState newState)
    {
        if (newState == AppState.Idle)
        {
            ResetState();
        }
        if (newState == AppState.Active)
        {
            ResetState();
            SetSwipeControlEnabled(true);
        }
    }

    private void OnEnable()
    {
        ResetState();
    }

    private void ResetState()
    {
        _currentSpeed = 1f;
        _finalTargetSpeed = 1f;
        _isReversePlayback = false;
        _state = PlaybackState.Normal;
        _heldPanelIndex = -1;
        _isPanelHoldActive = false;
        _lastPanelReleaseTime = 0f;
        _reachedZero = false;
        _previousSpeed = 0f;
        _holdZeroTime = 0f;
        _isHolding = false;
        _waitingForMouseSwipeRelease = false;
        _isMouseHolding = false;
        _processedMouseHold = false;
        _mouseHasMovedTooMuch = false;
        _velocity = 0f;
        _effectStartTime = 0f;
        _accumulatedTimeDelta = 0f;
        ClearSwipeHistory();

        if (_mediaPlayer != null)
        {
            _mediaPlayer.PlaybackSpeed = 1f;

            if (_mediaPlayer.DurationSeconds > 0)
            {
                _mediaPlayer.SeekToTime(0f);
            }
            else
            {
                Debug.LogWarning("[VideoPlaybackController] MediaPlayer duration is zero or unknown, cannot seek to 0");
            }
        }
        Debug.Log("[VideoPlaybackController] State Reset");
    }

    private bool IsAccidentalSwipe(int currentDirection)
    {
        if (_swipeDirectionHistory == null || _swipeDirectionHistory.Count == 0)
            return false;

        int forwardCount = _swipeDirectionHistory.Count(d => d == 1);
        int backwardCount = _swipeDirectionHistory.Count(d => d == -1);
        int totalHistory = _swipeDirectionHistory.Count;

        if (totalHistory < 2)
            return false;

        float forwardRatio = (float)forwardCount / totalHistory;
        float backwardRatio = (float)backwardCount / totalHistory;

        if (currentDirection == 1 && backwardRatio >= swipeIgnoreThreshold)
            return true;
        if (currentDirection == -1 && forwardRatio >= swipeIgnoreThreshold)
            return true;

        return false;
    }

    private void AddSwipeToHistory(int direction)
    {
        if (_swipeDirectionHistory == null) return;
        if (_swipeDirectionHistory.Count >= swipeHistoryCapacity)
        {
            _swipeDirectionHistory.Dequeue();
        }
        _swipeDirectionHistory.Enqueue(direction);
    }

    private void ClearSwipeHistory()
    {
        _swipeDirectionHistory?.Clear();
    }
}