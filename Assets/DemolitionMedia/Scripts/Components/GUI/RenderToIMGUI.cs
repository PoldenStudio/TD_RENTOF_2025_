using UnityEngine;


namespace DemolitionStudios.DemolitionMedia
{
    [AddComponentMenu("Demolition Media/Render to IMGUI")]
    public class RenderToIMGUI : MonoBehaviour
    {
        /// Source media component
        public Media sourceMedia;

        /// IMGUI color
        public Color color = Color.white;
        /// Whether to use the IMGUI alpha blending
        public bool alphaBlend = false;
        /// IMGUI scale mode
        public ScaleMode scaleMode = ScaleMode.ScaleToFit;
        /// IMGUI depth
        public int depth = 0;
        /// Whether to draw in fullscreen mode
        public bool fullScreen = true;
        /// Video rectangle position
        public Vector2 position = Vector2.zero;
        /// Video rectangle size
        public Vector2 size = Vector2.one;
        /// Flip
        public bool FlipX = false;
        public bool FlipY = false;
        /// This camera will be rendered on top of GUI if set (use for drawing uGUI)
        public Camera OnTopCamera;
        /// Whether to draw the on top camera
        public bool DrawOnTopCamera = false;

        public void OnGUI()
        {
            if (sourceMedia == null || sourceMedia.VideoRenderTexture == null)
                return;

            GUI.depth = depth;
            GUI.color = color;

            Rect drawRect = GetDrawRect();

            var scale = new Vector2(FlipX ? -1.0f :  1.0f,
                                    FlipY ?  1.0f : -1.0f);
            GUIUtility.ScaleAroundPivot(scale, drawRect.center);

            GUI.DrawTexture(drawRect, sourceMedia.VideoRenderTexture, scaleMode, alphaBlend);

            if (DrawOnTopCamera && OnTopCamera)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    OnTopCamera.Render();
                }
            }
        }

        public Rect GetDrawRect()
        {
            if (fullScreen)
            {
                return new Rect(0.0f, 0.0f, Screen.width, Screen.height);
            }

            return new Rect(position.x * (Screen.width - 1),
                            position.y * (Screen.height - 1),
                            size.x * Screen.width,
                            size.y * Screen.height);
        }
    }
}