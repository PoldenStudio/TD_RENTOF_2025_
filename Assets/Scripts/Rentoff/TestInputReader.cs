using System;
using UnityEngine;
using UnityEngine.UI;

public class TestInputReader : InputReader
{
    [System.Serializable]
    public struct TestData
    {
        public bool[] panelStates;
        public float sliderValue;
    }

    public TestData[] testDataArray;
    public Slider testSlider; // UI Slider
    public Button applyButton;  // UI Button

    private int _currentTestDataIndex = 0;


    public override bool IsConnected()
    {
        return true; 
    }

    protected override void ReadInput()
    {

    }

    private void Start()
    {
        SampleRate = Settings.Instance.sampleRate;

        if (testSlider != null)
        {
            testSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        if (applyButton != null)
        {
            applyButton.onClick.AddListener(ApplyTestData);
        }

        // Initialize panelStates arrays in testDataArray
        if (testDataArray != null)
        {
            for (int i = 0; i < testDataArray.Length; i++)
            {
                testDataArray[i].panelStates = new bool[Settings.Instance.rows * Settings.Instance.cols];
            }
        }
    }

    private void OnSliderValueChanged(float value)
    {
        if (testDataArray.Length > 0)
        {
            testDataArray[_currentTestDataIndex].sliderValue = value;
        }
    }

    public void ApplyTestData()
    {
        if (testDataArray.Length > 0)
        {
            OnInputReceived(testDataArray[_currentTestDataIndex].panelStates);


            _currentTestDataIndex = (_currentTestDataIndex + 1) % testDataArray.Length;
        }
    }


    public void SetPanelState(int index, bool state)
    {
        if (testDataArray.Length > 0 && index < testDataArray[_currentTestDataIndex].panelStates.Length)
        {
            testDataArray[_currentTestDataIndex].panelStates[index] = state;
        }
    }
}