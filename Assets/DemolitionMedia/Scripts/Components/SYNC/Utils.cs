using UnityEngine;
using System;

namespace DemolitionStudios.DemolitionMedia
{
    class Utils
    {
        public static void HandleKeyboardVsyncAndGraphy()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                QualitySettings.vSyncCount = 1;
            }
            if (Input.GetKeyDown(KeyCode.D))
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 120;
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                ToggleGraphy();
            }
            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleBorderlessFullscreen();
            }
        }

        public static void CheckVSync(bool forceVSync)
        {

            if (forceVSync)
            {
                QualitySettings.vSyncCount = 1;
            }
            else
            {
                const int MaxSafeFrameRate = 240;
                if (QualitySettings.vSyncCount == 0 && Application.targetFrameRate < 0)
                {
                    Debug.LogError("QualitySettings.vSyncCount = 0, the playback sync is likely to be unstable");
                }
                else if (Application.targetFrameRate > MaxSafeFrameRate)
                {
                    Debug.LogError($"Application.targetFrameRate is {Application.targetFrameRate}, values higher {MaxSafeFrameRate} not recommended, playback sync may be unstable");
                }
            }
        }

        static void ToggleBorderlessFullscreen(int deltaX = 9, int deltaY = -9)
        {
#if UNITY_STANDALONE_WIN
            if (BorderlessWindow.framed)
            {
                BorderlessWindow.MaximizeWindow();
                BorderlessWindow.SetFramelessWindow();
                BorderlessWindow.MoveWindowPos(new Vector2Int(deltaX, deltaY),
                                               Screen.currentResolution.width,
                                               Screen.currentResolution.height);
            }       
            else
            {
                BorderlessWindow.SetFramedWindow();
                BorderlessWindow.MaximizeWindow();
            }
#endif // UNITY_STANDALONE_WIN
        }

        static void ToggleGraphy()
        {
            RenderToIMGUI render = Camera.main.GetComponent(typeof(RenderToIMGUI)) as RenderToIMGUI;
            if (render.OnTopCamera) // windows
                render.DrawOnTopCamera = !render.DrawOnTopCamera;
            else // linux
                ToggleVideoScale(render);
        }

        static void ToggleVideoScale(RenderToIMGUI render)
        {
            if (render.size.x < 0.9)
            {
                render.position = new Vector2(0f, 0f);
                render.size = new Vector2(1f, 1f);
            }
            else
            {
                render.position = new Vector2(0f, 0f);
                render.size = new Vector2(0.7f, 0.7f);
            }
        }
    }

    struct Optional<T>
    {
        private readonly bool hasValue;
        public bool HasValue { get { return hasValue; } }

        private readonly T value;
        public T Value
        {
            get
            {
                if (!hasValue)
                {
                    throw new InvalidOperationException();
                }
                return value;
            }
        }

        public Optional(T value, bool hasValue = true)
        {
            this.value = value;
            this.hasValue = hasValue;
        }

        public static Optional<T> Empty => new Optional<T>(default(T), false);

        public static implicit operator Optional<T>(T value)
        {
            return new Optional<T>(value);
        }
    }
}
