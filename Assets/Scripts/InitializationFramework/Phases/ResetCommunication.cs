using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GlobalSettings.SACNSettings;


namespace InitializationFramework
{

    public class ResetCommunication : MonoBehaviour, IInitializable
    {

        [SerializeField]
        TimeSelector tm;

        [SerializeField]
        InitializationController parentCntrl;

        [SerializeField]
        GlobalSettings gs;

        NetworkController nc;

        //sACNController sAcn;

        GenericDMXFixture dmx;

        UnitySerialPort sp;

        public IEnumerator Initialize(System.Action<UnityEngine.Object> OnFinished)
        {
            gameObject.SetActive(true);

            nc = (nc == null) ? NetworkController.Instance : nc;

            if (nc != null)
            {
                nc.onSwitchCommandRecieved += HandleModesSwitch;
            }
            else
            { throw new Exception("Невозможон инициализировать NetworkController"); }

            //sAcn = (sAcn == null) ? sACNController.Instance : sAcn; // Удаляем

            dmx = (dmx == null) ? GenericDMXFixture.Instance : dmx;

            yield return null;
        }

        private void HandleModesSwitch(GlobalSettings.GeneralSettings.WorkingModes mode)
        {
            if (gs.generalSettings.activeMode != mode)
            {
                gs.generalSettings.activeMode = mode;

                nc.onSwitchCommandRecieved -= HandleModesSwitch;

                parentCntrl.ExplicitDeinitialize(null);

            }

        }

        public void ExplicitRestart()
        {
            nc.onSwitchCommandRecieved -= HandleModesSwitch;

            parentCntrl.ExplicitDeinitialize(null);

        }


        public IEnumerator Deinitialize(System.Action<UnityEngine.Object> OnFinished)
        {

            tm.ResetValuesAndStopRecieving();

            yield return null;

            //sAcn.ResetSACNData();
            //yield return new WaitForSeconds(1.0f);

            nc.ResetCommunication();

            dmx.ResetDmxData();

            yield return null;

        }

        private void Reset()
        {

            gameObject.SetActive(false);

        }

    }

}