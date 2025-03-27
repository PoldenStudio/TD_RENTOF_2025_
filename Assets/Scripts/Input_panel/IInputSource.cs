using System;
using UnityEngine;

public interface IInputSource
{
    event Action<int> OnPanelPressed;
    event Action<int> OnPanelReleased;
    event Action<Vector2, float> OnSwipeDetected;
    bool IsEnabled { get; set; } // Свойство для включения/выключения источника ввода
}