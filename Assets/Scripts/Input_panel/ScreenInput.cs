using UnityEngine;
using System;

public class ScreenInput : MonoBehaviour, IInputSource
{
    public float swipeCooldown = 0.5f;
    public float swipeSpeedThreshold = 100f;

    private Vector2 lastSwipePosition;
    private float lastSwipeTime;

    public event Action<int> OnPanelPressed;  // ������� ������� �� ������
    public event Action<int> OnPanelReleased; // ������� ���������� ������
    public event Action<Vector2, float> OnSwipeDetected; // ������� ������
    public bool IsEnabled { get; set; } = true; // ������� �� ���������

    void Update()
    {
        if (IsEnabled)
        {
            HandleMouseInput();
        }
    }

    void HandleMouseInput()
    {
        // ��������� ������� ���� (���������� ������� �� ������)
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                // ���������, ������ �� �� � ������ (��������, �� ���� "Panel")
                if (hit.collider.CompareTag("Panel"))
                {
                    // �������� ������ ������ (���� ��� ����������, ����� ���������� ���������� �� ������� ������)
                    int panelIndex = GetPanelIndex(hit.collider.gameObject);
                    OnPanelPressed?.Invoke(panelIndex); // �������� ������� �������
                }
            }
        }
        else if (Input.GetMouseButtonUp(0)) // ��������� ���������� ����
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.CompareTag("Panel"))
                {
                    int panelIndex = GetPanelIndex(hit.collider.gameObject);
                    OnPanelReleased?.Invoke(panelIndex); // �������� ������� ����������
                }
            }
        }

        // ��������� ������� ����
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

    // ��������������� ����� ��� ��������� ������� ������ (��������� ��������� ����� � ����� � Unity)
    int GetPanelIndex(GameObject panel)
    {
        // TODO: ���������� ������ ��������� ������� ������ (��������, �� �����, �� ����������)
        // � ������ ������� ������������ 0, ��� ����� �������� ��� �� �������� ������
        return 0;
    }
}