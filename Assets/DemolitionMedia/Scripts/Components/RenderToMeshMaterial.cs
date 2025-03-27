using UnityEngine;


namespace DemolitionStudios.DemolitionMedia
{
    [AddComponentMenu("Demolition Media/Render to mesh material")]
    public class RenderToMeshMaterial : MonoBehaviour
    {
        /// Source media component with video to map
        [SerializeField]
        private Media _sourceMedia;
        public Media SourceMedia
        {
            set { _sourceMedia = value; Update(); }
            get { return _sourceMedia; }
        }

        /// Target mesh renderer instance
        public MeshRenderer TargetMesh;

        /// Fallback texture
        public Texture FallbackTexture;

        /// Scale factor
        public Vector2 Scale = Vector2.one;

        /// Offset vecotr
        public Vector2 Offset = Vector2.zero;

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
            if (TargetMesh == null)
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

            Material[] materials = TargetMesh.materials;
            if (materials != null)
            {
                for (int i = 0; i < materials.Length; ++i)
                {
                    Material material = materials[i];
                    if (material != null)
                    {
                        material.mainTexture = texture;
                        material.mainTextureScale = scale;
                        material.mainTextureOffset = offset;
                    }
                }
            }
        }

        public virtual void OnEnable()
        {
            if (TargetMesh == null)
            {
                TargetMesh = GetComponent<MeshRenderer>();
                if (TargetMesh == null)
                {
                    Utilities.LogWarning("[DemolitionMedia] RenderToMeshMaterial: no MeshRenderer component set or found in " + this.name);
                }
            }

            Update();
        }

        public virtual void OnDisable()
        {
            Apply(FallbackTexture);
        }
    }
}