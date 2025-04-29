using System;
using System.Collections;
using UnityEngine;
using DemolitionStudios.DemolitionMedia;

public class CurtainController : MonoBehaviour
{
    private enum PlaybackState
    {
        Normal,
        Decelerating,
        HoldAccelerating
    }

    [Header("References")]
    [SerializeField] private Transform curtainObject;
    [SerializeField] private MeshRenderer curtainRenderer;

    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float minXPosition = -1032f;
    [SerializeField] private float maxXPosition = 0f;

    [Header("Curtain Movement Settings")]
    [SerializeField] private float smoothTimeNormal = 0.15f;
    [SerializeField] private float smoothTimeSwipeMin = 0.1f;
    [SerializeField] private float smoothTimeSwipeMax = 0.3f;
    [SerializeField] private float smoothTimeHoldMin = 0.1f;
    [SerializeField] private float smoothTimeHoldMax = 0.4f;
    [SerializeField] private float curtainSensitivity = 0.5f;
    [SerializeField] private float curtainThreshold = 0.9f;

    [Header("Media Players")]
    [SerializeField] private Media curtainMedia;
    [SerializeField] private InitializationFramework.InitializePlayers initializePlayers;
    [SerializeField] private StateManager stateManager;

    private PlaybackState _state = PlaybackState.Normal;
    private float _currentPosition = 0f; // 0 = closed, 1 = open
    private float _targetPosition = 0f;
    private float _velocity = 0f;
    private bool _isCurtainFull = false;
    private bool _isCometPlaying = false;
    private bool _isHolding = false;
    private float _holdZeroPosition = 0f;
    private Vector2 _holdPosition;
    private float _holdStartTime = 0f;

    private Coroutine _fadeCoroutine;
    private Coroutine _cometPlaybackCoroutine;

    private bool _shouldPlayComet = false;
    private bool _shouldCheckCometOnFull = false;

    public bool IsCurtainFull => _isCurtainFull;

    private Action _onCurtainFullCallback;
    private Action _onCurtainFadedCallback;

    private void Awake()
    {
        if (curtainObject == null || curtainRenderer == null)
        {
            Debug.LogError("[CurtainController] References not set");
            return;
        }

        ResetCurtainPosition();
        SetCurtainAlpha(1f);
    }

    private void Start()
    {
        if (curtainMedia == null)
        {
            Debug.LogError("[CurtainController] Curtain Media is not assigned");
        }

        curtainMedia.Play();

        if (initializePlayers == null)
        {
            Debug.LogError("[CurtainController] InitializePlayers reference not set");
        }
    }

    private void Update()
    {
        UpdateCurtainState();
        UpdateCurtainPosition();
        CheckCurtainFullState();
    }

    private void UpdateCurtainState()
    {
        float dt = Time.deltaTime;
        float currentSmoothTime = 0;

        switch (_state)
        {
            case PlaybackState.Decelerating:
                float targetPositionMagnitude = Mathf.Abs(_targetPosition);
                currentSmoothTime = Mathf.Lerp(smoothTimeSwipeMin, smoothTimeSwipeMax, targetPositionMagnitude);
                _currentPosition = Mathf.SmoothDamp(_currentPosition, _targetPosition, ref _velocity, currentSmoothTime, Mathf.Infinity, dt);

                if (Mathf.Abs(_currentPosition - _targetPosition) < 0.01f)
                {
                    _currentPosition = _targetPosition;
                    _state = PlaybackState.Normal;
                    _velocity = 0f;
                    Debug.Log($"[CurtainController] Deceleration complete. Final position: {_currentPosition:F2}");
                }

                break;

            case PlaybackState.HoldAccelerating:
                currentSmoothTime = Mathf.Lerp(smoothTimeHoldMin, smoothTimeHoldMax, Mathf.Abs(_currentPosition));
                _currentPosition = Mathf.SmoothDamp(_currentPosition, _holdZeroPosition, ref _velocity, currentSmoothTime, Mathf.Infinity, dt);

                if (Mathf.Abs(_currentPosition - _holdZeroPosition) < 0.01f)
                {
                    _currentPosition = _holdZeroPosition;
                }

                if (!_isHolding)
                {
                    HandleReleaseAfterHold();
                }
                break;

            case PlaybackState.Normal:
                currentSmoothTime = smoothTimeNormal;
                _currentPosition = Mathf.SmoothDamp(_currentPosition, _targetPosition, ref _velocity, currentSmoothTime, Mathf.Infinity, dt);
                break;
        }
    }

    private void UpdateCurtainPosition()
    {
        if (curtainObject == null) return;

        float positionNormalized = Mathf.Clamp01(_currentPosition);
        float xPosition = Mathf.Lerp(minXPosition, maxXPosition, positionNormalized);

        curtainObject.localPosition = new Vector3(
            xPosition,
            curtainObject.localPosition.y,
            curtainObject.localPosition.z
        );
    }

