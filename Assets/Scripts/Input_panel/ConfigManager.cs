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
        // Пример: Включить только COM-порт, отключить экранный ввод
        comPortInput.IsEnabled = true;
        screenInput.IsEnabled = false;

        // Пример: Отключить визуализацию панелей
        panelVisualizer.IsEnabled = false;

        // Пример: Отключить взаимодействие с видео и звук
        mediaController.IsEnabled = false;
        audioFeedback.IsEnabled = false;

        // Обновляем InputManager, если это необходимо (например, если источники ввода добавляются/удаляются динамически)
        UpdateInputManager();
    }

    // Метод для обновления InputManager на основе текущих настроек
    void UpdateInputManager()
    {
        // TODO: Реализуйте логику для добавления/удаления источников ввода из InputManager
        // В зависимости от значений IsEnabled
    }
}