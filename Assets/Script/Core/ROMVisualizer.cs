using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhackAMole.Debugging
{
    /// <summary>
    /// Attach this script to the Player's Shoulder or Head in the Scene.
    /// It uses Unity Gizmos to draw the 3D ROM reach boundaries based on the assigned RehabProfile.
    /// </summary>
    public class ROMVisualizer : MonoBehaviour
    {
        [Header("Profile Setup")]
        [Tooltip("The Rehab Profile to visualize. Drag your ScriptableObject here.")]
        public RehabProfileSO rehabProfile;

        [Header("Visualization Settings")]
        [Tooltip("The Transform representing the player's shoulder origin. Defaults to this object if left blank.")]
        public Transform shoulderOrigin;

        [Tooltip("Color of the Flexion (Reaching Forward/Up) Arc")]
        public Color flexionColor = new Color(0f, 0.5f, 1f, 1f); // Blue

        [Tooltip("Color of the Abduction (Reaching Side) Arc")]
        public Color abductionColor = new Color(1f, 0.2f, 0.2f, 1f); // Red

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            // Try to find a profile to draw.
            RehabProfileSO profileToDraw = rehabProfile;
            
            // If playing the game, dynamically grab the active profile being used.
            if (profileToDraw == null && Application.isPlaying && GameManager.Instance != null)
            {
                profileToDraw = GameManager.Instance.RehabProfile;
            }

            // If no profile is found, we can't draw anything.
            if (profileToDraw == null) return;

            Transform origin = shoulderOrigin != null ? shoulderOrigin : transform;
            
            Vector3 center = origin.position;
            Vector3 down = Vector3.down; // 0 degrees is usually arms resting straight down

            // Safety fallback just in case arm length is 0 in the profile
            float radius = profileToDraw.armLength;
            if (radius <= 0.1f) radius = 0.6f; 

            // ---------------------------------------------------------
            // 1. Draw Flexion Arc (Forward & Up movement)
            // ---------------------------------------------------------
            Handles.color = flexionColor;
            
            // Rotate around the Right axis to sweep the arc forward and up
            Handles.DrawWireArc(center, origin.right, down, profileToDraw.maxFlexion, radius);
            
            // Draw a line connecting the shoulder to the maximum Flexion limit
            Vector3 flexionLimitVector = Quaternion.AngleAxis(profileToDraw.maxFlexion, origin.right) * down;
            Handles.DrawLine(center, center + flexionLimitVector * radius);

            // ---------------------------------------------------------
            // 2. Draw Abduction Arc (Side movement)
            // ---------------------------------------------------------
            Handles.color = abductionColor;
            
            // For Abduction, rotation axis is Forward. Reverse the sweep direction for Left vs Right arm.
            Vector3 sweepAxis = profileToDraw.isLeftArm ? -origin.forward : origin.forward;
            
            Handles.DrawWireArc(center, sweepAxis, down, profileToDraw.maxAbduction, radius);
            
            // Draw a line connecting the shoulder to the maximum Abduction limit
            Vector3 abductionLimitVector = Quaternion.AngleAxis(profileToDraw.maxAbduction, sweepAxis) * down;
            Handles.DrawLine(center, center + abductionLimitVector * radius);

            // ---------------------------------------------------------
            // 3. Draw faint Wireframe sphere for overall Reach Volume
            // ---------------------------------------------------------
            Gizmos.color = new Color(1f, 1f, 1f, 0.8f); // Bright white grid
            Gizmos.DrawWireSphere(center, radius);

            // Draw a tiny solid sphere at the actual shoulder origin point
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(center, 0.05f);
            
            // Draw bright colored wedges to make the arcs pop out more!
            Handles.color = new Color(flexionColor.r, flexionColor.g, flexionColor.b, 0.4f);
            float flexAngle = Mathf.Max(profileToDraw.maxFlexion, 5f); // Ensure at least 5 degrees so it's always visible
            Handles.DrawSolidArc(center, origin.right, down, flexAngle, radius);
            
            Handles.color = new Color(abductionColor.r, abductionColor.g, abductionColor.b, 0.4f);
            float abAngle = Mathf.Max(profileToDraw.maxAbduction, 5f);
            Handles.DrawSolidArc(center, sweepAxis, down, abAngle, radius);
#endif
        }
    }
}
