using UnityEngine;
using System;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public List<MonoBehaviour> inputSources; // ������ ���������� ����� (ComPortInput, ScreenInput)
    private List<IInputSource> activeInputSources = new List<IInputSource>();

    public event Action<int> OnPanelPressed;
    public event Action<int> OnPanelReleased;
    public event Action<Vector2, float> OnSwipeDetected;

    void Start()
    {
        // ��������� ��� ��������� �����
        foreach (var source in inputSources)
        {
            IInputSource inputSource = source as IInputSource;
            if (inputSource != null)
            {
                // ������������� �� ������� ��������� �����, ������ ���� �� �������
                if (inputSource.IsEnabled)
                {
                    AddInputSource(inputSource);
                }
            }
            else
            {
                Debug.LogWarning("InputManager: Invalid input source in the list.");
            }
        }
    }

    // ����� ��� ���������� ��������� ����� � �������� �� ��� �������
    public void AddInputSource(IInputSource inputSource)
    {
        activeInputSources.Add(inputSource);
        inputSource.OnPanelPressed += OnPanelPressedHandler;
        inputSource.OnPanelReleased += OnPanelReleasedHandler;
        inputSource.OnSwipeDetected += OnSwipeDetectedHandler;
    }

    // ����� ��� �������� ��������� ����� � ������� �� ��� �������
    public void RemoveInputSource(IInputSource inputSource)
    {
        activeInputSources.Remove(inputSource);
        inputSource.OnPanelPressed -= OnPanelPressedHandler;
        inputSource.OnPanelReleased -= OnPanelReleasedHandler;
        inputSource.OnSwipeDetected -= OnSwipeDetectedHandler;
    }

    // ����������� ������� (�������������� ������� �� ��������� �����)
    void OnPanelPressedHandler(int panelIndex)
    {
        OnPanelPressed?.Invoke(panelIndex);
    }

    void OnPanelReleasedHandler(int panelIndex)
    {
        OnPanelReleased?.Invoke(panelIndex);
    }

    void OnSwipeDetectedHandler(Vector2 swipeDirection, float swipeSpeed)
    {
        OnSwipeDetected?.Invoke(swipeDirection, swipeSpeed);
    }
}