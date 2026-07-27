using UnityEngine;
using System.Collections;

namespace WhackAMole.Squirrel
{
    /// <summary>
    /// Controls the Squirrel's complete lifecycle:
    ///   1. Pop up → 2. Throw acorn → 3. Wait for player hit →
    ///   4a. If hit:   Seeds fly to hands → Pickup animation → Hide
    ///   4b. If missed: Acorn flies back to hands → Pickup animation → Hide
    /// </summary>
    public class SquirrelCharacter : BaseMole
    {
        [Header("Target Link")]
        [Tooltip("Drag the Acorn Target (AcornProp script) child object here.")]
        public AcornProp acornTarget;

        [Header("Animation Links")]
        [Tooltip("Drag the Left Hand bone from the skeleton here.")]
        public Transform leftHandBone;
        [Tooltip("Drag the Right Hand bone from the skeleton here.")]
        public Transform rightHandBone;

        [Header("Squirrel Timing")]
        [Tooltip("Seconds to wait for the player to hit the acorn before taking it back.")]
        public float hitWindow = 6f;
        [Tooltip("Seconds to wait after pickup animation before hiding the squirrel.")]
        public float hideDelay = 2.5f;

        [Header("VFX References")]
        [Tooltip("Assign the sand explosion VFX prefab here to play on popup and popdown.")]
        [SerializeField] private GameObject sandExplosionVFX;

        [Header("Animation Settings")]
        [Tooltip("The exact name of the Trigger parameter in the Animator to play the success/cheer animation.")]
        [SerializeField] private string happyAnimTrigger = "Success";
        [Tooltip("How long to wait for the cheer animation to play before jumping and rolling.")]
        [SerializeField] private float happyDuration = 1.0f;

        private Animator anim;

        // STATE: prevents the Animator's auto-transition from Throw→Pickup
        // from accidentally hiding the squirrel before we want it to.
        private bool isPickingUp = false;
        private Coroutine lifecycleCoroutine;
        private Vector3 originalLocalPos;

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            anim = GetComponentInChildren<Animator>();
            originalLocalPos = transform.localPosition;
            spawnOrigin = originalLocalPos; // Align BaseMole's spawnOrigin with this character's surface height
            originalScale = transform.localScale; // CRITICAL: Cache scale because we bypass base.OnEnable()

            // Disable hit detection on the squirrel body itself
            DisableBodyColliders();

            // CRITICAL SAFETY CHECK: Ensure acornTarget is our own local child acorn!
            // If it points to a prefab asset or a duplicate object in the scene, we override it.
            if (acornTarget == null || !acornTarget.transform.IsChildOf(transform))
            {
                AcornProp childAcorn = GetComponentInChildren<AcornProp>(true);
                if (childAcorn != null)
                {
                    Debug.LogWarning($"<color=orange>[SquirrelCharacter]</color> acornTarget on '{gameObject.name}' was {(acornTarget == null ? "NULL" : "assigned to a non-child object: " + acornTarget.name)}. Re-assigning to local child: '{childAcorn.name}' to prevent duplicate/shared target bugs.");
                    acornTarget = childAcorn;
                }
                else
                {
                    Debug.LogError($"<color=red>[SquirrelCharacter] ERROR:</color> No AcornProp found in children of '{gameObject.name}', and acornTarget is not assigned!");
                }
            }

            // Listen for the Acorn being smashed by the player
            if (acornTarget != null)
            {
                acornTarget.OnTargetDestroyed += HandleAcornSmashed;
            }
        }

