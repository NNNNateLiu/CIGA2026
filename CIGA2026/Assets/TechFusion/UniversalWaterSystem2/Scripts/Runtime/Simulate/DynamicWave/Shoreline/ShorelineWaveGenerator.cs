using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace UniversalWaterSystem
{
    //[ExecuteAlways]
    public class ShorelineWaveGenerator : MonoBehaviour
    {
        public uint guidePointCount = 3;

        public uint guideLineCount = 3;

        [Range(1, 200)]
        public uint segments = 20;

        //todo, use shader
        public Material material;

        //shoreline instance params
        public float initWidth = 20;

        public float initLength = 20;

        //---------------Wave--------------------
        [Range(0.0f, 1.0f)]
        public float shorelineStartValue = 0.8f;

        [Range(0.0f, 1.0f)]
        public float shorelineMinRandomEnd = 1.0f;

        [Range(0.1f, 20)]
        public float shorelineGenerateInterval = 1.0f;

        [Range(0.1f, 4)]
        public float shorelineGenerateIntervalVariation = 1.0f;

        [Range(0.1f, 160)]
        public float shorelineWidth = 5.0f;

        [Range(0.1f, 60)]
        public float shorelineAmplitude = 2.5f;

        [Range(0.1f, 0.5f)]
        public float shorelineDistortion = 0.2f;

        [Range(0, 0.5f)]
        public float speed = 0.03f;

        //[Range(0, 5f)]
        //public float phase = 1f;

        [Range(0, 5f)]
        public float variationUVStrength = 0.3f;

        [Range(0, 10f)]
        public float variationStrength = 4;

        //--------------foam----------------------
        [Range(0, 10f)]
        public float foamStrength = 6;

        [Range(0, 1.0f)]
        public float spaceCenter = 0.638f;
        [Range(1.0f, 10)]
        public float spaceFactor = 1.56f;
        [Range(0, 10.0f)]
        public float spaceExp = 10.0f;

        [Range(0, 1.0f)]
        public float timeCenter = 0.606f;
        [Range(1.0f, 10)]
        public float timeFactor = 1;
        [Range(0, 10.0f)]
        public float timeExp = 10.0f;

        //control params
        public Vector2[] waveRanges;
        public AnimationCurve displacementEdgeFade; //horizon
        public AnimationCurve displacementWaveFade; //verticle
        public AnimationCurve waveFrontSpeedMulti;
        public AnimationCurve waveValue;
        public AnimationCurve waveFrontVariationStart;
        public AnimationCurve waveFrontVariationEnd;

        //vfx foam
        public bool showFoamsVFX = false;
        public ShorelineFoams shorelineFoamsPrefab;
        ShorelineFoams shorelineFoams;

        private List<Vector3> waveFrontPositions = new List<Vector3>();
        //[0, 1]
        //private List<float> waveFrontLinearPositions = new List<float>();
        private Vector3[] waveFrontGuidePositions = new Vector3[1];
        private SplineCurve waveFrontCurve = null;


        [SerializeField, HideInInspector]
        public List<ShorelineGuideLine> guidelines = new List<ShorelineGuideLine>();

        //[SerializeField, HideInInspector]
        private List<ShorelineRenderer> shorelineRenderers = new List<ShorelineRenderer>();

        private float shorelineGenereteProcess = 0;
        private List<ShorelineRenderer> shorelineRenderersDeactive = new List<ShorelineRenderer>();

        private void Start()
        {
            Water.Instance.RegisterShorelineGenerator(this);

            if (shorelineFoamsPrefab != null)
            {
                shorelineFoams = Instantiate(shorelineFoamsPrefab) as ShorelineFoams;
                shorelineFoams.waveGenerator = this;
                shorelineFoams.transform.SetParent(transform);
                shorelineFoams.transform.localPosition = Vector3.zero;
                shorelineFoams.transform.localRotation = Quaternion.identity;
            }
        }

        private void OnDestroy()
        {
            Water.Instance.UnRegisterShorelineGenerator(this);
        }

        public void CreateGuideLines()
        {
            ClearAllShorelineRenderers();
            ClearGuideLines();

            waveFrontGuidePositions = new Vector3[guideLineCount];

            for (int i = 0; i < guideLineCount; i++)
            {
                ShorelineGuideLine guideline = new ShorelineGuideLine();
                float xBegin = -initWidth * 0.5f;
                float x = xBegin + (initWidth / (guideLineCount - 1)) * i;
                guideline.CreateGuidePoints(guidePointCount, transform, x, initLength);
                guidelines.Add(guideline);

                waveFrontGuidePositions[i] = Vector3.zero;
            }
        }

        void ClearGuideLines()
        {
            for (int i = 0; i < guidelines.Count; i++)
            {
                guidelines[i].ClearGuidePoints();
            }
            guidelines.Clear();
        }

        float RemapFromO1(float min, float max, float t)
        {
            return t * (max - min) + min;
        }

        Vector2 RandomRange()
        {
            if (waveRanges.Length > 0)
            {
                int randomNum = Random.Range(0, waveRanges.Length - 1);
                return waveRanges[randomNum];
            }

            return new Vector2(0, 1);
        }

        public float GetWaveValue(float waveValueFactor)
        {
            return waveValue.Evaluate(waveValueFactor);
        }

        //Calculate the spline curve points in world space for the shoreline renderer.
        public void CalculateWaveFrontPoints(float wfPos, Vector2 range)
        {
            for (int i = 0; i < guideLineCount; i++)
            {
                ShorelineGuideLine guideline = guidelines[i];
                Vector3 pos = guideline.GetInterpLocalPosition(wfPos);

                waveFrontGuidePositions[i] = pos;
            }

            UpdateWaveFrontPositions(range);
        }

        //call after CalculateWaveFrontPoints
        public List<Vector3> GetWaveFrontPoints()
        {
            return waveFrontPositions;
        }

        ////call after CalculateWaveFrontPoints
        //public List<float> GetWaveFrontLinearPoints()
        //{
        //    return waveFrontLinearPositions;
        //}

        public void ClearAllShorelineRenderers()
        {
            for (int i = 0; i < shorelineRenderers.Count; i++)
            {
                ShorelineRenderer sr = shorelineRenderers[i];

#if UNITY_EDITOR
                GameObject.DestroyImmediate(sr.gameObject);
#else
                GameObject.Destroy(sr.gameObject);
#endif
            }

            shorelineRenderers.Clear();

            for (int i = 0; i < shorelineRenderersDeactive.Count; i++)
            {
                ShorelineRenderer sr = shorelineRenderersDeactive[i];

#if UNITY_EDITOR
                GameObject.DestroyImmediate(sr.gameObject);
#else
                GameObject.Destroy(sr.gameObject);
#endif
            }

            shorelineRenderersDeactive.Clear();
        }

        public void CreateShorelineRenderer()
        {
            ShorelineRenderer sr = null;

            if (shorelineRenderersDeactive.Count > 0)
            {
                sr = shorelineRenderersDeactive[shorelineRenderersDeactive.Count - 1];
                shorelineRenderersDeactive.RemoveAt(shorelineRenderersDeactive.Count - 1);
            }

            if (sr == null)
            {
                GameObject shorelineRendererObj = new GameObject("Shoreline Renderer");
                shorelineRendererObj.transform.SetParent(transform);
                shorelineRendererObj.transform.localPosition = Vector3.zero;
                shorelineRendererObj.transform.localRotation = Quaternion.identity;
                
                sr = shorelineRendererObj.AddComponent<ShorelineRenderer>();
                sr.shorelineGen = this;
            }

            sr.Init(RandomRange(), shorelineStartValue, Random.Range(shorelineMinRandomEnd, 1.0f));
            shorelineRenderers.Add(sr);
        }

        public void SubmitDraws(CommandBuffer cmd)
        {
            for (int i = 0; i < shorelineRenderers.Count; i++)
            {
                ShorelineRenderer sr = shorelineRenderers[i];
                sr.SubmitDraws(cmd);
            }
        }

        void UpdateShorelineObjects()
        {
            shorelineGenereteProcess += Time.deltaTime;

            while (shorelineGenereteProcess > shorelineGenerateInterval)
            {
                shorelineGenereteProcess -= shorelineGenerateInterval + shorelineGenerateIntervalVariation * Random.Range(0, 1.0f);
                CreateShorelineRenderer();
            }
            
            for (int i = 0; i < shorelineRenderers.Count; i++)
            {
                ShorelineRenderer sr = shorelineRenderers[i];

                if (sr.NeedReset())
                {
                    shorelineRenderers.Remove(sr);
                    shorelineRenderersDeactive.Add(sr);
                    i--;
                }
            }
        }

        void UpdateWaveFrontPositions(Vector2 range)
        {
            if (waveFrontPositions.Count != segments)
            {
                waveFrontPositions.Clear();

                for (int i = 0; i < segments; i++)
                {
                    waveFrontPositions.Add(Vector3.zero);
                }

                waveFrontCurve = new SplineCurve(waveFrontGuidePositions);
            }

            //if (waveFrontLinearPositions.Count != segments)
            //{
            //    waveFrontLinearPositions.Clear();

            //    for (int i = 0; i < segments; i++)
            //    {
            //        waveFrontLinearPositions.Add(0);
            //    }
            //}

            waveFrontCurve.UpdatePoints(waveFrontGuidePositions);

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;

                waveFrontPositions[i] = waveFrontCurve.GetPoint(t);
                //waveFrontLinearPositions[i] = t;
            }
        }

        void UpdateVFXFoams()
        {
            if (shorelineFoams == null) return;

            shorelineFoams.SetVisible(showFoamsVFX);
            
            if (!showFoamsVFX) return;

            shorelineFoams.positionBuilder.ResetValidDataCount();

            //Retrieve the front wave positions from the shoreline renderer.
            for (int i = 0; i < shorelineRenderers.Count; i++)
            {
                ShorelineRenderer sr = shorelineRenderers[i];

                if (sr.NeedReset())
                {
                    continue;
                }

                shorelineFoams.AddData(sr.curvePoints, sr.waveLinearPos);
            }

            shorelineFoams.BuildData();
        }

        private void Update()
        {
            if (waveFrontGuidePositions.Length != guideLineCount)
            {
                waveFrontGuidePositions = new Vector3[guideLineCount];

                for (int i = 0; i < guideLineCount; i++)
                {
                    waveFrontGuidePositions[i] = Vector3.zero;
                }
            }

            for (int i = 0; i < shorelineRenderers.Count; i++)
            {
                ShorelineRenderer sr = shorelineRenderers[i];

                if (sr.NeedReset())
                {
                    continue;
                }

                if (sr.waveFrontPosition > 0)
                {
                    sr.waveFrontPosition -= Time.deltaTime * speed * waveFrontSpeedMulti.Evaluate(sr.waveFrontPosition);
                    sr.waveFrontPosition = Mathf.Max(0, sr.waveFrontPosition);
                }

                float waveValueFactor = Mathf.Lerp(1.0f, 0.0f, sr.waveFrontPosition);
                sr.SetLinearPos(waveValueFactor);
            }


            UpdateShorelineObjects();

            UpdateVFXFoams();
        }

        private void OnDrawGizmos()
        //private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.0f, 0.5f, 1.0f, 0.2f);

            float boundHeight = 3.0f;
            Bounds shorelineBound = new Bounds(Vector3.zero, Vector3.one * boundHeight);

            for (int i = 0; i < guidelines.Count; i++)
            {
                for (int j = 0; j < guidelines[i].points.Count; j++)
                {
                    Vector3 localPosition = guidelines[i].points[j].Node.localPosition;
                    shorelineBound.Encapsulate(localPosition);
                }
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Vector3 showBoundOffset = new Vector3(0, boundHeight * 0.5f, 0);
            Gizmos.DrawCube(shorelineBound.center + showBoundOffset, shorelineBound.size);

            Gizmos.color = new Color(0.0f, 0.5f, 1.0f, 0.5f);
            Gizmos.DrawWireCube(shorelineBound.center + showBoundOffset, shorelineBound.size);

            int debugShowSegments = 20;
            //draw guideline
            foreach (ShorelineGuideLine guideline in guidelines)
            {
                List<Vector3> renderPos = new List<Vector3>();
                for (int i = 0; i <= debugShowSegments; i++)
                {
                    renderPos.Add(guideline.GetInterpLocalPosition(i / (float)debugShowSegments));
                }

                Gizmos.color = Color.yellow;
                for (int i = 0; i < renderPos.Count - 1; i++)
                {
                    Gizmos.DrawLine(renderPos[i], renderPos[i + 1]);
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
