using UnityEngine;
using UnityEditor;

namespace UniversalWaterSystem
{
    /// <summary>
    /// WaterVortex 自定义 Inspector + 菜单工具
    /// 菜单：GameObject > Water > Create Water Vortex
    /// </summary>
    [CustomEditor(typeof(WaterVortex))]
    public class WaterVortexEditor : UnityEditor.Editor
    {
        // ── 菜单：一键创建漩涡 ───────────────────────────────────────────────
        [MenuItem("GameObject/Water/Create Water Vortex", false, 10)]
        static void CreateWaterVortex(MenuCommand cmd)
        {
            var go = new GameObject("WaterVortex");

            // 放到当前选中对象下，或场景根
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);

            // 挂脚本（Awake 会自动添加 MeshFilter / MeshRenderer）
            go.AddComponent<WaterVortex>();

            // 创建材质并赋给 MeshRenderer
            var shader = Shader.Find("Nate/VortexSurface");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "VortexSurface_Mat" };
                AssetDatabase.CreateAsset(mat, "Assets/Nate/VortexSurface.mat");
                AssetDatabase.SaveAssets();

                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = mat;
                    // 将材质引用写入脚本字段
                    var vortex = go.GetComponent<WaterVortex>();
                    var so     = new SerializedObject(vortex);
                    so.FindProperty("vortexMaterial").objectReferenceValue = mat;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else
            {
                Debug.LogWarning("[WaterVortex] 找不到 Nate/VortexSurface Shader，" +
                                 "请先确认 VortexSurface.shader 已导入项目。");
            }

            // 注册 Undo
            Undo.RegisterCreatedObjectUndo(go, "Create Water Vortex");
            Selection.activeObject = go;
        }

        // ── Inspector 自定义 ─────────────────────────────────────────────────
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var vortex = (WaterVortex)target;

            // 状态提示
            GUIStyle statusStyle = new GUIStyle(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                vortex.IsActive ? "状态：激活中" : "状态：已停止",
                vortex.IsActive
                    ? new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.2f, 0.8f, 0.4f) } }
                    : new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.8f, 0.3f, 0.2f) } });

            EditorGUILayout.Space(4);

            // 运行时按钮
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("激活", GUILayout.Height(24)))  vortex.Activate();
                    if (GUILayout.Button("停止", GUILayout.Height(24)))  vortex.Deactivate();
                    if (GUILayout.Button("切换", GUILayout.Height(24)))  vortex.Toggle();
                }

                EditorGUILayout.Space(2);

                // 强度滑条
                float intensity = EditorGUILayout.Slider("实时强度", 0.5f, 0f, 1f);
                if (GUI.changed && Application.isPlaying)
                    vortex.SetIntensity(intensity);
            }

            EditorGUILayout.Space(6);
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            // 提示：需要法线贴图
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "材质使用 Nate/VortexSurface Shader。\n" +
                "在材质上指定「法线贴图」可获得更好的水面效果。\n" +
                "深度泡沫需要在 URP Asset 中开启「Depth Texture」。",
                MessageType.Info);
        }
    }
}
