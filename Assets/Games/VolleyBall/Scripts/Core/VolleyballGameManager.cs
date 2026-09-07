using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using Rehab.Volleyball.Data;
using Rehab.Volleyball.Mechanics;

namespace Rehab.Volleyball.Core
{
    /// <summary>
    /// Singleton manager that coordinates the core gameplay loop.
    /// Uses a strict state machine to prevent double-serves, double-scoring, and wrong point attribution.
    /// </summary>
    public partial class VolleyballGameManager : MonoBehaviour
    {
        public static VolleyballGameManager Instance { get; private set; }

        public enum DifficultyMode
        {
            Easy = 0,
            Medium = 1,
            Hard = 2
        }

        [System.Serializable]
        public struct AIDifficultySettings
        {
            [Tooltip("Speed at which the opponent moves side-to-side to intercept the ball (m/s).")]
            public float moveSpeed;
            
            [Tooltip("Idle time in seconds before the AI begins reacting to the ball's trajectory.")]
            public float reactionDelay;
            
            [Tooltip("Multiplier applied to the return strike force. Higher means faster returns.")]
            public float forceFactor;
            
            [Tooltip("Probability (0.0 to 1.0) that the AI strikes the ball cleanly towards the player.")]
            public float hitAccuracy;
        }

        // ─── Game States ─────────────────────────────────────
        private enum GameState
        {
            WaitingToServe,   // Between points, delay before next serve
            PlayerServing,    // Ball floating in front of player, waiting to be hit
            AIServing,        // AI is winding up to serve
            RallyActive,      // Ball is in play — the only state where scoring can happen
            PointScored       // A point just happened — locked out from further scoring
        }
        private GameState state = GameState.WaitingToServe;

