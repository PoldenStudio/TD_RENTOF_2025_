using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DemolitionStudios.DemolitionMedia;
using InitializationFramework;

namespace InitializationFramework
{
    public class InitializePlayers : MonoBehaviour, IInitializable
    {
        [SerializeField]
        GlobalSettings gs;

        [SerializeField]
        List<Media> players;

        private string _idleModeMovieName;
        private string _defaultModeMovieName;
        //старое видео при начале корутины дожно останавливаться, а новое должно начинаться с нуля
        private bool _isIdleMode = true;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive(true);

            _idleModeMovieName = gs.contentSettings.idleModeMovieName;
            _defaultModeMovieName = gs.contentSettings.defaultModeMovieName;

            string initialMoviePath = _idleModeMovieName;

            Debug.Log($"[InitializePlayers] Initializing with idle movie: {initialMoviePath}");

            foreach (var player in players)
            {
                bool dataIsReady = false;

                player.openOnStart = true;
                player.playOnOpen = true;
                player.Loops = -1;

                void Handler(Media mediaPlayer, MediaEvent.Type eventType, MediaError errorCode)
                {
                    if (eventType == MediaEvent.Type.Opened)
                    {
                        dataIsReady = true;
                    }
                }

                player.Events.AddListener(Handler);

                Debug.Log($"[InitializePlayers] Opening initial movie for player: {player.name}");
                player.Open(initialMoviePath);

                while (!dataIsReady)
                {
                    yield return null;
                }

                player.Events.RemoveListener(Handler);
                player.Play();
                Debug.Log($"[InitializePlayers] Started playback for player: {player.name}");
                yield return null;
            }

            _isIdleMode = true;
            Debug.Log("[InitializePlayers] Initialization complete");
            OnFinished?.Invoke(this);
        }

        public IEnumerator SwitchToIdleMode()
        {
            if (_isIdleMode)
            {
                Debug.Log("[InitializePlayers] Already in idle mode, skipping switch");
                yield break;
            }

            Debug.Log("[InitializePlayers] Switching to idle mode video");

            foreach (var player in players)
            {
                bool dataIsReady = false;

                void Handler(Media mediaPlayer, MediaEvent.Type eventType, MediaError errorCode)
                {
                    if (eventType == MediaEvent.Type.Opened)
                    {
                        dataIsReady = true;
                    }
                }

                player.Events.AddListener(Handler);

                Debug.Log($"[InitializePlayers] Closing current video for player: {player.name}");
                //player.PlaybackSpeed = 0f;
                player.Close();

                Debug.Log($"[InitializePlayers] Opening idle video for player: {player.name}");
                player.Open(_idleModeMovieName);

                while (!dataIsReady)
                {
                    yield return null;
                }

                player.Events.RemoveListener(Handler);
                //player.PlaybackSpeed = 1f;
                //player.StartTime = 0f;
                //player.SeekToTime(0f);
                //player.SeekToFrame(0);
                player.Play();
                Debug.Log($"[InitializePlayers] Started idle video playback for player: {player.name}");
            }

            _isIdleMode = true;
            Debug.Log("[InitializePlayers] Switch to idle mode complete");
            yield return null;
        }

        public IEnumerator SwitchToDefaultMode()
        {
            if (!_isIdleMode)
            {
                Debug.Log("[InitializePlayers] Already in default mode, skipping switch");
                yield break;
            }

            Debug.Log("[InitializePlayers] Switching to default mode video");

            _isIdleMode = false;

            foreach (var player in players)
            {
                bool dataIsReady = false;

                void Handler(Media mediaPlayer, MediaEvent.Type eventType, MediaError errorCode)
                {
                    if (eventType == MediaEvent.Type.Opened)
                    {
                        dataIsReady = true;
                    }
                }

                player.Events.AddListener(Handler);

                Debug.Log($"[InitializePlayers] Closing current video for player: {player.name}");
                //player.PlaybackSpeed = 0f;
                player.Close();

                Debug.Log($"[InitializePlayers] Opening default video for player: {player.name}");
                player.Open(_defaultModeMovieName);

                while (!dataIsReady)
                {
                    yield return null;
                }

                player.Events.RemoveListener(Handler);
                //player.PlaybackSpeed = 1f;
                //player.StartTime = 0f;
                //player.SeekToTime(0f);
                //player.SeekToFrame(0);
                player.Play();
                Debug.Log($"[InitializePlayers] Started default video playback for player: {player.name}");
            }

            Debug.Log("[InitializePlayers] Switch to default mode complete");
            yield return null;
        }

        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {
            Debug.Log("[InitializePlayers] Deinitializing players");

            foreach (var player in players)
            {
                player.Close();
                Debug.Log($"[InitializePlayers] Closed player: {player.name}");
            }

            yield return null;
            gameObject.SetActive(false);
            Debug.Log("[InitializePlayers] Deinitialization complete");
            OnFinished?.Invoke(this);
        }

        private void Reset()
        {
            gameObject.SetActive(false);
        }
    }
}