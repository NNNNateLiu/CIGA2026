using System.Collections;
using UnityEngine;

namespace UniversalWaterSystem
{
    /// <summary>
    /// 海啸系统 —— 集成 TechFusion UniversalWaterSystem
    ///
    /// 工作原理：
    ///   通过 Shader.SetGlobalVector 向 Ocean.shader 写入海啸参数，
    ///   着色器中的顶点位移将模拟一堵巨浪从远处扑来、冲上岸边的过程。
    ///
    /// 阶段（Phase）：
    ///   0 = 待机        水面正常
    ///   1 = 蓄力        水面先下降（退潮预警），持续 withdrawDuration 秒
    ///   2 = 冲击        海浪从 originDistance 处以 waveSpeed 推进
    ///   3 = 消退        浪高逐渐衰减，水面恢复正常
    ///
    /// 使用：
    ///   在 Inspector 中配置参数，调用 TriggerTsunami() 即可。
    ///   也可以在 Ocean.shader / URP variant 里加入对应的顶点偏移代码，
    ///   下方 "Shader 集成参考" 注释里有完整的 HLSL 片段。
    /// </summary>
    public class Tsunami : MonoBehaviour
    {
        // ── Shader 属性 ID ──────────────────────────────────────────────────────
        static readonly int ID_TsunamiParams  = Shader.PropertyToID("_TsunamiParams");
        // _TsunamiParams: x=波前世界Z位置, y=波高(m), z=波宽(m), w=相位(0/1/2/3)
        static readonly int ID_TsunamiDir     = Shader.PropertyToID("_TsunamiDir");
        // _TsunamiDir:    xy=归一化传播方向(XZ), z=退潮深度, w=激活(1=是)
        static readonly int ID_TsunamiAnim    = Shader.PropertyToID("_TsunamiAnim");
        // _TsunamiAnim:   x=波前行进距离(从起点), y=总行进距离, z=衰减t(0-1), w=时间

        // ── 浪形参数 ────────────────────────────────────────────────────────────
        [Header("浪形")]
        [Tooltip("起始点到海啸发源的距离（米），决定浪从哪里'冲过来'")]
        [SerializeField] private float originDistance  = 800f;

        [Tooltip("海浪最大高度（米）")]
        [SerializeField] private float maxWaveHeight   = 30f;

        [Tooltip("波峰宽度（米），越大浪越'胖'")]
        [SerializeField] private float waveWidth       = 120f;

        [Tooltip("浪的传播速度（米/秒）")]
        [SerializeField] private float waveSpeed       = 25f;

        [Tooltip("海浪传播方向（世界 XZ 平面），默认从北往南（0,0,-1）")]
        [SerializeField] private Vector2 waveDirection = new Vector2(0f, -1f);

        // ── 阶段时长 ─────────────────────────────────────────────────────────────
        [Header("阶段时长（秒）")]
        [Tooltip("退潮预警阶段：水面下降，营造不祥的寂静")]
        [SerializeField] private float withdrawDuration = 8f;

        [Tooltip("海浪完全消退所需时间（浪过后渐渐平息）")]
        [SerializeField] private float fadeDuration     = 12f;

        // ── 退潮效果 ──────────────────────────────────────────────────────────────
        [Header("退潮")]
        [Tooltip("退潮时水面下降深度（米）")]
        [SerializeField] private float withdrawDepth    = 6f;

        // ── 物理破坏 ──────────────────────────────────────────────────────────────
        [Header("物理冲击力")]
        [Tooltip("冲击区内施加给刚体的力（N）")]
        [SerializeField] private float impactForce      = 8000f;

        [Tooltip("冲击检测半球半径（米，从波前往后计算）")]
        [SerializeField] private float impactRadius     = 40f;

        [Tooltip("作用层，默认全部层")]
        [SerializeField] private LayerMask affectedLayers = ~0;

        [Tooltip("玩家标签，用于特殊提示")]
        [SerializeField] private string playerTag = "Player";

        // ── 运行时状态 ────────────────────────────────────────────────────────────
        public enum TsunamiPhase { Idle, Withdraw, Impact, Fade }
        public TsunamiPhase Phase { get; private set; } = TsunamiPhase.Idle;

        private float phaseTimer;
        private float waveTravelDistance; // 波前已行进的距离（从起点）
        private Vector3 waveOriginPos;    // 世界空间中海啸起点
        private Vector3 waveDirWorld;     // 归一化世界方向（XZ）
        private Coroutine tsunamiCoroutine;

        // ── 公开接口 ──────────────────────────────────────────────────────────────

        /// <summary>触发海啸（可在任何地方调用）</summary>
        public void TriggerTsunami()
        {
            if (Phase != TsunamiPhase.Idle)
            {
                Debug.LogWarning("[Tsunami] 海啸已在进行中，忽略重复触发。");
                return;
            }
            if (tsunamiCoroutine != null) StopCoroutine(tsunamiCoroutine);
            tsunamiCoroutine = StartCoroutine(RunTsunami());
        }

