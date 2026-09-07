using System.Reflection;
using TMPro;
using UnityEngine;

namespace WhackAMole.UI
{
    public class UIMetricsDisplay : MonoBehaviour, IGameStateListener
    {
        #region Inspector Fields

        [Header("Telemetry Displays")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private TextMeshProUGUI missesText;

        [Header("Performance Optimizations")]
        [Tooltip("The OVROverlay Canvas script in the scene. Refreshes manually to optimize Quest performance.")]
        [SerializeField] private MonoBehaviour ovrOverlayCanvas;

        #endregion

        #region Private Fields

        private int lastSeconds = -1;
        private bool isDirty;
        private MethodInfo redrawMethod;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Debug active graphics device in editor
            Debug.Log($"<color=cyan>[GPU Target] Render Hardware: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})</color>");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterListener(this);
                OnGameStateChanged(GameManager.Instance.CurrentState);
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
                ScoreManager.Instance.OnComboChanged += OnComboChanged;
                ScoreManager.Instance.OnMissRegistered += OnMissRegistered;
            }

            // Cache reflection method lookup for MarkDirty to avoid runtime compilation dependencies on Oculus SDK
            if (ovrOverlayCanvas != null && ovrOverlayCanvas.enabled)
            {
                redrawMethod = ovrOverlayCanvas.GetType().GetMethod("MarkDirty")
                            ?? ovrOverlayCanvas.GetType().GetMethod("SetDirty");
            }
        }

        private bool isSubscribedToDirector = false;

        private void Update()
        {
            if (!isSubscribedToDirector && WhackAMoleLevelDirector.Instance != null)
            {
                WhackAMoleLevelDirector.Instance.OnRoundTimerUpdated += OnRoundTimerUpdated;
                WhackAMoleLevelDirector.Instance.OnBreakTimerUpdated += OnBreakTimerUpdated;
                isSubscribedToDirector = true;
            }
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
            {
                // If there's no Level Director, fallback to the basic GameManager SessionTimer
                if (WhackAMoleLevelDirector.Instance == null)
                {
                    UpdateTimerText(GameManager.Instance.SessionTimer);
                }
            }

            if (isDirty)
            {
                isDirty = false;
                RequestOverlayRedraw();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterListener(this);
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
                ScoreManager.Instance.OnComboChanged -= OnComboChanged;
                ScoreManager.Instance.OnMissRegistered -= OnMissRegistered;
            }

            if (WhackAMoleLevelDirector.Instance != null)
            {
                WhackAMoleLevelDirector.Instance.OnRoundTimerUpdated -= OnRoundTimerUpdated;
                WhackAMoleLevelDirector.Instance.OnBreakTimerUpdated -= OnBreakTimerUpdated;
            }
        }

        private void OnRoundTimerUpdated(float timeRemaining, int roundIndex)
        {
            UpdateTimerText(timeRemaining);
        }

        private void OnBreakTimerUpdated(float timeRemaining)
        {
            UpdateTimerText(timeRemaining);
        }

        private void UpdateTimerText(float timeRemaining)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);

            // Reduce redundant text allocations by checking if seconds changed
            if (seconds != lastSeconds)
            {
                lastSeconds = seconds;
                if (timerText != null)
                {
                    timerText.text = $"{minutes:00}:{seconds:00}";
                }
                isDirty = true;
            }
        }

        #endregion

        #region Score & Telemetry Updates

        private void OnScoreChanged(int newScore)
        {
            if (scoreText != null)
            {
                scoreText.text = $"{newScore}";
            }
            isDirty = true;
        }

        private void OnMissRegistered()
        {
            if (missesText != null && ScoreManager.Instance != null)
            {
                missesText.text = $"{ScoreManager.Instance.TotalMisses}";
            }
            isDirty = true;
        }

        private void OnComboChanged(int combo, float mult)
        {
            if (comboText != null)
            {
                if (combo >= 3)
                {
                    comboText.text = $"{mult:F1}x";
                }
                else if (combo == 2)
                {
                    comboText.text = "Hit 1 more!";
                }
                else if (combo == 1)
                {
                    comboText.text = "Hit 2 more!";
                }
                else
                {
                    comboText.text = "Hit 3 more!";
                }
            }
            isDirty = true;
        }

        #endregion

        #region Game State Listener

        public void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Calibration || newState == GameState.Ready || newState == GameState.Playing)
            {
                lastSeconds = -1;
                
                // CRITICAL FIX: Pull current values from ScoreManager instead of hardcoding 0.
                // This prevents the UI from resetting to 0 when unpausing the game!
                if (ScoreManager.Instance != null)
                {
                    OnScoreChanged(ScoreManager.Instance.CurrentScore);
                    OnComboChanged(ScoreManager.Instance.CurrentCombo, ScoreManager.Instance.ScoreMultiplier);
                    if (missesText != null) missesText.text = $"{ScoreManager.Instance.TotalMisses}";
                }
            }
            isDirty = true;
        }

        #endregion

        #region Redraw Methods

        public void MarkDirty()
        {
            isDirty = true;
        }

        private void RequestOverlayRedraw()
        {
            if (redrawMethod != null && ovrOverlayCanvas != null)
            {
                redrawMethod.Invoke(ovrOverlayCanvas, null);
            }
        }

        #endregion
    }
}
