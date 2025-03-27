using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace InitializationFramework
{
    public class InitializationController : MonoBehaviour , IProgressable , IInitializable
    {
        [SerializeField]
        bool initOnEnable = true;


        [SerializeField]
        GameObject[] initOrder;

        [SerializeField]
        GameObject[] deinitOrder;


        [SerializeField]
        // флаг состояния процесса иниц/деиниц
        bool isInProgress = false;


        private void OnEnable()
        {

            

            //System.Action<Object> postAction = (Object parm) => { print("Done! In: " + gameObject.name);  };
            // если иниц. нужно начать при включении объекта
            if (initOnEnable )
            {
                ExplicitInitialize(null);
            }

        }


        public void ExplicitInitialize(System.Action<Object> postAction)
        {

            StartCoroutine(Initialize(postAction));
        }

        public void ExplicitDeinitialize(System.Action<Object> postAction)
        {
  
            StartCoroutine(Deinitialize(postAction));
        }


        #region IInitializable 

        public IEnumerator Initialize ( System.Action<Object> postAction )
        {

            if (!isInProgress)
            {
                isInProgress = true;

                foreach (GameObject obj in initOrder)
                {
                    
                    IInitializable initComponent = obj.GetComponent<IInitializable>();

                    if (initComponent != null )
                    {
                        yield return StartCoroutine(initComponent.Initialize());
                    }

                    else
                    {
                        Debug.LogWarning("Initializable interface is missing on object: " + gameObject.name );

                        yield return null;
                    }

                }



                isInProgress = false;

                postAction?.Invoke(null);

            }

        }

        public IEnumerator Deinitialize(System.Action<Object> postAction)
        {

            print("Is deiniting : " + isInProgress );

            if (!isInProgress)
            {
                isInProgress = true;

                foreach (GameObject obj in deinitOrder)
                {

                    IInitializable deinitComponent = obj.GetComponent<IInitializable>();

                    //print("Deinit: " + obj.name );

                    if (deinitComponent != null)
                    {
                        yield return StartCoroutine(deinitComponent.Deinitialize());
                    }

                    else
                    {
                        Debug.LogWarning("Initializable interface is missing on object: " + gameObject.name);
                        yield return null;
                    }
                }

                isInProgress = false;

                gameObject.SetActive(false);

                postAction?.Invoke(null);
            }

        }



        #endregion

        #region IProgressable

        public bool IsInProgress()
        {

            return isInProgress;
        }


        #endregion

        private void Reset()
        {
            




        }




    }

}