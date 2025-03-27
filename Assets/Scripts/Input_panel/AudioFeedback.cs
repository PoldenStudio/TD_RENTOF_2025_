using UnityEngine;

public class AudioFeedback : MonoBehaviour
{
    public InputManager inputManager; //  ������ �������� ���� �� InputManager
    public AudioSource audioSource;
    public float baseFrequency = 440f;
    public float frequencyIncrement = 20f;
    public bool IsEnabled { get; set; } = true; // ������� �� ���������

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
            inputManager.OnPanelPressed += OnPanelPressed; //  ������������� �� ������� ����� InputManager
        }
    }

    void OnPanelPressed(int panelIndex)
    {
        if (!IsEnabled) return; // ���������, �������� �� �������� �������������

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
            inputManager.OnPanelPressed -= OnPanelPressed; //  ������������ �� ������� ����� InputManager
        }
    }
}