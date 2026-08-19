using UnityEngine;

namespace PopstrikeVR.UI
{
    /// <summary>
    /// Shows a 3D arrow floating in front of the player's face if they look too far away 
    /// from the gameplay area, guiding them back to the center.
    /// </summary>
    public class DirectionalGuidanceArrow : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The VR Camera. The arrow will float in front of this.")]
        public Transform playerCamera;
        
        [Tooltip("The object that defines the 'True Forward' of the room. Leave empty to use World Z+ (the direction balloons spawn).")]
        public Transform roomForwardReference;
        
        [Tooltip("The actual 3D Arrow model/prefab. This will be turned on/off.")]
        public GameObject arrowVisuals;

        [Header("Settings")]
        [Tooltip("If the player looks this many degrees away from the center, the arrow appears.")]
        [Range(10f, 90f)]
        public float showThresholdAngle = 40f;
        
        [Tooltip("How far in front of the camera the arrow floats.")]
        public float floatDistance = 1.0f;
        
        [Tooltip("Height offset from the camera center (negative = slightly below eyes).")]
        public float heightOffset = -0.2f;

        [Header("Animation")]
        [Tooltip("How fast the arrow bobs back and forth to grab attention.")]
        public float bobSpeed = 6f;
        
        [Tooltip("How far the arrow bobs along its pointing direction.")]
        public float bobDistance = 0.05f;

        private void Start()
        {
            // Auto-find camera if not assigned
            if (playerCamera == null && Camera.main != null)
            {
                playerCamera = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (playerCamera == null || arrowVisuals == null) return;

            // 1. Get the direction the balloons are spawning in (True Forward)
            Vector3 trueForward = roomForwardReference != null ? roomForwardReference.forward : Vector3.forward;
            trueForward.y = 0; // Flatten to horizontal plane
            if (trueForward.sqrMagnitude > 0.001f) trueForward.Normalize(); else trueForward = Vector3.forward;

            // 2. Get the direction the player is currently looking
            Vector3 headForward = playerCamera.forward;
            headForward.y = 0; // Flatten to horizontal plane
            if (headForward.sqrMagnitude > 0.001f) headForward.Normalize(); else headForward = Vector3.forward;

            // 3. Compare the two angles
            float angle = Vector3.Angle(headForward, trueForward);

            // 4. Show or hide the arrow based on the angle
            if (angle > showThresholdAngle)
            {
                if (!arrowVisuals.activeSelf) arrowVisuals.SetActive(true);

                // Point the arrow towards the true forward (the balloons)
                Quaternion targetRot = Quaternion.LookRotation(trueForward);
                transform.rotation = targetRot;

                // Create a smooth sweeping animation across the screen
                // Time.time * speed determines how fast it swipes
                float animSpeed = bobSpeed * 0.25f; // scale down speed to match old bobSpeed variable
                float animT = Mathf.Repeat(Time.time * animSpeed, 1f);
                
                // Use a smooth ease-out curve so it zips across and slows down near the target direction
                float easeT = 1f - Mathf.Pow(1f - animT, 3f);
                
                // Interpolate from the center of the player's view (headForward) 
                // towards the target direction (trueForward). 
                // We multiply by 0.65f so it stays within their FOV and doesn't wrap behind their head!
                Vector3 animDir = Vector3.Slerp(headForward, trueForward, easeT * 0.65f);

                // Position the arrow along this interpolated direction
                Vector3 basePosition = playerCamera.position + (animDir * floatDistance);
                basePosition.y = playerCamera.position.y + heightOffset;
                
                transform.position = basePosition;
            }
            else
            {
                // The player is looking at the balloons, hide the arrow!
                if (arrowVisuals.activeSelf) arrowVisuals.SetActive(false);
            }
        }
    }
}
