using UnityEngine;
using System;
using System.IO;

public class Settings : MonoBehaviour
{
    public static Settings Instance { get; private set; }

    #region Save settings
    [Header("Save settings")]

    public string defaultModeMovieName = "";
    public string idleModeMovieName = "";
    public string CurtainMovieName = "";
    public string CometMovieName = "";

    public int frameRate = 120;

    // SerialInputReader
    [Space(10)]
    public string[] serialPortNames = { "COM5", "COM6" };
    public int serialBaudRate = 115200;

    // DataSender
    [Space(10)]
    public string dataSenderPortName = "COM6";
    public int dataSenderBaudRate = 115200;

    // MIDI
    [Space(10)]
    public string loopMidiDeviceId = "";

    // DMX (LEDController)
    [Space(10)]
    public string dmxComPortName = "COM3";
    public int dmxBaudRate = 250000;
    
    // Display
    [Space(10)]
    public int targetDisplayId = 0;
    #endregion

    #region Runtime‑only settings (не сохраняются в JSON)
    [NonSerialized] public int rows = 5;
    [NonSerialized] public int cols = 5;
    [NonSerialized] public int segments = 12;

    [NonSerialized] public float sampleRate = 0.05f;
    [NonSerialized] public float swipeTimeThreshold = 0.2f;
    [NonSerialized] public float speedMultiplier = 2f;
    [NonSerialized] public float swipeContinuationTolerance = 0.4f;

    [NonSerialized] public float baseFrequency = 440.0f;
    [NonSerialized] public float frequencyIncrement = 50.0f;

    [NonSerialized] public float fadeDuration = 0.5f;
    [NonSerialized] public bool vSync = false;
    #endregion

    private string settingsPath;

    private void Awake()
    {
        InitializeSettingsPath();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ApplySettings();
    }

    private void InitializeSettingsPath()
    {
#if UNITY_EDITOR
        settingsPath = Path.Combine(Application.streamingAssetsPath, "settings.json");
#else
        settingsPath = Path.Combine(Application.persistentDataPath, "settings.json");
#endif

        var dir = Path.GetDirectoryName(settingsPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private void ApplySettings()
    {
        QualitySettings.vSyncCount = vSync ? 1 : 0;
        Application.targetFrameRate = frameRate;
        ApplyDisplaySettings();
    }

    private void ApplyDisplaySettings()
    {
        if (targetDisplayId >= 0 && targetDisplayId < Display.displays.Length)
        {
            for (int i = 1; i < Display.displays.Length; i++)
            {
                Display.displays[i].Activate();
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.targetDisplay = targetDisplayId;
            }
            else
            {
                Debug.LogWarning("[Settings] Главная камера не найдена!");
            }
            Debug.Log($"[Settings] Камера назначена на дисплей {targetDisplayId} из {Display.displays.Length}.");
        }
    }

    [ContextMenu("Save Settings")]
    public void SaveSettings()
    {
        try
        {
            InitializeSettingsPath();

            // Serialize only public fields (JsonUtility ignores [NonSerialized])
            string json = JsonUtility.ToJson(this, prettyPrint: true);
            File.WriteAllText(settingsPath, json);

            Debug.Log($"[Settings] Saved to {settingsPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Settings] Save failed: {ex}");
        }
    }

    [ContextMenu("Load Settings")]
    public void LoadSettings()
    {
        try
        {
            InitializeSettingsPath();

            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                JsonUtility.FromJsonOverwrite(json, this);
                Debug.Log($"[Settings] Loaded from {settingsPath}");
            }
            else
            {
                Debug.LogWarning("[Settings] No settings file found. Creating default one.");
                SaveSettings();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Settings] Load failed: {ex}");
        }
        finally
        {
            ApplySettings();
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Open Settings Folder")]
    private static void OpenSettingsFolder()
    {
        string path = Application.streamingAssetsPath;
        System.Diagnostics.Process.Start(path);
    }
#endif
}