using UnityEngine;
using System.Collections;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Collections.Generic;
using System;


namespace IA
{
    //[ExecuteAlways]
    public class DMX
    {
        public Action<string> OnDMXError;

        ArtNetData artNetData;

        int universe;

        int dmxUpdateRate;

        private const int DMX_INDEX_OFFSET = 5;
        private const int DMX_MESSAGE_OVERHEAD = 6;

        private const int N_DMX_CHANNELS = 512;
        public int nChannels
        {
            get { return N_DMX_CHANNELS; }
        }

        private const byte DMX_PRO_HEADER_SIZE = 4;
        private const byte DMX_PRO_START_MSG = 0x7E;
        private const byte DMX_PRO_LABEL_DMX = 6;
        private const byte DMX_PRO_LABEL_SERIAL_NUMBER = 10;
        private const byte DMX_PRO_START_CODE = 0;
        private const byte DMX_PRO_START_CODE_SIZE = 1;
        private const byte DMX_PRO_END_MSG = 0xE7;
        private const byte DMX_PRO_END_MSG_SIZE = 1;


        private const int TX_BUFFER_LENGTH = DMX_MESSAGE_OVERHEAD + N_DMX_CHANNELS;
        
        private Thread dmxThread;

        public bool updateDMX = false;

        private static SerialPort serialPort;
        public string[] serialPorts;
        public int serialPortIdx;

        private byte[] DMXLevels = new byte[N_DMX_CHANNELS];
        private byte[] TxBuffer = new byte[DMX_MESSAGE_OVERHEAD + N_DMX_CHANNELS];

        bool dmxIsActive = true;


        public DMX(int universe )
        {
            throw new NotImplementedException();
        }


        public DMX( ArtNetData arnd , GenericDMXFixture.SerialPortSettings sp_settings , int unvrs ,int updateRate )
        {
            Debug.Log("System started");
            artNetData = arnd;
            serialPorts = GetPortNames();

            
            //if (serialPortIdx > 0) 
            Debug.Log("System started");

            this.universe = unvrs;

            dmxUpdateRate = updateRate;

            OpenSerialPort(sp_settings);

            initTXBuffer();

            dmxThread = new Thread(
                () =>
                {
                    try
                    {
                        ThreadedIO();
                    }

                    catch (Exception e) 
                    {
                        //Debug.Log("DMX err caught: " + e );

                        OnDMXError?.Invoke(e.Message);

                    }

                });

            dmxThread.Start();  

        }

        /*
        public DMX(int universe)
        {

            serialPorts = GetPortNames();
            //if (serialPortIdx > 0) 
            Debug.Log("System started");
            this.universe = universe;

            OpenSerialPort();

            FindDataMap();

            initTXBuffer();

            dmxThread = new Thread(ThreadedIO);
            dmxThread.Start();

            updateDMX = true;

        }
        */

        public int this[int index]
        {
            get
            {
                if (index < 1 || index > N_DMX_CHANNELS)
                {
                    throw new UnityException("Channel out fo range: " + index);
                }
                else
                {
                    return (int)DMXLevels[index - 1];
                }
            }
            set
            {
                if (index < 1 || index > N_DMX_CHANNELS)
                {
                    throw new UnityException("Channel out fo range: " + index);
                }
                else
                {
                    if (value < 0 || value > 255)
                    {
                        throw new UnityException("Level out fo range");
                    }
                    DMXLevels[index - 1] = (byte)Mathf.Clamp(value, 0, 255);

                    updateDMX = true;
                }
            }
        }

        private void ThreadedIO()
        {

                while (dmxIsActive)
                {
                    if (updateDMX)
                    {
                            //updateDMX = false;
                            Buffer.BlockCopy(artNetData.dmxDataMap[universe], 0, TxBuffer, DMX_INDEX_OFFSET, N_DMX_CHANNELS);

                            serialPort?.Write(TxBuffer, 0, TX_BUFFER_LENGTH);

                            Thread.Sleep(dmxUpdateRate);
                    
                    }
                
                }

        }

        private string[] GetPortNames()
        {

            return SerialPort.GetPortNames();

        }
        private void FindDataMap()
        {
            //var path = "Assets/Scripts/ScriptableObjects/";
            //var objectName = "DataMap.asset";

            //artNetData = AssetDatabase.LoadAssetAtPath<ArtNetData>(path + objectName);

            
        }

        public bool IsReady()
        {

            return serialPort.IsOpen;

        }

        public void OpenSerialPort( GenericDMXFixture.SerialPortSettings portSettings)
        {
            if (serialPort != null)
            {
                serialPort.Close();
                
                serialPort.Dispose();
            
            }

            serialPort = new SerialPort(portSettings.portName, portSettings.baudRate, portSettings.parity , portSettings.dataBits , portSettings.sb) ;

            try
            {
                serialPort.Open();
                
                serialPort.ReadTimeout = 50;

                //updateDMX = true;
            }
            
            catch (System.Exception e)
            {
                serialPortIdx = 0;

                throw e;
            
            }

        }

        private void initTXBuffer()
        {
            for (int i = 0; i < TX_BUFFER_LENGTH; i++) TxBuffer[i] = (byte)255;

            TxBuffer[000] = DMX_PRO_START_MSG;
            TxBuffer[001] = DMX_PRO_LABEL_DMX;
            TxBuffer[002] = (byte)(N_DMX_CHANNELS + 1 & 255); ;
            TxBuffer[003] = (byte)((N_DMX_CHANNELS + 1 >> 8) & 255);
            TxBuffer[004] = DMX_PRO_START_CODE;
            TxBuffer[517] = DMX_PRO_END_MSG;
        }

        public void Quit()
        {


            //Reset DMX levels to 0;
            for (int i = 0; i < N_DMX_CHANNELS; i++) DMXLevels[i] = (byte)0x00;

            //
            //
            dmxThread.Abort();

            //dmxIsActive = false;

            serialPort?.Close();

        }
    }
}

