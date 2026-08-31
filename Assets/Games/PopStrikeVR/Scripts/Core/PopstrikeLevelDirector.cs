using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PopstrikeVR.Data;
using PopstrikeVR.Gameplay;

namespace PopstrikeVR.Core
{
    /// <summary>
    /// The main Game Loop manager. Reads the parsed JSON level and orchestrates the spawning of balloons over time.
    /// </summary>
    public class PopstrikeLevelDirector : MonoBehaviour
    {
        public static PopstrikeLevelDirector Instance { get; private set; }
        public BalloonTaskType CurrentTaskType { get; private set; } = BalloonTaskType.Orange_Punch;

        [Header("Accessibility")]
        [Tooltip("Changes where balloons spawn to ensure they are reachable by the active hand.")]
        public HandTrackingMode activeHandMode = HandTrackingMode.BothHands;

        [Header("Configuration")]
        public PatientProfileSO patientProfile;
        public SessionConfigSO sessionConfig;
        
        [Tooltip("The subdirectory path under StreamingAssets (e.g. PopStrikeVR/Easy)")]
        public string levelSubFolder = "PopStrikeVR/Easy";
        
        [Tooltip("Name of the JSON file located in the subdirectory")]
        public string jsonFileName = "level1.json";

        [Header("Timing")]
        [Tooltip("How much time the player has to figure out the puzzle before it times out.")]
        public float timeBetweenTasks = 10.0f; 
        
        [Tooltip("How long to wait after a wave is completely finished before spawning the next wave.")]
        public float delayBetweenWaves = 1.0f;
        
        [Header("Progression")]
        [Tooltip("If true, the tasks inside the CSV will be shuffled so the player cannot memorize the sequence.")]
        public bool randomizeTaskOrder = false;
        
        [Tooltip("How many mistakes the player can make on Trace/Trail balloons before the task fails.")]
        public int maxAttemptsAllowed = 3;

        [Header("UI Announcements & Canvas Sequences")]
        [Tooltip("The root GameObject of the HUD Canvas (Score, Timer)")]
        public GameObject hudCanvasRoot;
        [Tooltip("The root GameObject of the Level Indicator Panel (LEVEL - X)")]
        public GameObject levelIndicatorRoot;
        [Tooltip("Text element used to flash 'Level-X' on the screen before the game starts.")]
        public TMPro.TextMeshProUGUI levelAnnouncementText;

        [Header("Session Settings")]
        [Tooltip("Select whether this is a 3-minute or 5-minute session. This determines which minimum wave thresholds apply.")]
        public SessionDuration sessionDuration = SessionDuration.ThreeMinutes;

        private PopstrikeLevelJSON currentLevelConfig;
        private int currentTaskIndex = 0;
        
        private int totalWavesSpawned = 0;
        private int totalWavesMissed = 0;
        private int totalErrors = 0; // Counts individual in-wave errors (wrong TMT node, failed trace, broken slash)

        // --- Timer Variables ---
        public int TotalErrors => totalErrors;
        
        public int TheoreticalMaxScore { get; private set; } = 0;
        private int theoreticalMaxCombo = 0;

        private float gameStartTime = 0f;
        private float sessionDurationSeconds = 180f;
        private bool isGameActive = false;
        private float timeRemaining = 0f;

        private void Awake()
        {
            Instance = this;
            
            // Immediately hide canvases so they don't flash default Inspector values before GameLoop starts
            if (hudCanvasRoot != null) hudCanvasRoot.SetActive(false);
            if (levelIndicatorRoot != null) levelIndicatorRoot.SetActive(false);
            if (levelAnnouncementText != null) levelAnnouncementText.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isGameActive)
            {
                timeRemaining = sessionDurationSeconds - (Time.time - gameStartTime);
                if (timeRemaining <= 0f)
                {
                    timeRemaining = 0f;
                    EndSession();
                }

                if (PopstrikeVR.UI.PopstrikeHUDController.Instance != null)
                {
                    PopstrikeVR.UI.PopstrikeHUDController.Instance.UpdateTimerUI(timeRemaining);
                }
            }
        }

