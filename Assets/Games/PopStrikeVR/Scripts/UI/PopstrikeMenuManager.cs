using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

namespace PopstrikeVR.UI
{
    /// <summary>
    /// Controls the main menu selection UI. 
    /// Manages the pill-toggle highlighting for Hand Mode, Difficulty, and Duration.
    /// Stores the settings inside TemporarySessionData and triggers PopstrikeLevelDirector on Play.
    /// </summary>
    public class PopstrikeMenuManager : MonoBehaviour
    {
        public static PopstrikeMenuManager Instance { get; private set; }

        [Header("Menu Panel Container")]
        [Tooltip("The main parent panel GameObject of the menu, containing all buttons. Will be hidden when game starts.")]
        public GameObject menuPanel;

        [Header("Hand Tracking Buttons")]
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

        [Header("Accessibility & Environment")]
        [Tooltip("If checked, Night scene is loaded initially and the button is active. If unchecked, Morning is loaded initially and the button is inactive.")]
        public bool defaultToNightScene = true;
        public Button disableGesturesButton;
        public Button environmentToggleButton;

        [Header("Action Buttons")]
        public Button playButton;
        public Button exitButton;

        [Header("Scene Navigation")]
        [Tooltip("The name of the Night scene to load when Play is clicked (e.g. PopStrikeVRGameScene).")]
        public string gameSceneName = "";
        [Tooltip("The name of the Morning scene to load when Play is clicked (e.g. PopStrikeVRMorningScene).")]
        public string morningSceneName = "PopStrikeVRMorningScene";
        [Tooltip("The name of the Menu/Hub scene to load when Exit is clicked. Leave blank to quit app.")]
        public string exitSceneName = "";

        [Header("Button Visual Feedback")]
        [Tooltip("The color used for the selected option (e.g., a bright glowing yellow).")]
        public Color selectedColor = new Color(1f, 0.8f, 0.1f, 1.0f); // Bright Yellow
        [Tooltip("The color used for unselected options.")]
        public Color normalColor = new Color(1.0f, 1.0f, 1.0f, 1.0f); // Pure White

        // Cached selections
        private PopstrikeVR.Core.HandTrackingMode selectedHandMode;
        private string selectedDifficulty;
        private PopstrikeVR.Core.SessionDuration selectedDuration;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            
            // Set the default environment based on the Inspector checkbox
            PopstrikeVR.Core.TemporarySessionData.IsMorningScene = !defaultToNightScene;
        }

        private void Start()
        {
            // Bind listeners
            if (leftHandButton   != null) leftHandButton.onClick.AddListener(() => SetHandMode(PopstrikeVR.Core.HandTrackingMode.LeftHandOnly));
            if (bothHandsButton  != null) bothHandsButton.onClick.AddListener(() => SetHandMode(PopstrikeVR.Core.HandTrackingMode.BothHands));
            if (rightHandButton  != null) rightHandButton.onClick.AddListener(() => SetHandMode(PopstrikeVR.Core.HandTrackingMode.RightHandOnly));

            if (easyButton       != null) easyButton.onClick.AddListener(() => SetDifficulty("Easy"));
            if (mediumButton     != null) mediumButton.onClick.AddListener(() => SetDifficulty("Medium"));
            if (hardButton       != null) hardButton.onClick.AddListener(() => SetDifficulty("Hard"));

            if (threeMinButton   != null) threeMinButton.onClick.AddListener(() => SetDuration(PopstrikeVR.Core.SessionDuration.ThreeMinutes));
            if (fiveMinButton    != null) fiveMinButton.onClick.AddListener(() => SetDuration(PopstrikeVR.Core.SessionDuration.FiveMinutes));

            if (disableGesturesButton != null) disableGesturesButton.onClick.AddListener(ToggleDisableGestures);
            if (environmentToggleButton != null) environmentToggleButton.onClick.AddListener(ToggleEnvironment);

            if (playButton       != null) playButton.onClick.AddListener(OnPlayClicked);
            if (exitButton       != null) exitButton.onClick.AddListener(OnExitClicked);

            // Restore from temporary storage
            RestoreSelections();
        }

        private void RestoreSelections()
        {
            selectedHandMode = PopstrikeVR.Core.TemporarySessionData.HandMode;
            selectedDifficulty = PopstrikeVR.Core.TemporarySessionData.Difficulty;
            selectedDuration = PopstrikeVR.Core.TemporarySessionData.Duration;

            UpdateHandModeUI();
            UpdateDifficultyUI();
            UpdateDurationUI();
            UpdateDisableGesturesUI();
            UpdateEnvironmentUI();
        }

        private void SetHandMode(PopstrikeVR.Core.HandTrackingMode mode)
        {
            selectedHandMode = mode;
            PopstrikeVR.Core.TemporarySessionData.HandMode = mode;
            UpdateHandModeUI();
        }

        private void SetDifficulty(string difficulty)
        {
            selectedDifficulty = difficulty;
            PopstrikeVR.Core.TemporarySessionData.Difficulty = difficulty;
            UpdateDifficultyUI();
        }

        private void SetDuration(PopstrikeVR.Core.SessionDuration duration)
        {
            selectedDuration = duration;
            PopstrikeVR.Core.TemporarySessionData.Duration = duration;
            UpdateDurationUI();
        }

        private void ToggleDisableGestures()
        {
            bool currentState = PopstrikeVR.Core.TemporarySessionData.DisableGestures;
            PopstrikeVR.Core.TemporarySessionData.DisableGestures = !currentState;
            UpdateDisableGesturesUI();
        }

        private void ToggleEnvironment()
        {
            bool currentState = PopstrikeVR.Core.TemporarySessionData.IsMorningScene;
            PopstrikeVR.Core.TemporarySessionData.IsMorningScene = !currentState;
            UpdateEnvironmentUI();
        }

