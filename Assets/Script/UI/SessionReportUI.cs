using TMPro;
using UnityEngine;

namespace WhackAMole.UI
{
    public class SessionReportUI : MonoBehaviour, IGameStateListener
    {
        [Header("Metric Text Fields")]
        [SerializeField] private TextMeshProUGUI patientNameText;
        [SerializeField] private TextMeshProUGUI accuracyText;
        [SerializeField] private TextMeshProUGUI totalHitsText;
        [SerializeField] private TextMeshProUGUI fakeHitsText;
        [SerializeField] private TextMeshProUGUI avgVelocityText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI armRepsText;
        [SerializeField] private TextMeshProUGUI motivationText;

        [Header("Gamified Feedback")]
        [SerializeField] private GameObject[] stars = new GameObject[3];

        private bool registered;

        #region Unity Lifecycle

        private void OnEnable()
        {
            RegisterListener();
        }

        private void Start()
        {
            RegisterListener();
        }

        private void OnDisable()
        {
            UnregisterListener();
        }

        private void OnDestroy()
        {
            UnregisterListener();
        }

        #endregion

        #region State Listener

        public void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Finished)
            {
                GenerateReport();
            }
        }

        #endregion

        #region Report Generation

        private void GenerateReport()
        {
            if (ScoreManager.Instance == null) return;

            int hits = ScoreManager.Instance.TotalHits;
            int misses = ScoreManager.Instance.TotalMisses;
            int cognitiveErrors = ScoreManager.Instance.TotalFakeHits;
            float averageSpeed = ScoreManager.Instance.AverageHitVelocity;

            int totalExpectedTargets = hits + misses;
            float accuracyPercentage = totalExpectedTargets > 0 ? ((float)hits / totalExpectedTargets) * 100f : 0f;

            // Populate the fields
            if (patientNameText != null)
            {
                string patientName = GameManager.Instance != null && GameManager.Instance.RehabProfile != null
                    ? GameManager.Instance.RehabProfile.patientName
                    : "Patient";

                if (string.IsNullOrWhiteSpace(patientName))
                {
                    patientName = "Patient";
                }

                patientNameText.text = $"Session Report: {patientName}";
            }

            if (accuracyText != null)
            {
                accuracyText.text = $"Accuracy: {accuracyPercentage:F1}%";
            }

            if (totalHitsText != null)
            {
                totalHitsText.text = $"Total Hits: {hits}";
            }

            if (fakeHitsText != null)
            {
                fakeHitsText.text = $"Cognitive Errors: {cognitiveErrors}";
            }

            if (avgVelocityText != null)
            {
                // Convert m/s to cm/s for user-friendly medical units
                float speedCmS = averageSpeed * 100f;
                avgVelocityText.text = $"Avg Speed: {speedCmS:F0} cm/s";
            }

            // Expose active game score
            int score = ScoreManager.Instance.CurrentScore;
            if (scoreText != null)
            {
                scoreText.text = $"Score: {score}";
            }

            // Expose physical arm reps (swings) completed
            int reps = WhackAMole.Data.ProfileDataLoader.Instance != null 
                ? WhackAMole.Data.ProfileDataLoader.Instance.currentArmReps 
                : hits;
            if (armRepsText != null)
            {
                armRepsText.text = $"Reps: {reps}";
            }

            // Calculate stars based on accuracy
            int starsCount = 0;
            string motivationMsg = "Session Completed!";
            if (totalExpectedTargets > 0)
            {
                if (accuracyPercentage >= 80f)
                {
                    starsCount = 3;
                    motivationMsg = "Perfect Session!";
                }
                else if (accuracyPercentage >= 50f)
                {
                    starsCount = 2;
                    motivationMsg = "Great Effort!";
                }
                else
                {
                    starsCount = 1;
                    motivationMsg = "Keep Practicing!";
                }
            }

            if (motivationText != null)
            {
                motivationText.text = motivationMsg;
            }

            // Activate stars objects in the UI
            if (stars != null)
            {
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] != null)
                    {
                        stars[i].SetActive(i < starsCount);
                    }
                }
            }
        }

        /// <summary>
        /// Restarts the training session. Called directly by UI buttons.
        /// </summary>
        public void RestartSession()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UpdateState(GameState.Playing);
            }
        }

        #endregion

        #region Event Registration Helpers

        private void RegisterListener()
        {
            if (!registered && GameManager.Instance != null)
            {
                GameManager.Instance.RegisterListener(this);
                registered = true;

                if (GameManager.Instance.CurrentState == GameState.Finished)
                {
                    GenerateReport();
                }
            }
        }

        private void UnregisterListener()
        {
            if (registered && GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterListener(this);
                registered = false;
            }
        }

        #endregion
    }
}