        /// <summary>立即停止并重置</summary>
        public void StopTsunami()
        {
            if (tsunamiCoroutine != null) StopCoroutine(tsunamiCoroutine);
            tsunamiCoroutine = null;
            Phase = TsunamiPhase.Idle;
            ClearShaderParams();
        }

        // ── 生命周期 ──────────────────────────────────────────────────────────────
        void OnDisable() => ClearShaderParams();

        void FixedUpdate()
        {
            if (Phase == TsunamiPhase.Impact)
                ApplyImpactPhysics();
        }

        // ── 主协程 ────────────────────────────────────────────────────────────────
        IEnumerator RunTsunami()
        {
            // 计算波方向与起点
            waveDirWorld = new Vector3(waveDirection.x, 0f, waveDirection.y).normalized;
            if (waveDirWorld == Vector3.zero) waveDirWorld = Vector3.forward * -1f;
            waveOriginPos     = transform.position - waveDirWorld * originDistance;
            waveTravelDistance = 0f;

            // ── 阶段 1：退潮 ──────────────────────────────────────────────────────
            Phase = TsunamiPhase.Withdraw;
            Debug.Log("[Tsunami] 阶段1：退潮预警");

            float t = 0f;
            while (t < withdrawDuration)
            {
                t += Time.deltaTime;
                float withdrawT = Mathf.SmoothStep(0f, 1f, t / withdrawDuration);
                SetShaderWithdraw(withdrawT);
                yield return null;
            }

            // ── 阶段 2：冲击 ──────────────────────────────────────────────────────
            Phase = TsunamiPhase.Impact;
            Debug.Log("[Tsunami] 阶段2：海啸冲击！");

            float totalDistance = originDistance + 400f; // 浪要越过场景中心再消退
            while (waveTravelDistance < totalDistance)
            {
                waveTravelDistance += waveSpeed * Time.deltaTime;
                float heightT = Mathf.SmoothStep(1f, 0f,
                    Mathf.Max(0f, waveTravelDistance - originDistance) / 400f); // 越过中心后衰减
                SetShaderImpact(waveTravelDistance, Mathf.Max(heightT, 0.05f));
                yield return null;
            }

            // ── 阶段 3：消退 ──────────────────────────────────────────────────────
            Phase = TsunamiPhase.Fade;
            Debug.Log("[Tsunami] 阶段3：浪潮消退");

            t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float fadeT = Mathf.SmoothStep(0f, 1f, t / fadeDuration); // 0=有浪, 1=平静
                SetShaderFade(waveTravelDistance, fadeT);
                yield return null;
            }

            // ── 完成 ──────────────────────────────────────────────────────────────
            Phase = TsunamiPhase.Idle;
            ClearShaderParams();
            Debug.Log("[Tsunami] 海啸结束，水面恢复正常。");
        }

        // ── Shader 参数写入 ───────────────────────────────────────────────────────

        void SetShaderWithdraw(float withdrawT)
        {
            // 波前还在很远处（未到达），只表现退潮
            Shader.SetGlobalVector(ID_TsunamiParams, new Vector4(
                -99999f,           // 波前位置（投影到方向轴）
                maxWaveHeight * (1f - withdrawT), // 高度为0（尚未到来）
                waveWidth,
                (float)TsunamiPhase.Withdraw));

            Shader.SetGlobalVector(ID_TsunamiDir, new Vector4(
                waveDirWorld.x,
                waveDirWorld.z,
                withdrawDepth * withdrawT,  // 退潮深度随时间增加
                1f));

            Shader.SetGlobalVector(ID_TsunamiAnim, new Vector4(
                0f,
                originDistance + 400f,
                0f,
                Time.time));
        }

        void SetShaderImpact(float travelDist, float heightMult)
        {
            // 波前在世界空间的位置（投影到传播轴）
            float waveFrontProj = Vector3.Dot(waveOriginPos, waveDirWorld) + travelDist;

            Shader.SetGlobalVector(ID_TsunamiParams, new Vector4(
                waveFrontProj,
                maxWaveHeight * heightMult,
                waveWidth,
                (float)TsunamiPhase.Impact));

            Shader.SetGlobalVector(ID_TsunamiDir, new Vector4(
                waveDirWorld.x,
                waveDirWorld.z,
                0f,    // 退潮结束
                1f));

            Shader.SetGlobalVector(ID_TsunamiAnim, new Vector4(
                travelDist,
                originDistance + 400f,
                heightMult,
                Time.time));
        }

