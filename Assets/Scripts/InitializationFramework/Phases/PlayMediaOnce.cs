using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

namespace InitializationFramework
{
    public class PlayMediaOnce : MonoBehaviour, IInitializable 
    {
        [SerializeField]
        DisplayIMGUI display;

        [SerializeField]
        MediaPlayer pl;

        bool isPlayed = false;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive( true );
            // если медиа ни разу не проигрывалось, играем
            if (!isPlayed)
            {
                bool playbackIsInProgress = false;

                void Handler(MediaPlayer mp, MediaPlayerEvent.EventType eventType, ErrorCode code)
                {
                    playbackIsInProgress = false;

                    isPlayed = true;
                }

                pl.Events.AddListener(Handler);

                display.Player = pl;

                pl.Play();
                // ждем пока плеер проиграет контент
                while (playbackIsInProgress)
                {
                    yield return null;
                }

            }
            // иначе - ничего не делаем
            else
            {
                yield return null;
            }

        }

        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {
            yield return null;

            gameObject.SetActive(false);


        }

    


        private void Reset()
        {

            gameObject.SetActive(false);

        }

    }

}