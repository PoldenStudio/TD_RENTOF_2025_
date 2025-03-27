
using UnityEngine;
using System.Collections;

public class PitchTester : MonoBehaviour
{
    [SerializeField]
    new AudioSource audio;

    private float scrollPos = 0f;

    private void Start()
    {
        audio.Play();
    }

    private void OnGUI()
    {
        scrollPos = GUI.HorizontalSlider(new Rect(0f, 50f, Screen.width, 50f), scrollPos, 0, audio.clip.length);
        if (GUI.changed == true)
        {
            audio.time = scrollPos;
        }

        GUI.Label(new Rect(10f, 80f, 100f, 30f), (audio.time).ToString());
    }
}