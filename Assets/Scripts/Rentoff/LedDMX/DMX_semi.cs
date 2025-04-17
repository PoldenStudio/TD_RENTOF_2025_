/*using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using Unity.VisualScripting;
using UnityEngine;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

public class DMX_semi : MonoBehaviour
{

    public bool readyToSend = false;

    [SerializeField]
    Color upperColor;

    [SerializeField]
    Color lowerColor;


    [System.Serializable]
    public class DMXFrame
    {
        public byte[] frame = new byte[513];
    }

    public DMXFrame[] frames;


    [SerializeField]
    string pn = "COM12";

    DMXCommunicator dmxc;






    private void Start()
    {
        int dmxArrayFPS = 60;

        int contentLength = 184;

        frames = new DMXFrame[dmxArrayFPS * contentLength];

        try
        {
            dmxc = new DMXCommunicator(pn, 250000);
        }

        catch (System.Exception err)
        {
            print(err);
        }
        finally
        { }


    }



    public void SendFrame(int frameNum)
    {
        int fn = (frameNum < frames.Length) ? frameNum : frames.Length - 1;

        dmxc.SendFrame(frames[fn].frame);
    }


    private void OnApplicationQuit()
    {

        dmxc?.Stop();

    }


}*/