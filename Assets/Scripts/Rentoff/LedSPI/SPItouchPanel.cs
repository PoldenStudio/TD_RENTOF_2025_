

using LEDControl;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class SPItouchPanel : MonoBehaviour
{
    [SerializeField] public StripDataManager stripDataManager;
    [SerializeField] public ColorProcessor colorProcessor;
    [SerializeField] public EffectsManager effectsManager;
    [SerializeField] public SunManager sunManager;
    [SerializeField] public DataSender dataSender;
    [SerializeField] public StateManager stateManager;
    [SerializeField] private SwipeDetector swipeDetector;

    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = false;

    private float lastDataSendTime = 0f;
    private const float sendDataInterval = 0.028f;

    private Dictionary<int, HashSet<int>> activeSegments = new Dictionary<int, HashSet<int>>();
    private Dictionary<int, float> lastSwipeTime = new Dictionary<int, float>();
    private Dictionary<int, MoveDirection> lastSwipeDirection = new Dictionary<int, MoveDirection>();

    private readonly StringBuilder fullDataBuilder = new StringBuilder(4096);

    private Color32 blackColor = new Color32(0, 0, 0, 255);

    private void Awake()
    {
        if (stripDataManager == null) stripDataManager = gameObject.AddComponent<StripDataManager>();
        if (colorProcessor == null) colorProcessor = gameObject.AddComponent<ColorProcessor>();
        if (effectsManager == null) effectsManager = gameObject.AddComponent<EffectsManager>();
        if (dataSender == null) dataSender = gameObject.AddComponent<DataSender>();

        if (stateManager != null) stateManager.OnStateChanged += HandleStateChanged;
        if (swipeDetector != null)
        {
            swipeDetector.SwipeDetected += HandleSwipeDetected;
            swipeDetector.PanelPressed += HandlePanelPressed;
        }
    }

    private void OnDestroy()
    {
        if (stateManager != null) stateManager.OnStateChanged -= HandleStateChanged;
        if (swipeDetector != null)
        {
            swipeDetector.SwipeDetected -= HandleSwipeDetected;
            swipeDetector.PanelPressed -= HandlePanelPressed;
        }
    }

    private void HandleStateChanged(StateManager.AppState newState)
    {
        switch (newState)
        {
            case StateManager.AppState.Idle:
            case StateManager.AppState.Transition:
                activeSegments.Clear();
                lastSwipeTime.Clear();
                lastSwipeDirection.Clear();
                break;
            case StateManager.AppState.Active:
                break;
        }
    }

    private void Start()
    {
        stripDataManager.InitializeStripData();
        stripDataManager.CachePreviousValues();
        dataSender.Initialize();

        bool anyPortOpen = false;
        for (int i = 0; i < dataSender.portConfigs.Count; i++)
        {
            if (dataSender.IsPortOpen(i)) 
            {
                anyPortOpen = true;
                break;
            }
        }

        if (!anyPortOpen)
        {
            Debug.LogError("[SPItouchPanel] Ни один из serial-портов не открыт. Disabling component.");
            enabled = false;
            return;
        }
    }

    private void OnValidate()
    {
        bool colorsChanged = false;
        if (stripDataManager != null) colorsChanged = stripDataManager.CheckForChanges();
    }

    private void FixedUpdate()
    {
        bool shouldUpdateEffects = false;

        if (stripDataManager.currentDisplayModes.Contains(DisplayMode.SpeedSynthMode))
        {
            for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
            {
                if (stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                {
                    effectsManager.UpdateComets(stripIndex, stripDataManager);
                    shouldUpdateEffects = true;
                }
            }
        }

        if (stripDataManager.currentDisplayModes.Contains(DisplayMode.SunMovement))
        {
            UpdateSun();
            shouldUpdateEffects = true;
        }

        if (shouldUpdateEffects) { }

        if (Time.time - lastDataSendTime >= sendDataInterval)
        {
            SendDataToLEDStrip();
            lastDataSendTime = Time.time;
        }
    }

    private void UpdateSun()
    {
        sunManager.UpdateSunMovementPhase();
        for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
        {
            if (stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SunMovement)
            {

            }
        }
    }

    private void HandleSwipeDetected(SwipeDetector.SwipeData swipeData)
    {
        if (stateManager.CurrentState != StateManager.AppState.Active) return;

        for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
        {
            if (stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
            {
                int startSegment = stripDataManager.touchPanelOffset;
                int totalSegments = stripDataManager.GetTotalSegments(stripIndex);
                int panelsCount = Mathf.Min(swipeData.panelsCount, totalSegments - startSegment);

                if (!activeSegments.ContainsKey(stripIndex))
                {
                    activeSegments[stripIndex] = new HashSet<int>();
                }

                for (int i = 0; i < panelsCount; i++)
                {
                    int segmentIndex = startSegment + i;
                    if (segmentIndex < totalSegments)
                    {
                        activeSegments[stripIndex].Add(segmentIndex);
                        stripDataManager.SetSegmentColor(stripIndex, segmentIndex, stripDataManager.GetSynthColorForStrip(stripIndex), debugMode);
                    }
                }

                lastSwipeTime[stripIndex] = Time.time;
                lastSwipeDirection[stripIndex] = swipeData.direction.x > 0 ? MoveDirection.Forward : MoveDirection.Backward;

                if (activeSegments[stripIndex].Count > 0)
                {
                    int minSegment = activeSegments[stripIndex].Min();
                    float dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(panelsCount * effectsManager.speedLedCountFactor));
                    float dynamicBrightness = Mathf.Clamp01(stripDataManager.GetStripBrightness(stripIndex) + swipeData.speed * effectsManager.speedBrightnessFactor);
                    effectsManager.AddComet(stripIndex, minSegment * stripDataManager.ledsPerSegment[stripIndex], stripDataManager.GetSynthColorForStrip(stripIndex), dynamicLedCount, dynamicBrightness);
                    effectsManager.moveDirection = lastSwipeDirection[stripIndex];
                }
            }
        }
    }

    private void HandlePanelPressed(int panelIndex, bool isPressed)
    {
        if (stateManager.CurrentState != StateManager.AppState.Active) return;

        for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
        {
            if (stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
            {
                int segmentIndex = panelIndex + stripDataManager.touchPanelOffset;
                int totalSegments = stripDataManager.GetTotalSegments(stripIndex);

                if (segmentIndex >= 0 && segmentIndex < totalSegments)
                {
                    Color32 currentColor = stripDataManager.GetSegmentColor(stripIndex, segmentIndex);
                    Color32 synthColor = stripDataManager.GetSynthColorForStrip(stripIndex);

                    if (isPressed)
                    {
                        if (!activeSegments.ContainsKey(stripIndex))
                        {
                            activeSegments[stripIndex] = new HashSet<int>();
                        }

                        if (effectsManager.toggleTouchMode && !currentColor.Equals(blackColor))
                        {
                            stripDataManager.SetSegmentColor(stripIndex, segmentIndex, blackColor, debugMode);
                            activeSegments[stripIndex].Remove(segmentIndex);
                        }
                        else if (currentColor.Equals(blackColor))
                        {
                            stripDataManager.SetSegmentColor(stripIndex, segmentIndex, synthColor, debugMode);
                            stripDataManager.SetSegmentColor(stripIndex, segmentIndex, synthColor, debugMode);
                            activeSegments[stripIndex].Add(segmentIndex);
                        }
                        effectsManager.UpdateLastTouchTime(stripIndex);
                    }
                }
            }
        }
    }

    private void SendDataToLEDStrip()
    {
        fullDataBuilder.Clear();

        for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
        {
            if (!stripDataManager.stripEnabled[stripIndex]) continue;

            if (stripDataManager.currentDataModes[stripIndex] == DataMode.Monochrome1Color &&
                stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
            {
                Debug.LogWarning("[SPItouchPanel] Monochrome1Color не поддерживает SpeedSynthMode.");
                continue;
            }

            string dataString = dataSender.GenerateDataString(
                stripIndex,
                stripDataManager,
                sunManager,
                effectsManager,
                colorProcessor,
                stateManager.CurrentState
            );

            if (!string.IsNullOrEmpty(dataString))
            {
                int portIndex = stripDataManager.GetPortIndexForStrip(stripIndex);
                dataSender.EnqueueData(portIndex, dataString);
            }
        }
    }

    public void UpdateSynthParameters(float speed)
    {
        effectsManager.UpdateSpeed(speed);
        sunManager.UpdateSpeed(speed);
    }
}