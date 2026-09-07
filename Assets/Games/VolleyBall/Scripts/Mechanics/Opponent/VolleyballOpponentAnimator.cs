using UnityEngine;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.Mechanics
{
    /// <summary>
    /// Drives the Dog character's Animator based on real-time movement speed and
    /// AI hit/serve events. Handles 4 animation types:
    ///   - Serve animations (HitIndex 0, 1): Kick + Throw — used when the AI serves
    ///   - Volley animations (HitIndex 2, 3): Kick + other — used mid-rally
    /// The Kick animation appears in BOTH pools so it gets used more frequently.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class VolleyballOpponentAnimator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The main AI script on the parent AI Player object.")]
        [SerializeField] private VolleyballOpponent opponentAI;

        // ─── Animator Parameter Names (must match exactly in the Animator window) ─
        [Header("Animator Parameters")]
        [Tooltip("Float parameter name. Used to drive Walk/Run blend.")]
        [SerializeField] private string speedParam = "Speed";

        [Tooltip("Trigger parameter name. Fires when the dog hits the ball.")]
        [SerializeField] private string hitTriggerParam = "HitTrigger";

        [Tooltip("Integer parameter. Selects WHICH hit animation to play.")]
        [SerializeField] private string hitIndexParam = "HitIndex";

        [Header("Animation Pools")]
        [Tooltip("The HitIndex values to use during a mid-rally volley (0=Dog Success/Head, 1=Kick, 3=Dog Damage/Head).")]
        [SerializeField] private int[] volleyAnimationIndices = new int[] { 0, 1, 3 };
        
        [Tooltip("The HitIndex values to use during a serve (e.g. 2 for Throw, 1 for Kick).")]
        [SerializeField] private int[] serveAnimationIndices = new int[] { 2, 1 };

        // ─── Speed Tuning ─────────────────────────────────────────────────────────
        [Header("Speed Tuning")]
        [Tooltip("How fast the Animator's Speed parameter responds. Lower = smoother but slower. Higher = snappier. Tune this to stop sliding.")]
        [SerializeField] private float speedSmoothTime = 0.1f;

        [Tooltip("Minimum real-world speed (m/s) before we tell the Animator the dog is moving. Below this threshold, Speed is forced to 0 so Idle triggers properly.")]
        [SerializeField] private float movementDeadzone = 0.15f;

        // ─── Hit Animation Timing ─────────────────────────────────────────────────
        [Header("Hit Animation Timing")]
        [Tooltip("How many seconds BEFORE the ball physically arrives should the hit animation begin? This offsets the animation so the paw/kick peak lines up exactly with ball contact.")]
        [SerializeField] private float hitAnimationLeadTime = 0.35f;
        
        public float HitAnimationLeadTime => hitAnimationLeadTime;

        // ─── Private State ────────────────────────────────────────────────────────
        private Animator animator;
        private Vector3 lastPosition;
        private float smoothedSpeed;        // Current smoothed speed value fed to Animator
        private float speedSmoothVelocity;  // Internal velocity used by SmoothDamp

        // ─── Lifecycle ────────────────────────────────────────────────────────────
        private void Awake()
        {
            animator = GetComponent<Animator>();
            
            if (opponentAI == null)
                opponentAI = GetComponentInParent<VolleyballOpponent>();
        }

        private void Start()
        {
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            // Events removed - AI script now calls methods directly for better sync
        }

        private void OnDisable()
        {
        }

        // ─── Every Frame ──────────────────────────────────────────────────────────
        private void Update()
        {
            UpdateMovementAnimation();
        }

        /// <summary>
        /// Calculates the dog's real horizontal speed each frame and smoothly feeds
        /// it to the Animator Speed parameter. The SmoothDamp ensures the dog will
        /// NEVER slide — it will always transition through walk before reaching idle.
        /// </summary>
        private void UpdateMovementAnimation()
        {
            float rawSpeed = opponentAI != null ? opponentAI.CurrentMoveSpeed : 0f;

            // Apply deadzone: if barely moving, snap to 0 so idle triggers cleanly
            if (rawSpeed < movementDeadzone)
                rawSpeed = 0f;

            // Smooth the speed so transitions feel organic and never pop
            smoothedSpeed = Mathf.SmoothDamp(
                smoothedSpeed, rawSpeed, ref speedSmoothVelocity, speedSmoothTime);

            animator.SetFloat(speedParam, smoothedSpeed);
        }

        // ─── Hit Animation Handlers ───────────────────────────────────────────────

        /// <summary>
        /// Randomly selects the next index from the volley pool.
        /// </summary>
        public int ChooseNextVolleyIndex()
        {
            if (volleyAnimationIndices.Length == 0) return 0;
            return volleyAnimationIndices[Random.Range(0, volleyAnimationIndices.Length)];
        }

        /// <summary>
        /// Randomly selects the next index from the serve pool.
        /// </summary>
        public int ChooseNextServeIndex()
        {
            if (serveAnimationIndices.Length == 0) return 0;
            return serveAnimationIndices[Random.Range(0, serveAnimationIndices.Length)];
        }

        /// <summary>
        /// Applies the HitIndex and fires the HitTrigger.
        /// The leadTime offset means this is called BEFORE the ball physically arrives
        /// so the animation peak (the kick/throw peak) aligns exactly with contact.
        /// </summary>
        public void FirePreselectedHitAnimation(int index)
        {
            animator.SetInteger(hitIndexParam, index);
            animator.SetTrigger(hitTriggerParam);
            
            Debug.Log($"[DogAnimator] Playing hit animation index {index} with {hitAnimationLeadTime}s lead time.");
        }

        // ─── Animation Events (Triggered by the Animation Clips) ─────────────────
        
        public void OnThrowRelease()
        {
            if (VolleyballGameManager.Instance != null)
            {
                VolleyballGameManager.Instance.TriggerServeReleaseEvent();
            }
        }
        
        // Silences the error for the accidental 'NewEvent' you added in Unity
        public void NewEvent() { }
    }
}
