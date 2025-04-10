using System;
using UnityEngine;

public class MouseInputReader : InputReader
{
    [Header("Mouse Detection Settings")]
    [SerializeField] private float minSwipeDistance = 5.0f;
    [SerializeField] private float continuousUpdateInterval = 0.05f; // �������� ���������� ��� ��������������� ������
    [SerializeField] private bool enableDebug = false;
    [SerializeField] private bool sendContinuousUpdates = true; // ���������� ������������� ���������� �� ����� ������

    [Header("Swipe Force Settings")]
    [SerializeField][Range(0.1f, 10f)] private float speedMultiplier = 1.0f; // ��������� ��������
    [SerializeField][Range(0.1f, 10f)] private float distanceMultiplier = 1.0f; // ��������� ����������
    [SerializeField] private AnimationCurve responseVelocityCurve = AnimationCurve.Linear(0, 0, 1, 1); // ������ ������� ��������
    [SerializeField] private float speedCap = 10000f; // ����������� ������������ ��������

    [Header("Direction Settings")]
    [SerializeField][Range(0f, 1f)] private float horizontalBias = 0.5f; // 0 = ������ ������������, 1 = ������ ��������������
    [SerializeField] private bool invertXDirection = true; // ������������� �������������� �����������
    [SerializeField] private bool invertYDirection = false; // ������������� ������������ �����������

    [Header("References")]
    [SerializeField] private SwipeDetector swipeDetector;
    [SerializeField] private StateManager stateManager;

    private bool isMouseDragging = false;
    private Vector2 startDragPosition;
    private Vector2 currentDragPosition;
    private Vector2 lastSentPosition;
    private float dragStartTime;
    private float lastUpdateTime;

    protected override void ReadInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // ������ ��������������
            isMouseDragging = true;
            startDragPosition = Input.mousePosition;
            currentDragPosition = startDragPosition;
            lastSentPosition = startDragPosition;
            dragStartTime = Time.time;
            lastUpdateTime = Time.time;

            if (enableDebug)
                Debug.Log($"[MouseInputReader] Mouse drag started at position {startDragPosition}");
        }
        else if (Input.GetMouseButtonUp(0) && isMouseDragging)
        {
            // ���������� ��������������
            Vector2 endPosition = Input.mousePosition;
            float duration = Mathf.Max(0.001f, Time.time - dragStartTime); // ������������� ������� �� ����
            float distance = Vector2.Distance(startDragPosition, endPosition) * distanceMultiplier;

            if (swipeDetector != null && distance > minSwipeDistance)
            {
                SendSwipeEvent(startDragPosition, endPosition, duration, true);
            }

            isMouseDragging = false;
        }
        else if (isMouseDragging)
        {
            // ��������� ������� �������
            currentDragPosition = Input.mousePosition;

            // ���������, ����� �� ��������� ������������� ����������
            if (sendContinuousUpdates && Time.time - lastUpdateTime > continuousUpdateInterval)
            {
                float currentDistance = Vector2.Distance(lastSentPosition, currentDragPosition);
                if (currentDistance > minSwipeDistance)
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
        // ������������ ������� ��������� ������
        Vector2 rawDirection = endPos - startPos;
        float distance = rawDirection.magnitude * distanceMultiplier;

        // ��������� �������� ����������� (��������������/������������ ������������)
        float xComponent = rawDirection.x;
        float yComponent = rawDirection.y;

        // ��������� �������� ����������� ���� �����
        if (invertXDirection) xComponent = -xComponent;
        if (invertYDirection) yComponent = -yComponent;

        // ��������� ������������ �� ����
        xComponent *= horizontalBias * 2;
        yComponent *= (1 - horizontalBias) * 2;

        // ������� ����� ������ ����������� � ����������� ���
        Vector2 direction = new Vector2(xComponent, yComponent).normalized;

        // ������������ �������� � ������ ��������� � ���������� ������ �������
        float rawSpeed = distance / duration;
        float normalizedSpeed = Mathf.Clamp01(rawSpeed / speedCap); // ����������� ��� ������
        float curvedSpeed = responseVelocityCurve.Evaluate(normalizedSpeed) * speedCap;
        float finalSpeed = curvedSpeed * speedMultiplier;

        // ������� ������ � ������
        SwipeDetector.MouseSwipeData data = new SwipeDetector.MouseSwipeData
        {
            startPosition = startPos,
            endPosition = endPos,
            direction = direction,
            speed = finalSpeed,
            duration = duration,
            distance = distance
        };

        // ���������� ������� � SwipeDetector
        if (swipeDetector != null)
        {
            swipeDetector.ProcessMouseSwipe(startPos, endPos, duration, finalSpeed, direction, isFinal);
        }

        if (enableDebug)
        {
            Debug.Log($"[MouseInputReader] Mouse swipe: dir=({direction.x:F2}, {direction.y:F2}), " +
                     $"raw speed={rawSpeed:F0}, curved={finalSpeed:F0}, dist={distance:F0}, final={isFinal}");
        }
    }

    public override bool IsConnected()
    {
        // ���� ��������� ������ ������������
        return true;
    }

    private void Update()
    {
        ReadInput();
    }
}