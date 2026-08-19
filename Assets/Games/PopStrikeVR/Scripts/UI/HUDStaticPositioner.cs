using UnityEngine;

namespace PopstrikeVR.UI
{
    /// <summary>
    /// Professionally positions the HUD at session start relative to the player's head height, 
    /// but locked to the room/gameplay forward direction. 
    /// This prevents the UI from spawning off-center if the player happens to be looking 
    /// over their shoulder when the scene loads.
    /// </summary>
    public class HUDStaticPositioner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The player's VR Camera (Center Eye). Used to find their physical head height and position.")]
        public Transform playerCamera;
        
        [Tooltip("Optional: The object that defines the 'True Forward' where balloons spawn (e.g., the Camera Rig root or calibration center). If empty, world forward (Z+) is used.")]
        public Transform roomForwardReference;

        [Header("Positioning Parameters")]
        [Tooltip("How far in front of the player the UI should spawn (in meters).")]
        [Range(0.5f, 10f)]
        public float distance = 1.5f;
        
        [Tooltip("Height offset relative to the player's eyes. Negative values place it below eye level so it doesn't block balloons.")]
        [Range(-5f, 5f)]
        public float heightOffset = -0.4f;

        [Tooltip("Should the UI angle itself to face the camera exactly?")]
        public bool lookAtCamera = true;

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
            Debug.Log($"[HUDStaticPositioner] Headset recentered event received. Repositioning {gameObject.name}...");
            StartCoroutine(DelayedPositionUI());
        }

        private System.Collections.IEnumerator DelayedPositionUI()
        {
            yield return new WaitForSeconds(0.5f);
            PositionUI();
        }

        private System.Collections.IEnumerator Start()
        {
            // Wait for half a second to allow the Meta Quest headset to find its tracking position
            // (In Editor it's instant, but on device it takes a few frames)
            yield return new WaitForSeconds(0.5f);
            
            // Position the UI once tracking is stable
            PositionUI();
        }

        /// <summary>
        /// Calculates and sets the final position of the Canvas. 
        /// Can be called manually if you need to re-center the UI during gameplay.
        /// </summary>
        public void PositionUI()
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
                    Debug.LogWarning("[HUDStaticPositioner] No player camera assigned or found! Cannot position UI.");
                    return;
                }
            }

            // 1. Determine "True Forward" (The direction balloons are spawning)
            // If the user has a specific play-space root, use its forward. Otherwise use world Z-forward.
            Vector3 trueForward = roomForwardReference != null ? roomForwardReference.forward : Vector3.forward;
            
            // Flatten the forward vector on the Y axis so the UI doesn't accidentally spawn tilted into the floor or ceiling
            trueForward.y = 0;
            if (trueForward.sqrMagnitude > 0.001f)
            {
                trueForward.Normalize();
            }
            else
            {
                trueForward = Vector3.forward;
            }

            // 2. Calculate Position
            // Start exactly at the head, move out by True Forward (room forward), then adjust the height.
            Vector3 targetPosition = playerCamera.position + (trueForward * distance);
            targetPosition.y = playerCamera.position.y + heightOffset;

            transform.position = targetPosition;

            // 3. Calculate Rotation
            if (lookAtCamera)
            {
                // Make the canvas perfectly face the player's head, but keep it strictly vertical (no tilting backward)
                Vector3 lookDirection = transform.position - playerCamera.position;
                lookDirection.y = 0; 
                
                if (lookDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDirection);
                }
            }
            else
            {
                // Just face the room forward exactly
                transform.rotation = Quaternion.LookRotation(trueForward);
            }

            Debug.Log($"[HUDStaticPositioner] UI Locked into position at {transform.position} based on player head height and room forward.");
        }
    }
}
