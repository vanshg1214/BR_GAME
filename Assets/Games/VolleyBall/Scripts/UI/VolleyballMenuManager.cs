using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Rehab.Volleyball.Core;
using Rehab.Volleyball.Data;
using Rehab.Volleyball.Mechanics;

namespace Rehab.Volleyball.UI
{
    /// <summary>
    /// Controls the main menu selection UI for Volleyball.
    /// Handles Serving Hand (Left, Right), Difficulty (Auto, Easy, Medium, Hard),
    /// and Match Length (11, 21, 25 points).
    /// </summary>
    public class VolleyballMenuManager : MonoBehaviour
    {
        public static VolleyballMenuManager Instance { get; private set; }

        [Header("Menu Panel Container")]
        [Tooltip("The main parent panel GameObject of the menu, containing all buttons.")]
        public GameObject menuPanel;

        [Header("Hand Selection Buttons")]
        public Button leftHandButton;
        public Button rightHandButton;
        public Button bothHandsButton;

        [Header("Difficulty Buttons")]
        public Button easyButton;
        public Button mediumButton;
        public Button hardButton;

        [Header("Match Length Buttons")]
        public Button points15Button;
        public Button points25Button;

        [Header("Audience Settings")]
        [Tooltip("The single button used to toggle the audience on and off.")]
        public Button audienceToggleButton;
        [Tooltip("Text displayed when the audience is ON.")]
        public string audienceOnText = "Audience: ON";
        [Tooltip("Text displayed when the audience is OFF.")]
        public string audienceOffText = "Audience: OFF";

        [Header("Action Buttons")]
        public Button playButton;
        public Button exitButton;

        [Header("Scene Navigation (Optional)")]
        [Tooltip("Name of the scene to load on Play. If left blank, it will start the game in the current scene.")]
        public string gameSceneName = "";
        [Tooltip("Name of the scene to load on Exit. If left blank, it will exit the application.")]
        public string exitSceneName = "";

        [Header("Button Colors")]
        public Color selectedColor = new Color(1f, 0.8f, 0.1f, 1f); // Glowing Yellow
        public Color normalColor = new Color(1f, 1f, 1f, 1f);        // White

        // --- Static Settings for runtime lookup ---
        public static VolleyballRehabProfileSO.HandMode HandMode { get; private set; } = VolleyballRehabProfileSO.HandMode.Both;
        public static VolleyballGameManager.DifficultyMode Difficulty { get; private set; } = VolleyballGameManager.DifficultyMode.Easy;

        public static int PointsToWin { get; private set; } = 15;
        public static bool IsAudienceEnabled { get; private set; } = true;
        
        // Tells the GameManager if we arrived from the Menu (Build) or launched scene directly (Editor)
        public static bool HasStartedFromMenu { get; private set; } = false;

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
            if (leftHandButton != null) leftHandButton.onClick.AddListener(() => SetHandMode(VolleyballRehabProfileSO.HandMode.Left));
            if (rightHandButton != null) rightHandButton.onClick.AddListener(() => SetHandMode(VolleyballRehabProfileSO.HandMode.Right));
            if (bothHandsButton != null) bothHandsButton.onClick.AddListener(() => SetHandMode(VolleyballRehabProfileSO.HandMode.Both));

            if (easyButton != null) easyButton.onClick.AddListener(() => SetDifficulty(VolleyballGameManager.DifficultyMode.Easy));
            if (mediumButton != null) mediumButton.onClick.AddListener(() => SetDifficulty(VolleyballGameManager.DifficultyMode.Medium));
            if (hardButton != null) hardButton.onClick.AddListener(() => SetDifficulty(VolleyballGameManager.DifficultyMode.Hard));
            if (points15Button != null) points15Button.onClick.AddListener(() => SetPointsToWin(15));
            if (points25Button != null) points25Button.onClick.AddListener(() => SetPointsToWin(25));

