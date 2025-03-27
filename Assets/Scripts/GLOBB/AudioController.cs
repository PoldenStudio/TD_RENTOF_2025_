using UnityEngine.Audio;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioMixer audioMixer;

    public void SetAudioPosition(float position)
    {
        audioSource.time = position;
    }

    public void SetPlaybackSpeed(float speed)
    {
        audioSource.pitch = speed;
        audioMixer.SetFloat("PitchBend", 1f / speed);
    }
}