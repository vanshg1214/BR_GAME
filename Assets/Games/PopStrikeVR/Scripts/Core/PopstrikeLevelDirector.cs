using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PopstrikeVR.Data;
using PopstrikeVR.Gameplay;

namespace PopstrikeVR.Core
{
    /// <summary>
    /// The main Game Loop manager. Reads the parsed CSV level and orchestrates the spawning of balloons over time.
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
        
        [Tooltip("Name of the CSV file located in the subdirectory")]
        public string csvFileName = "level1.csv";

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

        [Header("UI Spawn Settings")]
        [Tooltip("How far in front of the player (in meters) to spawn the results screen.")]
        public float resultsUIDistance = 1.2f;
        [Tooltip("Height offset of the results UI relative to the player's head height. Positive is higher, negative is lower.")]
        public float resultsUIHeightOffset = 0f;
        
        [Header("UI Announcements & Canvas Sequences")]
        [Tooltip("The root GameObject of the HUD Canvas (Score, Timer)")]
        public GameObject hudCanvasRoot;
        [Tooltip("The root GameObject of the Level Indicator Panel (LEVEL - X)")]
        public GameObject levelIndicatorRoot;
        [Tooltip("Text element used to flash 'Level-X' on the screen before the game starts.")]
        public TMPro.TextMeshProUGUI levelAnnouncementText;

        [Header("Session & Fail Thresholds")]
        [Tooltip("Select whether this is a 3-minute or 5-minute session. This determines which minimum wave thresholds apply.")]
        public SessionDuration sessionDuration = SessionDuration.ThreeMinutes;

        [Tooltip("Minimum waves the player must clear in a 3-min EASY session to pass.")]
        public int minWaves_Easy_3min   = 18;
        [Tooltip("Minimum waves the player must clear in a 3-min MEDIUM session to pass.")]
        public int minWaves_Medium_3min = 25;
        [Tooltip("Minimum waves the player must clear in a 3-min HARD session to pass.")]
        public int minWaves_Hard_3min   = 35;

        [Tooltip("Minimum waves the player must clear in a 5-min EASY session to pass.")]
        public int minWaves_Easy_5min   = 30;
        [Tooltip("Minimum waves the player must clear in a 5-min MEDIUM session to pass.")]
        public int minWaves_Medium_5min = 42;
        [Tooltip("Minimum waves the player must clear in a 5-min HARD session to pass.")]
        public int minWaves_Hard_5min   = 50;

        private List<TaskRow> parsedTasks;
        private int currentTaskIndex = 0;
        
        private int totalWavesSpawned = 0;
        private int totalWavesMissed = 0;
        private int totalErrors = 0; // Counts individual in-wave errors (wrong TMT node, failed trace, broken slash)

