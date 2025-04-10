using System;
using UnityEngine;

public class MouseInputReader : InputReader
{
    [Header("Mouse Detection Settings")]
    [SerializeField] private float minSwipeDistance = 5.0f;
    [SerializeField] private float continuousUpdateInterval = 0.05f; // Интервал обновления при продолжительном свайпе
    [SerializeField] private bool enableDebug = false;
    [SerializeField] private bool sendContinuousUpdates = true; // Отправлять промежуточные обновления во время свайпа

    [Header("Swipe Force Settings")]
    [SerializeField][Range(0.1f, 10f)] private float speedMultiplier = 1.0f; // Множитель скорости
    [SerializeField][Range(0.1f, 10f)] private float distanceMultiplier = 1.0f; // Множитель расстояния
    [SerializeField] private AnimationCurve responseVelocityCurve = AnimationCurve.Linear(0, 0, 1, 1); // Кривая отклика скорости
    [SerializeField] private float speedCap = 10000f; // Ограничение максимальной скорости

    [Header("Direction Settings")]
    [SerializeField][Range(0f, 1f)] private float horizontalBias = 0.5f; // 0 = только вертикальные, 1 = только горизонтальные
    [SerializeField] private bool invertXDirection = true; // Инвертировать горизонтальное направление
    [SerializeField] private bool invertYDirection = false; // Инвертировать вертикальное направление

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
            // Начало перетаскивания
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
            // Завершение перетаскивания
            Vector2 endPosition = Input.mousePosition;
            float duration = Mathf.Max(0.001f, Time.time - dragStartTime); // Предотвращаем деление на ноль
            float distance = Vector2.Distance(startDragPosition, endPosition) * distanceMultiplier;

            if (swipeDetector != null && distance > minSwipeDistance)
            {
                SendSwipeEvent(startDragPosition, endPosition, duration, true);
            }

            isMouseDragging = false;
        }
        else if (isMouseDragging)
        {
            // Обновляем текущую позицию
            currentDragPosition = Input.mousePosition;

            // Проверяем, нужно ли отправить промежуточное обновление
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
        // Рассчитываем базовые параметры свайпа
        Vector2 rawDirection = endPos - startPos;
        float distance = rawDirection.magnitude * distanceMultiplier;

        // Применяем смещение направления (горизонтальное/вертикальное предпочтение)
        float xComponent = rawDirection.x;
        float yComponent = rawDirection.y;

        // Применяем инверсию направлений если нужно
        if (invertXDirection) xComponent = -xComponent;
        if (invertYDirection) yComponent = -yComponent;

        // Применяем предпочтение по осям
        xComponent *= horizontalBias * 2;
        yComponent *= (1 - horizontalBias) * 2;

        // Создаем новый вектор направления и нормализуем его
        Vector2 direction = new Vector2(xComponent, yComponent).normalized;

        // Рассчитываем скорость с учетом множителя и нелинейной кривой отклика
        float rawSpeed = distance / duration;
        float normalizedSpeed = Mathf.Clamp01(rawSpeed / speedCap); // Нормализуем для кривой
        float curvedSpeed = responseVelocityCurve.Evaluate(normalizedSpeed) * speedCap;
        float finalSpeed = curvedSpeed * speedMultiplier;

        // Создаем данные о свайпе
        SwipeDetector.MouseSwipeData data = new SwipeDetector.MouseSwipeData
        {
            startPosition = startPos,
            endPosition = endPos,
            direction = direction,
            speed = finalSpeed,
            duration = duration,
            distance = distance
        };

        // Отправляем событие в SwipeDetector
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
        // Мышь считается всегда подключенной
        return true;
    }

    private void Update()
    {
        ReadInput();
    }
}