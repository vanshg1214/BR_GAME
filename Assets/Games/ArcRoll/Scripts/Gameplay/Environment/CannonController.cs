using UnityEngine;
using ArcRoll.Core;

namespace ArcRoll.Gameplay
{
    public class CannonController : MonoBehaviour
    {
        [Header("Cannon Setup")]
        [SerializeField] private Transform firePoint;
        [Tooltip("The default ball prefab to shoot if none is passed via method.")]
        [SerializeField] private GameObject defaultBallPrefab;

        [Header("Profile Configuration")]
        [Tooltip("Drag the runtime ArcRollRehabProfileSO reference here.")]
        [SerializeField] private ArcRollRehabProfileSO activeProfile;

        [Header("Model Fix")]
        [Tooltip("Forces the cannon to stand up if the 3D model imported sideways.")]
        [SerializeField] private Vector3 forcedEulerOffset = new Vector3(-90f, 0f, 0f);

        [Header("Effects")]
        [Tooltip("The sound effect to play when the cannon fires a ball.")]
        [SerializeField] private AudioClip cannonFireSound;
        [Range(0f, 1f)] [SerializeField] private float fireSoundVolume = 1.0f;
        private AudioSource audioSource;

        [Header("Queue Manager")]
        [Tooltip("Drag the BallQueueManager from the scene here.")]
        [SerializeField] private BallQueueManager ballQueueManager;

        // ROM settings moved to LevelDirector

        [Header("Physics Settings")]
        [Tooltip("How fast the ball travels straight to the target in meters per second.")]
        [SerializeField] private float travelSpeed = 8f;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.minDistance = 3.0f;
            audioSource.maxDistance = 30.0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.playOnAwake = false;
        }

        /// <summary>
        /// Aims the cannon at a target!
        /// Uses world-space math so it works regardless of parent rotations.
        /// </summary>
        public void AimAtTarget(Vector3 targetPosition)
        {
            // ── Step 1: Rotate the cannon to face the target (flat on the floor) ──
            Vector3 flatLookPos = targetPosition;
            flatLookPos.y = transform.position.y;
            transform.LookAt(flatLookPos);
            
            // Force the euler offset so the cannon model stands upright!
            Vector3 currentEuler = transform.eulerAngles;
            transform.eulerAngles = new Vector3(forcedEulerOffset.x, currentEuler.y + forcedEulerOffset.y, forcedEulerOffset.z);
        }

        /// <summary>
        /// Shoots a ball towards the target position. Designed for general target tracking.
        /// </summary>
        public Ball ShootAtTarget(Vector3 targetPosition, Vector3 targetHoopPosition, GameObject overrideBallPrefab = null, GameObject romRing = null, Transform liveTargetTransform = null)
        {
            AimAtTarget(targetPosition);
            
            GameObject prefabToShoot = overrideBallPrefab != null ? overrideBallPrefab : defaultBallPrefab;
            
            if (prefabToShoot == null || firePoint == null)
            {
                Debug.LogWarning("CannonController: Missing prefab or fire point!");
                return null;
            }

            GameObject ballObj = Instantiate(prefabToShoot, firePoint.position, firePoint.rotation);
            
            if (audioSource != null && cannonFireSound != null)
            {
                audioSource.PlayOneShot(cannonFireSound, fireSoundVolume);
            }
            
            if (ballObj.TryGetComponent<Ball>(out Ball ballComponent))
            {
                // Register with the queue manager BEFORE firing so state tracking starts
                ballQueueManager?.RegisterBall(ballComponent);
                ballComponent.FireToTarget(targetPosition, targetHoopPosition, travelSpeed, romRing, liveTargetTransform);
                return ballComponent;
            }
            else if (ballObj.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                // Fallback if the object doesn't have a Ball script
                Vector3 direction = (targetPosition - firePoint.position).normalized;
                rb.linearVelocity = direction * travelSpeed;
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(firePoint.position, 0.12f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(firePoint.position, firePoint.forward * 0.4f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (firePoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(firePoint.position, 0.15f);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(firePoint.position, firePoint.forward * 0.8f);
            }
        }
    }
}
