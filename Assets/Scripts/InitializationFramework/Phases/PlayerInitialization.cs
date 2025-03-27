using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

namespace InitializationFramework
{
    public class PlayerInitialization : MonoBehaviour, IInitializable 
    {
        public System.Action<VideoEntry.EntryRoles> OnEntryFinished;


        [System.Serializable]
        public class VideoEntry
        {
            public enum EntryRoles
            {
                None = 0,

                IDLE_1 ,

                ACTIVATION_1 ,

                DEACTIVATION_1,


                IDLE_2 ,

                ACTIVATION_2,

                DEACTIVATION_2,


                IDLE_3,

                ACTIVATION_3,

                DEACTIVATION_3,

            }


            public AnimationCurve movementCurve;

            public EntryRoles role;
            public MediaPlayer player;
            public DisplayIMGUI playerRender;

            public string path = string.Empty;
            public bool isLooped = false;
        }
        
        public List<VideoEntry> entries;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {

            gameObject.SetActive( true );

            

            foreach ( var entry in entries)
            {

                UnityEngine.Events.UnityAction<MediaPlayer, MediaPlayerEvent.EventType, ErrorCode> evH = (mp, et, ec) =>
                {
                    OnEntryFinished?.Invoke(entry.role);
                };

                entry.player.Events.AddListener(evH);



                MediaPath path = new MediaPath(entry.path, RenderHeads.Media.AVProVideo.MediaPathType.RelativeToStreamingAssetsFolder);


                entry.player.AutoOpen = false;

                entry.player.AutoStart = false;

                entry.player.Loop = entry.isLooped;

                entry.player.OpenMedia(path,false);

                while (!entry.player.MediaOpened && !entry.player.Control.CanPlay() )
                {
                    yield return null;

                }

            }

            yield return null;


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