        [Header("System References")]
        [SerializeField] private VolleyballBall activeBall;
        public VolleyballBall ActiveBall => activeBall;
        [SerializeField] private VolleyballOpponent opponentAI;
        [Tooltip("The root of the VR player rig (used for general positioning).")]
        [SerializeField] private Transform playerTransform;
        [Tooltip("The specific VR Head/Camera transform (CenterEyeAnchor). If left empty, it will try to find Camera.main.")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform netTransform;
        public Transform NetTransform => netTransform;

        [Header("Court Boundaries")]
        [Tooltip("Box Collider defining the AI's court area.")]
        public Collider aiCourtBounds;
        [Tooltip("Box Collider defining the Player's court area.")]
        public Collider playerCourtBounds;

        [Header("Match Settings")]
        [Tooltip("Points required to win a set (must win by 2).")]
        public int pointsToWin = 25;
        [Tooltip("If false, hitting the ball twice in a row before it crosses the net is a fault.")]
        public bool allowDoubleHits = true;

        [Header("AI Difficulty Settings")]
        public DifficultyMode difficultyMode = DifficultyMode.Medium;
        [Tooltip("Configure the physical stats of the Dog for Easy, Medium, and Hard modes.")]
        public AIDifficultySettings[] difficultyPresets = new AIDifficultySettings[] {
            new AIDifficultySettings { moveSpeed = 3.0f, reactionDelay = 0.2f, forceFactor = 1.0f, hitAccuracy = 0.8f }, // Easy
            new AIDifficultySettings { moveSpeed = 4.5f, reactionDelay = 0.1f, forceFactor = 1.25f, hitAccuracy = 1.0f }, // Medium
            new AIDifficultySettings { moveSpeed = 6.5f, reactionDelay = 0.0f, forceFactor = 1.6f, hitAccuracy = 1.0f }  // Hard
        };

        public AIDifficultySettings CurrentDifficulty => difficultyPresets[(int)difficultyMode];

        [Header("CSV Level Loading (Unified)")]
        /// <summary>
        /// A unified dictionary of loaded opponent profiles.
        /// </summary>
        private Dictionary<int, OpponentProfile> profiles;
        
        [Tooltip("The current wave index (row) in the CSV playlist.")]
        [SerializeField] private int currentWaveIndex = 0;
        public int CurrentWaveIndex => currentWaveIndex;

        /// <summary>
        /// The most recently computed clamped CSV cartesian offset (relative to headset).
        /// The landing visualizer reads this to recompute its position live against the current headset.
        /// </summary>
        public Vector3 LastCSVTargetOffset { get; private set; } = Vector3.zero;
        public bool IsUsingCSVTargeting => opponentAI != null && opponentAI.CurrentProfile != null && opponentAI.CurrentProfile.HasTarget;

        [Header("Rally Escalation (Dynamic Difficulty)")]
        [Tooltip("How much the ball's apex height is reduced (in meters) for every hit after the 2nd hit in a rally. A lower apex forces a faster, flatter shot!")]
        public float heightReductionPerHit = 0.15f;
        [Tooltip("The maximum allowed height reduction, ensuring the ball doesn't get ridiculously flat.")]
        public float maxHeightReduction = 1.5f;
        [Tooltip("How much the AI's return flight time is reduced (as a multiplier) for every hit, making the AI return faster laser beams!")]
        public float aiSpeedEscalationPerHit = 0.0416f; // Reaches exactly 75% reduction at 20 hits
        [Tooltip("The absolute minimum flight time for the AI's shot (seconds).")]
        public float aiMinFlightTime = 0.5f;

        [Header("Fast Low Shot Settings")]
        [Tooltip("Probability (0-1) that any AI shot will be a fast, low, net-skimming shot. 0 = never, 1 = always. Recommended: 0.2-0.3")]
        [Range(0f, 1f)]
        public float fastShotChance = 0.25f;
        [Tooltip("Flight time for a fast low shot (seconds). Lower = faster. Normal shots are ~2.2s.")]
        [Range(0.7f, 1.6f)]
        public float fastShotFlightTime = 1.1f;
        
        public int ConsecutiveFastShots { get; set; } = 0;

        [Header("Player Smash Settings")]
        [Tooltip("Minimum physical hand speed required for the player to trigger a smash.")]
        public float playerSmashSpeedThreshold = 8.0f;
        [Tooltip("The flight time of a player's smash (1.2 to 1.4 is recommended).")]
        [Range(0.7f, 2.0f)]
        public float playerSmashFlightTime = 1.3f;
        [Tooltip("How many regular hits the player must make before they are allowed to smash again. Prevents spamming.")]
        public int playerSmashCooldownHits = 5;
        
        public int HitsSinceLastSmash { get; set; } = 3; // Starts ready to smash

        [Header("Serve Settings")]
        [Tooltip("Which side should the ball spawn on for the player to serve?")]
        public VolleyballRehabProfileSO.HandMode serveSide = VolleyballRehabProfileSO.HandMode.Right;
        
        [Tooltip("Offset for the ball when serving with the Right hand (X is right, Y is up, Z is forward).")]
        [SerializeField] private Vector3 rightServeOffset = new Vector3(0.3f, -0.1f, 0.4f);
        
        [Tooltip("Offset for the ball when serving with the Left hand.")]
        [SerializeField] private Vector3 leftServeOffset = new Vector3(-0.3f, -0.1f, 0.4f);

        [Header("Patient Configuration")]
        [SerializeField] private VolleyballRehabProfileSO rehabProfile;

        // ─── Events ──────────────────────────────────────────
        public event System.Action OnScoreUpdated;
        public event System.Action<string> OnMatchOver;

        [Header("UI Canvases")]
        [Tooltip("The main scoreboard during gameplay.")]
        [SerializeField] private GameObject scoreBoardContent;
        [Tooltip("The end match canvas with Menu/Next buttons.")]
        [SerializeField] private GameObject endCanvas;
        [Tooltip("Optional: A specific Transform to teleport the player to when the game ends. If unassigned, the game calculates a spot 6m in front of the End Canvas.")]
        [SerializeField] private Transform endGameTeleportPoint;

        // ─── Internal State ──────────────────────────────────
        
        // Stat Tracking
        public int PlayerScore { get; private set; } = 0;
        public int AIScore { get; private set; } = 0;
        public int BestRallyCount { get; private set; } = 0;
        public int BestWinStreak { get; private set; } = 0;
        
        public int CurrentRallyCount { get; private set; } = 0;
        public int CurrentWinStreak { get; private set; } = 0;
        
        private int consecutiveDrops = 0;
        private bool wasBallOnPlayerSide = false;
        private bool isPlayerServe = false;
        private bool isMatchOver = false;
        private bool isEditorTestMode = false;
        private float lastStrikeTime;
        private int playerConsecutiveHits = 0;
        private Coroutine activeServeCoroutine; // Tracked so we can cancel it to prevent double-serve
        
        // Data for static serve placement and recenter detection
        private Vector3 lastKnownHeadPos;
        private Vector3 staticServeHeadPos;
        private Vector3 staticServeHeadForward;
        private Vector3 staticServeHeadRight;
        
        // Tracks the last side the serve was placed on so it can alternate if "Both" is selected
        private bool lastServeWasRight = false;
        
        // Player Court Position
        private Vector3 playerCourtPosition;
        private Quaternion playerCourtRotation;

        // Public accessor — other scripts check this to know if the rally is live
        public bool IsRallyActive => state == GameState.RallyActive;

        // ─── Timing Constants ────────────────────────────────
        private const float DELAY_BEFORE_SERVE = 3.0f;
        private const float AI_SERVE_WINDUP = 1.5f;
        private const float FAILSAFE_TIMEOUT = 12.0f;
        private const float PLAYER_SERVE_TIMEOUT = 10.0f; // If player doesn't hit in 10s, give serve to dog
        private const float FIRST_SERVE_DELAY = 2.0f;     // Delay before the very first serve of the match
        private float playerServeStartTime;               // When the current player serve began

        // ═══════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance == null) { Instance = this; }
            else { Destroy(gameObject); return; }
            
            if (playerTransform != null)
            {
                playerCourtPosition = playerTransform.position;
                playerCourtRotation = playerTransform.rotation;
            }
        }

