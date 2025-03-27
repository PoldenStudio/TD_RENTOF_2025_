using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

namespace InitializationFramework
{
    public class FadeSound : MonoBehaviour, IInitializable 
    {

        [SerializeField]
        float fromVolume;

        [SerializeField]
        float toVolume;

        [SerializeField]
        float animTime = 1.0f;

        [SerializeField]
        AnimationCurve anmCurve;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {

            gameObject.SetActive( true );

            
            for( float fc = 0f; fc < 1.0f; fc += Time.deltaTime / animTime )
            {
                float nV = Mathf.Lerp( fromVolume, toVolume , anmCurve.Evaluate(fc) );

                AudioListener.volume = nV;

                yield return null;

            }

            AudioListener.volume = toVolume;

        }


        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {


            for (float fc = 0f; fc < 1.0f; fc += Time.deltaTime / animTime)
            {
                float nV = Mathf.Lerp(toVolume, fromVolume, anmCurve.Evaluate(fc));


                AudioListener.volume = nV;

                yield return null;

            }

            AudioListener.volume = fromVolume;

            gameObject.SetActive(false);

        }



        private void Reset()
        {

            gameObject.SetActive(false);

        }

    }

}