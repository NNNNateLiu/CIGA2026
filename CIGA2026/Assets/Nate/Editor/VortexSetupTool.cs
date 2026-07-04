using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace UniversalWaterSystem
{
    public static class VortexSetupTool
    {
        [MenuItem("Tools/Vortex/Set Water Vertex Density 80")]
        static void SetVertexDensity()
        {
            var water = Object.FindObjectOfType<Water>();
            if (water == null)
            {
                Debug.LogError("[VortexSetup] 场景中没有找到 Water 组件");
                return;
            }

            var field = typeof(Water).GetField("vertexDensity",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                Debug.LogError("[VortexSetup] 找不到 vertexDensity 字段");
                return;
            }

            int old = (int)field.GetValue(water);
            field.SetValue(water, 80);
            EditorUtility.SetDirty(water);

            Debug.Log($"[VortexSetup] vertexDensity {old} → 80，请保存场景");
        }
    }
}
