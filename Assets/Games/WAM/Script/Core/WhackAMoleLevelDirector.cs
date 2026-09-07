using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WhackAMole
{
    [Serializable]
    public class WAMLevelConfig
    {
        public List<WAMRound> rounds;
        public List<WAMBreak> breaks;
        public WAMCharacters characters;
        public WAMAzimuths azimuths;
        public WAMDistances distances;
        public WAMElevations elevations;
    }

    [Serializable]
    public class WAMRound
    {
        public float durationInMinutes;
    }

    [Serializable]
    public class WAMBreak
    {
        public float durationInMinutes;
    }

    [Serializable]
    public class WAMCharacters
    {
        public int standard;
        public int fake;
        public int dog;
        public int bird;
        public int heavy;
        public int treasure;
    }

    [Serializable]
    public class WAMAzimuths
    {
        public int center_0_15;
        public int left_15_45;
        public int right_15_45;
    }

    [Serializable]
    public class WAMDistances
    {
        public int near;
        public int mid;
        public int far;
    }

    [Serializable]
    public class WAMElevations
    {
        public int bird_15_30;
        public int bird_30_45;
    }

    public struct ProceduralSpawnData
    {
        public string characterTag;
        public float azimuth;
        public float distance;
    }

    public class WhackAMoleLevelDirector : MonoBehaviour, IGameStateListener
    {
        public static WhackAMoleLevelDirector Instance { get; private set; }

        [Header("Custom Override (Optional)")]
        [Tooltip("If provided, will attempt to load this JSON file. If empty or fails, falls back to the difficulty TextAssets below.")]
        [SerializeField] private string customJsonPath = "";

        [Header("Difficulty JSON Configs")]
        [SerializeField] private TextAsset easyConfig;
        [SerializeField] private TextAsset mediumConfig;
        [SerializeField] private TextAsset hardConfig;
        public event Action OnBreakStarted;
        public event Action OnBreakEnded;
        public event Action<float> OnBreakTimerUpdated;
        public event Action<float, int> OnRoundTimerUpdated;

        private WAMLevelConfig levelConfig;
        private bool isBreakPhase = false;
        private Coroutine gameLoopCoroutine;

        public bool IsBreakPhase => isBreakPhase;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(LoadConfigRoutine());
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterListener(this);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterListener(this);
            }
        }

        private IEnumerator LoadConfigRoutine()
        {
            // Wait for GameManager to be ready if it's not
            while (GameManager.Instance == null || GameManager.Instance.DifficultyProfile == null)
            {
                yield return null;
            }

            string loadedJsonText = null;

            // 1. Try to load Custom JSON path first (Cloud / Local Disk)
            if (!string.IsNullOrEmpty(customJsonPath))
            {
                string fullPath = customJsonPath;
                if (!fullPath.Contains("://") && !fullPath.Contains(":/") && !fullPath.StartsWith("/"))
                {
                    // Assume it's a relative path in StreamingAssets if no root is provided
                    fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, customJsonPath);
                }

                if (fullPath.Contains("://") || fullPath.Contains(":///"))
                {
                    using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(fullPath))
                    {
                        yield return request.SendWebRequest();
                        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                        {
                            loadedJsonText = request.downloadHandler.text;
                        }
                    }
                }
                else if (System.IO.File.Exists(fullPath))
                {
                    loadedJsonText = System.IO.File.ReadAllText(fullPath);
                }

                if (string.IsNullOrEmpty(loadedJsonText))
                {
                    Debug.LogWarning($"[WhackAMoleLevelDirector] Failed to load custom JSON at '{fullPath}'. Falling back to assigned TextAssets.");
                }
            }

            // 2. Fallback to TextAsset selection if custom loading failed or was empty
            if (string.IsNullOrEmpty(loadedJsonText))
            {
                TextAsset selectedConfigAsset = mediumConfig; // Default
                var difficulty = GameManager.Instance.DifficultyProfile.selectedDifficulty;
                switch (difficulty)
                {
                    case GameDifficulty.Easy:
                        selectedConfigAsset = easyConfig;
                        break;
                    case GameDifficulty.Medium:
                        selectedConfigAsset = mediumConfig;
                        break;
                    case GameDifficulty.Hard:
                        selectedConfigAsset = hardConfig;
                        break;
                }

                if (selectedConfigAsset != null)
                {
                    loadedJsonText = selectedConfigAsset.text;
                    Debug.Log($"[WhackAMoleLevelDirector] Loaded TextAsset config for difficulty: {difficulty}");
                }
            }

            // 3. Parse whatever JSON we successfully loaded
            if (!string.IsNullOrEmpty(loadedJsonText))
            {
                levelConfig = JsonUtility.FromJson<WAMLevelConfig>(loadedJsonText);
                Debug.Log($"[WhackAMoleLevelDirector] Config parsed successfully. Rounds: {levelConfig?.rounds?.Count}, Breaks: {levelConfig?.breaks?.Count}");
            }
            
            // 4. SAFETY NET: If everything failed, use a hardcoded default so the game never freezes
            if (levelConfig == null)
            {
                Debug.LogWarning("[WhackAMoleLevelDirector] No config loaded — using built-in default (2x2min rounds, 1min break).");
                levelConfig = new WAMLevelConfig
                {
                    rounds = new List<WAMRound> { new WAMRound { durationInMinutes = 2f }, new WAMRound { durationInMinutes = 2f } },
                    breaks = new List<WAMBreak> { new WAMBreak { durationInMinutes = 1f } },
                    characters = new WAMCharacters { standard = 80, fake = 0, dog = 10, bird = 10, heavy = 0, treasure = 0 },
                    azimuths = new WAMAzimuths { center_0_15 = 60, left_15_45 = 20, right_15_45 = 20 },
                    distances = new WAMDistances { near = 50, mid = 35, far = 15 },
                    elevations = new WAMElevations { bird_15_30 = 70, bird_30_45 = 30 }
                };
            }
            
            // 5. Start the gameplay loop now that config is guaranteed to be ready
            if (gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
            gameLoopCoroutine = StartCoroutine(GameLoopRoutine());
        }

        public void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Playing)
            {
                // Only restart the game loop here if config is already loaded.
                // If config isn't loaded yet, LoadConfigRoutine will start the loop itself when ready.
                if (levelConfig != null)
                {
                    if (gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
                    gameLoopCoroutine = StartCoroutine(GameLoopRoutine());
                }
            }
            else
            {
                if (gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
                isBreakPhase = false;
            }
        }

        private IEnumerator GameLoopRoutine()
        {
            // Config is guaranteed to be set before this coroutine is called, but guard just in case
            if (levelConfig == null)
            {
                Debug.LogError("[WhackAMoleLevelDirector] GameLoopRoutine started with null config — aborting!");
                yield break;
            }

            int roundIndex = 0;
            int breakIndex = 0;

            while (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                if (levelConfig.rounds != null && roundIndex >= levelConfig.rounds.Count)
                {
                    GameManager.Instance.UpdateState(GameState.Finished);
                    break;
                }

                // Play Round
                isBreakPhase = false;
                OnBreakEnded?.Invoke();
                
                float roundMinutes = 3f; // Fallback changed to 3 min
                if (levelConfig.rounds != null && levelConfig.rounds.Count > 0)
                {
                    int rIdx = Mathf.Min(roundIndex, levelConfig.rounds.Count - 1);
                    roundMinutes = levelConfig.rounds[rIdx].durationInMinutes;
                }
                
                float roundTimer = roundMinutes * 60f;
                while (roundTimer > 0)
                {
                    roundTimer -= Time.deltaTime;
                    OnRoundTimerUpdated?.Invoke(roundTimer, roundIndex + 1);
                    yield return null;
                }
                roundIndex++;

                // Take Break
                isBreakPhase = true;
                
                // Despawn all active moles
                BaseMole[] activeMoles = FindObjectsByType<BaseMole>(FindObjectsSortMode.None);
                foreach (var mole in activeMoles)
                {
                    if (mole.gameObject.activeInHierarchy) mole.RetractIntoHole();
                }

                OnBreakStarted?.Invoke();
                
                float breakMinutes = 1f; // Fallback
                if (levelConfig.breaks != null && levelConfig.breaks.Count > 0)
                {
                    int bIdx = Mathf.Min(breakIndex, levelConfig.breaks.Count - 1);
                    breakMinutes = levelConfig.breaks[bIdx].durationInMinutes;
                }

                float breakTimer = breakMinutes * 60f;
                while (breakTimer > 0)
                {
                    breakTimer -= Time.deltaTime;
                    OnBreakTimerUpdated?.Invoke(breakTimer);
                    yield return null;
                }
                breakIndex++;
            }
        }

        public ProceduralSpawnData GetProceduralTarget()
        {
            ProceduralSpawnData data = new ProceduralSpawnData();
            
            if (levelConfig == null)
            {
                data.characterTag = "Standard";
                data.azimuth = 0f;
                data.distance = 0.4f;
                return data;
            }

            // Check Menu Toggles (Override JSON if Fake Mole is toggled OFF in menu)
            int fakeWeight = levelConfig.characters.fake;
            if (GameManager.Instance != null && GameManager.Instance.DifficultyProfile != null)
            {
                if (GameManager.Instance.DifficultyProfile.distractorProbability <= 0f)
                {
                    fakeWeight = 0;
                }
            }

            // Roll Character
            int cTotal = levelConfig.characters.standard + fakeWeight + levelConfig.characters.dog + 
                         levelConfig.characters.bird + levelConfig.characters.heavy + levelConfig.characters.treasure;
            
            // Failsafe in case all weights are 0
            if (cTotal <= 0) 
            {
                data.characterTag = "Standard";
            }
            else
            {
                int cRoll = UnityEngine.Random.Range(0, cTotal);
                int cAccum = 0;

                if (cRoll < (cAccum += levelConfig.characters.standard)) data.characterTag = "Standard";
                else if (cRoll < (cAccum += fakeWeight)) data.characterTag = "Fake";
                else if (cRoll < (cAccum += levelConfig.characters.dog)) data.characterTag = "Dog";
                else if (cRoll < (cAccum += levelConfig.characters.bird)) data.characterTag = "Bird";
                else if (cRoll < (cAccum += levelConfig.characters.heavy)) data.characterTag = "Heavy";
                else data.characterTag = "Treasure";
            }

            // Roll Azimuth
            int aTotal = levelConfig.azimuths.center_0_15 + levelConfig.azimuths.left_15_45 + levelConfig.azimuths.right_15_45;
            int aRoll = UnityEngine.Random.Range(0, aTotal);
            int aAccum = 0;

            if (aRoll < (aAccum += levelConfig.azimuths.center_0_15))
            {
                data.azimuth = UnityEngine.Random.Range(-15f, 15f);
            }
            else if (aRoll < (aAccum += levelConfig.azimuths.left_15_45))
            {
                data.azimuth = UnityEngine.Random.Range(15f, 45f);
            }
            else
            {
                data.azimuth = UnityEngine.Random.Range(-45f, -15f);
            }

            // Roll Distance
            int dTotal = levelConfig.distances.near + levelConfig.distances.mid + levelConfig.distances.far;
            int dRoll = UnityEngine.Random.Range(0, dTotal);
            int dAccum = 0;

            if (dRoll < (dAccum += levelConfig.distances.near))
            {
                data.distance = UnityEngine.Random.Range(0.2f, 0.4f);
            }
            else if (dRoll < (dAccum += levelConfig.distances.mid))
            {
                data.distance = UnityEngine.Random.Range(0.4f, 0.5f);
            }
            else
            {
                data.distance = UnityEngine.Random.Range(0.5f, 0.65f); // far
            }

            // Elevations are handled externally (if it's a bird)
            return data;
        }

        public float GetBirdElevation()
        {
            if (levelConfig == null) return 15f;
            int eTotal = levelConfig.elevations.bird_15_30 + levelConfig.elevations.bird_30_45;
            int eRoll = UnityEngine.Random.Range(0, eTotal);
            if (eRoll < levelConfig.elevations.bird_15_30) return UnityEngine.Random.Range(15f, 30f);
            return UnityEngine.Random.Range(30f, 45f);
        }
    }
}
