using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
using Rehab.Volleyball.UI;

namespace Rehab.Volleyball.Core
{
    [System.Serializable]
    public class TargetSpawnChance {
        public float azimuth0to30;
        public float azimuth30to60;
        public float azimuth60to90;

        public float elevationNeg45toNeg15;
        public float elevationNeg15to15;
        public float elevation15to45;

        public float distance0to0_2;
        public float distance0_2to0_4;
        public float distance0_4to0_6;
    }

    [System.Serializable]
    public class VolleyballRound {
        public int pointsToWin;
    }

    [System.Serializable]
    public class VolleyballBreak {
        public float durationInMinutes;
    }

    [System.Serializable]
    public class VolleyballLevel {
        public VolleyballRound[] rounds;
        public VolleyballBreak[] breaks;
        public TargetSpawnChance targetSpawnChances;
    }

    public class VolleyballLevelDirector : MonoBehaviour
    {
        public static VolleyballLevelDirector Instance { get; private set; }

        public VolleyballLevel CurrentLevelConfig { get; private set; }
        public bool IsLevelRunning { get; private set; } = false;
        public bool IsBreakActive { get; private set; } = false;

        [Header("Local Level Fallbacks")]
        [SerializeField] private TextAsset easyLevelConfig;
        [SerializeField] private TextAsset mediumLevelConfig;
        [SerializeField] private TextAsset hardLevelConfig;

        [Header("Cloud Configuration")]
        [Tooltip("The URL to fetch the level config from. If empty or fetching fails, it falls back to the TextAssets above.")]
        [SerializeField] private string externalLevelUrl;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            StartCoroutine(LoadLevelConfigRoutine());
        }

