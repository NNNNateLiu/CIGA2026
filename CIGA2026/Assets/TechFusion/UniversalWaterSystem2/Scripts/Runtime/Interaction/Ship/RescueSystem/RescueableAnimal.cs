using UnityEngine;

namespace RescueSystem
{
    /// <summary>
    /// Component to tag a GameObject as a rescueable animal and identify its model.
    /// </summary>
    public class RescueableAnimal : MonoBehaviour
    {
        [Tooltip("The actual model of the animal that will be teleported to the boat.")]
        [SerializeField] private Transform animalModel;

        public Transform AnimalModel => animalModel;

        private void Reset()
        {
            // Try to find the first child as the default model if not assigned
            if (animalModel == null && transform.childCount > 0)
            {
                animalModel = transform.GetChild(0);
            }
        }
    }
}
