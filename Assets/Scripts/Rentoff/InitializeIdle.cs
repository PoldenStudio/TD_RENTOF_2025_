using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DemolitionStudios.DemolitionMedia;

namespace InitializationFramework
{
    public class InitializeIdle : MonoBehaviour, IInitializable
    {
        [SerializeField]
        GlobalSettings gs;

        [SerializeField]
        List<Media> players;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive(true);

            foreach (var player in players)
            {
                string movPath = (gs.generalSettings.activeMode == GlobalSettings.GeneralSettings.WorkingModes.DefaultMode)
                    ? gs.contentSettings.idleModeMovieName
                    : gs.contentSettings.spcModeMovieName;

                bool dataIsReady = false;

                player.openOnStart = true;
                player.playOnOpen = true;
                player.Loops = -1;

                // Добавление обработчика событий открытия
                void Handler(Media mediaPlayer, MediaEvent.Type eventType, MediaError errorCode)
                {
                    if (eventType == MediaEvent.Type.Opened)
                    {
                        dataIsReady = true;
                    }
                }

                player.Events.AddListener(Handler);


                player.Open(movPath);

                while (!dataIsReady)
                {
                    yield return null;
                }

                player.Events.RemoveListener(Handler);

                player.Play();

                yield return null;
            }

            OnFinished?.Invoke(this);
        }

        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {
            foreach (var player in players)
            {
                player.Close();
            }

            yield return null;
            gameObject.SetActive(false);
            OnFinished?.Invoke(this);
        }

        private void Reset()
        {
            gameObject.SetActive(false);
        }
    }
}