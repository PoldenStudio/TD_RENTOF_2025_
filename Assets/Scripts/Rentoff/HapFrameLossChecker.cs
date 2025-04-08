using UnityEngine;
using DemolitionStudios.DemolitionMedia;

public class HapFrameLossChecker : MonoBehaviour
{
    public Media media; // Ссылка на компонент Media

    private int expectedFrame = 0; // Ожидаемый номер кадра
    private int fixedUpdateCount = 0; // Счетчик FixedUpdate кадров
    private int frameLossCount = 0; // Счетчик потерянных кадров
    private int earlyDrops = 0; // Счетчик early drops
    private int lateDrops = 0; // Счетчик late drops

    void Start()
    {
        // Если media не назначен, попробуем получить его из текущего GameObject
        if (media == null)
        {
            media = GetComponent<Media>();
        }

        // Проверка, что media найден
        if (media == null)
        {
            Debug.LogError("Media компонент не найден! Убедитесь, что он назначен в Inspector или добавлен к GameObject.");
            enabled = false; // Отключаем скрипт, чтобы избежать ошибок
            return;
        }

        // Запускаем воспроизведение видео
        media.Play();

        // Инициализируем expectedFrame номером текущего кадра
        expectedFrame = media.VideoCurrentFrame;

        // Получаем начальное количество пропущенных кадров
        media.GetFramedropCount(out earlyDrops, out lateDrops);
    }

    void FixedUpdate()
    {
        fixedUpdateCount++;

        // Обновляем количество пропущенных кадров
        int newEarlyDrops, newLateDrops;
        media.GetFramedropCount(out newEarlyDrops, out newLateDrops);
        int droppedFrames = (newEarlyDrops + newLateDrops) - (earlyDrops + lateDrops);
        earlyDrops = newEarlyDrops;
        lateDrops = newLateDrops;

        // Проверяем, не потеряны ли кадры
        if (media.VideoCurrentFrame != expectedFrame)
        {
            // Вычисляем количество потерянных кадров
            int lostFrames = media.VideoCurrentFrame - expectedFrame;
            frameLossCount += lostFrames;

            // Учитываем пропущенные кадры
            lostFrames += droppedFrames;

            Debug.LogWarning("FixedUpdate #" + fixedUpdateCount + ": Потеряно " + lostFrames + " кадров! Ожидался кадр " + expectedFrame + ", получен кадр " + media.VideoCurrentFrame + ". Early Drops: " + earlyDrops + ", Late Drops: " + lateDrops);
        }

        // Обновляем ожидаемый номер кадра
        expectedFrame = media.VideoCurrentFrame + 1;

        // Выводим информацию о текущем кадре
        Debug.Log("FixedUpdate #" + fixedUpdateCount + ", Current Frame: " + media.VideoCurrentFrame + ". Early Drops: " + earlyDrops + ", Late Drops: " + lateDrops);
    }

    void OnDisable()
    {
        Debug.Log("HapFrameLossChecker отключен. Всего потеряно кадров: " + frameLossCount + ". Early Drops: " + earlyDrops + ", Late Drops: " + lateDrops);
    }
}