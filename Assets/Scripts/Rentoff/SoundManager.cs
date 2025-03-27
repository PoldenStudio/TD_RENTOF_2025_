using UnityEngine;
using System.Collections;
using DG.Tweening.Core.Easing;
using static StateManager;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
public class SoundManager : MonoBehaviour
{
    private AudioSource synthSource;
    private AudioLowPassFilter lowPassFilter;
    [SerializeField] private StateManager stateManager;

    [Header("Synth Settings")]
    public float basePitch = 1f;
    public float maxPitch = 1.5f;
    public float minCutoffFrequency = 500f;
    public float maxCutoffFrequency = 22000f;

    [Header("Acceleration Settings")]
    public float maxAccelerationDelta = 14f;

    [Header("Mix Settings")]
    public float baseVolume = 1f;

    [Header("Smoothing")]
    public float parameterSmoothing = 5f;
    public float volumeFadeRange = 1f;

    [Header("High Speed Mute Settings")]
    public bool muteAtHighSpeed = false;
    public float highSpeedThreshold = 2f;

    private float swipePitchMultiplier = 1f;
    private bool isSwipeSoundActive = false;
    private Coroutine currentFadeCoroutine;

    private void Awake()
    {
        synthSource = GetComponent<AudioSource>();
        lowPassFilter = GetComponent<AudioLowPassFilter>();

        synthSource.loop = true;
        synthSource.playOnAwake = false;
        synthSource.volume = 0f;
        synthSource.pitch = basePitch;
        lowPassFilter.cutoffFrequency = maxCutoffFrequency;
    }

    public void StartFadeIn(float duration)
    {
        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);

        synthSource.Play();
        currentFadeCoroutine = StartCoroutine(FadeSound(0f, baseVolume, duration));
    }

    public void StartFadeOut(float duration)
    {
        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);

        currentFadeCoroutine = StartCoroutine(FadeSound(synthSource.volume, 0f, duration, true));
    }

    public void StopSoundImmediately()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
            currentFadeCoroutine = null;
        }
        synthSource.volume = 0f;
        synthSource.Stop();
    }

    private IEnumerator FadeSound(float startVolume, float targetVolume, float duration, bool stopAfterFade = false)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            synthSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        synthSource.volume = targetVolume;
        if (stopAfterFade)
        {
            synthSource.Stop();
        }
        currentFadeCoroutine = null;
    }

    public void UpdateSynthParameters(float currentSpeed)
    {
        // Не обновляем параметры во время fade
        if (currentFadeCoroutine != null)
            return;

        if (stateManager.CurrentState != AppState.Active)
            return;

        float speedDelta = Mathf.Abs(currentSpeed) - 1f;
        float t = Mathf.Clamp01(speedDelta / maxAccelerationDelta);

        float targetPitch = Mathf.Lerp(basePitch, maxPitch, t);
        if (currentSpeed < 0)
            targetPitch *= -1f;

        float targetCutoff = Mathf.Lerp(maxCutoffFrequency, minCutoffFrequency, t);

        synthSource.pitch = Mathf.Lerp(synthSource.pitch, targetPitch, Time.deltaTime * parameterSmoothing);
        lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetCutoff, Time.deltaTime * parameterSmoothing);

        // Управление громкостью
        float targetVolume = baseVolume;

        // 1. Плавное снижение громкости около нулевой скорости
        if (Mathf.Abs(currentSpeed) <= volumeFadeRange)
        {
            targetVolume = Mathf.Lerp(0f, baseVolume, Mathf.Abs(currentSpeed) / volumeFadeRange);
        }

        // 2. Режим "Mute at High Speed": затухание при |currentSpeed| > highSpeedThreshold
        if (muteAtHighSpeed && Mathf.Abs(currentSpeed) > highSpeedThreshold)
        {
            targetVolume = 0f;
        }

        synthSource.volume = Mathf.Lerp(synthSource.volume, targetVolume, Time.deltaTime * parameterSmoothing);
    }

    public void OnPanelSwipe(float avgTimeBetween)
    {
        if (!synthSource.isPlaying || currentFadeCoroutine != null)
            return;

        if (!isSwipeSoundActive)
        {
            isSwipeSoundActive = true;
            synthSource.pitch = basePitch * swipePitchMultiplier;
            swipePitchMultiplier += 0.1f;
        }
        else
        {
            synthSource.pitch = basePitch * swipePitchMultiplier;
        }

        float pitchIncrease = Mathf.Clamp(1f / avgTimeBetween, 1f, 1.5f);
        synthSource.pitch *= pitchIncrease;
    }

    public void ResetSwipeSound()
    {
        isSwipeSoundActive = false;
        swipePitchMultiplier = 1f;
        synthSource.pitch = basePitch;
    }

    public void ToggleMuteAtHighSpeed(bool enabled)
    {
        muteAtHighSpeed = enabled;
    }
}