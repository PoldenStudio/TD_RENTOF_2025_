using UnityEngine;
using DemolitionStudios.DemolitionMedia;
using TMPro;
using static SwipeDetector;
using LEDControl;
using static StateManager;

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
    private float _targetAccelerationSpeed = 1f;
    private float _effectStartSpeed = 1f;
    private float _finalTargetSpeed = 1f;

    private float _totalEffectDuration = 0f;
    private float _accelerationDuration = 0f;
    private float _decelerationDuration = 0f;
    private float _onReleaseDuratin = 0f;
    private float _phaseTimer = 0f;

    private float _effectStartTime = 0f;
    private float _accumulatedTimeDelta = 0f;

    private float _holdStartTime = 0f;
    private float _holdDuration = 0f;
    private int _heldPanelIndex = -1;
    private bool _isPanelHoldActive = false;
    private const float HOLD_THRESHOLD = 1f;

    private const float MIN_DECELERATION_DURATION = 1f;
    private const float MAX_DECELERATION_DURATION = 5f;

    private const float MAX_DECELERATION_DURATION_FROM_HOLD = 3f;


    [SerializeField] private TextMeshProUGUI debugText;
    private SwipeData _lastSwipeData;

    private const float maxTargetSpeed = 15f;
    private const float minSlowSpeed = 0.25f;
    private const float minEffectDuration = 4f;
    private const float maxEffectDuration = 10f;
    private const float skipTime = 0.25f;
    private const float startfromTime = 0.3f;
    private bool _isReversePlayback = false;

    private const float SLOW_SWIPE_TIME = 0.5f;
    private const float FAST_SWIPE_TIME = 0.1f;
    private const float MAX_REVERSE_SPEED = -15f;
    private const float COUNTER_DIRECTION_MULTIPLIER = 1.5f;

    private float _lastPanelReleaseTime = 0f;
    private const float HOLD_SAFETY_TIMEOUT = 0.5f;

    [SerializeField] private SoundManager _SoundManager;
    [SerializeField] private MIDISoundManager _MIDISoundManager;
    [SerializeField] private StateManager stateManager;
    [SerializeField] private CurtainController curtainController;
    [SerializeField] private LEDController _LEDController;
    [SerializeField] private SPItouchPanel _SPItouchPanel;
    private bool _swipeControlEnabled = false;

    private float _transitionDelay = 0.1f;

    private float _velocity = 0f;
    private float _smoothTime = 0.3f;
    private bool _reachedZero = false;
    private float _previousSpeed = 0f;
    private float _holdZeroTime = 0f;
    private bool _isHolding = false;



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
            curtainController.ResetCurtainProgress();
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

        else if (stateManager != null && (stateManager.CurrentState == AppState.Active))
        {
            CancelHoldState();

            if (_state == PlaybackState.HoldAccelerating)
                return;

            if (_speedChangeCoroutine != null)
            {
                StopCoroutine(_speedChangeCoroutine);
            }

            _lastSwipeData = swipeData;

            _speedChangeCoroutine = StartCoroutine(ApplySpeedChangeWithDelay(swipeData));
        }
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

        // Преобразуем смещение в прогресс, учитывая направление
        float progressIncrement = relativeSwipeData.shift / (float)settings.cols;
        if (relativeSwipeData.shift < 0) // Влево
            progressIncrement = -Mathf.Abs(progressIncrement);
        else // Вправо
            progressIncrement = Mathf.Abs(progressIncrement);

        // Output the progress increment to the console
        Debug.Log($"[VideoPlaybackController] Curtain progress increment: {progressIncrement}");

        curtainController.AddSwipeProgress(progressIncrement);

        if (curtainController.IsCurtainFull)
        {
            Debug.Log("[VideoPlaybackController] Curtain is fully open");
        }
    }

    /*    private float CalculateCurtainProgressIncrement(RelativeSwipeData relativeSwipeData)
        {
            int cols = settings.cols;
            int totalIncrement = 0;

            // Суммируем относительные индексы
            for (int i = 1; i < relativeSwipeData.relativeIndices.Count; i++)
            {
                totalIncrement += relativeSwipeData.relativeIndices[i];
            }

            // Вычисляем инкремент прогресса шторки
            float progressIncrement = (float)totalIncrement / cols;

            Debug.Log("Total Increment: " + totalIncrement);
            Debug.Log("Progress Increment: " + progressIncrement);

            return progressIncrement;
        }*/

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

        int directionFactor = swipeData.direction.x < 0 ? -1 : 1;
        float avgTimeBetween = swipeData.avgTimeBetween;

        bool isCounterDirection = (_currentSpeed > 0 && directionFactor < 0) ||
                                  (_currentSpeed < 0 && directionFactor > 0);
        float speedMultiplier = isCounterDirection ? COUNTER_DIRECTION_MULTIPLIER : 1.0f;

        bool isSlowSwipe = avgTimeBetween > SLOW_SWIPE_TIME;
        bool isFastSwipe = avgTimeBetween <= FAST_SWIPE_TIME;

        float targetSpeed = CalculateTargetSpeed(directionFactor, avgTimeBetween, speedMultiplier, isSlowSwipe, isFastSwipe);
        float effectDuration = CalculateEffectDuration(avgTimeBetween, targetSpeed);

        _targetAccelerationSpeed = targetSpeed;
        _finalTargetSpeed = directionFactor > 0 ? 1f : -1f;
        _isReversePlayback = directionFactor < 0;

        _totalEffectDuration = effectDuration;
        _accelerationDuration = _totalEffectDuration * 0.4f;
        _decelerationDuration = _totalEffectDuration * 0.6f;

        _state = PlaybackState.Accelerating;
        _phaseTimer = 0f;
        _effectStartTime = _mediaPlayer.CurrentTime;
        _accumulatedTimeDelta = 0f;
        _lastSwipeData = swipeData;

        UpdateDebugText();
    }

    private float CalculateTargetSpeed(int directionFactor, float avgTimeBetween, float speedMultiplier, bool isSlowSwipe, bool isFastSwipe)
    {
        if (isSlowSwipe)
        {
            return directionFactor > 0 ? minSlowSpeed : -minSlowSpeed;
        }
        else if (isFastSwipe)
        {
            float baseSpeed = Mathf.Lerp(4f, 7f, 1f - avgTimeBetween / FAST_SWIPE_TIME);
            return directionFactor > 0 ? Mathf.Min(_currentSpeed + baseSpeed * speedMultiplier, maxTargetSpeed) :
                                         Mathf.Max(_currentSpeed - baseSpeed * speedMultiplier, MAX_REVERSE_SPEED);
        }
        else
        {
            float baseSpeed = Mathf.Lerp(2f, 4f, (SLOW_SWIPE_TIME - avgTimeBetween) / (SLOW_SWIPE_TIME - FAST_SWIPE_TIME));
            return directionFactor > 0 ? Mathf.Min(_currentSpeed + baseSpeed * speedMultiplier, maxTargetSpeed) :
                                         Mathf.Max(_currentSpeed - baseSpeed * speedMultiplier, MAX_REVERSE_SPEED);
        }
    }

    private float CalculateEffectDuration(float avgTimeBetween, float targetSpeed)
    {
        float baseDuration = Mathf.Lerp(minEffectDuration, maxEffectDuration, Mathf.InverseLerp(0.05f, 0.4f, avgTimeBetween));
        float speedDifference = Mathf.Abs(targetSpeed - _currentSpeed);
        float additionalTime = Mathf.Lerp(0f, 2f, Mathf.Clamp01(speedDifference / 10f));
        return baseDuration + additionalTime;
    }

    public void OnPanelPressed(int panelIndex, bool isPressed)
    {
        if (_mediaPlayer == null || !_swipeControlEnabled)
            return;

        if (isPressed)
        {
            if (Time.time - _lastPanelReleaseTime < HOLD_SAFETY_TIMEOUT)
                return;

            // Блокируем повторное начало удержания для одной и той же панели
            if (_isPanelHoldActive && _heldPanelIndex == panelIndex)
            {
                return; // Удержание уже активно, не обновляем время
            }

            Debug.Log($"Вызывается");
            _heldPanelIndex = panelIndex;
            _holdStartTime = Time.time; // Запись времени начала удержания
            _holdDuration = 0f; // Сброс (пересчитывается в Update)
            _isPanelHoldActive = true;
            _reachedZero = false;
            _holdZeroTime = 0f;
            _isHolding = true;
        }
        else
        {
            _lastPanelReleaseTime = Time.time;
            Debug.Log($"Вызывается1");
            HandleReleaseAfterHold();
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            _isHolding = false;
        }
    }

    private void HandleReleaseAfterHold()
    {
        if (_state == PlaybackState.HoldAccelerating)
        {
            if (Mathf.Abs(_currentSpeed) > skipTime)
            {
                _state = PlaybackState.Decelerating;
                _effectStartSpeed = _currentSpeed;
                _targetAccelerationSpeed = _currentSpeed > 0 ? 1f : -1f;
                _decelerationDuration = CalculateDecelerationDuration(Mathf.Abs(_currentSpeed));
                _phaseTimer = 0f;
            }
            else
            {
                _state = PlaybackState.Normal;
                _currentSpeed = 0f;
                _targetAccelerationSpeed = 0f;
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
        }
    }

    private float CalculateDecelerationDuration(float currentSpeed)
    {
        float normalizedSpeed = Mathf.InverseLerp(1f, maxTargetSpeed, Mathf.Abs(currentSpeed));
        return Mathf.Lerp(MIN_DECELERATION_DURATION, MAX_DECELERATION_DURATION, normalizedSpeed);
    }

    private float CalculateDecelerationDurationFromHold(float currentSpeed)
    {
        float normalizedSpeed = Mathf.InverseLerp(1f, maxTargetSpeed, Mathf.Abs(currentSpeed));
        return Mathf.Lerp(MIN_DECELERATION_DURATION, MAX_DECELERATION_DURATION_FROM_HOLD, normalizedSpeed);
    }

    private void Update()
    {
        //Debug.Log("Текущий state" + _state);
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
            Debug.Log($"Hold duration: {_holdDuration:F2}s, State: {_state}");
            if (_holdDuration >= HOLD_THRESHOLD && _state != PlaybackState.HoldAccelerating)
            {
                Debug.Log("Transitioning to HoldAccelerating state");
                _previousState = _state;
                _state = PlaybackState.HoldAccelerating;
                _effectStartSpeed = _currentSpeed;
                _targetAccelerationSpeed = 0f;
                _decelerationDuration = CalculateDecelerationDurationFromHold(Mathf.Abs(_currentSpeed));
                _phaseTimer = 0f;
                _reachedZero = false;
                _holdZeroTime = 0f;
            }
        }
    }

    private void UpdatePlaybackState()
    {
        _previousState = _state;

        switch (_state)
        {
            case PlaybackState.Accelerating:
                _phaseTimer += Time.deltaTime;
                float accelProgress = Mathf.Clamp01(_phaseTimer / _accelerationDuration);
                float targetSpeed = Mathf.Lerp(_effectStartSpeed, _targetAccelerationSpeed, accelProgress);
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _velocity, _smoothTime);

                if (_phaseTimer >= _accelerationDuration)
                {
                    if (Mathf.Approximately(_targetAccelerationSpeed, _finalTargetSpeed))
                    {
                        _state = PlaybackState.Normal;
                        _currentSpeed = _targetAccelerationSpeed;
                    }
                    else
                    {
                        _state = PlaybackState.Decelerating;
                        _effectStartSpeed = _currentSpeed;
                        _targetAccelerationSpeed = _finalTargetSpeed;
                        _decelerationDuration = CalculateDecelerationDuration(Mathf.Abs(_currentSpeed));
                        _phaseTimer = 0f;
                    }
                }
                break;

            case PlaybackState.Decelerating:
                _phaseTimer += Time.deltaTime;
                float decelProgress = Mathf.Clamp01(_phaseTimer / _decelerationDuration);
                float targetDecelSpeed = Mathf.Lerp(_effectStartSpeed, _targetAccelerationSpeed, decelProgress);
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetDecelSpeed, ref _velocity, _smoothTime);

                if (_phaseTimer >= _decelerationDuration)
                {
                    _state = PlaybackState.Normal;
                    _currentSpeed = _targetAccelerationSpeed;
                }
                break;

            case PlaybackState.HoldAccelerating:
                _phaseTimer += Time.deltaTime;
                float holdProgress = Mathf.Clamp01(_phaseTimer / _decelerationDuration);
                float holdTargetDecelSpeed = QuadraticEaseOut(_effectStartSpeed, _targetAccelerationSpeed, holdProgress);

                if (_isHolding && Mathf.Abs(holdTargetDecelSpeed) <= skipTime)
                {
                    holdTargetDecelSpeed = 0f;
                }
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, holdTargetDecelSpeed, ref _velocity, _smoothTime);

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

                if (_phaseTimer >= _decelerationDuration)
                {
                    _state = PlaybackState.Normal;
                    _currentSpeed = _targetAccelerationSpeed;
                }
                break;

            case PlaybackState.Normal:
                _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _finalTargetSpeed, ref _velocity, _smoothTime);
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
            PlaybackState.Accelerating => $"Acc (target: {_targetAccelerationSpeed:F2}×, final: {_finalTargetSpeed:F2}×, elapsed: {_phaseTimer:F1}/{_accelerationDuration:F1})",
            PlaybackState.Decelerating => $"Dec (remaining: {_decelerationDuration - _phaseTimer:F1}/{_decelerationDuration:F1}, current: {_currentSpeed:F2}×, final: {_finalTargetSpeed:F2}×)",
            PlaybackState.HoldAccelerating => $"Hold Stop (speed: {_currentSpeed:F2}×)",
            _ => $"Normal (final: {_finalTargetSpeed:F2}×)"
        };

        string panelInfo = _swipeControlEnabled ? $"Panels: {_lastSwipeData.panelsCount}, Avg Time: {_lastSwipeData.avgTimeBetween:F2}s" : "Swipe Disabled";
        string holdInfo = $"Hold: idx={_heldPanelIndex}, act={_isPanelHoldActive}, dur={_holdDuration:F1}s, reachedZero={_reachedZero}";

        debugText.text = $"Current Speed: {_currentSpeed:F2}×\nState: {stateInfo}\n{panelInfo}\n{holdInfo}";
    }

    public void SetNormalSpeed()
    {
        if (_mediaPlayer != null)
        {
            _currentSpeed = 1f;
            _targetAccelerationSpeed = 1f;
            _finalTargetSpeed = 1f;
            _isReversePlayback = false;
            _state = PlaybackState.Normal;
            _mediaPlayer.PlaybackSpeed = 1f;
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            Debug.Log("[VideoPlaybackController] Reset to normal speed");
        }
    }

    public void OnStateChanged(AppState newState)
    {
        if (newState == AppState.Idle)
        {
            ResetToIdleMode();
        }
/*        if (newState == AppState.Active)
        {
            ResetToIdleMode();
        }*/
    }

    private void ResetToIdleMode()
    {
        if (_mediaPlayer != null)
        {
            _currentSpeed = 1f;
            _targetAccelerationSpeed = 1f;
            _finalTargetSpeed = 1f;
            _isReversePlayback = false;
            _state = PlaybackState.Normal;
            _heldPanelIndex = -1;
            _isPanelHoldActive = false;
            _mediaPlayer.PlaybackSpeed = 1f;
            //_mediaPlayer.SeekToTime(0f);
            _mediaPlayer.SeekToFrame(0);
            _mediaPlayer.Play();
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
        _targetAccelerationSpeed = 1f;
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

        if (_mediaPlayer != null)
        {
            _mediaPlayer.PlaybackSpeed = 1f;
        }
    }
}