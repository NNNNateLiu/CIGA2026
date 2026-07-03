using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UniversalWaterSystem
{
    public partial class Water : MonoBehaviour
    {
        [SerializeField]
        Transform viewer;
        [SerializeField]
        Material waterMaterial;
        [SerializeField]
        bool updateMaterialProperties;
        //[SerializeField]
        //bool showMaterialLods;

        [SerializeField]
        float lengthScale = 100;
        [SerializeField, Range(1, 200)]
        int vertexDensity = 30;
        [SerializeField, Range(0, 8)]
        int clipLevels = 8;
        [SerializeField, Range(0, 100)]
        float skirtSize = 50;

        //sss
        [SerializeField, Range(0, 1)]
        float sssStrength = 1;
        [SerializeField, Range(0.01f, 2.0f)]
        float sssScale = 0.35f;
        [SerializeField, Range(0, 2.0f)]
        float sssBase = 2f;

        //underwater
        [SerializeField]
        Color underwaterColor = Color.green;
        [SerializeField]
        Color underwaterSSS = Color.black;

        //depth
        [SerializeField, Range(1, 500)]
        float visibility = 10;

        //foam
        [SerializeField, Range(0, 30)]
        float foamStrength = 1;
        [SerializeField, Range(0, 10)]
        float contactFoamStrength = 1;

        //reflection
        [SerializeField, Range(0, 1)]
        float reflectionStrength = 1f;

        //fade
        [SerializeField, Range(2000, 10000)]
        float normalFadeFar = 3000;
        [SerializeField, Range(200, 10000)]
        float foamFadeFar = 6000;

        [SerializeField, Range(0.01f, 5)]
        float geomteryScale = 1;

        public WaterResources waterResources;

        public bool underwaterEnable = true;

        List<Element> rings = new List<Element>();
        List<Element> trims = new List<Element>();
        Element center;
        Element skirt;
        Quaternion[] trimRotations;
        int previousVertexDensity;
        float previousSkirtSize;

        Material[] materials;
        public Transform geoRoot { get; private set; }

        public void SetViewer(Transform t)
        {
            if (t)
                viewer = t;
        }

        public Transform GetViewer()
        {
            return viewer;
        }

        private void InitGeometry()
        {
            if (viewer == null)
                viewer = Camera.main.transform;

            Shader.SetGlobalTexture("_Displacement_c0", cascade0.Displacement);
            Shader.SetGlobalTexture("_Derivatives_c0", cascade0.Derivatives);
            Shader.SetGlobalTexture("_Turbulence_c0", cascade0.Turbulence);

            Shader.SetGlobalTexture("_Displacement_c1", cascade1.Displacement);
            Shader.SetGlobalTexture("_Derivatives_c1", cascade1.Derivatives);
            Shader.SetGlobalTexture("_Turbulence_c1", cascade1.Turbulence);

            Shader.SetGlobalTexture("_Displacement_c2", cascade2.Displacement);
            Shader.SetGlobalTexture("_Derivatives_c2", cascade2.Derivatives);
            Shader.SetGlobalTexture("_Turbulence_c2", cascade2.Turbulence);

            UpdateVariables();


            materials = new Material[3];
            materials[0] = new Material(waterMaterial);
            materials[0].EnableKeyword("CLOSE");

            materials[1] = new Material(waterMaterial);
            materials[1].EnableKeyword("MID");
            materials[1].DisableKeyword("CLOSE");

            materials[2] = new Material(waterMaterial);
            materials[2].DisableKeyword("MID");
            materials[2].DisableKeyword("CLOSE");

            trimRotations = new Quaternion[]
            {
            Quaternion.AngleAxis(180, Vector3.up),
            Quaternion.AngleAxis(90, Vector3.up),
            Quaternion.AngleAxis(270, Vector3.up),
            Quaternion.identity,
            };

            InstantiateMeshes();
        }

        private void InitResources()
        {
            if (waterResources != null)
            {
                Shader.SetGlobalTexture("_FoamAlbedo", waterResources.foamTexture);
                Shader.SetGlobalTexture("_ContactFoamTexture", waterResources.contactFoamTexture);
                Shader.SetGlobalTexture("_FoamBubble", waterResources.bubbleTexture);
                Shader.SetGlobalTexture("_CausticsTexture", waterResources.causticsTexture);
            }
        }

        private void UpdateGeometry()
        {
            if (rings.Count != clipLevels || trims.Count != clipLevels
                || previousVertexDensity != vertexDensity || !Mathf.Approximately(previousSkirtSize, skirtSize))
            {
                InstantiateMeshes();
                previousVertexDensity = vertexDensity;
                previousSkirtSize = skirtSize;
            }

            UpdatePositions();
            UpdateMaterials();
        }

        void UpdateMaterials()
        {
            if (updateMaterialProperties)
            {
                for (int i = 0; i < 3; i++)
                {
                    materials[i].CopyPropertiesFromMaterial(waterMaterial);
                }
                materials[0].EnableKeyword("CLOSE");
                materials[1].EnableKeyword("MID");
                materials[1].DisableKeyword("CLOSE");
                materials[2].DisableKeyword("MID");
                materials[2].DisableKeyword("CLOSE");
            }

            int activeLevels = ActiveLodlevels();
            center.MeshRenderer.material = GetMaterial(clipLevels - activeLevels - 1);

            for (int i = 0; i < rings.Count; i++)
            {
                rings[i].MeshRenderer.material = GetMaterial(clipLevels - activeLevels + i);
                trims[i].MeshRenderer.material = GetMaterial(clipLevels - activeLevels + i);
            }

            //Shader.SetGlobalFloat("_SSSStrength", sssStrength);
            //Shader.SetGlobalFloat("_SSSScale", sssScale);
            //Shader.SetGlobalFloat("_SSSBase", sssBase);

            //Shader.SetGlobalFloat("_MaxDepth", visibility);
            //Shader.SetGlobalFloat("_FoamScale", foamStrength);
            //Shader.SetGlobalFloat("_ContactFoam", contactFoamStrength);
            //Shader.SetGlobalFloat("_ReflectionStrength", reflectionStrength);

            UpdateVariables();
        }

        void UpdateVariables()
        {
            Shader.SetGlobalFloat("_GeometryScale", geomteryScale);

            Shader.SetGlobalFloat("_SSSStrength", sssStrength);
            Shader.SetGlobalFloat("_SSSScale", sssScale);
            Shader.SetGlobalFloat("_SSSBase", sssBase);

            Shader.SetGlobalFloat("_MaxDepth", visibility);
            float timeFactor = Mathf.Clamp01((waterTime - 1.0f) / 1.5f);
            //Debug.Log("timeFactor = " + timeFactor.ToString());
            Shader.SetGlobalFloat("_FoamScale", foamStrength * timeFactor);
            Shader.SetGlobalFloat("_ContactFoam", contactFoamStrength);

            Shader.SetGlobalFloat("_ReflectionStrength", reflectionStrength);

            Shader.SetGlobalFloat("_NormalFadeFar", normalFadeFar);
            Shader.SetGlobalFloat("_FoamFadeFar", foamFadeFar);

            //underwater
            Shader.SetGlobalColor("Water_UnderBaseColor", underwaterColor);
            Shader.SetGlobalColor("Water_UnderSSSColor", underwaterSSS);
        }

        Material GetMaterial(int lodLevel)
        {
            if (lodLevel - 2 <= 0)
                return materials[0];

            if (lodLevel - 2 <= 2)
                return materials[1];

            return materials[2];
        }

        void UpdatePositions()
        {
            int k = GridSize();
            int activeLevels = ActiveLodlevels();

            float scale = ClipLevelScale(-1, activeLevels);
            Vector3 previousSnappedPosition = Snap(viewer.position, scale * 2);
            center.Transform.position = previousSnappedPosition + OffsetFromCenter(-1, activeLevels);
            center.Transform.localScale = new Vector3(scale, 1, scale);

            for (int i = 0; i < clipLevels; i++)
            {
                rings[i].Transform.gameObject.SetActive(i < activeLevels);
                trims[i].Transform.gameObject.SetActive(i < activeLevels);
                if (i >= activeLevels) continue;

                scale = ClipLevelScale(i, activeLevels);
                Vector3 centerOffset = OffsetFromCenter(i, activeLevels);
                Vector3 snappedPosition = Snap(viewer.position, scale * 2);

                Vector3 trimPosition = centerOffset + snappedPosition + scale * (k - 1) / 2 * new Vector3(1, 0, 1);
                int shiftX = previousSnappedPosition.x - snappedPosition.x < float.Epsilon ? 1 : 0;
                int shiftZ = previousSnappedPosition.z - snappedPosition.z < float.Epsilon ? 1 : 0;
                trimPosition += shiftX * (k + 1) * scale * Vector3.right;
                trimPosition += shiftZ * (k + 1) * scale * Vector3.forward;
                trims[i].Transform.position = trimPosition;
                trims[i].Transform.rotation = trimRotations[shiftX + 2 * shiftZ];
                trims[i].Transform.localScale = new Vector3(scale, 1, scale);

                rings[i].Transform.position = snappedPosition + centerOffset;
                rings[i].Transform.localScale = new Vector3(scale, 1, scale);
                previousSnappedPosition = snappedPosition;
            }

            scale = lengthScale * 2 * Mathf.Pow(2, clipLevels);
            skirt.Transform.position = new Vector3(-1, 0, -1) * scale * (skirtSize + 0.5f - 0.5f / GridSize()) + previousSnappedPosition;
            skirt.Transform.localScale = new Vector3(scale, 1, scale);
        }

        int ActiveLodlevels()
        {
            return clipLevels - Mathf.Clamp((int)Mathf.Log((1.7f * Mathf.Abs(viewer.position.y) + 1) / lengthScale, 2), 0, clipLevels);
        }

        float ClipLevelScale(int level, int activeLevels)
        {
            return lengthScale / GridSize() * Mathf.Pow(2, clipLevels - activeLevels + level + 1);
        }

        Vector3 OffsetFromCenter(int level, int activeLevels)
        {
            return (Mathf.Pow(2, clipLevels) + GeometricProgressionSum(2, 2, clipLevels - activeLevels + level + 1, clipLevels - 1))
                   * lengthScale / GridSize() * (GridSize() - 1) / 2 * new Vector3(-1, 0, -1);
        }

        float GeometricProgressionSum(float b0, float q, int n1, int n2)
        {
            return b0 / (1 - q) * (Mathf.Pow(q, n2) - Mathf.Pow(q, n1));
        }

        int GridSize()
        {
            return 4 * vertexDensity + 1;
        }

        Vector3 Snap(Vector3 coords, float scale)
        {
            if (coords.x >= 0)
                coords.x = Mathf.Floor(coords.x / scale) * scale;
            else
                coords.x = Mathf.Ceil((coords.x - scale + 1) / scale) * scale;

            if (coords.z < 0)
                coords.z = Mathf.Floor(coords.z / scale) * scale;
            else
                coords.z = Mathf.Ceil((coords.z - scale + 1) / scale) * scale;

            coords.y = 0;
            return coords;
        }

        void DestroyGO(Transform go)
        {
            go.parent = null;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                Object.Destroy(go.gameObject);
            }
            else
            {
                Object.DestroyImmediate(go.gameObject);
            }
#else
            Object.Destroy(go.gameObject);
#endif
        }

        void CleanMeshes()
        {
            geoRoot = transform.Find("Geometry Root");

            if (geoRoot == null)
            {
                return;
            }

            foreach (var child in geoRoot.gameObject.GetComponentsInChildren<Transform>())
            {
                if (child != geoRoot)
                {
                    DestroyGO(child);
                }
            }

            DestroyGO(geoRoot);
        }

        void InstantiateMeshes()
        {
            CleanMeshes();

            rings.Clear();
            trims.Clear();

            GameObject root = new GameObject("Geometry Root");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.parent = transform;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            geoRoot = root.transform;

            int k = GridSize();
            center = InstantiateElement("Center", CreatePlaneMesh(2 * k, 2 * k, 1, Seams.All), materials[materials.Length - 1]);
            Mesh ring = CreateRingMesh(k, 1);
            Mesh trim = CreateTrimMesh(k, 1);
            for (int i = 0; i < clipLevels; i++)
            {
                rings.Add(InstantiateElement("Ring " + i, ring, materials[materials.Length - 1]));
                trims.Add(InstantiateElement("Trim " + i, trim, materials[materials.Length - 1]));
            }
            skirt = InstantiateElement("Skirt", CreateSkirtMesh(k, skirtSize), materials[materials.Length - 1]);
        }

        Element InstantiateElement(string name, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject();
            go.name = name;
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(geoRoot);
            go.transform.localPosition = Vector3.zero;
            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = true;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            meshRenderer.material = mat;
            meshRenderer.allowOcclusionWhenDynamic = false;
            return new Element(go.transform, meshRenderer);
        }

        Mesh CreateSkirtMesh(int k, float outerBorderScale)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Clipmap skirt";
            CombineInstance[] combine = new CombineInstance[8];

            Mesh quad = CreatePlaneMesh(1, 1, 1);
            Mesh hStrip = CreatePlaneMesh(k, 1, 1);
            Mesh vStrip = CreatePlaneMesh(1, k, 1);


            Vector3 cornerQuadScale = new Vector3(outerBorderScale, 1, outerBorderScale);
            Vector3 midQuadScaleVert = new Vector3(1f / k, 1, outerBorderScale);
            Vector3 midQuadScaleHor = new Vector3(outerBorderScale, 1, 1f / k);

            combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, cornerQuadScale);
            combine[0].mesh = quad;

            combine[1].transform = Matrix4x4.TRS(Vector3.right * outerBorderScale, Quaternion.identity, midQuadScaleVert);
            combine[1].mesh = hStrip;

            combine[2].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
            combine[2].mesh = quad;

            combine[3].transform = Matrix4x4.TRS(Vector3.forward * outerBorderScale, Quaternion.identity, midQuadScaleHor);
            combine[3].mesh = vStrip;

            combine[4].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1)
                + Vector3.forward * outerBorderScale, Quaternion.identity, midQuadScaleHor);
            combine[4].mesh = vStrip;

            combine[5].transform = Matrix4x4.TRS(Vector3.forward * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
            combine[5].mesh = quad;

            combine[6].transform = Matrix4x4.TRS(Vector3.right * outerBorderScale
                + Vector3.forward * (outerBorderScale + 1), Quaternion.identity, midQuadScaleVert);
            combine[6].mesh = hStrip;

            combine[7].transform = Matrix4x4.TRS(Vector3.right * (outerBorderScale + 1)
                + Vector3.forward * (outerBorderScale + 1), Quaternion.identity, cornerQuadScale);
            combine[7].mesh = quad;
            mesh.CombineMeshes(combine, true);
            return mesh;
        }

        Mesh CreateTrimMesh(int k, float lengthScale)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Clipmap trim";
            CombineInstance[] combine = new CombineInstance[2];

            combine[0].mesh = CreatePlaneMesh(k + 1, 1, lengthScale, Seams.None, 1);
            combine[0].transform = Matrix4x4.TRS(new Vector3(-k - 1, 0, -1) * lengthScale, Quaternion.identity, Vector3.one);

            combine[1].mesh = CreatePlaneMesh(1, k, lengthScale, Seams.None, 1);
            combine[1].transform = Matrix4x4.TRS(new Vector3(-1, 0, -k - 1) * lengthScale, Quaternion.identity, Vector3.one);

            mesh.CombineMeshes(combine, true);
            return mesh;
        }

        Mesh CreateRingMesh(int k, float lengthScale)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Clipmap ring";
            if ((2 * k + 1) * (2 * k + 1) >= 256 * 256)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            CombineInstance[] combine = new CombineInstance[4];

            combine[0].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Bottom | Seams.Right | Seams.Left);
            combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            combine[1].mesh = CreatePlaneMesh(2 * k, (k - 1) / 2, lengthScale, Seams.Top | Seams.Right | Seams.Left);
            combine[1].transform = Matrix4x4.TRS(new Vector3(0, 0, k + 1 + (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

            combine[2].mesh = CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Left);
            combine[2].transform = Matrix4x4.TRS(new Vector3(0, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

            combine[3].mesh = CreatePlaneMesh((k - 1) / 2, k + 1, lengthScale, Seams.Right);
            combine[3].transform = Matrix4x4.TRS(new Vector3(k + 1 + (k - 1) / 2, 0, (k - 1) / 2) * lengthScale, Quaternion.identity, Vector3.one);

            mesh.CombineMeshes(combine, true);
            return mesh;
        }

        Mesh CreatePlaneMesh(int width, int height, float lengthScale, Seams seams = Seams.None, int trianglesShift = 0)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Clipmap plane";
            if ((width + 1) * (height + 1) >= 256 * 256)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            Vector3[] vertices = new Vector3[(width + 1) * (height + 1)];
            int[] triangles = new int[width * height * 2 * 3];
            Vector3[] normals = new Vector3[(width + 1) * (height + 1)];

            for (int i = 0; i < height + 1; i++)
            {
                for (int j = 0; j < width + 1; j++)
                {
                    int x = j;
                    int z = i;

                    if ((i == 0 && seams.HasFlag(Seams.Bottom)) || (i == height && seams.HasFlag(Seams.Top)))
                        x = x / 2 * 2;
                    if ((j == 0 && seams.HasFlag(Seams.Left)) || (j == width && seams.HasFlag(Seams.Right)))
                        z = z / 2 * 2;

                    vertices[j + i * (width + 1)] = new Vector3(x, 0, z) * lengthScale;
                    normals[j + i * (width + 1)] = Vector3.up;
                }
            }

            int tris = 0;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int k = j + i * (width + 1);
                    if ((i + j + trianglesShift) % 2 == 0)
                    {
                        triangles[tris++] = k;
                        triangles[tris++] = k + width + 1;
                        triangles[tris++] = k + width + 2;

                        triangles[tris++] = k;
                        triangles[tris++] = k + width + 2;
                        triangles[tris++] = k + 1;
                    }
                    else
                    {
                        triangles[tris++] = k;
                        triangles[tris++] = k + width + 1;
                        triangles[tris++] = k + 1;

                        triangles[tris++] = k + 1;
                        triangles[tris++] = k + width + 1;
                        triangles[tris++] = k + width + 2;
                    }
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            return mesh;
        }

        class Element
        {
            public Transform Transform;
            public MeshRenderer MeshRenderer;

            public Element(Transform transform, MeshRenderer meshRenderer)
            {
                Transform = transform;
                MeshRenderer = meshRenderer;
            }
        }


        [System.Flags]
        enum Seams
        {
            None = 0,
            Left = 1,
            Right = 2,
            Top = 4,
            Bottom = 8,
            All = Left | Right | Top | Bottom
        };
    }
}

