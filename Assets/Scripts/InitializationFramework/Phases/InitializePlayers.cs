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
        List<Media> players;

        [SerializeField]
        Media cometPlayer; // Separate player for comet video

        [SerializeField]
        Media curtainPlayer; // Separate player for curtain video

        private string _idleModeMovieName;
        private string _defaultModeMovieName;
        private string _CurtainMovieName;
        private string _CometMovieName;

        private bool _isIdleMode = true;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive(true);

            _idleModeMovieName = Settings.Instance.idleModeMovieName;
            _defaultModeMovieName = Settings.Instance.defaultModeMovieName;
            _CurtainMovieName = Settings.Instance.CurtainMovieName;
            _CometMovieName = Settings.Instance.CometMovieName;

            string initialMoviePath = _idleModeMovieName;

            Debug.Log($"[InitializePlayers] Initializing with idle movie: {initialMoviePath}");

            // Initialize main players
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
                player.SeekToTime(0);
                Debug.Log($"[InitializePlayers] Started playback for player: {player.name}");
                yield return null;
            }

            // Initialize curtain player
            if (curtainPlayer != null)
            {
                bool curtainDataReady = false;

                curtainPlayer.openOnStart = true;
                curtainPlayer.playOnOpen = true;
                curtainPlayer.Loops = -1;

                void CurtainHandler(Media mediaPlayer, MediaEvent.Type eventType, MediaError errorCode)
                {
                    if (eventType == MediaEvent.Type.Opened)
                    {
                        curtainDataReady = true;
                    }
                }

                curtainPlayer.Events.AddListener(CurtainHandler);
                Debug.Log($"[InitializePlayers] Opening curtain movie: {_CurtainMovieName}");
                curtainPlayer.Open(_CurtainMovieName);

                while (!curtainDataReady)
                {
                    yield return null;
                }

                curtainPlayer.Events.RemoveListener(CurtainHandler);
                //curtainPlayer.Pause(); // Don't play it yet
                Debug.Log($"[InitializePlayers] Curtain video ready but paused");
            }

            // Initialize comet player but don't open/play it yet
            if (cometPlayer != null)
            {
                cometPlayer.openOnStart = false;
                cometPlayer.playOnOpen = false;
                cometPlayer.Loops = 0; // Play only once
                Debug.Log($"[InitializePlayers] Comet player initialized but not opened yet");
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
                player.Close();

                Debug.Log($"[InitializePlayers] Opening idle video for player: {player.name}");
                player.Open(_idleModeMovieName);

                while (!dataIsReady)
                {
                    yield return null;
                }

                player.Events.RemoveListener(Handler);
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
                player.Close();

                Debug.Log($"[InitializePlayers] Opening default video for player: {player.name}");
                player.Open(_defaultModeMovieName);

                while (!dataIsReady)
                {
                    yield return null;
                }

                player.Events.RemoveListener(Handler);
                player.Play();
                Debug.Log($"[InitializePlayers] Started default video playback for player: {player.name}");
            }

            Debug.Log("[InitializePlayers] Switch to default mode complete");
            yield return null;
        }

        public IEnumerator PlayCometVideo()
        {
            if (cometPlayer == null)
            {
                Debug.LogError("[InitializePlayers] Comet player is not assigned");
                yield break;
            }

            Debug.Log("[InitializePlayers] Starting comet video sequence");

            bool dataIsReady = false;
            bool playbackFinished = false;

            void CometOpenHandler(Media mediaPlayer, MediaEvent.Type eventType, MediaError errorCode)
            {
                if (eventType == MediaEvent.Type.Opened)
                {
                    dataIsReady = true;
                }
                else if (eventType == MediaEvent.Type.PlaybackNewLoop)
                {
                    playbackFinished = true;
                }
            }

            cometPlayer.Events.AddListener(CometOpenHandler);

            // Close if already open
            if (cometPlayer.IsOpened)
            {
                cometPlayer.Close();
            }

            // Open comet video
            Debug.Log($"[InitializePlayers] Opening comet video: {_CometMovieName}");
            cometPlayer.Open(_CometMovieName);

            while (!dataIsReady)
            {
                yield return null;
            }

            // Play comet video once
            cometPlayer.Loops = 0; // Ensure it plays only once
            cometPlayer.Play();
            Debug.Log("[InitializePlayers] Started comet video playback");

            // Wait for playback to finish
            while (!playbackFinished)
            {
                yield return null;
            }

            cometPlayer.Events.RemoveListener(CometOpenHandler);
            cometPlayer.Close();
            Debug.Log("[InitializePlayers] Comet video playback completed and closed");

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

            if (curtainPlayer != null)
            {
                curtainPlayer.Close();
                Debug.Log("[InitializePlayers] Closed curtain player");
            }

            if (cometPlayer != null)
            {
                cometPlayer.Close();
                Debug.Log("[InitializePlayers] Closed comet player");
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