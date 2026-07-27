using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhackAMole.UI
{
    public class TherapistDashboardUI : MonoBehaviour, IGameStateListener
    {
        #region Inspector Fields

        [Header("Data Bindings")]
        [SerializeField] private DifficultyProfileSO difficultyProfile;
        [SerializeField] private RehabProfileSO rehabProfile;

        [Header("Patient Config Panel")]
        [SerializeField] private TMP_InputField patientNameInput;
        [SerializeField] private Toggle leftArmToggle;
        [SerializeField] private TextMeshProUGUI armLabel;

        [Header("Session Parameters")]
        [SerializeField] private TMP_Dropdown difficultyDropdown;
        [SerializeField] private Slider sessionDurationSlider;
        [SerializeField] private TextMeshProUGUI durationLabel;
        [SerializeField] private Toggle enableFakeMolesToggle;

        [Header("Range of Motion (ROM) Configurations")]
        [SerializeField] private Slider flexionSlider;
        [SerializeField] private TextMeshProUGUI flexionLabel;
        [SerializeField] private Slider abductionSlider;
        [SerializeField] private TextMeshProUGUI abductionLabel;
        [SerializeField] private Slider armLengthSlider;
        [SerializeField] private TextMeshProUGUI armLengthLabel;

        [Header("Table Calibration Configurations")]
        [SerializeField] private Button recenterBoardButton;

        [Header("Active Interaction Tools")]
        [Tooltip("Automatically populated Left Hand Hammer game objects.")]
        [SerializeField] private List<GameObject> leftHammers = new List<GameObject>();
        [Tooltip("Automatically populated Right Hand Hammer game objects.")]
        [SerializeField] private List<GameObject> rightHammers = new List<GameObject>();

        [Header("Control Actions")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;

        [Header("Real-time Progress Panels")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI statusText;

        #endregion

        #region Private Fields

        private string patientName = "Sujal";
        private int lastTimerSeconds = -1;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                if (difficultyProfile == null)
                {
                    difficultyProfile = GameManager.Instance.DifficultyProfile;
                }
                if (rehabProfile == null)
                {
                    rehabProfile = GameManager.Instance.RehabProfile;
                }
                GameManager.Instance.RegisterListener(this);
            }

            SetupControls();
        }

        private void Update()
        {
            UpdateTimerDisplay();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterListener(this);
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= OnScoreUpdated;
            }
        }

        #endregion

        #region Control Setup & Auto-Discovery

        private void SetupControls()
        {
            // Patient Setup
            if (patientNameInput != null && rehabProfile != null)
            {
                patientName = rehabProfile.patientName;
                patientNameInput.text = patientName;
                patientNameInput.onEndEdit.AddListener(OnPatientNameChanged);
            }

            AutoFindHammers();

            if (leftArmToggle != null)
            {
                leftArmToggle.onValueChanged.AddListener(OnArmToggled);
                if (rehabProfile != null && rehabProfile.handMode == RehabProfileSO.HandMode.Both)
                {
                    UpdateHammerStates();
                }
                else
                {
                    leftArmToggle.isOn = rehabProfile != null && rehabProfile.isLeftArm;
                    OnArmToggled(leftArmToggle.isOn);
                }
            }
            else
            {
                UpdateHammerStates();
            }

            // Session setup dropdown
            if (difficultyDropdown != null)
            {
                difficultyDropdown.ClearOptions();
                difficultyDropdown.AddOptions(new List<string> { "Easy", "Medium", "Hard" });
                difficultyDropdown.value = 1; // Default to Medium
                difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
            }

            // Duration setup slider
            if (sessionDurationSlider != null)
            {
                sessionDurationSlider.minValue = 30f;
                sessionDurationSlider.maxValue = 180f;
                sessionDurationSlider.wholeNumbers = true;
                sessionDurationSlider.value = difficultyProfile != null ? difficultyProfile.sessionDuration : 60f;
                sessionDurationSlider.onValueChanged.AddListener(OnDurationChanged);
                UpdateDurationLabel();
            }

            if (enableFakeMolesToggle != null && difficultyProfile != null)
            {
                enableFakeMolesToggle.isOn = difficultyProfile.distractorProbability > 0f;
                enableFakeMolesToggle.onValueChanged.AddListener(OnFakeMolesToggled);
            }

            // Calibration & physical parameters setup
            if (rehabProfile != null)
            {
                InitializeRomSliders();
            }

            // Command actions registration
            if (startButton != null) startButton.onClick.AddListener(OnStartClick);
            if (stopButton != null) stopButton.onClick.AddListener(OnStopClick);

            SetButtonStates(canStart: true, canStop: false);

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += OnScoreUpdated;
                OnScoreUpdated(ScoreManager.Instance.CurrentScore);
            }
        }

        private void AutoFindHammers()
        {
            if (leftHammers.Count > 0 || rightHammers.Count > 0) return;

            // Search scene hierarchy for physical hammer components
            foreach (var hammer in FindObjectsOfType<HandHammer>(true))
            {
                if (hammer.ControllerSide == OVRInput.Controller.LTouch)
                {
                    leftHammers.Add(hammer.gameObject);
                }
                else if (hammer.ControllerSide == OVRInput.Controller.RTouch)
                {
                    rightHammers.Add(hammer.gameObject);
                }
            }
            Debug.Log($"[TherapistDashboard] Configured hammers. Left: {leftHammers.Count}, Right: {rightHammers.Count}");
        }

        private void InitializeRomSliders()
        {
            if (flexionSlider != null)
            {
                flexionSlider.minValue = 10f;
                flexionSlider.maxValue = 180f;
                flexionSlider.value = rehabProfile.maxFlexion;
                flexionSlider.onValueChanged.AddListener(OnFlexionChanged);
                OnFlexionChanged(rehabProfile.maxFlexion);
            }

            if (abductionSlider != null)
            {
                abductionSlider.minValue = 10f;
                abductionSlider.maxValue = 180f;
                abductionSlider.value = rehabProfile.maxAbduction;
                abductionSlider.onValueChanged.AddListener(OnAbductionChanged);
                OnAbductionChanged(rehabProfile.maxAbduction);
            }

            if (armLengthSlider != null)
            {
                armLengthSlider.minValue = 0.3f;
                armLengthSlider.maxValue = 1.2f;
                armLengthSlider.value = rehabProfile.armLength;
                armLengthSlider.onValueChanged.AddListener(OnArmLengthChanged);
                OnArmLengthChanged(rehabProfile.armLength);
            }


            if (recenterBoardButton != null)
            {
                recenterBoardButton.onClick.AddListener(OnRecenterClicked);
            }
        }

        #endregion

        #region Event Callbacks

        private void OnPatientNameChanged(string value)
        {
            patientName = value;
            if (rehabProfile != null)
            {
                rehabProfile.patientName = value;
            }
        }

        private void OnArmToggled(bool isLeft)
        {
            if (rehabProfile != null)
            {
                rehabProfile.handMode = isLeft ? RehabProfileSO.HandMode.Left : RehabProfileSO.HandMode.Right;
                rehabProfile.isLeftArm = isLeft;
            }
            UpdateHammerStates();
        }

        private void UpdateHammerStates()
        {
            if (rehabProfile == null) return;

            bool enableLeft = rehabProfile.handMode == RehabProfileSO.HandMode.Left || rehabProfile.handMode == RehabProfileSO.HandMode.Both;
            bool enableRight = rehabProfile.handMode == RehabProfileSO.HandMode.Right || rehabProfile.handMode == RehabProfileSO.HandMode.Both;

            for (int i = 0; i < leftHammers.Count; i++)
            {
                if (leftHammers[i] != null) leftHammers[i].SetActive(enableLeft);
            }

            for (int i = 0; i < rightHammers.Count; i++)
            {
                if (rightHammers[i] != null) rightHammers[i].SetActive(enableRight);
            }

            if (armLabel != null)
            {
                if (rehabProfile.handMode == RehabProfileSO.HandMode.Left) armLabel.text = "Left Arm";
                else if (rehabProfile.handMode == RehabProfileSO.HandMode.Right) armLabel.text = "Right Arm";
                else armLabel.text = "Both Arms";
            }

            Debug.Log($"[TherapistDashboard] Set active hammers. Left: {enableLeft}, Right: {enableRight} (Mode: {rehabProfile.handMode})");
        }

        private void OnDifficultyChanged(int index)
        {
            if (difficultyProfile == null) return;

            // [FIXED]: The Dashboard UI was permanently overwriting the ScriptableObject's custom settings 
            // the moment the game started! We have disabled this hardcoded override.
            // If you want the Dropdown to swap difficulties, it is better to swap the whole 
            // DifficultyProfileSO reference in the GameManager instead of overwriting the fields of the active one.
            Debug.Log($"[TherapistDashboard] Difficulty Dropdown changed to index {index}. (Hardcoded overrides disabled to protect custom DifficultyProfileSO settings)");
        }

        private void OnDurationChanged(float val)
        {
            if (difficultyProfile != null)
            {
                difficultyProfile.sessionDuration = val;
            }
            UpdateDurationLabel();
        }

        private void OnFakeMolesToggled(bool on)
        {
            if (difficultyProfile != null)
            {
                difficultyProfile.distractorProbability = on ? 0.15f : 0f;
            }
        }

        private void OnFlexionChanged(float val)
        {
            if (rehabProfile != null) rehabProfile.maxFlexion = val;
            if (flexionLabel != null) flexionLabel.text = $"Flexion: {val:F0}°";
            RefreshArcadeTableLayout();
        }

        private void OnAbductionChanged(float val)
        {
            if (rehabProfile != null) rehabProfile.maxAbduction = val;
            if (abductionLabel != null) abductionLabel.text = $"Abduction: {val:F0}°";
            RefreshArcadeTableLayout();
        }

        private void OnArmLengthChanged(float val)
        {
            if (rehabProfile != null) rehabProfile.armLength = val;
            if (armLengthLabel != null) armLengthLabel.text = $"Arm: {val:F2}m";
            RefreshArcadeTableLayout();
        }


        private void OnRecenterClicked()
        {
            TriggerRepositionBoard();
        }

        private void TriggerRepositionBoard()
        {
            if (WorkspaceAutoPositioner.Instance != null)
            {
                WorkspaceAutoPositioner.Instance.RepositionBoard();
            }
            RefreshArcadeTableLayout();
        }

        #endregion

        #region Layout & Display Updates

        private void RefreshArcadeTableLayout()
        {
            var layoutGenerator = FindFirstObjectByType<HoleLayoutGenerator>();
            if (layoutGenerator != null)
            {
                layoutGenerator.GenerateLayout();
            }
        }

        private void UpdateTimerDisplay()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing) return;

            float sessionTime = GameManager.Instance.SessionTimer;
            int totalSec = Mathf.CeilToInt(sessionTime);

            if (totalSec != lastTimerSeconds)
            {
                lastTimerSeconds = totalSec;
                int min = totalSec / 60;
                int sec = totalSec % 60;
                if (timerText != null) timerText.text = $"{min:00}:{sec:00}";
            }
        }

        private void OnScoreUpdated(int newScore)
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score: {newScore}";
            }
        }

        private void UpdateDurationLabel()
        {
            if (durationLabel != null && difficultyProfile != null)
            {
                int durationSec = Mathf.RoundToInt(difficultyProfile.sessionDuration);
                durationLabel.text = $"{durationSec / 60}:{durationSec % 60:00}";
            }
        }

        private void SetStatusText(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
            }
        }

        private void SetButtonStates(bool canStart, bool canStop)
        {
            if (startButton != null) startButton.interactable = canStart;
            if (stopButton != null) stopButton.interactable = canStop;
        }

        #endregion

        #region Session Trigger Handlers

        private void OnStartClick()
        {
            if (patientNameInput != null && rehabProfile != null)
            {
                string textValue = patientNameInput.text;
                patientName = textValue;
                rehabProfile.patientName = textValue;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.UpdateState(GameState.Playing);
            }
        }

        private void OnStopClick()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UpdateState(GameState.Finished);
            }
        }

        #endregion

        #region Game State Listener

        public void OnGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Calibration:
                case GameState.Ready:
                    SetStatusText("Ready");
                    SetButtonStates(canStart: true, canStop: false);
                    break;
                case GameState.Playing:
                    SetStatusText("Session Active");
                    SetButtonStates(canStart: false, canStop: true);
                    break;
                case GameState.Finished:
                    SetStatusText("Session Complete");
                    SetButtonStates(canStart: true, canStop: false);
                    break;
            }
        }

        #endregion
    }
}
