using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using System.Threading.Tasks; // Import for Task.Run
using System.Runtime.InteropServices; //For GetLastError

public class DMXCommunicator : IDisposable
{
    public static DMXCommunicator Instance { get; private set; }

    private byte[] buffer = new byte[513];
    private bool isActive = false;
    private Thread senderThread;
    private SerialPort serialPort;
    private readonly object lockObject = new object();
    private CancellationTokenSource cts;
    private bool isDisposed = false;
    private readonly string portName;
    private readonly int baudRate;

    private const int DMX_FRAME_RATE = 30; // Standard DMX refresh rate
    private const int BREAK_TIME_MS = 1;   // Break time (min 88us)
    private const int MAB_TIME_MS = 1;     // Mark After Break (min 8us)

    public DMXCommunicator(string portName, int baudRate)
    {


        this.portName = portName;
        this.baudRate = baudRate;
        buffer[0] = 0; // Start code
        serialPort = ConfigureSerialPort();
        if (serialPort == null)
        {
            Debug.LogError($"[DMXCommunicator] Failed to initialize DMX, invalid port: {portName}");
            Instance = null; // Important
            return; // Prevent further initialization
        }
        cts = new CancellationTokenSource();
        Instance = this;
        StartSending();
    }

    private SerialPort ConfigureSerialPort()
    {
        try
        {
            SerialPort port = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                Handshake = Handshake.None,
                Parity = Parity.None,
                StopBits = StopBits.Two,
                WriteTimeout = 1000,
                ReadTimeout = 1000
            };

            if (port.IsOpen)
                port.Close();

            port.Open();
            Debug.Log($"[DMXCommunicator] Successfully opened COM port: {portName} at {baudRate} baud");
            return port;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DMXCommunicator] Failed to configure COM port {portName}: {ex.Message}");
            return null; // Indicate failure
        }
    }

    private void StartSending()
    {
        isActive = true;
        senderThread = new Thread(() =>
        {
            try
            {
                while (isActive && !cts.Token.IsCancellationRequested)
                {
                    SendFrameInternal();
                    Thread.Sleep(1000 / DMX_FRAME_RATE);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DMXCommunicator] Sending thread error: {ex.Message}");
                isActive = false;
            }
        })
        {
            IsBackground = true,
            Name = "DMX_Sender_Thread"
        };
        senderThread.Start();
    }

    public bool IsActive
    {
        get
        {
            lock (lockObject)
            {
                return isActive && !isDisposed && serialPort.IsOpen;
            }
        }
    }

    public void SetChannel(int channel, byte value)
    {
        if (Instance == null) return; // Check if the DMXComm is initialized
        if (channel < 1 || channel > 512)
            throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be between 1 and 512");

        lock (lockObject)
        {
            buffer[channel] = value;
        }
    }

    public void SendFrame(byte[] newBuffer)
    {
        if (newBuffer.Length != 513)
            throw new ArgumentException("This byte array does not contain 513 elements", "newBuffer");

        lock (this)
        {
            // Копируем каналы 1-512 из newBuffer (индексы 1-512) в buffer начиная с индекса 1
            Array.Copy(newBuffer, 1, buffer, 1, 512);
        }
    }

    public void SetBytes(byte[] newBuffer)
    {
        if (newBuffer.Length != 513)
            throw new ArgumentException("This byte array does not contain 513 elements", "newBuffer");

        lock (this)
        {
            Array.Copy(newBuffer, 1, buffer, 1, 512);
        }
    }

    public void SendFrameInternal()
    {
        if (isDisposed || !serialPort.IsOpen) return;

        try
        {
            lock (lockObject)
            {
                // Send break
                serialPort.BreakState = true;
                Thread.Sleep(BREAK_TIME_MS);
                serialPort.BreakState = false;
                Thread.Sleep(MAB_TIME_MS);


                //Debug.Log("sending: " + buffer[1]);

                // Send data
                serialPort.Write(buffer, 0, buffer.Length);

                //Thread.Sleep(DMX_FRAME_RATE);


            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DMXCommunicator] Failed to send frame on {portName}: {ex.Message}");
            isActive = false;
            // Potentially attempt to re-initialize the port here (carefully)
        }
    }

    public static List<string> GetValidSerialPorts()
    {
        List<string> portNames = new List<string>();
        try
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                try
                {
                    using (SerialPort testPort = new SerialPort(port))
                    {
                        testPort.Open();
                        testPort.Close();
                        portNames.Add(port);
                    }
                }
                catch (Exception)
                {
                    // Ignore ports that can't be opened (e.g., in use)
                    Debug.LogWarning($"[DMXCommunicator] Could not open port {port} for testing");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DMXCommunicator] Error getting serial port names: {ex.Message}");
        }

        return portNames;
    }

    public void Dispose()
    {
        if (isDisposed) return;

        lock (lockObject)
        {
            isDisposed = true;
            isActive = false;
            cts?.Cancel();

            try
            {
                if (senderThread?.IsAlive == true)
                {
                    senderThread.Join(1000);
                    if (senderThread.IsAlive)
                        Debug.LogWarning("[DMXCommunicator] Sender thread did not terminate gracefully");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DMXCommunicator] Error stopping sender thread: {ex.Message}");
            }

            try
            {
                if (serialPort?.IsOpen == true)
                {
                    serialPort.Close();
                    Debug.Log($"[DMXCommunicator] COM port {portName} closed");
                }
                serialPort?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DMXCommunicator] Error closing COM port {portName}: {ex.Message}");
            }

            cts?.Dispose();
            if (Instance == this)
                Instance = null;
        }
    }

    public void Stop()
    {
        // Prevents it from being stopped more than once
        if (this.IsActive)
        {
            lock (this)
            {
                if (this.IsActive)
                {
                    this.isActive = false;
                    try
                    {
                        senderThread.Join(1000);
                    }
                    catch (Exception)
                    {
                        // TODO: Better exception handling
                    }
                    if (serialPort != null && serialPort.IsOpen)
                        serialPort.Close();
                }
            }
        }
    }
}