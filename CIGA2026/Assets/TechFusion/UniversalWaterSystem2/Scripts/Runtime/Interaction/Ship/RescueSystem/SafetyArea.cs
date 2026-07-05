using UnityEngine;

namespace RescueSystem
{
    /// <summary>
    /// A trigger zone placed in the level. When the boat enters, all rescued animals
    /// onboard are delivered and removed from the ship.
    /// Resize and reposition freely via the BoxCollider in the Inspector.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SafetyArea : MonoBehaviour
    {
        private void Awake()
        {
            // Enforce trigger mode
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            BoatRescueManager manager = other.GetComponentInParent<BoatRescueManager>();
            if (manager == null)
                manager = other.GetComponent<BoatRescueManager>();

            if (manager != null)
            {
                manager.DeliverAnimals();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null) return;

            Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
            Gizmos.matrix = Matrix4x4.TRS(
                transform.TransformPoint(col.center),
                transform.rotation,
                transform.lossyScale
            );
            Gizmos.DrawCube(Vector3.zero, col.size);

            Gizmos.color = new Color(0f, 1f, 0.4f, 0.85f);
            Gizmos.DrawWireCube(Vector3.zero, col.size);
        }
#endif
    }
}
