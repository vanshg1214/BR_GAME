using UnityEngine;

namespace ArcRoll.Gameplay.Obstacles
{
    [RequireComponent(typeof(Collider))]
    public class SpeedBreakerObstacle : MonoBehaviour
    {
        [Header("Speed Breaker Settings")]
        [Tooltip("How much to multiply the ball's speed. E.g. 0.4 means it loses 60% of its speed instantly!")]
        [Range(0.1f, 0.9f)]
        public float speedMultiplier = 0.4f;

        [Header("Feedback (Optional)")]
        [Tooltip("Sound to play when a ball is slowed down.")]
        public AudioSource slowDownSFX;
        
        [Tooltip("Particle effect to play when triggered.")]
        public ParticleSystem impactVFX;

        private void OnTriggerEnter(Collider other)
        {
            ApplySlowdown(other.attachedRigidbody, other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            ApplySlowdown(collision.rigidbody, collision.gameObject);
        }

        private void ApplySlowdown(Rigidbody rb, GameObject hitObject)
        {
            if (rb != null)
            {
                // Make sure we only slow down actual game objects (Balls or Frisbees)
                bool isBall = hitObject.GetComponentInParent<ArcRoll.Gameplay.Ball>() != null;
                bool isFrisbee = hitObject.GetComponentInParent<ArcRoll.Gameplay.Frisbee.Frisbee>() != null;

                if (isBall || isFrisbee)
                {
                    // Dramatically reduce the velocity
                    rb.linearVelocity *= speedMultiplier;

                    // Play feedback
                    if (slowDownSFX != null) slowDownSFX.Play();
                    if (impactVFX != null) impactVFX.Play();
                    
                    Debug.Log($"[SpeedBreaker] Slowed down {hitObject.name}! New Speed: {rb.linearVelocity.magnitude:F1} m/s");
                }
            }
        }
    }
}
