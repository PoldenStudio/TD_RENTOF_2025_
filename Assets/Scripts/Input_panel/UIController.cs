using UnityEngine;
using UnityEngine.UI;
using DemolitionStudios.DemolitionMedia;

public class UIController : MonoBehaviour
{
    public MediaController mediaController;
    public Media demolitionMedia;
    public InputField mediaURLInput;
    public Button openMediaButton;
    public Slider playbackSpeedSlider;
    public Text playbackSpeedText;

    void Start()
    {
        if (mediaController == null)
        {
            Debug.LogError("UIController: MediaController not assigned!");
        }

        if (demolitionMedia == null)
        {
            Debug.LogError("UIController: DemolitionMedia not assigned!");
        }

        if (mediaURLInput != null)
        {
            mediaURLInput.onEndEdit.AddListener(OnMediaURLChanged);
        }

        if (openMediaButton != null)
        {
            openMediaButton.onClick.AddListener(OpenMedia);
        }

        if (playbackSpeedSlider != null)
        {
            playbackSpeedSlider.onValueChanged.AddListener(OnPlaybackSpeedChanged);
        }

        UpdatePlaybackSpeedUI();
    }

    public void OnMediaURLChanged(string url)
    {
        Debug.Log("UIController: Media URL changed to " + url);
        // Здесь можно сохранить URL для MediaController или непосредственно открыть медиа.
    }

    public void OpenMedia()
    {
        Debug.Log("UIController: Open Media button clicked");
        // Здесь вызов метода в MediaController для открытия медиа.
    }

    public void OnPlaybackSpeedChanged(float speed)
    {
        if (mediaController != null && demolitionMedia != null)
        {
            demolitionMedia.PlaybackSpeed = speed;
            UpdatePlaybackSpeedUI();
            Debug.Log("UIController: Playback speed changed to " + speed);
        }
    }

    void UpdatePlaybackSpeedUI()
    {
        if (demolitionMedia != null && playbackSpeedSlider != null && playbackSpeedText != null)
        {
            playbackSpeedSlider.value = demolitionMedia.PlaybackSpeed;
            playbackSpeedText.text = "Speed: " + demolitionMedia.PlaybackSpeed.ToString("F2");
        }
    }

    void OnDestroy()
    {
        if (mediaURLInput != null)
            mediaURLInput.onEndEdit.RemoveListener(OnMediaURLChanged);
        if (openMediaButton != null)
            openMediaButton.onClick.RemoveListener(OpenMedia);
        if (playbackSpeedSlider != null)
            playbackSpeedSlider.onValueChanged.RemoveListener(OnPlaybackSpeedChanged);
    }
}