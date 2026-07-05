using UnityEngine;
using UniversalWaterSystem;
using RescueSystem;

namespace UniversalWaterSystem
{
    [RequireComponent(typeof(Rigidbody))]
    
    public class ShipDynamics : MonoBehaviour
    {
        [SerializeField] private float finalSpeed = 100f;
        [SerializeField] private float inertiaFactor = 0.005f;
        [SerializeField] private float turningFactor = 2.0f;
        [SerializeField] private float backwardSpeedFactor = 0.3f;
        
        [Header("Teleport Settings")]
        [SerializeField] private Transform startTransform;
        [SerializeField] private KeyCode teleportKey = KeyCode.T;
        [SerializeField] private Transform rescuePositionsRoot;

        private float verticalImpetus = 0f;
        private float horizontalImpetus = 0f;
        private Rigidbody rigidbodyComponent;

        private float acceleration = 0f;
        private float accelerationBreak;
        
        public bool IsFrozen { get; private set; } = false;

        public float FinalSpeed { set { finalSpeed = value; } get { return finalSpeed; } }

        public void Freeze()
        {
            IsFrozen = true;
            acceleration = 0f;
            SetImpetus(0f, 0f);
            if (rigidbodyComponent == null) rigidbodyComponent = GetComponent<Rigidbody>();
            rigidbodyComponent.velocity        = Vector3.zero;
            rigidbodyComponent.angularVelocity = Vector3.zero;
            rigidbodyComponent.isKinematic     = true;
        }

        public void Unfreeze()
        {
            IsFrozen = false;
            rigidbodyComponent.isKinematic = false;
            SetImpetus(1f, 0f);
        }

        public void ClearForces()
        {
            acceleration = 0f;
            SetImpetus(0f, 0f);
            rigidbodyComponent.velocity        = Vector3.zero;
            rigidbodyComponent.angularVelocity = Vector3.zero;
        }
        
        void Awake()
        {
            rigidbodyComponent = GetComponent<Rigidbody>();
            accelerationBreak  = finalSpeed * backwardSpeedFactor;
        }
        
        void Start()
        {
            rigidbodyComponent = GetComponent<Rigidbody>();

            //accelerationBreak = finalSpeed * backwardSpeedFactor;
            
            //SetImpetus(1,0);
            
            // 若外部已在 Awake/Start 里调用 Freeze()，不覆盖冻结状态
            if (!IsFrozen)
                SetImpetus(1, 0);

            if (rescuePositionsRoot == null)
            {
                rescuePositionsRoot = transform.Find("RescuePositions");
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(teleportKey))
            {
                TeleportToStart();
            }
        }

        private void TeleportToStart()
        {
            if (startTransform == null)
            {
                Debug.LogWarning("Start Transform is not assigned in ShipDynamics!");
                return;
            }

            // Return animals to water before teleporting the boat
            ReturnAnimalsToWater();

            // Reset position and rotation
            transform.position = startTransform.position;
            transform.rotation = startTransform.rotation;

            // Clear physics momentum
            if (rigidbodyComponent != null)
            {
                rigidbodyComponent.velocity = Vector3.zero;
                rigidbodyComponent.angularVelocity = Vector3.zero;
            }
            
            // Reset internal motor state
            acceleration = 0f;
        }

        private void ReturnAnimalsToWater()
        {
            if (rescuePositionsRoot == null) return;

            // Iterate through each spot (Spot1, Spot2, etc.)
            foreach (Transform spot in rescuePositionsRoot)
            {
                // Each spot might have multiple animals, or we check for children
                // According to prompt: "any child objects under Spot in rescue positions"
                
                // Copy children to a list to avoid modification issues during iteration
                System.Collections.Generic.List<Transform> animals = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in spot)
                {
                    animals.Add(child);
                }

                foreach (Transform animal in animals)
                {
                    RescuedAnimal rescuedComp = animal.GetComponent<RescuedAnimal>();
                    if (rescuedComp != null)
                    {
                        // Detach and return to water
                        animal.SetParent(null);
                        rescuedComp.ReturnToWater();
                    }
                }
            }
        }

        public void SetImpetus(float verticalImpetus, float horizontalImpetus)
        {
            this.verticalImpetus = Mathf.Clamp(verticalImpetus, -1, 1);
            this.horizontalImpetus = Mathf.Clamp(horizontalImpetus, -1, 1);
        }

        public Vector3 GetFrontDir()
        {
            return transform.forward;
        }

        void FixedUpdate()
        {
            if (IsFrozen) return;
            if (verticalImpetus > 0)
            {
                acceleration = Mathf.MoveTowards(acceleration, finalSpeed * verticalImpetus, finalSpeed * inertiaFactor);
            }
            else if (verticalImpetus == 0)
            {
                if (acceleration > 0)
                {
                    acceleration -= finalSpeed * inertiaFactor;
                    acceleration = Mathf.Max(acceleration, 0);
                }
                if (acceleration < 0)
                {
                    acceleration += finalSpeed * inertiaFactor;
                    acceleration = Mathf.Min(acceleration, 0);
                }
            }
            else if (verticalImpetus < 0)
            {
                if (acceleration > -accelerationBreak)
                {
                    acceleration -= finalSpeed * inertiaFactor * backwardSpeedFactor;
                }
            }

            rigidbodyComponent.AddRelativeForce(Vector3.forward * acceleration);
            rigidbodyComponent.AddRelativeTorque(0, horizontalImpetus * turningFactor, 0);


            //for stability

            float angle = transform.rotation.eulerAngles.z;
            if (angle >= 180) angle = angle - 360;
            //Debug.Log("stable angle = " + angle.ToString());
            rigidbodyComponent.AddRelativeTorque(0, 0, -1000 * angle);
            
            if (TryGetComponent<BoatForceDebugger>(out var debugger))
            {
                debugger.LogRelativeForce(rigidbodyComponent.velocity);
            }
        }
    }
}