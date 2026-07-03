using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    [System.Serializable]
    public class ShorelineGuideLine
    {
        //0 - 1
        public float waveFront;

        public List<ShorelineGuidePoint> points = new List<ShorelineGuidePoint>();

        private SplineCurve spline = null;

        public void CreateGuidePoints(uint count, Transform root, float x = 0, float length = 1.0f)
        {
            ClearGuidePoints();

            if (count < 2) return;

            for (int i = 0; i < count; i++)
            {
                ShorelineGuidePoint guidepoint = new ShorelineGuidePoint();
                float z = -(length / (count - 1)) * i;
                guidepoint.Create(root, x, z);
                points.Add(guidepoint);
            }

            UpdateCurve();
        }

        public void ClearGuidePoints()
        {
            for (int i = 0; i < points.Count; i++)
            {
                points[i].Destroy();
            }
            points.Clear();
        }

        public void UpdateCurve()
        {
            Vector3[] vector3s = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                //vector3s[i] = points[i].Node.position;
                vector3s[i] = points[i].Node.localPosition;
            }

            spline = new SplineCurve(vector3s, false, SplineType.Catmullrom, tension: 0.5f);
        }

        public Vector3 GetInterpLocalPosition(float t)
        {
            if (spline == null)
            {
                UpdateCurve();
            }

            return spline.GetPoint(t);
        }
    }
}
