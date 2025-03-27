using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


namespace DemolitionStudios.DemolitionMedia
{
    [AddComponentMenu("Demolition Media/Render to uGUI")]
    public class RenderToUGUI : UnityEngine.UI.MaskableGraphic
    {
        /// Source media component
        public Media sourceMedia;
        /// Target uv rectangle
        public Rect _UVRect = new Rect(0f, 0f, 1f, 1f);
        /// Flip
        public bool FlipX = false;
        public bool FlipY = false;
        /// Whether to preserve aspect ratio of the video
        public bool keepAspectRatio = true;
        /// Whether to keep the original size for the video texture (1:1 scaling)
        public bool setNativeSize = false;

        /// Last known video texture width
        private int _lastWidth;
        /// Last known video texture height
        private int _lastHeight;
        /// Last known video texture
        private Texture _prevTexture;

        /// Video texture
        public override Texture mainTexture
        {
            get
            {
                if (!TextureReady())
                {
#if UNITY_EDITOR
                    //return Resources.Load<Texture2D>("DemolitionMediaIcon");
#endif
                    //return Texture2D.whiteTexture;
                    return null;
                }

                return sourceMedia.VideoRenderTexture;
            }
        }

        /// Whether the video texture is ready
        public bool TextureReady()
        {
            return sourceMedia != null && sourceMedia.VideoRenderTexture != null;
        }

        /// Using LateUpdate here allows making changes to the texture in Update
        void LateUpdate()
        {
            if (setNativeSize)
                SetNativeSize();

            if (_prevTexture != mainTexture)
            {
                _prevTexture = mainTexture;
                SetVerticesDirty();
            }

            if (TextureReady() && mainTexture != null)
            {
                if (mainTexture.width != _lastWidth || mainTexture.height != _lastHeight)
                {
                    _lastWidth = mainTexture.width;
                    _lastHeight = mainTexture.height;
                    SetVerticesDirty();
                }
            }

            SetMaterialDirty();
        }

        /// Source media instance
        public Media SourceMedia
        {
            get
            {
                return sourceMedia;
            }
            set
            {
                if (sourceMedia != value)
                {
                    sourceMedia = value;
                    SetMaterialDirty();
                }
            }
        }

        /// UV rectangle used by the video texture
        public Rect UVRect
        {
            get
            {
                return _UVRect;
            }
            set
            {
                if (_UVRect == value)
                {
                    return;
                }
                _UVRect = value;
                SetVerticesDirty();
            }
        }

        /// Make the scale of the Graphic 1:1 scale (pixel to pixel)
        [ContextMenu("Set Native Size")]
        public override void SetNativeSize()
        {
            Texture tex = mainTexture;
            if (tex != null)
            {
                int w = Mathf.RoundToInt(tex.width * UVRect.width);
                int h = Mathf.RoundToInt(tex.height * UVRect.height);

                rectTransform.anchorMax = rectTransform.anchorMin;
                rectTransform.sizeDelta = new Vector2(w, h);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
			if (sourceMedia == null)
				return;
			
            // Get the indices
            List<int> indices = new List<int>(new int[] { 0, 1, 2, 2, 3, 0 });

            // Get the vertices
            List<UIVertex> vertices = new List<UIVertex>();
            FillVerticesList(vertices);

            // Feed the data to VertexHelper
            vh.Clear();
            vh.AddUIVertexStream(vertices, indices);
        }

        private void FillVerticesList(List<UIVertex> vertices)
        {
            // Get the drawing dimensions
            Vector4 v = GetDrawingDimensions(keepAspectRatio);

            // Fill the vertices list
            Func<Vector2, Vector2, bool> addVertex = (pos, uv) =>
            {
                var vert = UIVertex.simpleVert;
                vert.position = new Vector2(pos.x, pos.y);
                vert.uv0 = new Vector2(uv.x, 1.0f - uv.y);
                if (FlipX)
                    vert.uv0.x = 1.0f - vert.uv0.x;
                if (FlipY)
                    vert.uv0.y = 1.0f - vert.uv0.y;

                vert.color = color;
                vertices.Add(vert);
                return true;
            };

            addVertex(new Vector2(v.x, v.y), new Vector2(UVRect.xMin, UVRect.yMin));
            addVertex(new Vector2(v.x, v.w), new Vector2(UVRect.xMin, UVRect.yMax));
            addVertex(new Vector2(v.z, v.w), new Vector2(UVRect.xMax, UVRect.yMax));
            addVertex(new Vector2(v.z, v.y), new Vector2(UVRect.xMax, UVRect.yMin));
        }

        private Vector4 GetDrawingDimensions(bool needKeepAspectRatio)
        {
            Vector4 returnSize = Vector4.zero;

            if (mainTexture != null)
            {
                var padding = Vector4.zero;
                var textureSize = new Vector2(mainTexture.width, mainTexture.height);

                Rect r = GetPixelAdjustedRect();
                // Utilities.Log(string.Format("r:{2}, textureSize:{0}, padding:{1}", textureSize, padding, r));

                int spriteW = Mathf.RoundToInt(textureSize.x);
                int spriteH = Mathf.RoundToInt(textureSize.y);

                var size = new Vector4(padding.x / spriteW,
                                       padding.y / spriteH,
                                       (spriteW - padding.z) / spriteW,
                                       (spriteH - padding.w) / spriteH);

                if (needKeepAspectRatio && textureSize.sqrMagnitude > 0.0f)
                {
                    var spriteRatio = textureSize.x / textureSize.y;
                    var rectRatio = r.width / r.height;

                    if (spriteRatio > rectRatio)
                    {
                        var oldHeight = r.height;
                        r.height = r.width * (1.0f / spriteRatio);
                        r.y += (oldHeight - r.height) * rectTransform.pivot.y;
                    }
                    else
                    {
                        var oldWidth = r.width;
                        r.width = r.height * spriteRatio;
                        r.x += (oldWidth - r.width) * rectTransform.pivot.x;
                    }
                }

                returnSize = new Vector4(r.x + r.width  * size.x,
                                         r.y + r.height * size.y,
                                         r.x + r.width  * size.z,
                                         r.y + r.height * size.w);

            }

            return returnSize;
        }
    }
}