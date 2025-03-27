using System.Collections;
using UnityEngine;

namespace InitializationFramework
{
    public class SoundInitialization : MonoBehaviour, IInitializable
    {
        [SerializeField]
        private AudioSource src;

        [SerializeField]
        private GlobalSettings gs;

        public IEnumerator Initialize(System.Action<Object> OnFinished)
        {
            gameObject.SetActive(true);

            // Устанавливаем уровень громкости, записанный ранее.
            AudioListener.volume = gs.generalSettings.lastSetVolumeLevel;

            // Выбираем аудиоклип в зависимости от активного режима.
            AudioClip tgClip = (gs.generalSettings.activeMode == GlobalSettings.GeneralSettings.WorkingModes.DefaultMode)
                ? gs.contentSettings.defaulModeOST
                : gs.contentSettings.spcModeOST;

            if (tgClip == null)
            {
                Debug.LogError("[SoundInitialization] Не назначен аудиоклип в GlobalSettings для текущего режима!");
            }
            else
            {
                Debug.Log("[SoundInitialization] Назначен аудиоклип: " + tgClip.name);
            }

            // Устанавливаем аудиоклип в AudioSource.
            src.clip = tgClip;
            src.loop = true; // Если необходимо зациклить воспроизведение

            // Дополнительная проверка: если AudioSource отключён или стоит mute, включаем его.
            if (!src.enabled)
            {
                src.enabled = true;
            }
            if (src.mute)
            {
                src.mute = false;
            }
            src.volume = 1.0f; // При необходимости можно регулировать громкость

            // Запускаем воспроизведение аудио.
            src.Play();

            // Выводим предупреждение, если аудио не начало играть.
            if (!src.isPlaying)
            {
                Debug.LogWarning("[SoundInitialization] AudioSource не начал воспроизведение! Проверьте настройки AudioSource и аудиоклип.");
            }
            else
            {
                Debug.Log("[SoundInitialization] AudioSource начал воспроизведение.");
            }

            yield return null;
            OnFinished?.Invoke(this);
        }

        public IEnumerator Deinitialize(System.Action<Object> OnFinished)
        {
            if (src.isPlaying)
                src.Stop();

            yield return null;
            gameObject.SetActive(false);
            OnFinished?.Invoke(this);
        }

        private void Reset()
        {
            gameObject.SetActive(false);
        }
    }
}