using UnityEngine;
using DemolitionStudios.DemolitionMedia;

public class MediaController : MonoBehaviour
{
    public DemolitionStudios.DemolitionMedia.Media demolitionMedia;
    public InputManager inputManager; //  Теперь получает ввод от InputManager
    public float swipeSpeedMultiplier = 0.01f;
    public float maxPlaybackSpeed = 5.0f;
    public float minPlaybackSpeed = -5.0f;

    private bool isPausedAfterSwipe = false;
    private float pauseDuration = 1.0f;
    private float lastSwipeTime;
    public bool IsEnabled { get; set; } = true; // Включен по умолчанию

    void Start()
    {
        if (demolitionMedia == null)
        {
            Debug.LogError("MediaController: DemolitionMedia not assigned!");
            return;
        }

        if (inputManager == null)
        {
            Debug.LogError("MediaController: InputManager not assigned!");
            return;
        }
        if (IsEnabled)
        {
            inputManager.OnSwipeDetected += OnSwipeDetected; //  Подписываемся на событие через InputManager
        }
    }

    void OnSwipeDetected(Vector2 swipeDirection, float swipeSpeed)
    {
        if (!IsEnabled) return; // Проверяем, включено ли взаимодействие с видео

        if (Time.time - lastSwipeTime < pauseDuration)
        {
            return;
        }

        float playbackSpeedChange = swipeSpeed * swipeSpeedMultiplier;

        if (swipeDirection.x > 0)
        {
            demolitionMedia.PlaybackSpeed = Mathf.Clamp(demolitionMedia.PlaybackSpeed + playbackSpeedChange, minPlaybackSpeed, maxPlaybackSpeed);
            Debug.Log("MediaController: Swipe Right - Playback Speed: " + demolitionMedia.PlaybackSpeed);
        }
        else if (swipeDirection.x < 0)
        {
            demolitionMedia.PlaybackSpeed = Mathf.Clamp(demolitionMedia.PlaybackSpeed - playbackSpeedChange, minPlaybackSpeed, maxPlaybackSpeed);
            Debug.Log("MediaController: Swipe Left - Playback Speed: " + demolitionMedia.PlaybackSpeed);
        }

        lastSwipeTime = Time.time;

        if (!isPausedAfterSwipe)
        {
            demolitionMedia.Pause();
            Invoke("ResumePlayback", pauseDuration);
            isPausedAfterSwipe = true;
        }
    }

    void ResumePlayback()
    {
        demolitionMedia.Play();
        isPausedAfterSwipe = false;
    }

    void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnSwipeDetected -= OnSwipeDetected; //  Отписываемся от события через InputManager
        }
    }
}