        void SetShaderFade(float travelDist, float fadeT)
        {
            float waveFrontProj = Vector3.Dot(waveOriginPos, waveDirWorld) + travelDist;

            Shader.SetGlobalVector(ID_TsunamiParams, new Vector4(
                waveFrontProj,
                maxWaveHeight * (1f - fadeT),
                waveWidth * (1f + fadeT * 2f), // 浪宽扩散
                (float)TsunamiPhase.Fade));

            Shader.SetGlobalVector(ID_TsunamiDir, new Vector4(
                waveDirWorld.x,
                waveDirWorld.z,
                0f,
                1f - fadeT)); // 激活度逐渐降为0

            Shader.SetGlobalVector(ID_TsunamiAnim, new Vector4(
                travelDist,
                originDistance + 400f,
                1f - fadeT,
                Time.time));
        }

        void ClearShaderParams()
        {
            Shader.SetGlobalVector(ID_TsunamiParams, Vector4.zero);
            Shader.SetGlobalVector(ID_TsunamiDir,    Vector4.zero);
            Shader.SetGlobalVector(ID_TsunamiAnim,   Vector4.zero);
        }

        // ── 物理冲击力 ────────────────────────────────────────────────────────────

        void ApplyImpactPhysics()
        {
            // 波前世界坐标
            Vector3 waveFrontCenter = waveOriginPos + waveDirWorld * waveTravelDistance;
            waveFrontCenter.y = 0f;

            Collider[] cols = Physics.OverlapSphere(waveFrontCenter, impactRadius, affectedLayers);

            foreach (Collider col in cols)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;

                // 只推前方的物体（点积 > 0 表示在波前传播方向上）
                Vector3 toObj = (rb.position - waveFrontCenter);
                toObj.y = 0f;
                float dot = Vector3.Dot(toObj.normalized, waveDirWorld);
                if (dot < -0.3f) continue; // 跳过身后的物体

                float dist     = Mathf.Max(toObj.magnitude, 0.1f);
                float falloff  = Mathf.Clamp01(1f - dist / impactRadius);
                float heightMult = Mathf.SmoothStep(1f, 0f,
                    Mathf.Max(0f, waveTravelDistance - originDistance) / 400f);

                // 水平推力 + 向上抬升力
                Vector3 force = (waveDirWorld + Vector3.up * 0.5f) * impactForce * falloff * heightMult;
                rb.AddForce(force, ForceMode.Force);

                if (rb.CompareTag(playerTag))
                    Debug.Log("[Tsunami] 玩家被海啸冲击！");
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────────────
        void OnDrawGizmosSelected()
        {
            Vector3 dir = new Vector3(waveDirection.x, 0f, waveDirection.y).normalized;
            if (dir == Vector3.zero) dir = Vector3.back;

            Vector3 origin = transform.position - dir * originDistance;

            // 起点线
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawLine(origin - Vector3.right * 200f, origin + Vector3.right * 200f);

            // 传播方向箭头
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.8f);
            Gizmos.DrawLine(origin, origin + dir * (originDistance + 400f));
            Gizmos.DrawSphere(transform.position, 5f);

            // 冲击半径
            if (Phase == TsunamiPhase.Impact)
            {
                Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
                Vector3 front = origin + dir * waveTravelDistance;
                front.y = transform.position.y;
                Gizmos.DrawWireSphere(front, impactRadius);
            }
        }
    }
}

/*
 ┌─────────────────────────────────────────────────────────────────────────────┐
 │                      Ocean.shader  顶点偏移集成参考                          │
 │  在 Ocean.shader 的 vert() 函数里，worldPos 计算完毕后追加以下代码：          │
 └─────────────────────────────────────────────────────────────────────────────┘

 // ---- Tsunami ---------------------------------------------------------------
 float4 _TsunamiParams; // x=波前轴投影, y=浪高, z=波宽, w=phase
 float4 _TsunamiDir;    // x=dirX, y=dirZ, z=退潮深度, w=激活
 float4 _TsunamiAnim;   // x=行进距离, y=总距离, z=衰减t, w=时间

 if (_TsunamiDir.w > 0.01)
 {
     float2 dir2     = normalize(_TsunamiDir.xz);    // XZ 方向
     float  proj     = dot(worldPos.xz, dir2);        // 顶点在传播轴上的投影
     float  waveFront = _TsunamiParams.x;
     float  halfWidth = _TsunamiParams.z * 0.5;

     // 退潮下压（相位0-1）
     float withdrawY = -_TsunamiDir.z * smoothstep(0,1, _TsunamiDir.w);

     // 海浪形状：高斯波包
     float distToFront = proj - (waveFront - halfWidth);
     float gaussian    = exp(-pow(distToFront / max(halfWidth,0.01), 2.0));
     float waveY       = _TsunamiParams.y * gaussian;

     worldPos.y += withdrawY + waveY;
 }
 // ---- End Tsunami -----------------------------------------------------------

*/