        private void Start()
        {
            // Try to load the CSV using the universal Async loader that works in Editor and Android!
            StartCoroutine(CSVLoader.LoadProfilesAsync((loadedProfiles) =>
            {
                profiles = loadedProfiles;
                if (profiles != null && profiles.Count > 0)
                {
                    ApplyWave(0); // Start at row 0
                }
                else
                {
                    Debug.LogWarning("[GameManager] Failed to load CSV profiles. Fallback behavior will be used.");
                }
            }));

            // Apply Audience settings from the Menu Manager static properties
            if (!Rehab.Volleyball.UI.VolleyballMenuManager.IsAudienceEnabled)
            {
                var audienceMembers = FindObjectsByType<Rehab.Volleyball.Mechanics.VolleyballAudienceMember>(FindObjectsSortMode.None);
                foreach (var member in audienceMembers)
                {
                    member.gameObject.SetActive(false);
                }
                Debug.Log("[GameManager] Audience animals disabled based on Menu settings.");
            }

            OnScoreUpdated?.Invoke();
            
            // Auto-start if we are testing directly in the Game scene without the Menu open!
            bool hasMenuOpen = false;
            var menu = FindObjectOfType<Rehab.Volleyball.UI.VolleyballMenuManager>();
            if (menu != null && menu.menuPanel != null && menu.menuPanel.activeSelf)
            {
                hasMenuOpen = true;
            }

            if (!hasMenuOpen)
            {
                Debug.Log("[GameManager] No active Menu detected. Auto-starting game for Editor testing!");
                isEditorTestMode = true;
                pointsToWin = 5; // Direct editor test is 5 points
                
                // If bypassing the menu, sync the HandMode directly from the SO!
                if (rehabProfile != null)
                {
                    serveSide = rehabProfile.handMode;
                }
                
                StartGame();
            }
        }

