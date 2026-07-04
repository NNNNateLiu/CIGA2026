using System.Collections;
using UnityEngine;

namespace UniversalWaterSystem
{
    /// <summary>
    /// 海啸系统 v2 —— 集成 TechFusion UniversalWaterSystem
    ///
    /// 阶段：Idle → Withdraw（退潮预警）→ Impact（巨浪冲击）→ Fade（消退）→ Idle
    ///
    /// Shader 参数（每帧 LateUpdate 写入）：
    ///   _TsunamiParams : x=波前轴投影, y=浪高(m), z=波宽(m), w=整体强度(0-1)
    ///   _TsunamiDir    : x=dirX, y=dirZ, z=退潮深度, w=预兆扰动强度(0-1)
    ///   _TsunamiAnim   : x=行进距离, y=总距离, z=尾部翻腾衰减t, w=时间
    /// </summary>
    public class Tsunami : MonoBehaviour
    {
        // ── Shader ID ─────────────────────────────────────────────────────────
        static readonly int ID_Params = Shader.PropertyToID("_TsunamiParams");
        static readonly int ID_Dir    = Shader.PropertyToID("_TsunamiDir");
        static readonly int ID_Anim   = Shader.PropertyToID("_TsunamiAnim");

        // ── 浪形参数 ──────────────────────────────────────────────────────────
        [Header("浪形")]
        [SerializeField] private float originDistance = 800f;   // 发源距离（m）
        [SerializeField] private float maxWaveHeight  = 100f;   // 最大浪高（m）
        [SerializeField] private float waveWidth      = 120f;   // 波宽（m）
        [SerializeField] private float waveSpeed      = 35f;    // 传播速度（m/s）
        [SerializeField] private Vector2 waveDirection = new Vector2(0f, -1f);

        [Header("阶段时长（秒）")]
        [SerializeField] private float withdrawDuration = 8f;
        [SerializeField] private float fadeDuration     = 12f;

        [Header("退潮")]
        [SerializeField] private float withdrawDepth = 6f;

        [Header("物理冲击")]
        [SerializeField] private float impactForce    = 8000f;
        [SerializeField] private float impactRadius   = 40f;
        [SerializeField] private LayerMask affectedLayers = ~0;
        [SerializeField] private string playerTag     = "Player";

        // ── 运行时状态 ────────────────────────────────────────────────────────
        public enum TsunamiPhase { Idle, Withdraw, Impact, Fade }
        public TsunamiPhase Phase { get; private set; } = TsunamiPhase.Idle;

        /// <summary>当前整体强度 0-1，可在编辑器实时查看</summary>
        public float Intensity { get; private set; }

        // 内部驱动变量（LateUpdate 读这些，协程只负责更新）
        private float waveFrontProj;    // 波前在传播轴上的世界投影
        private float currentHeight;    // 当前浪高
        private float currentWidth;     // 当前波宽
        private float currentWithdraw;  // 当前退潮深度
        private float preTurbulence;    // 预兆扰动强度 0-1
        private float churnDecay;       // 尾部翻腾衰减 0=满，1=平
        private float travelDistance;   // 已行进距离

        private Vector3 waveDirWorld;
        private Vector3 waveOriginPos;
        private float   totalDistance;

        private Coroutine tsunamiCoroutine;

        // ── 公开接口 ──────────────────────────────────────────────────────────
        public void TriggerTsunami()
        {
            if (Phase != TsunamiPhase.Idle)
            {
                Debug.LogWarning("[Tsunami] 已在进行中，忽略重复触发。");
                return;
            }
            if (tsunamiCoroutine != null) StopCoroutine(tsunamiCoroutine);
            tsunamiCoroutine = StartCoroutine(RunTsunami());
        }

        public void StopTsunami()
        {
            if (tsunamiCoroutine != null) StopCoroutine(tsunamiCoroutine);
            tsunamiCoroutine = null;
            Phase = TsunamiPhase.Idle;
            Intensity = 0f;
            ClearShader();
        }

        // ── 生命周期 ──────────────────────────────────────────────────────────
        void OnDisable() => ClearShader();

        void LateUpdate()
        {
            if (Phase == TsunamiPhase.Idle) return;
            PushToShader();
        }

        void FixedUpdate()
        {
            if (Phase == TsunamiPhase.Impact)
                ApplyPhysics();
        }

        // ── 协程：只负责更新内部状态，不直接写 Shader ────────────────────────
        IEnumerator RunTsunami()
        {
            waveDirWorld  = new Vector3(waveDirection.x, 0f, waveDirection.y).normalized;
            if (waveDirWorld == Vector3.zero) waveDirWorld = Vector3.back;
            waveOriginPos = transform.position - waveDirWorld * originDistance;
            totalDistance = originDistance + waveWidth * 4f;  // 浪需要完全通过场景
            travelDistance = 0f;
            churnDecay    = 0f;

            // ── 阶段1：退潮预警 ────────────────────────────────────────────
            Phase = TsunamiPhase.Withdraw;
            Debug.Log("[Tsunami] 阶段1：退潮预警");

            // 海面开始预兆性扰动（像地震在传导）
            float t = 0f;
            while (t < withdrawDuration)
            {
                t += Time.deltaTime;
                float progress = t / withdrawDuration;
                float eased    = Mathf.SmoothStep(0f, 1f, progress);

                currentWithdraw = withdrawDepth * eased;
                preTurbulence   = eased;
                currentHeight   = 0f;
                currentWidth    = waveWidth;
                Intensity       = eased * 0.5f;
                // 波前放很远，只有退潮效果
                waveFrontProj   = Vector3.Dot(waveOriginPos, waveDirWorld) - waveWidth * 3f;

                yield return null;
            }

            // ── 阶段2：巨浪冲击 ────────────────────────────────────────────
            Phase = TsunamiPhase.Impact;
            Debug.Log("[Tsunami] 阶段2：巨浪冲击！");

            currentWithdraw = 0f;
            preTurbulence   = 1f;

            while (travelDistance < totalDistance)
            {
                travelDistance += waveSpeed * Time.deltaTime;

                // 浪高：从 0 逐渐隆起（前 40% 路程内线性增长），越过目标后衰减
                float buildupT = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(travelDistance / (originDistance * 0.4f)));
                float overrun  = Mathf.Max(0f, travelDistance - originDistance);
                float decayT   = 1f - Mathf.SmoothStep(0f, 1f, overrun / (waveWidth * 3f));
                currentHeight  = maxWaveHeight * buildupT * Mathf.Max(decayT, 0.02f);
                currentWidth   = waveWidth;
                Intensity      = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(travelDistance / (originDistance * 0.2f)));

