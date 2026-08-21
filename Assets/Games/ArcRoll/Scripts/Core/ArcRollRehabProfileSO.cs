using UnityEngine;

namespace ArcRoll.Core
{
    [CreateAssetMenu(fileName = "NewArcRollRehabProfile", menuName = "ArcRoll/Data/Rehab Profile")]
    public class ArcRollRehabProfileSO : ScriptableObject
    {
        [Header("Patient Info")]
        public string patientName = "Player";
        public bool isLeftArm = true;

        [Header("Calibrated Limits")]
        public float armLength = 0.6f;
        public float maxFlexion = 90f;
        public float maxAbduction = 90f;
        
        [Tooltip("How far the arm can reach inward, across the chest to the opposite shoulder.")]
        public float maxAdduction = 45f;
    }
}
