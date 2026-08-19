using System.Collections;
using UnityEngine;
using PopstrikeVR.Data;

namespace PopstrikeVR.Core
{
    /// <summary>
    /// Samples the player's maximum reach points across a 180° front hemisphere.
    /// Updates the PatientProfileSO with accurate ReachRadius, MaxElevation, and MaxAzimuth.
    /// </summary>
    public class ROMCalibrationManager : MonoBehaviour
    {
        [Header("References")]
        public PatientProfileSO patientProfile;
        public Transform playerHead;
        public Transform leftHand;
        public Transform rightHand;

        [Header("Settings")]
        public float calibrationDuration = 10f; // Give patient 10 seconds to reach around

        private float maxRadius = 0f;
        private float maxElevation = 0f;
        private float maxAzimuth = 0f;

        private bool isCalibrating = false;

        public void StartCalibration()
        {
            if (!isCalibrating)
            {
                StartCoroutine(CalibrationRoutine());
            }
        }

        private IEnumerator CalibrationRoutine()
        {
            isCalibrating = true;
            Debug.Log("[ROM Calibration] Started! Please reach as far as possible in all directions.");
            
            // Reset temp variables
            maxRadius = 0.3f; // Minimum baseline
            maxElevation = 0f;
            maxAzimuth = 0f;

            float timer = 0f;

            while (timer < calibrationDuration)
            {
                timer += Time.deltaTime;
                SampleHand(leftHand);
                SampleHand(rightHand);
                yield return null;
            }

            // Save to Profile
            if (patientProfile != null)
            {
                patientProfile.ReachRadius = maxRadius;
                patientProfile.MaxElevation = maxElevation;
                patientProfile.MaxAzimuth = maxAzimuth;
                
                // Note: In a full project, you would call EditorUtility.SetDirty(patientProfile) 
                // if running in editor, or serialize to JSON. For runtime ScriptableObjects, changes hold until application quit.
            }

            Debug.Log($"[ROM Calibration] Complete! Radius: {maxRadius:0.00}m, Elevation: {maxElevation:0.0}°, Azimuth: {maxAzimuth:0.0}°");
            isCalibrating = false;

            // Trigger visual feedback
            PopstrikeFeedbackManager.Instance?.PlayTrailFlourish();
        }

        private void SampleHand(Transform hand)
        {
            if (hand == null || playerHead == null) return;

            Vector3 shoulderBase = playerHead.position - new Vector3(0, 0.2f, 0); // Approx shoulder level
            Vector3 handDir = hand.position - shoulderBase;
            
            float distance = handDir.magnitude;
            
            // 1. Update Radius (Max Reach)
            if (distance > maxRadius) maxRadius = distance;

            // Normalize for angle calculations
            Vector3 handDirFlat = new Vector3(handDir.x, 0, handDir.z).normalized;
            Vector3 forwardFlat = new Vector3(playerHead.forward.x, 0, playerHead.forward.z).normalized;

            // 2. Update Azimuth (Horizontal Spread from center)
            float azimuth = Vector3.Angle(forwardFlat, handDirFlat);
            if (azimuth > maxAzimuth && handDir.z > 0) // Only count forward hemisphere
            {
                maxAzimuth = Mathf.Min(azimuth, 90f); // Cap at 90 degrees left/right
            }

            // 3. Update Elevation (Vertical Spread)
            float elevation = Vector3.Angle(Vector3.up, handDir.normalized);
            // Angle from Up vector: 90 is forward, 0 is straight up. 
            // We want Elevation to be 0 at horizon, 90 at straight up.
            float mappedElevation = 90f - elevation; 
            
            if (mappedElevation > maxElevation)
            {
                maxElevation = Mathf.Min(mappedElevation, 90f);
            }
        }
    }
}
