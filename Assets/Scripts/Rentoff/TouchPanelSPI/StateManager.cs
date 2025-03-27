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
    [SerializeField] private SoundManager soundManager; // Добавлена ссылка на SoundManager

    [Header("Transition Parameters")]
    [SerializeField] private float curtainFullWait = 1f;
    [SerializeField] private float swipeReactivateDelay = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float soundFadeDuration = 1f;

    public event Action<AppState> OnStateChanged;
    public event Action<AppState> OnPreviousStateChanged;

    void Start()
    {
        CurrentState = AppState.Idle;
        playbackController?.SetSwipeControlEnabled(false);
        curtainController.SetOnCurtainFullCallback(OnCurtainFull);
    }

    private void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(TransitionToIdleCoroutine());
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
        if (CurrentState != AppState.Active) return;
        StartCoroutine(TransitionToIdleCoroutine());
    }

    private IEnumerator TransitionToActiveCoroutine()
    {
        AppState previousState = CurrentState;
        CurrentState = AppState.Transition;
        OnPreviousStateChanged?.Invoke(previousState);
        OnStateChanged?.Invoke(CurrentState);


        curtainController.SetOnCurtainFullCallback(null);

        // LED переход: fade-out, смена JSON на активный, fade-in
        yield return ledController.FadeOut(fadeOutDuration);
        ledController.SwitchToActiveJSON();
        yield return ledController.FadeIn(fadeInDuration, ledController.DefaultGlobalBrightness);

        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });
        while (!slideCompleted)
            yield return null;

        bool cometFinished = false;
        cometController.StartCometTravel(() => { cometFinished = true; });
        while (!cometFinished)
            yield return null;

        yield return new WaitForSeconds(curtainFullWait);

        yield return videoPlayer.SwitchToDefaultMode();
        soundManager?.StartFadeIn(soundFadeDuration);

        bool fadeCompleted = false;
        curtainController.FadeCurtain(false, () => { fadeCompleted = true; });
        while (!fadeCompleted)
            yield return null;

        yield return new WaitForSeconds(swipeReactivateDelay);

        previousState = CurrentState;
        CurrentState = AppState.Active;
        OnPreviousStateChanged?.Invoke(previousState);
        OnStateChanged?.Invoke(CurrentState);

        playbackController.SetSwipeControlEnabled(true);

        Debug.Log("[StateManager] Transition to Active mode completed.");
    }

    private IEnumerator TransitionToIdleCoroutine()
    {
        AppState previousState = CurrentState;
        CurrentState = AppState.Transition;
        OnPreviousStateChanged?.Invoke(previousState);
        OnStateChanged?.Invoke(CurrentState);

        Debug.Log("[StateManager] Starting transition to Idle.");
        playbackController.SetSwipeControlEnabled(false);


        // LED переход: переключаем JSON на Idle (без fade-эффекта)
        ledController.SwitchToIdleJSON();

        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });
        while (!slideCompleted)
            yield return null;

        soundManager?.StartFadeOut(soundFadeDuration);
        yield return videoPlayer.SwitchToIdleMode();

        previousState = CurrentState;
        CurrentState = AppState.Idle;
        OnPreviousStateChanged?.Invoke(previousState);
        OnStateChanged?.Invoke(CurrentState);

        bool fadeCompleted = false;
        curtainController.FadeCurtain(false, () => { fadeCompleted = true; });
        while (!fadeCompleted)
            yield return null;

        curtainController.ResetCurtainProgress();
        cometController.ResetComet();

        Debug.Log("[StateManager] Transition to Idle mode completed.");
        curtainController.SetOnCurtainFullCallback(OnCurtainFull);
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
            CurrentState = AppState.Idle;
            playbackController.SetSwipeControlEnabled(false);
            curtainController.ResetCurtainProgress();
            cometController.ResetComet();

            soundManager?.StopSoundImmediately();

            OnPreviousStateChanged?.Invoke(previousState);
            OnStateChanged?.Invoke(CurrentState);
            Debug.Log("[StateManager] Switched directly to Idle mode.");
            curtainController.SetOnCurtainFullCallback(OnCurtainFull);
        }
    }

    private IEnumerator SwitchToIdleModeRoutine()
    {
        AppState previousState = CurrentState;
        CurrentState = AppState.Idle;
        OnPreviousStateChanged?.Invoke(previousState);
        OnStateChanged?.Invoke(CurrentState);

        yield return videoPlayer.SwitchToIdleMode();
        ledController.SwitchToIdleJSON();

        soundManager?.StartFadeOut(soundFadeDuration);

        Debug.Log("[StateManager] Starting SwitchToIdle workflow.");
        playbackController.SetSwipeControlEnabled(false);

        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });
        while (!slideCompleted)
            yield return null;

        bool fadeCompleted = false;
        curtainController.FadeCurtain(false, () => { fadeCompleted = true; });
        while (!fadeCompleted)
            yield return null;

        curtainController.ResetCurtainProgress();
        cometController.ResetComet();

        Debug.Log("[StateManager] SwitchToIdle workflow completed.");
        curtainController.SetOnCurtainFullCallback(OnCurtainFull);
    }
}