using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    /// <summary>
    /// 水面大漩涡 —— 支持场景中同时存在多个（最多4个）
    /// 所有实例共用一套 Shader 数组参数，由第一个实例统一推送
    /// </summary>
    public class WaterVortex : MonoBehaviour
    {
        public const int MAX_VORTEX = 4;

        [Header("漩涡形状")]
        [SerializeField] private float outerRadius = 150f;
        [SerializeField] private float innerRadius = 70f;
        [SerializeField] private float vortexDepth = 100f;
        [SerializeField] private bool  clockwise   = true;

        [Header("旋转")]
        [SerializeField] private float rotateSpeed = 80f;

        [Header("物理力")]
        [SerializeField] private float pullForce    = 12000f;
        [SerializeField] private float spinForce    = 8000f;
        [SerializeField] private float dragInVortex = 10f;

        [Header("游戏逻辑")]
        [SerializeField] private LayerMask affectedLayers = ~0;

        // ── 运行时 ────────────────────────────────────────────────────────────
        public bool IsActive { get; private set; } = true;

        /// <summary>玩家到达漩涡底部时触发</summary>
        public static event System.Action OnPlayerReachedBottom;

        // 公开只读属性，供静态方法访问
        public float OuterRadius => outerRadius;
        public float InnerRadius => innerRadius;
        public float VortexDepth => vortexDepth;
        public bool  Clockwise   => clockwise;
        public float RotAngle    => rotAngle;

        private float rotAngle;

        // 原始配置值，供 SetIntensity 插值使用
        private float _baseOuterRadius;
        private float _baseInnerRadius;
        private float _baseVortexDepth;
        private float _basePullForce;
        private float _baseSpinForce;

        // ── 静态实例列表 ──────────────────────────────────────────────────────
        private static readonly List<WaterVortex> s_all = new List<WaterVortex>();

        // Shader 全局数组 ID
        static readonly int ID_VortexParamsArr = Shader.PropertyToID("_VortexParamsArr");
        static readonly int ID_VortexAnimArr   = Shader.PropertyToID("_VortexAnimArr");
        static readonly int ID_VortexCount     = Shader.PropertyToID("_VortexCount");

        // 复用数组，避免每帧 GC
        static readonly Vector4[] s_paramsArr = new Vector4[MAX_VORTEX];
        static readonly Vector4[] s_animArr   = new Vector4[MAX_VORTEX];

        // ── 生命周期 ──────────────────────────────────────────────────────────
        void Awake()
        {
            _baseOuterRadius = outerRadius;
            _baseInnerRadius = innerRadius;
            _baseVortexDepth = vortexDepth;
            _basePullForce   = pullForce;
            _baseSpinForce   = spinForce;

            if (!s_all.Contains(this))
                s_all.Add(this);
            PushAll();
        }

        void OnEnable()
        {
            if (!s_all.Contains(this))
                s_all.Add(this);
        }

        void OnDisable()
        {
            s_all.Remove(this);
            // 清空该槽位
            PushAll();
        }

        void OnDestroy()
        {
            s_all.Remove(this);
            PushAll();
        }

        // ── 更新 ──────────────────────────────────────────────────────────────
        void Update()
        {
            if (!IsActive) return;
            rotAngle += rotateSpeed * Time.deltaTime * (clockwise ? 1f : -1f);
        }

        void LateUpdate()
        {
            // 只有列表中第一个实例负责推送所有数据（避免重复写入）
            if (s_all.Count > 0 && s_all[0] == this)
                PushAll();
        }

        void FixedUpdate()
        {
            if (!IsActive) return;
            ApplyVortexForces();
        }

        // ── 统一推送所有实例到 Shader ─────────────────────────────────────────
        static void PushAll()
        {
            int count = Mathf.Min(s_all.Count, MAX_VORTEX);

            for (int i = 0; i < MAX_VORTEX; i++)
            {
                if (i < count)
                {
                    var v = s_all[i];
                    s_paramsArr[i] = new Vector4(
                        v.transform.position.x,
                        v.transform.position.z,
                        v.outerRadius,
                        v.innerRadius);

                    s_animArr[i] = new Vector4(
                        v.IsActive ? v.vortexDepth : 0f,
                        v.rotAngle * Mathf.Deg2Rad,
                        v.clockwise ? 1f : -1f,
                        v.IsActive ? 1f : 0f);
                }
                else
                {
                    // 空槽位：激活位清零
                    s_paramsArr[i] = Vector4.zero;
                    s_animArr[i]   = Vector4.zero;
                }
            }

            Shader.SetGlobalVectorArray(ID_VortexParamsArr, s_paramsArr);
            Shader.SetGlobalVectorArray(ID_VortexAnimArr,   s_animArr);
            Shader.SetGlobalInt(ID_VortexCount, count);
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

                if (dist < innerRadius) { OnSwallowed(rb); continue; }

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

            if (rb.GetComponent<ShipDynamics>() == null) return;

            float bottomY = transform.position.y - vortexDepth;
            Debug.Log($"[Vortex] Ship swallowed | pos.y={rb.position.y:F1} | bottomY={bottomY:F1}");

            if (rb.position.y <= bottomY)
            {
                Debug.Log("[Vortex] Bottom reached → firing OnPlayerReachedBottom");
                OnPlayerReachedBottom?.Invoke();
                if (GameManager.Instance != null) GameManager.Instance.ShowDiePanel();
            }
        }

        // ── 公开接口 ──────────────────────────────────────────────────────────
        public void Activate()   => IsActive = true;
        public void Deactivate() => IsActive = false;
        public void Toggle()     => IsActive = !IsActive;

        public void SetIntensity(float t)
        {
            t           = Mathf.Clamp01(t);
            outerRadius = Mathf.Lerp(0f, _baseOuterRadius, t);
            innerRadius = Mathf.Lerp(0f, _baseInnerRadius, t);
            vortexDepth = Mathf.Lerp(0f, _baseVortexDepth, t);
            pullForce   = Mathf.Lerp(0f, _basePullForce,   t);
            spinForce   = Mathf.Lerp(0f, _baseSpinForce,   t);
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
        void OnDrawGizmosSelected()
        {
            // 显示实例编号
            int idx = s_all.IndexOf(this);
            string label = idx >= 0 ? $"Vortex [{idx}]" : "Vortex";

            Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.25f);
            DrawCircleXZ(transform.position, outerRadius, 48);
            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.7f);
            DrawCircleXZ(transform.position, innerRadius, 24);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, label);
#endif
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
