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

    public void OnMouseSwipeDetected(MouseSwipeData mouseSwipeData)
    {
        if (_mediaPlayer == null)
            return;

        Debug.Log($"[VideoPlaybackController] OnMouseSwipeDetected called: dir=({mouseSwipeData.direction.x:F2},{mouseSwipeData.direction.y:F2}), " +
           $"speed={mouseSwipeData.speed:F0}, distance={mouseSwipeData.distance:F0}, isFinal={mouseSwipeData.isFinalSwipe}");

        // Защита от дублирования событий с очень малым интервалом
        if (Time.time - _lastMouseSwipeTime < 0.01f)
            return;

        _lastMouseSwipeTime = Time.time;

        // Обработка направления свайпа
        Vector2 direction = mouseSwipeData.direction;
        Vector2 significantDir = direction;

        // Предпочитаем горизонтальное направление если нужно
        if (preferMouseHorizontalDirection)
        {
            if (Mathf.Abs(direction.x) > 0.3f)
            {
                significantDir = new Vector2(Mathf.Sign(direction.x), 0).normalized;
            }
        }

        _lastMouseSwipeDir = significantDir;
        _lastMouseSwipeSpeed = mouseSwipeData.speed;

        // Обработка удержания мышью
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
                Debug.Log($"[VideoPlaybackController] Mouse hold started at {_mouseHoldPosition}");
            }
            else
            {
                float distanceMoved = Vector2.Distance(_mouseHoldStartPosition, mouseSwipeData.endPosition);
                if (distanceMoved > mouseHoldMovementThreshold)
                {
                    _mouseHasMovedTooMuch = true;
                    Debug.Log($"[VideoPlaybackController] Mouse moved too much for hold: {distanceMoved:F1} pixels > threshold {mouseHoldMovementThreshold}");
                }
            }

            float holdDuration = Time.time - _mouseHoldStartTime;

            if (holdDuration >= mouseHoldThreshold && !_processedMouseHold && _swipeControlEnabled && !_mouseHasMovedTooMuch)
            {
                Debug.Log("[VideoPlaybackController] Mouse hold threshold reached, activating hold mode");
                ActivateMouseHoldMode();
                _processedMouseHold = true;
            }

            return;
        }
        else if (mouseSwipeData.isFinalSwipe && _isMouseHolding)
        {
            Debug.Log("[VideoPlaybackController] Mouse hold released");
            HandleMouseHoldRelease();

            if (!_processedMouseHold || _mouseHasMovedTooMuch)
            {
                Debug.Log("[VideoPlaybackController] Processing as regular swipe after hold release");
            }
            else
            {
                return; 
            }
        }

        if (stateManager != null && (stateManager.CurrentState == AppState.Active))
        {
            if (!_swipeControlEnabled)
            {
                Debug.Log("[VideoPlaybackController] Swipe control is disabled, ignoring mouse swipe");
                return;
            }

            CancelHoldState();

            if (_state == PlaybackState.HoldAccelerating)
                return;

            int currentSwipeDirection = significantDir.x > 0 ? 1 : -1;

            if (enableSwipeFiltering && IsAccidentalSwipe(currentSwipeDirection))
            {
                Debug.Log($"[VideoPlaybackController] Accidental mouse swipe detected and ignored. Direction: {currentSwipeDirection}");
                return;
            }

            float swipeImpact = CalculateMouseSwipeImpact(mouseSwipeData.speed, mouseSwipeData.duration);


            Vector2 correctedDirection = new Vector2(-significantDir.x, significantDir.y);

            SwipeData convertedSwipeData = new SwipeData
            {
                direction = correctedDirection,
                speed = swipeImpact,
                panelsCount = 2,
                avgTimeBetween = mouseSwipeData.isFinalSwipe ? 0.03f : 0.08f, 
                isSmoothDrag = true
            };

            bool shouldProcess = mouseSwipeData.isFinalSwipe || enableMouseSwipeContinuousUpdate;

            if (shouldProcess)
            {
                if (_speedChangeCoroutine != null)
                {
                    StopCoroutine(_speedChangeCoroutine);
                }

                _lastSwipeData = convertedSwipeData;
                _speedChangeCoroutine = StartCoroutine(ApplySpeedChangeWithDelay(convertedSwipeData));

                if (enableSwipeFiltering)
                {
                    AddSwipeToHistory(currentSwipeDirection);
                }

                _waitingForMouseSwipeRelease = !mouseSwipeData.isFinalSwipe;

                Debug.Log($"[VideoPlaybackController] Mouse swipe processed: dir={currentSwipeDirection}, impact={swipeImpact}, speed={mouseSwipeData.speed:F0}, applied direction={correctedDirection}");
            }
        }
        else if (stateManager != null && (stateManager.CurrentState == AppState.Idle))
        {
            HandleMouseCurtainSwipe(mouseSwipeData);
        }
    }


    private void ActivateMouseHoldMode()
    {
        if (_state != PlaybackState.HoldAccelerating)
        {
            _previousState = _state;
            _state = PlaybackState.HoldAccelerating;
            _effectStartSpeed = _currentSpeed;
            _immediateTargetSpeed = 0f;
            _finalTargetSpeed = 0f;
            _decelerationDuration = CalculateDecelerationDurationFromHold(Mathf.Abs(_currentSpeed));
            _phaseTimer = 0f;
            _reachedZero = false;
            _holdZeroTime = 0f;
            _isHolding = true;
            ClearSwipeHistory();

            Debug.Log("[VideoPlaybackController] Mouse hold mode activated");
        }
    }

    public void OnMouseHoldDetected(SwipeDetector.MouseHoldData holdData)
    {
        if (_mediaPlayer == null || !_swipeControlEnabled)
            return;

        Debug.Log($"[VideoPlaybackController] OnMouseHoldDetected: isStart={holdData.isStart}, duration={holdData.duration:F2}s");

        if (holdData.isStart)
        {
            if (_state == PlaybackState.HoldAccelerating)
                return;

            // Активируем режим удержания, аналогично удержанию панели
            _isMouseHolding = true;
            _mouseHoldStartTime = Time.time;
            _processedMouseHold = false;
            _mouseHoldPosition = holdData.position;

            // Переход в режим удержания для остановки видео
            _previousState = _state;
            _state = PlaybackState.HoldAccelerating;
            _effectStartSpeed = _currentSpeed;
            _immediateTargetSpeed = 0f;
            _finalTargetSpeed = 0f;
            _decelerationDuration = CalculateDecelerationDurationFromHold(Mathf.Abs(_currentSpeed));
            _phaseTimer = 0f;
            _reachedZero = false;
            _holdZeroTime = 0f;
            _isHolding = true;

            ClearSwipeHistory();
            Debug.Log("[VideoPlaybackController] Activated mouse hold mode");
        }
        else
        {
            // Завершение удержания мыши
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

            Debug.Log("[VideoPlaybackController] Released mouse hold, resuming playback");
        }
    }

    private float CalculateMouseSwipeImpact(float speed, float duration)
    {
        float normalizedSpeed = Mathf.Min(1.0f, speed * mouseSensitivity);
        float curvedImpact = mouseResponseCurve.Evaluate(normalizedSpeed);

        float durationFactor = Mathf.Lerp(1.0f, 0.5f, Mathf.Min(1.0f, duration));

        float finalImpact = Mathf.Lerp(mouseMinimumImpact, mouseMaximumImpact, curvedImpact) * durationFactor;

        Debug.Log($"[VideoPlaybackController] Mouse impact calculation: normalizedSpeed={normalizedSpeed:F3}, curvedImpact={curvedImpact:F3}, durationFactor={durationFactor:F3}, finalImpact={finalImpact:F3}");

        return finalImpact;
    }

    private void HandleMouseCurtainSwipe(MouseSwipeData mouseSwipeData)
    {
        if (curtainController == null || !mouseSwipeData.isFinalSwipe)
            return;

        float horizontalFactor = mouseSwipeData.direction.x;

        float progressChange = horizontalFactor * (mouseSwipeData.distance / Screen.width) * mouseCurtainSensitivity;

        progressChange = -progressChange;

        Debug.Log($"[VideoPlaybackController] Mouse curtain progress change: {progressChange:F3}, factor: {horizontalFactor:F2}");

        curtainController.AddSwipeProgress(progressChange);

        if (curtainController.IsCurtainFull)
        {
            Debug.Log("[VideoPlaybackController] Curtain is fully open by mouse swipe");
        }
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

        if (totalHistory < 2)
            return false;

        float forwardRatio = (float)forwardCount / totalHistory;
        float backwardRatio = (float)backwardCount / totalHistory;

        if (currentDirection == 1 && backwardRatio >= swipeIgnoreThreshold)
        {
            return true;
        }

        if (currentDirection == -1 && forwardRatio >= swipeIgnoreThreshold)
        {
            return true;
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
        directionFactor *= -1;

        float avgTimeBetween = swipeData.avgTimeBetween;
        int panelsCount = swipeData.panelsCount;

        float speedChangeAmount = CalculateSpeedChangeAmount(avgTimeBetween);

        if (avgTimeBetween <= fastSwipeTimeThreshold)
        {
            float panelsMultiplier = Mathf.Lerp(1f, 2f, Mathf.InverseLerp(1, settings.cols, panelsCount));
            speedChangeAmount *= panelsMultiplier;
        }

        _immediateTargetSpeed = _currentSpeed + directionFactor * speedChangeAmount;
        _immediateTargetSpeed = Mathf.Clamp(_immediateTargetSpeed, MAX_REVERSE_SPEED, maxTargetSpeed);

        float speedRatio = Mathf.InverseLerp(fastSwipeTimeThreshold, 0f, avgTimeBetween);
        _accelerationDuration = Mathf.Lerp(2f, 5f, speedRatio);

        _phaseTimer = 0f;
        _state = PlaybackState.Accelerating;

        if (Mathf.Abs(_immediateTargetSpeed) > maintainSpeedThreshold)
        {
            _finalTargetSpeed = _immediateTargetSpeed;
            _decelerationDuration = 0f;
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

        Debug.Log($"[VideoPlaybackController] Applied speed change: dir={directionFactor}, " +
                 $"change={speedChangeAmount:F2}, target={_immediateTargetSpeed:F2}, final={_finalTargetSpeed:F2}, " +
                 $"acc_dur={_accelerationDuration:F2}, dec_dur={_decelerationDuration:F2}");

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
            return Mathf.Lerp(2f, 4f, 1f - avgTimeBetween / fastSwipeTimeThreshold);
        }
        else
        {
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
        ClearSwipeHistory();
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

        if (_isMouseHolding)
        {
            HandleMouseHoldRelease();
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

                if ((_isHolding || _isMouseHolding) && Mathf.Abs(holdTargetDecelSpeed) <= skipTime)
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

                if (_phaseTimer >= _decelerationDuration && !_isHolding && !_isMouseHolding)
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
        string holdInfo = $"Hold: panel={_heldPanelIndex}, panel_act={_isPanelHoldActive}, mouse_act={_isMouseHolding}, moved={_mouseHasMovedTooMuch}";
        string swipeHistoryInfo = $"SwipeHist: [{string.Join(",", _swipeDirectionHistory)}]";
        string mouseInfo = $"Mouse: dir=({_lastMouseSwipeDir.x:F2},{_lastMouseSwipeDir.y:F2}), spd={_lastMouseSwipeSpeed:F0}";

        debugText.text = $"Current Speed: {_currentSpeed:F2}×\nState: {stateInfo}\n{panelInfo}\n{holdInfo}\n{swipeHistoryInfo}\n{mouseInfo}";
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
            _isMouseHolding = false;
            _processedMouseHold = false;
            _mouseHasMovedTooMuch = false;
            ClearSwipeHistory();
            Debug.Log("[VideoPlaybackController] Reset to normal speed");
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
        _waitingForMouseSwipeRelease = false;
        _isMouseHolding = false;
        _processedMouseHold = false;
        _mouseHasMovedTooMuch = false;
        ClearSwipeHistory();

        if (_mediaPlayer != null)
        {
            _mediaPlayer.PlaybackSpeed = 1f;
            _mediaPlayer.SeekToTime(0f);
            _effectStartTime = 0f;
            _accumulatedTimeDelta = 0f;
        }
    }

    private void OnValidate()
    {
        if (fastSwipeTimeThreshold > slowSwipeTimeThreshold)
        {
            fastSwipeTimeThreshold = slowSwipeTimeThreshold;
        }
    }
}