using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using ArcRoll.Core;
using UnityEngine.SceneManagement;

namespace ArcRoll.UI
{
    public class ArcRollEndGameUI : MonoBehaviour
    {
        [Header("Title")]
        public TextMeshProUGUI scoreTitleText; // "Level Cleared!" or "Level Failed"

        [Header("Star Graphics")]
        public Image[] stars = new Image[3];
        public Color starEarnedColor = Color.yellow;
        public Color starEmptyColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public float starAnimationDuration = 0.5f;

        [Header("Left Circle — Score")]
        public TextMeshProUGUI scoreValueText;
        public Image scoreRingFill;
        public int scoreTargetGoal = 10000;

        [Header("Center Circle — Accuracy")]
        public TextMeshProUGUI accuracyValueText;
        public Image accuracyRingFill;

        [Header("Right Circle — Best Streak")]
        public TextMeshProUGUI streakValueText;
        public Image streakRingFill;
        public int streakTargetGoal = 10;

        [Header("Star Condition Labels")]
        public TextMeshProUGUI star1LabelText;
        public TextMeshProUGUI star2LabelText;
        public TextMeshProUGUI star3LabelText;

        [Header("Ring Colors")]
        public Color ringColorLow = new Color(1f, 0.35f, 0.1f, 1f); // Orange-Red
        public Color ringColorMid = new Color(0.1f, 0.9f, 1f, 1f); // Cyan
        public Color ringColorHigh = new Color(0.2f, 1f, 0.4f, 1f); // Neon Green
        public float ringAnimDuration = 0.8f;

        [Header("Buttons")]
        public Button nextLevelButton;
        public Button retryButton;
        public Button mainMenuButton;

        [Header("Panel Control")]
        [Tooltip("Drag the child Panel GameObject here (NOT the root). This is hidden on start and shown at end of level.")]
        public GameObject resultsPanel;

        [Header("Scene Navigation")]
        [Tooltip("The exact name of your Main Menu scene (e.g. 'MainMenu' or 'Menu')")]
        public string mainMenuSceneName = "MainMenu";

        [Header("Positioning Parameters")]
        public Transform roomForwardReference;
        
        [Header("Target Goals")]
        public float targetAccuracy = 75f;
        public int targetMaxErrors = 5;

        private Vector3[] starOriginalScales;
        private string originalStar1Text;
        private string originalStar2Text;
        private string originalStar3Text;

        private struct StarCondition
        {
            public string labelText;
            public bool isEarned;
        }

        private void Awake()
        {
            // Force the star color to bright yellow in case it was accidentally set to gray in the Inspector
            starEarnedColor = new Color(1f, 0.85f, 0f, 1f); 

            // Cache the original scales so we don't blow them up to 1.0 if the user authored them smaller
            starOriginalScales = new Vector3[stars.Length];
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null) starOriginalScales[i] = stars[i].transform.localScale;
                else starOriginalScales[i] = Vector3.one;
            }

