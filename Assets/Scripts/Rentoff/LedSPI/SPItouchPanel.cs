using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LEDControl;
using static StateManager;


public class SPItouchPanel : MonoBehaviour
{
    [SerializeField] private StripDataManager stripDataManager;
    [SerializeField] private ColorProcessor colorProcessor;
    [SerializeField] private EffectsManager effectsManager;
    [SerializeField] private DataSender dataSender;
    [SerializeField] private StateManager stateManager;

    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = false;

    private bool dataChanged = true;

    private void Awake()
    {

        if (stripDataManager == null)
        {
            stripDataManager = gameObject.AddComponent<StripDataManager>();
        }

        if (colorProcessor == null)
        {
            colorProcessor = gameObject.AddComponent<ColorProcessor>();
        }

        if (effectsManager == null)
        {
            effectsManager = gameObject.AddComponent<EffectsManager>();
        }

        if (dataSender == null)
        {
            dataSender = gameObject.AddComponent<DataSender>();
        }

        if (stateManager != null)
        {
            stateManager.OnStateChanged += HandleStateChanged;
        }

    }

    private void HandleStateChanged(AppState newState)
    {

        switch (newState)
        {
            case AppState.Idle:
                break;
            case AppState.Active:
                break;
            case AppState.Transition:
                break;
        }
    }

    private void Start()
    {
        // Инициализация
        stripDataManager.InitializeStripData();
        stripDataManager.CachePreviousValues();
        dataSender.Initialize();


        if (!dataSender.IsPortOpen())
        {
            Debug.LogError("[SPItouchPanel] Serial port not open. Disabling component.");
            enabled = false;
            return;
        }
    }

    private void OnValidate()
    {
        bool colorsChanged = false;

        if (stripDataManager != null)
        {
            colorsChanged = stripDataManager.CheckForChanges();
        }

        if (effectsManager != null)
        {
            colorsChanged |= effectsManager.CheckForChanges();
        }

        if (colorsChanged)
        {
            dataChanged = true;
        }
    }

    void FixedUpdate()
    {
        if (dataChanged || dataSender.ShouldSendData())
        {
            if (stripDataManager.currentDisplayModes.Contains(DisplayMode.SpeedSynthMode))
            {
                effectsManager.UpdateSynthPosition();
            }

            if (stripDataManager.currentDisplayModes.Contains(DisplayMode.SunMovement))
            {
                effectsManager.UpdateSunMovementPhase();
            }

            SendDataToLEDStrip();
            dataChanged = false;
        }
    }

    private void SendDataToLEDStrip()
    {
        StringBuilder fullData = new StringBuilder();

        for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
        {
            if (!stripDataManager.stripEnabled[stripIndex]) continue; // Пропускаем отключенные ленты

            // Проверяем ограничения режимов
            if (stripDataManager.currentDataModes[stripIndex] == DataMode.Monochrome1Color &&
                stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
            {
                Debug.LogWarning("[SPItouchPanel] Monochrome1Color не поддерживает SpeedSynthMode.");
                continue;
            }

            string dataString = dataSender.GenerateDataString(stripIndex, stripDataManager, effectsManager, colorProcessor);
            fullData.Append(dataString);
        }

        //fullData.Append("sync\r\n");
        dataSender.SendDataToLEDStrip(fullData.ToString());
    }

    // Публичные методы для внешнего управления
    public void SetSegmentColor(int stripIndex, int segmentIndex, Color32 color)
    {
        stripDataManager.SetSegmentColor(stripIndex, segmentIndex, color, debugMode);
        dataChanged = true;
    }

    public Color32 GetSegmentColor(int stripIndex, int segmentIndex)
    {
        return stripDataManager.GetSegmentColor(stripIndex, segmentIndex);
    }

    public List<Color32> GetSegmentColors(int stripIndex)
    {
        return stripDataManager.GetSegmentColors(stripIndex);
    }

    public void SetGlobalColorMonochrome(int stripIndex, Color32 color)
    {
        stripDataManager.SetGlobalColorMonochrome(stripIndex, color, debugMode);
        dataChanged = true;
    }

    public Color32 GetGlobalColorMonochrome(int stripIndex)
    {
        return stripDataManager.GetGlobalColorMonochrome(stripIndex);
    }

    public void SetGlobalColorRGB(int stripIndex, Color32 color)
    {
        stripDataManager.SetGlobalColorRGB(stripIndex, color, debugMode);
        dataChanged = true;
    }

    public Color32 GetGlobalColorRGB(int stripIndex)
    {
        return stripDataManager.GetGlobalColorRGB(stripIndex);
    }

    public void SetDisplayMode(int stripIndex, int modeIndex)
    {
        stripDataManager.SetDisplayMode(stripIndex, modeIndex, debugMode);
        dataChanged = true;
    }

    public void SetDataMode(int stripIndex, int modeIndex)
    {
        stripDataManager.SetDataMode(stripIndex, modeIndex, debugMode);
        dataChanged = true;
    }

    public void SetStripEnabled(int stripIndex, bool enabled)
    {
        stripDataManager.SetStripEnabled(stripIndex, enabled, debugMode);
        dataChanged = true;
    }

    public void UpdateSynthParameters(float speed)
    {
        effectsManager.UpdateSpeed(speed);
        dataChanged = true;
    }

    public bool IsPortOpen()
    {
        return dataSender.IsPortOpen();
    }
}