            if (audienceToggleButton != null) audienceToggleButton.onClick.AddListener(ToggleAudience);

            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);

            // Initialize defaults
            SetHandMode(HandMode);
            SetDifficulty(Difficulty);
            SetPointsToWin(PointsToWin);
            SetAudience(IsAudienceEnabled);
        }

        public void SetHandMode(VolleyballRehabProfileSO.HandMode mode)
        {
            HandMode = mode;
            UpdateHandButtonsUI();
        }

        public void SetDifficulty(VolleyballGameManager.DifficultyMode mode)
        {
            Difficulty = mode;
            UpdateDifficultyButtonsUI();
        }

        public void SetPointsToWin(int points)
        {
            PointsToWin = points;
            UpdatePointsButtonsUI();
        }

        public void SetAudience(bool enabled)
        {
            IsAudienceEnabled = enabled;
            UpdateAudienceButtonsUI();
        }

        public void ToggleAudience()
        {
            SetAudience(!IsAudienceEnabled);
        }

        private void UpdateHandButtonsUI()
        {
            SetButtonColor(leftHandButton, HandMode == VolleyballRehabProfileSO.HandMode.Left);
            SetButtonColor(rightHandButton, HandMode == VolleyballRehabProfileSO.HandMode.Right);
            SetButtonColor(bothHandsButton, HandMode == VolleyballRehabProfileSO.HandMode.Both);
        }

        private void UpdateDifficultyButtonsUI()
        {
            SetButtonColor(easyButton, Difficulty == VolleyballGameManager.DifficultyMode.Easy);
            SetButtonColor(mediumButton, Difficulty == VolleyballGameManager.DifficultyMode.Medium);
            SetButtonColor(hardButton, Difficulty == VolleyballGameManager.DifficultyMode.Hard);
        }

        private void UpdatePointsButtonsUI()
        {
            SetButtonColor(points15Button, PointsToWin == 15);
            SetButtonColor(points25Button, PointsToWin == 25);
        }

        private void UpdateAudienceButtonsUI()
        {
            SetButtonColor(audienceToggleButton, IsAudienceEnabled);
            
            if (audienceToggleButton != null)
            {
                TextMeshProUGUI btnText = audienceToggleButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = IsAudienceEnabled ? audienceOnText : audienceOffText;
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
            Debug.Log("[VolleyballMenuManager] Play Clicked!");
            HasStartedFromMenu = true; // Tell the GameManager we are running a real session!

            // Apply selected settings to the GameManager directly
            if (VolleyballGameManager.Instance != null)
            {
                VolleyballGameManager.Instance.serveSide = HandMode;
                VolleyballGameManager.Instance.difficultyMode = Difficulty;
                VolleyballGameManager.Instance.pointsToWin = PointsToWin;
            }

            // Apply audience setting instantly before the match begins
            if (!IsAudienceEnabled)
            {
                VolleyballAudienceMember[] audienceMembers = FindObjectsOfType<VolleyballAudienceMember>();
                foreach (var member in audienceMembers)
                {
                    member.gameObject.SetActive(false);
                }
                Debug.Log("[VolleyballMenuManager] Audience hidden for this match.");
            }

            if (!string.IsNullOrEmpty(gameSceneName))
            {
                // If a game scene name is specified, load it asynchronously
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                if (VolleyballLevelDirector.Instance != null)
                {
                    VolleyballLevelDirector.Instance.StartLevel();
                }
                else
                {
                    // Otherwise, assume the menu is in the same scene: hide it and start the game!
                    if (menuPanel != null)
                    {
                        menuPanel.SetActive(false);
                    }

                    if (VolleyballGameManager.Instance != null)
                    {
                        VolleyballGameManager.Instance.StartGame();
                    }
                }
            }
        }

        private void OnExitClicked()
        {
            Debug.Log("[VolleyballMenuManager] Exit Clicked");
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
