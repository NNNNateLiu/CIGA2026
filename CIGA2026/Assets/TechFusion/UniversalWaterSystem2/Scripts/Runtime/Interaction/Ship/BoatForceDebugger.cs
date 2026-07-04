using UnityEngine;

namespace UniversalWaterSystem
{

    [RequireComponent(typeof(Rigidbody))]

    public class BoatForceDebugger : MonoBehaviour
    {
        private Rigidbody rb;
        private Vector3 lastAppliedRelativeForce;
        private Vector3 lastAppliedWorldForce;

        [Header("Debug Settings")] public bool showDebugGizmos = true;
        public float forceScale = 0.01f; // 力的缩放比例，如果力很大，箭头会极长，需要缩放
        public float velocityScale = 1.0f;

        public Transform drawTransform;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        // 提供一个公共方法，供你原本的移动脚本调用
        // 例如：debugger.LogRelativeForce(Vector3.forward * speed);
        public void LogRelativeForce(Vector3 relativeForce)
        {
            lastAppliedRelativeForce = relativeForce;
            // 将局部力转换为世界坐标下的力，方便 Gizmos 绘制
            lastAppliedWorldForce = transform.TransformDirection(relativeForce);
        }

        void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying || rb == null) return;

            // 1. 绘制施加的局部力 (绿色)
            Gizmos.color = Color.green;
            Vector3 forceVisualEnd = drawTransform.position + (lastAppliedWorldForce * forceScale);
            Gizmos.DrawLine(drawTransform.position, forceVisualEnd);
            DrawArrowTip(drawTransform.position, forceVisualEnd, Color.green);

            // 2. 绘制当前船体的实际速度 (红色)
            Gizmos.color = Color.red;
            Vector3 velocityVisualEnd =
                drawTransform.position + (rb.velocity * velocityScale); // Unity 2026 推荐使用 linearVelocity，旧版本用 velocity
            Gizmos.DrawLine(drawTransform.position, velocityVisualEnd);
            DrawArrowTip(drawTransform.position, velocityVisualEnd, Color.red);
        }

        // 辅助方法：绘制箭头挂饰
        private void DrawArrowTip(Vector3 start, Vector3 end, Color color)
        {
            Vector3 direction = (end - start).normalized;
            if (direction == Vector3.zero) return;

            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;

            Gizmos.color = color;
            Gizmos.DrawRay(end, right * 0.5f);
            Gizmos.DrawRay(end, left * 0.5f);
        }

        // 每帧物理清除，避免力停了箭头还在
        void FixedUpdate()
        {
            // 如果你的移动脚本每帧都在调用 LogRelativeForce，这里可以不清零
            // lastAppliedWorldForce = Vector3.zero; 
        }
    }
}