using LEDControl;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Hibernate : MonoBehaviour
{
    [SerializeField] private LEDController ledController;
    [SerializeField] private DataSender dataSender;
    [SerializeField] private VideoPlaybackController videoPlaybackController;
    [SerializeField] private SoundManager soundManager;
    [SerializeField] private Image hibernateImage;

    private bool isHibernated = false;
    private float pausedTime = 0f;
    private float accumulatedTime = 0f;

    public bool IsHibernated => isHibernated;

    public void SetHibernate(bool hibernate)
    {
        if (hibernate && !isHibernated)
        {
            ledController.TurnOffAllLEDs();
            pausedTime = Time.time;
            isHibernated = true;
            ledController.enabled = false;

            if (videoPlaybackController != null) videoPlaybackController.PauseVideo();
            if (soundManager != null) soundManager.PauseSound();
            ClearAllPorts();

            StartFadeImage(true);
        }
        else if (!hibernate && isHibernated)
        {
            isHibernated = false;
            ledController.enabled = true;

            accumulatedTime = Time.time - pausedTime;
            ledController.kineticStartTime += accumulatedTime;

            if (videoPlaybackController != null) videoPlaybackController.ResumeVideo();
            if (soundManager != null) soundManager.ResumeSound();

            StartFadeImage(false);
        }
    }

    private void StartFadeImage(bool fadeIn)
    {
        if (hibernateImage == null) return;

        StopAllCoroutines();
        StartCoroutine(FadeImageCoroutine(fadeIn));
    }

    private IEnumerator FadeImageCoroutine(bool fadeIn)
    {
        float duration = 1f;
        float timer = 0f;
        Color startColor = hibernateImage.color;
        Color endColor = startColor;
        endColor.a = fadeIn ? 1f : 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            hibernateImage.color = Color.Lerp(startColor, endColor, timer / duration);
            yield return null;
        }

        hibernateImage.color = endColor;
    }

    private void ClearAllPorts()
    {
        for (int i = 0; i < dataSender.portConfigs.Count; i++)
        {
            if (dataSender.IsPortOpen(i))
            {
                for (int j = 0; j < 4; j++)
                {
                    string clearCommand = $"{j}:clear\r\n";
                    dataSender.SendString(i, clearCommand);
                }
            }
        }
    }
}