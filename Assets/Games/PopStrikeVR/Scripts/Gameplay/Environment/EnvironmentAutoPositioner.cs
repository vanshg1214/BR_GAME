using System.Collections;
using UnityEngine;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// Dynamically aligns the VR environment (chair, floor, props) to the player's true physical position
    /// and rotation once tracking is initialized. It also listens for Oculus Recenter events.
    /// </summary>
    public class EnvironmentAutoPositioner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The player's VR Camera (Center Eye). Used to find their physical head height and position.")]
        public Transform playerCamera;

        [Header("Positioning")]
        [Tooltip("How far below the player's eyes the environment should spawn. E.g., -1.0 puts the floor near feet level and the chair around waist level.")]
        [Range(-2f, 2f)]
        public float heightOffset = -1.0f;

        [Tooltip("If true, the environment will automatically rotate to match the direction the player is looking when the game starts or recenters.")]
        public bool matchPlayerRotation = true;
        
        [Tooltip("Optional rotation offset if the environment model isn't facing perfectly forward (Z+).")]
        public Vector3 rotationOffset = Vector3.zero;

        private void OnEnable()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose += OnRecentered;
            }
        }

        private void OnDisable()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose -= OnRecentered;
            }
        }

        private void OnRecentered()
        {
            Debug.Log($"[EnvironmentAutoPositioner] Headset recentered event received. Repositioning {gameObject.name}...");
            StartCoroutine(DelayedPositionEnvironment());
        }

        private IEnumerator DelayedPositionEnvironment()
        {
            yield return new WaitForSeconds(0.5f);
            PositionEnvironment();
        }

        private IEnumerator Start()
        {
            // Wait for half a second to allow the Meta Quest headset to find its tracking position
            // (In Editor it's instant, but on device it takes a few frames to initialize the Guardian bounds)
            yield return new WaitForSeconds(0.5f);
            
            PositionEnvironment();
        }

        /// <summary>
        /// Calculates and sets the final position and rotation of the Environment.
        /// </summary>
        public void PositionEnvironment()
        {
            // Auto-find main camera if none was assigned
            if (playerCamera == null)
            {
                if (Camera.main != null)
                {
                    playerCamera = Camera.main.transform;
                }
                else
                {
                    Debug.LogWarning("[EnvironmentAutoPositioner] No player camera assigned or found! Cannot position environment.");
                    return;
                }
            }

            // 1. Calculate Position
            // Center the environment exactly on the player's X and Z coordinates, but apply the height offset to Y.
            Vector3 targetPosition = new Vector3(playerCamera.position.x, playerCamera.position.y + heightOffset, playerCamera.position.z);
            transform.position = targetPosition;

            // 2. Calculate Rotation
            if (matchPlayerRotation)
            {
                Vector3 forward = playerCamera.forward;
                forward.y = 0; // Flatten the forward vector so the floor doesn't tilt up or down

                if (forward.sqrMagnitude > 0.001f)
                {
                    forward.Normalize();
                    Quaternion targetRotation = Quaternion.LookRotation(forward);
                    // Apply any custom offset the designer added in the inspector
                    transform.rotation = targetRotation * Quaternion.Euler(rotationOffset);
                }
            }
            
            Debug.Log($"[EnvironmentAutoPositioner] Positioned environment at {transform.position} with height offset {heightOffset}");
        }
    }
}