                waveFrontProj  = Vector3.Dot(waveOriginPos, waveDirWorld) + travelDistance;

                // 预兆扰动在浪抵达后逐渐减弱
                preTurbulence  = Mathf.Max(0f, 1f - overrun / waveWidth);

                // 尾部翻腾随行进距离积累（浪过后留下白浪）
                churnDecay     = Mathf.Clamp01(overrun / (waveWidth * 2f));

                yield return null;
            }

            // ── 阶段3：消退 ────────────────────────────────────────────────
            Phase = TsunamiPhase.Fade;
            Debug.Log("[Tsunami] 阶段3：浪潮消退");

            t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float fadeT = Mathf.SmoothStep(0f, 1f, t / fadeDuration);

                currentHeight = maxWaveHeight * 0.02f * (1f - fadeT);
                currentWidth  = waveWidth * (1f + fadeT * 2f); // 浪扩散变宽
                Intensity     = 1f - fadeT;
                churnDecay    = Mathf.Min(1f, churnDecay + Time.deltaTime / fadeDuration);

                yield return null;
            }

            // ── 结束 ───────────────────────────────────────────────────────
            Phase = TsunamiPhase.Idle;
            Intensity = 0f;
            ClearShader();
            Debug.Log("[Tsunami] 海啸结束。");
        }

        // ── 推送到 Shader ─────────────────────────────────────────────────────
        void PushToShader()
        {
            // _TsunamiParams: x=波前投影, y=浪高, z=波宽, w=整体强度
            Shader.SetGlobalVector(ID_Params, new Vector4(
                waveFrontProj,
                currentHeight,
                currentWidth,
                Intensity));

            // _TsunamiDir: xy=方向, z=退潮深度, w=预兆扰动
            Shader.SetGlobalVector(ID_Dir, new Vector4(
                waveDirWorld.x,
                waveDirWorld.z,
                currentWithdraw,
                preTurbulence));

            // _TsunamiAnim: x=行进距离, y=总距离, z=尾部衰减, w=时间
            Shader.SetGlobalVector(ID_Anim, new Vector4(
                travelDistance,
                totalDistance,
                churnDecay,
                Time.time));
        }

        void ClearShader()
        {
            Shader.SetGlobalVector(ID_Params, Vector4.zero);
            Shader.SetGlobalVector(ID_Dir,    Vector4.zero);
            Shader.SetGlobalVector(ID_Anim,   Vector4.zero);
        }

        // ── 物理冲击力 ────────────────────────────────────────────────────────
        void ApplyPhysics()
        {
            Vector3 frontCenter = waveOriginPos + waveDirWorld * travelDistance;
            frontCenter.y = 0f;

            Collider[] cols = Physics.OverlapSphere(frontCenter, impactRadius, affectedLayers);
            foreach (Collider col in cols)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                Vector3 toObj = rb.position - frontCenter;
                toObj.y = 0f;
                if (Vector3.Dot(toObj.normalized, waveDirWorld) < -0.3f) continue;

                float falloff    = Mathf.Clamp01(1f - toObj.magnitude / impactRadius);
                float heightMult = currentHeight / maxWaveHeight;

                rb.AddForce((waveDirWorld + Vector3.up * 0.5f) * impactForce * falloff * heightMult,
                            ForceMode.Force);

                if (rb.CompareTag(playerTag))
                    Debug.Log("[Tsunami] 玩家被海啸冲击！");
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────────
        void OnDrawGizmosSelected()
        {
            Vector3 dir = new Vector3(waveDirection.x, 0f, waveDirection.y).normalized;
            if (dir == Vector3.zero) dir = Vector3.back;

            Vector3 origin = transform.position - dir * originDistance;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawLine(origin - Vector3.right * 200f, origin + Vector3.right * 200f);

            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.8f);
            Gizmos.DrawLine(origin, transform.position + dir * waveWidth * 4f);
            Gizmos.DrawSphere(transform.position, 4f);

            // 波宽示意
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * 100f;
            Gizmos.DrawLine(transform.position - perp, transform.position + perp);

            if (Phase == TsunamiPhase.Impact)
            {
                Gizmos.color = new Color(1f, 0.2f, 0f, 0.4f);
                Vector3 front = origin + dir * travelDistance;
                front.y = transform.position.y;
                Gizmos.DrawWireSphere(front, impactRadius);
            }
        }
    }
}
