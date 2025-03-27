using System;
using System.Collections;


using UnityEngine;



namespace InitializationFramework
{
    public class IntroPhaseIinitialization : MonoBehaviour, IInitializable 
    {
        [SerializeField]
        GlobalSettings gs;

        [SerializeField]
        GameObject defaultModeIntroPlate;

        [SerializeField]
        UnityEngine.UI.Image defaultModeProgressBar;


        [SerializeField]
        UnityEngine.UI.Image overlayPlate;

        [SerializeField]
        float fadeTime = 0.5f;

        [SerializeField]
        CanvasGroup rootCanvasGroup;


        [SerializeField]
        UnityEngine.UI.Image defModeClipPlate;


        [SerializeField]
        GameObject spcModeIntroPlate;
        [SerializeField]
        UnityEngine.UI.Image spcModeClipPlate;



        GameObject activePlate = null;
        UnityEngine.UI.Image activeUIPlate = null;


       UnitySerialPort serial;

        [SerializeField]
        private float multiplier = 1;



        int ticksPerSecond = 2000;

        int? baseSerialVal;

        bool introSceneIsPlayed = false;

        bool introModeIsActive = false;

        int delta = 0;

        int lastRawData = 0;


        WaitForFixedUpdate updateDelay;

        public IEnumerator Initialize(System.Action<UnityEngine.Object> OnFinished)
        {

            gameObject.SetActive( true );


            if (!introSceneIsPlayed)
            {

                serial = (serial == null) ? UnitySerialPort.Instance : serial;

                //activePlate = (gs.generalSettings.activeMode == GlobalSettings.GeneralSettings.WorkingModes.DefaultMode) ? defaultModeIntroPlate : spcModeIntroPlate;

                activeUIPlate = defModeClipPlate;

                updateDelay = (updateDelay == null) ? new WaitForFixedUpdate() : updateDelay;

                introModeIsActive = true;

                yield return StartCoroutine(WaitForInitToFinish());


            }

            else { print("INTRO PLAYED "); }


            yield return null;


        }



        IEnumerator WaitForInitToFinish() 
        {


            while (introModeIsActive)
            {



                baseSerialVal = (baseSerialVal == null) ? Convert.ToInt32(serial.RawData.Split('\t')[0]) : baseSerialVal;

                int rawData = (baseSerialVal == null ) ? baseSerialVal.Value : (Convert.ToInt32(serial.RawData.Split('\t')[0]));

                delta = (baseSerialVal != null) ? (rawData - baseSerialVal.Value) : 0;

                if ( delta < 0 )
                {
                    baseSerialVal = null;

                    delta = 0;

                }

                var d_ticksPerSecond = (int)(ticksPerSecond * multiplier);

                double tm = delta / (double)d_ticksPerSecond;

                float targetTimeToWait = 5.0f;

                //tm = Mathf.Clamp((float)tm, 0.0f, targetTimeToWait);

                //print("delta: " + delta);

                float fillingParm = Mathf.Clamp01( (float)(tm / targetTimeToWait) ) ;

                defaultModeProgressBar.fillAmount = Mathf.Lerp(defaultModeProgressBar.fillAmount, fillingParm , 0.5f ) ;


                if (fillingParm == 1.0f )
                {
                    introModeIsActive = !introModeIsActive;
                }

                lastRawData = rawData;

                yield return updateDelay;
            }

            {

                float initAlpha = rootCanvasGroup.alpha;

                float tgAlpha = 0f;


                for (float fc = 0; fc <= 1.0f; fc += Time.deltaTime / fadeTime)
                {
                    float nAlpha = Mathf.Lerp(initAlpha, tgAlpha, fc);

                    rootCanvasGroup.alpha = nAlpha;

                    yield return null;
                }

                rootCanvasGroup.alpha = tgAlpha;


            }

            introSceneIsPlayed = true;

            print("DONE INIT");

            yield return null;
        }


        /*
        private void FixedUpdate()
        {
            if (introModeIsActive)
            {

                baseSerialVal = (baseSerialVal == null) ? Convert.ToInt32(serial.RawData.Split('\t')[0]) : baseSerialVal;

                int rawData = Convert.ToInt32(serial.RawData.Split('\t')[0]);

                delta = (baseSerialVal != null) ? (rawData - baseSerialVal.Value) : 0;

                var d_ticksPerSecond = (int)(ticksPerSecond * multiplier);

                double tm = delta / (double)d_ticksPerSecond;


            }

        }
        */

        public IEnumerator Deinitialize(System.Action<UnityEngine.Object> OnFinished)
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