        protected override void OnEnable()
        {
            // ===== DO NOT CALL base.OnEnable()! =====
            // BaseMole.OnEnable does harmful things for the squirrel:
            //   1. Pushes us 1.8m underground and starts MoleLifecycleRoutine
            //      which fights with our Animator by overriding localPosition.
            //   2. Re-enables all colliders (undoing our Awake disable).
            //   3. Starts breathing/sway animations that conflict with the character Animator.

            isHit = false;
            isPickingUp = false;

            // CRITICAL FIX: Reset spawnOrigin to visualOffset every spawn!
            // ObjectPooler now sets localPosition = Vector3.zero when parenting to the hole,
            // so the "hole surface" is always local (0,0,0). We add the visualOffset in case the prefab mesh is off-center.
            spawnOrigin = visualOffset;

            // === DEBUG: Trace spawn positioning ===
            Debug.Log($"<color=cyan>[Squirrel OnEnable]</color> '{gameObject.name}' spawning." +
                $"\n  Parent: {(transform.parent != null ? transform.parent.name : "NONE")}" +
                $"\n  Parent worldPos: {(transform.parent != null ? transform.parent.position.ToString("F4") : "N/A")}" +
                $"\n  My localPos: {transform.localPosition.ToString("F4")}" +
                $"\n  My worldPos: {transform.position.ToString("F4")}" +
                $"\n  My localScale: {transform.localScale.ToString("F4")}" +
                $"\n  spawnOrigin: {spawnOrigin.ToString("F4")}" +
                $"\n  hideDepth: {hideDepth}");

            // Re-enable Animator in case it was turned off by a previous hit
            if (anim != null)
            {
                anim.enabled = true;
                // Clear any leftover triggers from a previous cycle
                anim.ResetTrigger("Throw");
                anim.ResetTrigger("Pickup");
            }

            // Re-disable the squirrel body colliders every spawn
            // (in case anything re-enabled them)
            DisableBodyColliders();
            SetCollidersEnabled(false); // CRITICAL: Disable acorn target colliders while emerging/jumping!
            SetCollidersEnabled(false); // CRITICAL: Disable acorn target colliders while emerging/jumping!
            SetCollidersEnabled(false); // CRITICAL: Disable acorn target colliders while emerging/jumping!

            // Face the player camera on the Y axis
            FaceCamera();

            isScalingProgrammatically = false;
            transform.localScale = originalScale;

            // Reset the acorn to its unbroken state in the squirrel's hand
            if (acornTarget != null)
            {
                acornTarget.gameObject.SetActive(true);
                acornTarget.ResetProp();
            }

            // Play the sand explosion VFX on emergence
            PlaySandVFX();

            // Start the main lifecycle coroutine
            StopAllCoroutines();
            lifecycleCoroutine = StartCoroutine(SquirrelLifecycle());
        }

        #endregion

        protected override void OnDisable()
        {
            // Always hide the acorn when the squirrel hides, 
            // so it never gets left floating orphaned in the scene!
            if (acornTarget != null)
            {
                acornTarget.gameObject.SetActive(false);
            }

            // CRITICAL FIX: We must call base.OnDisable() so the MoleSpawner knows 
            // this hole is free again! Otherwise, the game stops spawning completely.
            base.OnDisable();
        }

        #region Core Lifecycle

