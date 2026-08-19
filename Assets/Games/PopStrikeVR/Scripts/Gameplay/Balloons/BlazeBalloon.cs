using UnityEngine;
using PopstrikeVR.Interaction;
using PopstrikeVR.Data;
using PopstrikeVR.Core;
using System.Collections;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// The Orange Blaze Balloon. 
    /// Requires a CLOSED_FIST gesture and a minimum velocity strike to pop.
    /// </summary>
    public class BlazeBalloon : BaseBalloon
    {
        [Header("Materials")]
        public Material defaultMaterial;
        public Material errorMaterial;

        [Header("Interaction Settings")]
        [Tooltip("Base minimum velocity. Patient profiles can override this if higher.")]
        [SerializeField] private float minimumPunchVelocity = 0.6f;

        public static bool HasCompletedTutorial = false;

        private MeshRenderer[] meshRenderers;
        private PatientProfileSO patientProfile;

        private void Awake()
        {
            base.Awake(); // CRITICAL: let BaseBalloon record initialScale from the prefab
            meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        }

        protected override void Update()
        {
            base.Update(); // Ensure base Update runs for tutorial cleanup

            // Guard: don't pulse while the spawn animation is running or after pop
            if (isPopped || isAnimating || VisualsParent == null) return;

            // 1Hz breathing scale (Pulse between 1.0x and 1.08x)
            float sineValue = Mathf.Sin(Time.time * Mathf.PI * 2f);
            float scaleMultiplier = Mathf.Lerp(1.0f, 1.08f, (sineValue + 1f) / 2f);
            
            // Use initialScale from BaseBalloon which was safely captured in Awake!
            VisualsParent.localScale = initialScale * scaleMultiplier;
        }

        public void ResetState()
        {
            if (meshRenderers != null && defaultMaterial != null)
            {
                foreach (var renderer in meshRenderers)
                {
                    renderer.material = defaultMaterial;
                }
            }
        }

        private IEnumerator FlashRedRoutine()
        {
            if (meshRenderers != null && errorMaterial != null)
            {
                foreach (var renderer in meshRenderers)
                {
                    // Temporarily clear property block so error material shows cleanly
                    renderer.SetPropertyBlock(null);
                    renderer.material = errorMaterial;
                }
            }
            
            yield return new WaitForSeconds(0.3f);
            
            ResetState();
        }

        /// <summary>
        /// Injects the active patient profile to access dynamic velocity thresholds.
        /// </summary>
        public void Initialize(PatientProfileSO profile)
        {
            patientProfile = profile;
            
            // Trigger approach pulse (Two splashes of Orange)
            // TEMPORARILY DISABLED TO TEST USER FEEDBACK
            if (PopstrikeVR.Visuals.ScreenEffectsController.Instance != null)
            {
                float intensity = 0.6f;
                Color spawnColor = new Color(1f, 0.5f, 0f, 1f);
                if (PopstrikeVR.Core.PopstrikeFeedbackManager.Instance != null)
                {
                    intensity = PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.SpawnVignetteIntensity;
                    spawnColor = PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.BlazeSpawnColor;
                }
                PopstrikeVR.Visuals.ScreenEffectsController.Instance.TriggerSpawnWarning(spawnColor, intensity);
            }
            
            // Wait exactly 0.5s for the flashes to finish before animating from Scale 0
            spawnDelay = 0.5f;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isPopped) return;
            
            // ONLY accept collisions from our custom bone hitboxes
            if (!other.name.StartsWith("Hitbox_")) return;

            if (!IsValidHand(other)) return;

            if (!IsPhysicallyTouching(other)) return;

            Debug.Log($"<color=orange>[BlazeBalloon]</color> COLLISION DETECTED with: {other.gameObject.name}");

            if (patientProfile == null)
            {
                Debug.LogError("<color=red>[BlazeBalloon] IGNORING PUNCH: No PatientProfile assigned! Was this spawned by the LevelDirector?</color>");
                return;
            }


            // Look for velocity data anywhere on the hand hierarchy (since colliders are child bones)
            var handVelocity = other.GetComponentInParent<IHandVelocityProvider>();
            
            if (handVelocity == null)
            {
                Debug.LogWarning($"<color=yellow>[BlazeBalloon] IGNORING PUNCH: Could not find 'SimpleHandVelocity' script on {other.gameObject.name} or its parents!</color>");
            }
            else
            {
                // 1. Verify Gesture
                GestureState handGesture = GetHandGesture(other);
                if (PopstrikeVR.Core.TemporarySessionData.DisableGestures)
                {
                    handGesture = GestureState.CLOSED_FIST;
                }

                if (handGesture != GestureState.CLOSED_FIST)
                {
                    bool isLeft = IsLeftHand(other);
                    Debug.Log($"<color=yellow>[BlazeBalloon] IGNORING PUNCH: Hand is not a Fist.</color>");
                    
                    bool canReport = false;
                    if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
                        canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();
                        
                    if (!canReport) return; // Completely ignore if cooldown is active
                        
                    // User physically touched it but with wrong gesture - play error
                    if (PopstrikeFeedbackManager.Instance != null)
                        PopstrikeFeedbackManager.Instance.PlayErrorTone();
                        
                    PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(transform.position);
                    StartCoroutine(FlashRedRoutine());
                    return; 
                }

                // 2. Verify Velocity Threshold
                float requiredVelocity = patientProfile != null ? Mathf.Max(minimumPunchVelocity, patientProfile.MinimumPunchVelocity) : minimumPunchVelocity;
                
                // --- ACCESSIBILITY: Disable velocity requirement if gestures are disabled ---
                if (PopstrikeVR.Core.TemporarySessionData.DisableGestures)
                {
                    requiredVelocity = 0f;
                }
                
                float strikeSpeed = handVelocity.GetVelocity().magnitude;
                Debug.Log($"<color=orange>[BlazeBalloon]</color> FIST DETECTED! Punch Speed: {strikeSpeed:F2} m/s (Needs {requiredVelocity:F2} m/s)");
                
                if (strikeSpeed >= requiredVelocity)
                {
                    Debug.Log("<color=green>[BlazeBalloon] SUCCESS! POPPING BALLOON!</color>");
                    
                    // --- ACCESSIBILITY: Lock Gesture on first hit for Hard Mode ---
                    if (PopstrikeVR.Core.TemporarySessionData.Difficulty == "Hard")
                    {
                        if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
                        {
                            PopstrikeVR.Interaction.GestureDetector.Instance.LockGesture(IsLeftHand(other), PopstrikeVR.Interaction.GestureState.CLOSED_FIST);
                        }
                    }

                    HasCompletedTutorial = true;
                    Pop(transform.position); // Ensure VFX plays at the center of the balloon
                    ComboManager.Instance?.RegisterHit(50);
                }
                else
                {
                    Debug.Log($"<color=yellow>[BlazeBalloon] IGNORING PUNCH: Too slow!</color>");
                    
                    bool canReport = false;
                    if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
                        canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();
                        
                    if (!canReport) return; // Completely ignore if cooldown is active
                        
                    // User physically touched it but too slow - play error
                    if (PopstrikeFeedbackManager.Instance != null)
                        PopstrikeFeedbackManager.Instance.PlayErrorTone();
                        
                    PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(transform.position);
                    StartCoroutine(FlashRedRoutine());
                }
            }
        }

        protected override bool ShouldPlayTutorial()
        {
            return !HasCompletedTutorial;
        }

        public override void StartTutorial()
        {
            base.StartTutorial();
            if (spawnedTutorial != null)
            {
                spawnedTutorial.PlayPunchTutorial(transform);
            }
        }

        public override void Pop(Vector3 hitPoint = default)
        {
            if (isPopped) return;
            isPopped = true;

            StopTutorial(); // CRITICAL: Destroy the tutorial icon on pop!

            if (InteractionCollider != null)
                InteractionCollider.enabled = false;

            if (hitPoint == default) hitPoint = transform.position;

            PlayPopVFX(hitPoint);
            PlayPopSFX();

            // Instantly pop without scaling up
            transform.localScale = Vector3.zero;
            PopstrikePooler.DespawnBalloon(this.gameObject, 0.4f);
        }

        protected override void PlayPopVFX(Vector3 hitPoint)
        {
            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayBlazeVFX(hitPoint);
        }

        protected override void PlayPopSFX()
        {
            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayBlazeThud();
        }
    }
}
