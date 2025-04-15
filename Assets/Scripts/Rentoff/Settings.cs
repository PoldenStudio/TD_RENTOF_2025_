using UnityEngine;
using System.IO;
using System;

public class Settings : MonoBehaviour
{
    public static Settings Instance { get; private set; }

    [Header("Video Settings")]
    public string defaultModeMovieName = "";
    public string idleModeMovieName = "";
    public string CurtainMovieName = "";
    public string CometMovieName = "";

    [Header("Grid Settings")]
    public int rows = 5;
    public int cols = 5;
    public int segments = 12;

    [Header("Input Settings")]
    public float sampleRate = 0.05f;

    [Header("Swipe Settings")]
    public float swipeTimeThreshold = 0.2f;
    public float speedMultiplier = 2f;
    public float swipeContinuationTolerance = 0.4f;

    [Header("Audio Settings")]
    public float baseFrequency = 440.0f;
    public float frequencyIncrement = 50.0f;

    [Header("Visualisation")]
    public float fadeDuration = 0.5f;

    [Header("App Settings")]
    public bool vSync = false;
    public int frameRate = 120;

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
        }

        ApplySettings();
    }

    private void InitializeSettingsPath()
    {
        settingsPath = Path.Combine(Application.persistentDataPath, "settings.json");

#if UNITY_EDITOR
        if (!Directory.Exists(Application.streamingAssetsPath))
        {
            Directory.CreateDirectory(Application.streamingAssetsPath);
        }
#endif
    }

    private void ApplySettings()
    {
        QualitySettings.vSyncCount = vSync ? 1 : 0;
        Application.targetFrameRate = frameRate;
    }

    [ContextMenu("Save Settings")]
    public void SaveSettings()
    {
        try
        {
            InitializeSettingsPath();

            if (string.IsNullOrEmpty(settingsPath))
            {
                Debug.LogError("Settings path is invalid!");
                return;
            }

            string directory = Path.GetDirectoryName(settingsPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string jsonData = JsonUtility.ToJson(this, true);
            File.WriteAllText(settingsPath, jsonData);
            Debug.Log($"Settings saved to: {settingsPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save settings: {e.Message}\n{e.StackTrace}");
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
                string jsonData = File.ReadAllText(settingsPath);
                JsonUtility.FromJsonOverwrite(jsonData, this);
                Debug.Log($"Settings loaded from: {settingsPath}");
                ApplySettings();
            }
            else
            {
                Debug.Log("No saved settings found. Using default values.");
                SaveSettings();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load settings: {e.Message}\n{e.StackTrace}");
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Open Settings Folder")]
    private static void OpenSettingsFolder()
    {
        string path = Path.Combine(Application.persistentDataPath);
        System.Diagnostics.Process.Start(path);
    }
#endif
}