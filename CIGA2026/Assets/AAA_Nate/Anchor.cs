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
        [SerializeField] private float anchorDragSteerStrength = 1.0f; // 锚曳力转向强度（系数）
        [SerializeField] private float anchorBaseDragForce = 2000f;    // 锚落底后的基础摩擦拖拽力

        [Header("下锚/起锚速度")]
        [SerializeField] private float dropSpeed = 4f;   // 锚下沉速度（m/s）
        [SerializeField] private float raiseSpeed = 2f;  // 起锚速度（m/s）

        [Header("链条视觉")]
        [SerializeField] private LineRenderer chainRenderer;
        [SerializeField] private GameObject anchorVisualPrefab; // 锚的可视化预制体
        [SerializeField] private int chainSegments = 16; // 贝塞尔曲线分段数

        private GameObject instantiatedAnchor_Left; // 左侧实例化的锚
        private GameObject instantiatedAnchor_Right; // 右侧实例化的锚
        private Quaternion anchorInitialRotation; // 记录初始旋转

        [Header("出链点（船舷位置）")]
        [SerializeField] private Transform leftChainExitPoint;
        [SerializeField] private Transform rightChainExitPoint;

        [Header("物理受力点")]
        [SerializeField] private Transform leftForcePoint;  // 左锚物理受力点
        [SerializeField] private Transform rightForcePoint; // 右锚物理受力点

        [SerializeField] private float anchorSeabedOffect;

        [Header("按键")]
        [SerializeField] private KeyCode dropLeftKey  = KeyCode.Q;
        [SerializeField] private KeyCode dropRightKey = KeyCode.E;
        [SerializeField] private KeyCode raiseKey     = KeyCode.R;
        
        [Header("Misc")]
        [SerializeField] private GameObject drivingVirtualCam;
        
        // ── 状态 ─────────────────────────────────────────────────────────────
        public AnchorState State { get; private set; } = AnchorState.Idle;

        private Rigidbody    rb;
        private ShipDynamics shipDynamics;
        private Transform    activeExit;       // 当前使用的出链点
        private Transform    activeForcePoint; // 当前使用的受力点
        private Vector3      anchorPos;        // 锚的当前世界坐标（动画过程中变化）
        private Vector3      seabedPos;        // 锚落底的目标坐标
        private float        paidChain;        // 已放出的链长

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

            if (anchorVisualPrefab != null)
            {
                // 初始化左侧锚
                if (leftChainExitPoint != null)
                {
                    instantiatedAnchor_Left = Instantiate(anchorVisualPrefab, leftChainExitPoint);
                    instantiatedAnchor_Left.transform.localPosition = Vector3.zero;
                    instantiatedAnchor_Left.transform.localRotation = Quaternion.identity;
                    anchorInitialRotation = instantiatedAnchor_Left.transform.rotation;
                }
                
                // 初始化右侧锚
                if (rightChainExitPoint != null)
                {
                    instantiatedAnchor_Right = Instantiate(anchorVisualPrefab, rightChainExitPoint);
                    instantiatedAnchor_Right.transform.localPosition = Vector3.zero;
                    instantiatedAnchor_Right.transform.localRotation = Quaternion.identity;
                }
            }
            
            drivingVirtualCam.SetActive(true);
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
                if (Input.GetKeyDown(dropLeftKey))  BeginDrop(leftChainExitPoint, true);
                if (Input.GetKeyDown(dropRightKey)) BeginDrop(rightChainExitPoint, false);
            }
            else if (State == AnchorState.Anchored || State == AnchorState.Dropping)
            {
                if (Input.GetKeyDown(raiseKey)) BeginRaise();
            }
        }

        // ── 下锚 ──────────────────────────────────────────────────────────────
        void BeginDrop(Transform exitPoint, bool isDroppingLeftAnchor)
        {
            float currentSeabedOffset = 0;
            
            activeExit = exitPoint != null ? exitPoint : transform;
            activeForcePoint = isDroppingLeftAnchor ? leftForcePoint : rightForcePoint;

            anchorPos  = activeExit.position;

            float seabedY = GetSeabedY(anchorPos);
            if (isDroppingLeftAnchor)
            {
                currentSeabedOffset = -anchorSeabedOffect;
            }
            else
            {
                currentSeabedOffset = anchorSeabedOffect;
            }
            // 使用船的变换方向来计算偏移，而不是简单的世界坐标偏移
            seabedPos = anchorPos + transform.right * currentSeabedOffset;
            seabedPos.y = seabedY;

            paidChain = 0f;
            State     = AnchorState.Dropping;

            if (chainRenderer != null) chainRenderer.enabled = true;
            
            // 下锚时，将对应的锚模型从父级（挂架）分离，以便独立运动
            GameObject activeAnchor = (activeExit == leftChainExitPoint) ? instantiatedAnchor_Left : instantiatedAnchor_Right;
            if (activeAnchor != null) activeAnchor.transform.SetParent(null);
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

                        // 起锚完成，将锚模型重新挂载到出链点（挂架）并重置位置
                        GameObject activeAnchor = (activeExit == leftChainExitPoint) ? instantiatedAnchor_Left : instantiatedAnchor_Right;
                        if (activeAnchor != null)
                        {
                            activeAnchor.transform.SetParent(activeExit);
                            activeAnchor.transform.localPosition = Vector3.zero;
                            activeAnchor.transform.localRotation = Quaternion.identity;
                        }
        
                        // Restore forward movement
                        if (shipDynamics != null) 
                        {
                            shipDynamics.SetImpetus(1f, 0f); 
                        }
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
            // 确定受力点：使用配置的受力点，若未配置则回退到出链点
            Vector3 forcePos = activeForcePoint != null ? activeForcePoint.position : exitPos;

            // 1. 基础摩擦力：模拟锚在海底拖行的阻力
            Vector3 worldVel = rb.GetPointVelocity(forcePos);
            Vector3 dragDir = -new Vector3(worldVel.x, 0, worldVel.z).normalized;
            if (worldVel.magnitude > 0.1f)
            {
                rb.AddForceAtPosition(dragDir * anchorBaseDragForce, forcePos, ForceMode.Force);
            }

            // 2. 链条张紧力
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

            // 在配置的受力点施加力
            rb.AddForceAtPosition(pullDir * forceMag * anchorDragSteerStrength, forcePos, ForceMode.Force);

            // 3. 阻尼：消除沿链条方向的速度分量
            Vector3 localForcePos = transform.InverseTransformPoint(forcePos);
            float velAlongChain = Vector3.Dot(rb.GetRelativePointVelocity(localForcePos), pullDir);
            if (velAlongChain > 0f)
                rb.AddForceAtPosition(-pullDir * velAlongChain * chainDamping * rb.mass, forcePos, ForceMode.Impulse);
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

            // 更新当前活动的锚模型位置 and 旋转
            GameObject activeAnchor = (activeExit == leftChainExitPoint) ? instantiatedAnchor_Left : instantiatedAnchor_Right;
            if (activeAnchor != null && State != AnchorState.Idle)
            {
                activeAnchor.transform.position = end;
                
                float tEnd = 1.0f;
                float tStart = 0.95f;
                Vector3 pStart = Mathf.Pow(1 - tStart, 2) * start + 2f * (1 - tStart) * tStart * mid + tStart * tStart * end;
                Vector3 tangent = (end - pStart).normalized;
                
                if (tangent != Vector3.zero)
                {
                    // 让锚的头部（模型 -Y 轴）朝向切线方向，
                    // 这样在下锚时头部向下，拖行时头部朝向受力方向
                    // Quaternion.FromToRotation(Vector3.down, tangent) 将模型的“下”对齐到切线
                    activeAnchor.transform.rotation = Quaternion.FromToRotation(Vector3.down, tangent);
                }
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
