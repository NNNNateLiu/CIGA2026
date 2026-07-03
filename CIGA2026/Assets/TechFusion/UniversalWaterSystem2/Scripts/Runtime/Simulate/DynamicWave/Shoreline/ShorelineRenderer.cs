using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace UniversalWaterSystem
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteAlways]
    //The shoreline renderer should be in "shoreline space", which means the localPosition should be zero and the localRotation should be identity.
    public class ShorelineRenderer : MonoBehaviour
    {
        const int VARIATION_COUNT = 8;
      
        public List<Vector3> curvePoints = new List<Vector3>(); // points in the curve
        //public List<float> curveLinearPos = new List<float>(); //front linear pos[0,1]

        public float waveFrontPosition = 0; //0 - 1
        public float waveValue = 0;
        public float waveLinearPos = 0;
        
        public float variationID = 0;
        public Vector2 waveRange = new Vector2(0, 1);
        public ShorelineWaveGenerator shorelineGen;

        private float endPos = 1;
        private int curvePointCount = 0;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ComputeBuffer curveBuffer; // todo release
        private Material materialInstance;

        //variables
        private Vector4 WaveVariable = Vector4.zero;
        private Vector4 VariationVariable = Vector4.zero;
        private Vector4 FoamSpaceVariable = Vector4.zero;
        private Vector4 FoamTimeVariable = Vector4.zero;

        private bool avalible = false;

        public bool NeedReset()
        {
            return waveFrontPosition <= 0;
        }

        public void Init(Vector2 range, float waveFront, float end)
        {
            waveRange = range;
            variationID = Mathf.Floor(Random.Range(0, VARIATION_COUNT));
            waveFrontPosition = waveFront;
            endPos = end;
        }

        public void SetCurvePoints(List<Vector3> points)
        {
            curvePoints = points;

            UpdateMesh(points.Count);
        }

        public void SetLinearPos(float linearPos)
        {
            waveLinearPos = Mathf.Clamp01(linearPos);
            waveValue = shorelineGen.GetWaveValue(waveLinearPos);

            //variation wave front
            float postWaveFrontPos = 1.0f - Mathf.Lerp(0, endPos, waveLinearPos);

            shorelineGen.CalculateWaveFrontPoints(postWaveFrontPos, waveRange);
            
            List<Vector3> waveFrontPoints = shorelineGen.GetWaveFrontPoints();

            SetCurvePoints(waveFrontPoints);

            //curveLinearPos = shorelineGen.GetWaveFrontLinearPoints();
        }

        private void Start()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.enabled = false;

            materialInstance = new Material(shorelineGen.material);
            meshRenderer.material = materialInstance;

            avalible = true;
        }

        private void OnDestroy()
        {
            ReleaseMeshData();
        }

        public void SubmitDraws(CommandBuffer cmd)
        {
            if (!avalible) return;

            //update mesh
            materialInstance.SetBuffer(GlobalShaderVariables.Simulation.ShorelineCurvePoints, curveBuffer);
            materialInstance.SetInt(GlobalShaderVariables.Simulation.ShorelineCurvePointCount, curvePoints.Count);
            materialInstance.SetFloat(GlobalShaderVariables.Simulation.ShorelineWidth, shorelineGen.shorelineWidth);
            materialInstance.SetFloat(GlobalShaderVariables.Simulation.ShorelineWaveFade, shorelineGen.displacementWaveFade.Evaluate(waveLinearPos));

            //new Vector4(waveValue, shorelineGen.shorelineAmplitude, shorelineGen.foamStrength, 0)
            WaveVariable.x = waveValue;
            WaveVariable.y = shorelineGen.shorelineAmplitude;
            WaveVariable.z = shorelineGen.foamStrength;
            materialInstance.SetVector(GlobalShaderVariables.Simulation.ShorelineWaveVariable, WaveVariable);

            //new Vector4(variationID, shorelineGen.variationUVStrength, shorelineGen.variationStrength, 0)
            VariationVariable.x = variationID;
            VariationVariable.y = shorelineGen.variationUVStrength;
            VariationVariable.z = shorelineGen.variationStrength;
            materialInstance.SetVector(GlobalShaderVariables.Simulation.ShorelineVariationVariable, VariationVariable);

            //new Vector4(shorelineGen.spaceCenter, shorelineGen.spaceFactor, shorelineGen.spaceExp, 0)
            FoamSpaceVariable.x = shorelineGen.spaceCenter;
            FoamSpaceVariable.y = shorelineGen.spaceFactor;
            FoamSpaceVariable.z = shorelineGen.spaceExp;
            materialInstance.SetVector(GlobalShaderVariables.Simulation.ShorelineFoamSpaceVariable, FoamSpaceVariable);

            //new Vector4(shorelineGen.timeCenter, shorelineGen.timeFactor, shorelineGen.timeExp, 0)
            FoamTimeVariable.x = shorelineGen.timeCenter;
            FoamTimeVariable.y = shorelineGen.timeFactor;
            FoamTimeVariable.z = shorelineGen.timeExp;
            materialInstance.SetVector(GlobalShaderVariables.Simulation.ShorelineFoamTimeVariable, FoamTimeVariable);

            cmd.DrawRenderer(meshRenderer, materialInstance);
        }

        void CreateMesh(int pointCount)
        {
            if (pointCount == curvePointCount) return;
            
            ReleaseMeshData();
            curvePointCount = pointCount;

            mesh = new Mesh();
            mesh.name = "Shoreline Mesh";

            int numberOfSegments = curvePointCount - 1;
            int vertexCount = numberOfSegments * 2 + 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[numberOfSegments * 6];



            for (int i = 0; i < curvePointCount; i++)
            {
                float t = (float)i / (curvePointCount - 1);
                
                vertices[i * 2] = Vector3.zero;
                vertices[i * 2 + 1] = Vector3.zero;
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;

                //tangent xyz, fadealpha w
                float fadeEdge = shorelineGen.displacementEdgeFade.Evaluate(t);
                float fadeWave = 1;// shorelineGen.displacementWaveFade.Evaluate(waveLinearPos);
                float fadeAlpha = fadeEdge * fadeWave;
                tangents[i * 2] = new Vector4(0, 0, 0, fadeAlpha);
                tangents[i * 2 + 1] = new Vector4(0, 0, 0, fadeAlpha);

                uv[i * 2] = new Vector2(1, (float)i / numberOfSegments);
                uv[i * 2 + 1] = new Vector2(0, (float)i / numberOfSegments);
            }
            
            for (int i = 0; i < numberOfSegments; i++)
            {
                int baseIndex = i * 2;
                triangles[i * 6] = baseIndex;
                triangles[i * 6 + 1] = baseIndex + 2;
                triangles[i * 6 + 2] = baseIndex + 1;
                triangles[i * 6 + 3] = baseIndex + 2;
                triangles[i * 6 + 4] = baseIndex + 3;
                triangles[i * 6 + 5] = baseIndex + 1;
            }
            
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetIndices(triangles, MeshTopology.Triangles, 0);
            mesh.UploadMeshData(true);
            
            meshFilter.mesh = mesh;

            curveBuffer = new ComputeBuffer(curvePointCount, sizeof(float) * 3);
        }

        void ReleaseMeshData()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }

            if (curveBuffer != null)
            {
                curveBuffer.Release();
            }
        }

        void UpdateMeshCurve()
        {
            curveBuffer.SetData<Vector3>(curvePoints);
        }

        void UpdateMesh(int pointCount)
        {
            CreateMesh(pointCount);
            UpdateMeshCurve();
        }
    }
}
