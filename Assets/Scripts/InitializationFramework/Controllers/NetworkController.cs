using UnityEngine;
using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Linq;
using InitializationFramework;
using UnityEngine.SceneManagement;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine.Events;

public class NetworkController : MonoBehaviour
{
    public Action<GlobalSettings.GeneralSettings.WorkingModes> onSwitchCommandRecieved;


    [SerializeField]
    int tgLevelIndex = 0;

    [SerializeField]
    GlobalSettings gs;


    Socket rs;

    IPEndPoint rP;



    // сокет управления звуком
    Socket soundSocket;

    IPEndPoint soundRP;





    byte[] rBuffer;

    byte[] soundBuffer;



    bool recievingIsActive = true;

    WaitForSeconds waitEnum;


    public static NetworkController Instance
    { get; private set; }

    bool switchIsInProgress = false;

    private void Start()
    {

        Instance = ( Instance == null ) ? this : Instance;


        rs = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        rP = new IPEndPoint(IPAddress.Any, gs.networkSettings.commandsRecievePort);



        soundSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        soundRP = new IPEndPoint(IPAddress.Any, gs.networkSettings.soundPort);

        soundSocket.Bind(soundRP);



        rBuffer = new byte[gs.networkSettings.commandPacketSize];

        soundBuffer = new byte[1];


        waitEnum = new WaitForSeconds(1.0f/60.0f);

        rs.Bind(rP);
        
        StartCoroutine(ListenForCommands());

        StartCoroutine(ListenForSoundCommands());

    }

    IEnumerator ListenForCommands()
    {

        char splitChar = ';';


        while (recievingIsActive)
        {
            if (rs.Available == gs.networkSettings.commandPacketSize  )
            {
                rs.Receive(rBuffer);

                print("RECIEVED");

                if (rBuffer[gs.networkSettings.commandPacketSize - 1] == splitChar)
                {

                    var recievedCommand = (GlobalSettings.GeneralSettings.WorkingModes)rBuffer[0];

                    onSwitchCommandRecieved?.Invoke(recievedCommand);

                }


            }

            yield return waitEnum;


        }


    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.F1))
        { onSwitchCommandRecieved?.Invoke( GlobalSettings.GeneralSettings.WorkingModes.DefaultMode );  }
        if (Input.GetKey(KeyCode.F2))
        { onSwitchCommandRecieved?.Invoke( GlobalSettings.GeneralSettings.WorkingModes.SPCMode); }

    }

    IEnumerator ListenForSoundCommands()
    {

        while (recievingIsActive)
        {
            if (soundSocket.Available >= 1)
            {
                try
                {

                    soundSocket.Receive(soundBuffer);

                    float val = (float)soundBuffer[0];

                    float maxVal = (float)100.0f;

                    float volumeLevel = Mathf.Clamp01(val / maxVal);
                    print("VOLUME: " + volumeLevel);
                    AudioListener.volume = volumeLevel;

                    gs.generalSettings.lastSetVolumeLevel = volumeLevel;

                }

                catch { }

            }

            yield return waitEnum;
        }

    }

    public void ResetCommunication()
    {
        onSwitchCommandRecieved = null;
    }


    private void OnDestroy()
    {
        recievingIsActive = false;

        rs?.Close();

        soundSocket?.Close();
    }


}
