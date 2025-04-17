using InitializationFramework;
using LEDControl;
using System;
using System.Collections;
using UnityEngine;

public class StateManager : MonoBehaviour
{
    public enum AppState { Idle, Active, Transition }

    public AppState CurrentState { get; private set; } = AppState.Idle;

    [Header("Controllers")]
    [SerializeField] private InitializePlayers videoPlayer;
    [SerializeField] private VideoPlaybackController playbackController;
    [SerializeField] private CurtainController curtainController;
    [SerializeField] private CometController cometController;
    [SerializeField] private LEDController ledController;
    [SerializeField] private SoundManager soundManager;
    [SerializeField] private SPItouchPanel spiTouchPanel;
    [SerializeField] private EffectsManager effectsManager;
    [SerializeField] private SunManager sunManager;
    [SerializeField] private SwipeDetector swipeDetector;

    [Header("Transition Parameters")]
    [SerializeField] private float curtainFullWait = 0f;
    [SerializeField] private float swipeReactivateDelay = 0.5f;
    [SerializeField] private float soundFadeDuration = 1f;
    [SerializeField] private float cometDelayInTransition = 1f;
    [SerializeField] private float sunFadeOutOnTransitionDuration = 0.5f;
    [SerializeField] private float idleTimeout = 180.0f;

    // Internal state
    private float _lastInteractionTime;

    public event Action<AppState> OnStateChanged;
    public event Action<AppState> OnPreviousStateChanged;

    private void Awake()
    {
        if (effectsManager == null)
        {
            effectsManager = GetComponent<EffectsManager>();
            if (effectsManager == null)
            {
                Debug.LogError("[StateManager] EffectsManager not assigned and not found on GameObject!");
            }
        }

        if (swipeDetector == null)
        {
            swipeDetector = FindObjectOfType<SwipeDetector>();
            if (swipeDetector == null)
            {
                Debug.LogError("[StateManager] SwipeDetector not assigned and not found in scene!");
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            StartTransitionToIdle();
        }
    }

    private void Start()
    {
        CurrentState = AppState.Idle;
        sunManager?.SetAppState(CurrentState);
        playbackController?.SetSwipeControlEnabled(false);
        curtainController.SetOnCurtainFullCallback(OnCurtainFull);

        _lastInteractionTime = Time.time;
    }

    private void FixedUpdate()
    {
        if (CurrentState == AppState.Active && (Time.time - _lastInteractionTime > idleTimeout))
        {
            Debug.Log($"[StateManager] Idle timeout ({idleTimeout}s) reached. Initiating transition to Idle.");
            StartTransitionToIdle();
        }
    }

    public void ResetIdleTimer()
    {
        if (CurrentState == AppState.Active)
        {
            _lastInteractionTime = Time.time;
            Debug.Log("[StateManager] Idle timer reset due to interaction.");
        }
    }

    private void OnCurtainFull()
    {
        if (CurrentState == AppState.Idle)
        {
            Debug.Log("[StateManager] Curtain is full in Idle mode - transitioning to Active.");
            StartTransitionToActive();
        }
    }

    public void StartTransitionToActive()
    {
        if (CurrentState != AppState.Idle) return;
        StartCoroutine(TransitionToActiveCoroutine());
    }

    public void StartTransitionToIdle()
    {
        if (CurrentState != AppState.Active && CurrentState != AppState.Transition) return;
        StartCoroutine(TransitionToIdleCoroutine());
    }

    private IEnumerator TransitionToActiveCoroutine()
    {
        AppState previousState = CurrentState;
        SetState(AppState.Transition, previousState);

        curtainController.SetOnCurtainFullCallback(null);

        // Запускаем переход яркости в LEDController
        ledController.StartTransitionToActive();
        ledController.SwitchToActiveJSON();

        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });
        while (!slideCompleted) yield return null;

        bool cometFinished = false;
        cometController.StartCometTravel(() => { cometFinished = true; });
        while (!cometFinished) yield return null;

        sunManager?.StartSunFadeOut(sunFadeOutOnTransitionDuration);
        sunManager.SetAppState(AppState.Active);

        yield return new WaitForSeconds(cometDelayInTransition);

        if (spiTouchPanel != null && spiTouchPanel.stripDataManager != null)
        {
            for (int stripIndex = 0; stripIndex < spiTouchPanel.stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (spiTouchPanel.stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                {
                    float dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(effectsManager.synthLedCountBase + Mathf.Abs(effectsManager.CurrentCometSpeed) * effectsManager.speedLedCountFactor));
                    float dynamicBrightness = Mathf.Clamp01(spiTouchPanel.stripDataManager.GetStripBrightness(stripIndex) + Mathf.Abs(effectsManager.CurrentCometSpeed) * effectsManager.speedBrightnessFactor);
                    effectsManager.AddComet(stripIndex, 0, spiTouchPanel.stripDataManager.GetSynthColorForStrip(stripIndex), dynamicLedCount, dynamicBrightness);
                }
            }
        }

