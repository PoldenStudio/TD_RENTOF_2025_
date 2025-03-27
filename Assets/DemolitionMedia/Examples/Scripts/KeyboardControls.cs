using UnityEngine;


namespace DemolitionStudios.DemolitionMedia.Examples
{
    [RequireComponent(typeof(Media))]
    public class KeyboardControls : MonoBehaviour
    {

        void Update()
        {
            var media = GetComponent<Media>();
            // Handle keyboard input
            var currentTime = media.CurrentTime;
            var step = 1.0f;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                media.TogglePause();
            }
            else if (Input.GetKeyDown(KeyCode.M))
            {
                media.SetMuted(!media.Muted);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                media.StepForward();
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                media.StepBackward();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                // Seek backward
                print("LeftArrow");
                media.SeekToTime(currentTime - step);
                print("Before: " + currentTime);
                currentTime = media.CurrentTime;
                print("After:  " + currentTime);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                // Seek forward
                print("RightArrow");
                media.SeekToTime(currentTime + step);
                print("Before: " + currentTime);
                currentTime = media.CurrentTime;
                print("After:  " + currentTime);
            }
        }
    }
}
