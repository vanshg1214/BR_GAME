using UnityEngine;

namespace ArcRoll.Gameplay.Obstacles
{
    [RequireComponent(typeof(Collider))]
    public class PinballBumperObstacle : MonoBehaviour
    {
        [Header("Bumper Settings")]
        [Tooltip("How much speed to multiply when the ball hits this bumper. 1.0 = normal bounce.")]
        public float speedMultiplier = 1.3f;
        
        [Tooltip("Flat speed added to every bounce just to ensure it pops away fast!")]
        public float flatBoost = 2.0f;
        
        [Tooltip("The absolute maximum speed the ball can reach from bouncing")]
        public float maxSpeedLimit = 15f;
        
        [Header("Optional Feedback")]
        public ParticleSystem bounceVFX;
        public AudioSource bounceSFX;
        
        [Tooltip("If assigned, the bumper will visually 'pulse' larger for a moment when hit.")]
        public Transform bumperMeshToPulse;
        
        private Vector3 originalScale;
        private float pulseTimer;

        private void Start()
        {
            if (bumperMeshToPulse != null)
            {
                originalScale = bumperMeshToPulse.localScale;
            }
        }

        private void Update()
        {
            if (bumperMeshToPulse != null && pulseTimer > 0)
            {
                pulseTimer -= Time.deltaTime * 5f;
                // Pulse up to 30% larger instantly, then shrink back down
                float scale = 1f + (Mathf.Sin(pulseTimer * Mathf.PI) * 0.3f); 
                if (pulseTimer <= 0) scale = 1f;
                
                bumperMeshToPulse.localScale = originalScale * scale;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            Rigidbody rb = collision.rigidbody;
            
            // Make sure we are only bouncing balls!
            if (rb != null && collision.collider.GetComponentInParent<ArcRoll.Gameplay.Ball>() != null)
            {
                // 1. Calculate the richochet direction based on where the ball hit us
                Vector3 bounceDirection = Vector3.Reflect(rb.linearVelocity.normalized, collision.contacts[0].normal);
                
                // If it hit perfectly dead center or from a stop, just push it away from our center
                if (bounceDirection == Vector3.zero || rb.linearVelocity.magnitude < 0.1f)
                {
                    bounceDirection = (collision.transform.position - transform.position).normalized;
                }

                // 2. Ensure the bounce stays perfectly flat on the ground (no flying up in the air)
                bounceDirection.y = 0;
                bounceDirection.Normalize();

                // 3. Calculate the new boosted speed
                float newSpeed = rb.linearVelocity.magnitude * speedMultiplier + flatBoost;
                newSpeed = Mathf.Min(newSpeed, maxSpeedLimit);
                
                // 4. Apply the velocity to shoot the ball away!
                rb.linearVelocity = bounceDirection * newSpeed;

                // 5. Play juicy feedback
                if (bounceVFX != null) bounceVFX.Play();
                if (bounceSFX != null) bounceSFX.Play();
                
                // Trigger the visual pulse effect
                pulseTimer = 1.0f; 
            }
        }
    }
}
