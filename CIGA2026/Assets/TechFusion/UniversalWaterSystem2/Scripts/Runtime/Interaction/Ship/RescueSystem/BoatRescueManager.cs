using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RescueSystem
{
    /// <summary>
    /// Manages the rescue of animals when they collide with the boat.
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

        private HashSet<Transform> occupiedPositions = new HashSet<Transform>();

        private void OnCollisionEnter(Collision collision)
        {
            // Check if the collided object is a rescueable animal
            RescueableAnimal animal = collision.gameObject.GetComponent<RescueableAnimal>();
            
            if (animal == null)
            {
                // Also check children in case the collider is on a child object
                animal = collision.gameObject.GetComponentInParent<RescueableAnimal>();
            }

            if (animal != null)
            {
                RescueAnimal(animal);
            }
        }

        private void RescueAnimal(RescueableAnimal animal)
        {
            Transform availableSpot = GetRandomUnoccupiedPosition();

            if (availableSpot != null)
            {
                Transform model = animal.AnimalModel;
                
                if (model != null)
                {
                    // Teleport the model to the boat spot
                    model.SetParent(availableSpot);
                    model.localPosition = Vector3.zero;
                    model.localRotation = Quaternion.identity;
                    
                    // Activate model if it was hidden
                    model.gameObject.SetActive(true);

                    // Add/Configure Physics
                    Rigidbody rb = model.gameObject.GetComponent<Rigidbody>();
                    if (rb == null) rb = model.gameObject.AddComponent<Rigidbody>();
                    rb.mass = animalMass;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                    BoxCollider col = model.gameObject.GetComponent<BoxCollider>();
                    if (col == null) col = model.gameObject.AddComponent<BoxCollider>();
                    col.size = animalColliderSize;
                    col.center = animalColliderCenter;

                    // Add RescuedAnimal component to handle falling back
                    RescuedAnimal rescued = model.gameObject.GetComponent<RescuedAnimal>();
                    if (rescued == null) rescued = model.gameObject.AddComponent<RescuedAnimal>();
                    rescued.Initialize(this, availableSpot, floatingAnimalPrefab);

                    // Trigger the rescued animation if an Animator exists
                    Animator animator = model.GetComponent<Animator>();
                    if (animator == null)
                    {
                        animator = model.GetComponentInChildren<Animator>();
                    }

                    if (animator != null)
                    {
                        animator.SetTrigger("rescued");
                    }
                    
                    // Mark position as occupied
                    occupiedPositions.Add(availableSpot);
                    
                    Debug.Log($"Animal {model.name} rescued, physics enabled, and placed at {availableSpot.name}.");
                }

                // Destroy the original floating animal container
                Destroy(animal.gameObject);
            }
            else
            {
                Debug.LogWarning("No unoccupied rescue positions available on the boat!");
            }
        }

        private Transform GetRandomUnoccupiedPosition()
        {
            var unoccupied = rescuePositions.Where(p => !occupiedPositions.Contains(p)).ToList();
            
            if (unoccupied.Count == 0) return null;

            int randomIndex = Random.Range(0, unoccupied.Count);
            return unoccupied[randomIndex];
        }

        /// <summary>
        /// Clears an occupied position (e.g., if an animal is removed from the boat).
        /// </summary>
        public void ClearPosition(Transform position)
        {
            if (occupiedPositions.Contains(position))
            {
                occupiedPositions.Remove(position);
            }
        }
    }
}
