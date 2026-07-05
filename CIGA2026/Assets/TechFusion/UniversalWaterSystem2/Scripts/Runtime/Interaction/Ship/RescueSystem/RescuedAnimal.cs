using UnityEngine;
using UniversalWaterSystem;

namespace RescueSystem
{
    /// <summary>
    /// Attached to the animal model while it is on the boat.
    /// Handles falling back into the water using the project's water system.
    /// </summary>
    public class RescuedAnimal : MonoBehaviour
    {
        private BoatRescueManager manager;
        private Transform assignedSpot;
        private GameObject floatingPrefab;

        [Tooltip("Offset above water level to trigger return to water.")]
        [SerializeField] private float waterContactThreshold = 0.2f;

        public void Initialize(BoatRescueManager manager, Transform spot, GameObject prefab)
        {
            this.manager = manager;
            this.assignedSpot = spot;
            this.floatingPrefab = prefab;
        }

        private void Update()
        {
            if (Water.Instance == null) return;

            Vector3 pos = transform.position;
            float waterHeight = Water.Instance.GetWaterHeight(pos);

            // Check if the animal is at or below the water surface level
            if (pos.y <= waterHeight + waterContactThreshold)
            {
                ReturnToWater();
            }
        }

        public void ReturnToWater()
        {
            if (floatingPrefab != null)
            {
                // Instantiate the floating version at current position
                Instantiate(floatingPrefab, transform.position, Quaternion.identity);
            }

            // Notify manager and clean up
            if (manager != null && assignedSpot != null)
            {
                manager.ClearPosition(assignedSpot);
            }

            Debug.Log($"{gameObject.name} detected water level at {transform.position.y} and returned to floating state.");
            Destroy(gameObject);
        }
    }
}
