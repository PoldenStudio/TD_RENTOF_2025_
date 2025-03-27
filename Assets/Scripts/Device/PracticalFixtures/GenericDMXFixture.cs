using UnityEngine;
using System.IO.Ports;
using System;
using System.Threading;
using IA;
using static GenericDMXFixture;



//[ExecuteAlways]
public class GenericDMXFixture : IA.DMXFixture
 {
    public static GenericDMXFixture Instance
    { get; private set; }


    [SerializeField]
    GlobalSettings gs;


    [Serializable]
    public class SerialPortSettings
    {
        public string portName;

        public int baudRate = 56600;

        public System.IO.Ports.Parity parity = Parity.None;

        public int dataBits = 8;

        public System.IO.Ports.StopBits sb = StopBits.One;

    }

    [SerializeField]
    SerialPortSettings serialPortSettings;

    DMX dmxContr;

    /*

    public override int getUniverse { get { return universe = 1; } }
    public override int getDmxAddress { get { return dmxAddress; } }

    */   

    public override void OnEnable()
    {
        base.OnEnable();
        try
        {
            serialPortSettings.portName = gs.dmxSettings.dmxPortName;
            dmxContr = new DMX(artNetData, serialPortSettings, gs.dmxSettings.dmxUniverseIndex , gs.dmxSettings.dmxUpdateRate );

            ResetLightData();
        }

        catch (System.Exception err)
        {
            print("Unable to open dmx port !: " + err.Message);

        }

        finally
        {
            Instance = (Instance == null) ? this : Instance;
        }
    }

    void ResetLightData()
    {
        dmxContr.updateDMX = true;
        
        artNetData.ResetData();

        Thread.Sleep(100);

        dmxContr.updateDMX = false;

        Thread.Sleep(100);

        dmxContr.updateDMX = true;
 
        /**/
    }

    public void SendDataToLight(byte data, int channelNum)
    {
        artNetData.dmxDataMap[gs.dmxSettings.dmxUniverseIndex][channelNum] = data;
    }

    private void OnDestroy()
    {
        if (dmxContr != null)
        {
            artNetData.ResetData();

            Thread.Sleep(100);

            dmxContr.Quit();
        }


    }

    public void ResetDmxData()
    {
        artNetData.ResetData();
    }




}



