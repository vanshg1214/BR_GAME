using UnityEngine;

namespace ArcRoll.UI
{
    /// <summary>
    /// Professional VR UI Helper.
    /// When this GameObject is enabled/activated, it places itself directly in front of the 
    /// player's current view at a customizable Z distance and eye level, but remains completely 
    /// stationary in the world so it does NOT follow head movements (preventing motion sickness).
    /// </summary>
    public class VRAlignToCamera : MonoBehaviour
    {
        [Header("Positioning Settings")]
        [Tooltip("The Z distance (in meters) to place the UI in front of the player.")]
        [SerializeField] private float zDistance = 1.5f;

        [Tooltip("If checked, the UI will match the player's exact eye height. If unchecked, it keeps its original height.")]
        [SerializeField] private bool matchEyeHeight = true;

        [Tooltip("If checked, the UI will rotate to face the player's view direction.")]
        [SerializeField] private bool rotateToFacePlayer = true;

        [Tooltip("Optional: If assigned, the script will use this specific camera. Otherwise, it searches for the Main Camera or CenterEyeAnchor.")]
        [SerializeField] private Transform vrCameraOverride;

        private void OnEnable()
        {
            AlignNow();
        }

        /// <summary>
        /// Forces the UI to immediately teleport in front of the player.
        /// Call this right before showing popup text if the GameObject is permanently enabled.
        /// </summary>
        public void AlignNow()
        {
            Transform vrCamera = GetVRCamera();
            if (vrCamera == null)
            {
                Debug.LogWarning("[VRAlignToCamera] VR Camera not found! Unable to reposition UI.");
                return;
            }

            // Get camera position and direction
            Vector3 cameraPos = vrCamera.position;
            Vector3 cameraForward = vrCamera.forward;

            // Flatten the forward direction so the panel doesn't tilt up/down if the player is looking up/down
            cameraForward.y = 0;
            if (cameraForward.sqrMagnitude < 0.01f)
            {
                cameraForward = vrCamera.forward; // Fallback
            }
            cameraForward.Normalize();

            // Calculate new position
            Vector3 targetPos = cameraPos + cameraForward * zDistance;

            // Apply height
            if (!matchEyeHeight)
            {
                targetPos.y = transform.position.y;
            }

            // Move the UI to the calculated spot
            transform.position = targetPos;

            // Rotate to face the player's view direction
            if (rotateToFacePlayer)
            {
                transform.rotation = Quaternion.LookRotation(cameraForward);
            }
        }

        private Transform GetVRCamera()
        {
            if (vrCameraOverride != null) return vrCameraOverride;

            if (Camera.main != null) return Camera.main.transform;

            // Look for Oculus CenterEyeAnchor
            GameObject centerEye = GameObject.Find("CenterEyeAnchor");
            if (centerEye != null) return centerEye.transform;

            return null;
        }
    }
}
