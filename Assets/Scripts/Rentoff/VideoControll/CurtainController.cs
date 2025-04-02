using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CurtainController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform curtainRect;
    [SerializeField] private Image curtainImage;

    [Header("Animation Settings")]
    [SerializeField] private float slideDuration = 2f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float slideSpeed = 0.1f;

    [Tooltip("True => instant snapping, False => uses a smooth Easing for motion.")]
    public bool instantMove = false;

    [Header("Logic Settings")]
    [SerializeField] private float targetProgressThreshold = 5f;
    [SerializeField] private float swipeProgressMultiplier = 1f;
    [SerializeField] private float inactivityCloseDelay = 4f;

    private float _finalApproachDelay = 0.3f;

    private float _currentProgress = 0f;
    private float _targetProgress = 0f;
    private Coroutine _slideCoroutine;
    private Coroutine _fadeCoroutine;
    private bool _isCurtainAnimating = false;
    private bool _isCurtainFull = false;

    private float _lastControlTime = 0f;
    private float _inactivityTimer = 0f;

    public bool IsCurtainFull => _isCurtainFull;

    private int _stableSign = 0;
    private int _pendingSign = 0;
    private int _pendingCount = 0;
    private float _pendingValue = 0f;
    private float _firstPendingValue = 0f;

    private Action _onCurtainFullCallback;
    private Action _onCurtainFadedCallback;

    private void Awake()
    {
        if (curtainRect == null || curtainImage == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }

        SetCurtainPosition(0f);
        SetCurtainAlpha(1f);
    }

    private void Update()
    {
        if (_lastControlTime > 0f)
        {
            _inactivityTimer += Time.deltaTime;

            if (_inactivityTimer >= inactivityCloseDelay && !_isCurtainAnimating && !_isCurtainFull)
            {
                Debug.Log("[CurtainController] Inactivity detected. Closing curtain.");
                SlideCurtain(false);
                _lastControlTime = 0f;
                _inactivityTimer = 0f;
            }
            else if (_inactivityTimer >= _finalApproachDelay && !_isCurtainAnimating)
            {
                if (!Mathf.Approximately(_currentProgress, _targetProgress))
                {
                    if (instantMove)
                    {
                        SetCurtainProgress(_targetProgress);
                    }
                    else
                    {
                        if (_slideCoroutine != null)
                            StopCoroutine(_slideCoroutine);

                        _slideCoroutine = StartCoroutine(SmoothSlideToTargetProgress(
                            toValue: _targetProgress
                        ));
                    }
                }
            }
        }
    }

    public void AddSwipeProgress(float progressIncrement)
    {
        _lastControlTime = Time.time;
        _inactivityTimer = 0f;

        int sign = Mathf.Sign(progressIncrement) == 0 ? 0 : (int)Mathf.Sign(progressIncrement);
        if (sign == 0) return;

        if (_stableSign == 0)
        {
            _stableSign = sign;
            AddSwipeProgressReal(progressIncrement);
            return;
        }

        if (sign == _stableSign)
        {
            _pendingSign = 0;
            _pendingCount = 0;
            _pendingValue = 0f;
            _firstPendingValue = 0f;
            AddSwipeProgressReal(progressIncrement);
            return;
        }
        else
        {
            if (_pendingSign == sign)
            {
                _pendingCount++;
                _pendingValue += progressIncrement;

                if (_pendingCount == 2)
                {
                    if (_pendingCount == 2)
                    {
                        _stableSign = sign;

                        AddSwipeProgressReal(_firstPendingValue * -1f);

                        AddSwipeProgressReal(_pendingValue);

                        _pendingSign = 0;
                        _pendingCount = 0;
                        _pendingValue = 0f;
                        _firstPendingValue = 0f; 
                    }
                }
            }
            else
            {
                _pendingSign = sign;
                _pendingCount = 1;
                _pendingValue = progressIncrement;
                _firstPendingValue = progressIncrement; 
            }
        }
    }

    private void AddSwipeProgressReal(float progressIncrement)
    {
        if (_currentProgress >= targetProgressThreshold)
        {
            Debug.Log("[CurtainController] Already full");
            return;
        }

        float newTarget = _currentProgress + progressIncrement * swipeProgressMultiplier;
        newTarget = Mathf.Clamp(newTarget, 0f, targetProgressThreshold);

        if (Mathf.Approximately(newTarget, _currentProgress))
        {
            return;
        }

        _targetProgress = newTarget;

        if (instantMove)
        {
            SetCurtainProgress(_targetProgress);
        }
        else
        {
            if (_slideCoroutine != null)
            {
                StopCoroutine(_slideCoroutine);
                _slideCoroutine = null;
            }
            _slideCoroutine = StartCoroutine(SmoothSlideToTargetProgress(
                toValue: _targetProgress
            ));
        }
    }

    private IEnumerator SmoothSlideToTargetProgress(float toValue)
    {
        _isCurtainAnimating = true;
        float startValue = _currentProgress;
        float progressDiff = toValue - startValue;

        if (Mathf.Approximately(progressDiff, 0f))
        {
            _isCurtainAnimating = false;
            _slideCoroutine = null;
            yield break;
        }

        float duration = Mathf.Abs(progressDiff) / slideSpeed;
        duration = Mathf.Max(duration, 0.05f);

        float timeElapsed = 0f;
        float initialDuration = duration;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(timeElapsed / initialDuration);
            float easedTime = EaseOutSmooth(normalizedTime);

            float currentTargetValue = toValue;
            if (!Mathf.Approximately(_targetProgress, toValue))
            {
                currentTargetValue = _targetProgress; 
                duration = Mathf.Max(Mathf.Abs(currentTargetValue - _currentProgress) / slideSpeed, 0.05f);
                initialDuration = duration; 
                timeElapsed = 0f; 
                toValue = currentTargetValue; 
                startValue = _currentProgress; 
            }

            float nextProgress = Mathf.Lerp(startValue, currentTargetValue, easedTime);
            SetCurtainProgress(nextProgress);

            if (Mathf.Abs(_currentProgress - currentTargetValue) < 0.001f)
            {
                SetCurtainProgress(currentTargetValue);
                break;
            }
            yield return null;
        }

        SetCurtainProgress(toValue); 
        _isCurtainAnimating = false;
        _slideCoroutine = null;

        if (_currentProgress >= targetProgressThreshold && !_isCurtainFull)
        {
            _isCurtainFull = true;
            Debug.Log("[CurtainController] Curtain is now full!");
            _onCurtainFullCallback?.Invoke();
        }
        else if (_currentProgress < targetProgressThreshold)
        {
            _isCurtainFull = false;
        }
    }

    // Even smoother easing function (similar to smoothstep but more pronounced start/end ease)
    private float EaseOutSmooth(float t)
    {
        return 1 - Mathf.Pow(1 - t, 3); // Out Cubic Easing
    }

    private float EaseOutBack(float t, float overshoot = 1.0f)
    {
        float s = overshoot;
        t = t - 1f;
        return (t * t * ((s + 1f) * t + s) + 1f);
    }

    private void SetCurtainProgress(float progress)
    {
        float clamped = Mathf.Clamp(progress, 0f, targetProgressThreshold);
        _currentProgress = clamped;

        float normalized = (_currentProgress / targetProgressThreshold);
        SetCurtainPosition(normalized);
    }

    private void SetCurtainPosition(float normalizedPosition)
    {
        if (curtainRect == null) return;

        float screenWidth = ((RectTransform)curtainRect.parent).rect.width;
        float clamp01 = Mathf.Clamp01(normalizedPosition);
        float targetX = Mathf.Lerp(-screenWidth, 0, clamp01);

        curtainRect.anchoredPosition = new Vector2(targetX, curtainRect.anchoredPosition.y);
    }

    #region Public methods

    public Coroutine SlideCurtain(bool slideIn, Action onComplete = null)
    {
        if (_slideCoroutine != null)
        {
            StopCoroutine(_slideCoroutine);
            _slideCoroutine = null;
        }

        _slideCoroutine = StartCoroutine(SlideAnimationSafe(slideIn, onComplete));
        return _slideCoroutine;
    }

    private IEnumerator SlideAnimationSafe(bool slideIn, Action onComplete)
    {
        _isCurtainAnimating = true;

        if (curtainImage != null)
        {
            curtainImage.enabled = true;
            SetCurtainAlpha(1f);
        }

        float screenWidth = ((RectTransform)curtainRect.parent).rect.width;
        float startPosition = curtainRect.anchoredPosition.x;
        float targetPosition = slideIn ? 0 : -screenWidth;

        // Устанавливаем целевой прогресс в начале анимации
        float targetProgress = slideIn ? targetProgressThreshold : 0f;
        _targetProgress = targetProgress;

        if (instantMove)
        {
            curtainRect.anchoredPosition = new Vector2(targetPosition, curtainRect.anchoredPosition.y);
            _isCurtainAnimating = false;
            _isCurtainFull = slideIn;
            _currentProgress = targetProgress;
            _targetProgress = targetProgress;
            onComplete?.Invoke();
            _slideCoroutine = null;
            Debug.Log($"[CurtainController] Slide animation completed instantly: slideIn={slideIn}, final pos={curtainRect.anchoredPosition.x}");
            yield break;
        }

        float duration = slideDuration;
        float timeElapsed = 0f;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(timeElapsed / duration);
            float easedT = EaseOutQuad(t);
            float newX = Mathf.Lerp(startPosition, targetPosition, easedT);

            // Обновляем позицию шторки
            curtainRect.anchoredPosition = new Vector2(newX, curtainRect.anchoredPosition.y);

            // Обновляем текущий прогресс на основе новой позиции
            float normalized = (newX + screenWidth) / screenWidth; 
            float currentProgress = normalized * targetProgressThreshold;
            _currentProgress = currentProgress;

            yield return null;
        }

        // Убедимся, что прогресс установлен точно в целевое значение
        _currentProgress = targetProgress;
        _targetProgress = targetProgress;
        _isCurtainFull = slideIn;

        // Сброс прогресса, если шторка закрыта
        if (!slideIn)
        {

        }

        Debug.Log($"[CurtainController] Slide animation completed: slideIn={slideIn}, final pos={curtainRect.anchoredPosition.x}");
        onComplete?.Invoke();
        _slideCoroutine = null;
    }
    private float EaseOutQuad(float t)
    {
        return t * (2 - t);
    }

    public Coroutine FadeCurtain(bool fadeIn, Action onComplete = null)
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (curtainImage != null)
            curtainImage.enabled = true;

        _fadeCoroutine = StartCoroutine(FadeAnimationSafe(fadeIn, onComplete));
        return _fadeCoroutine;
    }

    private IEnumerator FadeAnimationSafe(bool fadeIn, Action onComplete)
    {
        if (curtainImage == null)
        {
            Debug.LogError("[CurtainController] Fade failed - curtainImage is null");
            onComplete?.Invoke();
            yield break;
        }

        float startAlpha = curtainImage.color.a;
        float targetAlpha = fadeIn ? 1f : 0f;

        if (Mathf.Abs(startAlpha - targetAlpha) < 0.01f)
        {
            SetCurtainAlpha(targetAlpha);
            onComplete?.Invoke();
            if (!fadeIn)
                _onCurtainFadedCallback?.Invoke();
            _fadeCoroutine = null;
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
        if (!fadeIn && targetAlpha <= 0.01f)
            curtainImage.enabled = false;

        onComplete?.Invoke();
        if (!fadeIn)
            _onCurtainFadedCallback?.Invoke();
        _fadeCoroutine = null;
    }

    private float EaseInOutSine(float t)
    {
        return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
    }

    private void SetCurtainAlpha(float alpha)
    {
        if (curtainImage == null) return;

        Color c = curtainImage.color;
        c.a = alpha;
        curtainImage.color = c;
        curtainImage.raycastTarget = (alpha > 0.01f);

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
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
        _currentProgress = 0f;
        _targetProgress = 0f;
        _isCurtainFull = false;
        _lastControlTime = 0f;
        _inactivityTimer = 0f;

        _stableSign = 0;
        _pendingSign = 0;
        _pendingCount = 0;
        _pendingValue = 0f;
        _firstPendingValue = 0f; 

        if (_slideCoroutine != null)
        {
            StopCoroutine(_slideCoroutine);
            _slideCoroutine = null;
        }
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        SetCurtainProgress(0f);

        float screenWidth = ((RectTransform)curtainRect.parent).rect.width;
        curtainRect.anchoredPosition = new Vector2(-screenWidth, curtainRect.anchoredPosition.y);

        if (curtainImage != null)
        {
            Color c = curtainImage.color;
            c.a = 1f;
            curtainImage.color = c;
            curtainImage.enabled = true;
        }
    }

    #endregion
}