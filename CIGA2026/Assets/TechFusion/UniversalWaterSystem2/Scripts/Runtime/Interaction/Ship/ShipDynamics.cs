using UnityEngine;
using UniversalWaterSystem;

namespace UniversalWaterSystem
{
    public class ShipDynamics : MonoBehaviour
    {
        [SerializeField] private float finalSpeed = 100f;
        [SerializeField] private float inertiaFactor = 0.005f;
        [SerializeField] private float turningFactor = 2.0f;
        [SerializeField] private float backwardSpeedFactor = 0.3f;
        
        [Header("Teleport Settings")]
        [SerializeField] private Transform startTransform;
        [SerializeField] private KeyCode teleportKey = KeyCode.T;

        private float verticalImpetus = 0f;
        private float horizontalImpetus = 0f;
        private Rigidbody rigidbodyComponent;

        private float acceleration = 0f;
        private float accelerationBreak;

        public float FinalSpeed { set { finalSpeed = value; } get { return finalSpeed; } }

        void Start()
        {
            rigidbodyComponent = GetComponent<Rigidbody>();

            accelerationBreak = finalSpeed * backwardSpeedFactor;
            
            SetImpetus(1,0);
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
            if (verticalImpetus > 0)
            {
                if (acceleration < finalSpeed)
                {
                    acceleration += (finalSpeed * inertiaFactor);
                    acceleration *= verticalImpetus;
                }
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