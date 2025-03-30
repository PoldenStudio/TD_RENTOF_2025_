using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LEDControl;
using System.Linq;

public class SPItouchPanel : MonoBehaviour
{
    [SerializeField] public StripDataManager stripDataManager;
    [SerializeField] public ColorProcessor colorProcessor;
    [SerializeField] public EffectsManager effectsManager;
    [SerializeField] public DataSender dataSender;
    [SerializeField] public StateManager stateManager;
    [SerializeField] private SwipeDetector swipeDetector;

    [Header("Debug Settings")]
    [SerializeField] private bool debugMode = false;

    private bool dataChanged = true;
    private float transitionStartTime = 0f;
    private bool isTransitioning = false;
    private bool initialCometSpawned = false; // Флаг для отслеживания создания первой кометы в Transition

    private Dictionary<int, HashSet<int>> activeSegments = new Dictionary<int, HashSet<int>>(); // Подсвеченные сегменты для каждой ленты
    private Dictionary<int, float> lastSwipeTime = new Dictionary<int, float>(); // Время последнего свайпа для каждой ленты
    private Dictionary<int, MoveDirection> lastSwipeDirection = new Dictionary<int, MoveDirection>(); // Направление последнего свайпа для каждой ленты

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

        if (swipeDetector != null)
        {
            swipeDetector.SwipeDetected += HandleSwipeDetected;
            swipeDetector.PanelPressed += HandlePanelPressed;
        }
    }

    private void OnDestroy()
    {
        if (stateManager != null)
        {
            stateManager.OnStateChanged -= HandleStateChanged;
        }

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
                dataChanged = true;
                activeSegments.Clear(); // Сбрасываем подсвеченные сегменты
                lastSwipeTime.Clear(); // Сбрасываем время последнего свайпа
                lastSwipeDirection.Clear(); // Сбрасываем направление последнего свайпа
                initialCometSpawned = false; // Сбрасываем флаг при переходе в Idle
                break;
            case StateManager.AppState.Active:
                dataChanged = true;
                initialCometSpawned = false; // Сбрасываем флаг при переходе в Active
                break;
            case StateManager.AppState.Transition:
                transitionStartTime = Time.time;
                isTransitioning = true;
                dataChanged = true;
                activeSegments.Clear(); // Сбрасываем подсвеченные сегменты
                lastSwipeTime.Clear(); // Сбрасываем время последнего свайпа
                lastSwipeDirection.Clear(); // Сбрасываем направление последнего свайпа
                initialCometSpawned = false; // Сбрасываем флаг при переходе в Transition
                break;
        }
    }

    private void Start()
    {
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
        if (dataChanged && dataSender.ShouldSendData())
        {
            if (stateManager.CurrentState == StateManager.AppState.Transition && Time.time - transitionStartTime >= 1f && isTransitioning && !initialCometSpawned)
            {
                // Создаем первую комету в режиме Transition только через секунду
                for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
                {
                    if (stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                    {
                        float dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(effectsManager.synthLedCountBase + Mathf.Abs(effectsManager.currentSpeed) * effectsManager.speedLedCountFactor));
                        float dynamicBrightness = Mathf.Clamp01(stripDataManager.GetStripBrightness(stripIndex) + Mathf.Abs(effectsManager.currentSpeed) * effectsManager.speedBrightnessFactor);
                        effectsManager.AddComet(stripIndex, 0, stripDataManager.GetSynthColorForStrip(stripIndex), dynamicLedCount, dynamicBrightness);
                    }
                }
                initialCometSpawned = true; // Устанавливаем флаг, чтобы больше не создавать комету в этом Transition
                isTransitioning = false; // Отключаем флаг isTransitioning после создания первой кометы
            }
            else if (stateManager.CurrentState == StateManager.AppState.Transition && !isTransitioning && initialCometSpawned)
            {
                // Проверяем, достигли ли все кометы конца ленты после первоначального спавна
                bool allCometsFinished = true;
                for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
                {
                    if (stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                    {
                        effectsManager.UpdateComets(stripIndex, stripDataManager);
                        if (effectsManager.stripComets.ContainsKey(stripIndex) && effectsManager.stripComets[stripIndex].Count > 0)
                        {
                            allCometsFinished = false;
                        }
                    }
                }
                if (allCometsFinished)
                {
                    stateManager.StartTransitionToIdle();
                }
            }
            else if (stateManager.CurrentState == StateManager.AppState.Transition && isTransitioning)
            {
                // Ничего не делаем, ждем секунду
            }
            else
            {
                isTransitioning = false; // Гарантируем, что флаг выключен в других состояниях
            }

            if (stripDataManager.currentDisplayModes.Contains(DisplayMode.SpeedSynthMode))
            {
                for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
                {
                    if (stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
                    {
                        effectsManager.UpdateComets(stripIndex, stripDataManager);
                    }
                }
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
        byte[] fullData = new byte[1024]; // Временный буфер
        int offset = 0;

        for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
        {
            if (!stripDataManager.stripEnabled[stripIndex]) continue;

            if (stripDataManager.currentDataModes[stripIndex] == DataMode.Monochrome1Color &&
                stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
            {
                Debug.LogWarning("[SPItouchPanel] Monochrome1Color не поддерживает SpeedSynthMode.");
                continue;
            }

            byte[] dataString = dataSender.GenerateDataString(stripIndex, stripDataManager, effectsManager, colorProcessor, stateManager.CurrentState);
            if (dataString != null && dataString.Length > 0)
            {
                if (offset + dataString.Length > fullData.Length)
                {
                    // Увеличиваем буфер, если не хватает места
                    Array.Resize(ref fullData, fullData.Length * 2);
                }
                Array.Copy(dataString, 0, fullData, offset, dataString.Length);
                offset += dataString.Length;
            }
        }

        if (offset > 0)
        {
            byte[] finalData = new byte[offset];
            Array.Copy(fullData, finalData, offset);
            dataSender.EnqueueData(finalData);
        }
    }

    private void HandleSwipeDetected(SwipeDetector.SwipeData swipeData)
    {
        if (stateManager.CurrentState != StateManager.AppState.Active) return;

        for (int stripIndex = 0; stripIndex < stripDataManager.totalLEDsPerStrip.Count; stripIndex++)
        {
            if (stripDataManager.currentDisplayModes[stripIndex] == DisplayMode.SpeedSynthMode)
            {
                // Определяем начальный сегмент с учетом touchPanelOffset
                int startSegment = stripDataManager.touchPanelOffset;
                int totalSegments = stripDataManager.GetTotalSegments(stripIndex);

                // Ограничиваем количество панелей, чтобы не выйти за пределы ленты
                int panelsCount = Mathf.Min(swipeData.panelsCount, totalSegments - startSegment);

                // Подсвечиваем сегменты, участвующие в свайпе
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

                // Обновляем время последнего свайпа и направление
                lastSwipeTime[stripIndex] = Time.time;
                lastSwipeDirection[stripIndex] = swipeData.direction.x > 0 ? MoveDirection.Forward : MoveDirection.Backward;

                // Создаем комету из всех подсвеченных сегментов
                if (activeSegments[stripIndex].Count > 0)
                {
                    int minSegment = activeSegments[stripIndex].Min();
                    float dynamicLedCount = Mathf.Max(1, Mathf.RoundToInt(panelsCount * effectsManager.speedLedCountFactor)); // Размер кометы зависит от длины свайпа
                    float dynamicBrightness = Mathf.Clamp01(stripDataManager.GetStripBrightness(stripIndex) + swipeData.speed * effectsManager.speedBrightnessFactor);
                    // Убрано isCircular, так как круговое движение теперь стандартное
                    effectsManager.AddComet(stripIndex, minSegment * stripDataManager.ledsPerSegment, stripDataManager.GetSynthColorForStrip(stripIndex), dynamicLedCount, dynamicBrightness);
                    effectsManager.moveDirection = lastSwipeDirection[stripIndex];
                }

                dataChanged = true;
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
                    Color32 blackColor = new Color32(0, 0, 0, 255);
                    Color32 synthColor = stripDataManager.GetSynthColorForStrip(stripIndex);

                    if (isPressed)
                    {
                        if (effectsManager.toggleTouchMode && !currentColor.Equals(blackColor))
                        {
                            stripDataManager.SetSegmentColor(stripIndex, segmentIndex, blackColor, debugMode);
                            if (activeSegments.ContainsKey(stripIndex))
                            {
                                activeSegments[stripIndex].Remove(segmentIndex);
                            }
                        }
                        else if (currentColor.Equals(blackColor))
                        {
                            stripDataManager.SetSegmentColor(stripIndex, segmentIndex, synthColor, debugMode);
                            if (!activeSegments.ContainsKey(stripIndex))
                            {
                                activeSegments[stripIndex] = new HashSet<int>();
                            }
                            activeSegments[stripIndex].Add(segmentIndex);
                        }

                        // Обновляем время последнего касания
                        effectsManager.UpdateLastTouchTime(stripIndex);
                    }

                    dataChanged = true;
                }
            }
        }
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

    public void SetSunMode(int stripIndex, SunMode mode)
    {
        stripDataManager.SetSunMode(stripIndex, mode, debugMode);
        dataChanged = true;
    }
}