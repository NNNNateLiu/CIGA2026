using UnityEngine;

namespace UniversalWaterSystem
{
    /// <summary>
    /// 船只抛锚系统
    /// 状态机：Idle → Dropping → Anchored → Raising → Idle
    /// 用自定义水平拉力约束船只，不限制Y轴，避免沉船
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Anchor : MonoBehaviour
    {
        public enum AnchorState { Idle, Dropping, Anchored, Raising }

        [Header("锚链设置")]
        [SerializeField] private float maxChainLength = 40f;      // 最大链长（米）
        [SerializeField] private float seabedDepth = 15f;         // 海底深度（水面以下）
        [SerializeField] private float chainTensionForce = 8000f; // 链条张紧时对船施加的力
        [SerializeField] private float chainDamping = 1.2f;       // 链条收紧时的速度阻尼

        [Header("下锚/起锚速度")]
        [SerializeField] private float dropSpeed = 4f;   // 锚下沉速度（m/s）
        [SerializeField] private float raiseSpeed = 2f;  // 起锚速度（m/s）

        [Header("链条视觉")]
        [SerializeField] private LineRenderer chainRenderer;
        [SerializeField] private int chainSegments = 16; // 贝塞尔曲线分段数

        [Header("出链点（船舷位置）")]
        [SerializeField] private Transform leftChainExitPoint;
        [SerializeField] private Transform rightChainExitPoint;

        [Header("按键")]
        [SerializeField] private KeyCode dropLeftKey  = KeyCode.Q;
        [SerializeField] private KeyCode dropRightKey = KeyCode.E;
        [SerializeField] private KeyCode raiseKey     = KeyCode.R;

        // ── 状态 ─────────────────────────────────────────────────────────────
        public AnchorState State { get; private set; } = AnchorState.Idle;

        private Rigidbody    rb;
        private ShipDynamics shipDynamics;
        private Transform    activeExit;  // 当前使用的出链点
        private Vector3      anchorPos;   // 锚的当前世界坐标（动画过程中变化）
        private Vector3      seabedPos;   // 锚落底的目标坐标
        private float        paidChain;   // 已放出的链长

        // ── 初始化 ────────────────────────────────────────────────────────────
        void Start()
        {
            rb           = GetComponent<Rigidbody>();
            shipDynamics = GetComponent<ShipDynamics>();

            if (chainRenderer != null)
            {
                chainRenderer.positionCount = chainSegments;
                chainRenderer.enabled       = false;
            }
        }

        // ── 主循环 ────────────────────────────────────────────────────────────
        void Update()
        {
            HandleInput();
            UpdateAnchorSimulation();
            UpdateChainVisuals();
        }

        void FixedUpdate()
        {
            if (State == AnchorState.Anchored)
                ApplyChainForce();
        }

        // ── 输入处理 ──────────────────────────────────────────────────────────
        void HandleInput()
        {
            if (State == AnchorState.Idle)
            {
                if (Input.GetKeyDown(dropLeftKey))  BeginDrop(leftChainExitPoint);
                if (Input.GetKeyDown(dropRightKey)) BeginDrop(rightChainExitPoint);
            }
            else if (State == AnchorState.Anchored || State == AnchorState.Dropping)
            {
                if (Input.GetKeyDown(raiseKey)) BeginRaise();
            }
        }

        // ── 下锚 ──────────────────────────────────────────────────────────────
        void BeginDrop(Transform exitPoint)
        {
            activeExit = exitPoint != null ? exitPoint : transform;
            anchorPos  = activeExit.position;

            float seabedY = GetSeabedY(anchorPos);
            seabedPos = new Vector3(anchorPos.x, seabedY, anchorPos.z);

            paidChain = 0f;
            State     = AnchorState.Dropping;

            if (chainRenderer != null) chainRenderer.enabled = true;
        }

        // ── 起锚 ──────────────────────────────────────────────────────────────
        void BeginRaise()
        {
            State = AnchorState.Raising;
        }

        // ── 锚位置动画 ────────────────────────────────────────────────────────
        void UpdateAnchorSimulation()
        {
            if (activeExit == null) return;

            switch (State)
            {
                case AnchorState.Dropping:
                {
                    anchorPos = Vector3.MoveTowards(anchorPos, seabedPos, dropSpeed * Time.deltaTime);
                    paidChain = Vector3.Distance(activeExit.position, anchorPos);

                    if (Vector3.Distance(anchorPos, seabedPos) < 0.05f)
                    {
                        anchorPos = seabedPos;
                        State     = AnchorState.Anchored;
                        if (shipDynamics != null) shipDynamics.SetImpetus(0f, 0f);
                    }
                    break;
                }

                case AnchorState.Raising:
                {
                    Vector3 target = activeExit.position;
                    anchorPos = Vector3.MoveTowards(anchorPos, target, raiseSpeed * Time.deltaTime);
                    paidChain = Vector3.Distance(activeExit.position, anchorPos);

                    if (Vector3.Distance(anchorPos, target) < 0.3f)
                    {
                        State = AnchorState.Idle;
                        if (chainRenderer != null) chainRenderer.enabled = false;
                        if (shipDynamics != null) shipDynamics.SetImpetus(1f, 0f);
                    }
                    break;
                }
            }
        }

        // ── 锚链拉力（只施加水平分量，避免沉船）────────────────────────────
        void ApplyChainForce()
        {
            if (activeExit == null) return;

            Vector3 exitPos = activeExit.position;

            float effectiveLen = Mathf.Min(paidChain + 1.5f, maxChainLength);

            Vector3 toAnchorFlat = new Vector3(
                anchorPos.x - exitPos.x,
                0f,
                anchorPos.z - exitPos.z);
            float flatDist = toAnchorFlat.magnitude;

            if (flatDist <= effectiveLen) return;

            float   excess   = flatDist - effectiveLen;
            Vector3 pullDir  = toAnchorFlat.normalized;
            float   forceMag = chainTensionForce * excess;

            rb.AddForce(pullDir * forceMag, ForceMode.Force);

            // 阻尼：消除沿链条方向的速度分量，模拟链条拉紧瞬间的顿挫感
            float velAlongChain = Vector3.Dot(rb.velocity, pullDir);
            if (velAlongChain > 0f)
                rb.AddForce(-pullDir * velAlongChain * chainDamping * rb.mass, ForceMode.Impulse);
        }

        // ── 悬链线视觉（二次贝塞尔 + 垂度）─────────────────────────────────
        void UpdateChainVisuals()
        {
            if (chainRenderer == null || State == AnchorState.Idle || activeExit == null) return;

            Vector3 start = activeExit.position;
            Vector3 end   = anchorPos;

            float straightDist = Vector3.Distance(start, end);
            float slack        = Mathf.Max(0f, paidChain - straightDist);
            float sag          = slack * 0.45f;

            Vector3 mid = (start + end) * 0.5f - Vector3.up * sag;

            for (int i = 0; i < chainSegments; i++)
            {
                float   t = (float)i / (chainSegments - 1);
                Vector3 p = Mathf.Pow(1 - t, 2) * start
                          + 2f * (1 - t) * t     * mid
                          + t  * t                * end;
                chainRenderer.SetPosition(i, p);
            }
        }

        // ── 海底探测 ──────────────────────────────────────────────────────────
        float GetSeabedY(Vector3 origin)
        {
            Ray ray = new Ray(new Vector3(origin.x, 5f, origin.z), Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, seabedDepth + 50f))
                return hit.point.y;

            return origin.y - seabedDepth;
        }

        // ── Gizmos 调试 ───────────────────────────────────────────────────────
        void OnDrawGizmosSelected()
        {
            if (State == AnchorState.Idle || activeExit == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(anchorPos, 0.4f);

            Gizmos.color = State == AnchorState.Anchored ? Color.red : Color.cyan;
            Gizmos.DrawLine(activeExit.position, anchorPos);

            if (State == AnchorState.Anchored)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                DrawWireCircle(anchorPos, paidChain + 1.5f, 32);
            }
        }

        void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float step = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float   a0 = Mathf.Deg2Rad * (i       * step);
                float   a1 = Mathf.Deg2Rad * ((i + 1) * step);
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}
