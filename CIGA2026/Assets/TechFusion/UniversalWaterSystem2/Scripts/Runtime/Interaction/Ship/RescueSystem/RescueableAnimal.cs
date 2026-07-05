using UnityEngine;

namespace RescueSystem
{
    /// <summary>
    /// Tags a GameObject as a rescueable animal, identifies its model,
    /// and holds per-animal physics settings applied when it is rescued onto the boat.
    /// </summary>
    public class RescueableAnimal : MonoBehaviour
    {
        [Tooltip("The actual model of the animal that will be teleported to the boat.")]
        [SerializeField] private Transform animalModel;

        [Header("Physics Settings")]
        [SerializeField] private float mass = 50f;
        [SerializeField] private Vector3 colliderSize = new Vector3(2f, 2f, 2f);
        [SerializeField] private Vector3 colliderCenter = new Vector3(0f, 1f, 0f);

        public Transform AnimalModel => animalModel;
        public float Mass => mass;
        public Vector3 ColliderSize => colliderSize;
        public Vector3 ColliderCenter => colliderCenter;

        private void Reset()
        {
            // Try to find the first child as the default model if not assigned
            if (animalModel == null && transform.childCount > 0)
                animalModel = transform.GetChild(0);
        }
    }
}