        /// <summary>
        /// The main coroutine that controls the entire squirrel sequence.
        /// Throws the acorn, and handles pickup/hide sequence.
        /// </summary>
        private IEnumerator SquirrelLifecycle()
        {
            // --- 1. POP UP (Inverse Roll Sequence) ---
            // spawnOrigin is at the HOLE SURFACE (local 0,0,0).
            // Start underground at tiny scale, grow while rising — "popping out of a small hole" feel.
            Vector3 startPos = spawnOrigin + Vector3.down * hideDepth;
            Vector3 peekPosition = spawnOrigin;              // AT the hole surface (mole stands here)
            Vector3 overshootPosition = spawnOrigin + Vector3.up * 0.15f; // Small bounce above surface

            // === DEBUG: Trace lifecycle positions ===
            Debug.Log($"<color=yellow>[Squirrel Lifecycle]</color> Starting pop-up sequence." +
                $"\n  spawnOrigin (local): {spawnOrigin.ToString("F4")}" +
                $"\n  startPos (underground): {startPos.ToString("F4")}" +
                $"\n  peekPosition (surface): {peekPosition.ToString("F4")}" +
                $"\n  overshootPosition: {overshootPosition.ToString("F4")}" +
                $"\n  Parent worldPos: {(transform.parent != null ? transform.parent.position.ToString("F4") : "N/A")}" +
                $"\n  My worldPos BEFORE move: {transform.position.ToString("F4")}");

            // 1. Start deep underground, scaled down to tiny
            transform.localPosition = startPos;
            currentDynamicScale = GetTargetLocalScale(originalScale * 0.05f);
            isScalingProgrammatically = true;
            transform.localScale = currentDynamicScale;

            Debug.Log($"<color=yellow>[Squirrel Lifecycle]</color> Moved underground." +
                $"\n  localPos: {transform.localPosition.ToString("F4")}" +
                $"\n  worldPos: {transform.position.ToString("F4")}");

            // 2. Play the Roll animation
            if (anim != null)
            {
                anim.enabled = true;
                anim.Play("Squirrel Roll", 0, 0f);
            }

            // 3. Shoot up from hole to overshoot position while growing to 100%
            StartCoroutine(AnimateScale(originalScale, 0.35f));
            yield return StartCoroutine(AnimatePosition(overshootPosition, 0.35f, EaseType.EaseOut));

            // 4. Hang in the air while rolling (same as going down)
            yield return new WaitForSeconds(0.6f);

            // 5. Land properly on the ground
            if (anim != null)
            {
                // Disabling and re-enabling the Animator instantly stops any playing state 
                // and resets it to the default Entry state (Idle).
                anim.enabled = false;
                anim.enabled = true; 
                anim.Rebind(); // Force it to immediately forget the Roll state
                anim.Update(0f); // Force it to visually apply the default standing pose instantly
            }
            yield return StartCoroutine(AnimatePosition(peekPosition, 0.15f, EaseType.EaseIn));

            // Play the Throw animation now that we are up
            if (anim != null)
            {
                anim.SetTrigger("Throw");
            }

            // The squirrel has finished landing. The acorn is now hittable!
            SetCollidersEnabled(true);
            DisableBodyColliders(); // Ensure body remains unhittable just in case SetCollidersEnabled re-enabled it

            // The squirrel has finished landing. The acorn is now hittable!
            SetCollidersEnabled(true);
            DisableBodyColliders(); // Ensure body remains unhittable just in case SetCollidersEnabled re-enabled it

            // Wait for the hit window
            yield return new WaitForSeconds(hitWindow);

            // If the acorn was NOT smashed after the timeout, take it back!
            if (acornTarget != null && !acornTarget.IsBroken)
            {
                if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterMiss();

                isPickingUp = true;
                isHit = true; // Tell BaseMole not to auto-hide us so we control the exit!

                // Keep the acorn floating at its target height
                Vector3 dropPos = acornTarget.transform.position;
                acornTarget.MoveAcornToTarget(dropPos, 0.15f, false);
                yield return new WaitForSeconds(0.2f);

                // Play the Pickup animation
                if (anim != null) anim.SetTrigger("Pickup");

                // Wait for half the animation to play so the hands physically reach down
                yield return new WaitForSeconds(0.3f); // Reduced to snap earlier

                // Attach the acorn to the hands so it pulls up smoothly
                if (leftHandBone != null && rightHandBone != null)
                {
                    acornTarget.AnimateAcornToHands(leftHandBone, rightHandBone);
                }

                // Give the squirrel enough time to complete the animation perfectly
                yield return new WaitForSeconds(0.9f); // Adjusted to maintain total animation duration

                // Smooth drop back into the hole
                yield return StartCoroutine(HideSmoothly());
            }
        }

        #endregion

        #region Animation Events (Called by Unity Animator)

        /// <summary>
        /// Animation Event: Called at the exact frame the squirrel's hand opens to throw.
        /// You must place this event on the Throw animation clip timeline.
        /// </summary>
        public void OnThrowRelease()
        {
            if (acornTarget != null)
            {
                acornTarget.ThrowForward();
            }
        }

