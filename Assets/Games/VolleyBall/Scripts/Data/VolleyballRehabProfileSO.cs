using UnityEngine;

namespace Rehab.Volleyball.Data
{
    /// <summary>
    /// ScriptableObject for the Volleyball game's patient rehab profile.
    /// Separate from other games but follows the same structural parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "NewVolleyballRehabProfile", menuName = "Rehab/Volleyball/Data/Rehab Profile")]
    public class VolleyballRehabProfileSO : ScriptableObject
    {
        public enum HandMode { Left, Right, Both }

        [Header("Patient Info")]
        [Tooltip("Patient name — loaded from save profile.")]
        public string patientName = "Player";

        [Tooltip("Which hand(s) are active for the session.")]
        public HandMode handMode = HandMode.Both;

        [Tooltip("True if the patient is exercising their left arm this session.")]
        public bool isLeftArm = true;

        [Header("Calibration Data")]
        [Tooltip("Arm length in meters, used to scale the reaching boundaries.")]
        public float armLength = 0.6f;

        [Tooltip("Max shoulder flexion angle in degrees.")]
        public float maxFlexion = 120f;

        [Tooltip("Max shoulder abduction angle in degrees.")]
        public float maxAbduction = 90f;

        [Tooltip("Max horizontal adduction/abduction sweep angle.")]
        public float shoulderHorizontalAdductionMax = 90f;
        
        [Tooltip("Safety margin percentage (0.0 to 1.0) to keep targets strictly within a safe inner boundary of the absolute maximum ROM. E.g., 0.15 = 15% safety margin.")]
        public float safetyMargin = 0.15f;
    }
}