            // Bind Buttons
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(PlayAgain);
            if (retryButton != null) retryButton.onClick.AddListener(PlayAgain);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ReturnToMainMenu);

            // Cache original texts and disable word wrapping so they stay strictly on 2 lines
            if (star1LabelText != null)
            {
                star1LabelText.enableWordWrapping = false;
                originalStar1Text = star1LabelText.text;
            }
            if (star2LabelText != null)
            {
                star2LabelText.enableWordWrapping = false;
                originalStar2Text = star2LabelText.text;
            }
            if (star3LabelText != null)
            {
                star3LabelText.enableWordWrapping = false;
                originalStar3Text = star3LabelText.text;
            }

            HideUI();
        }

        public void HideUI()
        {
            if (resultsPanel != null)
                resultsPanel.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            #if OCULUS_INTEGRATION_PRESENT || OVR_SDK
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose += OnRecentered;
            }
            #endif
        }

        private void OnDisable()
        {
            #if OCULUS_INTEGRATION_PRESENT || OVR_SDK
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose -= OnRecentered;
            }
            #endif
        }

        private void OnRecentered()
        {
            bool isPanelActive = resultsPanel != null ? resultsPanel.activeSelf : gameObject.activeSelf;
            if (isPanelActive)
            {
                RepositionPanel();
            }
        }

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
                Vector3 trueForward = roomForwardReference != null ? roomForwardReference.forward : Vector3.forward;
                Vector3 forwardFlat = trueForward;
                forwardFlat.y = 0;
                if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward;
                forwardFlat.Normalize();

                transform.position = centerEye.position + forwardFlat * 1.5f;
                transform.rotation = Quaternion.LookRotation(forwardFlat);
            }
        }

        private void Start()
        {
            if (ArcRollGameManager.Instance != null)
            {
                ArcRollGameManager.Instance.OnGameEnded += ShowEndGameScreen;
            }
        }

        private void OnDestroy()
        {
            if (ArcRollGameManager.Instance != null)
            {
                ArcRollGameManager.Instance.OnGameEnded -= ShowEndGameScreen;
            }
        }

        private void ShowEndGameScreen()
        {
            int finalScore = 0;
            int bestStreak = 0;
            int successful = 0;
            int errors = 0;

            if (ArcRollScoreManager.Instance != null)
            {
                finalScore = ArcRollScoreManager.Instance.currentScore;
                bestStreak = ArcRollScoreManager.Instance.highestStreak;
                successful = ArcRollScoreManager.Instance.successfulThrows;
                errors = ArcRollScoreManager.Instance.totalErrors;
            }

            int totalThrows = successful + errors;
            float accuracyPercent = totalThrows > 0 ? ((float)successful / totalThrows) * 100f : 0f;

            // Calculate Independent Star Conditions
            bool star1Earned = true; // Level Complete
            bool star2Earned = accuracyPercent >= targetAccuracy;
            bool star3Earned = errors <= targetMaxErrors;

            // Sort conditions so earned ones appear first (from left to right)
            var conditions = new System.Collections.Generic.List<StarCondition>()
            {
                new StarCondition { labelText = string.IsNullOrEmpty(originalStar1Text) ? "Level\nComplete" : originalStar1Text, isEarned = star1Earned },
                new StarCondition { labelText = string.IsNullOrEmpty(originalStar2Text) ? $"≥ {Mathf.RoundToInt(targetAccuracy)}%\nAccuracy" : originalStar2Text, isEarned = star2Earned },
                new StarCondition { labelText = string.IsNullOrEmpty(originalStar3Text) ? $"≤ {targetMaxErrors}\nErrors" : originalStar3Text, isEarned = star3Earned }
            };

            var orderedConditions = new System.Collections.Generic.List<StarCondition>();
            foreach (var cond in conditions)
            {
                if (cond.isEarned) orderedConditions.Add(cond);
            }
            foreach (var cond in conditions)
            {
                if (!cond.isEarned) orderedConditions.Add(cond);
            }

            // Update the star labels in order
            if (star1LabelText != null) star1LabelText.text = orderedConditions[0].labelText;
            if (star2LabelText != null) star2LabelText.text = orderedConditions[1].labelText;
            if (star3LabelText != null) star3LabelText.text = orderedConditions[2].labelText;

            // Build array of ordered earned states
            bool[] earnedStarsArray = new bool[] 
            { 
                orderedConditions[0].isEarned, 
                orderedConditions[1].isEarned, 
                orderedConditions[2].isEarned 
            };

            int starCount = (star1Earned ? 1 : 0) + (star2Earned ? 1 : 0) + (star3Earned ? 1 : 0);

            // Activate UI
            if (resultsPanel != null) resultsPanel.SetActive(true);
            else gameObject.SetActive(true);

            RepositionPanel();

            // Title and Buttons
            if (scoreTitleText != null)
            {
                scoreTitleText.text = starCount > 0 ? "LEVEL CLEARED!" : "LEVEL FAILED";
            }

            bool isCleared = starCount > 0;
            if (nextLevelButton != null) nextLevelButton.gameObject.SetActive(isCleared);
            if (retryButton != null) retryButton.gameObject.SetActive(!isCleared);
            if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(true);

            // Immediate text setup
            if (scoreValueText != null) scoreValueText.text = finalScore.ToString("N0");
            if (accuracyValueText != null) accuracyValueText.text = $"{Mathf.RoundToInt(accuracyPercent)}%";
            if (streakValueText != null) streakValueText.text = bestStreak.ToString();

            // Reset rings
            SetRingFill(scoreRingFill, 0f, ringColorMid);
            SetRingFill(accuracyRingFill, 0f, ringColorMid);
            SetRingFill(streakRingFill, 0f, ringColorMid);

            // Reset stars
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    stars[i].color = starEmptyColor;
                    stars[i].transform.localScale = Vector3.zero;
                }
            }

            StartCoroutine(RunResultsAnimation(accuracyPercent, bestStreak, earnedStarsArray, finalScore));
        }

        private IEnumerator RunResultsAnimation(float accuracy, int streak, bool[] earnedStars, int score)
        {
            yield return new WaitForSeconds(0.3f);

            float elapsed = 0f;
            float scoreFillTarget = Mathf.Clamp01((float)score / scoreTargetGoal);
            float accuracyFillTarget = Mathf.Clamp01(accuracy / 100f);
            float streakFillTarget = Mathf.Clamp01((float)streak / streakTargetGoal);

            while (elapsed < ringAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / ringAnimDuration);

                SetRingFill(scoreRingFill, t * scoreFillTarget, GetRingColor(scoreFillTarget));
                SetRingFill(accuracyRingFill, t * accuracyFillTarget, GetRingColor(accuracyFillTarget));
                SetRingFill(streakRingFill, t * streakFillTarget, GetRingColor(streakFillTarget));

                yield return null;
            }

            SetRingFill(scoreRingFill, scoreFillTarget, GetRingColor(scoreFillTarget));
            SetRingFill(accuracyRingFill, accuracyFillTarget, GetRingColor(accuracyFillTarget));
            SetRingFill(streakRingFill, streakFillTarget, GetRingColor(streakFillTarget));

            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(AnimateStarsRoutine(earnedStars));
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
            if (fillRatio >= 0.4f) return ringColorMid;
            return ringColorLow;
        }

        private IEnumerator AnimateStarsRoutine(bool[] earnedStars)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;

                // Color is applied independently based on whether THIS SPECIFIC star's condition was met!
                stars[i].color = earnedStars[i] ? starEarnedColor : starEmptyColor;

                float elapsed = 0f;
                while (elapsed < starAnimationDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / starAnimationDuration;
                    float bounceT;
                    if (t < 0.7f) bounceT = t * 1.5f;
                    else bounceT = 1.0f + Mathf.Sin((t - 0.7f) * Mathf.PI * 3f) * 0.2f * (1f - t);

                    stars[i].transform.localScale = Vector3.LerpUnclamped(Vector3.zero, starOriginalScales[i], bounceT);
                    yield return null;
                }
                
                stars[i].transform.localScale = starOriginalScales[i];
                yield return new WaitForSeconds(0.15f);
            }
        }

        public void PlayAgain()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMainMenu()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                Debug.LogWarning("[ArcRollEndGameUI] No Main Menu Scene Name assigned in the Inspector!");
            }
        }
    }
}
