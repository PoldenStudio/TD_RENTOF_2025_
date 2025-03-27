using UnityEngine;
using System.Collections.Generic;

public class ConfigManager : MonoBehaviour
{
    public InputManager inputManager;
    public ComPortInput comPortInput;
    public ScreenInput screenInput;
    public PanelInteractionVisualizer panelVisualizer;
    public MediaController mediaController;
    public AudioFeedback audioFeedback;

    void Start()
    {
        // ������: �������� ������ COM-����, ��������� �������� ����
        comPortInput.IsEnabled = true;
        screenInput.IsEnabled = false;

        // ������: ��������� ������������ �������
        panelVisualizer.IsEnabled = false;

        // ������: ��������� �������������� � ����� � ����
        mediaController.IsEnabled = false;
        audioFeedback.IsEnabled = false;

        // ��������� InputManager, ���� ��� ���������� (��������, ���� ��������� ����� �����������/��������� �����������)
        UpdateInputManager();
    }

    // ����� ��� ���������� InputManager �� ������ ������� ��������
    void UpdateInputManager()
    {
        // TODO: ���������� ������ ��� ����������/�������� ���������� ����� �� InputManager
        // � ����������� �� �������� IsEnabled
    }
}