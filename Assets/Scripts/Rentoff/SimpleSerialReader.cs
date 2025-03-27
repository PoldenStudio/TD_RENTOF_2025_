using UnityEngine;
using System.IO.Ports;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

public class SimpleSerialReader : MonoBehaviour
{
    [SerializeField] private string portName = "COM5";
    [SerializeField] private int baudRate = 115200;

    private SerialPort serialPort;
    private LoadScene loadScene;
    private void Start()
    {
        OpenPort();
    }

    private void OnDestroy()
    {
        ClosePort();
    }

    private void Update()
    {
        if (serialPort != null && serialPort.IsOpen && serialPort.BytesToRead > 0)
        {
            try
            {
                string data = serialPort.ReadLine().Trim();
                ProcessData(data);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error reading serial port: {e.Message}");
                ClosePort();
            }
        }
    }

    private void ProcessData(string data)
    {
        Match match = Regex.Match(data, @"touch_status:?\s*(\d+)");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int touchStatus))
            {
                Debug.Log($"Touch status received: {touchStatus}");

                ClosePort();
                StartCoroutine(loadScene.FadeAndLoadScene());
                //SceneManager.LoadScene(1);
            }
        }
    }

    private void OpenPort()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.Open();
            Debug.Log("Port opened successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error opening port: {e.Message}");
        }
    }

    private void ClosePort()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            serialPort = null;
            Debug.Log("Port closed");
        }
    }
}