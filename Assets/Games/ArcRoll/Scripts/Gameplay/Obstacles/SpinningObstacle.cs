using UnityEngine;

namespace ArcRoll.Gameplay.Obstacles
{
    public class SpinningObstacle : MonoBehaviour
    {
        [Header("Spinning Settings")]
        [Tooltip("Rotation speed in degrees per second.")]
        [SerializeField] private float spinSpeed = 90f;
        
        [Tooltip("Axis to spin around. Y is horizontal sweeping.")]
        [SerializeField] private Vector3 spinAxis = Vector3.up;

        private void Update()
        {
            // Rotate smoothly every frame based on the spin speed
            transform.Rotate(spinAxis * (spinSpeed * Time.deltaTime), Space.Self);
        }
    }
}
