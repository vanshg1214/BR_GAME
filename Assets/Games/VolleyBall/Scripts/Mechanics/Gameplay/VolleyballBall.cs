using UnityEngine;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.Mechanics
{
    public enum BallHitter { None, Player, AI }

    /// <summary>
    /// Custom physics controller for the VR Rehab Volleyball.
    /// Overrides default Unity rigid body physics to create a slow-motion "balloon" effect
    /// and implements aim-assisted redirection to ensure a high success rate for patients.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VolleyballBall : MonoBehaviour
    {
        [Header("Balloon Physics Settings")]
        [Tooltip("Custom gravity applied to the ball (positive value). Higher means it falls faster.")]
        [SerializeField] private float customGravity = 9.81f;
        public float CustomGravity => customGravity;
        
        [Tooltip("Linear drag applied against velocity. Simulates balloon/beachball floatiness.")]
        [SerializeField] private float airResistance = 1.2f;
        public float AirResistance => airResistance;

        [Header("Aim Assist Settings")]
        [Tooltip("How strongly the ball magnetically corrects its path towards the target (0.0 = Raw Physics, 1.0 = Perfect Auto-Aim)")]
        [Range(0f, 1f)]
        [SerializeField] private float assistWeight = 1.0f;
        public float AssistWeight => assistWeight;
        
        [Tooltip("Multiplier for the player's physical hand speed when SERVING (hitting a stationary ball).")]
        [SerializeField] private float playerServeMultiplier = 1.2f;
        public float PlayerServeMultiplier => playerServeMultiplier;

        [Tooltip("Multiplier for the player's physical hand speed when BLOCKING/RETURNING (hitting a moving ball).")]
        [SerializeField] private float playerBlockMultiplier = 1.5f;
        public float PlayerBlockMultiplier => playerBlockMultiplier;

        [Tooltip("How much of the ball's incoming momentum is retained when bouncing off the player's hand.")]
        [SerializeField] private float blockRestitution = 0.7f;
        public float BlockRestitution => blockRestitution;

        // Audio is now fully handled by VolleyballEffectsManager!

        // Audio is now fully handled by VolleyballEffectsManager!

        private Rigidbody rb;
        public Rigidbody Rb => rb;
        
        private AudioSource audioSource;
        private bool isBallActive = false;
        public bool IsBallActive => isBallActive;
        
        // Launch immunity: ignore floor collisions for a brief window after being struck.
        // This prevents the "ball clips floor near hand then registers a fault" bug.
        private float lastLaunchTime = -999f;
        private const float LAUNCH_IMMUNITY_WINDOW = 0.35f;
        
        public BallHitter LastHitter { get; set; } = BallHitter.None;
        public Vector3 LastTargetPosition { get; private set; } = Vector3.zero;
        public float CurrentCurveAcceleration { get; private set; } = 0f;
        
        private float currentBallTimeScale = 1.0f;
        public float CurrentBallTimeScale => currentBallTimeScale;

        public void SetTimeScale(float targetScale)
        {
            if (targetScale <= 0.01f) targetScale = 0.01f;
            if (Mathf.Approximately(currentBallTimeScale, targetScale)) return;
            
            float ratio = targetScale / currentBallTimeScale;
            if (rb != null) rb.linearVelocity *= ratio;
            
            currentBallTimeScale = targetScale;
        }

        /// <summary>
        /// Resets LastHitter to None. Called by GameManager before a new serve
        /// so that stale data from the previous rally doesn't cause wrong scoring.
        /// </summary>
        public void ResetHitter()
        {
            LastHitter = BallHitter.None;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                Debug.LogWarning("[VolleyballBall] Rigidbody was missing from the Volleyball GameObject. Added automatically.");
            }
            
            // Set up 3D Audio Source dynamically
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // Full 3D sound
            audioSource.playOnAwake = false;

            rb.useGravity = false; // We handle gravity manually
        }

        private float? shotGravityOverride = null;
        public float ActiveGravity => shotGravityOverride ?? customGravity;

        private void FixedUpdate()
        {
            if (!isBallActive) return;

            // Apply custom balloon physics, scaling perfectly by CurrentBallTimeScale
            float activeG = shotGravityOverride ?? customGravity;
            float g = activeG * currentBallTimeScale * currentBallTimeScale;
            float k = airResistance * currentBallTimeScale;
            
            Vector3 gravityForce = Vector3.down * g;
            Vector3 dragForce = -rb.linearVelocity * k;
            Vector3 curveForce = Vector3.right * CurrentCurveAcceleration * currentBallTimeScale * currentBallTimeScale;
            
            rb.AddForce(gravityForce + dragForce + curveForce, ForceMode.Acceleration);
        }

        /// <summary>
        /// Activates the ball with an initial launch velocity (e.g., from a serve).
        /// </summary>
        public void Launch(Vector3 initialVelocity, BallHitter hitter = BallHitter.None)
        {
            CurrentCurveAcceleration = 0f;
            rb.linearVelocity = initialVelocity;
            isBallActive = true;
            LastHitter = hitter;
            lastLaunchTime = Time.time;
        }

        /// <summary>
        /// Halts the ball's movement and physics processing.
        /// </summary>
        public void StopBall()
        {
            isBallActive = false;
            CurrentCurveAcceleration = 0f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Calculates the exact velocity required to reach a target position in a specific time, factoring in linear drag.
        /// </summary>
        public void LaunchToTarget(Vector3 targetPosition, float timeToTarget, BallHitter hitter = BallHitter.None, Vector3? strikeDirection = null, float assist = 1.0f, float curveAcceleration = 0f, float? gravityOverride = null)
        {
            isBallActive = true;
            LastHitter = hitter;
            LastTargetPosition = targetPosition;
            lastLaunchTime = Time.time;
            CurrentCurveAcceleration = curveAcceleration;
            shotGravityOverride = gravityOverride;
            Vector3 displacement = targetPosition - transform.position;
            
            float k = airResistance;
            float g = ActiveGravity;

            Vector3 perfectVelocity;

            if (k < 0.001f) // Fallback for zero drag
            {
                perfectVelocity.x = (displacement.x - 0.5f * CurrentCurveAcceleration * timeToTarget * timeToTarget) / timeToTarget;
                perfectVelocity.y = (displacement.y + 0.5f * g * timeToTarget * timeToTarget) / timeToTarget;
                perfectVelocity.z = displacement.z / timeToTarget;
            }
            else
            {
                // Exact physics formulas for trajectory with linear drag
                float e_kT = Mathf.Exp(-k * timeToTarget);
                float term = (1f - e_kT) / k;

                float ax = CurrentCurveAcceleration;
                float vx = (displacement.x - (ax / k) * (timeToTarget - term)) / term;
                float vz = displacement.z / term;
                float vy = (displacement.y + (g / k) * timeToTarget - (g / k) * term) / term;

                perfectVelocity = new Vector3(vx, vy, vz);
            }

            // We assign the exact calculated perfect velocity to the ball.
            // MUST multiply by currentBallTimeScale so that the physics arc works perfectly in slow-motion!
            rb.linearVelocity = perfectVelocity * currentBallTimeScale;

            rb.angularVelocity = Random.insideUnitSphere * 2f;

            // Trigger central VFX and Audio
            if (VolleyballEffectsManager.Instance != null)
            {
                if (hitter == BallHitter.Player) VolleyballEffectsManager.Instance.PlayPlayerHit(transform.position);
                else if (hitter == BallHitter.AI) VolleyballEffectsManager.Instance.PlayAIHit(transform.position);
            }
        }


        private void OnCollisionEnter(Collision collision)
        {
            if (!isBallActive) return;

            // ---- NET HIT: Real Volleyball Rules ----
            // Touching the net is NOT a fault! The ball simply bounces off it.
            // We detect the net via the VolleyballNet marker component (safe, no tag errors).
            bool isNet = collision.gameObject.GetComponent<VolleyballNet>() != null;
            
            if (isNet)
            {
                if (rb != null)
                {
                    // Physically deflect the ball off the net so it doesn't stick
                    rb.linearVelocity = new Vector3(
                        -rb.linearVelocity.x * 0.5f, 
                        Mathf.Max(rb.linearVelocity.y * 0.5f, 1.0f), 
                        -rb.linearVelocity.z * 0.5f);
                }
                if (VolleyballEffectsManager.Instance != null)
                    VolleyballEffectsManager.Instance.PlayNetHit(transform.position);
                
                // DO NOT call any fault logic here. Let the ball continue flying!
                return;
            }

            // ---- FLOOR / WALL HIT: This is the ONLY real fault in volleyball ----
            // IMMUNITY CHECK: If the ball was just struck/launched, ignore floor collisions
            // for a brief window. This prevents the "ball clips floor near hand" bug.
            if (Time.time - lastLaunchTime < LAUNCH_IMMUNITY_WINDOW)
            {
                if (VolleyballEffectsManager.Instance != null)
                    VolleyballEffectsManager.Instance.PlayFloorHit(transform.position); // Visual feedback only
                return; // DO NOT score — the ball was just hit!
            }
            
            if (VolleyballGameManager.Instance != null)
            {
                if (VolleyballEffectsManager.Instance != null)
                    VolleyballEffectsManager.Instance.PlayFloorHit(transform.position);
                    
                VolleyballGameManager.Instance.HandleBallDropped(this);
            }
        }
    }
}
