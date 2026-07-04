using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace UniversalWaterSystem
{
    public static class VortexSetupTool
    {
        // ── 漩涡 ─────────────────────────────────────────────────────────────
        [MenuItem("GameObject/Water/Create Water Vortex", false, 10)]
        static void CreateVortex(MenuCommand cmd)
        {
            var go = new GameObject("WaterVortex");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            go.AddComponent<WaterVortex>();
            Undo.RegisterCreatedObjectUndo(go, "Create Water Vortex");
            Selection.activeObject = go;
        }

        // ── 海啸 ─────────────────────────────────────────────────────────────
        [MenuItem("GameObject/Water/Create Tsunami", false, 11)]
        static void CreateTsunami(MenuCommand cmd)
        {
            var go = new GameObject("Tsunami");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            go.AddComponent<Tsunami>();
            Undo.RegisterCreatedObjectUndo(go, "Create Tsunami");
            Selection.activeObject = go;
            Debug.Log("[Tsunami] GameObject 已创建。在 Inspector 设置 Wave Direction 和 Origin Distance，运行时点击【触发海啸】。");
        }

        // ── 工具 ─────────────────────────────────────────────────────────────
        [MenuItem("Tools/Water/Set Water Vertex Density 80")]
        static void SetVertexDensity()
        {
            var water = Object.FindObjectOfType<Water>();
            if (water == null)
            {
                Debug.LogError("[WaterSetup] 场景中没有找到 Water 组件");
                return;
            }

            var field = typeof(Water).GetField("vertexDensity",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                Debug.LogError("[WaterSetup] 找不到 vertexDensity 字段");
                return;
            }

            int old = (int)field.GetValue(water);
            field.SetValue(water, 80);
            EditorUtility.SetDirty(water);
            Debug.Log($"[WaterSetup] vertexDensity {old} → 80，请保存场景");
        }
    }
}
