using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;
using static RenderHeads.Media.AVProVideo.MediaPlayerEvent;

namespace InitializationFramework
{
    public class ResetCommunicationData : MonoBehaviour, IInitializable 
    {
        [SerializeField]
        GlobalSettings gs;

        [SerializeField]
        bool resetOnInit = true;

        [SerializeField]
        bool resetOnDeinit = true;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {

            gameObject.SetActive( true );

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