        public void ApplyWave(int waveIndex)
        {
            if (profiles == null) return;
            
            if (profiles.ContainsKey(waveIndex))
            {
                currentWaveIndex = waveIndex;
                if (opponentAI != null)
                {
                    opponentAI.SetProfile(profiles[waveIndex]);
                }
                Debug.Log($"[GameManager] Applied CSV Wave {waveIndex}");
            }
            else
            {
                Debug.LogWarning($"[GameManager] Attempted to apply Wave {waveIndex} but it doesn't exist in the CSV!");
            }
        }

        public void StartGame()
        {
            if (isMatchOver) return;
            Debug.Log("[GameManager] Starting Game from Menu!");
            
            if (!isEditorTestMode)
            {
                // Override settings from Menu if the game was launched from it
                if (Rehab.Volleyball.UI.VolleyballMenuManager.HasStartedFromMenu)
                {
                    serveSide = Rehab.Volleyball.UI.VolleyballMenuManager.HandMode;
                    pointsToWin = Rehab.Volleyball.UI.VolleyballMenuManager.PointsToWin;
                }
            }
            
            // First serve always goes to the Dog!
            isPlayerServe = false;
            
            // Make sure the timer is reset so the 10-second fault doesn't trigger instantly if state was weird
            playerServeStartTime = Time.time + 999f; 

            // Start the first serve immediately (handles the 2-second delay internally now!)
            TransitionToServe(isFirstServe: true);
        }

        private Transform cachedHeadTransform;

        private Transform GetHeadTransform()
        {
            if (cachedHeadTransform != null) return cachedHeadTransform;

            if (headTransform != null) { cachedHeadTransform = headTransform; return cachedHeadTransform; }
            if (Camera.main != null) { cachedHeadTransform = Camera.main.transform; return cachedHeadTransform; }

            // Explicitly search for the Oculus VR headset camera if tags are missing
            if (playerTransform != null)
            {
                Transform[] children = playerTransform.GetComponentsInChildren<Transform>();
                foreach (Transform t in children)
                {
                    if (t.name == "CenterEyeAnchor") { cachedHeadTransform = t; return cachedHeadTransform; }
                }
                cachedHeadTransform = playerTransform; // Fallback
                return cachedHeadTransform;
            }

            // Ultimate failsafe so we never throw a NullReferenceException
            cachedHeadTransform = this.transform; 
            return cachedHeadTransform;
        }

        private void Update()
        {
            if (activeBall == null || isMatchOver) return;

            Transform currentHead = GetHeadTransform();

            if (state == GameState.PlayerServing)
            {
                // Detect VR Recentering (or huge playspace movements > 0.5m in one frame)
                if (Vector3.Distance(currentHead.position, lastKnownHeadPos) > 0.5f)
                {
                    Debug.Log("[GameManager] VR Recenter detected! Respawning serve ball.");
                    SpawnPlayerServe();
                }
                
                // Serve timeout: if player hasn't served in 10s, it's a fault! Dog gets the point AND the serve!
                if (Time.time - playerServeStartTime > PLAYER_SERVE_TIMEOUT)
                {
                    Debug.Log("[GameManager] Serve timeout FAULT! Awarding point and serve to Dog.");
                    activeBall.StopBall();
                    
                    // Award point to AI (this automatically sets isPlayerServe = false and starts the transition)
                    AwardPointToAI();
                    
                    // Check for match winner immediately after awarding point
                    OnScoreUpdated?.Invoke();
                    CheckForMatchWinner();
                    if (!isMatchOver)
                    {
                        opponentAI.ResetPosition();
                        TransitionToServe();
                    }
                    return;
                }
            }
            lastKnownHeadPos = currentHead.position;

            // Failsafe: ball stuck for too long during rally
            if (state == GameState.RallyActive && activeBall.IsBallActive &&
                Time.time - lastStrikeTime > FAILSAFE_TIMEOUT)
            {
                Debug.LogWarning("[GameManager] Failsafe: Ball stuck. Forcing reset.");
                HandleBallDropped(activeBall);
                return;
            }

            // Only track net crossings during active rally
            if (state != GameState.RallyActive) return;

            float netZ = netTransform != null ? netTransform.position.z : 5.0f;
            bool isBallOnPlayerSide = activeBall.transform.position.z < netZ;

            // Dynamically calculate the bottom gap of the physical net
            float netBottom = 1.0f;
            if (netTransform != null)
            {
                Collider netCol = netTransform.GetComponentInChildren<Collider>();
                if (netCol != null) netBottom = netCol.bounds.min.y;
            }

            if (wasBallOnPlayerSide && !isBallOnPlayerSide)
            {
                if (activeBall.transform.position.y < netBottom)
                    HandleUnderNetFault(crossedToAI: true);
                else
                    HandleBallCrossedToAI();
            }
            else if (!wasBallOnPlayerSide && isBallOnPlayerSide)
            {
                if (activeBall.transform.position.y < netBottom)
                    HandleUnderNetFault(crossedToAI: false);
                else
                    HandleBallCrossedToPlayer();
            }

            wasBallOnPlayerSide = isBallOnPlayerSide;
        }

