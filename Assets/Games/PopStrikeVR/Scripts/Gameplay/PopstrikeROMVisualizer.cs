using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using PopstrikeVR.Data;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// Visualizes the calculated Range of Motion (ROM) in the Unity Editor based on the Patient Profile.
    /// This helps designers understand exactly where the balloons can spawn.
    /// </summary>
    public class PopstrikeROMVisualizer : MonoBehaviour
    {
        [Header("Profile Setup")]
        [Tooltip("The central anchor for mapping, typically the VR Camera tracking the player's head. If null, uses this object's transform.")]
        public Transform HeadOrigin;

        [Tooltip("The profile used to visualize the patient's safe limits.")]
        public PatientProfileSO PatientProfile;

        [Header("Visualization Settings")]
        public Color ColorAzimuth = new Color(0f, 1f, 0f, 1f); // Green for Horizontal
        public Color ColorElevation = new Color(0f, 0.5f, 1f, 1f); // Blue for Vertical

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (PatientProfile == null) return;
            
            Transform origin = HeadOrigin != null ? HeadOrigin : transform;

            Vector3 center = origin.position;
            Vector3 forward = origin.forward; // 0 degrees is straight ahead
            float radius = PatientProfile.GetSafeRadius();

            if (radius <= 0.1f) radius = 0.5f;

            // ---------------------------------------------------------
            // 1. Draw Azimuth Arc (Horizontal sweeping left/right)
            // ---------------------------------------------------------
            Handles.color = ColorAzimuth;
            
            // Start at the far-left limit and sweep to the right
            Vector3 startAzimuth = Quaternion.AngleAxis(-PatientProfile.MaxAzimuth, origin.up) * forward;
            Handles.DrawWireArc(center, origin.up, startAzimuth, PatientProfile.MaxAzimuth * 2f, radius);
            
            // Draw lines connecting the head to the maximum Azimuth limits
            Vector3 endAzimuth = Quaternion.AngleAxis(PatientProfile.MaxAzimuth, origin.up) * forward;
            Handles.DrawLine(center, center + startAzimuth * radius);
            Handles.DrawLine(center, center + endAzimuth * radius);

            // ---------------------------------------------------------
            // 2. Draw Elevation Arc (Vertical sweeping up/down)
            // ---------------------------------------------------------
            Handles.color = ColorElevation;
            
            // Start at the lowest limit and sweep upwards
            Vector3 startElevation = Quaternion.AngleAxis(PatientProfile.MaxElevation, origin.right) * forward; 
            Vector3 endElevation = Quaternion.AngleAxis(-PatientProfile.MaxElevation, origin.right) * forward;

            // Sweep upward (negative rotation around Right axis)
            Handles.DrawWireArc(center, -origin.right, startElevation, PatientProfile.MaxElevation * 2f, radius);

            Handles.DrawLine(center, center + startElevation * radius);
            Handles.DrawLine(center, center + endElevation * radius);

            // ---------------------------------------------------------
            // 3. Draw faint Wireframe sphere for overall Reach Volume
            // ---------------------------------------------------------
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f); // Faint white grid
            Gizmos.DrawWireSphere(center, radius);

            // Draw a tiny solid sphere at the actual head origin point
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(center, 0.05f);
            
            // ---------------------------------------------------------
            // 4. Draw bright colored solid wedges
            // ---------------------------------------------------------
            Handles.color = new Color(ColorAzimuth.r, ColorAzimuth.g, ColorAzimuth.b, 0.2f);
            float azAngle = Mathf.Max(PatientProfile.MaxAzimuth * 2f, 5f);
            Handles.DrawSolidArc(center, origin.up, startAzimuth, azAngle, radius);
            
            Handles.color = new Color(ColorElevation.r, ColorElevation.g, ColorElevation.b, 0.2f);
            float elAngle = Mathf.Max(PatientProfile.MaxElevation * 2f, 5f);
            Handles.DrawSolidArc(center, -origin.right, startElevation, elAngle, radius);
#endif
        }
    }
}