        // --- Timer Variables ---
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
                    isGameActive = false;
                    StopAllCoroutines(); // Force stop GameLoop and all nested yield waits
                    PopstrikePooler.DespawnAllBalloons(); // Clear any remaining balloons immediately
                    CalculateAndShowResults();
                }

                if (PopstrikeVR.UI.PopstrikeHUDController.Instance != null)
                {
                    PopstrikeVR.UI.PopstrikeHUDController.Instance.UpdateTimerUI(timeRemaining);
                }
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
                csvFileName = "try_level.csv";
                levelSubFolder = "PopStrikeVR"; // try_level.csv is NOT inside the Easy/Medium folders!
                randomizeTaskOrder = false;
                TemporarySessionData.CsvFileName = csvFileName;
                Debug.Log($"[LevelDirector] DEVELOPER MODE: Bypassed Menu, loading {csvFileName} from {levelSubFolder}");
            }
            else if (TemporarySessionData.IsRetry)
            {
                csvFileName = TemporarySessionData.CsvFileName;
                randomizeTaskOrder = (TemporarySessionData.CurrentLevelIndex > 1);
                Debug.Log($"[LevelDirector] RETRY TRIGGERED. Reloading exactly {csvFileName} for Level {TemporarySessionData.CurrentLevelIndex}");
                TemporarySessionData.IsRetry = false; // Reset the flag after consuming it
            }
            else if (TemporarySessionData.CurrentLevelIndex == 1)
            {
                // Level 1 is always exactly level1.csv and never shuffled.
                csvFileName = "level1.csv";
                randomizeTaskOrder = false; 
                TemporarySessionData.CsvFileName = csvFileName; // Cache it
                Debug.Log($"[LevelDirector] Selected {csvFileName} for Level 1 (Strict Authored Order)");
            }
            else
            {
                // Level 2+ pulls randomly from the remaining levels and shuffles their waves
                int randomNum = UnityEngine.Random.Range(2, 7); // Random from 2 to 6 inclusive
                csvFileName = $"level{randomNum}.csv";
                randomizeTaskOrder = true;
                TemporarySessionData.CsvFileName = csvFileName; // Cache it
                Debug.Log($"[LevelDirector] Selected {csvFileName} randomly for Level {TemporarySessionData.CurrentLevelIndex} (Randomized Waves)");
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

            string path = System.IO.Path.Combine(Application.streamingAssetsPath, levelSubFolder, csvFileName);
            string csvContent = "";

#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, StreamingAssets are locked inside the APK, so we MUST use UnityWebRequest
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(path))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    csvContent = www.downloadHandler.text;
                }
                else
                {
                    Debug.LogError($"[LevelDirector] Failed to load CSV from StreamingAssets: {www.error}");
                }
            }
#else
            // In the Editor, standard System.IO works fine
            if (System.IO.File.Exists(path))
            {
                csvContent = System.IO.File.ReadAllText(path);
            }
            else
            {
                Debug.LogError($"[LevelDirector] File not found: {path}");
            }
            yield return null; // Yield one frame just to be safe