        private void EndSession()
        {
            if (!isGameActive) return; // Prevent double-firing
            
            Debug.Log("[LevelDirector] Timer hit 00:00! Ending Session...");
            isGameActive = false;
            
            StopAllCoroutines(); // Force stop GameLoop and all nested yield waits
            PopstrikePooler.DespawnAllBalloons(); // Clear any remaining balloons immediately
            
            // Explicitly hide the Gameplay HUD so it doesn't overlap the Results Canvas
            if (hudCanvasRoot != null) hudCanvasRoot.SetActive(false);
            if (levelIndicatorRoot != null) levelIndicatorRoot.SetActive(false);
            if (levelAnnouncementText != null) levelAnnouncementText.gameObject.SetActive(false);

            if (TryGetComponent<PopstrikeResultsCalculator>(out var calc))
            {
                calc.CalculateAndShowResults(levelSubFolder, sessionDuration, totalWavesSpawned, totalWavesMissed, totalErrors, TheoreticalMaxScore);
            }
            else
            {
                Debug.LogError("[LevelDirector] Missing PopstrikeResultsCalculator on Manager object! Cannot show end results.");
            }
        }

        private IEnumerator Start()
        {
            // Wait one frame to ensure singleton components are initialized
            yield return null;

            // If session was already configured in memory (e.g. Retry reload), start it automatically
            if (TemporarySessionData.IsConfigured)
            {
                if (PopstrikeVR.UI.PopstrikeMenuManager.Instance != null && PopstrikeVR.UI.PopstrikeMenuManager.Instance.menuPanel != null)
                {
                    PopstrikeVR.UI.PopstrikeMenuManager.Instance.menuPanel.SetActive(false);
                }
                StartSession(TemporarySessionData.HandMode, TemporarySessionData.Difficulty, TemporarySessionData.Duration);
                yield break;
            }

            // If Menu is active in the scene, wait for the player to click Play
            if (PopstrikeVR.UI.PopstrikeMenuManager.Instance != null && PopstrikeVR.UI.PopstrikeMenuManager.Instance.gameObject.activeInHierarchy)
            {
                Debug.Log("[LevelDirector] Main Menu detected. Waiting for player to click Play...");
                yield break;
            }

            // Fallback (for developer scene testing without a menu): Start directly with inspector defaults
            StartSession(activeHandMode, levelSubFolder.Replace("PopStrikeVR/", ""), sessionDuration);
        }

