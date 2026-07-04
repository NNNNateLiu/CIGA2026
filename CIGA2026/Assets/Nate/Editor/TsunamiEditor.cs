using UnityEditor;
using UnityEngine;

namespace UniversalWaterSystem
{
    [CustomEditor(typeof(Tsunami))]
    public class TsunamiEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var tsunami = (Tsunami)target;

            // ── 状态显示 ──────────────────────────────────────────────────
            Color phaseColor = tsunami.Phase switch
            {
                Tsunami.TsunamiPhase.Withdraw => new Color(1f, 0.8f, 0f),
                Tsunami.TsunamiPhase.Impact   => new Color(1f, 0.2f, 0.1f),
                Tsunami.TsunamiPhase.Fade     => new Color(0.4f, 0.7f, 1f),
                _                             => new Color(0.5f, 0.9f, 0.5f),
            };

            string phaseLabel = tsunami.Phase switch
            {
                Tsunami.TsunamiPhase.Idle     => "待机",
                Tsunami.TsunamiPhase.Withdraw => "退潮预警",
                Tsunami.TsunamiPhase.Impact   => "巨浪冲击",
                Tsunami.TsunamiPhase.Fade     => "浪潮消退",
                _                             => "未知",
            };

            var boldColored = new GUIStyle(EditorStyles.boldLabel);
            boldColored.normal.textColor = phaseColor;
            EditorGUILayout.LabelField($"阶段：{phaseLabel}", boldColored);

            // 强度进度条
            EditorGUI.ProgressBar(
                EditorGUILayout.GetControlRect(false, 18f),
                tsunami.Intensity, $"强度  {tsunami.Intensity:P0}");

            EditorGUILayout.Space(4);

            // ── 运行时按钮 ────────────────────────────────────────────────
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("触发海啸", GUILayout.Height(28)))
                        tsunami.TriggerTsunami();
                    if (GUILayout.Button("立即停止", GUILayout.Height(28)))
                        tsunami.StopTsunami();
                }
            }

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "把此组件放在场景中任意位置。\n" +
                "Wave Direction 为传播方向（XZ），默认(0,-1)=从北往南。\n" +
                "Origin Distance = 波从 GameObject 背后多远处发源。",
                MessageType.Info);

            if (Application.isPlaying)
                Repaint();
        }
    }
}
