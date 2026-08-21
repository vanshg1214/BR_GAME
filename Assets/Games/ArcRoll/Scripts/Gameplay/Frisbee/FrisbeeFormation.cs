using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArcRoll.Core;

namespace ArcRoll.Gameplay.Frisbee
{
    public class FrisbeeFormation : MonoBehaviour
    {
        [Header("Cleanup")]
        [SerializeField] private float despawnDelay = 2.0f;

        private readonly List<FrisbeeTarget> targets = new List<FrisbeeTarget>();

        private Frisbee associatedFrisbee = null;
        private bool frisbeeHitAtLeastOneTarget = false;
        private bool cleanupStarted = false;

        private void Awake()
        {
            targets.AddRange(GetComponentsInChildren<FrisbeeTarget>(true));
            foreach (var target in targets)
            {
                target.SetFormation(this);
            }

            // Keep all child rigidbodies kinematic so the pyramid is 100% stable
            // and doesn't collapse on spawn or slide around.
            SetAllKinematic(true);
        }

        private void SetAllKinematic(bool kinematic)
        {
            Rigidbody[] childRbs = GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in childRbs)
            {
                if (rb != null)
                {
                    rb.isKinematic = kinematic;
                    // Always preserve ContinuousSpeculative so Frisbees never tunnel through on high-speed hits
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    if (!kinematic)
                    {
                        // Wake it up so it responds to physics immediately
                        rb.WakeUp();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (associatedFrisbee != null)
                associatedFrisbee.OnStateChanged -= OnFrisbeeStateChanged;
        }

        public void RegisterFrisbee(Frisbee frisbee)
        {
            if (frisbee == null) return;
            associatedFrisbee = frisbee;
            associatedFrisbee.OnStateChanged += OnFrisbeeStateChanged;
        }

        public void NotifyFrisbeeTouched(Frisbee touchingFrisbee)
        {
            if (!frisbeeHitAtLeastOneTarget)
            {
                frisbeeHitAtLeastOneTarget = true;
                
                // Wake up all targets to fall naturally now!
                SetAllKinematic(false);

                // Play instant arcade feedback! Assume perfect if there's only 1 target, otherwise random good shot
                if (ArcRollMotivationalManager.Instance != null)
                {
                    bool isPerfect = targets.Count == 1; // E.g. Balloon pop is always perfect
                    ArcRollMotivationalManager.Instance.ReportScore(ArcRollMotivationalManager.SportType.Frisbee, isPerfect);
                }

                if (touchingFrisbee != null)
                {
                    touchingFrisbee.hasScored = true;
                }
                else if (associatedFrisbee != null)
                {
                    associatedFrisbee.hasScored = true;
                }
            }
        }

        private void OnFrisbeeStateChanged(Frisbee frisbee, Frisbee.FrisbeeState state)
        {
            if ((state != Frisbee.FrisbeeState.Dead && state != Frisbee.FrisbeeState.Missed) || cleanupStarted) return;

            cleanupStarted = true;
            if (associatedFrisbee != null)
                associatedFrisbee.OnStateChanged -= OnFrisbeeStateChanged;

            StartCoroutine(ScoreAndCleanup());
        }

        private IEnumerator ScoreAndCleanup()
        {
            yield return new WaitForSeconds(despawnDelay);

            int fallenCount = 0;
            int totalScore = 0;
            
            foreach (var target in targets)
            {
                if (target == null || target.IsKnockedDown)
                {
                    fallenCount++;
                    if (target != null)
                    {
                        totalScore += target.scoreValue;
                    }
                    else
                    {
                        totalScore += 1; // Fallback if destroyed early
                    }
                }
            }

            if (frisbeeHitAtLeastOneTarget && fallenCount > 0)
            {
                if (ArcRollScoreManager.Instance != null)
                {
                    ArcRollScoreManager.Instance.IncrementStreak();
                    ArcRollScoreManager.Instance.AddScore(totalScore);
                }
            }

            Destroy(gameObject);
        }
    }
}