    public void ApplySwipeMovement(float movement)
    {
        if (_state == PlaybackState.HoldAccelerating)
            return;

        float newPosition = _currentPosition + movement * curtainSensitivity;
        newPosition = Mathf.Clamp01(newPosition);

        _targetPosition = newPosition;
        _state = PlaybackState.Decelerating;
        _velocity = 0f;
    }

    public void ApplyMouseSwipe(Vector2 direction, float speed)
    {
        if (_state == PlaybackState.HoldAccelerating)
            return;

        float horizontalFactor = direction.x;
        float movement = -horizontalFactor * speed * curtainSensitivity * 0.01f;

        float newPosition = _currentPosition + movement;
        newPosition = Mathf.Clamp01(newPosition);

        _targetPosition = newPosition;
        _state = PlaybackState.Decelerating;
        _velocity = 0f;
    }

    public void OnMouseHoldStart(Vector2 position)
    {
        _isHolding = true;
        _holdPosition = position;
        _holdStartTime = Time.time;
        _holdZeroPosition = _currentPosition;

        _state = PlaybackState.HoldAccelerating;
        _velocity = 0f;
    }

    public void OnMouseHoldEnd()
    {
        HandleReleaseAfterHold();
    }

    private void HandleReleaseAfterHold()
    {
        if (_state == PlaybackState.HoldAccelerating && _isHolding)
        {
            _isHolding = false;

            _state = PlaybackState.Decelerating;

            // Decide where to snap to based on current position
            if (_currentPosition >= curtainThreshold)
            {
                _targetPosition = 1f;
            }
            else
            {
                _targetPosition = 0f;
            }

            _velocity = 0f;
            Debug.Log($"[CurtainController] Hold release => target: {_targetPosition}");
        }
    }

    private void CheckCurtainFullState()
    {
        bool wasFull = _isCurtainFull;
        _isCurtainFull = _currentPosition >= curtainThreshold;

        if (_isCurtainFull && _shouldPlayComet && _shouldCheckCometOnFull)
        {
            _shouldCheckCometOnFull = false;
            if (!_isCometPlaying && initializePlayers != null)
            {
                _isCometPlaying = true;
                if (_cometPlaybackCoroutine != null)
                {
                    StopCoroutine(_cometPlaybackCoroutine);
                }
                _cometPlaybackCoroutine = StartCoroutine(PlayCometSequence());
            }
        }

        if (_isCurtainFull && !wasFull)
        {
            Debug.Log("[CurtainController] Curtain is now full");
            _onCurtainFullCallback?.Invoke();
        }
    }

    private IEnumerator PlayCometSequence()
    {
        Debug.Log("[CurtainController] Starting comet sequence");

        if (initializePlayers != null)
        {
            yield return StartCoroutine(initializePlayers.PlayCometVideo());
        }
        else
        {
            Debug.LogError("[CurtainController] InitializePlayers reference is null, cannot play comet video");
        }

        _isCometPlaying = false;
        _cometPlaybackCoroutine = null;
        Debug.Log("[CurtainController] Comet sequence completed");
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

    public void SlideCurtain(bool slideIn, Action onComplete = null)
    {
        CancelHold();

        if (curtainRenderer != null)
        {
            curtainRenderer.enabled = true;
            SetCurtainAlpha(1f);
        }

        _targetPosition = slideIn ? 1f : 0f;
        _state = PlaybackState.Decelerating;
        _velocity = 0f;

        // Wait one frame to check if animation has completed
        StartCoroutine(WaitForSlideComplete(onComplete));
    }

    private IEnumerator WaitForSlideComplete(Action onComplete)
    {
        yield return null;

        // Wait until the state changes back to Normal
        while (_state == PlaybackState.Decelerating)
        {
            yield return null;
        }

        onComplete?.Invoke();
    }

    public void FadeCurtain(bool fadeIn, Action onComplete = null)
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (curtainRenderer != null)
            curtainRenderer.enabled = true;

        _fadeCoroutine = StartCoroutine(FadeAnimationSafe(fadeIn, onComplete));
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
        CancelHold();

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (_cometPlaybackCoroutine != null)
        {
            StopCoroutine(_cometPlaybackCoroutine);
            _cometPlaybackCoroutine = null;
        }

        ResetCurtainPosition();
        _state = PlaybackState.Normal;
        _velocity = 0f;
        _targetPosition = 0f;
        _isCurtainFull = false;
        _isCometPlaying = false;

        _shouldPlayComet = false;
        _shouldCheckCometOnFull = false;

        if (curtainRenderer != null)
        {
            SetCurtainAlpha(1f);
            curtainRenderer.enabled = true;
        }
    }

    private void ResetCurtainPosition()
    {
        _currentPosition = 0f;

        if (curtainObject != null)
        {
            curtainObject.localPosition = new Vector3(
                minXPosition,
                curtainObject.localPosition.y,
                curtainObject.localPosition.z
            );
        }
    }

    private void CancelHold()
    {
        _isHolding = false;
    }

    public void SetShouldPlayComet(bool value)
    {
        _shouldPlayComet = value;
        _shouldCheckCometOnFull = value;
    }

    #endregion
}