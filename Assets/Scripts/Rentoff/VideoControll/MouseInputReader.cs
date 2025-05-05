using System;
using UnityEngine;
using static StateManager;

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

    private AppState dragInitiatedInState;

    protected override void ReadInput()
    {
        if (stateManager == null || (stateManager.CurrentState != AppState.Active && stateManager.CurrentState != AppState.Idle))
        {
            if (isMouseDragging)
            {
                isMouseDragging = false;
                isHoldChecking = false;
                isHolding = false;
                holdProcessed = false;
                if (enableDebug) Debug.Log($"[MouseInputReader] Drag cancelled due to state becoming neither Active nor Idle.");
            }
            return;
        }

        bool mouseActive = Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0);
        if (mouseActive)
        {
            stateManager.ResetInteractionTimers(); // Reset both idle and hibernation timers
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragInitiatedInState = stateManager.CurrentState;

            isMouseDragging = true;
            startDragPosition = Input.mousePosition;
            currentDragPosition = startDragPosition;
            lastSentPosition = startDragPosition;
            dragStartTime = Time.time;
            lastUpdateTime = Time.time;

            if (enableHoldDetection && stateManager.CurrentState == AppState.Active)
            {
                isHoldChecking = true;
                isHolding = false;
                holdProcessed = false;
                holdMovementDistance = 0f;
            }

            if (enableDebug)
                Debug.Log($"[MouseInputReader] Mouse drag started at position {startDragPosition} in state {dragInitiatedInState}");
        }
        else if (Input.GetMouseButtonUp(0) && isMouseDragging)
        {
            if (dragInitiatedInState == AppState.Idle && stateManager.CurrentState == AppState.Active)
            {
                if (enableDebug) Debug.Log($"[MouseInputReader] Mouse up in Active, but drag started in Idle. Ignoring swipe for video.");
                isMouseDragging = false;
                return;
            }

            Vector2 endPosition = Input.mousePosition;
            float duration = Mathf.Max(0.001f, Time.time - dragStartTime);
            float distance = Vector2.Distance(startDragPosition, endPosition) * distanceMultiplier;

            if (isHolding && enableHoldDetection && stateManager.CurrentState == AppState.Active)
            {
                swipeDetector?.ProcessMouseHold(startDragPosition, endPosition, duration, false);
                if (enableDebug)
                    Debug.Log($"[MouseInputReader] Mouse hold ended, duration: {duration:F2}s");
            }
            else
            {
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
            if (dragInitiatedInState == AppState.Idle)
            {
                currentDragPosition = Input.mousePosition;
                swipeDetector?.ProcessMouseDrag(currentDragPosition);
                if (enableDebug) Debug.Log($"[MouseInputReader] Drag ongoing in Idle state. Sending drag position: {currentDragPosition}");
            }
            else if (dragInitiatedInState == AppState.Active)
            {
                currentDragPosition = Input.mousePosition;

                if (isHoldChecking && enableHoldDetection && stateManager.CurrentState == AppState.Active && !holdProcessed)
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
    }

    private void SendSwipeEvent(Vector2 startPos, Vector2 endPos, float duration, bool isFinal)
    {
        Vector2 rawDirection = endPos - startPos;
        float distance = rawDirection.magnitude * distanceMultiplier;

        float xComp = rawDirection.x;
        float yComp = rawDirection.y;

        if (invertXDirection) xComp = -xComp;
        if (invertYDirection) yComp = -yComp;

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
        return true;
    }

    private void Update()
    {
        ReadInput();
    }
}