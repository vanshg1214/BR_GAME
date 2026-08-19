using System.Collections;
using UnityEngine;
using PopstrikeVR.Core;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// The highly-optimized foundational class for all balloons.
    /// Handles zero-garbage popping, spawning, and smooth coroutine animations.
    /// </summary>
    public abstract class BaseBalloon : MonoBehaviour
    {
        [Header("Base References")]
        [Tooltip("The parent transform containing the mesh and visual elements to scale/animate.")]
        public Transform VisualsParent;
        
        [Tooltip("The primary interaction collider for hand detection.")]
        public Collider InteractionCollider;

        protected bool isAnimating = false;
        protected bool isPopped = false;
        public bool IsPopped => isPopped;
        protected float spawnDelay = 0f;
        protected Vector3 initialScale = Vector3.one;

        [Header("Tutorial System")]
        public PopstrikeVR.UI.TutorialGestureAnimator tutorialPrefab;
        protected PopstrikeVR.UI.TutorialGestureAnimator spawnedTutorial;

        [Header("Material Overrides")]
        [Tooltip("Multiplies the emission/glow intensity of the materials on this balloon.")]
        public float glowIntensityMultiplier = 1.0f;
        private float lastGlowMultiplier = -1f;

        protected virtual void Awake()
        {
            if (VisualsParent != null)
                initialScale = VisualsParent.localScale;
        }

        protected virtual void OnEnable()
        {
            isPopped = false;
            // Disable collider on spawn. It will be enabled when AnimateSpawn finishes.
            if (InteractionCollider != null)
                InteractionCollider.enabled = false;
        }

        protected virtual void OnDisable()
        {
            StopTutorial();
        }

        /// <summary>
        /// Sets up the balloon when pulled from the pool. Resets scales to zero for spawn animation.
        /// </summary>
        public virtual void Setup(Vector3 spawnPosition)
        {
            transform.position = spawnPosition;
            
            // CRITICAL FIX: Ensure the collider is a Trigger so the hand can pass through it!
            if (InteractionCollider != null)
            {
                InteractionCollider.isTrigger = true;
            }

            // Record the exact scale the user set in the Prefab
            if (initialScale == Vector3.one && transform.localScale != Vector3.zero)
            {
                initialScale = transform.localScale;
            }
            
            Debug.Log($"<color=cyan>[BaseBalloon] Setup() called. Name: {gameObject.name}. InitialScale recorded as: {initialScale}. Shrinking root to Vector3.zero.</color>");
            
            // Immediately shrink the Root to 0 so it is invisible before the animation starts
            transform.localScale = Vector3.zero;
            
            // Ensure VisualsParent is at 1 so it doesn't double-scale
            // ONLY if VisualsParent is actually a child, not the root itself!
            if (VisualsParent != null && VisualsParent != transform)
            {
                VisualsParent.localScale = Vector3.one;
            }
            
            // Force property block application on spawn
            lastGlowMultiplier = -1f; 
            
            // Start the tutorial animation immediately if a prefab is assigned!
            StartTutorial();
        }

        protected virtual void Update()
        {
            // Allow real-time tuning of glow intensity in the editor
            if (glowIntensityMultiplier != lastGlowMultiplier)
            {
                ApplyGlowMultiplier();
            }

            // Instantly remove this tutorial if the player successfully learned the gesture elsewhere
            if (!ShouldPlayTutorial() && spawnedTutorial != null)
            {
                StopTutorial();
            }
        }

        private void ApplyGlowMultiplier()
        {
            lastGlowMultiplier = glowIntensityMultiplier;
            var renderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                
                if (r.sharedMaterials != null)
                {
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        
                        // Multiply the original Asset color by the slider
                        if (mat.HasProperty("_EmissionColor"))
                        {
                            Color em = mat.GetColor("_EmissionColor");
                            mpb.SetColor("_EmissionColor", em * glowIntensityMultiplier);
                        }
                        if (mat.HasProperty("_RimColor"))
                        {
                            Color rc = mat.GetColor("_RimColor");
                            mpb.SetColor("_RimColor", rc * glowIntensityMultiplier);
                        }
                    }
                }
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Pop logic that triggers VFX/SFX and despawns safely without GC spikes.
        /// </summary>
        public virtual void Pop(Vector3 hitPoint = default)
        {
            if (isPopped) return;
            isPopped = true;
            
            StopTutorial();

            if (InteractionCollider != null)
                InteractionCollider.enabled = false;

            if (hitPoint == default) hitPoint = transform.position;

            PlayPopVFX(hitPoint);
            PlayPopSFX();
            
            // Spawn the new 2D Tick Indicator
            PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowTick(hitPoint);

            // Play a satisfying micro-expansion pop visual before despawning
            if (gameObject.activeInHierarchy && VisualsParent != null)
            {
                StartCoroutine(PopVisualRoutine(0.08f));
            }
            else
            {
                PopstrikePooler.DespawnBalloon(this.gameObject, 0.5f);
            }
        }
        
        protected System.Collections.IEnumerator PopVisualRoutine(float duration)
        {
            float elapsed = 0f;
            Vector3 targetScale = initialScale * 1.4f; // Expand by 40%
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
                yield return null;
            }
            
            // Instantly disappear to simulate a pop
            transform.localScale = Vector3.zero;
                
            // Delay returning to pool so the sound/VFX can finish playing
            PopstrikePooler.DespawnBalloon(this.gameObject, 0.4f);
        }

        // Abstract methods every specific balloon must implement
        protected abstract void PlayPopVFX(Vector3 hitPoint);
        protected abstract void PlayPopSFX();

        #region Tutorial System

        protected virtual bool ShouldPlayTutorial()
        {
            return true;
        }

        public virtual void StartTutorial()
        {
            if (ShouldPlayTutorial() && tutorialPrefab != null && spawnedTutorial == null)
            {
                spawnedTutorial = Instantiate(tutorialPrefab, transform.position, Quaternion.identity);
            }
        }

        public virtual void StopTutorial()
        {
            if (spawnedTutorial != null)
            {
                spawnedTutorial.StopTutorial();
                Destroy(spawnedTutorial.gameObject);
                spawnedTutorial = null;
            }
        }

        #endregion

        #region Zero-Garbage Animation Coroutines

        public void AnimateSpawn(float duration)
        {
            Debug.Log($"<color=cyan>[BaseBalloon] AnimateSpawn() called. Name: {gameObject.name}. ActiveInHierarchy: {gameObject.activeInHierarchy}. Will scale to: {initialScale}</color>");
            if (gameObject.activeInHierarchy)
            {
                // Spawn exactly in place, scaling from 0 to its original prefab size
                StartCoroutine(SpawnRoutine(transform.position, transform.position, Vector3.zero, initialScale, duration, spawnDelay));
            }
        }

        public void AnimateDespawn(float duration)
        {
            if (gameObject.activeInHierarchy && !isPopped)
            {
                // Shrink away exactly in place using the Root transform
                StartCoroutine(SpawnRoutine(transform.position, transform.position, transform.localScale, Vector3.zero, duration));
            }
        }

        /// <summary>
        /// Completely garbage-free animation coroutine that handles both Scale and Position!
        /// </summary>
        private IEnumerator SpawnRoutine(Vector3 startPos, Vector3 endPos, Vector3 startScale, Vector3 endScale, float duration, float delay = 0f)
        {
            isAnimating = true; // Mark as animating IMMEDIATELY so Update loops don't hijack it during the delay

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Ease Out Cubic for smooth, snappy popping up
                float easeT = 1f - Mathf.Pow(1f - t, 3f);

                transform.localScale = Vector3.Lerp(startScale, endScale, easeT);
                
                // Float the whole object up
                transform.position = Vector3.Lerp(startPos, endPos, easeT);
                
                yield return null; 
            }
            
            transform.localScale = endScale;
            transform.position = endPos;
            isAnimating = false;
            
            // Enable interaction only after the spawn animation is fully complete
            if (InteractionCollider != null && gameObject.activeInHierarchy)
            {
                InteractionCollider.enabled = true;
            }
        }

        #endregion

        #region Hand-Specific Gesture Helpers

        /// <summary>
        /// Strict physical distance check to prevent 'popping at a distance' due to oversized colliders.
        /// </summary>
        protected bool IsPhysicallyTouching(Collider handCollider, float forgivenessMargin = 0.10f)
        {
            if (InteractionCollider == null) return true;

            // Use the actual physical bounds of the collider, which automatically accounts for prefab scaling!
            float strictRadius = InteractionCollider.bounds.extents.x;
            
            // Add forgiveness margin to account for the size of the player's hand/fist
            strictRadius += forgivenessMargin;

            // CRITICAL FIX: Measure distance to the hand's Transform center, NOT the closest point on the collider.
            // This prevents massive colliders (like UI Laser Pointers) from tricking the distance check from across the room!
            float actualDistance = Vector3.Distance(transform.position, handCollider.transform.position);

            if (actualDistance > strictRadius)
            {
                Debug.Log($"<color=yellow>[BaseBalloon] IGNORING DISTANT HIT: Hand Transform is {actualDistance:F2}m away, which is outside the {strictRadius:F2}m physical bounds. Ignored laser pointer!</color>");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Analyzes the colliding hand object (by components or name) to determine if it is the Left Hand.
        /// </summary>
        protected bool IsLeftHand(Collider other)
        {
            // 1. Check HandColliderForwarder
            var forwarder = other.GetComponent<PopstrikeVR.Interaction.HandColliderForwarder>();
            if (forwarder != null && forwarder.VelocityProvider != null)
            {
                return forwarder.VelocityProvider.isLeftHand;
            }
            
            // 2. Check parent MetaHandIntegrator
            var integrator = other.GetComponentInParent<PopstrikeVR.Interaction.MetaHandIntegrator>();
            if (integrator != null)
            {
                return integrator.isLeftHand;
            }

            // 3. Fallback to name parsing (for standard XR Hand rigs)
            string nameLower = other.gameObject.name.ToLower();
            if (nameLower.Contains("left") || nameLower.Contains("_l_") || nameLower.StartsWith("l_") || nameLower.Contains("lhand"))
            {
                return true;
            }
            return false; // Default fallback to Right hand
        }

        /// <summary>
        /// Gets the current gesture state for the specific hand that triggered the collision.
        /// </summary>
        protected PopstrikeVR.Interaction.GestureState GetHandGesture(Collider other)
        {
            if (PopstrikeVR.Interaction.GestureDetector.Instance == null)
                return PopstrikeVR.Interaction.GestureState.UNKNOWN;

            return IsLeftHand(other) 
                ? PopstrikeVR.Interaction.GestureDetector.Instance.LeftState 
                : PopstrikeVR.Interaction.GestureDetector.Instance.RightState;
        }

        /// <summary>
        /// Validates if the hand is allowed to pop balloons based on the active Session HandTrackingMode.
        /// </summary>
        protected bool IsValidHand(Collider other)
        {
            bool isLeft = IsLeftHand(other);
            var mode = PopstrikeVR.Core.TemporarySessionData.HandMode;
            
            if (mode == PopstrikeVR.Core.HandTrackingMode.BothHands) return true;
            if (mode == PopstrikeVR.Core.HandTrackingMode.LeftHandOnly && isLeft) return true;
            if (mode == PopstrikeVR.Core.HandTrackingMode.RightHandOnly && !isLeft) return true;
            
            return false;
        }

        #endregion
    }
}