        soundManager.SetSoundClip(soundManager.ActiveClip);
        yield return new WaitForSeconds(curtainFullWait);

        yield return videoPlayer.SwitchToDefaultMode();

        soundManager?.StartFadeIn(soundFadeDuration);
        SetState(AppState.Active, CurrentState);

        bool fadeCompleted = false;
        curtainController.FadeCurtain(false, () => { fadeCompleted = true; });
        while (!fadeCompleted) yield return null;

        yield return new WaitForSeconds(swipeReactivateDelay);

        sunManager.SetAppState(AppState.Active);
        playbackController.SetSwipeControlEnabled(true);
        _lastInteractionTime = Time.time;
        Debug.Log("[StateManager] Transition to Active mode completed.");
    }

    private IEnumerator TransitionToIdleCoroutine()
    {
        AppState previousState = CurrentState;
        SetState(AppState.Transition, previousState);

        Debug.Log("[StateManager] Starting transition to Idle.");
        playbackController.SetSwipeControlEnabled(false);

        sunManager?.StartSunFadeOut(sunFadeOutOnTransitionDuration);
        sunManager?.SetAppState(AppState.Idle);

        // Запускаем переход яркости в LEDController
        ledController.StartTransitionToIdle();
        ledController.wasIdled = true;
        ledController.SwitchToIdleJSON();

        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });
        while (!slideCompleted) yield return null;

        soundManager?.StartFadeOut(soundFadeDuration);
        yield return videoPlayer.SwitchToIdleMode();
        yield return null;

        SetState(AppState.Idle, CurrentState);
        CompleteTransitionToIdle();
    }

    private void CompleteTransitionToIdle()
    {
        curtainController.FadeCurtain(false, () => { });

        curtainController.ResetCurtainProgress();
        cometController.ResetComet();

        if (spiTouchPanel != null && spiTouchPanel.stripDataManager != null)
        {
            for (int stripIndex = 0; stripIndex < spiTouchPanel.stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (spiTouchPanel.stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                {
                    effectsManager?.ResetComets(stripIndex);
                }
            }
        }

        Debug.Log("[StateManager] Transition to Idle mode completed.");
        curtainController.SetOnCurtainFullCallback(OnCurtainFull);
    }

    private void SetState(AppState newState, AppState previousState)
    {
        CurrentState = newState;
        OnPreviousStateChanged?.Invoke(previousState);
        OnStateChanged?.Invoke(CurrentState);
        sunManager?.SetAppState(CurrentState);

        if (CurrentState == AppState.Active)
        {
            _lastInteractionTime = Time.time;
        }
    }

    public void SwitchToIdleDirect(bool performTransition = true)
    {
        if (performTransition)
        {
            StartCoroutine(SwitchToIdleModeRoutine());
        }
        else
        {
            AppState previousState = CurrentState;
            SetState(AppState.Idle, previousState);

            playbackController.SetSwipeControlEnabled(false);
            curtainController.ResetCurtainProgress();
            cometController.ResetComet();
            soundManager?.StopSoundImmediately();
            soundManager.SetSoundClip(soundManager.IdleClip);

            if (spiTouchPanel != null && spiTouchPanel.stripDataManager != null)
            {
                for (int stripIndex = 0; stripIndex < spiTouchPanel.stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
                {
                    if (spiTouchPanel.stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                    {
                        effectsManager?.ResetComets(stripIndex);
                    }
                }
            }

            Debug.Log("[StateManager] Switched directly to Idle mode.");
            curtainController.SetOnCurtainFullCallback(OnCurtainFull);
        }
    }

    private IEnumerator SwitchToIdleModeRoutine()
    {
        AppState previousState = CurrentState;
        SetState(AppState.Idle, previousState);

        yield return videoPlayer.SwitchToIdleMode();
        ledController.SwitchToIdleJSON();
        soundManager?.StartFadeOut(soundFadeDuration);

        Debug.Log("[StateManager] Starting SwitchToIdle workflow.");
        playbackController.SetSwipeControlEnabled(false);

        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });
        while (!slideCompleted) yield return null;

        bool fadeCompleted = false;
        curtainController.FadeCurtain(false, () => { fadeCompleted = true; });
        while (!fadeCompleted) yield return null;

        soundManager.SetSoundClip(soundManager.IdleClip);
        curtainController.ResetCurtainProgress();
        cometController.ResetComet();

        if (spiTouchPanel != null && spiTouchPanel.stripDataManager != null)
        {
            for (int stripIndex = 0; stripIndex < spiTouchPanel.stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (spiTouchPanel.stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                {
                    effectsManager?.ResetComets(stripIndex);
                }
            }
        }

        Debug.Log("[StateManager] SwitchToIdle workflow completed.");
        curtainController.SetOnCurtainFullCallback(OnCurtainFull);
    }
}