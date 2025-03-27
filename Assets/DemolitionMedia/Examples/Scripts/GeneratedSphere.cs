using UnityEngine;


namespace DemolitionStudios.DemolitionMedia.Examples
{
    // Based on ViveMediaDecoder's UVSphere
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GeneratedSphere : MonoBehaviour
    {
        public enum FrontFaceType
        {
            Inside,
            Outside
        }
        public enum TextureOriginType
        {
            BottomLeft,
            TopLeft
        }
        public int SubdivisionsLat = 32;
        public int SubdivisionsLon = 32;
        public float Radius = 1.0f;
        public FrontFaceType FrontFace = FrontFaceType.Outside;
        public TextureOriginType TextureOrigin = TextureOriginType.BottomLeft;

        void Awake()
        {
            MakeTriangles(SubdivisionsLat, SubdivisionsLon, Radius);
        }

        void MakeTriangles(int subdivisionsLat, int subdivisionsLon, float radius)
        {
            int numVertices = (subdivisionsLon + 1) * (subdivisionsLat + 2);
            int numMesh = (subdivisionsLon) * (subdivisionsLat + 1);
            int numTriangles = numMesh * 2;

            // 1. Compute vertices
            Vector3[] vertices = new Vector3[numVertices];
            int latIdxMax = subdivisionsLat + 1;    //  Latitude vertex number is latNum + 2, index numbers are from 0 ~ latNum + 1
            int longVertNum = subdivisionsLon + 1;  //  Longitude vertex number is longNum + 1, index numbers are from 0 ~ longNum
            float preComputeV = Mathf.PI / latIdxMax;
            float preComputeH = 2.0f * Mathf.PI / subdivisionsLon;
            for (int i = 0; i <= latIdxMax; i++)
            {
                float thetaV = i * preComputeV;     //  PI * i / latIdxMax;
                float sinV = Mathf.Sin(thetaV);
                float cosV = Mathf.Cos(thetaV);
                int lineStartIdx = i * longVertNum;
                for (int j = 0; j <= subdivisionsLon; j++)
                {
                    float thetaH = j * preComputeH; //  2PI * j / longNum;
                    vertices[lineStartIdx + j] = new Vector3(
                        Mathf.Cos(thetaH) * sinV,
                        cosV,
                        Mathf.Sin(thetaH) * sinV
                    ) * radius;
                }
            }

            // 2. Compute normals
            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                normals[i] = vertices[i].normalized;
                if (FrontFace == FrontFaceType.Inside)
                {
                    normals[i] *= -1.0f;
                }
            }

            // 3. Compute uvs
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i <= latIdxMax; i++)
            {
                int lineStartIdx = i * longVertNum;
                float vVal = (float)i / latIdxMax;
                if (TextureOrigin == TextureOriginType.BottomLeft)
                {
                    vVal = 1.0f - vVal;
                }
                for (int j = 0; j <= subdivisionsLon; j++)
                {
                    float uVal = (float)j / subdivisionsLon;
                    if (FrontFace == FrontFaceType.Inside)
                    {
                        uVal = 1.0f - uVal;
                    }
                    uvs[lineStartIdx + j] = new Vector2(uVal, vVal);
                }
            }

            // 4. Compute triangles
            int[] triangles = new int[numTriangles * 3];
            int index = 0;
            for (int i = 0; i <= subdivisionsLat; i++)
            {
                for (int j = 0; j < subdivisionsLon; j++)
                {
                    int curVertIdx = i * longVertNum + j;
                    int nextLineVertIdx = curVertIdx + longVertNum;

                    if (FrontFace == FrontFaceType.Outside)
                    {
                        triangles[index++] = curVertIdx;
                        triangles[index++] = curVertIdx + 1;
                        triangles[index++] = nextLineVertIdx + 1;
                        triangles[index++] = curVertIdx;
                        triangles[index++] = nextLineVertIdx + 1;
                        triangles[index++] = nextLineVertIdx;
                    }
                    else
                    {
                        triangles[index++] = curVertIdx;
                        triangles[index++] = nextLineVertIdx + 1;
                        triangles[index++] = curVertIdx + 1;
                        triangles[index++] = curVertIdx;
                        triangles[index++] = nextLineVertIdx;
                        triangles[index++] = nextLineVertIdx + 1;
                    }
                }
            }

            // 5. Assign to mesh
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            Mesh mesh = filter.mesh;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.material.mainTexture = Texture2D.blackTexture;
        }
    }
}