using UnityEngine;

namespace ArcRoll.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class ArcRollBallAudio : MonoBehaviour
    {
        [Header("Impact Sounds")]
        [Tooltip("Add multiple bouncing/hitting sounds here. It will pick a random one each time.")]
        public AudioClip[] impactSounds;
        
        [Tooltip("Minimum velocity required to trigger an impact sound.")]
        public float minImpactVelocity = 0.5f;
        
        [Tooltip("Velocity where the impact sound plays at maximum volume.")]
        public float maxImpactVelocity = 10.0f;

        [Header("Rolling Sound")]
        [Tooltip("The continuous loop sound for when the ball is rolling on the floor.")]
        public AudioClip rollingSound;
        
        [Tooltip("Velocity where the rolling sound plays at maximum volume/pitch.")]
        public float maxRollingVelocity = 8.0f;

        private Rigidbody rb;
        private AudioSource audioSource;
        private bool isRolling = false;
        private int floorContacts = 0; // Keep track of how many floor segments we are touching

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();
            
            // Basic VR audio configuration (ensures it plays physically from the ball)
            audioSource.spatialBlend = 1.0f; // 100% 3D Audio
            audioSource.playOnAwake = false;
            
            // Fix Unity's default brutal logarithmic distance dropoff!
            // This ensures players can still hear bounces that happen far away down the lane.
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 3.0f;
            audioSource.maxDistance = 25.0f;
            
            // Set up the rolling clip
            if (rollingSound != null)
            {
                audioSource.clip = rollingSound;
                audioSource.loop = true;
            }
        }

        // ==============================================================
        // IMPACT LOGIC (Bouncing off walls, hoops, pins, or hitting the floor)
        // ==============================================================
        private void OnCollisionEnter(Collision collision)
        {
            // 1. Play Impact Sound
            if (impactSounds != null && impactSounds.Length > 0)
            {
                float impactVelocity = collision.relativeVelocity.magnitude;
                
                if (impactVelocity > minImpactVelocity)
                {
                    // Scale volume between 0 and 1 based on how hard it hit
                    float volume = Mathf.Clamp01(impactVelocity / maxImpactVelocity);
                    
                    // Pick a random impact clip for variety
                    AudioClip randomClip = impactSounds[Random.Range(0, impactSounds.Length)];
                    
                    // Use PlayOneShot so impacts can overlap without cutting each other off!
                    audioSource.PlayOneShot(randomClip, volume);
                }
            }

            // 2. Track Floor Contact for Rolling
            if (collision.gameObject.CompareTag("Floor"))
            {
                floorContacts++;
            }
        }

        // ==============================================================
        // ROLLING LOGIC (Continuous looping sound while on the floor)
        // ==============================================================
        private void OnCollisionExit(Collision collision)
        {
            if (collision.gameObject.CompareTag("Floor"))
            {
                floorContacts--;
                if (floorContacts < 0) floorContacts = 0;
            }
        }

        private void Update()
        {
            // If we are touching the floor AND moving, we are rolling!
            float currentSpeed = rb.linearVelocity.magnitude;
            bool shouldBeRolling = (floorContacts > 0) && (currentSpeed > 0.1f) && (rollingSound != null);

            if (shouldBeRolling)
            {
                if (!isRolling)
                {
                    audioSource.Play();
                    isRolling = true;
                }

                // Dynamically adjust volume and pitch based on speed!
                float speedRatio = Mathf.Clamp01(currentSpeed / maxRollingVelocity);
                
                // Volume gets louder the faster we roll
                audioSource.volume = Mathf.Lerp(0.1f, 1.0f, speedRatio);
                
                // Pitch goes higher the faster we roll
                audioSource.pitch = Mathf.Lerp(0.8f, 1.2f, speedRatio);
            }
            else
            {
                if (isRolling)
                {
                    audioSource.Stop();
                    isRolling = false;
                }
            }
        }
    }
}
