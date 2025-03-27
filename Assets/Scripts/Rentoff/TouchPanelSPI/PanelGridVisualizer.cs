using UnityEngine;
using System.Collections.Generic;

public class PanelGridVisualizer : MonoBehaviour
{
    public GameObject panelPrefab;
    public GameObject panelContainer;  
    public Color activeColor = Color.green;
    public bool testModeActive = true;


    private GameObject[] _panels;
    private bool[] _panelStates;

    private bool _isDragging = false;
    private List<int> _draggedPanels = new List<int>();

    public void Init(int rows, int cols)
    {
        _panels = new GameObject[rows * cols];
        _panelStates = new bool[rows * cols];
        CreateGrid(rows, cols);
    }

    private void CreateGrid(int rows, int cols)
    {


        // �������, ���� ��� ���� ������
        if (_panels != null)
        {
            foreach (GameObject panel in _panels)
            {
                Destroy(panel);
            }
        }

        RectTransform containerRect = panelContainer.GetComponent<RectTransform>();
        float containerWidth = containerRect.rect.width;
        float containerHeight = containerRect.rect.height;

        // ��������� ������� �������, ����� ��� ������ ��������� ���������
        float panelWidth = containerWidth / cols;
        float panelHeight = containerHeight / rows;
        float panelSize = Mathf.Min(panelWidth, panelHeight);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                GameObject panel = Instantiate(panelPrefab, panelContainer.transform);
                RectTransform panelRect = panel.GetComponent<RectTransform>();

                // ���������� ������
                float xPos = (col * panelSize) + (panelSize / 2);
                float yPos = ((rows - 1 - row) * panelSize) + (panelSize / 2); // ����������� row


                panelRect.sizeDelta = new Vector2(panelSize, panelSize);
                panelRect.anchoredPosition = new Vector2(xPos, yPos);

                int index = row * cols + col;
                _panels[index] = panel;
                _panelStates[index] = false; //����������� ���������
            }
        }
    }
    public void SetPanelState(int index, bool state)
    {
        if (index >= 0 && index < _panels.Length)
        {
            _panelStates[index] = state;
                UpdatePanelColor(index);
        }

    }

    private void UpdatePanelColor(int index)
    {

        if (_panels[index] != null)
        {
            UnityEngine.UI.Image image = _panels[index].GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {

                if (_panelStates[index])
                {
                    image.color = activeColor;
                    //Debug.Log("����� ����� � ������: " + _panelStates[index]);
                    //StartCoroutine(FadePanel(index, activeColor, Color.black, Settings.Instance.fadeDuration));
                }
                else
                {
                    image.color = Color.black;
                }
            }
        }
    }

    private System.Collections.IEnumerator FadePanel(int index, Color startColor, Color endColor, float duration)
    {
        float startTime = Time.time;
        UnityEngine.UI.Image image = _panels[index].GetComponent<UnityEngine.UI.Image>();
        while (Time.time - startTime < duration && _panelStates[index]) 
        {
            float t = (Time.time - startTime) / duration;
            image.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        if (_panelStates[index])
        {
            image.color = endColor;
        }

    }

    //--------- ��������� ���� ��� ��������� ������ --------
    private void Update()
    {
        if (!testModeActive) return;

        if (Input.GetMouseButtonDown(0)) // ����� ������ ���� ������
        {
            _isDragging = true;
            _draggedPanels.Clear();
            HandleMouseInput();
        }
        else if (Input.GetMouseButtonUp(0)) // ����� ������ ���� ��������
        {
            _isDragging = false;
            // _draggedPanels.Clear(); //�� �������, ����� ������� ���������
            if (_draggedPanels.Count > 0)
            {
                Debug.Log("Mouse Drag End, panels count " + _draggedPanels.Count);
                OnMouseDragEnded(); //��������, ��� ���� ��������
            }

        }

        if (_isDragging)
        {
            HandleMouseInput();
        }
    }

    private void HandleMouseInput()
    {
        
        Vector2 mousePos = Input.mousePosition;
        //�������������� � ��������� ����������
        RectTransform rectTransform = panelContainer.GetComponent<RectTransform>();

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, mousePos, null, out localPoint))
        {

            //���������� ������ ������
            float panelWidth = rectTransform.rect.width / Settings.Instance.cols;
            float panelHeight = rectTransform.rect.height / Settings.Instance.rows;

            int col = Mathf.FloorToInt(localPoint.x / panelWidth);
            int row = Mathf.FloorToInt(localPoint.y / panelHeight);
            //����������� row
            row = Settings.Instance.rows - 1 - row;

            if (col >= 0 && col < Settings.Instance.cols && row >= 0 && row < Settings.Instance.rows)
            {
                int index = row * Settings.Instance.cols + col;
                Debug.Log("Mouse over panel: " + index);

                SetPanelState(index, true);

                if (!_draggedPanels.Contains(index))  //���� ������ ��� �� ���� ������
                {
                    _draggedPanels.Add(index);
                    OnPanelTouched(index); //��������, ��� ������ ���������
                    Debug.Log("Panel Touched, index = " + index);
                }
            }
        }
    }

    //�������, ������� �������� ������ �����
    public event System.Action<int> PanelTouched; //��� �����, ��������
    public event System.Action MouseDragEnded;  //��� ����������� ������

    protected virtual void OnPanelTouched(int index)
    {
        PanelTouched?.Invoke(index);
    }

    protected virtual void OnMouseDragEnded()
    {
        MouseDragEnded?.Invoke();
    }

    public void SetTestMode(bool active)
    {
        testModeActive = active;
        //���������� ���� �������
        if (_panels != null)
        {
            for (int i = 0; i < _panels.Length; i++)
            {
                _panelStates[i] = false; //����������
                if (_panels[i] != null)
                {
                    UnityEngine.UI.Image image = _panels[i].GetComponent<UnityEngine.UI.Image>();
                    if (image != null)
                    {
                        image.color = Color.black;
                    }
                }
            }
        }
    }
}