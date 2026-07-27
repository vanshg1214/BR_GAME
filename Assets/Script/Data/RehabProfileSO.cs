using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Patient profile containing ROM limits and physical measurements.
    /// Created per-patient via Assets > Create > WhackAMole > Data > Rehab Profile.
    /// The therapist adjusts these values through the dashboard before each session.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRehabProfile", menuName = "WhackAMole/Data/Rehab Profile")]
    public class RehabProfileSO : ScriptableObject
    {
        public enum HandMode { Left, Right, Both }

        [Header("Patient Info")]
        [Tooltip("Patient name — shown on the HUD and session reports.")]
        public string patientName = "Sujal";

        [Tooltip("Which hand(s) are active for the session.")]
        public HandMode handMode = HandMode.Both;

        [Tooltip("True if the patient is exercising their left arm this session.")]
        public bool isLeftArm = true;

        [Tooltip("Approximate arm length in metres, used to scale the workspace grid.")]
        [Range(0.3f, 1.2f)]
        public float armLength = 0.6f;

        [Header("Shoulder ROM Limits (Degrees)")]
        [Range(0, 180)] public float maxFlexion = 90f;
        [Range(0, 180)] public float maxAbduction = 90f;
        [Tooltip("Max horizontal adduction/abduction angle (symmetric sweep)")]
        [Range(0, 180)] public float shoulderHorizontalAdductionMax = 90f;

    }
}
