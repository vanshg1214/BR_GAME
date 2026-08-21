using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

namespace ArcRoll.UI
{
    /// <summary>
    /// Controls the main menu selection UI for ArcRoll.
    /// Handles Hand Mode (Left, Right, Both), Session Duration (3, 5, 7 minutes),
    /// and Difficulty (Easy, Medium, Hard) controlling aim assist strength.
    /// </summary>
    public class ArcRollMenuManager : MonoBehaviour
    {
        public static ArcRollMenuManager Instance { get; private set; }

        [Header("Menu Panel Container")]
        [Tooltip("The main parent panel GameObject of the menu, containing all buttons.")]
        public GameObject menuPanel;

        [Header("Hand Selection Buttons")]
        public Button leftHandButton;
        public Button bothHandsButton;
        public Button rightHandButton;

        [Header("Difficulty Buttons")]
        public Button easyButton;
        public Button mediumButton;
        public Button hardButton;

        [Header("Session Duration Buttons")]
        public Button threeMinButton;
        public Button fiveMinButton;
        public Button sevenMinButton;

        [Header("Action Buttons")]
        public Button playButton;
        public Button exitButton;

        [Header("Scene Navigation (Optional)")]
        [Tooltip("Name of the scene to load on Play. If left blank, it will start the game in the current scene.")]
        public string gameSceneName = "";
        [Tooltip("Name of the scene to load on Exit. If left blank, it will exit the application.")]
        public string exitSceneName = "";

        [Header("Accessibility Settings")]
        public Button autoGrabButton;

        [Header("Button Colors")]
        public Color selectedColor = new Color(1f, 0.8f, 0.1f, 1f); // Glowing Yellow
        public Color normalColor = new Color(1f, 1f, 1f, 1f);        // White

        // --- Static Settings for runtime lookup by Frisbees/Balls/Managers ---
        public static string HandMode { get; private set; } = "Right";       // "Left", "Right"
        public static string Difficulty { get; private set; } = "Medium";    // "Easy", "Medium", "Hard"
        public static float SessionDuration { get; private set; } = 180f;     // Default 3 minutes
        public static float AimAssistStrength { get; private set; } = 0.5f;   // Easy=0.8, Medium=0.5, Hard=0.2
        public static bool IsAutoGrabMode { get; private set; } = true; // Auto-Grab Accessibility Mode

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Register Button Click Listeners
            if (leftHandButton != null) leftHandButton.onClick.AddListener(() => SetHandMode("Left"));
            if (rightHandButton != null) rightHandButton.onClick.AddListener(() => SetHandMode("Right"));

            if (bothHandsButton != null)
            {
                bothHandsButton.gameObject.SetActive(false); // Hide the Both hands button, game is single hand only
            }

            if (easyButton != null) easyButton.onClick.AddListener(() => SetDifficulty("Easy"));
            if (mediumButton != null) mediumButton.onClick.AddListener(() => SetDifficulty("Medium"));
            if (hardButton != null) hardButton.onClick.AddListener(() => SetDifficulty("Hard"));

            if (threeMinButton != null) threeMinButton.onClick.AddListener(() => SetDuration(180f));
            if (fiveMinButton != null) fiveMinButton.onClick.AddListener(() => SetDuration(300f));
            if (sevenMinButton != null) sevenMinButton.onClick.AddListener(() => SetDuration(420f));

            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);

            if (autoGrabButton != null)
            {
                autoGrabButton.onClick.AddListener(ToggleAutoGrabMode);
            }

            // Initialize defaults
            SetHandMode(HandMode);
            SetDifficulty(Difficulty);
            SetDuration(SessionDuration);
            UpdateAutoGrabButtonUI();
        }

        private void Update()
        {
            // Debug hotkey to toggle Auto-Grab in Editor
            if (Input.GetKeyDown(KeyCode.A))
            {
                ToggleAutoGrabMode();
            }
        }

        public void SetHandMode(string mode)
        {
            HandMode = mode;
            UpdateHandButtonsUI();
        }

        public void SetDifficulty(string diff)
        {
            Difficulty = diff;
            
            // Adjust aim assist values according to difficulty settings
            if (diff == "Easy") AimAssistStrength = 0.8f;
            else if (diff == "Medium") AimAssistStrength = 0.5f;
            else if (diff == "Hard") AimAssistStrength = 0.2f;

            UpdateDifficultyButtonsUI();
        }

        public void SetAutoGrabMode(bool isEnabled)
        {
            IsAutoGrabMode = isEnabled;
            UpdateAutoGrabButtonUI();
            Debug.Log($"[ArcRollMenuManager] Auto-Grab Mode set to {isEnabled} via Menu UI.");
        }

        public void ToggleAutoGrabMode()
        {
            SetAutoGrabMode(!IsAutoGrabMode);
        }

        public void SetDuration(float durationInSeconds)
        {
            SessionDuration = durationInSeconds;
            UpdateDurationButtonsUI();
        }

        private void UpdateHandButtonsUI()
        {
            SetButtonColor(leftHandButton, HandMode == "Left");
            SetButtonColor(bothHandsButton, HandMode == "Both");
            SetButtonColor(rightHandButton, HandMode == "Right");
        }

        private void UpdateDifficultyButtonsUI()
        {
            SetButtonColor(easyButton, Difficulty == "Easy");
            SetButtonColor(mediumButton, Difficulty == "Medium");
            SetButtonColor(hardButton, Difficulty == "Hard");
        }

        private void UpdateDurationButtonsUI()
        {
            SetButtonColor(threeMinButton, Mathf.Approximately(SessionDuration, 180f));
            SetButtonColor(fiveMinButton, Mathf.Approximately(SessionDuration, 300f));
            SetButtonColor(sevenMinButton, Mathf.Approximately(SessionDuration, 420f));
        }

        private void UpdateAutoGrabButtonUI()
        {
            SetButtonColor(autoGrabButton, IsAutoGrabMode);
            
            if (autoGrabButton != null)
            {
                TextMeshProUGUI btnText = autoGrabButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = IsAutoGrabMode ? "Auto-Grab: ON" : "Auto-Grab: OFF";
                }
            }
        }

        private void SetButtonColor(Button button, bool isSelected)
        {
            if (button == null) return;
            Image img = button.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSelected ? selectedColor : normalColor;
            }
        }

        private void OnPlayClicked()
        {
            Debug.Log("[ArcRollMenuManager] Play Clicked");

            // Apply selected duration to the GameManager directly
            if (ArcRoll.Core.ArcRollGameManager.Instance != null)
            {
                ArcRoll.Core.ArcRollGameManager.Instance.gameDuration = SessionDuration;
            }

            if (!string.IsNullOrEmpty(gameSceneName))
            {
                // If a game scene name is specified, load it asynchronously
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                // Otherwise, assume the menu is in the same scene: hide it and start the game!
                if (menuPanel != null)
                {
                    menuPanel.SetActive(false);
                }

                if (ArcRoll.Core.ArcRollGameManager.Instance != null)
                {
                    ArcRoll.Core.ArcRollGameManager.Instance.StartGame();
                }
            }
        }

        private void OnExitClicked()
        {
            Debug.Log("[ArcRollMenuManager] Exit Clicked");
            if (!string.IsNullOrEmpty(exitSceneName))
            {
                SceneManager.LoadScene(exitSceneName);
            }
            else
            {
                Application.Quit();
            }
        }
    }
}
