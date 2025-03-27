using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace InitializationFramework
{
    public class ActivationInitialization : MonoBehaviour, IInitializable 
    {
        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {

            gameObject.SetActive( true );
            Debug.Log("Включение:" + gameObject);
            yield return null;


        }

        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {
            yield return null;
            Debug.Log("Выключение:" + gameObject);
            gameObject.SetActive(false);


        }



        private void Reset()
        {

            gameObject.SetActive(false);

        }

    }

}