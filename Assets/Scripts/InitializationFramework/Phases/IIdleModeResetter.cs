using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace InitializationFramework
{

    public class IdleModeResetter : MonoBehaviour
    {

        public float lastActivityTime = 0;

        [SerializeField]
        InitializationController parentCntrl;


        [SerializeField]
        float resetTime = 10.0f;

        bool countDownIsStarted = false;

        FixedUpdate updateDelay;

        bool countDownFinished = false;


        private void Start()
        {

            updateDelay = new FixedUpdate();

            StartCoroutine(CalculateTimeBeforeIdleMode());
        }


        IEnumerator CalculateTimeBeforeIdleMode()
        {
            while (!countDownFinished)
            {
                float tm = Mathf.Clamp01((Time.time - lastActivityTime) / resetTime);

                countDownFinished = (tm == 1.0f) ? true : false;

                yield return updateDelay;
            }

            parentCntrl.ExplicitDeinitialize(null);
        }




    }

}