        /// <summary>
        /// Animation Event: Called when the Pickup animation finishes.
        /// SAFETY: Only hides the squirrel if isPickingUp is true.
        /// This prevents the Animator's auto-transition (Throw→Pickup)
        /// from accidentally hiding the squirrel before the player even had a chance to swing!
        /// </summary>
        public void OnPickupComplete()
        {
            // CRITICAL SAFETY CHECK:
            // If the Animator auto-transitions from Throw to Pickup,
            // this event will fire prematurely. We IGNORE it unless
            // we explicitly set isPickingUp = true.
            if (!isPickingUp)
            {
                return;
            }

            // We do NOT call gameObject.SetActive(false) here anymore.
            // The coroutine (SquirrelLifecycle or SmashedPickupSequence) 
            // will naturally call HideSmoothly() after the hideDelay.
        }

        #endregion

        #region Acorn Smashed Handler

        /// <summary>
        /// Called when the player successfully breaks the acorn with the hammer.
        /// </summary>
        private void HandleAcornSmashed()
        {
            // CRITICAL FIX: Stop all coroutines to prevent AnimateScale/AnimatePosition 
            // from the popup sequence fighting with the drop sequence if hit early!
            StopAllCoroutines();

            isPickingUp = true;
            isHit = true; // Tell BaseMole not to auto-hide us so we control the exit!

            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(SmashedPickupSequence());
            }
        }

        private IEnumerator SmashedPickupSequence()
        {
            if (acornTarget != null)
            {
                // 1. Drop seeds straight down to the table surface (with a small 5cm offset so it doesn't sink)
                float yOffset = 0.05f;
                Vector3 currentPos = acornTarget.transform.position;
                Vector3 dropPos = new Vector3(currentPos.x, transform.position.y + yOffset, currentPos.z);
                acornTarget.MoveSeedsToTarget(dropPos, 0.15f, false);
                yield return new WaitForSeconds(0.15f);

                // 2. Slide closer to the squirrel
                Vector3 slidePos = transform.position + (transform.forward * 0.08f);
                slidePos.y = transform.position.y + yOffset;
                acornTarget.MoveSeedsToTarget(slidePos, 0.25f, false);
                yield return new WaitForSeconds(0.25f);

                // 3. Play the pickup animation
                if (anim != null) anim.SetTrigger("Pickup");

                // 4. Wait for half the animation to play so the hands physically reach down
                yield return new WaitForSeconds(0.3f); // Reduced to snap earlier
            }

            // 5. Attach seeds to hands
            if (acornTarget != null && leftHandBone != null && rightHandBone != null)
            {
                acornTarget.AnimateSeedsToHands(leftHandBone, rightHandBone);
            }

            // 6. Give the squirrel enough time to complete the animation perfectly
            yield return new WaitForSeconds(0.9f); // Adjusted to maintain total animation duration

            // Deactivate the broken shell pieces sitting on the table BEFORE the squirrel moves down.
            // This prevents the illusion of the pieces sinking into the hole with her.
            if (acornTarget != null && acornTarget.brokenMeshParent != null)
            {
                acornTarget.brokenMeshParent.SetActive(false);
            }

            // 7. Smooth drop back into the hole
            yield return StartCoroutine(HideSmoothly());
        }

        private IEnumerator HideSmoothly()
        {
            // Disable colliders immediately
            DisableBodyColliders();
            if (acornTarget != null)
            {
                Collider[] acornCols = acornTarget.GetComponentsInChildren<Collider>();
                foreach (Collider c in acornCols) c.enabled = false;
            }

            // 1. Play the Cheer/Success animation first!
            if (anim != null)
            {
                anim.enabled = true;
                anim.SetTrigger(happyAnimTrigger);
            }
            yield return new WaitForSeconds(happyDuration);

            // Play the sand explosion VFX rotated downwards for a vacuum effect right as it dives!
            PlaySandVFX(true);

            // Ensure the dive happens perfectly straight down the hole, ignoring any drift from the animation!
            Vector3 currentLocalPos = spawnOrigin;

            // 2. Jump up quickly
            yield return StartCoroutine(AnimatePosition(currentLocalPos + Vector3.up * 0.15f, 0.15f, EaseType.EaseOut));

            // 2. Play the roll animation
            if (anim != null)
            {
                anim.enabled = true;
                anim.Play("Squirrel Roll", 0, 0f);
                UnityEngine.Debug.Log("[Squirrel] Played 'Squirrel Roll' animation! Animator is active.");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Squirrel] Animator is NULL! Cannot play roll animation.");
            }

            // 3. Hold in the air while rolling so the animation actually plays out!
            yield return new WaitForSeconds(0.8f);

            // 4. Shrink (to 10%) and drop simultaneously over a visible 0.5s
            // We KEEP rolling for the first 80% of this drop cycle!
            isScalingProgrammatically = true;
            StartCoroutine(AnimateScale(originalScale * 0.1f, 0.5f));
            Coroutine dropRoutine = StartCoroutine(AnimatePosition(currentLocalPos + Vector3.down * hideDepth, 0.5f, EaseType.EaseIn));

            // Wait 80% of the drop time (0.4s out of 0.5s)
            yield return new WaitForSeconds(0.4f);

            // CRITICAL FIX: Stop revolving and force straight for the last 20% of the drop!
            if (anim != null)
            {
                anim.enabled = false;
                anim.enabled = true;
                anim.Rebind(); // Forget the Roll state
                anim.Update(0f); // Snap to straight Idle pose instantly
            }

            // Wait the remaining 20% (0.1s) for the drop to finish
            yield return dropRoutine;

            // Wait a tiny bit extra to ensure smooth completion (0.15s guarantees it's fully underground)
            yield return new WaitForSeconds(0.15f);

            // Reset scale and flags for the next spawn AFTER deactivating (so it resets cleanly while invisible)
            gameObject.SetActive(false);
            isScalingProgrammatically = false;
            transform.localScale = originalScale;
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(hideDelay);
            gameObject.SetActive(false);
        }

        #endregion

        #region Helpers

        private void DisableBodyColliders()
        {
            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (Collider c in cols)
            {
                // Keep the acorn's colliders active so the hammer can hit them!
                if (acornTarget == null || !c.transform.IsChildOf(acornTarget.transform))
                {
                    c.enabled = false;
                }
            }
        }

        private void FaceCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 lookDir = cam.transform.position - transform.position;
                lookDir.y = 0; // Only rotate horizontally
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }

        // Override BaseMole duration so it doesn't auto-hide
        protected override float GetVisibleDuration()
        {
            return 99f;
        }

        // Override and IGNORE the old hitting logic — the squirrel body cannot be hit
        public override void OnHit(Vector3 velocity, Vector3 hitPosition) { }
        protected override void TriggerFeedback(Vector3 hitPosition, Vector3 velocity, int holeIndex) { }

        private void PlaySandVFX(bool suckInwards = false)
        {
            if (sandExplosionVFX == null) return;

            // Use the hole surface world position
            Vector3 pos = transform.parent != null
                ? transform.parent.position + Vector3.up * 0.05f
                : transform.position + Vector3.up * 0.05f;
            
            // Default explosion points UP (-90 on X). Suck-in points DOWN (90 on X).
            Quaternion rot = suckInwards ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(-90f, 0f, 0f);

            if (ObjectPooler.Instance != null)
            {
                GameObject instance = ObjectPooler.Instance.SpawnOrAddPool(
                    "VFX_" + sandExplosionVFX.name,
                    sandExplosionVFX,
                    5,
                    pos,
                    rot
                );

                if (instance != null)
                {
                    ParticleSystem ps = instance.GetComponent<ParticleSystem>();
                    if (ps != null) ps.Play(true);

                    ObjectPooler.Instance.ReturnToPool(instance, 1.5f);
                }
            }
            else
            {
                GameObject instance = Instantiate(sandExplosionVFX, pos, rot);
                Destroy(instance, 1.5f);
            }
        }

        #endregion
    }
}
