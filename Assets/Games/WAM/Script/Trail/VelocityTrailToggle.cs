using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Enables a TrailRenderer only when the object is moving faster than a specified speed.
    /// </summary>
    public class VelocityTrailToggle : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Trail Renderer to toggle. If left empty, it will look on this object.")]
        public TrailRenderer trailRenderer;

        [Header("Settings")]
        [Tooltip("The speed required to activate the trail.")]
        public float activationSpeed = 5f;

        private Vector3 _lastPosition;

        private void Start()
        {
            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
            }
            
            if (trailRenderer != null)
            {
                trailRenderer.emitting = false; // Start with the trail disabled
            }

            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (trailRenderer == null) return;

            // Calculate current speed (distance moved this frame divided by time taken)
            float distance = Vector3.Distance(transform.position, _lastPosition);
            float currentSpeed = distance / Time.deltaTime;

            // Turn on the trail if we are swinging fast enough!
            if (currentSpeed >= activationSpeed && !trailRenderer.emitting)
            {
                trailRenderer.emitting = true;
            }
            else if (currentSpeed < activationSpeed && trailRenderer.emitting)
            {
                trailRenderer.emitting = false;
            }

            // Save the position for the next frame
            _lastPosition = transform.position;
        }
    }
}
