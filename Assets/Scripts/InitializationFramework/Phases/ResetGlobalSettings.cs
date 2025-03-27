using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RenderHeads.Media.AVProVideo;

namespace InitializationFramework
{
    public class ResetGlobalSettings : MonoBehaviour, IInitializable 
    {
        [SerializeField]
        GlobalSettings gs;

    

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive( true );

            gs.ResetData();

           
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