#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
//using UnityEditor.SceneManagement;
using UniversalWaterSystem;

namespace UniversalWaterSystem.Editor
{
    [CustomEditor(typeof(ShorelineWaveGenerator))]
    public class ShorelineWaveGeneratorEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            DrawDefaultInspector();
            
            ShorelineWaveGenerator ShorelineWaveGenerator = (ShorelineWaveGenerator)target;

            if (GUILayout.Button("Recreate GuideLines"))
            {
                ShorelineWaveGenerator.CreateGuideLines();
            }

            //if (GUILayout.Button("Create Shoreline Renderer"))
            //{
            //    ShorelineWaveGenerator.CreateShorelineRenderer();
            //}

            //if (GUILayout.Button("Clear All Shoreline Renderers"))
            //{
            //    ShorelineWaveGenerator.ClearAllShorelineRenderers();
            //}

            serializedObject.ApplyModifiedProperties();
        }

        void OnDrawGizmos()
        {
        }

        void OnSceneGUI()
        {
            Draw();
        }

        void Draw()
        {
            serializedObject.Update();

            ShorelineWaveGenerator ShorelineWaveGenerator = (ShorelineWaveGenerator)target;

            if (ShorelineWaveGenerator.guidelines.Count > 0)
            {
                foreach (ShorelineGuideLine guideline in ShorelineWaveGenerator.guidelines)
                {
                    Handles.color = Color.white;
                    foreach (ShorelineGuidePoint guidepoint in guideline.points)
                    {
                        Transform t = guidepoint.Node;

                        if (t == null) continue;

                        Vector3 oldPos = t.position;
                        var fmh_69_73_639187165349904808 = Quaternion.identity; t.position = Handles.FreeMoveHandle(t.position, 0.5f, Vector2.zero, Handles.CircleHandleCap);
                        t.position = new Vector3(t.position.x, 0f, t.position.z);

                        //need to dirty scene
                        if ((oldPos - t.position).sqrMagnitude > 0.001)
                        {
                            guideline.UpdateCurve();
                            MarkSceneAsDirty();
                        }

                    }
                }
            }
        }

        void MarkSceneAsDirty()
        {
            if (Application.isPlaying) return;

            UnityEngine.SceneManagement.Scene currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(currentScene);
        }
    }
}
#endif