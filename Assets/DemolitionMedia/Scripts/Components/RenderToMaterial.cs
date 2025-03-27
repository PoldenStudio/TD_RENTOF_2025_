using UnityEngine;


namespace DemolitionStudios.DemolitionMedia
{
    [AddComponentMenu("Demolition Media/Render to material")]
    public class RenderToMaterial : MonoBehaviour
    {
        /// Source media component with video to map
        public Media SourceMedia;
        /// Target material instance
        public Material TargetMaterial;
        /// Target texture name inside the target material
        public string TargetTextureName;
        /// Fallback texture
        public Texture FallbackTexture;
        /// Scale factor
        public Vector2 Scale = Vector2.one;
        /// Offset vecotr
        public Vector2 Offset = Vector2.zero;
        /// Old texture
        private Texture Texture;

        /// Flip
        public bool FlipX = false;
        public bool FlipY = false;

        public virtual void Update()
        {
			if (SourceMedia == null || SourceMedia.VideoRenderTexture == null)
            {
                Apply(FallbackTexture);
                return;
            }

            Apply(SourceMedia.VideoRenderTexture);
        }

        private void Apply(Texture texture)
        {
			if (TargetMaterial == null)
                return;

            if (texture == null)
                texture = Texture2D.blackTexture;

            Vector2 scale = Scale;
            Vector2 offset = Offset;
            if (!FlipX)
            {
                scale.Scale(new Vector2(-1.0f, 1.0f));
                offset.x += 1.0f;
            }
            if (FlipY)
            {
                scale.Scale(new Vector2(1.0f, -1.0f));
                offset.y += 1.0f;
            }

            if (string.IsNullOrEmpty(TargetTextureName))
            {
                //Utilities.Log("Setting main texture");
                TargetMaterial.mainTexture = texture;
                TargetMaterial.mainTextureScale = scale;
                TargetMaterial.mainTextureOffset = offset;
            }
            else
            {
                //Utilities.Log("Setting texture" + TargetTextureName);
                TargetMaterial.SetTexture(TargetTextureName, texture);
                TargetMaterial.SetTextureScale(TargetTextureName, scale);
                TargetMaterial.SetTextureOffset(TargetTextureName, offset);
            }
        }

        public virtual void OnEnable()
        {
            if (TargetMaterial != null)
            {
                Scale = TargetMaterial.mainTextureScale;
                Offset = TargetMaterial.mainTextureOffset;
                Texture = TargetMaterial.mainTexture;
            }
            Update();
        }

        public virtual void OnDisable()
        {
            Apply(FallbackTexture);
            if (TargetMaterial != null)
            {
                TargetMaterial.mainTextureScale = Scale;
                TargetMaterial.mainTextureOffset = Offset;
                TargetMaterial.mainTexture = Texture;
            }
        }
    }
}