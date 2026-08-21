using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ArcRoll.Grid;
using ArcRoll.Gameplay;
using ArcRoll.Gameplay.Frisbee;
using UnityEngine.Networking;
using System.IO;

namespace ArcRoll.Core
{
    /// <summary>
    /// Reads the level CSV and feeds shot requests to BallQueueManager one at a time.
    /// It no longer uses WaitForSeconds between shots — the queue manager decides
    /// when the scene is ready for the next ball (i.e., after the player grabs the current one).
    /// </summary>
    public class ArcRollLevelDirector : MonoBehaviour
    {
        [Header("Level Data")]
        [Tooltip("Name of the CSV file inside the StreamingAssets folder")]
        [SerializeField] private string levelFileName = "level1.csv";

        [Header("Dependencies")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Transform playerHead;
        [SerializeField] private BallQueueManager ballQueueManager;

        [Header("Cannons")]
        [SerializeField] private CannonController leftCannon;
        [SerializeField] private CannonController rightCannon;
        [Range(0.1f, 1.0f)]
        [SerializeField] private float reachSafetyRatio = 0.85f;

        [Header("ROM Profile")]
        [Tooltip("Drag the runtime ArcRollRehabProfileSO reference here.")]
        [SerializeField] private ArcRollRehabProfileSO activeProfile;

        [Header("ROM Ring Visuals")]
        [SerializeField] private GameObject romRingPrefab;
        [SerializeField] private float ringHeightOffset = 0f;
        [SerializeField] private Vector3 ringRotation = Vector3.zero;

        [Header("Prefabs - Balls")]
        [SerializeField] private GameObject basketballPrefab;
        [SerializeField] private GameObject bowlingBallPrefab;

        [Header("Prefabs - Frisbee")]
        [SerializeField] private Frisbee frisbeePrefab;
        [SerializeField] private GameObject canPyramidPrefab;
        [SerializeField] private GameObject balloonPrefab;
        [SerializeField] private Vector3 frisbeeSpawnOffset = new Vector3(0.3f, -0.2f, 0.4f);

        [Header("Prefabs - Targets")]
        [SerializeField] private GameObject basketballHoopPrefab;
        [SerializeField] private GameObject bowlingPinPrefab;
        [SerializeField] private GameObject curveCoveringPrefab;

        [Header("Prefabs - Obstacles")]
        [SerializeField] private GameObject slidingObstaclePrefab;
        [SerializeField] private GameObject rumbleStripObstaclePrefab;
        [SerializeField] private GameObject speedBumpObstaclePrefab;
        [SerializeField] private GameObject swingingObstaclePrefab;
        [SerializeField] private GameObject timedGateObstaclePrefab;

        // (Target Cleanup settings are now managed directly on the Prefabs!)

        // ── Internal ──────────────────────────────────────────────────────────
        private struct LevelEvent
        {
            public float timeDelay; // Kept for initial startup ramp-up only
            public int row;
            public int col;
            public string targetType;
            public string obstacle;
        }

        private List<LevelEvent> levelEvents = new List<LevelEvent>();
        private int currentEventIndex = 0;
        private bool levelStarted = false;
        private bool levelFinished = false;
        private GameObject activeTarget;
        public bool IsTargetDestroyed => activeTarget == null;

        private List<float> calculatedRomAngles = new List<float>();
        private Vector3 calibratedForward;

        // ── Unity ─────────────────────────────────────────────────────────────
        private void Start()
        {
            CalculateDiscreteRomAngles();
            StartCoroutine(LoadLevelAndPlay());
        }

        private void CalculateDiscreteRomAngles()
        {
            if (activeProfile == null) return;

            // 1. Read raw profile data with a fallback
            float rawAbduction = activeProfile.maxAbduction > 5f ? activeProfile.maxAbduction : 45f;
            float rawAdduction = activeProfile.maxAdduction > 5f ? activeProfile.maxAdduction : 45f;

            // 2. HARD CLAMPS for VR Playability
            // Abduction > 90 means the ring is behind their ear (invisible).
            // Adduction > 60 means they are breaking their shoulder trying to reach across their chest.
            float cappedAbduction = Mathf.Min(rawAbduction, 90f);
            float cappedAdduction = Mathf.Min(rawAdduction, 60f);

            // 3. Apply a tiny 5% safety compression for angles (0.95) so they don't stretch to 100% of their painful limit
            // Note: The reachSafetyRatio (e.g. 0.85) is strictly reserved for the arm length/distance reach!
            float angleSafetyMargin = 0.95f;
            float finalAbduction = cappedAbduction * angleSafetyMargin;
            float finalAdduction = cappedAdduction * angleSafetyMargin;

            float minAngle, maxAngle;
            if (activeProfile.isLeftArm)
            {
                // Left Arm: Reaches left for Abduction (Negative), Right for Adduction (Positive)
                minAngle = -finalAbduction;
                maxAngle = finalAdduction;
            }
            else
            {
                // Right Arm: Reaches left for Adduction (Negative), Right for Abduction (Positive)
                minAngle = -finalAdduction;
                maxAngle = finalAbduction;
            }

            float totalSweep = maxAngle - minAngle;
            
            // We want 5 rings if the sweep is huge, 4 if medium, 3 if small.
            // BUT we must guarantee that the CENTER ring is always exactly 0!
            calculatedRomAngles.Clear();

            if (totalSweep >= 120f)
            {
                // 5 Angles: min, half-min, 0, half-max, max
                calculatedRomAngles.Add(minAngle);
                calculatedRomAngles.Add(minAngle / 2f);
                calculatedRomAngles.Add(0f);
                calculatedRomAngles.Add(maxAngle / 2f);
                calculatedRomAngles.Add(maxAngle);
            }
            else if (totalSweep >= 90f)
            {
                // 4 Angles is tricky to center 0. Let's force 5 angles anyway if it's over 90, 
                // just so we always have a true center!
                calculatedRomAngles.Add(minAngle);
                calculatedRomAngles.Add(minAngle / 2f);
                calculatedRomAngles.Add(0f);
                calculatedRomAngles.Add(maxAngle / 2f);
                calculatedRomAngles.Add(maxAngle);
            }
            else
            {
                // 3 Angles: min, 0, max
                calculatedRomAngles.Add(minAngle);
                calculatedRomAngles.Add(0f);
                calculatedRomAngles.Add(maxAngle);
            }
            
            Debug.Log($"[ArcRollLevelDirector] Calculated {calculatedRomAngles.Count} discrete ROM angles: {string.Join(", ", calculatedRomAngles)}");
        }

        // ── Level Loading ─────────────────────────────────────────────────────
        private IEnumerator LoadLevelAndPlay()
        {
            // Tell the GameManager that we are officially playing so it allows scoring
            if (ArcRollGameManager.Instance != null)
            {
                ArcRollGameManager.Instance.StartGame();
            }

            // Lock the forward direction exactly once when the game starts, 
            // so if they turn their head later, the rings don't spawn behind them!
            calibratedForward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up).normalized;
            if (calibratedForward.sqrMagnitude < 0.001f) calibratedForward = Vector3.forward;

            // Instantly wake up the cannons and aim them at the player so they stand upright!
            if (playerHead != null)
            {
                if (leftCannon != null) leftCannon.AimAtTarget(playerHead.position);
                if (rightCannon != null) rightCannon.AimAtTarget(playerHead.position);
            }

            yield return StartCoroutine(ParseCSVFromStreamingAssets(levelFileName));

            if (levelEvents.Count > 0)
            {
                // Short intro pause before the very first ball
                yield return new WaitForSeconds(3.0f);
                levelStarted = true;
                RequestNextShot(); // Kick off the chain
            }
        }

