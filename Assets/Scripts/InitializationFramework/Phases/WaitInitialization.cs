using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace InitializationFramework
{
    public class WaitInitialization : MonoBehaviour, IInitializable 
    {
        [SerializeField]
        float idleTime = 1.0f;

        [SerializeField]
        bool byPassOnDeinit = true;

        [SerializeField]
        bool byPassOnInit = false;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {

            gameObject.SetActive( true );

            yield return (byPassOnInit) ? null : new WaitForSeconds(idleTime);

        }

        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {

            gameObject.SetActive(false);

            yield  return ( byPassOnDeinit ) ? null : new WaitForSeconds(idleTime);


        }


        private void Reset()
        {

            gameObject.SetActive(false);

        }


    }

}