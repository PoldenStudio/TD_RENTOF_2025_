using UnityEngine;
using System;

public class ScreenInput : MonoBehaviour, IInputSource
{
    public float swipeCooldown = 0.5f;
    public float swipeSpeedThreshold = 100f;

    private Vector2 lastSwipePosition;
    private float lastSwipeTime;

    public event Action<int> OnPanelPressed;  // Событие нажатия на панель
    public event Action<int> OnPanelReleased; // Событие отпускания панели
    public event Action<Vector2, float> OnSwipeDetected; // Событие свайпа
    public bool IsEnabled { get; set; } = true; // Включен по умолчанию

    void Update()
    {
        if (IsEnabled)
        {
            HandleMouseInput();
        }
    }

    void HandleMouseInput()
    {
        // Обработка нажатий мыши (симулируем нажатия на панели)
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                // Проверяем, попали ли мы в панель (например, по тегу "Panel")
                if (hit.collider.CompareTag("Panel"))
                {
                    // Получаем индекс панели (если это необходимо, можно передавать информацию из скрипта панели)
                    int panelIndex = GetPanelIndex(hit.collider.gameObject);
                    OnPanelPressed?.Invoke(panelIndex); // Вызываем событие нажатия
                }
            }
        }
        else if (Input.GetMouseButtonUp(0)) // Обработка отпускания мыши
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.CompareTag("Panel"))
                {
                    int panelIndex = GetPanelIndex(hit.collider.gameObject);
                    OnPanelReleased?.Invoke(panelIndex); // Вызываем событие отпускания
                }
            }
        }

        // Обработка свайпов мыши
        if (Input.GetMouseButtonDown(0))
        {
            lastSwipePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            Vector2 currentSwipePosition = Input.mousePosition;
            Vector2 swipeDirection = currentSwipePosition - lastSwipePosition;
            float swipeSpeed = swipeDirection.magnitude / Time.deltaTime;

            if (Time.time - lastSwipeTime > swipeCooldown && swipeSpeed > swipeSpeedThreshold)
            {
                OnSwipeDetected?.Invoke(swipeDirection.normalized, swipeSpeed);
                lastSwipeTime = Time.time;
            }
        }
    }

    // Вспомогательный метод для получения индекса панели (требуется настройка тегов и слоев в Unity)
    int GetPanelIndex(GameObject panel)
    {
        // TODO: Реализуйте логику получения индекса панели (например, по имени, по компоненту)
        // В данном примере возвращается 0, вам нужно заменить это на реальную логику
        return 0;
    }
}