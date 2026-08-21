using UnityEngine;

namespace ArcRoll.Gameplay.Obstacles
{
    public class SwingingObstacle : MonoBehaviour
    {
        [Header("Swinging Settings")]
        [Tooltip("Maximum angle of the swing in degrees.")]
        [SerializeField] private float maxAngle = 45f;

        [Tooltip("How fast the obstacle swings.")]
        [SerializeField] private float swingSpeed = 2f;

        [Tooltip("Local axis to swing around. Z is standard for forward/backward lanes.")]
        [SerializeField] private Vector3 swingAxis = Vector3.forward;

        private Quaternion startRotation;
        private float internalTime;

        private void Start()
        {
            startRotation = transform.localRotation;
            internalTime = 0f;
        }

        private void Update()
        {
            internalTime += Time.deltaTime;
            // Use internal timer so Sin(0) = 0, meaning it ALWAYS starts in the center!
            float angle = Mathf.Sin(internalTime * swingSpeed) * maxAngle;
            transform.localRotation = startRotation * Quaternion.Euler(swingAxis * angle);
        }
    }
}