        // ═══════════════════════════════════════════════════════
        // STATE TRANSITIONS — The core fix for all race conditions
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// The ONLY way a new serve should ever be started.
        /// Cancels any pending serve coroutine first to prevent double-serves.
        /// </summary>
        private void TransitionToServe(bool isFirstServe = false)
        {
            // Kill any existing pending serve coroutine
            if (activeServeCoroutine != null)
            {
                StopCoroutine(activeServeCoroutine);
                activeServeCoroutine = null;
            }

            activeServeCoroutine = StartCoroutine(ServeSequence(isFirstServe));
        }

        private bool hasFiredServeAnimationEvent = false;

        public void TriggerServeReleaseEvent()
        {
            hasFiredServeAnimationEvent = true;
        }

        private IEnumerator ServeSequence(bool isFirstServe)
        {
            state = GameState.WaitingToServe;

            // 1. WAIT FOR SCORE ABSORPTION OR GAME START (Ball stays on the ground!)
            float waitTime = isFirstServe ? FIRST_SERVE_DELAY : DELAY_BEFORE_SERVE;
            yield return new WaitForSeconds(waitTime);

            while (VolleyballLevelDirector.Instance != null && !VolleyballLevelDirector.Instance.CanSpawnServe())
            {
                yield return null;
            }

            if (isMatchOver) yield break;

            // 2. WHISTLE BLOWS! SERVE IS NOW LIVE!
            if (VolleyballEffectsManager.Instance != null)
                VolleyballEffectsManager.Instance.PlayWhistle();

            // 3. SNAP BALL TO SERVE POSITION! 
            // Turn off the ball briefly to break the TrailRenderer history when teleporting
            if (activeBall != null) 
            {
                activeBall.gameObject.SetActive(false);
            }
            
            // CRITICAL FIX: Wait exactly one frame while the ball is disabled. 
            // This forces Unity's graphics pipeline to flush the TrailRenderer!
            yield return null;
            
            if (isPlayerServe)
            {
                SpawnPlayerServe();
            }
            else
            {
                if (opponentAI != null) opponentAI.PrepareServe(activeBall);
            }
            
            // Turn the ball back on now that it is in the correct position
            if (activeBall != null) 
            {
                activeBall.gameObject.SetActive(true);
                
                // Extra failsafe: manually call Clear on any trails if they exist
                TrailRenderer[] trails = activeBall.GetComponentsInChildren<TrailRenderer>(true);
                foreach (var t in trails) t.Clear();
            }

            if (isPlayerServe)
            {
                state = GameState.PlayerServing;
                playerServeStartTime = Time.time; // Start the 10-second timeout clock
                // State stays PlayerServing until HandlePlayerStrike transitions it to RallyActive
            }
            else
            {
                state = GameState.AIServing;
                
                // Immersive pause before AI serves (windup)
                yield return new WaitForSeconds(AI_SERVE_WINDUP);
                if (isMatchOver) yield break;
                
                // Tell dog to start the serve animation (kick/throw)
                if (opponentAI != null) opponentAI.PlayServeAnimation();
                
                // Wait exactly until the animation reaches the release frame!
                float releaseDelay = (opponentAI != null) ? opponentAI.GetServeReleaseDelay() : 0.4f;
                float timer = 0f;
                hasFiredServeAnimationEvent = false;

                while (timer < releaseDelay && !hasFiredServeAnimationEvent)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }
                
                if (isMatchOver) yield break;
                
                ServeBallToPlayer();
                state = GameState.RallyActive;
            }

