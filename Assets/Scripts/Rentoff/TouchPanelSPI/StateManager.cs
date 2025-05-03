using InitializationFramework;
using LEDControl;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StateManager : MonoBehaviour
{
    public enum AppState { Idle, Active, Transition }
    public enum IdleTransitionMode { Curtain, ImageOverlay }

    public AppState CurrentState { get; private set; } = AppState.Idle;

    [Header("Controllers")]
    [SerializeField] private InitializePlayers videoPlayer;
    [SerializeField] private VideoPlaybackController playbackController;
    [SerializeField] private CurtainController curtainController;
    [SerializeField] private LEDController ledController;
    [SerializeField] private SoundManager soundManager;
    [SerializeField] private SPItouchPanel spiTouchPanel;
    [SerializeField] private EffectsManager effectsManager;
    [SerializeField] private SunManager sunManager;
    [SerializeField] private SwipeDetector swipeDetector;

    [Header("Transition Parameters")]
    [SerializeField] private float curtainFullWait = 0f;
    [SerializeField] private float swipeReactivateDelay = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float soundFadeDuration = 1f;
    [SerializeField] private float cometDelayInTransition = 1f;
    [SerializeField] private float sunFadeOutOnTransitionDuration = 0.5f;
    [SerializeField] private float idleTimeout = 180.0f;
    [SerializeField] private float delayBeforeVideoSwitch = 1f;
    [SerializeField] private float delayBeforeCurtainFadeOut = 0.5f;

    [Header("Idle Image Overlay Transition")]
    [SerializeField] private IdleTransitionMode idleTransitionMode = IdleTransitionMode.Curtain;
    [SerializeField] private Image transitionImage;
    [SerializeField] private float imageFadeDuration = 0.5f;
    [SerializeField] private float imageHoldDuration = 1f;

    private float _lastInteractionTime;

    public event Action<AppState> OnStateChanged;
    public event Action<AppState> OnPreviousStateChanged;

    private void Awake()
    {
        if (effectsManager == null)
            effectsManager = GetComponent<EffectsManager>();

        if (swipeDetector == null)
            swipeDetector = GetComponent<SwipeDetector>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
            StartTransitionToIdle();
    }

    private void Start()
    {
        CurrentState = AppState.Idle;
        sunManager?.SetAppState(CurrentState);
        playbackController?.SetSwipeControlEnabled(false);
        curtainController.SetOnCurtainFullCallback(OnCurtainFull);
        curtainController.SetShouldPlayComet(false);
        _lastInteractionTime = Time.time;

        if (transitionImage != null)
        {
            var col = transitionImage.color;
            col.a = 0;
            transitionImage.color = col;
            transitionImage.gameObject.SetActive(false);
        }
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
        curtainController.SetShouldPlayComet(true);

        soundManager?.PlayCometSound();
        soundManager?.StartFadeOut(delayBeforeVideoSwitch);

        ledController?.StartFadeIn();

        sunManager?.StartSunFadeOut(sunFadeOutOnTransitionDuration);


        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });
        while (!slideCompleted) yield return null;

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

        yield return new WaitForSeconds(delayBeforeVideoSwitch);

        yield return new WaitForSeconds(delayBeforeCurtainFadeOut);

        SetState(AppState.Active, CurrentState);

        sunManager?.SetAppState(AppState.Active);
        sunManager?.StartSunFadeIn(sunFadeOutOnTransitionDuration);

        yield return videoPlayer.SwitchToDefaultMode();
        ledController?.StartFadeOut();
        ledController?.SwitchToActiveJSON();
        soundManager?.ResetTimeCodeSounds();
        soundManager.SetSoundClip(soundManager.ActiveClip);
        soundManager?.StartFadeIn(fadeInDuration);


        bool fadeCompleted = false;
        curtainController.FadeCurtain(false, () => { fadeCompleted = true; });
        while (!fadeCompleted) yield return null;

        yield return new WaitForSeconds(swipeReactivateDelay);

        playbackController.SetSwipeControlEnabled(true);
        _lastInteractionTime = Time.time;

        curtainController.SetShouldPlayComet(false);
    }

    private IEnumerator TransitionToIdleCoroutine()
    {
        AppState previousState = CurrentState;
        SetState(AppState.Transition, previousState);

        if (spiTouchPanel != null && spiTouchPanel.stripDataManager != null)
        {
            for (int stripIndex = 0; stripIndex < spiTouchPanel.stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (spiTouchPanel.stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                    effectsManager?.ResetComets(stripIndex);
            }
        }

        Debug.Log("[StateManager] Starting transition to Idle.");

        curtainController.SetShouldPlayComet(false);
        playbackController.SetSwipeControlEnabled(false);

        sunManager?.StartSunFadeOut(sunFadeOutOnTransitionDuration);

        ledController?.StartFadeIn();

        soundManager?.StartFadeOut(soundFadeDuration);

        switch (idleTransitionMode)
        {
            case IdleTransitionMode.Curtain:
                yield return StartCoroutine(CurtainIdleTransition());
                break;

            case IdleTransitionMode.ImageOverlay:
                yield return StartCoroutine(ImageOverlayIdleTransition());
                break;
        }

    }

    private IEnumerator CurtainIdleTransition()
    {
        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });
        while (!slideCompleted) yield return null;



        SetState(AppState.Idle, CurrentState);
        CompleteTransitionToIdle();

        soundManager?.StartFadeOut(soundFadeDuration);
        yield return videoPlayer.SwitchToIdleMode();

        sunManager?.SetAppState(AppState.Idle);
        sunManager?.StartSunFadeIn(sunFadeOutOnTransitionDuration);
    }

    private IEnumerator ImageOverlayIdleTransition()
    {
        if (transitionImage == null)
        {
            Debug.LogError("[StateManager] Transition image not assigned, falling back to curtain transition.");
            yield return StartCoroutine(CurtainIdleTransition());
            yield break;
        }

        transitionImage.gameObject.SetActive(true);



        yield return StartCoroutine(FadeImage(true, imageFadeDuration));
        yield return new WaitForSeconds(imageHoldDuration);

        SetState(AppState.Idle, CurrentState);
        CompleteTransitionToIdle();

        soundManager.SetSoundClip(soundManager.IdleClip);
        soundManager?.StartFadeIn(soundFadeDuration);

        yield return videoPlayer.SwitchToIdleMode();

        if (ledController != null)
        {
            ledController.wasIdled = true;
            ledController.SwitchToIdleJSON();
        }

        ledController?.StartFadeOut();

        sunManager?.SetAppState(AppState.Idle);
        sunManager?.StartSunFadeIn(sunFadeOutOnTransitionDuration);

        yield return StartCoroutine(FadeImage(false, imageFadeDuration));



        transitionImage.gameObject.SetActive(false);
    }

    private IEnumerator FadeImage(bool fadeIn, float duration)
    {
        float timer = 0f;
        Color startColor = transitionImage.color;
        Color targetColor = startColor;
        targetColor.a = fadeIn ? 1f : 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            transitionImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        transitionImage.color = targetColor;
    }

    private void CompleteTransitionToIdle()
    {
        curtainController.FadeCurtain(false, () => { });

        curtainController.ResetCurtainProgress();
        curtainController.SetShouldPlayComet(false);

        curtainController.SetOnCurtainFullCallback(OnCurtainFull);
    }

    private void SetState(AppState newState, AppState previousState)
    {
        CurrentState = newState;
        OnPreviousStateChanged?.Invoke(previousState);
        OnStateChanged?.Invoke(CurrentState);
        //sunManager?.SetAppState(CurrentState);

        if (CurrentState == AppState.Active)
            _lastInteractionTime = Time.time;
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
            curtainController.SetShouldPlayComet(false);
            soundManager?.StopSoundImmediately();
            soundManager.SetSoundClip(soundManager.IdleClip);

            if (spiTouchPanel != null && spiTouchPanel.stripDataManager != null)
            {
                for (int stripIndex = 0; stripIndex < spiTouchPanel.stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
                {
                    if (spiTouchPanel.stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                        effectsManager?.ResetComets(stripIndex);
                }
            }

            curtainController.SetOnCurtainFullCallback(OnCurtainFull);
        }
    }

    private IEnumerator SwitchToIdleModeRoutine()
    {
        AppState previousState = CurrentState;
        SetState(AppState.Idle, previousState);

        yield return videoPlayer.SwitchToIdleMode();
        ledController.SwitchToIdleJSON();
        soundManager?.StartFadeOut(0.5f);

        playbackController.SetSwipeControlEnabled(false);

        curtainController.SetShouldPlayComet(false);

        bool slideCompleted = false;
        curtainController.SlideCurtain(true, () => { slideCompleted = true; });

        soundManager.SetSoundClip(soundManager.IdleClip);
        soundManager?.StartFadeIn(soundFadeDuration);

        curtainController.ResetCurtainProgress();
        curtainController.SetShouldPlayComet(false);

        if (spiTouchPanel != null && spiTouchPanel.stripDataManager != null)
        {
            for (int stripIndex = 0; stripIndex < spiTouchPanel.stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (spiTouchPanel.stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                    effectsManager?.ResetComets(stripIndex);
            }
        }

        while (!slideCompleted) yield return null;

        curtainController.SetOnCurtainFullCallback(OnCurtainFull);
    }
}