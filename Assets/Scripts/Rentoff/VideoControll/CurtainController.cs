using System;
using System.Collections;
using UnityEngine;
using DemolitionStudios.DemolitionMedia;

public class CurtainController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform curtainObject;
    [SerializeField] private MeshRenderer curtainRenderer;

    [Header("Animation Settings")]
    [SerializeField] private float slideDuration = 2f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float slideSpeed = 15f;
    [SerializeField] private float minXPosition = -1032f;
    [SerializeField] private float maxXPosition = 0f;

    [Tooltip("True => instant move, False => using smooth motion")]
    public bool instantMove = false;

    [Header("Logic Settings")]
    [SerializeField] private float targetProgressThreshold = 5f;
    [SerializeField] private float swipeProgressMultiplier = 1f;
    [SerializeField] private float inactivityCloseDelay = 4f;

    [Header("Swipe Logic")]
    [Tooltip("How many opposite swipes to change direction")]
    [SerializeField] private int oppositeSwipeTolerance = 1;

    [Header("Demolition Media")]
    [SerializeField] private Media _demolitionMedia;
    private IMediaPlayer _mediaPlayer;
    [SerializeField] private StateManager stateManager;

    private readonly float _finalApproachDelay = 0.3f;

    private float _currentProgress = 0f;
    private float _targetProgress = 0f;
    private Coroutine _progressCoroutine;
    private Coroutine _fadeCoroutine;
    private bool _isCurtainAnimating = false;
    private bool _isCurtainFull = false;

    private float _lastControlTime = 0f;
    private float _inactivityTimer = 0f;

    public bool IsCurtainFull => _isCurtainFull;

    private Action _onCurtainFullCallback;
    private Action _onCurtainFadedCallback;

    private int _lastCommittedSwipeSign = 0;
    private int _oppositeSwipeStreak = 0;

    private void Awake()
    {
        if (curtainObject == null || curtainRenderer == null)
        {
            Debug.LogError("[CurtainController] References not set");
            return;
        }

        // Initialize position
        curtainObject.localPosition = new Vector3(
            minXPosition,
            curtainObject.localPosition.y,
            curtainObject.localPosition.z
        );

        SetCurtainAlpha(1f);
    }

    private void Start()
    {
        if (_demolitionMedia == null)
        {
            Debug.LogError("[CurtainController] Demolition Media is not assigned");
            return;
        }

        _mediaPlayer = new DemolitionMediaPlayer(_demolitionMedia);

        _mediaPlayer.Pause();
    }

    private void Update()
    {
        HandleInactivity();

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (stateManager != null && stateManager.CurrentState == StateManager.AppState.Idle)
            {
                AddSwipeProgress(0.3f);
            }
        }
    }

    private void HandleInactivity()
    {
        if (_lastControlTime > 0f)
        {
            _inactivityTimer += Time.deltaTime;

            if (_inactivityTimer >= inactivityCloseDelay && !_isCurtainAnimating && !_isCurtainFull)
            {
                Debug.Log("[CurtainController] Inactivity detected. Closing curtain");
                SlideCurtain(false);
                ResetInactivityTimer();
            }
            else if (_inactivityTimer >= _finalApproachDelay && !_isCurtainAnimating)
            {
                if (!Mathf.Approximately(_currentProgress, _targetProgress))
                {
                    GoToTargetProgress(_targetProgress, false);
                }
            }
        }
    }

    public void AddSwipeProgress(float progressIncrement)
    {
        UpdateInactivityTimer();

        if (Mathf.Approximately(progressIncrement, 0f)) return;

        if (_isCurtainFull && progressIncrement > 0)
        {
            Debug.Log("[CurtainController] Already full, ignoring positive swipe");
            return;
        }

        int currentSwipeSign = Math.Sign(progressIncrement);
        float processedIncrement = progressIncrement;

        if (_lastCommittedSwipeSign == 0)
        {
            _lastCommittedSwipeSign = currentSwipeSign;
            _oppositeSwipeStreak = 0;
        }
        else if (currentSwipeSign == _lastCommittedSwipeSign)
        {
            _oppositeSwipeStreak = 0;
        }
        else
        {
            _oppositeSwipeStreak++;
            if (_oppositeSwipeStreak <= oppositeSwipeTolerance)
            {
                processedIncrement = Mathf.Abs(progressIncrement) * _lastCommittedSwipeSign;
                Debug.Log($"[CurtainController] Tolerating opposite swipe. Original: {progressIncrement}, Processed: {processedIncrement}");
            }
            else
            {
                _lastCommittedSwipeSign = currentSwipeSign;
                _oppositeSwipeStreak = 0;
                Debug.Log($"[CurtainController] Opposite swipe confirmed. New direction: {_lastCommittedSwipeSign}");
            }
        }

        ApplyProgressChange(processedIncrement * swipeProgressMultiplier);
    }

    private void ApplyProgressChange(float change)
    {
        float newTarget = _targetProgress + change;
        newTarget = Mathf.Clamp(newTarget, 0f, targetProgressThreshold);

        if (Mathf.Approximately(newTarget, _targetProgress))
        {
            return;
        }

        GoToTargetProgress(newTarget, false);
    }

    private void GoToTargetProgress(float target, bool useSlideDuration)
    {
        _targetProgress = Mathf.Clamp(target, 0f, targetProgressThreshold);

        if (instantMove)
        {
            SetCurrentProgressInternal(_targetProgress);
            CheckCurtainFullState();
            return;
        }

        float duration = slideDuration;
        if (!useSlideDuration)
        {
            float distance = Mathf.Abs(_targetProgress - _currentProgress);
            if (slideSpeed <= 0)
            {
                Debug.LogError("[CurtainController] SlideSpeed must be positive");
                slideSpeed = 1f;
            }
            duration = distance / slideSpeed;
        }

        if (_progressCoroutine != null)
        {
            StopCoroutine(_progressCoroutine);
        }
        _progressCoroutine = StartCoroutine(AnimateProgress(_targetProgress, duration));
    }

    private IEnumerator AnimateProgress(float targetValue, float duration)
    {
        _isCurtainAnimating = true;
        float startValue = _currentProgress;
        float timeElapsed = 0f;
        float initialTarget = targetValue;

        if (Mathf.Approximately(duration, 0f) || Mathf.Approximately(startValue, targetValue))
        {
            SetCurrentProgressInternal(targetValue);
            OnProgressAnimationEnd();
            yield break;
        }

        while (timeElapsed < duration)
        {
            if (!Mathf.Approximately(_targetProgress, initialTarget))
            {
                GoToTargetProgress(_targetProgress, false);
                yield break;
            }

            timeElapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(timeElapsed / duration);
            float easedTime = EaseOutCubic(normalizedTime);
            float nextProgress = Mathf.LerpUnclamped(startValue, targetValue, easedTime);
            SetCurrentProgressInternal(nextProgress);
            yield return null;
        }

        SetCurrentProgressInternal(targetValue);
        OnProgressAnimationEnd();
    }

    private void OnProgressAnimationEnd()
    {
        _isCurtainAnimating = false;
        CheckCurtainFullState();
        _progressCoroutine = null;
    }

    private void SetCurrentProgressInternal(float progress)
    {
        _currentProgress = Mathf.Clamp(progress, 0f, targetProgressThreshold);
        ApplyCurtainProgress(_currentProgress);
    }

    private void ApplyCurtainProgress(float progress)
    {
        float normalized = 0f;
        if (targetProgressThreshold > 0)
        {
            normalized = Mathf.Clamp01(progress / targetProgressThreshold);
        }
        SetCurtainPosition(normalized);
    }

    private void SetCurtainPosition(float normalizedPosition)
    {
        if (curtainObject == null) return;

        float targetX = Mathf.Lerp(minXPosition, maxXPosition, normalizedPosition);
        curtainObject.localPosition = new Vector3(
            targetX,
            curtainObject.localPosition.y,
            curtainObject.localPosition.z
        );
    }

    private void CheckCurtainFullState()
    {
        bool wasFull = _isCurtainFull;
        _isCurtainFull = _currentProgress >= targetProgressThreshold;

        if (_isCurtainFull && !wasFull)
        {
            Debug.Log("[CurtainController] Curtain is now full");
            _onCurtainFullCallback?.Invoke();
        }
    }

    private void UpdateInactivityTimer()
    {
        _lastControlTime = Time.time;
        _inactivityTimer = 0f;
    }

    private void ResetInactivityTimer()
    {
        _lastControlTime = 0f;
        _inactivityTimer = 0f;
    }

    #region Easing Functions

    private float EaseOutCubic(float t)
    {
        return 1 - Mathf.Pow(1 - t, 3);
    }

    private float EaseInOutSine(float t)
    {
        return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
    }

    #endregion

    #region Public methods

    public Coroutine SlideCurtain(bool slideIn, Action onComplete = null)
    {
        ResetInactivityTimer();

        if (curtainRenderer != null)
        {
            curtainRenderer.enabled = true;
            SetCurtainAlpha(1f);
        }

        _lastCommittedSwipeSign = slideIn ? 1 : -1;
        _oppositeSwipeStreak = 0;

        float targetProgress = slideIn ? targetProgressThreshold : 0f;
        GoToTargetProgress(targetProgress, true);

        if (_progressCoroutine != null)
        {
            StartCoroutine(WaitForProgressCompletion(onComplete));
            return _progressCoroutine;
        }
        else
        {
            onComplete?.Invoke();
            return null;
        }
    }

    private IEnumerator WaitForProgressCompletion(Action onComplete)
    {
        if (_progressCoroutine != null)
        {
            yield return _progressCoroutine;
        }
        onComplete?.Invoke();
    }

    public Coroutine FadeCurtain(bool fadeIn, Action onComplete = null)
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (curtainRenderer != null)
            curtainRenderer.enabled = true;

        _fadeCoroutine = StartCoroutine(FadeAnimationSafe(fadeIn, onComplete));
        return _fadeCoroutine;
    }

    private IEnumerator FadeAnimationSafe(bool fadeIn, Action onComplete)
    {
        if (curtainRenderer == null)
        {
            Debug.LogError("[CurtainController] CurtainRenderer is null");
            onComplete?.Invoke();
            _fadeCoroutine = null;
            yield break;
        }

        float startAlpha = curtainRenderer.material.color.a;
        float targetAlpha = fadeIn ? 1f : 0f;

        if (Mathf.Abs(startAlpha - targetAlpha) < 0.01f)
        {
            SetCurtainAlpha(targetAlpha);
            FinalizeFade(fadeIn, onComplete);
            yield break;
        }

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(timer / fadeDuration);
            float easedT = EaseInOutSine(normalizedTime);
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, easedT);
            SetCurtainAlpha(newAlpha);
            yield return null;
        }

        SetCurtainAlpha(targetAlpha);
        FinalizeFade(fadeIn, onComplete);
    }

    private void FinalizeFade(bool fadedIn, Action onComplete)
    {
        if (!fadedIn)
        {
            if (curtainRenderer != null && curtainRenderer.material.color.a <= 0.01f)
                curtainRenderer.enabled = false;
            _onCurtainFadedCallback?.Invoke();
        }
        onComplete?.Invoke();
        _fadeCoroutine = null;
    }

    private void SetCurtainAlpha(float alpha)
    {
        if (curtainRenderer == null) return;

        Color c = curtainRenderer.material.color;
        c.a = alpha;
        curtainRenderer.material.color = c;
    }

    public void SetOnCurtainFullCallback(Action callback)
    {
        _onCurtainFullCallback = callback;
    }

    public void SetOnCurtainFadedCallback(Action callback)
    {
        _onCurtainFadedCallback = callback;
    }

    public void ResetCurtainProgress()
    {
        if (_progressCoroutine != null)
        {
            StopCoroutine(_progressCoroutine);
            _progressCoroutine = null;
        }
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        _currentProgress = 0f;
        _targetProgress = 0f;
        _isCurtainFull = false;
        _isCurtainAnimating = false;
        ResetInactivityTimer();
        _lastCommittedSwipeSign = 0;
        _oppositeSwipeStreak = 0;

        SetCurrentProgressInternal(0f);

        if (curtainRenderer != null)
        {
            SetCurtainAlpha(1f);
            curtainRenderer.enabled = true;
        }
    }

    #endregion
}