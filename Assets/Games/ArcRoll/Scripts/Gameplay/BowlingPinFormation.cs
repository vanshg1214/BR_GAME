using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArcRoll.Core;

namespace ArcRoll.Gameplay
{
    /// <summary>
    /// Attached to the root GameObject of a 10-pin formation.
    /// Manages scoring (only when ball actually hits), cleanup timing,
    /// and guarantees the lane is CLEAR before the next target can spawn.
    /// </summary>
    public class BowlingPinFormation : MonoBehaviour
    {
        [Header("Cleanup")]
        [Tooltip("Seconds after the ball stops before pins start despawning.")]
        [SerializeField] private float despawnDelay = 2.0f;

        // All the pins in this formation — populated in Awake so it's ready before RegisterBall is called
        private readonly List<BowlingPin> pins = new List<BowlingPin>();

        private Ball associatedBall = null;
        private bool ballHitAtLeastOnePin = false;
        private bool cleanupStarted = false;

        private bool strikeDetected = false;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // NOTE: When pins are spawned dynamically (via coroutine), Awake runs BEFORE
            // any pins exist. InitPins() is called manually by LevelDirector after pins are added.
            // This Awake is kept as a safety fallback for prefab-based usage.
            InitPins();
        }

        /// <summary>
        /// Call this AFTER all child BowlingPin objects have been instantiated and parented.
        /// Required when pins are spawned dynamically — Awake() runs too early in that case.
        /// </summary>
        public void InitPins()
        {
            pins.Clear();
            pins.AddRange(GetComponentsInChildren<BowlingPin>(true));
            Debug.Log($"[BowlingPinFormation] InitPins — found {pins.Count} pins.");

            // Tell every child pin who their formation manager is (push, not pull)
            foreach (var pin in pins)
                pin.SetFormation(this);
        }

        private void OnDestroy()
        {
            if (associatedBall != null)
                associatedBall.OnStateChanged -= OnBallStateChanged;
        }

        private void Update()
        {
            if (ballHitAtLeastOnePin && !strikeDetected)
            {
                int fallen = 0;
                foreach (var pin in pins)
                {
                    if (pin == null || pin.IsKnockedDown)
                        fallen++;
                }

                // The millisecond all 10 pins are down, trigger the Strike!
                if (fallen >= 10)
                {
                    strikeDetected = true;
                    if (ArcRollMotivationalManager.Instance != null)
                    {
                        ArcRollMotivationalManager.Instance.ReportScore(ArcRollMotivationalManager.SportType.Bowling, true);
                    }
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by ArcRollLevelDirector right after spawning the formation.
        /// </summary>
        public void RegisterBall(Ball ball)
        {
            if (ball == null) return;
            associatedBall = ball;
            associatedBall.OnStateChanged += OnBallStateChanged;
            Debug.Log($"[BowlingPinFormation] Ball registered. Tracking {pins.Count} pins.");
        }

        /// <summary>
        /// Called by a child BowlingPin when the real bowling ball physically collides with it.
        /// </summary>
        public void NotifyBallTouched()
        {
            if (!ballHitAtLeastOnePin)
            {
                ballHitAtLeastOnePin = true;
                if (associatedBall != null)
                {
                    associatedBall.hasScored = true; // Prevent the ball despawn from resetting the streak!
                }
                
                Debug.Log("[BowlingPinFormation] Ball touched a pin! Score will be counted.");
            }
        }

        // ── Ball State ────────────────────────────────────────────────────────

        private void OnBallStateChanged(Ball ball, Ball.BallState state)
        {
            if ((state != Ball.BallState.Dead && state != Ball.BallState.Missed) || cleanupStarted) return;

            cleanupStarted = true;
            if (associatedBall != null)
                associatedBall.OnStateChanged -= OnBallStateChanged;

            StartCoroutine(ScoreAndCleanup());
        }

        private IEnumerator ScoreAndCleanup()
        {
            // Wait the despawn delay BEFORE scoring and cleaning up
            yield return new WaitForSeconds(despawnDelay);

            // Count how many pins are fallen right now
            int fallen = 0;
            foreach (var pin in pins)
            {
                if (pin == null || pin.IsKnockedDown)
                    fallen++;
            }

            Debug.Log($"[BowlingPinFormation] Cleanup — ballHit={ballHitAtLeastOnePin}, fallen={fallen}/{pins.Count}");

            // Only score if the ball actually touched at least one pin in THIS group
            if (ballHitAtLeastOnePin && fallen > 0)
            {
                if (ArcRollScoreManager.Instance != null)
                {
                    ArcRollScoreManager.Instance.IncrementStreak(); // Increase combo streak!
                    ArcRollScoreManager.Instance.AddScore(fallen);
                }

                // If it wasn't a strike, play the non-perfect announcer sound now that the dust has settled
                if (ArcRollMotivationalManager.Instance != null && !strikeDetected)
                {
                    ArcRollMotivationalManager.Instance.ReportScore(ArcRollMotivationalManager.SportType.Bowling, false);
                }

                Debug.Log($"[BowlingPinFormation] +{fallen} points awarded!");
            }
            else if (!ballHitAtLeastOnePin)
            {
                Debug.Log("[BowlingPinFormation] Ball never touched the pins — no score.");
            }

            // Destroy entire formation
            Destroy(gameObject);
        }
    }
}
