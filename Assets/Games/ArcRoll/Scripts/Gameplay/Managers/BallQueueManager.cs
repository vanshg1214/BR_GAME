using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ArcRoll.Gameplay
{
    /// <summary>
    /// Manages the entire lifecycle of balls in the scene:
    ///   • Max 5 balls alive at any time
    ///   • Max 1 ball waiting at the ROM rest position
    ///   • Next shot fires only after the current ball is Grabbed
    ///   • Balls ignore each other physically (no chaotic collisions)
    ///   • Tracks ISDK grab/release and notifies Ball.cs via its public API
    /// </summary>
    public class BallQueueManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Limits")]
        [Tooltip("Maximum number of ball GameObjects allowed in the scene at once.")]
        [SerializeField] private int maxBallsInScene = 5;

        [Header("Ball Layer")]
        [Tooltip("Name of the Physics Layer that all balls will be moved to so they don't collide with each other.")]
        [SerializeField] private string ballLayerName = "ArcRollBall";

        [Header("Timing")]
        [Tooltip("Seconds to wait after a ball is grabbed before firing the next one.")]
        [SerializeField] private float delayAfterGrab = 0.5f;

        [Header("Director Reference")]
        [SerializeField] private ArcRoll.Core.ArcRollLevelDirector levelDirector;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly List<Ball> activeBalls = new List<Ball>();
        private readonly List<ArcRoll.Gameplay.Frisbee.Frisbee> activeFrisbees = new List<ArcRoll.Gameplay.Frisbee.Frisbee>();
        private bool isWaitingForGrab = false;   // A ball or frisbee is sitting at the rest position
        private bool nextShotPending  = false;   // Director has a shot queued up

        // Queue of pending fire requests from the Level Director
        private readonly Queue<System.Action> shotQueue = new Queue<System.Action>();

        // Cached layer int
        private int ballLayer = -1;

        // ── Unity ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            ballLayer = LayerMask.NameToLayer(ballLayerName);
            if (ballLayer == -1)
                Debug.LogWarning($"[BallQueueManager] Physics Layer '{ballLayerName}' not found. " +
                                 "Create it in Edit > Project Settings > Tags & Layers. " +
                                 "Balls will collide with each other until then.");
            else
                // Make balls ignore each other
                Physics.IgnoreLayerCollision(ballLayer, ballLayer, true);

            if (levelDirector == null)
            {
                levelDirector = FindFirstObjectByType<ArcRoll.Core.ArcRollLevelDirector>();
            }
        }

        private void Update()
        {
            // Remove any null (destroyed) balls and frisbees from the list
            activeBalls.RemoveAll(b => b == null);
            activeFrisbees.RemoveAll(f => f == null);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by ArcRollLevelDirector to request a shot.
        /// The shot is queued and only executed when conditions allow.
        /// </summary>
        public void RequestShot(System.Action fireAction)
        {
            shotQueue.Enqueue(fireAction);
            TryFireNext();
        }

        /// <summary>
        /// Called by CannonController immediately after it instantiates the ball,
        /// so BallQueueManager can start tracking it.
        /// </summary>
        public void RegisterBall(Ball ball)
        {
            if (ball == null) return;

            // Removed the script-forced layer override so balls can keep their unique layers (Basketball vs BowlingBall)

            ball.OnStateChanged += OnBallStateChanged;
            activeBalls.Add(ball);

            isWaitingForGrab = true;
        }

        /// <summary>
        /// Called by ArcRollLevelDirector immediately after it instantiates a Frisbee,
        /// so BallQueueManager can start tracking it.
        /// </summary>
        public void RegisterFrisbee(ArcRoll.Gameplay.Frisbee.Frisbee frisbee)
        {
            if (frisbee == null) return;

            frisbee.OnStateChanged += OnFrisbeeStateChanged;
            activeFrisbees.Add(frisbee);

            isWaitingForGrab = true;
        }

        /// <summary>Returns how many balls/frisbees are currently alive in the scene.</summary>
        public int ActiveBallCount => activeBalls.Count + activeFrisbees.Count;

        // ── ISDK Grab Polling ─────────────────────────────────────────────────
        // All ISDK Reflection code and polling loops have been removed!
        // Grab detection is now perfectly handled by the physical distance anchor in Ball.cs.

        // ── State Reactions ───────────────────────────────────────────────────
        private void OnBallStateChanged(Ball ball, Ball.BallState newState)
        {
            switch (newState)
            {
                case Ball.BallState.Grabbed:
                    isWaitingForGrab = false;
                    // We no longer fire the next ball on grab! We wait until this ball is DEAD.
                    break;

                case Ball.BallState.Missed:
                case Ball.BallState.Dead:
                    ball.OnStateChanged -= OnBallStateChanged;
                    if (activeBalls.Contains(ball))
                    {
                        activeBalls.Remove(ball);
                        // Start tracking cleanup and wait 0.5s before spawning next target/ball
                        StartCoroutine(FireNextAfterDelayCoroutine());
                    }
                    break;
            }
        }

        private void OnFrisbeeStateChanged(ArcRoll.Gameplay.Frisbee.Frisbee frisbee, ArcRoll.Gameplay.Frisbee.Frisbee.FrisbeeState newState)
        {
            switch (newState)
            {
                case ArcRoll.Gameplay.Frisbee.Frisbee.FrisbeeState.Grabbed:
                    isWaitingForGrab = false;
                    break;

                case ArcRoll.Gameplay.Frisbee.Frisbee.FrisbeeState.Missed:
                case ArcRoll.Gameplay.Frisbee.Frisbee.FrisbeeState.Dead:
                    frisbee.OnStateChanged -= OnFrisbeeStateChanged;
                    if (activeFrisbees.Contains(frisbee))
                    {
                        activeFrisbees.Remove(frisbee);
                        // Start tracking cleanup and wait 0.5s before spawning next target/frisbee
                        StartCoroutine(FireNextAfterDelayCoroutine());
                    }
                    break;
            }
        }


        private IEnumerator FireNextAfterDelayCoroutine()
        {
            // 1. Wait until all active balls and frisbees are completely destroyed/null from the scene
            while (activeBalls.Count > 0 || activeFrisbees.Count > 0)
            {
                activeBalls.RemoveAll(b => b == null);
                activeFrisbees.RemoveAll(f => f == null);
                yield return null;
            }

            // 2. Wait until the current target is completely destroyed/null (gone from the scene)
            if (levelDirector != null)
            {
                while (!levelDirector.IsTargetDestroyed)
                {
                    yield return null;
                }
            }

            // 3. Wait the exact 0.5 seconds clean transition delay
            yield return new WaitForSeconds(0.5f);

            // 4. Try spawning the next target and projectile
            TryFireNext();
        }

        private void TryFireNext()
        {
            // If the timer ended, ArcRollGameManager.isGameActive will be false, so stop firing!
            if (ArcRoll.Core.ArcRollGameManager.Instance != null && !ArcRoll.Core.ArcRollGameManager.Instance.isGameActive) return;

            // Don't fire if:
            // (a) A ball/frisbee is already sitting at the rest position waiting to be grabbed
            // (b) We're already at the max ball cap
            // (c) No shots are queued
            // (d) The previous target is still visible/cleaning up
            if (isWaitingForGrab) return;
            if (activeBalls.Count + activeFrisbees.Count >= maxBallsInScene) return;
            if (shotQueue.Count == 0) return;
            if (levelDirector != null && !levelDirector.IsTargetDestroyed) return;

            var fireAction = shotQueue.Dequeue();
            fireAction?.Invoke();
        }

        /// <summary>
        /// Clears pending shots and destroys any balls/frisbees that have not been grabbed yet.
        /// Returns true if at least one ungrabbed ball/frisbee was destroyed.
        /// </summary>
        public bool ClearUngrabbedShots()
        {
            shotQueue.Clear();
            bool didClear = false;

            for (int i = activeBalls.Count - 1; i >= 0; i--)
            {
                var b = activeBalls[i];
                if (b != null && (b.State == Ball.BallState.InRack || b.State == Ball.BallState.AtRestPosition || b.State == Ball.BallState.TravelingToTarget))
                {
                    activeBalls.RemoveAt(i);
                    Destroy(b.gameObject);
                    didClear = true;
                }
            }

            for (int i = activeFrisbees.Count - 1; i >= 0; i--)
            {
                var f = activeFrisbees[i];
                if (f != null && (f.State == ArcRoll.Gameplay.Frisbee.Frisbee.FrisbeeState.InRack || f.State == ArcRoll.Gameplay.Frisbee.Frisbee.FrisbeeState.AtRestPosition || f.State == ArcRoll.Gameplay.Frisbee.Frisbee.FrisbeeState.TravelingToTarget))
                {
                    activeFrisbees.RemoveAt(i);
                    Destroy(f.gameObject);
                    didClear = true;
                }
            }

            if (didClear) isWaitingForGrab = false;
            return didClear;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
