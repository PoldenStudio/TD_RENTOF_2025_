using DemolitionStudios.DemolitionMedia;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class SearchTest : MonoBehaviour
{
    [SerializeField]
    GlobalSettings gs; // �������� ������ �� GlobalSettings

    [Serializable]
    class SoundEvent
    {
        public bool isPlayed = false;
        public float activationTime;
        public AudioClip clip;

        public void Reset()
        {
            isPlayed = false;
        }
    }

    [SerializeField]
    List<SoundEvent> newEvents;

    [SerializeField]
    int sampliongFreq = 60;

    [SerializeField]
    AudioSource src;

    [SerializeField]
    AnimationClip clp;

    [SerializeField]
    AnimationCurve debCurve;

    [SerializeField]
    Media player;

    WaitForSeconds wc;

    SoundEvent activeEvent = null;

    private void Start()
    {
        wc = new WaitForSeconds(1f / (float)sampliongFreq);

        // ���������� ����������� ������� �������� �����
        void MediaHandler(Media mediaPlayer, MediaEvent.Type eventType, MediaError errorCode)
        {
            if (eventType == MediaEvent.Type.Opened)
            {
                StartCoroutine(ActivateScearySound());
            }
        }
        player.Events.AddListener(MediaHandler);

        // �������� �����
        player.Open(GetMediaPath());
    }

    private string GetMediaPath()
    {
        return Path.Combine(
            Application.streamingAssetsPath,
            (gs.generalSettings.activeMode == GlobalSettings.GeneralSettings.WorkingModes.DefaultMode)
                ? gs.contentSettings.defaultModeMovieName
                : gs.contentSettings.spcModeMovieName
        );
    }

    IEnumerator ActivateScearySound()
    {
        while (true)
        {
            // ��������� �������� ������� � ������������
            float currentMediaTime = player.CurrentTime / player.DurationSeconds;

            int scenaryIndex = (int)debCurve.Evaluate(currentMediaTime);

            if (scenaryIndex != -1 && scenaryIndex < newEvents.Count)
            {
                var newEvent = newEvents[scenaryIndex];

                if (activeEvent != newEvent)
                {
                    activeEvent?.Reset();

                    activeEvent = newEvent;

                    src.clip = activeEvent.clip;

                    src.Play();

                    activeEvent.isPlayed = true;
                }
            }
            else
            {
                activeEvent?.Reset();
                activeEvent = null;
            }

            yield return wc;
        }
    }
}