        /// <summary>
        /// Public entry point called by PopstrikeMenuManager when the player clicks Play.
        /// </summary>
        public void StartSession(HandTrackingMode handMode, string difficulty, SessionDuration duration)
        {
            activeHandMode = handMode;
            sessionDuration = duration;
            levelSubFolder = "PopStrikeVR/" + difficulty;
            
            // --- DIFFICULTY TIMING LOGIC ---
            if (difficulty.Equals("Easy", System.StringComparison.OrdinalIgnoreCase))
            {
                timeBetweenTasks = 10f;
                delayBetweenWaves = 2f;
            }
            else if (difficulty.Equals("Medium", System.StringComparison.OrdinalIgnoreCase))
            {
                timeBetweenTasks = 8f;
                delayBetweenWaves = 1.5f;
            }
            else if (difficulty.Equals("Hard", System.StringComparison.OrdinalIgnoreCase))
            {
                timeBetweenTasks = 6f;
                delayBetweenWaves = 1f;
            }

            // --- DYNAMIC PROGRESSION & RANDOMIZATION LOGIC ---
            if (!TemporarySessionData.IsConfigured)
            {
                // DEVELOPER FALLBACK: Played directly from the scene without the Menu
                jsonFileName = "try_level.json";
                levelSubFolder = "PopStrikeVR"; // try_level.json is NOT inside the Easy/Medium folders!
                randomizeTaskOrder = false;
                TemporarySessionData.JsonFileName = jsonFileName;
                TemporarySessionData.LevelSubFolder = levelSubFolder; // Save so Next Level works
                Debug.Log($"[LevelDirector] DEVELOPER MODE: Bypassed Menu, loading {jsonFileName} from {levelSubFolder}");
            }
            else if (TemporarySessionData.IsRetry)
            {
                jsonFileName = TemporarySessionData.JsonFileName;
                levelSubFolder = TemporarySessionData.LevelSubFolder; // Restore exact path
                randomizeTaskOrder = (TemporarySessionData.CurrentLevelIndex > 1);
                Debug.Log($"[LevelDirector] RETRY TRIGGERED. Reloading exactly {jsonFileName} for Level {TemporarySessionData.CurrentLevelIndex}");
                TemporarySessionData.IsRetry = false; // Reset the flag after consuming it
            }
            else if (TemporarySessionData.CurrentLevelIndex == 1)
            {
                // Level 1 is always exactly level1.json and never shuffled.
                jsonFileName = "level1.json";
                randomizeTaskOrder = false; 
                TemporarySessionData.JsonFileName = jsonFileName; // Cache it
                TemporarySessionData.LevelSubFolder = levelSubFolder; // Save the subfolder too
                Debug.Log($"[LevelDirector] Selected {jsonFileName} for Level 1 (Strict Authored Order)");
            }
            else
            {
                // Level 2+ reuses the exact same JSON configuration indefinitely.
                // Because the coordinates are procedurally generated, the same JSON produces infinite unique levels!
                jsonFileName = TemporarySessionData.JsonFileName;
                levelSubFolder = TemporarySessionData.LevelSubFolder; // CRITICAL: Restore the exact folder so path is correct
                
                // Safety fallback if it was somehow lost
                if (string.IsNullOrEmpty(jsonFileName)) jsonFileName = "try_level.json";
                if (string.IsNullOrEmpty(levelSubFolder)) levelSubFolder = "PopStrikeVR";

                randomizeTaskOrder = true; // Always randomize tasks for subsequent levels
                Debug.Log($"[LevelDirector] Reusing {levelSubFolder}/{jsonFileName} for Level {TemporarySessionData.CurrentLevelIndex} (Procedurally Shuffled for infinite levels)");
            }

            StartCoroutine(InitSessionSequence());
        }

        private void TriggerHUDPositioner(GameObject canvasRoot)
        {
            if (canvasRoot != null)
            {
                var positioner = canvasRoot.GetComponentInChildren<PopstrikeVR.UI.HUDStaticPositioner>();
                if (positioner != null)
                {
                    positioner.PositionUI();
                }
            }
        }

        private void SetTMTTrailMode(bool active)
        {
            var trails = FindObjectsOfType<PopstrikeVR.Gameplay.GestureTrailManager>();
            foreach (var t in trails) t.SetTMTMode(active);
        }

        private IEnumerator InitSessionSequence()
        {
            // Skybox alignment is now handled by the standalone SkyboxAligner component.
            // First, trigger a Fade FROM black (in case we just transitioned scenes)
            if (PopstrikeVR.Visuals.ScreenEffectsController.Instance != null)
            {
                PopstrikeVR.Visuals.ScreenEffectsController.Instance.FadeFromBlack(1.0f);
            }
            
            // Wait 1 second to allow VR Headset Tracking to initialize (crucial when playing directly in Editor)
            yield return new WaitForSeconds(1.0f);
            
            // 1. Hide HUD
            if (hudCanvasRoot != null) hudCanvasRoot.SetActive(false);

            // 2. Show Level Indicator text: "LEVEL - X"
            if (levelAnnouncementText != null)
            {
                levelAnnouncementText.text = $"LEVEL - {TemporarySessionData.CurrentLevelIndex}";
                levelAnnouncementText.gameObject.SetActive(true);
                levelAnnouncementText.alpha = 1f;
            }
            if (levelIndicatorRoot != null) 
            {
                levelIndicatorRoot.SetActive(true);
                TriggerHUDPositioner(levelIndicatorRoot);
            }
            
            // 3. Wait for player to read it
            yield return new WaitForSeconds(2.5f);
            
            // 4. Hide Level Indicator and Show HUD
            if (levelIndicatorRoot != null) levelIndicatorRoot.SetActive(false);
            if (hudCanvasRoot != null) 
            {
                hudCanvasRoot.SetActive(true);
                TriggerHUDPositioner(hudCanvasRoot);
            }

            if (patientProfile == null)
            {
                Debug.LogError("[LevelDirector] Patient Profile is missing! Cannot start session.");
                yield break;
            }
            // Pass the profile to the GestureDetector
            if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
            {
                PopstrikeVR.Interaction.GestureDetector.Instance.SetPatientProfile(patientProfile);
            }

            string path = System.IO.Path.Combine(Application.streamingAssetsPath, levelSubFolder, jsonFileName);
            string jsonContent = "";

#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, StreamingAssets are locked inside the APK, so we MUST use UnityWebRequest
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(path))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    jsonContent = www.downloadHandler.text;
                }
                else
                {
                    Debug.LogError($"[LevelDirector] Failed to load JSON from StreamingAssets: {www.error}");
                }
            }