        /// <summary>
        /// Called by BallQueueManager (via the queued Action) when it's ready for
        /// the next ball — i.e., after the player grabs the previous one.
        /// Also called once at level start.
        /// </summary>
        public void RequestNextShot()
        {
            // If the timer ended, ArcRollGameManager.isGameActive will be false, so stop spawning!
            if (!levelStarted || levelFinished || (ArcRollGameManager.Instance != null && !ArcRollGameManager.Instance.isGameActive)) return;
            
            if (currentEventIndex >= levelEvents.Count)
            {
                if (levelEvents.Count > 0)
                {
                    Debug.Log("[ArcRollLevelDirector] CSV ended, looping back to the start.");
                    currentEventIndex = 0;
                }
                else
                {
                    levelFinished = true;
                    return;
                }
            }

            int indexSnapshot = currentEventIndex;
            currentEventIndex++;

            // Hand a lambda to the queue manager — it will call it when ready
            ballQueueManager?.RequestShot(() => SpawnTargetAndShoot(levelEvents[indexSnapshot]));
        }

        // ── CSV Parsing ───────────────────────────────────────────────────────
        private IEnumerator ParseCSVFromStreamingAssets(string fileName)
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "ArcRoll", fileName);
            string csvText  = "";

            if (filePath.Contains("://") || filePath.Contains(":///"))
            {
                using (UnityWebRequest www = UnityWebRequest.Get(filePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                        csvText = www.downloadHandler.text;
                    else
                    {
                        Debug.LogError($"[ArcRollLevelDirector] Failed to load CSV: {www.error}");
                        yield break;
                    }
                }
            }
            else
            {
                if (File.Exists(filePath))
                    csvText = File.ReadAllText(filePath);
                else
                {
                    Debug.LogError($"[ArcRollLevelDirector] CSV not found at: {filePath}");
                    yield break;
                }
            }

            string[] lines = csvText.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] cols = line.Split(',');
                if (cols.Length >= 4)
                {
                    int.TryParse(cols[0], out int row);
                    int.TryParse(cols[1], out int col);
                    string targetType = cols[2].Trim().ToLower();
                    string obstacle = cols[3].Trim();

                    levelEvents.Add(new LevelEvent 
                    { 
                        timeDelay = 0f, // No longer used, handled by QueueManager
                        row = row, 
                        col = col, 
                        targetType = targetType,
                        obstacle = obstacle 
                    });
                }
                else if (cols.Length >= 3)
                {
                    // Backwards compatibility if obstacle is missing
                    int.TryParse(cols[0], out int row);
                    int.TryParse(cols[1], out int col);
                    string targetType = cols[2].Trim().ToLower();
                    
                    levelEvents.Add(new LevelEvent 
                    { 
                        timeDelay = 0f,
                        row = row, 
                        col = col, 
                        targetType = targetType,
                        obstacle = "none" 
                    });
                }
            }

            Debug.Log($"[ArcRollLevelDirector] Parsed {levelEvents.Count} events from {fileName}");
        }

        // ── Shot Execution ────────────────────────────────────────────────────
        private void SpawnTargetAndShoot(LevelEvent evt)
        {
            if (playerHead == null || activeProfile == null || gridManager == null)
            {
                Debug.LogError("[ArcRollLevelDirector] Missing PlayerHead, ActiveProfile, or GridManager.");
                return;
            }

            // ====================================================================
            // 1. CALCULATE THE FINAL GOAL (Where the Target spawns far away)
            // ====================================================================
            Vector3 finalGoalPos = gridManager.GetWorldPosition(evt.row, evt.col);

            GameObject floorObj = GameObject.FindGameObjectWithTag("Floor");
            float floorY = floorObj != null ? floorObj.transform.position.y : 0f;

            GameObject requiredBallPrefab = null;
            GameObject spawnedTarget      = null;

            // We want the front of the target to face the player.
            Vector3 flatPlayerPos = playerHead.position;
            flatPlayerPos.y = finalGoalPos.y;
            Vector3 dirToPlayer = (flatPlayerPos - finalGoalPos).normalized;
            Vector3 dirAwayFromPlayer = -dirToPlayer;
            Quaternion facePlayerRot = dirAwayFromPlayer != Vector3.zero ? Quaternion.LookRotation(dirAwayFromPlayer) : Quaternion.identity;

            bool isFrisbeeGame = (evt.targetType == "balloon" || evt.targetType == "canpyramid");

            // Pick the ROM angle based on the CSV column (0 to 4 mapped to available angles)
            int numAngles = calculatedRomAngles.Count;
            int angleIndex = 0;
            if (numAngles > 1)
            {
                float percent = Mathf.Clamp01(evt.col / 4f);
                angleIndex = Mathf.RoundToInt(percent * (numAngles - 1));
            }
            float selectedAngle = numAngles > 0 ? calculatedRomAngles[angleIndex] : 0f;

            // Calculate the exact origin point from the patient's active shoulder
            Vector3 flatForward = calibratedForward;
            float dropAmount = playerHead.position.y < 1.3f ? 0.2f : 0.4f;
            float shoulderX = (activeProfile != null && activeProfile.isLeftArm) ? -0.15f : 0.15f;
            Vector3 shoulderOffset = new Vector3(shoulderX, -dropAmount, 0f);
            Vector3 shoulderOrigin = playerHead.position + playerHead.TransformDirection(shoulderOffset);

            // Calculate target catch position using angle and arm length
            Vector3 targetDir = Quaternion.AngleAxis(selectedAngle, Vector3.up) * flatForward;
            float maxReach = (activeProfile != null && activeProfile.armLength > 0.1f) ? activeProfile.armLength : 0.6f;
            
            // Use reachSafetyRatio for distance compression (0.85 default)
            float finalReach = maxReach * reachSafetyRatio;
            
            Vector3 catchPos = shoulderOrigin + targetDir * finalReach;
            if (catchPos.y < floorY + 0.3f) catchPos.y = floorY + 0.3f; // Safety clamp above floor

            if (evt.targetType == "pins")
            {
                finalGoalPos.y = floorY; // Pins go on the floor
                if (bowlingPinPrefab != null)
                    spawnedTarget = Spawn10PinFormation(bowlingPinPrefab, finalGoalPos, facePlayerRot);
                requiredBallPrefab = bowlingBallPrefab;
            }
            else if (evt.targetType == "hoop")
            {
                if (finalGoalPos.y < floorY + 0.3f) finalGoalPos.y = floorY + 0.3f; // Keep hoops off the floor
                if (basketballHoopPrefab != null)
                {
                    Quaternion finalHoopRot = facePlayerRot * basketballHoopPrefab.transform.rotation;
                    spawnedTarget = Instantiate(basketballHoopPrefab, finalGoalPos, finalHoopRot);

                    BasketballHoop hoop = spawnedTarget.GetComponent<BasketballHoop>();
                    if (hoop != null)
                    {
                        if (evt.row >= 2) hoop.SetScoreValue(5);
                        else hoop.SetScoreValue(3);
                    }
                }
                requiredBallPrefab = basketballPrefab;
            }
            else if (evt.targetType == "canpyramid")
            {
                finalGoalPos.y = floorY; // Pyramid goes on the floor
                if (canPyramidPrefab != null)
                {
                    Quaternion finalPyramidRot = facePlayerRot * canPyramidPrefab.transform.rotation;
                    spawnedTarget = Instantiate(canPyramidPrefab, finalGoalPos, finalPyramidRot);
                }
            }
            else if (evt.targetType == "balloon")
            {
                if (finalGoalPos.y < floorY + 0.3f) finalGoalPos.y = floorY + 0.3f; // Keep balloons off the floor
                if (balloonPrefab != null)
                {
                    Quaternion finalBalloonRot = facePlayerRot * balloonPrefab.transform.rotation;
                    spawnedTarget = Instantiate(balloonPrefab, finalGoalPos, finalBalloonRot);

                    // Compensate for any model visual/collider pivot offsets (e.g. Balloon 3D model Z-offset)
                    Collider col = spawnedTarget.GetComponentInChildren<Collider>();
                    if (col != null)
                    {
                        Vector3 localCenter = spawnedTarget.transform.InverseTransformPoint(col.bounds.center);
                        spawnedTarget.transform.position = finalGoalPos - finalBalloonRot * localCenter;
                    }
                }
            }

            // ====================================================================
            // OBSTACLE SPAWNING LOGIC (Based on CSV)
            // ====================================================================
            string obsType = evt.obstacle.ToLower().Trim();
            if (!string.IsNullOrEmpty(obsType) && obsType != "none")
            {
                if (evt.row == 0)
                {
                    // ==========================================
                    // BOWLING/CAN PYRAMID OBSTACLES (Floor Blockers)
                    // ==========================================
                    GameObject obstaclePrefabToSpawn = null;
                    if (obsType == "sliding") obstaclePrefabToSpawn = slidingObstaclePrefab;
                    else if (obsType == "rumble") obstaclePrefabToSpawn = rumbleStripObstaclePrefab;
                    else if (obsType == "speedbump") obstaclePrefabToSpawn = speedBumpObstaclePrefab;
                    else if (obsType == "swinging") obstaclePrefabToSpawn = swingingObstaclePrefab;
                    else if (obsType == "gate") obstaclePrefabToSpawn = timedGateObstaclePrefab;

                    if (obstaclePrefabToSpawn != null)
                    {
                        // 1. Calculate Position: 30% from the Target back towards the Player
                        Vector3 obstaclePos = Vector3.Lerp(finalGoalPos, flatPlayerPos, 0.3f);
                        
                        // 2. Spawn and Rotate to face player perfectly
                        Quaternion finalObsRot = facePlayerRot * obstaclePrefabToSpawn.transform.rotation;
                        GameObject spawnedObstacle = Instantiate(obstaclePrefabToSpawn, obstaclePos, finalObsRot);

                        // 3. Parent it to the target
                        if (spawnedTarget != null)
                        {
                            spawnedObstacle.transform.SetParent(spawnedTarget.transform, true);
                        }
                    }
                }
                else
                {
                    // ==========================================
                    // BASKETBALL/BALLOON OBSTACLES (Moving Targets!)
                    // ==========================================
                    if (spawnedTarget != null)
                    {
                        ArcRoll.Gameplay.Obstacles.ArcRollDynamicMover mover = null;
                        
                        if (obsType == "sideways")
                        {
                            mover = spawnedTarget.AddComponent<ArcRoll.Gameplay.Obstacles.ArcRollDynamicMover>();
                            mover.Setup(ArcRoll.Gameplay.Obstacles.ArcRollDynamicMover.MovementType.Sideways);
                        }
                        else if (obsType == "updown")
                        {
                            mover = spawnedTarget.AddComponent<ArcRoll.Gameplay.Obstacles.ArcRollDynamicMover>();
                            mover.Setup(ArcRoll.Gameplay.Obstacles.ArcRollDynamicMover.MovementType.UpDown);
                        }
                        else if (obsType == "wavy")
                        {
                            mover = spawnedTarget.AddComponent<ArcRoll.Gameplay.Obstacles.ArcRollDynamicMover>();
                            mover.Setup(ArcRoll.Gameplay.Obstacles.ArcRollDynamicMover.MovementType.Wavy);
                        }
                    }
                }
            }

            if (isFrisbeeGame)
            {
                if (frisbeePrefab != null && spawnedTarget != null)
                {
                    FrisbeeFormation formation = spawnedTarget.GetComponent<FrisbeeFormation>();
                    if (formation == null)
                    {
                        formation = spawnedTarget.AddComponent<FrisbeeFormation>();
                    }

                    // The spot where the frisbee will hover for the player to grab:
                    // Calculated exactly using the patient's ROM catch position, but without showing the visual ring!
                    Vector3 hoverAnchorPos = catchPos;
                    
                    // Spawn it high up (sky) and off to the left or right so it sails down beautifully!
                    bool spawnRight = UnityEngine.Random.value > 0.5f;
                    Vector3 sideDir = spawnRight ? playerHead.right : -playerHead.right;
                    Vector3 spawnPos = hoverAnchorPos + sideDir * 5.0f + playerHead.forward * 3.0f + Vector3.up * 4.0f;
                    Frisbee frisbee = Instantiate(frisbeePrefab, spawnPos, frisbeePrefab.transform.rotation);
                    
                    if (formation != null)
                    {
                        formation.RegisterFrisbee(frisbee);
                    }

                    // Register with the BallQueueManager so it waits for this Frisbee
                    ballQueueManager?.RegisterFrisbee(frisbee);

                    // Setup the frisbee so it knows where to aim (aim assist)
                    Vector3 aimPos = finalGoalPos;
                    if (evt.targetType == "canpyramid") aimPos.y += 0.5f; // Aim slightly above the ground for Can Pyramids
                    frisbee.ShootToTarget(hoverAnchorPos, aimPos, spawnedTarget.transform);
                    
                    // Tell the frisbee to trigger RequestNextShot when it dies!
                    frisbee.OnStateChanged += (f, state) =>
                    {
                        if (state == Frisbee.FrisbeeState.Dead || state == Frisbee.FrisbeeState.Missed)
                        {
                            RequestNextShot();
                        }
                    };
                }
            }
            else
            {
                // ====================================================================
                // 2. SPAWN THE CATCH RING FOR BASKETBALL/BOWLING
                // ====================================================================

                // Spawn the ROM Ring exactly at the catch position!
                GameObject spawnedRing = null;
                if (romRingPrefab != null)
                {
                    Vector3 ringPos = catchPos;
                    ringPos.y += ringHeightOffset;
                    spawnedRing = Instantiate(romRingPrefab, ringPos, Quaternion.Euler(ringRotation));
                }

                // ====================================================================
                // 3. DYNAMIC CANNON ROUTING
                // ====================================================================
                CannonController activeCannon;
                bool shootFromRight;

                if (selectedAngle < -5f)
                {
                    activeCannon = leftCannon;
                    shootFromRight = false;
                }
                else if (selectedAngle > 5f)
                {
                    activeCannon = rightCannon;
                    shootFromRight = true;
                }
                else
                {
                    shootFromRight = Random.value > 0.5f;
                    activeCannon = shootFromRight ? rightCannon : leftCannon;
                }

                // ====================================================================
                // 4. FIRE THE BALL
                // ====================================================================
                if (activeCannon != null)
                {
                    // Grab the EXACT physical target point from the spawned obstacle!
                    Vector3 truePhysicsTarget = finalGoalPos;
                    if (spawnedTarget != null && spawnedTarget.TryGetComponent<BasketballHoop>(out var hoop))
                    {
                        truePhysicsTarget = hoop.TargetPoint;
                    }

                    // Shoot ball TO the catchPos, but aim assist exactly at the true physics target!
                    Transform liveTargetTransform = spawnedTarget != null ? spawnedTarget.transform : null;
                    Ball firedBall = activeCannon.ShootAtTarget(catchPos, truePhysicsTarget, requiredBallPrefab, spawnedRing, liveTargetTransform);

                    if (firedBall != null && spawnedTarget != null)
                    {
                        if (spawnedTarget.TryGetComponent<BasketballHoop>(out var registerHoop))
                        {
                            registerHoop.RegisterBall(firedBall);
                        }
                        else
                        {
                            // Register with the Formation manager — it handles all scoring and cleanup
                            ArcRoll.Gameplay.BowlingPinFormation formation = spawnedTarget.GetComponent<ArcRoll.Gameplay.BowlingPinFormation>();
                            formation?.RegisterBall(firedBall);
                        }
                    }

                    RequestNextShot();
                }
                else
                {
                    Debug.LogWarning("[ArcRollLevelDirector] Active Cannon is null. Cannot shoot.");
                }
            }
            activeTarget = spawnedTarget;
        }

        private GameObject Spawn10PinFormation(GameObject prefab, Vector3 spawnPos, Quaternion rotation)
        {
            // Create a parent so we can cleanly despawn all 10 pins at once later
            GameObject formationParent = new GameObject("BowlingPinFormation");
            formationParent.transform.position = spawnPos;
            formationParent.transform.rotation = rotation;

            // IMPORTANT: AddComponent must happen HERE (before the coroutine) so that
            // RegisterBall() — called by LevelDirector immediately after this returns — can find it.
            // InitPins() is called again inside the coroutine once pins actually exist.
            formationParent.AddComponent<ArcRoll.Gameplay.BowlingPinFormation>();

            // Spawn the curve covering over the pins FIRST
            GameObject covering = null;
            if (curveCoveringPrefab != null)
            {
                covering = Instantiate(curveCoveringPrefab, formationParent.transform);
                covering.transform.localPosition = new Vector3(-0.28f, 0.96f, 0.585f);
                covering.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            StartCoroutine(SpawnPinsWithDelay(prefab, formationParent, rotation, covering, 0.5f));

            return formationParent;
        }

        private IEnumerator SpawnPinsWithDelay(GameObject prefab, GameObject formationParent, Quaternion rotation, GameObject covering, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (formationParent == null) yield break; // In case the level ended early

            // Spacing for VR standard 10-pin bowling (increased to 45cm for wider setup)
            float dx = 0.45f; 
            float dz = 0.45f * 0.866f; // Height of an equilateral triangle
            
            Vector3[] pinOffsets = new Vector3[10]
            {
                new Vector3(0, 0, 0),                                                               // Row 1 (1)
                new Vector3(-dx/2f, 0, dz), new Vector3(dx/2f, 0, dz),                              // Row 2 (2)
                new Vector3(-dx, 0, dz*2f), new Vector3(0, 0, dz*2f), new Vector3(dx, 0, dz*2f),    // Row 3 (3)
                new Vector3(-dx*1.5f, 0, dz*3f), new Vector3(-dx*0.5f, 0, dz*3f),                   // Row 4 (4)
                new Vector3(dx*0.5f, 0, dz*3f), new Vector3(dx*1.5f, 0, dz*3f)
            };

            List<Collider> spawnedColliders = new List<Collider>();

            foreach (Vector3 localOffset in pinOffsets)
            {
                Vector3 worldPos = formationParent.transform.TransformPoint(localOffset);
                Quaternion finalPinRot = rotation * prefab.transform.rotation;
                
                GameObject pin = Instantiate(prefab, worldPos, finalPinRot);
                pin.transform.SetParent(formationParent.transform);
                
                spawnedColliders.AddRange(pin.GetComponentsInChildren<Collider>());
            }

            // ── Now that pins are parented, tell the formation manager about them ──
            // Awake() ran at AddComponent time (0.5s ago) with 0 pins. We call InitPins() again now.
            var formationManager = formationParent.GetComponent<ArcRoll.Gameplay.BowlingPinFormation>();
            if (formationManager != null) formationManager.InitPins();

            // Temporarily ignore collisions between the pins and the covering so they don't get knocked over
            Collider[] coveringColliders = covering != null ? covering.GetComponentsInChildren<Collider>() : new Collider[0];
            
            foreach (var pinCol in spawnedColliders)
            {
                foreach (var covCol in coveringColliders)
                {
                    if (pinCol != null && covCol != null)
                        Physics.IgnoreCollision(pinCol, covCol, true);
                }
            }

            // Wait another 1 second, then re-enable collisions (just in case they need to interact later)
            yield return new WaitForSeconds(1.0f);

            foreach (var pinCol in spawnedColliders)
            {
                foreach (var covCol in coveringColliders)
                {
                    if (pinCol != null && covCol != null)
                        Physics.IgnoreCollision(pinCol, covCol, false);
                }
            }
        }
    }
}
