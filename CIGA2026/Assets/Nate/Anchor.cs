using UnityEngine;

namespace UniversalWaterSystem
{
    public class Anchor : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float anchorDepth = 20f;   // Depth to the sea floor
        [SerializeField] private float chainLength = 25f;  // Max distance ship can move from anchor
        [SerializeField] private KeyCode anchorKey = KeyCode.Space;
        
        [Header("References")]
        [SerializeField] private Transform chainExitPoint; // Transform at the ship's bow/side
        [SerializeField] private LineRenderer chainRenderer;
        
        private ConfigurableJoint anchorJoint;
        private Vector3 droppedAnchorPos;
        private bool isAnchorDropped = false;
        private Rigidbody rb;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            SetupJoint();
            
            if (chainRenderer != null) chainRenderer.enabled = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(anchorKey))
            {
                ToggleAnchor();
            }

            if (isAnchorDropped)
            {
                UpdateChainVisuals();
            }
        }

        private void SetupJoint()
        {
            // Create the joint on the ship
            anchorJoint = gameObject.AddComponent<ConfigurableJoint>();
            
            // Set all movement to Free by default
            anchorJoint.xMotion = ConfigurableJointMotion.Free;
            anchorJoint.yMotion = ConfigurableJointMotion.Free;
            anchorJoint.zMotion = ConfigurableJointMotion.Free;
            anchorJoint.angularXMotion = ConfigurableJointMotion.Free;
            anchorJoint.angularYMotion = ConfigurableJointMotion.Free;
            anchorJoint.angularZMotion = ConfigurableJointMotion.Free;
            
            // Define the "Chain" limit
            SoftJointLimit limit = new SoftJointLimit();
            limit.limit = chainLength;
            anchorJoint.linearLimit = limit;

            anchorJoint.autoConfigureConnectedAnchor = false;
        }

        private void ToggleAnchor()
        {
            isAnchorDropped = !isAnchorDropped;

            if (isAnchorDropped)
            {
                // Set anchor point directly below the ship (or use a Raycast for real terrain)
                Vector3 origin = (chainExitPoint != null) ? chainExitPoint.position : transform.position;
                droppedAnchorPos = new Vector3(origin.x, origin.y - anchorDepth, origin.z);

                // Lock the joint to the world point
                anchorJoint.connectedAnchor = droppedAnchorPos;
                anchorJoint.xMotion = ConfigurableJointMotion.Limited;
                anchorJoint.yMotion = ConfigurableJointMotion.Limited;
                anchorJoint.zMotion = ConfigurableJointMotion.Limited;

                if (chainRenderer != null) chainRenderer.enabled = true;
            }
            else
            {
                // Release the ship
                anchorJoint.xMotion = ConfigurableJointMotion.Free;
                anchorJoint.yMotion = ConfigurableJointMotion.Free;
                anchorJoint.zMotion = ConfigurableJointMotion.Free;

                if (chainRenderer != null) chainRenderer.enabled = false;
            }
        }

        private void UpdateChainVisuals()
        {
            if (chainRenderer == null) return;
            Vector3 start = (chainExitPoint != null) ? chainExitPoint.position : transform.position;
            chainRenderer.SetPosition(0, start);
            chainRenderer.SetPosition(1, droppedAnchorPos);
        }
    }
}
