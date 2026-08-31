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
        [Tooltip("Name of the JSON file inside the StreamingAssets folder")]
        [SerializeField] private string levelFileName = "levelConfig.json";

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
        [System.Serializable]
        public class LevelConfig
        {
            public List<RoundConfig> rounds;
            public List<BreakConfig> breaks;
            public SpawnChances spawnChances;
        }

        [System.Serializable]
        public class RoundConfig { public float durationInMinutes; }
        
        [System.Serializable]
        public class BreakConfig { public float durationInMinutes; }
        
        [System.Serializable]
        public class SpawnChances
        {
            public TargetChances targets;
            public FloorObstacleChances floorObstacles;
            public AerialObstacleChances aerialObstacles;
        }

        [System.Serializable]
        public class TargetChances
        {
            public float hoop = 60f;
            public float pins = 20f;
            public float balloon = 10f;
            public float canPyramid = 10f;
        }

        [System.Serializable]
        public class FloorObstacleChances
        {
            public float none = 50f;
            public float sliding = 10f;
            public float rumble = 10f;
            public float speedbump = 10f;
            public float swinging = 10f;
            public float gate = 10f;
        }

        [System.Serializable]
        public class AerialObstacleChances
        {
            public float none = 50f;
            public float sideways = 20f;
            public float updown = 15f;
            public float wavy = 15f;
        }

        private LevelConfig currentLevelConfig;
        private bool readyForNextShot = false;
        private bool levelStarted = false;
        private bool levelFinished = false;
        private bool isInBreak = false;
        private GameObject activeTarget;
        public bool IsTargetDestroyed => activeTarget == null;

        public void ClearActiveTarget()
        {
            if (activeTarget != null)
            {
                Destroy(activeTarget);
                activeTarget = null;
            }
        }

        // ── Initialization ────────────────────────────────────────────────────
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

            float minAngle = activeProfile.isLeftArm ? -finalAbduction : -finalAdduction;
            float maxAngle = activeProfile.isLeftArm ? finalAdduction : finalAbduction;
            float totalSweep = maxAngle - minAngle;
            
            calculatedRomAngles.Clear();
            calculatedRomAngles.Add(minAngle);
            if (totalSweep >= 90f) calculatedRomAngles.Add(minAngle / 2f);
            calculatedRomAngles.Add(0f);
            if (totalSweep >= 90f) calculatedRomAngles.Add(maxAngle / 2f);
            calculatedRomAngles.Add(maxAngle);
            
            Debug.Log($"[ArcRollLevelDirector] Calculated {calculatedRomAngles.Count} discrete ROM angles: {string.Join(", ", calculatedRomAngles)}");
        }

        // ── Level Loading ─────────────────────────────────────────────────────
        private IEnumerator LoadLevelAndPlay()
        {
            calibratedForward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up).normalized;
            if (calibratedForward.sqrMagnitude < 0.001f) calibratedForward = Vector3.forward;

            if (playerHead != null)
            {
                leftCannon?.AimAtTarget(playerHead.position);
                rightCannon?.AimAtTarget(playerHead.position);
            }

            yield return StartCoroutine(LoadJSONConfigFromStreamingAssets(levelFileName));

            if (currentLevelConfig != null && currentLevelConfig.rounds != null && currentLevelConfig.rounds.Count > 0)
            {
                // Calculate Total Session Time for GameManager
                float totalMinutes = 0f;
                foreach (var r in currentLevelConfig.rounds) totalMinutes += r.durationInMinutes;
                if (currentLevelConfig.breaks != null)
                {
                    foreach (var b in currentLevelConfig.breaks) totalMinutes += b.durationInMinutes;
                }
                
                if (ArcRollGameManager.Instance != null)
                {
                    ArcRollGameManager.Instance.gameDuration = totalMinutes * 60f;
                    ArcRollGameManager.Instance.StartGame();
                }

                // Short intro pause before the very first ball
                yield return new WaitForSeconds(3.0f);
                levelStarted = true;
                
                StartCoroutine(ProceduralGameLoop());
            }
            else
            {
                Debug.LogError("[ArcRollLevelDirector] LevelConfig is null or has no rounds!");
            }
        }

        /// <summary>
        /// Called by BallQueueManager (via the queued Action) when it's ready for
        /// the next ball — i.e., after the player grabs the previous one.
        /// Also called once at level start.
        /// </summary>
        public void RequestNextShot()
        {
            // Block new shots during break and countdown — nothing should spawn until 321 is done
            if (!levelStarted || levelFinished || isInBreak) return;
            if (ArcRollGameManager.Instance != null && !ArcRollGameManager.Instance.isGameActive) return;
            
            readyForNextShot = true;
        }

        // ── JSON Parsing & Procedural Loop ────────────────────────────────────
        private IEnumerator LoadJSONConfigFromStreamingAssets(string fileName)
        {
            if (fileName.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[ArcRollLevelDirector] Inspector had legacy filename '{fileName}'. Forcing to 'levelConfig.json'.");
                fileName = "levelConfig.json";
            }

            string filePath = Path.Combine(Application.streamingAssetsPath, "ArcRoll", fileName);
            string jsonText = "";

            if (filePath.Contains("://") || filePath.Contains(":///"))
            {
                using (UnityWebRequest www = UnityWebRequest.Get(filePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                        jsonText = www.downloadHandler.text;
                    else
                    {
                        Debug.LogWarning($"[ArcRollLevelDirector] Failed to load JSON {fileName}: {www.error}. Trying default levelConfig.json");
                        string fallbackPath = Path.Combine(Application.streamingAssetsPath, "ArcRoll", "levelConfig.json");
                        using (UnityWebRequest www2 = UnityWebRequest.Get(fallbackPath))
                        {
                            yield return www2.SendWebRequest();
                            if (www2.result == UnityWebRequest.Result.Success) jsonText = www2.downloadHandler.text;
                        }
                    }
                }
            }
            else
            {
                if (File.Exists(filePath)) jsonText = File.ReadAllText(filePath);
                else
                {
                    Debug.LogWarning($"[ArcRollLevelDirector] JSON not found at: {filePath}. Trying default levelConfig.json");
                    string fallbackPath = Path.Combine(Application.streamingAssetsPath, "ArcRoll", "levelConfig.json");
                    if (File.Exists(fallbackPath)) jsonText = File.ReadAllText(fallbackPath);
                }
            }

            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[ArcRollLevelDirector] No JSON found at all! Using hardcoded fallback.");
                currentLevelConfig = new LevelConfig();
                currentLevelConfig.rounds = new List<RoundConfig> { new RoundConfig { durationInMinutes = 3f } };
                currentLevelConfig.spawnChances = new SpawnChances { targets = new TargetChances { hoop = 100f }, floorObstacles = new FloorObstacleChances(), aerialObstacles = new AerialObstacleChances() };
                yield break;
            }

            try
            {
                currentLevelConfig = JsonUtility.FromJson<LevelConfig>(jsonText);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ArcRollLevelDirector] Error parsing JSON: {e.Message}. Using hardcoded fallback.");
                currentLevelConfig = new LevelConfig();
                currentLevelConfig.rounds = new List<RoundConfig> { new RoundConfig { durationInMinutes = 3f } };
                currentLevelConfig.spawnChances = new SpawnChances { targets = new TargetChances { hoop = 100f }, floorObstacles = new FloorObstacleChances(), aerialObstacles = new AerialObstacleChances() };
            }
        }

        private IEnumerator ProceduralGameLoop()
        {
            readyForNextShot = true; // Kick off the chain

            for (int r = 0; r < currentLevelConfig.rounds.Count; r++)
            {
                float roundDurationSecs = currentLevelConfig.rounds[r].durationInMinutes * 60f;
                float elapsed = 0f;

                while (elapsed < roundDurationSecs && !levelFinished)
                {
                    if (ArcRollGameManager.Instance != null && !ArcRollGameManager.Instance.isGameActive) yield break;

                    if (readyForNextShot)
                    {
                        readyForNextShot = false;
                        
                        string targetType = PickProceduralTarget();
                        string obstacleType = PickProceduralObstacle(targetType);
                        
                        ballQueueManager?.RequestShot(() => SpawnTargetAndShoot(targetType, obstacleType));
                    }
                    
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // Handle Break if not the last round
                if (r < currentLevelConfig.rounds.Count - 1 && currentLevelConfig.breaks != null && r < currentLevelConfig.breaks.Count)
                {
                    // ── BREAK PHASE ──────────────────────────────────────────
                    isInBreak = true;
                    readyForNextShot = false;

                    // Despawn ungrabbed balls/targets to keep the room clean during the break
                    if (ballQueueManager != null)
                    {
                        bool cleared = ballQueueManager.ClearUngrabbedShots();
                        if (cleared || ballQueueManager.ActiveBallCount == 0)
                        {
                            ClearActiveTarget();
                        }
                    }
                    else
                    {
                        ClearActiveTarget();
                    }

                    float breakSecs = currentLevelConfig.breaks[r].durationInMinutes * 60f;
                    float breakElapsed = 0f;

                    if (ArcRoll.UI.ArcRollUIManager.Instance != null)
                        ArcRoll.UI.ArcRollUIManager.Instance.SetBreakText("BREAK");

                    while (breakElapsed < breakSecs && !levelFinished)
                    {
                        if (ArcRollGameManager.Instance != null && !ArcRollGameManager.Instance.isGameActive) yield break;
                        breakElapsed += Time.deltaTime;
                        yield return null;
                    }

                    // ── 3-2-1 COUNTDOWN (after break, before next round) ─────
                    for (int count = 3; count >= 1; count--)
                    {
                        if (ArcRoll.UI.ArcRollUIManager.Instance != null)
                            ArcRoll.UI.ArcRollUIManager.Instance.SetBreakText(count.ToString());
                        yield return new WaitForSeconds(1f);
                    }

                    // ── RESET & RESUME ────────────────────────────────────────
                    ArcRoll.Core.ArcRollScoreManager.Instance?.ResetStreak();
                    isInBreak = false;
                    readyForNextShot = true;

                    if (ArcRoll.UI.ArcRollUIManager.Instance != null)
                        ArcRoll.UI.ArcRollUIManager.Instance.EndBreakText();
                }
            }

            levelFinished = true;
        }

        private string PickProceduralTarget()
        {
            if (currentLevelConfig.spawnChances == null || currentLevelConfig.spawnChances.targets == null) return "hoop";
            var t = currentLevelConfig.spawnChances.targets;
            float roll = UnityEngine.Random.Range(0f, t.hoop + t.pins + t.balloon + t.canPyramid);
            if ((roll -= t.hoop) <= 0) return "hoop";
            if ((roll -= t.pins) <= 0) return "pins";
            if ((roll -= t.balloon) <= 0) return "balloon";
            return "canpyramid";
        }

        private string PickProceduralObstacle(string targetType)
        {
            if (currentLevelConfig.spawnChances == null) return "none";

            // Enforce: Obstacles are ONLY for Bowling Pins and Basketball Hoops.
            // Balloons and Can Pyramids (Frisbee targets) never get obstacles.
            if (targetType == "balloon" || targetType == "canpyramid") return "none";

            bool isFloorTarget = (targetType == "pins");

            if (isFloorTarget)
            {
                if (currentLevelConfig.spawnChances.floorObstacles == null) return "none";
                var o = currentLevelConfig.spawnChances.floorObstacles;
                float roll = UnityEngine.Random.Range(0f, o.none + o.sliding + o.rumble + o.speedbump + o.swinging + o.gate);
                if ((roll -= o.none) <= 0) return "none";
                if ((roll -= o.sliding) <= 0) return "sliding";
                if ((roll -= o.rumble) <= 0) return "rumble";
                if ((roll -= o.speedbump) <= 0) return "speedbump";
                if ((roll -= o.swinging) <= 0) return "swinging";
                return "gate";
            }
            else
            {
                if (currentLevelConfig.spawnChances.aerialObstacles == null) return "none";
                var o = currentLevelConfig.spawnChances.aerialObstacles;
                float roll = UnityEngine.Random.Range(0f, o.none + o.sideways + o.updown + o.wavy);
                if ((roll -= o.none) <= 0) return "none";
                if ((roll -= o.sideways) <= 0) return "sideways";
                if ((roll -= o.updown) <= 0) return "updown";
                return "wavy";
            }
        }

        // ── Shot Execution ────────────────────────────────────────────────────
        private void SpawnTargetAndShoot(string targetType, string obstacle)
        {
            if (playerHead == null || activeProfile == null || gridManager == null)
            {
                Debug.LogError("[ArcRollLevelDirector] Missing PlayerHead, ActiveProfile, or GridManager.");
                return;
            }

            // ====================================================================
            // 1. CALCULATE THE FINAL GOAL (Procedural Position)
            // ====================================================================
            int logicalRow = (targetType == "pins" || targetType == "canpyramid") ? 0 : UnityEngine.Random.Range(1, 3);
            
            int numAngles = calculatedRomAngles.Count;
            int angleIndex = numAngles > 0 ? UnityEngine.Random.Range(0, numAngles) : 0;
            float selectedAngle = numAngles > 0 ? calculatedRomAngles[angleIndex] : 0f;

            int mappedCol = numAngles > 1 ? Mathf.RoundToInt(((float)angleIndex / (numAngles - 1)) * 4f) : 2;
            
            Vector3 finalGoalPos = gridManager.GetWorldPosition(logicalRow, mappedCol);

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

            bool isFrisbeeGame = (targetType == "balloon" || targetType == "canpyramid");

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

            if (targetType == "pins")
            {
                finalGoalPos.y = floorY; // Pins go on the floor
                if (bowlingPinPrefab != null)
                    spawnedTarget = Spawn10PinFormation(bowlingPinPrefab, finalGoalPos, facePlayerRot);
                requiredBallPrefab = bowlingBallPrefab;
            }
            else if (targetType == "hoop")
            {
                if (finalGoalPos.y < floorY + 0.3f) finalGoalPos.y = floorY + 0.3f; // Keep hoops off the floor
                if (basketballHoopPrefab != null)
                {
                    Quaternion finalHoopRot = facePlayerRot * basketballHoopPrefab.transform.rotation;
                    spawnedTarget = Instantiate(basketballHoopPrefab, finalGoalPos, finalHoopRot);

                    BasketballHoop hoop = spawnedTarget.GetComponent<BasketballHoop>();
                    if (hoop != null)
                    {
                        if (logicalRow >= 2) hoop.SetScoreValue(5);
                        else hoop.SetScoreValue(3);
                    }
                }
                requiredBallPrefab = basketballPrefab;
            }
            else if (targetType == "canpyramid")
            {
                finalGoalPos.y = floorY; // Pyramid goes on the floor
                if (canPyramidPrefab != null)
                {
                    Quaternion finalPyramidRot = facePlayerRot * canPyramidPrefab.transform.rotation;
                    spawnedTarget = Instantiate(canPyramidPrefab, finalGoalPos, finalPyramidRot);
                }
            }
            else if (targetType == "balloon")
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
            string obsType = obstacle.ToLower().Trim();
            if (!string.IsNullOrEmpty(obsType) && obsType != "none")
            {
                if (logicalRow == 0)
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
                    if (targetType == "canpyramid") aimPos.y += 0.5f; // Aim slightly above the ground for Can Pyramids
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
