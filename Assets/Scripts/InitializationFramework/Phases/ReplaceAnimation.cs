using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

namespace InitializationFramework
{
    public class ReplaceAnimation : MonoBehaviour, IInitializable 
    {

        [SerializeField]
        DisplayUGUI display;

        [SerializeField]
        MediaPlayer playerToStart;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive( true );

            display.Player = playerToStart;

            playerToStart.Play();

        yield return null;

        }

        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive(false);

            playerToStart.Stop();

            yield return null;

        }




        private void Reset()
        {

            gameObject.SetActive(false);

        }

    }

}