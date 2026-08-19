using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace PopstrikeVR.UI
{
    /// <summary>
    /// Controls the 3D Canvas Results Screen at the end of a level.
    /// Handles Star Logic and animated UI feedback with three circular gauges:
    ///   Left  = Score     | Center = Accuracy %  | Right = Best Streak
    /// </summary>
    public class LevelResultsUI : MonoBehaviour
    {
        public static LevelResultsUI Instance { get; private set; }

        [Header("Title")]
        public TextMeshProUGUI scoreTitleText; // "Level Cleared!" or "Level Failed"

        [Header("Star Graphics")]
        public Image[] stars;
        public Color starEarnedColor = Color.yellow;
        public Color starEmptyColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public float starAnimationDuration = 0.5f;

        [Header("Left Circle — Score")]
        public TextMeshProUGUI scoreValueText;
        public Image scoreRingFill;
        public int   scoreTargetGoal = 10000; // Ring fills to 100% at this score

        [Header("Center Circle — Accuracy")]
        public TextMeshProUGUI accuracyValueText;
        public Image accuracyRingFill;

        [Header("Right Circle — Best Streak")]
        public TextMeshProUGUI  streakValueText;
        public Image streakRingFill;
        public int   streakTargetGoal = 10; // Ring fills to 100% at a streak of 10

        [Header("Star Condition Labels")]
        public TextMeshProUGUI star1LabelText;
        public TextMeshProUGUI star2LabelText;
        public TextMeshProUGUI star3LabelText;

        [Header("Ring Colors")]
        public Color ringColorLow    = new Color(1f,  0.35f, 0.1f,  1f); // Orange-Red
        public Color ringColorMid    = new Color(0.1f,0.9f,  1f,    1f); // Cyan
        public Color ringColorHigh   = new Color(0.2f,1f,    0.4f,  1f); // Neon Green
        public float ringAnimDuration = 0.8f;

        [Header("Buttons")]
        public Button nextLevelButton;
        public Button retryButton;
        public Button mainMenuButton;

        [Header("Panel Control")]
        [Tooltip("Drag the child Panel GameObject here (NOT the root). This is hidden on start and shown at end of level.")]
        public GameObject resultsPanel;

        private Vector3[] starOriginalScales;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            // Cache the original scales so we don't blow them up to 1.0 if the user authored them smaller
            starOriginalScales = new Vector3[stars.Length];
            for(int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null) starOriginalScales[i] = stars[i].transform.localScale;
                else starOriginalScales[i] = Vector3.one;
            }

            // IMPORTANT: The ROOT object stays active so Awake() always runs.
            // Only the visual panel is hidden.
            HideUI();
        }

        /// <summary>
        /// Hides the results panel. Called automatically on Awake and can be
        /// called externally to clean up after a retry or menu transition.
        /// </summary>
        public void HideUI()
        {
            if (resultsPanel != null)
                resultsPanel.SetActive(false);
            else
                gameObject.SetActive(false); // Fallback if panel not assigned
        }

        private void OnEnable()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose += OnRecentered;
            }
        }

        private void OnDisable()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose -= OnRecentered;
            }
        }

        private void OnRecentered()
        {
            bool isPanelActive = resultsPanel != null ? resultsPanel.activeSelf : gameObject.activeSelf;
            if (isPanelActive)
            {
                Debug.Log("[LevelResultsUI] Headset recentered. Repositioning results panel...");
                StartCoroutine(DelayedRepositionPanel());
            }
        }

        private IEnumerator DelayedRepositionPanel()
        {
            yield return new WaitForSeconds(0.5f);
            RepositionPanel();
        }

        [Header("Positioning Parameters")]
        [Tooltip("Optional: The object that defines the 'True Forward' where balloons spawn. If empty, world forward (Z+) is used to match the HUD.")]
        public Transform roomForwardReference;

        private void RepositionPanel()
        {
            Transform centerEye = Camera.main != null ? Camera.main.transform : null;
            if (centerEye == null)
            {
                var anchor = GameObject.Find("CenterEyeAnchor");
                if (anchor != null) centerEye = anchor.transform;
            }

            if (centerEye != null)
            {
                // Force the UI to spawn in the room's forward direction (where the HUD is)
                // INSTEAD of wherever the player happens to be looking
                Vector3 trueForward = roomForwardReference != null ? roomForwardReference.forward : Vector3.forward;
                
                // Flatten the Y to keep the menu upright but at eye level
                Vector3 forwardFlat = trueForward;
                forwardFlat.y = 0;
                if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward; // edge case
                forwardFlat.Normalize();

                transform.position = centerEye.position + forwardFlat * 1.5f; // 1.5 meters away
                transform.rotation = Quaternion.LookRotation(forwardFlat);
            }
        }

        private void Start()
        {
            if (nextLevelButton  != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            if (retryButton      != null) retryButton.onClick.AddListener(OnRetryClicked);
            if (mainMenuButton   != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        /// <summary>
        /// Displays the results dashboard and animates all UI elements.
        /// Best Streak = highest consecutive waves cleared with ZERO errors (errors reset the streak).
        /// </summary>
        public void DisplayResults(float accuracyPercent, int highestStreak, int starCount, int finalScore, float targetAccuracy, int targetErrors)
        {
            // Show the panel — root object is already active; only the panel was hidden
            if (resultsPanel != null)
                resultsPanel.SetActive(true);
            else
                gameObject.SetActive(true); // Fallback

            // --- Reposition the UI in front of the player's gaze ---
            RepositionPanel();

            // 0. (Removed auto-find logic to preserve custom canvas layouts)
            // The script will now strictly only update text fields explicitly assigned in the Inspector.

            // Update Star Condition Labels dynamically based on difficulty
            if (star1LabelText != null) star1LabelText.text = "Level Complete";
            if (star2LabelText != null) star2LabelText.text = $"≥ {Mathf.RoundToInt(targetAccuracy)}% Accuracy";
            if (star3LabelText != null) star3LabelText.text = $"≤ {targetErrors} Errors";

            // 1. Title (Color is left alone so it uses the user's authored prefab color)
            if (scoreTitleText != null)
            {
                if (starCount > 0)
                {
                    scoreTitleText.text = "LEVEL CLEARED!";
                    
                    // --- PERSISTENT LEVEL PROGRESSION ---
                    // If they cleared this level, unlock the next one (infinite progression)
                    int nextLevel = PopstrikeVR.Core.TemporarySessionData.CurrentLevelIndex + 1;
                    string saveKey = $"PopStrike_Progress_{PopstrikeVR.Core.TemporarySessionData.Difficulty}";
                    
                    int highestSaved = PlayerPrefs.GetInt(saveKey, 1);
                    if (nextLevel > highestSaved)
                    {
                        PlayerPrefs.SetInt(saveKey, nextLevel);
                        PlayerPrefs.Save();
                        Debug.Log($"[LevelResultsUI] Saved persistent progression! {saveKey} is now unlocked up to level {nextLevel}");
                    }
                }
                else
                {
                    scoreTitleText.text = "LEVEL FAILED";
                }
            }

            // 2. Buttons
            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(starCount > 0);
            if (retryButton     != null) retryButton.gameObject.SetActive(starCount == 0);
            if (mainMenuButton  != null) mainMenuButton.gameObject.SetActive(true);

            // 3. Set text values immediately
            if (scoreValueText    != null) scoreValueText.text    = finalScore.ToString("N0");
            if (accuracyValueText != null) accuracyValueText.text = $"{Mathf.RoundToInt(accuracyPercent)}%";
            if (streakValueText   != null) streakValueText.text   = highestStreak.ToString();

            // 4. Reset rings to zero fill before animation
            SetRingFill(scoreRingFill,    0f, ringColorMid);
            SetRingFill(accuracyRingFill, 0f, ringColorMid);
            SetRingFill(streakRingFill,   0f, ringColorMid);

            // 5. Set stars starting scale to 0
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    stars[i].color = starEmptyColor;
                    stars[i].transform.localScale = Vector3.zero;
                }
            }

            // 6. Run all animations in sequence
            StartCoroutine(RunResultsAnimation(accuracyPercent, highestStreak, starCount, finalScore));
        }

        private IEnumerator RunResultsAnimation(float accuracy, int streak, int starCount, int score)
        {
            // Small delay on entry
            yield return new WaitForSeconds(0.3f);

            // Phase 1: Animate the three rings filling simultaneously
            float elapsed = 0f;
            float scoreFillTarget    = Mathf.Clamp01((float)score  / scoreTargetGoal);
            float accuracyFillTarget = Mathf.Clamp01(accuracy / 100f);
            float streakFillTarget   = Mathf.Clamp01((float)streak / streakTargetGoal);

            while (elapsed < ringAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / ringAnimDuration);

                SetRingFill(scoreRingFill,    t * scoreFillTarget,    GetRingColor(scoreFillTarget));
                SetRingFill(accuracyRingFill, t * accuracyFillTarget, GetRingColor(accuracyFillTarget));
                SetRingFill(streakRingFill,   t * streakFillTarget,   GetRingColor(streakFillTarget));

                yield return null;
            }

            // Snap to final values
            SetRingFill(scoreRingFill,    scoreFillTarget,    GetRingColor(scoreFillTarget));
            SetRingFill(accuracyRingFill, accuracyFillTarget, GetRingColor(accuracyFillTarget));
            SetRingFill(streakRingFill,   streakFillTarget,   GetRingColor(streakFillTarget));

            // Phase 2: Animate the stars popping in after rings finish
            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(AnimateStarsRoutine(starCount));
        }

        private void SetRingFill(Image ring, float fill, Color color)
        {
            if (ring == null) return;
            ring.fillAmount = fill;
            ring.color = color;
        }

        private Color GetRingColor(float fillRatio)
        {
            if (fillRatio >= 0.75f) return ringColorHigh;
            if (fillRatio >= 0.4f)  return ringColorMid;
            return ringColorLow;
        }

        private IEnumerator AnimateStarsRoutine(int earnedStars)
        {
            TextMeshProUGUI[] labels = new TextMeshProUGUI[] { star1LabelText, star2LabelText, star3LabelText };

            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;

                stars[i].color = (i < earnedStars) ? starEarnedColor : starEmptyColor;

                if (i < earnedStars && PopstrikeVR.Core.PopstrikeFeedbackManager.Instance != null)
                {
                    PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.PlayTMTAScaleNote(i, true);
                }

                // Bouncy scale-up animation
                float elapsed = 0f;
                while (elapsed < starAnimationDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / starAnimationDuration;
                    float bounceT;
                    if (t < 0.7f) bounceT = t * 1.5f;
                    else          bounceT = 1.0f + Mathf.Sin((t - 0.7f) * Mathf.PI * 3f) * 0.2f * (1f - t);

                    stars[i].transform.localScale = Vector3.LerpUnclamped(Vector3.zero, starOriginalScales[i], bounceT);
                    yield return null;
                }
                
                stars[i].transform.localScale = starOriginalScales[i];

                yield return new WaitForSeconds(0.15f);
            }
        }

        // --- BUTTON EVENTS (Placeholders) ---

        public void OnNextLevelClicked()
        {
            Debug.Log("[LevelResultsUI] Next Level Clicked — progressing to the next level.");
            
            PopstrikeVR.Core.TemporarySessionData.CurrentLevelIndex++;
            PopstrikeVR.Core.TemporarySessionData.IsConfigured = true;
            PopstrikeVR.Core.TemporarySessionData.IsRetry = false;

            StartCoroutine(FadeAndLoadScene(PopstrikeVR.Core.TemporarySessionData.GameSceneName, false));
        }

        public void OnRetryClicked()
        {
            Debug.Log("[LevelResultsUI] Retry — reloading scene.");
            
            // Keep the exact same CSV level index and configuration
            PopstrikeVR.Core.TemporarySessionData.IsConfigured = true;
            PopstrikeVR.Core.TemporarySessionData.IsRetry = true;
            StartCoroutine(FadeAndLoadScene(PopstrikeVR.Core.TemporarySessionData.GameSceneName, false));
        }

        public void OnMainMenuClicked()
        {
            Debug.Log("[LevelResultsUI] Main Menu Clicked — returning to menu configuration.");
            
            PopstrikeVR.Core.TemporarySessionData.IsConfigured = false;
            PopstrikeVR.Core.TemporarySessionData.IsRetry = false;
            StartCoroutine(FadeAndLoadScene(PopstrikeVR.Core.TemporarySessionData.MenuSceneName, true));
        }

        private IEnumerator FadeAndLoadScene(string sceneName, bool isMainMenu)
        {
            if (PopstrikeVR.Visuals.ScreenEffectsController.Instance != null)
            {
                PopstrikeVR.Visuals.ScreenEffectsController.Instance.FadeToBlack(1.0f);
                yield return new WaitForSeconds(1.0f);
            }
            
            if (!string.IsNullOrEmpty(sceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            }
            else
            {
                if (isMainMenu)
                {
                    Debug.LogWarning("[LevelResultsUI] Menu scene name is empty! Loading 'PoPStrikeVRMenu' as a fallback.");
                    UnityEngine.SceneManagement.SceneManager.LoadScene("PoPStrikeVRMenu");
                }
                else
                {
                    string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    Debug.LogWarning($"[LevelResultsUI] Game scene name is empty (testing in Editor). Reloading current scene: {activeScene}");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(activeScene);
                }
            }
        }
    }
}