            activeServeCoroutine = null; // Coroutine finished cleanly
        }

        // Scoring and Net Crossing methods extracted to VolleyballGameManager.Scoring.cs
        // ═══════════════════════════════════════════════════════

        public void SpawnPlayerServe()
        {
            activeBall.StopBall();
            lastStrikeTime = Time.time;

            Transform currentHead = GetHeadTransform();
            
            staticServeHeadPos = currentHead.position;
            
            Vector3 lookDir = currentHead.forward;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude < 0.001f) lookDir = Vector3.forward;
            staticServeHeadForward = lookDir.normalized;

            Vector3 rightDir = currentHead.right;
            rightDir.y = 0;
            staticServeHeadRight = rightDir.normalized;

            ApplyStaticServePosition();

            // CRITICAL: Reset LastHitter so stale data from the previous rally
            // doesn't cause wrong scoring if the serve ball falls without being hit
            activeBall.ResetHitter();

            Debug.Log("[GameManager] Ball spawned for Player Serve!");
            float netZ = netTransform != null ? netTransform.position.z : 5.0f;
            wasBallOnPlayerSide = activeBall.transform.position.z < netZ;
        }

        private void ApplyStaticServePosition()
        {
            // Choose the offset based on the selected serve side
            Vector3 offset = rightServeOffset;
            
            if (serveSide == VolleyballRehabProfileSO.HandMode.Left)
            {
                offset = leftServeOffset;
            }
            else if (serveSide == VolleyballRehabProfileSO.HandMode.Both)
            {
                // Alternate serve side!
                offset = lastServeWasRight ? leftServeOffset : rightServeOffset;
                lastServeWasRight = !lastServeWasRight; // Flip it for the next time
            }
            else
            {
                offset = rightServeOffset;
            }

            // Apply offset relative to the SAVED head data (keeps ball strictly static in space!)
            Vector3 spawnPos = staticServeHeadPos 
                             + (staticServeHeadRight * offset.x) 
                             + (Vector3.up * offset.y) 
                             + (staticServeHeadForward * offset.z);

            activeBall.transform.position = spawnPos;
        }

        public void ServeBallToPlayer()
        {
            lastStrikeTime = Time.time;
            
            // Release the ball from the dog's hand and restore physics
            activeBall.transform.parent = null;
            if (activeBall.Rb != null) activeBall.Rb.isKinematic = false;
            
            Vector3 targetPos = GetPlayerTargetPosition();

            // All serves are normal, looping shots. Fast shots only happen during rallies.
            float chosenFlightTime = 2.2f;
            float? chosenGravity = null;

            ConsecutiveFastShots = 0; // Reset counter on serve

            activeBall.LaunchToTarget(targetPos, chosenFlightTime, BallHitter.AI, null, 1.0f, 0f, chosenGravity);

            float netZ = netTransform != null ? netTransform.position.z : 5.0f;
            wasBallOnPlayerSide = activeBall.transform.position.z < netZ;
        }



        // Physics math (WillClearNet) extracted to VolleyballGameManager.Physics.cs

        // ═══════════════════════════════════════════════════════
        // STRIKE HANDLERS
        // ═══════════════════════════════════════════════════════

        // Match Management and Strike Handlers extracted to VolleyballGameManager.Scoring.cs

        // ═══════════════════════════════════════════════════════
        // UTILITY
        // ═══════════════════════════════════════════════════════

        // Targeting methods extracted to VolleyballGameManager.Targeting.cs

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {

                // Allow real-time tuning of serve offset in Inspector without sticking the ball to the camera
                if (state == GameState.PlayerServing && activeBall != null)
                {
                    ApplyStaticServePosition();
                }
            }
        }
#endif
    }
}
