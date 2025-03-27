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

        // ���� �� ������� ����� ����������� ��������
        public int commandsRecievePort = -1;
        // ���-�� ���� � ��������
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
            // ����������� �����
            DefaultMode = 0,
            // ���� �����
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

        // � ����� ������� ������
        public ushort universeToWrite = 0;
        // � ������ ������ ( �������� )
        // ����� sACN
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
        // ��������� ��������� ��� DMX
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
