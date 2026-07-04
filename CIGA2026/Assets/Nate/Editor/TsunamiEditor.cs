using UnityEngine;
using UnityEditor;

namespace UniversalWaterSystem
{
    /// <summary>
    /// Tsunami 自定义 Inspector + 菜单工具
    /// 菜单：GameObject > Water > Create Tsunami
    /// </summary>
    [CustomEditor(typeof(Tsunami))]
    public class TsunamiEditor : UnityEditor.Editor
    {
        // ── 菜单：一键创建海啸对象 ──────────────────────────────────────────────
        [MenuItem("GameObject/Water/Create Tsunami", false, 11)]
        static void CreateTsunami(MenuCommand cmd)
        {
            var go = new GameObject("Tsunami");
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            go.AddComponent<Tsunami>();
            Undo.RegisterCreatedObjectUndo(go, "Create Tsunami");
            Selection.activeObject = go;
        }

        // ── Inspector 自定义 ─────────────────────────────────────────────────
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var tsunami = (Tsunami)target;

            // ── 状态显示 ──────────────────────────────────────────────────────
            Color phaseColor = tsunami.Phase switch
            {
                Tsunami.TsunamiPhase.Withdraw => new Color(1f, 0.85f, 0.1f),
                Tsunami.TsunamiPhase.Impact   => new Color(1f, 0.3f, 0.1f),
                Tsunami.TsunamiPhase.Fade     => new Color(0.4f, 0.8f, 1f),
                _                             => new Color(0.5f, 0.9f, 0.5f),
            };
            string phaseLabel = tsunami.Phase switch
            {
                Tsunami.TsunamiPhase.Withdraw => "阶段：退潮预警",
                Tsunami.TsunamiPhase.Impact   => "阶段：冲击中！",
                Tsunami.TsunamiPhase.Fade     => "阶段：消退中",
                _                             => "阶段：待机",
            };

            var style = new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = phaseColor }, fontSize = 13 };
            EditorGUILayout.LabelField(phaseLabel, style);

            EditorGUILayout.Space(4);

            // ── 运行时控制按钮 ────────────────────────────────────────────────
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = new Color(0.9f, 0.4f, 0.2f);
                    if (GUILayout.Button("触发海啸", GUILayout.Height(30)))
                        tsunami.TriggerTsunami();

                    GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                    if (GUILayout.Button("立即停止", GUILayout.Height(30)))
                        tsunami.StopTsunami();

                    GUI.backgroundColor = Color.white;
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play 模式后可点击按钮触发海啸。", MessageType.Info);
            }

            EditorGUILayout.Space(6);

            // ── 默认字段 ──────────────────────────────────────────────────────
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();

            // ── 提示 ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Tsunami 组件通过 Shader.SetGlobalVector 驱动 Ocean.shader 中的顶点位移。\n" +
                "确保场景中只有一个激活的 Tsunami 实例。\n" +
                "waveDirection 使用 XZ 平面方向，(0,-1) 表示从北往南推进。",
                MessageType.Info);
        }
    }
}