#else
            // In the Editor, standard System.IO works fine
            if (System.IO.File.Exists(path))
            {
                jsonContent = System.IO.File.ReadAllText(path);
            }
            else
            {
                Debug.LogError($"[LevelDirector] File not found: {path}");
            }
            yield return null; // Yield one frame just to be safe
#endif

            currentLevelConfig = JSONLevelParser.ParseLevelJSON(jsonContent);
            
            if (currentLevelConfig != null && currentLevelConfig.spawnChances != null)
            {
                Debug.Log($"[LevelDirector] Successfully loaded procedural config. Starting Game Loop...");
                
                // Calculate total duration from JSON rounds/breaks to pre-populate the HUD timer
                if (currentLevelConfig.rounds != null && currentLevelConfig.rounds.Count > 0)
                {
                    float totalMinutes = 0f;
                    foreach (var r in currentLevelConfig.rounds) totalMinutes += r.durationInMinutes;
                    if (currentLevelConfig.breaks != null)
                    {
                        foreach (var b in currentLevelConfig.breaks) totalMinutes += b.durationInMinutes;
                    }
                    sessionDurationSeconds = totalMinutes * 60f;
                }
                else
                {
                    // Fallback to menu configuration
                    sessionDurationSeconds = (TemporarySessionData.Duration == SessionDuration.ThreeMinutes) ? 180f : 300f;
                }
                
                if (PopstrikeVR.UI.PopstrikeHUDController.Instance != null)
                {
                    PopstrikeVR.UI.PopstrikeHUDController.Instance.UpdateTimerUI(sessionDurationSeconds);
                }

                StartCoroutine(GameLoop());
            }
            else
            {
                Debug.LogWarning("[LevelDirector] Invalid or missing JSON procedural configuration.");
            }
        }

        private float lastErrorTime = -999f;
        private int errorsInCurrentWave = 0; // Tracks physical errors made in a single wave
        
        public bool IsCooldownActive
        {
            get 
            {
                float errorCooldown = 2.0f;
                if (TemporarySessionData.Difficulty != null)
                {
                    if (TemporarySessionData.Difficulty.Equals("Easy", System.StringComparison.OrdinalIgnoreCase)) errorCooldown = 2.5f;
                    else if (TemporarySessionData.Difficulty.Equals("Hard", System.StringComparison.OrdinalIgnoreCase)) errorCooldown = 1.5f;
                }
                return (Time.time - lastErrorTime < errorCooldown);
            }
        }

        /// <summary>
        /// Called by any balloon manager (TMT, Trace, Slash) when an in-wave error occurs.
        /// Returns true if the error was accepted (and combo broken).
        /// Returns false if the error was ignored due to the invincibility cooldown.
        /// </summary>
        public bool TryReportError()
        {
            // Dynamic Error Cooldown (Invincibility Frames) based on Difficulty
            float errorCooldown = 2.0f; // Default Medium
            if (TemporarySessionData.Difficulty != null)
            {
                if (TemporarySessionData.Difficulty.Equals("Easy", System.StringComparison.OrdinalIgnoreCase)) errorCooldown = 2.5f;
                else if (TemporarySessionData.Difficulty.Equals("Hard", System.StringComparison.OrdinalIgnoreCase)) errorCooldown = 1.5f;
            }

            if (Time.time - lastErrorTime < errorCooldown)
            {
                Debug.Log($"[LevelDirector] Secondary error ignored (Cooldown active). Needs {errorCooldown}s");
                return false;
            }
            lastErrorTime = Time.time;

            totalErrors++;
            errorsInCurrentWave++;
            
            // Check if they have exceeded the allowed errors for this wave based on difficulty
            int errorsToBreakCombo = 2; // Default Medium (breaks on 2nd error)
            if (TemporarySessionData.Difficulty != null)
            {
                if (TemporarySessionData.Difficulty.Equals("Easy", System.StringComparison.OrdinalIgnoreCase)) errorsToBreakCombo = 3;
                else if (TemporarySessionData.Difficulty.Equals("Hard", System.StringComparison.OrdinalIgnoreCase)) errorsToBreakCombo = 1;
            }

            if (errorsInCurrentWave >= errorsToBreakCombo)
            {
                // Break the streak — they exceeded the allowed errors for this difficulty
                PopstrikeVR.Gameplay.ComboManager.Instance?.BreakCombo();
                Debug.Log($"[LevelDirector] Error reported. Total errors: {totalErrors} | Wave Errors: {errorsInCurrentWave} | Combo Reset.");
            }
            else
            {
                Debug.Log($"[LevelDirector] Error reported. Total errors: {totalErrors} | Wave Errors: {errorsInCurrentWave} | Combo Forgiven!");
            }
            return true;
        }

        private IEnumerator GameLoop()
        {
            totalWavesSpawned = 0;
            totalWavesMissed = 0;
            totalErrors = 0;
            TheoreticalMaxScore = 0;
            theoreticalMaxCombo = 0;
            
            // sessionDurationSeconds is now calculated in InitSessionSequence

            gameStartTime = Time.time;
            isGameActive = true;

            // Give the player a short delay before the first balloon
            
            // --- ACCESSIBILITY / UX: Align Physical Environment ---
            // The balloons spawn relative to the CenterEyeAnchor, which depends on where the player is physically looking.
            // If they manually built a table/plants in the editor, we need to snap that environment's rotation 
            // to match the player's initial view, otherwise the table will be beside or behind them!
            // 

            yield return new WaitForSeconds(2.0f);

            if (currentLevelConfig != null && currentLevelConfig.rounds != null && currentLevelConfig.rounds.Count > 0)
            {
                for (int roundIndex = 0; roundIndex < currentLevelConfig.rounds.Count; roundIndex++)
                {
                    if (!isGameActive) break;

                    float roundDurationSeconds = currentLevelConfig.rounds[roundIndex].durationInMinutes * 60f;
                    float roundStartTime = Time.time; // Local timer for this specific round

                    while (isGameActive && Time.time - roundStartTime < roundDurationSeconds)
                    {
                        if (currentLevelConfig.spawnChances == null) break;

                        errorsInCurrentWave = 0; // Reset error tracker for the new wave
                        
                        // --- PROCEDURAL GENERATION ---
                        float safeRadius = patientProfile != null ? patientProfile.GetSafeRadius() : 0.6f;
                        TaskRow currentTask = ProceduralTaskGenerator.GenerateNextTask(currentLevelConfig.spawnChances, safeRadius);
                        
                        CurrentTaskType = currentTask.TaskType;
                        
                        totalWavesSpawned++;
                        
                        // --- ACCESSIBILITY: Auto-Lock Gesture for Easy/Medium ---
                        Coroutine autoLockRoutine = null;
                        if (PopstrikeVR.Core.TemporarySessionData.Difficulty != "Hard")
                        {
                            autoLockRoutine = StartCoroutine(AutoLockGestureRoutine(GetRequiredGesture(CurrentTaskType)));
                        }
                        
                        List<GameObject> spawnedBalloons = PopstrikeWaveSpawner.SpawnTask(currentTask, patientProfile);
                        
                        // --- DYNAMIC MAX SCORE SIMULATION ---
                        int simulatedHits = (CurrentTaskType == BalloonTaskType.Orange_Punch) ? spawnedBalloons.Count : 1;
                        int basePoints = ProceduralTaskGenerator.GetBaseScoreForTask(CurrentTaskType);
                        
                        for (int i = 0; i < simulatedHits; i++)
                        {
                            theoreticalMaxCombo++;
                            // Hardcode the Ghost Player to exactly 1.5x multiplier for the entire game
                            float simulatedMultiplier = 1.5f;
                            TheoreticalMaxScore += Mathf.RoundToInt(basePoints * simulatedMultiplier);
                        }
                        
                        // Trigger the spawn animation so they scale up from zero
                        foreach(var balloon in spawnedBalloons)
                        {
                            if (balloon != null && balloon.TryGetComponent<PopstrikeVR.Gameplay.BaseBalloon>(out var baseBalloon))
                            {
                                baseBalloon.AnimateSpawn(0.5f);
                            }
                        }
                        
                        // DYNAMIC YIELDING with timeout limit (timeBetweenTasks) for all types!
                        float elapsed = 0f;
                        if (CurrentTaskType == BalloonTaskType.TMTA || CurrentTaskType == BalloonTaskType.TMTB)
                        {
                            SetTMTTrailMode(true);
                            if (TMTSolverScript.Instance != null)
                            {
                                while (TMTSolverScript.Instance.IsSequenceActive && elapsed < timeBetweenTasks)
                                {
                                    if (!TMTSolverScript.Instance.HasSequenceStarted())
                                    {
                                        elapsed += Time.deltaTime;
                                    }
                                    yield return null;
                                }
                            }
                            else
                            {
                                yield return new WaitForSeconds(timeBetweenTasks);
                            }
                            SetTMTTrailMode(false);
                        }
                        else if (CurrentTaskType == BalloonTaskType.Green_Trace)
                        {
                            if (TracePathManager.Instance != null)
                            {
                                bool wasTracking = false;
                                while (TracePathManager.Instance.IsSequenceActive && elapsed < timeBetweenTasks)
                                {
                                    if (!TracePathManager.Instance.IsTracking)
                                    {
                                        if (wasTracking)
                                        {
                                            elapsed = Mathf.Min(elapsed, timeBetweenTasks - 1.0f);
                                            wasTracking = false;
                                        }
                                        elapsed += Time.deltaTime;
                                    }
                                    else
                                    {
                                        wasTracking = true;
                                    }
                                    yield return null;
                                }
                            }
                            else
                            {
                                yield return new WaitForSeconds(timeBetweenTasks);
                            }
                        }
                        else if (CurrentTaskType == BalloonTaskType.Blue_Slash)
                        {
                            if (PopstrikeVR.Gameplay.BladeSlashManager.Instance != null)
                            {
                                while (PopstrikeVR.Gameplay.BladeSlashManager.Instance.IsSequenceActive() && elapsed < timeBetweenTasks)
                                {
                                    if (!PopstrikeVR.Gameplay.BladeSlashManager.Instance.IsTracking)
                                    {
                                        elapsed += Time.deltaTime;
                                    }
                                    yield return null;
                                }
                            }
                            else
                            {
                                yield return new WaitForSeconds(timeBetweenTasks);
                            }
                        }
                        else
                        {
                            while (elapsed < timeBetweenTasks)
                            {
                                bool allPopped = true;
                                foreach(var balloon in spawnedBalloons)
                                {
                                    if (balloon != null && balloon.activeInHierarchy)
                                    {
                                        if (balloon.TryGetComponent<PopstrikeVR.Gameplay.BaseBalloon>(out var baseBalloon))
                                        {
                                            if (!baseBalloon.IsPopped) 
                                            {
                                                allPopped = false;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            allPopped = false;
                                            break;
                                        }
                                    }
                                }
                                
                                if (allPopped)
                                {
                                    break; 
                                }
                                
                                elapsed += Time.deltaTime;
                                yield return null;
                            }
                        }
                        
                        // --- ACCESSIBILITY: Unlock Gesture at end of wave ---
                        if (autoLockRoutine != null) StopCoroutine(autoLockRoutine);
                        if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
                        {
                            PopstrikeVR.Interaction.GestureDetector.Instance.UnlockGesture(true);
                            PopstrikeVR.Interaction.GestureDetector.Instance.UnlockGesture(false);
                        }

                        bool missedAny = false;
                        foreach(var balloon in spawnedBalloons)
                        {
                            if (balloon != null && balloon.activeInHierarchy)
                            {
                                if (balloon.TryGetComponent<PopstrikeVR.Gameplay.BaseBalloon>(out var baseBalloon))
                                {
                                    if (baseBalloon.IsPopped) continue; 
                                    if (baseBalloon is PopstrikeVR.Gameplay.BladeBalloon blade && blade.IsSliced) continue; 
                                }

                                missedAny = true;
                                if (PopstrikeVR.Gameplay.ComboManager.Instance != null) 
                                    PopstrikeVR.Gameplay.ComboManager.Instance.BreakCombo();
                                
                                if (baseBalloon != null)
                                {
                                    baseBalloon.AnimateDespawn(0.5f);
                                }

                                PopstrikePooler.DespawnBalloon(balloon, 0.5f);
                            }
                        }

                        if (missedAny)
                        {
                            totalWavesMissed++;
                            if (PopstrikeFeedbackManager.Instance != null)
                                PopstrikeFeedbackManager.Instance.PlayErrorTone();
                            
                            yield return new WaitForSeconds(0.5f);
                        }
                        else
                        {
                            yield return new WaitForSeconds(delayBetweenWaves);
                        }
                    } // End of Inner Round Loop

                    if (!isGameActive) break;

                    // --- HANDLE BREAK TIME ---
                    if (currentLevelConfig.breaks != null && roundIndex < currentLevelConfig.breaks.Count)
                    {
                        float breakDurationSeconds = currentLevelConfig.breaks[roundIndex].durationInMinutes * 60f;
                        if (breakDurationSeconds > 0f)
                        {
                            float breakStartTime = Time.time;
                            
                            if (levelIndicatorRoot != null)
                            {
                                levelIndicatorRoot.SetActive(true);
                                TriggerHUDPositioner(levelIndicatorRoot);
                            }
                            if (levelAnnouncementText != null)
                            {
                                levelAnnouncementText.gameObject.SetActive(true);
                                levelAnnouncementText.text = "BREAK";
                            }

                            while (isGameActive && Time.time - breakStartTime < breakDurationSeconds)
                            {
                                float remainingSeconds = breakDurationSeconds - (Time.time - breakStartTime);
                                
                                if (levelAnnouncementText != null)
                                {
                                    if (remainingSeconds <= 3.0f)
                                        levelAnnouncementText.text = Mathf.CeilToInt(remainingSeconds).ToString();
                                    else
                                        levelAnnouncementText.text = "BREAK";
                                }

                                // Global Update() handles the HUD Timer now, so we just wait!
                                yield return null;
                            }

                            if (levelIndicatorRoot != null) levelIndicatorRoot.SetActive(false);
                            if (levelAnnouncementText != null) levelAnnouncementText.gameObject.SetActive(false);
                        }
                    }
                } // End of Outer Rounds Loop
            }
            
            // If we naturally exit the loop without the Update timer catching it, end the session safely.
            EndSession();
        }

        private PopstrikeVR.Interaction.GestureState GetRequiredGesture(BalloonTaskType taskType)
        {
            switch (taskType)
            {
                case BalloonTaskType.Orange_Punch: return PopstrikeVR.Interaction.GestureState.CLOSED_FIST;
                case BalloonTaskType.Blue_Slash: return PopstrikeVR.Interaction.GestureState.OPEN_BLADE;
                case BalloonTaskType.Green_Trace: return PopstrikeVR.Interaction.GestureState.INDEX_POINT;
                default: return PopstrikeVR.Interaction.GestureState.UNKNOWN;
            }
        }

        private IEnumerator AutoLockGestureRoutine(PopstrikeVR.Interaction.GestureState requiredGesture)
        {
            if (requiredGesture == PopstrikeVR.Interaction.GestureState.UNKNOWN) yield break;

            while (true)
            {
                if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
                {
                    if (PopstrikeVR.Interaction.GestureDetector.Instance.LeftState == requiredGesture)
                        PopstrikeVR.Interaction.GestureDetector.Instance.LockGesture(true, requiredGesture);
                    
                    if (PopstrikeVR.Interaction.GestureDetector.Instance.RightState == requiredGesture)
                        PopstrikeVR.Interaction.GestureDetector.Instance.LockGesture(false, requiredGesture);
                }
                yield return null;
            }
        }
    }
}
