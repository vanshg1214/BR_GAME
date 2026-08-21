using UnityEngine;

namespace ArcRoll.Gameplay.Obstacles
{
    public class TimedGateObstacle : MonoBehaviour
    {
        [Header("Gate Parts")]
        [Tooltip("The left side of the gate that slides outwards.")]
        public Transform leftDoor;
        [Tooltip("The right side of the gate that slides outwards.")]
        public Transform rightDoor;

        [Header("Settings")]
        [Tooltip("How far each door slides outwards when fully open (in meters).")]
        public float openDistance = 0.8f;
        
        [Tooltip("How long it takes to complete one full open-and-close cycle (in seconds).")]
        public float cycleDuration = 3.0f;
        
        [Tooltip("Use this to offset the timing. E.g. 0.5 will start the gate fully open instead of closed.")]
        [Range(0f, 1f)]
        public float phaseOffset = 0f;

        [Tooltip("Which local axis the doors slide along. Usually (1, 0, 0) for X-axis.")]
        public Vector3 slideAxis = Vector3.right;

        private Vector3 leftStartPos;
        private Vector3 rightStartPos;

        private void Start()
        {
            if (leftDoor != null) leftStartPos = leftDoor.localPosition;
            if (rightDoor != null) rightStartPos = rightDoor.localPosition;
        }

        private void Update()
        {
            // Calculate a repeating 0 to 1 value based on time and duration
            float t = (Time.time / cycleDuration) + phaseOffset;
            
            // Use a Sine wave to smoothly animate back and forth
            // Mathf.Sin goes from -1 to 1. We map it to 0 to 1.
            float animationPhase = (Mathf.Sin(t * Mathf.PI * 2f) + 1f) / 2f; 
            // 0 = fully closed, 1 = fully open

            if (leftDoor != null)
            {
                // Slide left door in the negative direction
                leftDoor.localPosition = leftStartPos - slideAxis.normalized * (openDistance * animationPhase);
            }

            if (rightDoor != null)
            {
                // Slide right door in the positive direction
                rightDoor.localPosition = rightStartPos + slideAxis.normalized * (openDistance * animationPhase);
            }
        }
    }
}
