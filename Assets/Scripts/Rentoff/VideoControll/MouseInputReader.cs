using System;
using UnityEngine;

public class MouseInputReader : InputReader
{
    [Header("Mouse Detection Settings")]
    [SerializeField] private float minSwipeDistance = 5.0f;
    [SerializeField] private float continuousUpdateInterval = 0.05f;
    [SerializeField] private bool enableDebug = false;
    [SerializeField] private bool sendContinuousUpdates = true;

    [Header("Hold Detection Settings")]
    [SerializeField] private float holdThresholdTime = 0.5f;
    [SerializeField] private float maxHoldMovement = 5.0f;
    [SerializeField] private bool enableHoldDetection = true;

    [Header("Swipe Force Settings")]
    [SerializeField][Range(0.1f, 10f)] private float speedMultiplier = 1.0f;
    [SerializeField][Range(0.1f, 10f)] private float distanceMultiplier = 1.0f;
    [SerializeField] private AnimationCurve responseVelocityCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] private float speedCap = 10000f;

    [Header("Direction Settings")]
    [SerializeField][Range(0f, 1f)] private float horizontalBias = 0.5f;
    [SerializeField] private bool invertXDirection = true;
    [SerializeField] private bool invertYDirection = false;

    [Header("References")]
    [SerializeField] private SwipeDetector swipeDetector;
    [SerializeField] private StateManager stateManager;

    private bool isMouseDragging = false;
    private Vector2 startDragPosition;
    private Vector2 currentDragPosition;
    private Vector2 lastSentPosition;
    private float dragStartTime;
    private float lastUpdateTime;

    private bool isHoldChecking = false;
    private bool isHolding = false;
    private bool holdProcessed = false;
    private float holdMovementDistance = 0f;

    protected override void ReadInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isMouseDragging = true;
            startDragPosition = Input.mousePosition;
            currentDragPosition = startDragPosition;
            lastSentPosition = startDragPosition;
            dragStartTime = Time.time;
            lastUpdateTime = Time.time;

            if (enableHoldDetection)
            {
                isHoldChecking = true;
                isHolding = false;
                holdProcessed = false;
                holdMovementDistance = 0f;
            }

            if (enableDebug)
                Debug.Log($"[MouseInputReader] Mouse drag started at position {startDragPosition}");
        }
        else if (Input.GetMouseButtonUp(0) && isMouseDragging)
        {
            Vector2 endPosition = Input.mousePosition;
            float duration = Mathf.Max(0.001f, Time.time - dragStartTime);
            float distance = Vector2.Distance(startDragPosition, endPosition) * distanceMultiplier;

            // Если удерживаем - завершаем hold
            if (isHolding && enableHoldDetection)
            {
                swipeDetector?.ProcessMouseHold(startDragPosition, endPosition, duration, false);

                if (enableDebug)
                    Debug.Log($"[MouseInputReader] Mouse hold ended, duration: {duration:F2}s");
            }
            else
            {
                // Финальный свайп - тоже отправляем
                if (distance > minSwipeDistance)
                {
                    SendSwipeEvent(startDragPosition, endPosition, duration, true);
                }
            }

            isMouseDragging = false;
            isHoldChecking = false;
            isHolding = false;
            holdProcessed = false;
        }
        else if (isMouseDragging)
        {
            currentDragPosition = Input.mousePosition;

            // Проверка на удержание (hold)
            if (isHoldChecking && enableHoldDetection && !holdProcessed)
            {
                float currentHoldDistance = Vector2.Distance(startDragPosition, currentDragPosition);
                holdMovementDistance = Mathf.Max(holdMovementDistance, currentHoldDistance);

                if (holdMovementDistance > maxHoldMovement)
                {
                    isHoldChecking = false;
                    isHolding = false;

                    if (enableDebug)
                        Debug.Log($"[MouseInputReader] Hold check canceled - mouse moved too much: {holdMovementDistance:F1} px");
                }
                else if (Time.time - dragStartTime >= holdThresholdTime)
                {
                    isHolding = true;

                    if (!holdProcessed)
                    {
                        swipeDetector?.ProcessMouseHold(startDragPosition, currentDragPosition, Time.time - dragStartTime, true);
                        holdProcessed = true;

                        if (enableDebug)
                            Debug.Log($"[MouseInputReader] Hold detected at {currentDragPosition}, movement: {holdMovementDistance:F1} px");
                    }
                }
            }

            // Промежуточные свайпы (continuous updates)
            if (!isHolding && sendContinuousUpdates && Time.time - lastUpdateTime > continuousUpdateInterval)
            {
                float movedSinceLast = Vector2.Distance(lastSentPosition, currentDragPosition);

                if (movedSinceLast > minSwipeDistance)
                {
                    float partialDuration = Mathf.Max(0.001f, Time.time - lastUpdateTime);
                    SendSwipeEvent(lastSentPosition, currentDragPosition, partialDuration, false);
                    lastSentPosition = currentDragPosition;
                    lastUpdateTime = Time.time;
                }
            }
        }
    }

    private void SendSwipeEvent(Vector2 startPos, Vector2 endPos, float duration, bool isFinal)
    {
        Vector2 rawDirection = endPos - startPos;
        float distance = rawDirection.magnitude * distanceMultiplier;

        float xComp = rawDirection.x;
        float yComp = rawDirection.y;

        if (invertXDirection) xComp = -xComp;
        if (invertYDirection) yComp = -yComp;

        // Смесь горизонтального и вертикального
        xComp *= horizontalBias * 2;
        yComp *= (1 - horizontalBias) * 2;

        Vector2 direction = new Vector2(xComp, yComp).normalized;

        float rawSpeed = distance / duration;
        float normalizedSpeed = Mathf.Clamp01(rawSpeed / speedCap);
        float curvedSpeed = responseVelocityCurve.Evaluate(normalizedSpeed) * speedCap;
        float finalSpeed = curvedSpeed * speedMultiplier;

        if (swipeDetector != null)
        {
            swipeDetector.ProcessMouseSwipe(startPos, endPos, duration, finalSpeed, direction, isFinal);
        }

        if (enableDebug)
        {
            Debug.Log($"[MouseInputReader] Mouse swipe sent -> dir=({direction.x:F2}, {direction.y:F2}), " +
                      $"rawSpeed={rawSpeed:F0}, curved={finalSpeed:F0}, dist={distance:F1}, final={isFinal}");
        }
    }

    public override bool IsConnected()
    {
        // Мышь "подключена" всегда
        return true;
    }

    private void Update()
    {
        ReadInput();
    }
}