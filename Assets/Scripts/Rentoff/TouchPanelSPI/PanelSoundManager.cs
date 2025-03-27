using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PanelSoundManager : MonoBehaviour
{
    [Header("Sound Settings")]
    public AudioClip touchSound;
    public float baseFrequency = 440f;
    public float frequencyStep = 100f;
    public float maxFrequency = 1000f;
    public float frequencyDecayTime = 0.5f;

    private AudioSource _audioSource;
    private float _currentFrequency;
    private bool _isSwiping;
    private Coroutine _decayCoroutine;
    public SwipeDetector _swipeDetector;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _currentFrequency = baseFrequency;
        _isSwiping = false;

        if (_swipeDetector == null)
        {
            Debug.LogError("SwipeDetector не найден в сцене. PanelSoundManager не будет работать корректно.");
        }
    }

    private void Start()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (_swipeDetector != null)
        {
            _swipeDetector.PanelPressed += OnPanelPressed;
            _swipeDetector.SwipeDetected += OnSwipeDetected;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_swipeDetector != null)
        {
            _swipeDetector.PanelPressed -= OnPanelPressed;
            _swipeDetector.SwipeDetected -= OnSwipeDetected;
        }
    }

    private void OnPanelPressed(int panelIndex, bool isPressed)
    {
        if (isPressed && touchSound != null)
        {
            PlaySound(touchSound);
        }
    }

    private void OnSwipeDetected(SwipeDetector.SwipeData swipeData)
    {
        _isSwiping = true;
        _currentFrequency = baseFrequency;

        if (_decayCoroutine != null)
        {
            StopCoroutine(_decayCoroutine);
            _decayCoroutine = null;
        }

        StartCoroutine(AdjustFrequencyDuringSwipe(swipeData));
    }

    private IEnumerator AdjustFrequencyDuringSwipe(SwipeDetector.SwipeData swipeData)
    {
        float stepDuration = swipeData.avgTimeBetween;

        float frequencyIncreasePerPanel = Mathf.Min(frequencyStep, (maxFrequency - baseFrequency) / Mathf.Max(1, swipeData.panelsCount - 1));

        for (int i = 0; i < swipeData.panelsCount; i++)
        {
            _currentFrequency = Mathf.Min(_currentFrequency + frequencyIncreasePerPanel, maxFrequency);
            _audioSource.pitch = _currentFrequency / baseFrequency;
            if (i < swipeData.panelsCount - 1)
            {
                yield return new WaitForSeconds(stepDuration);
            }
        }

        _decayCoroutine = StartCoroutine(DecayFrequency());
    }

    private IEnumerator DecayFrequency()
    {
        float elapsedTime = 0f;
        float startFrequency = _currentFrequency;
        while (elapsedTime < frequencyDecayTime)
        {
            _currentFrequency = Mathf.Lerp(startFrequency, baseFrequency, elapsedTime / frequencyDecayTime);
            _audioSource.pitch = _currentFrequency / baseFrequency;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        _currentFrequency = baseFrequency;
        _isSwiping = false;
        _decayCoroutine = null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;

        if (_audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
        _audioSource.clip = clip;
        _audioSource.pitch = _currentFrequency / baseFrequency;
        _audioSource.Play();
    }
}