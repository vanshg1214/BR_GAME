using UnityEngine;

namespace PopstrikeVR.Data
{
    /// <summary>
    /// Stores patient-specific clinical configuration such as Range of Motion (ROM) 
    /// limitations and gesture detection thresholds.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPatientProfile", menuName = "PopstrikeVR/Data/Patient Profile", order = 1)]
    public class PatientProfileSO : ScriptableObject
    {
        [Header("Calibration Data")]
        [Tooltip("The calibrated maximum reach radius (in meters) for this patient.")]
        public float ReachRadius = 0.8f;

        [Tooltip("Minimum vertical angle (e.g. -30) the patient can safely reach down.")]
        public float MinElevation = -30f;

        [Tooltip("Maximum vertical angle (shoulder flexion) the patient can reach safely up.")]
        public float MaxElevation = 60f;

        [Tooltip("Minimum horizontal angle (e.g. -45) the patient can safely reach inward.")]
        public float MinAzimuth = -45f;

        [Tooltip("Maximum horizontal angle (shoulder abduction/adduction) the patient can reach safely outward.")]
        public float MaxAzimuth = 60f;

        [Tooltip("Safety margin scalar (default 0.85). Target spawns will be multiplied by this to prevent over-extension.")]
        [Range(0.5f, 1.0f)]
        public float SafetyMargin = 0.85f;

        [Header("Gesture Thresholds")]
        [Tooltip("Minimum confidence required for the CLOSED_FIST gesture to register.")]
        [Range(0f, 1f)]
        public float FistConfidenceThreshold = 0.8f;
        
        [Tooltip("Minimum confidence required for the OPEN_BLADE gesture to register.")]
        [Range(0f, 1f)]
        public float BladeConfidenceThreshold = 0.8f;

        [Tooltip("Minimum confidence required for the INDEX_POINT gesture to register.")]
        [Range(0f, 1f)]
        public float PointConfidenceThreshold = 0.8f;

        [Header("Gameplay Thresholds")]
        [Tooltip("Minimum wrist velocity (m/s) required to successfully pop a Blaze balloon.")]
        public float MinimumPunchVelocity = 1.5f;

        /// <summary>
        /// Calculates the final safe working radius based on calibration and safety margin.
        /// </summary>
        public float GetSafeRadius()
        {
            return ReachRadius * SafetyMargin;
        }
    }
}
