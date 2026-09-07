using UnityEngine;
using System.Collections;

namespace WhackAMole
{
    /// <summary>
    /// A decoy target that always holds an acorn.
    /// If the player hits this squirrel, they are penalized, the squirrel plays an angry animation, and then retreats.
    /// </summary>
    public class DecoySquirrel : BaseMole
    {
        [Header("Decoy Settings")]
        [Tooltip("The exact name of the Trigger parameter in the Animator to play the angry/failure animation.")]
        [SerializeField] private string angryTrigger = "Failure";
        [Tooltip("How long to wait for the angry animation to play before hiding.")]
        [SerializeField] private float angryAnimationDuration = 1.5f;

        [Header("Animation Settings")]
        [Tooltip("The exact name of the Trigger parameter in the Animator to play the success/cheer animation when retreating un-hit.")]
        [SerializeField] private string happyAnimTrigger = "Success";
        [Tooltip("How long to wait for the cheer animation to play before jumping and rolling.")]
        [SerializeField] private float happyDuration = 1.0f;

        [Header("Penalty Settings")]
        [Tooltip("How many points the player loses for hitting the decoy.")]
        [SerializeField] private int scorePenalty = 20;
        [Tooltip("Sound to play when the player mistakenly hits the squirrel.")]
        [SerializeField] private AudioClip angrySoundFX;

        [Header("VFX References")]
        [Tooltip("Assign the sand explosion VFX prefab here to play on popup.")]
        [SerializeField] private GameObject sandExplosionVFX;

        protected override void OnEnable()
        {
            // Call base.OnEnable so it handles the smooth popup animation perfectly.
            base.OnEnable();
            
            // Play the sand explosion VFX on emergence
            PlaySandVFX();

            // Re-enable the Animator just in case it was disabled from a previous hit
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = true;
        }

        public override void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            if (isHit) return;
            isHit = true; // Mark as hit so BaseMole stops the normal auto-hide timer

            // 1. Penalize the player
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(-scorePenalty, velocity.magnitude, false); 
            }

            if (FeedbackManager.Instance != null)
            {
                // Play a negative hit feedback (red sparks, buzz sound, etc.)
                FeedbackManager.Instance.PlayFakeHit(hitPosition); 
            }

            if (angrySoundFX != null)
            {
                AudioSource.PlayClipAtPoint(angrySoundFX, hitPosition);
            }