        private IEnumerator LoadLevelConfigRoutine()
        {
            string jsonText = null;

            // 1. Try to fetch from external URL
            if (!string.IsNullOrEmpty(externalLevelUrl))
            {
                string requestUrl = externalLevelUrl;
                if (!requestUrl.Contains("://") && !requestUrl.Contains(":///"))
                {
                    requestUrl = "file:///" + requestUrl;
                }

                using (UnityWebRequest request = UnityWebRequest.Get(requestUrl))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        jsonText = request.downloadHandler.text;
                        Debug.Log($"[VolleyballLevelDirector] Successfully loaded JSON from URL: {requestUrl}");
                    }
                    else
                    {
                        Debug.LogWarning($"[VolleyballLevelDirector] Failed to load JSON from URL: {requestUrl}. Error: {request.error}");
                    }
                }
            }

            // 2. Try to parse external JSON if fetched
            if (!string.IsNullOrEmpty(jsonText))
            {
                try
                {
                    CurrentLevelConfig = JsonUtility.FromJson<VolleyballLevel>(jsonText);
                    // Check if it parsed at least something meaningful
                    if (CurrentLevelConfig != null && CurrentLevelConfig.rounds != null)
                    {
                        Debug.Log("[VolleyballLevelDirector] Cloud JSON parsed successfully.");
                    }
                    else
                    {
                        throw new System.Exception("Parsed JSON was empty or invalid structure.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[VolleyballLevelDirector] Error parsing Cloud JSON: {e.Message}");
                    CurrentLevelConfig = null; // Force fallback
                }
            }

            // 3. CRITICAL FALLBACK: Use local TextAsset if cloud failed
            if (CurrentLevelConfig == null || CurrentLevelConfig.rounds == null)
            {
                Debug.Log("[VolleyballLevelDirector] Falling back to local TextAsset based on difficulty...");
                TextAsset fallbackAsset = mediumLevelConfig; // Default

                // Wait for GameManager to be ready so we can read the difficulty
                yield return new WaitUntil(() => VolleyballGameManager.Instance != null);

                switch (VolleyballGameManager.Instance.difficultyMode)
                {
                    case VolleyballGameManager.DifficultyMode.Easy:
                        fallbackAsset = easyLevelConfig;
                        break;
                    case VolleyballGameManager.DifficultyMode.Medium:
                        fallbackAsset = mediumLevelConfig;
                        break;
                    case VolleyballGameManager.DifficultyMode.Hard:
                        fallbackAsset = hardLevelConfig;
                        break;
                }

                if (fallbackAsset != null && !string.IsNullOrEmpty(fallbackAsset.text))
                {
                    try
                    {
                        CurrentLevelConfig = JsonUtility.FromJson<VolleyballLevel>(fallbackAsset.text);
                        Debug.Log($"[VolleyballLevelDirector] Successfully loaded fallback JSON from TextAsset: {fallbackAsset.name}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[VolleyballLevelDirector] CRITICAL ERROR parsing fallback TextAsset: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogError("[VolleyballLevelDirector] CRITICAL ERROR: No fallback TextAsset assigned for the current difficulty!");
                }
            }

            if (CurrentLevelConfig != null)
            {
                // Auto-start if there is no menu open (direct Editor testing)
                bool hasMenuOpen = false;
                var menu = FindFirstObjectByType<VolleyballMenuManager>();
                if (menu != null && menu.menuPanel != null && menu.menuPanel.activeSelf)
                {
                    hasMenuOpen = true;
                }

                if (!hasMenuOpen)
                {
                    Debug.Log("[VolleyballLevelDirector] Auto-starting level (No menu detected)");
                    StartLevel();
                }
            }
        }

        public void StartLevel()
        {
            if (CurrentLevelConfig == null || CurrentLevelConfig.rounds == null || CurrentLevelConfig.rounds.Length == 0)
            {
                VolleyballGameManager.Instance.StartGame();
                return;
            }

            if (IsLevelRunning) return;
            IsLevelRunning = true;
            
            StartCoroutine(GameLoopRoutine());
        }

        private IEnumerator GameLoopRoutine()
        {
            yield return new WaitUntil(() => VolleyballGameManager.Instance != null);

            if (VolleyballMenuManager.Instance != null && VolleyballMenuManager.Instance.menuPanel != null)
            {
                VolleyballMenuManager.Instance.menuPanel.SetActive(false);
            }

            VolleyballGameManager.Instance.StartGame();

            for (int i = 0; i < CurrentLevelConfig.rounds.Length; i++)
            {
                VolleyballRound currentRound = CurrentLevelConfig.rounds[i];
                Debug.Log($"[VolleyballLevelDirector] Starting Round {i + 1}");

                while (true)
                {
                    if (VolleyballGameManager.Instance.PlayerScore >= currentRound.pointsToWin || 
                        VolleyballGameManager.Instance.AIScore >= currentRound.pointsToWin)
                    {
                        break; // Round finished
                    }
                    yield return null;
                }

                if (CurrentLevelConfig.breaks != null && i < CurrentLevelConfig.breaks.Length)
                {
                    VolleyballBreak nextBreak = CurrentLevelConfig.breaks[i];
                    IsBreakActive = true;
                    Debug.Log($"[VolleyballLevelDirector] Break Started for {nextBreak.durationInMinutes} minutes.");
                    
                    DespawnInactiveBall();

                    float breakTimer = nextBreak.durationInMinutes * 60f;
                    
                    if (VolleyballBreakUIManager.Instance != null)
                    {
                        VolleyballBreakUIManager.Instance.ShowBreakUI();
                    }

                    while (breakTimer > 0)
                    {
                        breakTimer -= Time.deltaTime;
                        
                        if (VolleyballBreakUIManager.Instance != null)
                        {
                            VolleyballBreakUIManager.Instance.UpdateCountdownText(breakTimer);
                        }
                        
                        yield return null;
                    }

                    IsBreakActive = false;
                    VolleyballGameManager.Instance.ResetMatchScores(); 
                    
                    if (VolleyballBreakUIManager.Instance != null)
                    {
                        VolleyballBreakUIManager.Instance.HideBreakUI();
                    }
                }
            }
            
            VolleyballGameManager.Instance.ForceMatchOver();
        }

        private void DespawnInactiveBall()
        {
            if (!VolleyballGameManager.Instance.IsRallyActive && VolleyballGameManager.Instance.ActiveBall != null)
            {
                VolleyballGameManager.Instance.ActiveBall.gameObject.SetActive(false);
            }
        }

        public bool CanSpawnServe()
        {
            return !IsBreakActive;
        }
    }
}
