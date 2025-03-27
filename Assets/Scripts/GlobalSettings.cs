using System.Collections;
using System.Collections.Generic;
using System.Security.Policy;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using static GlobalSettings;

[CreateAssetMenu(fileName ="GlobalSettings",menuName ="Malva/Create GlobalSettings",order = 0 )]
public class GlobalSettings : ScriptableObject
{

    [System.Serializable]
    public class ContentSettings
    {
        public string defaultModeMovieName = "";

        public string idleModeMovieName = "";

        public string spcModeMovieName = "";


        public string defaultModeChan = "";

        public string spcModeChan = "";


        public AudioClip defaulModeOST;

        public AudioClip spcModeOST;

    }

    public ContentSettings contentSettings;


    [System.Serializable]
    public class NetworkSettings
    {
        public int soundPort = 7027;

        // порт на который будут присылаться комманды
        public int commandsRecievePort = -1;
        // кол-во байт в комманде
        public int commandPacketSize = 2;

        public void Reset()
        {
            commandsRecievePort = -1;

            commandPacketSize = 2;
        }
    }


    public NetworkSettings networkSettings;


    [System.Serializable]
    public class GeneralSettings
    {

        public enum WorkingModes : byte
        {
            // стандартный режим
            DefaultMode = 0,
            // спец показ
            SPCMode = 1,

        }

        public WorkingModes activeMode = WorkingModes.DefaultMode;


        public float lastSetVolumeLevel = 5.0f;

        public string installationName = "MALVA";

        public int targetFPS = 50;

        public float inputScaleFactor = 1.0f;

        public float threshold = 0.125f;

        public void Reset()
        {
            targetFPS = 50;
            inputScaleFactor = 1.0f;
            threshold = 0.125f;
        }

    }


    public GeneralSettings generalSettings;


    [System.Serializable]
    public class SACNSettings
    {

        // в какой юниверс писать
        public ushort universeToWrite = 0;
        // с какого адреса ( адаптера )
        // слать sACN
        public string ipToUse = string.Empty;


        public void Reset()
        {
            universeToWrite = 0;

            ipToUse = string.Empty;


        }


    }


    [System.Serializable]
    public class DMXSettings
    {
/*
        // Добавляем параметры для DMX
        public int dmxStartChannel = 1;

        public int channelsPerFixture = 16;

        public bool useArtNet = false;

        public string comPort = "COM3";

        public int baudRate = 115200;
*/

        public string dmxPortName = string.Empty;

        public int dmxUpdateRate = 30;

        public int dmxUniverseIndex = 0;


        public void Reset()
        {

            dmxPortName = string.Empty;

            dmxUpdateRate = 30;

            dmxUniverseIndex = 0;


        }

    }

    public DMXSettings dmxSettings;




    public void ResetData()
    {
        
    }


}
