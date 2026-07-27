using UnityEngine;

namespace WhackAMole.Gaze
{

    [DisallowMultipleComponent]
    public class InstantGazeClick : MonoBehaviour
    {
        [Header("Instant Gaze Settings")]
        [Tooltip("Optional custom delay before the instant click triggers (0 for absolute instant click).")]
        [SerializeField] private float activationDelay = 0.5f;

        
        public float ActivationDelay => activationDelay;
    }
}

