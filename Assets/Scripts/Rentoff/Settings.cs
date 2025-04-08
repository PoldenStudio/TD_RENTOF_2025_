using UnityEngine;

public class Settings : MonoBehaviour 
{
    public static Settings Instance { get; private set; }

    [Header("Video Settings")]
    public string defaultModeMovieName = "";
    public string idleModeMovieName = "";

    [Header("Grid Settings")]
    public int rows = 5;
    public int cols = 5;
    public int segments = 12;

    [Header("Input Settings")]
    public float sampleRate = 0.05f; 

    [Header("Swipe Settings")]
    public float swipeTimeThreshold = 0.2f; // Время удержания свайпа
    public float speedMultiplier = 2f;    // Ускорение от свайпа
    public float swipeContinuationTolerance = 0.4f;

    [Header("Audio Settings")] 
    public float baseFrequency = 440.0f;
    public float frequencyIncrement = 50.0f;

    [Header("Visualisation")]
    public float fadeDuration = 0.5f; 

private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}