            // 2. Play the angry/failure animation
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger(angryTrigger);
            }

            // 3. Stop BaseMole's auto-hide and start our custom Angry & Leave sequence
            StopAllCoroutines(); 
            StartCoroutine(AngryAndLeaveSequence());
        }

        private IEnumerator AngryAndLeaveSequence()
        {
            // Wait for the angry animation to play out
            yield return new WaitForSeconds(angryAnimationDuration);

            // Disable colliders immediately
            SetCollidersEnabled(false);

            // Play the sand explosion VFX rotated downwards for a vacuum effect right as it dives!
            PlaySandVFX(true);

            // Ensure the dive happens perfectly straight down the hole, ignoring any drift from the animation!
            Vector3 currentLocalPos = spawnOrigin;

            // 1. Jump up quickly
            yield return StartCoroutine(AnimatePosition(currentLocalPos + Vector3.up * 0.15f, 0.15f, EaseType.EaseOut));

            // 2. FORCE play the Roll animation directly by state name
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.enabled = true;
                anim.Play("Squirrel Roll", 0, 0f); // User must create this state in Animator
            }

            // 3. Hold in the air while rolling (fast)
            yield return new WaitForSeconds(0.8f);

            // CRITICAL FIX: Stop revolving and force the character perfectly straight before diving!
            if (anim != null)
            {
                anim.enabled = false;
                anim.enabled = true;
                anim.Rebind(); // Forget the Roll state
                anim.Update(0f); // Snap to straight Idle pose instantly
            }

            // 4. Shrink and drop simultaneously - fast!
            isScalingProgrammatically = true;
            StartCoroutine(AnimateScale(originalScale * 0.05f, 0.2f)); // Shrink to almost nothing
            yield return StartCoroutine(AnimatePosition(currentLocalPos + Vector3.down * hideDepth, 0.2f, EaseType.EaseIn));

            // Reset scale for the next spawn AFTER deactivating (so it resets cleanly while invisible)
            // Wait a tiny bit extra to ensure smooth completion (0.15s guarantees it's fully underground)
            yield return new WaitForSeconds(0.15f);
            
            // Deactivate and return to the object pool
            gameObject.SetActive(false);
            isScalingProgrammatically = false;
            transform.localScale = GetTargetLocalScale(originalScale);
        }

        // Override TriggerFeedback to do nothing, since we handled custom penalty feedback in OnHit.
        protected override void TriggerFeedback(Vector3 hitPosition, Vector3 velocity, int holeIndex)
        {
        }

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

        protected override IEnumerator MoleLifecycleRoutine()
        {
            // Play ground popup sound
            if (PlaysPopupSound && FeedbackManager.Instance != null)
            {
                FeedbackManager.Instance.PlayGroundPopup(transform.position);
            }

            // --- 1. POP UP (Inverse Roll Sequence) ---
            // spawnOrigin is at the HOLE SURFACE (local 0,0,0).
            // Same design as BaseMole: start underground tiny, grow while rising to surface.
            Vector3 startPos = spawnOrigin + Vector3.down * hideDepth;
            Vector3 peekPosition = spawnOrigin; // AT hole surface, mole stands above table
            Vector3 overshootPosition = peekPosition + Vector3.up * 0.15f; // Same jump height

            // 1. Start underground, scaled down to 20%
            transform.localPosition = startPos;
            currentDynamicScale = GetTargetLocalScale(originalScale * 0.2f);
            isScalingProgrammatically = true;
            transform.localScale = currentDynamicScale;

            // 2. Play the Roll animation
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.enabled = true;
                anim.Play("Squirrel Roll", 0, 0f);
            }

            // 3. Shoot up from hole to overshoot position while growing to 100%
            StartCoroutine(AnimateScale(originalScale, 0.25f));
            yield return StartCoroutine(AnimatePosition(overshootPosition, 0.25f, EaseType.EaseOut));

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

            SetCollidersEnabled(true);

            // --- 2. IDLE BREATHING & SWAY ---
            float elapsed = 0f;
            while (elapsed < currentVisibleDuration && !isHit)
            {
                elapsed += Time.deltaTime;
                isScalingProgrammatically = true;
                float pulse = 1f + Mathf.Sin(elapsed * 4f) * 0.03f;
                currentDynamicScale = GetTargetLocalScale(new Vector3(originalScale.x * pulse, originalScale.y * (2f - pulse), originalScale.z * pulse));
                float sway = Mathf.Sin(elapsed * 2.5f) * 0.005f;
                transform.localPosition = peekPosition + Vector3.right * sway;
                yield return null;
            }

            isScalingProgrammatically = false;
            transform.localScale = GetTargetLocalScale(originalScale);

            // --- 3. AUTO-HIDE (Cheer, Jump, Roll, Shrink, Underground) ---
            if (!isHit)
            {
                // 1. Play the Cheer/Success animation first!
                anim = GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.enabled = true;
                    anim.SetTrigger(happyAnimTrigger);
                }
                yield return new WaitForSeconds(happyDuration);

                SetCollidersEnabled(false);
                PlaySandVFX(true);

                // Ensure the dive happens perfectly straight down the hole, ignoring any drift from the animation!
                Vector3 currentLocalPos = spawnOrigin;

                // 2. Jump up quickly
                yield return StartCoroutine(AnimatePosition(currentLocalPos + Vector3.up * 0.15f, 0.15f, EaseType.EaseOut));

                // 2. FORCE play the Roll animation directly by state name
                anim = GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.enabled = true;
                    anim.Play("Squirrel Roll", 0, 0f);
                }

                // 3. Hold in the air while rolling
                yield return new WaitForSeconds(0.8f);

                // CRITICAL FIX: Stop revolving and force the character perfectly straight before diving!
                if (anim != null)
                {
                    anim.enabled = false;
                    anim.enabled = true;
                    anim.Rebind(); // Forget the Roll state
                    anim.Update(0f); // Snap to straight Idle pose instantly
                }

                // 4. Shrink and drop underground simultaneously
                isScalingProgrammatically = true;
                StartCoroutine(AnimateScale(originalScale * 0.05f, 0.2f));
                yield return StartCoroutine(AnimatePosition(currentLocalPos + Vector3.down * hideDepth, 0.2f, EaseType.EaseIn));

                // Wait a tiny bit extra to ensure smooth completion (0.15s guarantees it's fully underground)
                yield return new WaitForSeconds(0.15f);
                
                gameObject.SetActive(false);
                isScalingProgrammatically = false;
                transform.localScale = GetTargetLocalScale(originalScale);
            }
        }

        public override bool IsFakeOrDecoy => true;
        protected override bool PlaysPopupSound => true;
    }
}
