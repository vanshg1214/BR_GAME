using UnityEngine;

namespace ArcRoll.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public class BowlingPin : MonoBehaviour
    {
        [Header("Effects")]
        [SerializeField] private ParticleSystem hitParticles;
        [SerializeField] private AudioSource hitAudio;
        
        [Tooltip("Add multiple wood-crashing sounds here to make impacts dynamic.")]
        [SerializeField] private AudioClip[] impactSounds;

        // Set by BowlingPinFormation via SetFormation() — avoids ordering issues
        private BowlingPinFormation formation = null;
        private bool _isKnockedDown = false;
        private float lastSoundTime = 0f;

        public bool IsKnockedDown => _isKnockedDown;

        private void Awake()
        {
            if (hitAudio == null) hitAudio = GetComponent<AudioSource>();
            if (hitAudio == null) hitAudio = gameObject.AddComponent<AudioSource>();

            if (hitAudio != null)
            {
                hitAudio.spatialBlend = 1.0f; // Make it full 3D sound
                hitAudio.playOnAwake = false;
            }
        }

        /// <summary>
        /// Called by BowlingPinFormation.Awake() to inject the parent reference.
        /// This avoids the race condition where pin.Start() runs before the Formation component exists.
        /// </summary>
        public void SetFormation(BowlingPinFormation f)
        {
            formation = f;
        }

        private void Update()
        {
            if (_isKnockedDown) return;

            // The imported 3D model is rotated -90 on X, meaning its local Z-axis (forward) points UP.
            // So we check if the local 'forward' is tilting away from the world 'up'.
            if (Vector3.Angle(transform.forward, Vector3.up) > 45f)
            {
                _isKnockedDown = true;
                PlayHitEffects();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Always play physics sound for ANY collision (even if the pin isn't part of a game formation yet)
            PlayDynamicImpactSound(collision.relativeVelocity.magnitude);

            if (formation == null) return;

            // Check if the colliding object is a bowling ball — try component first, then tag fallback
            bool isBowlingBall = false;

            Ball ball = collision.gameObject.GetComponentInParent<Ball>();
            if (ball != null && ball.Type == Ball.BallType.BowlingBall)
            {
                isBowlingBall = true;
            }
            else if (collision.gameObject.CompareTag("BowlingBall") ||
                     (collision.transform.root != null && collision.transform.root.CompareTag("BowlingBall")))
            {
                isBowlingBall = true;
            }

            if (isBowlingBall)
            {
                formation.NotifyBallTouched();
                PlayHitEffects();
            }
        }

        private void PlayHitEffects()
        {
            if (hitParticles != null && !hitParticles.isPlaying) hitParticles.Play();
        }

        private void PlayDynamicImpactSound(float velocity)
        {
            Debug.Log($"[BowlingPin] '{gameObject.name}' collision | velocity={velocity:F2} | hitAudio={hitAudio != null} | clips={(impactSounds != null ? impactSounds.Length : 0)}");

            if (hitAudio == null)
            {
                Debug.LogError($"[BowlingPin] '{gameObject.name}' has NO AudioSource! Audio cannot play.");
                return;
            }
            if (impactSounds == null || impactSounds.Length == 0)
            {
                Debug.LogWarning($"[BowlingPin] '{gameObject.name}' Impact Sounds array is EMPTY! Assign clips in Inspector.");
                return;
            }
            if (Time.time - lastSoundTime < 0.1f) return; // Prevent audio machine-gunning
            if (velocity < 0.05f)
            {
                Debug.Log($"[BowlingPin] '{gameObject.name}' velocity too low ({velocity:F2}) — skipped.");
                return;
            }
            
            // Map velocity to volume: even a soft bump (velocity=0.5) gets a solid 30% volume
            float volume = Mathf.Clamp(velocity / 3.0f, 0.3f, 1.0f);
            
            AudioClip clip = impactSounds[Random.Range(0, impactSounds.Length)];
            Debug.Log($"[BowlingPin] Playing '{clip.name}' at volume={volume:F2}");
            hitAudio.PlayOneShot(clip, volume);
            lastSoundTime = Time.time;
        }
    }
}
