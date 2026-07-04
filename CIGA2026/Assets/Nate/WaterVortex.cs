using UnityEngine;

namespace UniversalWaterSystem
{
    /// <summary>
    /// 水面大漩涡 —— 集成 TechFusion UniversalWaterSystem
    ///
    /// 工作原理：
    ///   每帧向 Ocean.shader 写入漩涡参数（中心、半径、深度、旋转角），
    ///   由顶点着色器用数学公式直接下压水面顶点，形成漏斗形凹陷。
    ///   零贴图依赖，立即生效。
    /// </summary>
    public class WaterVortex : MonoBehaviour
    {
        [Header("漩涡形状")]
        [SerializeField] private float outerRadius = 40f;   // 影响半径
        [SerializeField] private float innerRadius = 2f;    // 死区半径
        [SerializeField] private float vortexDepth = 18f;   // 水面下压深度（越大漏斗越深）
        [SerializeField] private bool  clockwise   = true;

        [Header("旋转")]
        [SerializeField] private float rotateSpeed = 80f;   // 度/秒（越快螺旋感越强）

        [Header("物理力")]
        [SerializeField] private float pullForce    = 1200f;
        [SerializeField] private float spinForce    = 800f;
        [SerializeField] private float dragInVortex = 2f;

        [Header("游戏逻辑")]
        [SerializeField] private LayerMask affectedLayers = ~0;
        [SerializeField] private string    shipTag        = "Player";

        // ── 运行时 ────────────────────────────────────────────────────────────
        public bool IsActive { get; private set; } = true;

        private float rotAngle;

        static readonly int ID_VortexParams = Shader.PropertyToID("_VortexParams");
        static readonly int ID_VortexAnim   = Shader.PropertyToID("_VortexAnim");

        // ── 生命周期 ──────────────────────────────────────────────────────────
        void Awake()
        {
            // 立即写入，避免第一帧渲染时变量还是默认值 0
            PushToShader();
        }

        void OnDisable()
        {
            // 关闭时清除漩涡，让水面恢复正常
            Shader.SetGlobalVector(ID_VortexAnim, Vector4.zero);
        }

        void Update()
        {
            if (!IsActive) return;
            rotAngle += rotateSpeed * Time.deltaTime * (clockwise ? 1f : -1f);
        }

        void LateUpdate()
        {
            PushToShader();
        }

        void PushToShader()
        {
            Shader.SetGlobalVector(ID_VortexParams, new Vector4(
                transform.position.x,
                transform.position.z,
                outerRadius,
                innerRadius));

            Shader.SetGlobalVector(ID_VortexAnim, new Vector4(
                IsActive ? vortexDepth : 0f,
                rotAngle * Mathf.Deg2Rad,
                clockwise ? 1f : -1f,
                IsActive ? 1f : 0f));
        }


        void FixedUpdate()
        {
            if (!IsActive) return;
            ApplyVortexForces();
        }

        // ── 物理力 ────────────────────────────────────────────────────────────
        void ApplyVortexForces()
        {
            Vector3 center = transform.position;
            Collider[] cols = Physics.OverlapSphere(center, outerRadius, affectedLayers);

            foreach (Collider col in cols)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                Vector3 toCenter = center - rb.position;
                toCenter.y = 0f;
                float dist = toCenter.magnitude;
                if (dist < 0.1f) continue;

                if (dist < innerRadius)
                {
                    OnSwallowed(rb);
                    continue;
                }

                float falloff = 1f - Mathf.Clamp01(dist / outerRadius);
                falloff *= falloff;

                Vector3 pullDir = toCenter.normalized;
                rb.AddForce(pullDir * pullForce * falloff, ForceMode.Force);

                Vector3 tangent = clockwise
                    ? new Vector3(-pullDir.z, 0f,  pullDir.x)
                    : new Vector3( pullDir.z, 0f, -pullDir.x);
                rb.AddForce(tangent * spinForce * falloff, ForceMode.Force);
                rb.AddForce(-rb.velocity * dragInVortex * falloff, ForceMode.Force);
            }
        }

        void OnSwallowed(Rigidbody rb)
        {
            Vector3 dir = (transform.position - rb.position).normalized;
            rb.AddForce(pullForce * 3f * (dir + Vector3.down * 2f), ForceMode.Force);
            if (rb.CompareTag(shipTag))
                Debug.Log("[WaterVortex] 玩家被漩涡吞噬！");
        }

        // ── 公开接口 ──────────────────────────────────────────────────────────
        public void Activate()   => IsActive = true;
        public void Deactivate() => IsActive = false;
        public void Toggle()     => IsActive = !IsActive;

        public void SetIntensity(float t)
        {
            t           = Mathf.Clamp01(t);
            vortexDepth = Mathf.Lerp(0f,    8f, t);
            pullForce   = Mathf.Lerp(0f, 1200f, t);
            spinForce   = Mathf.Lerp(0f,  800f, t);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.3f);
            DrawCircleXZ(transform.position, outerRadius, 48);
            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.7f);
            DrawCircleXZ(transform.position, innerRadius, 24);
        }

        void DrawCircleXZ(Vector3 c, float r, int segs)
        {
            float step = 2f * Mathf.PI / segs;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step;
                Gizmos.DrawLine(
                    c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r,
                    c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r);
            }
        }
    }
}
