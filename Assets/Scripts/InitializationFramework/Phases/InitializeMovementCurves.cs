using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

namespace InitializationFramework
{
    public class InitializeMovementCurves : MonoBehaviour, IInitializable 
    {
        [SerializeField]
        List<MediaPlayer> players;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {

            gameObject.SetActive( true );

            
            foreach ( var player in players)
            {

                //MediaPath path = new MediaPath(entry.path, RenderHeads.Media.AVProVideo.MediaPathType.RelativeToStreamingAssetsFolder);


                player.AutoOpen = false;

                player.AutoStart = false;

                //player.Loop = entry.isLooped;

                player.OpenMedia(false);

                while (!player.MediaOpened && !player.Control.CanPlay() )
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