        private void UpdateHandModeUI()
        {
            SetButtonVisual(leftHandButton,  selectedHandMode == PopstrikeVR.Core.HandTrackingMode.LeftHandOnly);
            SetButtonVisual(bothHandsButton, selectedHandMode == PopstrikeVR.Core.HandTrackingMode.BothHands);
            SetButtonVisual(rightHandButton, selectedHandMode == PopstrikeVR.Core.HandTrackingMode.RightHandOnly);
        }

        private void UpdateDifficultyUI()
        {
            SetButtonVisual(easyButton,   selectedDifficulty == "Easy");
            SetButtonVisual(mediumButton, selectedDifficulty == "Medium");
            SetButtonVisual(hardButton,   selectedDifficulty == "Hard");
        }

        private void UpdateDurationUI()
        {
            SetButtonVisual(threeMinButton, selectedDuration == PopstrikeVR.Core.SessionDuration.ThreeMinutes);
            SetButtonVisual(fiveMinButton,  selectedDuration == PopstrikeVR.Core.SessionDuration.FiveMinutes);
        }

        private void UpdateDisableGesturesUI()
        {
            if (disableGesturesButton == null) return;
            
            bool isDisabled = PopstrikeVR.Core.TemporarySessionData.DisableGestures;
            bool gesturesAreOn = !isDisabled;

            // Change Text
            TMP_Text txt = disableGesturesButton.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.text = gesturesAreOn ? "Gesture : ON" : "Gesture : OFF";
            }
            else
            {
                // Fallback: update normal text
                var textObj = disableGesturesButton.GetComponentInChildren<TextMeshProUGUI>();
                if (textObj != null) textObj.text = gesturesAreOn ? "GESTURES: ON" : "GESTURES: OFF";
            }
            
            // Highlight button correctly based on state
            SetButtonVisual(disableGesturesButton, gesturesAreOn);
        }

        private void UpdateEnvironmentUI()
        {
            if (environmentToggleButton == null) return;
            
            bool isMorning = PopstrikeVR.Core.TemporarySessionData.IsMorningScene;

            var textObj = environmentToggleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (textObj != null) textObj.text = isMorning ? "ENV : MORN" : "ENV : NIGHT";
            
            // User requested: Night = Selected Color, Morning = Deselected Color
            SetButtonVisual(environmentToggleButton, !isMorning);
        }

        private void SetButtonVisual(Button button, bool isSelected)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = isSelected ? selectedColor : normalColor;
            }
        }

        private void OnPlayClicked()
        {
            Debug.Log($"[MenuManager] Play Clicked! Attempting to load Game Scene: {gameSceneName}");
            
            // Store scene names in temporary data for the Results UI to use later
            PopstrikeVR.Core.TemporarySessionData.MenuSceneName = SceneManager.GetActiveScene().name;
            PopstrikeVR.Core.TemporarySessionData.GameSceneName = gameSceneName;
            
            PopstrikeVR.Core.TemporarySessionData.IsConfigured = true;
            
            // --- LOAD PERSISTENT LEVEL PROGRESSION ---
            string saveKey = $"PopStrike_Progress_{selectedDifficulty}";
            PopstrikeVR.Core.TemporarySessionData.CurrentLevelIndex = PlayerPrefs.GetInt(saveKey, 1);
            Debug.Log($"[MenuManager] Loaded Persistent Progression. Starting {selectedDifficulty} at Level {PopstrikeVR.Core.TemporarySessionData.CurrentLevelIndex}");
            PopstrikeVR.Core.TemporarySessionData.GenerateLevelSequence();

            string sceneToLoad = PopstrikeVR.Core.TemporarySessionData.IsMorningScene ? morningSceneName : gameSceneName;
            StartCoroutine(FadeAndLoadScene(sceneToLoad));
        }

        private void OnExitClicked()
        {
            if (!string.IsNullOrEmpty(exitSceneName))
            {
                StartCoroutine(FadeAndLoadScene(exitSceneName));
            }
            else
            {
                Debug.Log("[MenuManager] Exit clicked - quitting application.");
                Application.Quit();
            }
        }

        private IEnumerator FadeAndLoadScene(string sceneName)
        {
            Debug.Log($"[MenuManager] Starting FadeAndLoadScene for: {sceneName}");
            
            // Instead of SetActive(false) which kills the coroutine, we disable the Canvas component 
            // so the GameObject stays active and the Coroutine finishes executing!
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = false;
            
            // Trigger Fade to Black if ScreenEffectsController exists
            if (PopstrikeVR.Visuals.ScreenEffectsController.Instance != null)
            {
                Debug.Log("[MenuManager] Triggering FadeToBlack...");
                PopstrikeVR.Visuals.ScreenEffectsController.Instance.FadeToBlack(1.0f);
            }
            else
            {
                Debug.LogWarning("[MenuManager] ScreenEffectsController is missing in this scene! Skipping fade.");
            }
            
            if (!string.IsNullOrEmpty(sceneName))
            {
                Debug.Log($"[MenuManager] Loading Scene Asynchronously: {sceneName}");
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
                if (asyncLoad != null)
                {
                    asyncLoad.allowSceneActivation = false;
                    // Wait for the fade duration (1.0f)
                    yield return new WaitForSeconds(1.0f);
                    asyncLoad.allowSceneActivation = true;
                }
                else
                {
                    // Fallback
                    yield return new WaitForSeconds(1.0f);
                    SceneManager.LoadScene(sceneName);
                }
            }
            else
            {
                Debug.LogError("[MenuManager] Scene name is empty! Cannot load scene.");
            }
        }
    }
}