#endif

            parsedTasks = CSVLevelParser.ParseSessionCSVText(csvContent, patientProfile);
            
            if (randomizeTaskOrder && parsedTasks != null && parsedTasks.Count > 0)
            {
                // Fisher-Yates Shuffle to randomize task order
                for (int i = 0; i < parsedTasks.Count; i++)
                {
                    int randomIndex = UnityEngine.Random.Range(i, parsedTasks.Count);
                    TaskRow temp = parsedTasks[i];
                    parsedTasks[i] = parsedTasks[randomIndex];
                    parsedTasks[randomIndex] = temp;
                }
                Debug.Log("[LevelDirector] Task order has been randomized!");
            }
            
            if (parsedTasks.Count > 0)
            {
                currentTaskIndex = 0; // Reset index to start from the beginning
                Debug.Log($"[LevelDirector] Successfully loaded {parsedTasks.Count} tasks. Starting Game Loop...");
                StartCoroutine(GameLoop());
            }
            else
            {
                Debug.LogWarning("[LevelDirector] No valid tasks found in the CSV.");
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
            
            // Sync with Menu Configuration!
            sessionDurationSeconds = (TemporarySessionData.Duration == SessionDuration.ThreeMinutes) ? 180f : 300f;
            gameStartTime = Time.time;
            isGameActive = true;

            // Give the player a short delay before the first balloon
            
            // --- ACCESSIBILITY / UX: Align Physical Environment ---
            // The balloons spawn relative to the CenterEyeAnchor, which depends on where the player is physically looking.
            // If they manually built a table/plants in the editor, we need to snap that environment's rotation 
            // to match the player's initial view, otherwise the table will be beside or behind them!
            GameObject envRoot = GameObject.Find("Environment_Root");
            if (envRoot != null && PopstrikeVR.Gameplay.WorkspaceMapper.Instance != null && PopstrikeVR.Gameplay.WorkspaceMapper.Instance.HeadOrigin != null)
            {
                Vector3 euler = PopstrikeVR.Gameplay.WorkspaceMapper.Instance.HeadOrigin.rotation.eulerAngles;
                envRoot.transform.rotation = Quaternion.Euler(0, euler.y, 0);
            }

            yield return new WaitForSeconds(2.0f);

            while (isGameActive)
            {
                if (parsedTasks == null || parsedTasks.Count == 0) break;

                // Fallback: If CSV has ended, start looping again from start until time reaches
                if (currentTaskIndex >= parsedTasks.Count)
                {
                    Debug.Log("[LevelDirector] CSV ended. Looping back to start to fill remaining time.");
                    currentTaskIndex = 0;
                }

                errorsInCurrentWave = 0; // Reset error tracker for the new wave
                TaskRow currentTask = parsedTasks[currentTaskIndex];
                CurrentTaskType = currentTask.TaskType;
                
                totalWavesSpawned++;
                
                // --- ACCESSIBILITY: Auto-Lock Gesture for Easy/Medium ---
                Coroutine autoLockRoutine = null;
                if (PopstrikeVR.Core.TemporarySessionData.Difficulty != "Hard")
                {
                    autoLockRoutine = StartCoroutine(AutoLockGestureRoutine(GetRequiredGesture(CurrentTaskType)));
                }
                
                List<GameObject> spawnedBalloons = SpawnTask(currentTask);
                
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
                            // If they have started connecting the trail, PAUSE the global timer!
                            // TMTSolverScript has its own 3-second connection timeout, so they can't wait forever.
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
                            // If they are actively tracing the corridor, PAUSE the global timer!
                            if (!TracePathManager.Instance.IsTracking)
                            {
                                if (wasTracking)
                                {
                                    // They just failed the trace! Add a 1 sec extra timer to let them try again.
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
                    // For regular Orange Punch balloons, give them the standard time limit
                    // BUT allow early exit if they pop all balloons quickly!
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
                            break; // Skip the rest of the task timer!
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

                // GDD Rule: Miss/Timeout
                // If balloons are still active after the timer AND they are not currently popping, they were missed!
                bool missedAny = false;
                foreach(var balloon in spawnedBalloons)
                {
                    if (balloon != null && balloon.activeInHierarchy)
                    {
                        // Check if it's already popped/dissolving
                        if (balloon.TryGetComponent<PopstrikeVR.Gameplay.BaseBalloon>(out var baseBalloon))
                        {
                            if (baseBalloon.IsPopped) continue; // It's just playing its pop animation, don't count as a miss!
                            if (baseBalloon is PopstrikeVR.Gameplay.BladeBalloon blade && blade.IsSliced) continue; // It's waiting for its cascade pop!
                        }

                        missedAny = true;
                        if (PopstrikeVR.Gameplay.ComboManager.Instance != null) 
                            PopstrikeVR.Gameplay.ComboManager.Instance.BreakCombo();
                        
                        // Tell the balloon to smoothly shrink away instead of popping
                        if (baseBalloon != null)
                        {
                            baseBalloon.AnimateDespawn(0.5f);
                        }

                        // Delay the actual despawn so the shrink animation has time to play
                        PopstrikePooler.DespawnBalloon(balloon, 0.5f);
                    }
                }

                if (missedAny)
                {
                    totalWavesMissed++;
                    if (PopstrikeFeedbackManager.Instance != null)
                        PopstrikeFeedbackManager.Instance.PlayErrorTone();
                    
                    // Wait for the deflate animations to finish before spawning the next wave
                    yield return new WaitForSeconds(0.5f);
                }
                else
                {
                    // Give the player a breather between successful waves so they don't instantly overlap!
                    yield return new WaitForSeconds(delayBetweenWaves);
                }

                currentTaskIndex++;
            }
            
            CalculateAndShowResults();
        }

        private void CalculateAndShowResults()
        {
            Debug.Log("[LevelDirector] Session Complete! Showing Results UI...");
            
            // --- Determine minimum waves required based on difficulty + session mode ---
            string difficulty = levelSubFolder.Contains("Medium") ? "Medium" : levelSubFolder.Contains("Hard") ? "Hard" : "Easy";

            // --- Accuracy (includes both missed waves AND in-wave errors for clinical precision) ---
            // Weight the errors based on difficulty to make it more forgiving
            float weightedErrors = totalErrors;
            if (difficulty == "Easy") weightedErrors = totalErrors / 3.0f;
            else if (difficulty == "Medium") weightedErrors = totalErrors / 2.0f;
            else if (difficulty == "Hard") weightedErrors = totalErrors / 1.5f;

            // Total correct actions = spawned waves cleared, penalised by any in-wave errors.
            int totalCorrectActions = totalWavesSpawned - totalWavesMissed;
            float totalActions = totalWavesSpawned + weightedErrors;
            float accuracy = totalActions > 0 ? (float)totalCorrectActions / totalActions * 100f : 0f;
            accuracy = Mathf.Clamp(accuracy, 0f, 100f);
            
            int maxCombo = PopstrikeVR.Gameplay.ComboManager.Instance != null ? 
                PopstrikeVR.Gameplay.ComboManager.Instance.HighestCombo : 0;
            int minWavesRequired;

            if (sessionDuration == SessionDuration.ThreeMinutes)
            {
                if (difficulty == "Medium")     minWavesRequired = minWaves_Medium_3min;
                else if (difficulty == "Hard")  minWavesRequired = minWaves_Hard_3min;
                else                            minWavesRequired = minWaves_Easy_3min;
            }
            else // FiveMinutes
            {
                if (difficulty == "Medium")     minWavesRequired = minWaves_Medium_5min;
                else if (difficulty == "Hard")  minWavesRequired = minWaves_Hard_5min;
                else                            minWavesRequired = minWaves_Easy_5min;
            }

            int wavesCleared = totalWavesSpawned - totalWavesMissed;
            bool levelPassed = wavesCleared >= minWavesRequired;

            // --- Star & Accuracy thresholds based on difficulty ---
            float targetAccuracy = 75f; // Easy default
            int star3ErrorThreshold = 5; // Easy default

            if (difficulty == "Medium")
            {
                targetAccuracy = 80f;
                star3ErrorThreshold = 4;
            }
            else if (difficulty == "Hard")
            {
                targetAccuracy = 85f;
                star3ErrorThreshold = 2;
            }

            // Star 0: Level FAILED — not enough waves cleared.
            // Star 1: Level PASSED — cleared the minimum wave threshold.
            // Star 2: Accuracy >= targetAccuracy.
            // Star 3: Star 2 earned + errors within the difficulty threshold.
            // Strict Left → Center → Right hierarchical sequence.
            int starCount = 0;
            if (levelPassed)
            {
                starCount = 1;
                if (accuracy >= targetAccuracy) starCount = 2;
                if (starCount == 2 && totalErrors <= star3ErrorThreshold) starCount = 3;
            }

            // --- Read Final Score from HUD ---
            int finalScore = PopstrikeVR.UI.PopstrikeHUDController.Instance != null ?
                PopstrikeVR.UI.PopstrikeHUDController.Instance.CurrentScore : 0;

            Debug.Log($"[LevelDirector] Final -> Difficulty: {difficulty} | Session: {sessionDuration} | Waves Cleared: {wavesCleared}/{minWavesRequired} | Passed: {levelPassed} | Accuracy: {accuracy:0.0}% (Target: {targetAccuracy}%) | Errors: {totalErrors} (Threshold: {star3ErrorThreshold}) | Streak: {maxCombo} | Score: {finalScore} | Stars: {starCount}");

            // --- Spawn UI ---
            if (PopstrikeVR.UI.LevelResultsUI.Instance != null && WorkspaceMapper.Instance != null && WorkspaceMapper.Instance.HeadOrigin != null)
            {
                Transform head = WorkspaceMapper.Instance.HeadOrigin;
                Vector3 forwardLevel = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (forwardLevel == Vector3.zero) forwardLevel = head.up;
                
                Vector3 spawnPos = head.position + (forwardLevel * resultsUIDistance);
                spawnPos.y = head.position.y + resultsUIHeightOffset;
                
                PopstrikeVR.UI.LevelResultsUI.Instance.transform.position = spawnPos;
                PopstrikeVR.UI.LevelResultsUI.Instance.transform.rotation = Quaternion.LookRotation(forwardLevel, Vector3.up);
                
                PopstrikeVR.UI.LevelResultsUI.Instance.DisplayResults(accuracy, maxCombo, starCount, finalScore, targetAccuracy, star3ErrorThreshold);
            }
        }
        private List<Vector3> ExpandAuthoredCoordinates(List<Vector3> coords, float expansionMultiplier)
        {
            if (coords == null || coords.Count <= 1) return new List<Vector3>(coords);

            Vector3 center = Vector3.zero;
            foreach (var c in coords) center += c;
            center /= coords.Count;

            List<Vector3> expanded = new List<Vector3>();
            foreach (var c in coords)
            {
                float newAz = center.x + (c.x - center.x) * expansionMultiplier;
                float newEl = center.y + (c.y - center.y) * expansionMultiplier;
                expanded.Add(new Vector3(newAz, newEl, c.z));
            }
            return expanded;
        }
        
        private List<GameObject> SpawnTask(TaskRow task)
        {
            List<GameObject> spawned = new List<GameObject>();
            List<Vector3> mappedPositions = new List<Vector3>();
            float shapeExpansionMultiplier = 2.2f; // Scales authored shapes out to fit large balloons without overlapping

            switch (task.TaskType)
            {
                case BalloonTaskType.Orange_Punch:
                    foreach(var spherical in task.SphericalCoordinates)
                    {
                        // True for relaxation: Push these apart if they overlap!
                        Vector3 pos = WorkspaceMapper.Instance.GetWorldPositionSafely(spherical, patientProfile, mappedPositions, true);
                        mappedPositions.Add(pos);
                        GameObject obj = PopstrikePooler.SpawnBalloon("BlazeBalloon", pos, Quaternion.identity);
                        if (obj != null)
                        {
                            if (obj.TryGetComponent<PopstrikeVR.Gameplay.BlazeBalloon>(out var blaze))
                            {
                                blaze.Setup(pos);
                                blaze.Initialize(patientProfile);
                            }
                            spawned.Add(obj);
                        }
                    }
                    break;

                case BalloonTaskType.Blue_Slash:
                    {
                        var expandedCoords = ExpandAuthoredCoordinates(task.SphericalCoordinates, shapeExpansionMultiplier);
                        foreach(var spherical in expandedCoords)
                        {
                            // True for relaxation: Prevent overlap using the new MinSafeDistance
                            Vector3 pos = WorkspaceMapper.Instance.GetWorldPositionSafely(spherical, patientProfile, mappedPositions, true);
                            mappedPositions.Add(pos);
                            GameObject obj = PopstrikePooler.SpawnBalloon("BladeBalloon", pos, Quaternion.identity);
                            if (obj != null) 
                            {
                                if (obj.TryGetComponent<PopstrikeVR.Gameplay.BaseBalloon>(out var baseB)) baseB.Setup(pos);
                                spawned.Add(obj);
                            }
                        }
                    }
                    
                    if (PopstrikeVR.Gameplay.BladeSlashManager.Instance == null)
                    {
                        var go = new GameObject("BladeSlashManager");
                        go.AddComponent<PopstrikeVR.Gameplay.BladeSlashManager>();
                    }
                    PopstrikeVR.Gameplay.BladeSlashManager.Instance.RegisterSequence(spawned);
                    break;

                case BalloonTaskType.Green_Trace:
                    {
                        var expandedCoords = ExpandAuthoredCoordinates(task.SphericalCoordinates, shapeExpansionMultiplier);
                        foreach(var spherical in expandedCoords)
                        {
                            // Pull the Green Trace task 15cm closer to the patient for easier depth perception!
                            float traceDepthOffset = 0.15f; 
                            
                            // True for relaxation: Prevent overlap using the new MinSafeDistance
                            Vector3 pos = WorkspaceMapper.Instance.GetWorldPositionSafely(spherical, patientProfile, mappedPositions, true, traceDepthOffset);
                            mappedPositions.Add(pos);
                            GameObject obj = PopstrikePooler.SpawnBalloon("TraceBalloon", pos, Quaternion.identity);
                            if (obj != null) 
                            {
                                if (obj.TryGetComponent<PopstrikeVR.Gameplay.BaseBalloon>(out var baseB)) baseB.Setup(pos);
                                spawned.Add(obj);
                            }
                        }
                    }
                    
                    if (PopstrikeVR.Gameplay.TracePathManager.Instance == null)
                    {
                        var go = new GameObject("TracePathManager");
                        go.AddComponent<PopstrikeVR.Gameplay.TracePathManager>();
                    }
                    PopstrikeVR.Gameplay.TracePathManager.Instance.RegisterSequence(spawned);
                    break;

                case BalloonTaskType.TMTA:
                case BalloonTaskType.TMTB:
                    {
                        // Spawn Transparent balloons for the Trail Making Test
                        List<GameObject> tmtSequence = new List<GameObject>();
                        var expandedCoords = ExpandAuthoredCoordinates(task.SphericalCoordinates, shapeExpansionMultiplier);
                        foreach(var spherical in expandedCoords)
                        {
                            // True for relaxation: Prevent overlap using the new MinSafeDistance
                            Vector3 pos = WorkspaceMapper.Instance.GetWorldPositionSafely(spherical, patientProfile, mappedPositions, true);
                            mappedPositions.Add(pos);
                            GameObject obj = PopstrikePooler.SpawnBalloon("TrailBalloon", pos, Quaternion.identity);
                            if (obj != null) 
                            {
                                Debug.Log($"<color=cyan>[LevelDirector] SUCCESSFULLY SPAWNED TrailBalloon at Pos: {pos}, Scale: {obj.transform.localScale}, Active: {obj.activeInHierarchy}</color>");
                                tmtSequence.Add(obj);
                                spawned.Add(obj);
                            }
                            else
                            {
                                Debug.LogError($"<color=red>[LevelDirector] FAILED TO SPAWN TrailBalloon at Pos: {pos}. SpawnBalloon returned null!</color>");
                            }
                        }
                    // Assign Labels based on Task Type
                    int number = 1;
                    char letter = 'A';
                    for(int i = 0; i < tmtSequence.Count; i++)
                    {
                        var obj = tmtSequence[i];
                        if(obj.TryGetComponent<PopstrikeVR.Gameplay.TrailBalloon>(out var trail))
                        {
                            if (task.TaskType == BalloonTaskType.TMTA)
                            {
                                trail.SetupTMT(obj.transform.position, number.ToString());
                                number++;
                            }
                            else if (task.TaskType == BalloonTaskType.TMTB)
                            {
                                if (i % 2 == 0)
                                {
                                    trail.SetupTMT(obj.transform.position, number.ToString());
                                    number++;
                                }
                                else
                                {
                                    trail.SetupTMT(obj.transform.position, letter.ToString());
                                    letter++;
                                }
                            }
                        }
                    }

                    if (TMTSolverScript.Instance != null && tmtSequence.Count > 0)
                        TMTSolverScript.Instance.RegisterSequence(tmtSequence, task.TaskType == BalloonTaskType.TMTB);
                    }
                    break;
            }
            return spawned;
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
