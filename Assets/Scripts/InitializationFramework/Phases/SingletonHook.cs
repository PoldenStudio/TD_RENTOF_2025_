using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

namespace InitializationFramework
{
    public class SingletonHook : MonoBehaviour, IInitializable 
    {

    

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive( true );

            InitializationControllerSingleton sngl = InitializationControllerSingleton.Instance;

            if (sngl.IsInitialized)
            {
                yield return null;
            }
            else
            {

                yield return StartCoroutine(sngl.Initialize(OnFinished));

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