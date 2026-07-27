using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Lifecycle manager attached to animal mole prefabs. Handles spawning/despawning
    /// targets (like bottles or boxes) when retrieved from/returned to the object pool.
    /// </summary>
    public class MoleTargetEquip : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The destructible prefab to spawn (e.g., the Bottle prefab).")]
        [SerializeField] private GameObject targetPrefab;

        [Tooltip("The bone joint on the animal's head armature where the target should be attached.")]
        [SerializeField] private Transform attachJoint;

        [Tooltip("Local Position offset relative to the attach joint.")]
        [SerializeField] private Vector3 localPositionOffset = Vector3.zero;

        [Tooltip("Local Rotation offset relative to the attach joint (Euler angles).")]
        [SerializeField] private Vector3 localRotationOffset = Vector3.zero;

        private GameObject currentTargetInstance;

        private void OnEnable()
        {
            SpawnTarget();
        }

        private void OnDisable()
        {
            CleanUpTarget();
        }

        private void SpawnTarget()
        {
            if (targetPrefab == null || attachJoint == null) return;

            // Clean up any old instance just in case
            CleanUpTarget();

            // Instantiate a fresh target inside the head bone hierarchy
            currentTargetInstance = Instantiate(targetPrefab, attachJoint);
            
            // Apply offsets so it sits perfectly on the head
            currentTargetInstance.transform.localPosition = localPositionOffset;
            currentTargetInstance.transform.localRotation = Quaternion.Euler(localRotationOffset);
            currentTargetInstance.transform.localScale = Vector3.one;

            // Add the hit bridge component if it isn't already on the prefab
            if (currentTargetInstance.GetComponent<HittableExplosive>() == null)
            {
                currentTargetInstance.AddComponent<HittableExplosive>();
            }
        }

        private void CleanUpTarget()
        {
            if (currentTargetInstance != null)
            {
                Destroy(currentTargetInstance);
                currentTargetInstance = null;
            }
        }
    }
}
