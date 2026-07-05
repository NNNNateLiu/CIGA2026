using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RescueSystem
{
    /// <summary>
    /// Manages the rescue of animals when they collide with the boat.
    /// Tracks total rescued count and handles delivery at the Safety Area.
    /// </summary>
    public class BoatRescueManager : MonoBehaviour
    {
        [Header("Rescue Positions")]
        [Tooltip("List of transforms on the boat where animals will be teleported.")]
        [SerializeField] private List<Transform> rescuePositions = new List<Transform>();

        [Header("Physics Settings")]
        [SerializeField] private float animalMass = 50f;
        [SerializeField] private Vector3 animalColliderSize = new Vector3(2f, 2f, 2f);
        [SerializeField] private Vector3 animalColliderCenter = new Vector3(0f, 1f, 0f);

        [Header("Floating Settings")]
        [SerializeField] private GameObject floatingAnimalPrefab;

        // --- State ---
        private HashSet<Transform> occupiedPositions = new HashSet<Transform>();

        /// <summary>Total animals successfully delivered to a Safety Area.</summary>
        public int TotalRescued { get; private set; }

        // --- Rescue Detection ---

        private void OnCollisionEnter(Collision collision)
        {
            RescueableAnimal animal = collision.gameObject.GetComponent<RescueableAnimal>()
                ?? collision.gameObject.GetComponentInParent<RescueableAnimal>();

            if (animal != null)
                RescueAnimal(animal);
        }

        // --- Rescue Sequence ---

        private void RescueAnimal(RescueableAnimal animal)
        {
            Transform availableSpot = GetRandomUnoccupiedPosition();

            if (availableSpot == null)
            {
                Debug.LogWarning("[BoatRescueManager] No unoccupied rescue positions available on the boat!");
                return;
            }

            Transform model = animal.AnimalModel;

            if (model != null)
            {
                // Snap model to boat spot
                model.SetParent(availableSpot);
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                model.gameObject.SetActive(true);

                // Add/Configure box collider physics
                Rigidbody rb = model.gameObject.GetComponent<Rigidbody>();
                if (rb == null) rb = model.gameObject.AddComponent<Rigidbody>();
                rb.mass = animalMass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                BoxCollider col = model.gameObject.GetComponent<BoxCollider>();
                if (col == null) col = model.gameObject.AddComponent<BoxCollider>();
                col.size = animalColliderSize;
                col.center = animalColliderCenter;

                // Water-return monitor
                RescuedAnimal rescued = model.gameObject.GetComponent<RescuedAnimal>();
                if (rescued == null) rescued = model.gameObject.AddComponent<RescuedAnimal>();
                rescued.Initialize(this, availableSpot, floatingAnimalPrefab);

                // Animation trigger
                Animator animator = model.GetComponent<Animator>()
                    ?? model.GetComponentInChildren<Animator>();
                animator?.SetTrigger("rescued");

                occupiedPositions.Add(availableSpot);

                Debug.Log($"[BoatRescueManager] {model.name} rescued and placed at {availableSpot.name}.");
            }

            Destroy(animal.gameObject);
        }

        // --- Safety Area Delivery ---

        /// <summary>
        /// Called by SafetyArea when the boat enters the trigger.
        /// Removes all rescued animals from every Spot and increments the counter.
        /// </summary>
        public void DeliverAnimals()
        {
            int delivered = 0;

            foreach (Transform spot in rescuePositions)
            {
                if (spot == null) continue;

                // Collect children of the spot (the rescued animal models)
                List<Transform> children = new List<Transform>();
                foreach (Transform child in spot)
                    children.Add(child);

                foreach (Transform child in children)
                {
                    // Remove RescuedAnimal first so it won't spawn a Floating Animal on destroy
                    RescuedAnimal rescuedComp = child.GetComponent<RescuedAnimal>();
                    if (rescuedComp != null)
                        Destroy(rescuedComp);

                    Destroy(child.gameObject);
                    delivered++;
                }

                occupiedPositions.Remove(spot);
            }

            if (delivered > 0)
            {
                TotalRescued += delivered;
                Debug.Log($"[BoatRescueManager] Delivered {delivered} animal(s) to safety. Total rescued so far: {TotalRescued}.");
            }
        }

        // --- Position Management ---

        private Transform GetRandomUnoccupiedPosition()
        {
            List<Transform> unoccupied = rescuePositions
                .Where(p => p != null && !occupiedPositions.Contains(p))
                .ToList();

            if (unoccupied.Count == 0) return null;
            return unoccupied[Random.Range(0, unoccupied.Count)];
        }

        /// <summary>
        /// Frees a rescue spot (called by RescuedAnimal when the animal falls back into water).
        /// </summary>
        public void ClearPosition(Transform position)
        {
            occupiedPositions.Remove(position);
        }
    }
}
