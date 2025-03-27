using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
//using static GlobalSettings.SACNSettings;


namespace InitializationFramework
{

    public class ComPoertResetter : MonoBehaviour
    {
        [SerializeField]
        bool debug_add_value = true;

        [SerializeField]
        int maxFreezeFramesAllowed = 3;

        [SerializeField]
        int updateFreq_ms = 50;

        [SerializeField]
        ResetCommunication rs;

        WaitForSeconds readDelay;

        int freezeReadsCount = 0;

        UnitySerialPort serial;

        int lastRecievedVal = 0;

        int debugTickValue = 0;

        int debugResets = 0;

        private void OnEnable()
        {
            readDelay = new WaitForSeconds( 1.0f / (float)updateFreq_ms );

            serial = UnitySerialPort.Instance;

            freezeReadsCount = 0;

            if (serial != null)
            {

                //StartCoroutine(DebugIncreaseValue());

                StartCoroutine(ListenForUARTTicks());

                //StartCoroutine(DebugResetFlag());

            }
            else
            {
                
                print("Невозможно получить инстанс UART контроллера");

            }
            


        }

        IEnumerator DebugIncreaseValue()
        {
            while (debug_add_value)
            {
                debugTickValue = ++debugTickValue % 255;

                yield return readDelay;
            }

        
        }


        IEnumerator DebugResetFlag()
        {
                yield return new WaitForSeconds( 30.0f );

            print("RESETS: " + ++debugResets );

            debug_add_value = false;

            StartCoroutine(DebugResetFlag());


        }



        IEnumerator ListenForUARTTicks()
        {
            freezeReadsCount = 0;

            while (freezeReadsCount <= maxFreezeFramesAllowed )
            {
                try
                {
                    int tickValue = Convert.ToInt32(serial.RawData.Split('\t')[1]);

                    if (tickValue == lastRecievedVal)
                    { ++freezeReadsCount; }
                    else
                    { freezeReadsCount = 0; }


                    lastRecievedVal = tickValue;
                }

                catch{ }

                yield return readDelay;

            }

            {
                print("RESTART");

                
                serial?.CloseSerialPort();

                yield return new WaitForSeconds(0.125f);

                serial?.OpenSerialPort();
                /**/
            }



            yield return readDelay;

            debug_add_value = true;

            freezeReadsCount = 0;

            StartCoroutine(ListenForUARTTicks());
        }

        private void OnDestroy()
        {

            StopAllCoroutines();

        }



    }

}