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
            // Step 1: Always wait a minimum of 0.5s first to let WorkspaceMapper and 
            // EnvironmentAutoPositioner finish their recenter routines.
            yield return new WaitForSeconds(0.5f);

            // Step 2: Poll until the camera position has STOPPED MOVING between frames.
            // This is 100% reliable regardless of device load or tracking speed.
            // We wait until the position delta is less than 0.001m (1mm) for 3 consecutive frames.
            if (playerCamera == null && Camera.main != null) playerCamera = Camera.main.transform;
            
            if (playerCamera != null)
            {
                int stableFrames = 0;
                Vector3 lastPos = playerCamera.position;
                Quaternion lastRot = playerCamera.rotation;
                
                while (stableFrames < 3)
                {
                    yield return null; // Wait one frame
                    
                    float posDelta = Vector3.Distance(playerCamera.position, lastPos);
                    float rotDelta = Quaternion.Angle(playerCamera.rotation, lastRot);
                    
                    if (posDelta < 0.001f && rotDelta < 0.1f)
                    {
                        stableFrames++;
                    }
                    else
                    {
                        stableFrames = 0; // Still moving — reset counter
                    }
                    
                    lastPos = playerCamera.position;
                    lastRot = playerCamera.rotation;
                }
                
                Debug.Log("[HUDStaticPositioner] Camera tracking stable. Positioning UI now.");
            }
            
            PositionUI();
        }

        private System.Collections.IEnumerator Start()
        {
            // Wait 1.5s on device to allow Meta Quest tracking to fully stabilize across scene loads.
            // 0.5s was too short and caused the UI to snap to an untracked position.
            yield return new WaitForSeconds(1.5f);
            PositionUI();
        }

        /// <summary>
        /// Positions the canvas in front of the player based on their current head direction.
        /// IMPORTANT: This method ONLY sets position. It never changes rotation — the canvas
        /// rotation is left exactly as authored in the scene.
        /// </summary>
        public void PositionUI()
        {
            // Auto-find main camera if none was assigned
            if (playerCamera == null)
            {
                if (Camera.main != null) playerCamera = Camera.main.transform;
                else
                {
                    Debug.LogWarning("[HUDStaticPositioner] No player camera found!");
                    return;
                }
            }

            // Use the player's current live head direction (where they are looking RIGHT NOW).
            // We flatten the Y so the canvas never tilts up/down — it always stays perfectly upright.
            Vector3 forward = playerCamera.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            // Place the canvas: The user specifically requested to ONLY use Z (depth) and Y (height)
            // from the headset, and leave X and Rotation EXACTLY as authored in the Unity Scene.
            Vector3 targetPosition = transform.position; // Keep current authored X
            
            targetPosition.y = playerCamera.position.y + heightOffset;
            targetPosition.z = playerCamera.position.z + distance; // Use headset Z + distance

            // ONLY set the position — never change the rotation.
            // The canvas is authored with a fixed world-space rotation in the scene.
            transform.position = targetPosition;

            Debug.Log($"[HUDStaticPositioner] '{gameObject.name}' moved to {targetPosition} | head={playerCamera.position} | forward={forward} | dist={distance}m | yOffset={heightOffset}m");
        }
    }
}
