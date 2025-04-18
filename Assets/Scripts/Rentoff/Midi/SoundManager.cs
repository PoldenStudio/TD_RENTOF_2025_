using UnityEngine;
using System.Collections;
using DG.Tweening.Core.Easing;
using DemolitionStudios.DemolitionMedia;
using static StateManager;
using System.Collections.Generic;

[System.Serializable]
public class TimeCodeSound
{
    public float timeCodeInSeconds;
    public AudioClip soundClip;
    public float volume = 1f;
    [Tooltip("Set to true if this sound should play only once per session")]
    public bool playOnce = false;
    [HideInInspector]
    public bool hasPlayed = false;
}

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
public class SoundManager : MonoBehaviour
{
    private AudioSource synthSource;
    private AudioLowPassFilter lowPassFilter;
    [SerializeField] private StateManager stateManager;
    [SerializeField] private Media _demolitionMedia;

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

    [Header("Time Code Sounds")]
    [SerializeField] private List<TimeCodeSound> timeCodeSounds = new List<TimeCodeSound>();
    [SerializeField] private AudioSource timeCodeAudioSource; // Separate audio source for time code sounds

    private float swipePitchMultiplier = 1f;
    private bool isSwipeSoundActive = false;
    private Coroutine currentFadeCoroutine;

    private IMediaPlayer mediaPlayer;

    public AudioClip IdleClip;
    public AudioClip ActiveClip;
    public AudioClip CometSound;

    private AudioClip currentClip;
    private float lastFrameTime = -1f;

    private void Awake()
    {
        synthSource = GetComponent<AudioSource>();
        lowPassFilter = GetComponent<AudioLowPassFilter>();

        if (IdleClip == null)
        {
            Debug.LogError("IdleClip is not assigned in the inspector!");
            return;
        }

        mediaPlayer = new DemolitionMediaPlayer(_demolitionMedia);

        // Create a separate audio source for time code sounds if not assigned
        if (timeCodeAudioSource == null)
        {
            GameObject audioSourceObj = new GameObject("TimeCodeAudioSource");
            audioSourceObj.transform.parent = this.transform;
            timeCodeAudioSource = audioSourceObj.AddComponent<AudioSource>();
        }

        SetSoundClip(IdleClip);
        synthSource.loop = true;
        synthSource.playOnAwake = true;
        synthSource.volume = baseVolume;
        synthSource.pitch = basePitch;
        lowPassFilter.cutoffFrequency = maxCutoffFrequency;

        synthSource.Play();
    }

    private void FixedUpdate()
    {
        if (stateManager.CurrentState == AppState.Active && mediaPlayer != null)
        {
            float currentTime = mediaPlayer.CurrentTime;

            if (lastFrameTime < 0)
            {
                lastFrameTime = currentTime;
                return;
            }

            foreach (TimeCodeSound timeCodeSound in timeCodeSounds)
            {
                if (timeCodeSound.playOnce && timeCodeSound.hasPlayed)
                    continue;

                bool crossedForward = lastFrameTime < timeCodeSound.timeCodeInSeconds &&
                                    currentTime >= timeCodeSound.timeCodeInSeconds;

                bool crossedBackward = lastFrameTime > timeCodeSound.timeCodeInSeconds &&
                                     currentTime <= timeCodeSound.timeCodeInSeconds;

                if (crossedForward || crossedBackward)
                {
                    PlayTimeCodeSound(timeCodeSound);

                    if (timeCodeSound.playOnce)
                        timeCodeSound.hasPlayed = true;

                    Debug.Log($"Time code sound triggered at {timeCodeSound.timeCodeInSeconds}s (Current time: {currentTime}s)");
                }
            }

            lastFrameTime = currentTime;
        }
    }

    public void PlayTimeCodeSound(TimeCodeSound timeCodeSound)
    {
        if (timeCodeSound.soundClip != null)
        {
            timeCodeAudioSource.PlayOneShot(timeCodeSound.soundClip, timeCodeSound.volume);
        }
    }

    public void ResetTimeCodeSounds()
    {
        foreach (TimeCodeSound timeCodeSound in timeCodeSounds)
        {
            timeCodeSound.hasPlayed = false;
        }
        lastFrameTime = -1f;
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

    void Start()
    {
        SetSoundClip(IdleClip);
    }

    public void SetSoundClip(AudioClip clip)
    {
        if (currentClip != clip)
        {
            currentClip = clip;
            synthSource.clip = clip;
        }
    }

    public void PlayCometSound()
    {
        if (CometSound != null)
        {
            AudioSource.PlayClipAtPoint(CometSound, transform.position);
        }
    }

    public void UpdateSynthParameters(float currentSpeed)
    {
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