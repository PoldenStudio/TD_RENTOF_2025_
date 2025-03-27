using UnityEngine;

public class AudioFeedback : MonoBehaviour
{
    public InputManager inputManager; //  Теперь получает ввод от InputManager
    public AudioSource audioSource;
    public float baseFrequency = 440f;
    public float frequencyIncrement = 20f;
    public bool IsEnabled { get; set; } = true; // Включен по умолчанию

    void Start()
    {
        if (inputManager == null)
        {
            Debug.LogError("AudioFeedback: InputManager not assigned!");
            return;
        }
        if (audioSource == null)
        {
            Debug.LogError("AudioFeedback: AudioSource not assigned!");
            return;
        }
        if (IsEnabled)
        {
            inputManager.OnPanelPressed += OnPanelPressed; //  Подписываемся на событие через InputManager
        }
    }

    void OnPanelPressed(int panelIndex)
    {
        if (!IsEnabled) return; // Проверяем, включено ли звуковое сопровождение

        float frequency = baseFrequency + (panelIndex * frequencyIncrement);
        frequency = Mathf.Clamp(frequency, 100f, 2000f);

        PlayTone(frequency);
    }

    void PlayTone(float frequency)
    {
        audioSource.pitch = frequency / baseFrequency;
        audioSource.Play();
    }

    void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnPanelPressed -= OnPanelPressed; //  Отписываемся от события через InputManager
        }
    }
}