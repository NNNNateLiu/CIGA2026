using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    [System.Serializable]
    public class ShorelineGuidePoint
    {
        [SerializeField]
        private Transform transform;

        public Transform Node { get { return transform; } }

        public void Create(Transform root, float x, float z)
        {
            Destroy();

            GameObject po = new GameObject("GuidePoint");
            transform = po.transform;
            transform.SetParent(root);
            transform.localPosition = new Vector3(x, 0, z);
            transform.localRotation = Quaternion.identity;
        }

        public void Destroy()
        {
            if (transform == null) return;

#if UNITY_EDITOR
            GameObject.DestroyImmediate(transform.gameObject);
#else
            GameObject.Destroy(transform.gameObject);
#endif

        }
    }
}
