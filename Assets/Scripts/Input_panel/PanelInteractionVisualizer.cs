using UnityEngine;

public class PanelInteractionVisualizer : MonoBehaviour, IPanelVisualizer
{
    public InputManager inputManager; //  ������ �������� ���� �� InputManager
    public GameObject panelPrefab;
    public Color pressedColor = Color.green;
    public Color releasedColor = Color.black;
    public float panelSpacing = 1.0f;

    private GameObject[] panels;
    public bool IsEnabled { get; set; } = true; // ������� �� ���������

    void Start()
    {
        if (inputManager == null)
        {
            Debug.LogError("PanelInteractionVisualizer: InputManager not assigned!");
            return;
        }
        if (IsEnabled)
        {
            CreatePanels();
            inputManager.OnPanelPressed += OnPanelPressed;
            inputManager.OnPanelReleased += OnPanelReleased;
        }
    }

    void CreatePanels()
    {
        int numberOfPanels = 20; // ���������� �������
        panels = new GameObject[numberOfPanels];

        // ������� ������
        for (int i = 0; i < numberOfPanels; i++)
        {
            panels[i] = Instantiate(panelPrefab, transform);
            panels[i].transform.localPosition = new Vector3(i * panelSpacing, 0, 0); // ��������� ������ � ���
            Renderer renderer = panels[i].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = releasedColor; // ���������� ���� ���������� ������
            }
            // ��������� ��� "Panel" ��� ��������� ����� � ������
            panels[i].tag = "Panel";
        }
    }

    void OnPanelPressed(int panelIndex)
    {
        VisualizePanelPressed(panelIndex);
    }

    void OnPanelReleased(int panelIndex)
    {
        VisualizePanelReleased(panelIndex);
    }

    public void VisualizePanelPressed(int panelIndex)
    {
        if (panelIndex >= 0 && panelIndex < panels.Length)
        {
            Renderer renderer = panels[panelIndex].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = pressedColor;
            }
        }
    }

    public void VisualizePanelReleased(int panelIndex)
    {
        if (panelIndex >= 0 && panelIndex < panels.Length)
        {
            Renderer renderer = panels[panelIndex].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = releasedColor;
            }
        }
    }

    void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnPanelPressed -= OnPanelPressed;
            inputManager.OnPanelReleased -= OnPanelReleased;
        }
    }
}