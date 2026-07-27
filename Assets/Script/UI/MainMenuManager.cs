using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace WhackAMole.UI
{
    /// <summary>
    /// Handles the main menu UI interactions in MenuScene, including hand selection,
    /// scene transition to gameplay (Play), and exiting to a customizable scene (Exit).
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Scene Navigation")]
        [Tooltip("The name of the gameplay scene to load.")]
        [SerializeField] private string playSceneName = "GameScene";

        [Tooltip("Type the name of the scene you want to load when clicking Exit. If left blank, it will exit/quit the application.")]
        [SerializeField] private string exitSceneName = "";

        [Header("Hand Selection Buttons")]
        [SerializeField] private Button leftHandButton;
        [SerializeField] private Button bothHandsButton;
        [SerializeField] private Button rightHandButton;

        [Header("Action Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;

        [Header("Button Visual Feedback")]
        [Tooltip("Color tint when a hand mode is selected.")]
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.8f, 0.2f, 1.0f); // Bright yellow highlight

        [Tooltip("Default color tint for unselected buttons.")]
        [SerializeField] private Color normalColor = Color.white;

        [Header("Profile Bindings")]
        [Tooltip("The RehabProfile ScriptableObject used to pass the selected hand mode to the GameScene.")]
        [SerializeField] private RehabProfileSO rehabProfile;

        [Header("Difficulty Profile Binding")]
        [Tooltip("The DifficultyProfile ScriptableObject used to pass duration and spawning settings to the GameScene.")]
        [SerializeField] private DifficultyProfileSO difficultyProfile;

        [Header("Duration Buttons")]
        [SerializeField] private Button duration1MinButton;
        [SerializeField] private Button duration3MinButton;
        [SerializeField] private Button duration5MinButton;

        [Header("Toggle Buttons")]
        [SerializeField] private Button fakeMoleToggleButton;
        [SerializeField] private Button flyMoleToggleButton;

        [Header("Toggle Button Custom Texts")]
        [SerializeField] private string fakeMoleOnText = "Fake Mole: ON";
        [SerializeField] private string fakeMoleOffText = "Fake Mole: OFF";
        [SerializeField] private string flyMoleOnText = "Fly Mole: ON";
        [SerializeField] private string flyMoleOffText = "Fly Mole: OFF";

        private bool isFakeMoleOn = false;
        private bool isFlyMoleOn = true;

        private void Start()
        {
            // Auto-assign rehab profile if not set in Inspector
            if (rehabProfile == null && GameManager.Instance != null)
            {
                rehabProfile = GameManager.Instance.RehabProfile;
            }
            if (rehabProfile == null)
            {
                rehabProfile = Resources.Load<RehabProfileSO>("RehabProfile");
            }

            // Auto-assign difficulty profile if not set in Inspector
            if (difficultyProfile == null && GameManager.Instance != null)
            {
                difficultyProfile = GameManager.Instance.DifficultyProfile;
            }
            if (difficultyProfile == null)
            {
                difficultyProfile = Resources.Load<DifficultyProfileSO>("DifficultyProfile");
            }

            // Deactivate all hammers in the Main Menu scene so the user only sees their controllers/hands
            foreach (var hammer in FindObjectsOfType<HandHammer>(true))
            {
                hammer.gameObject.SetActive(false);
            }

            // Register button click events
            if (leftHandButton != null)
            {
                leftHandButton.onClick.AddListener(() => SelectHandMode(RehabProfileSO.HandMode.Left));
            }
            if (bothHandsButton != null)
            {
                bothHandsButton.onClick.AddListener(() => SelectHandMode(RehabProfileSO.HandMode.Both));
            }
            if (rightHandButton != null)
            {
                rightHandButton.onClick.AddListener(() => SelectHandMode(RehabProfileSO.HandMode.Right));
            }

            // Duration Button Listeners
            if (duration1MinButton != null) duration1MinButton.onClick.AddListener(() => SetDuration(60f));
            if (duration3MinButton != null) duration3MinButton.onClick.AddListener(() => SetDuration(180f));
            if (duration5MinButton != null) duration5MinButton.onClick.AddListener(() => SetDuration(300f));

            // Toggle Button Listeners
            if (fakeMoleToggleButton != null) fakeMoleToggleButton.onClick.AddListener(ToggleFakeMole);
            if (flyMoleToggleButton != null) flyMoleToggleButton.onClick.AddListener(ToggleFlyMole);

            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
            }
            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitClicked);
            }

            // Initialize button highlighting based on stored selection
            if (rehabProfile != null)
            {
                UpdateVisualSelection(rehabProfile.handMode);
            }

            // Initialize difficulty-based selections with FORCED defaults:
            // Fake Mole = OFF (distractor disabled), Fly Mole = ON (bird enabled)
            isFakeMoleOn = false;
            isFlyMoleOn = true;

            if (difficultyProfile != null)
            {
                // Force defaults into the ScriptableObject so gameplay picks them up
                difficultyProfile.distractorProbability = 0f;    // Fake mole OFF
                difficultyProfile.birdSpawnProbability = 0.5f;   // Fly mole ON

                UpdateDurationVisuals(difficultyProfile.sessionDuration);
                UpdateToggleVisuals();
            }
        }

        private void SelectHandMode(RehabProfileSO.HandMode mode)
        {
            if (rehabProfile != null)
            {
                rehabProfile.handMode = mode;
                
                // Maintain legacy/fallback values for scripts checking isLeftArm
                rehabProfile.isLeftArm = (mode == RehabProfileSO.HandMode.Left);

                // If ProfileDataLoader is present in the scene, sync it immediately
                if (Data.ProfileDataLoader.Instance != null)
                {
                    Data.ProfileDataLoader.Instance.useLeftArm = rehabProfile.isLeftArm;
                }
            }

            UpdateVisualSelection(mode);
            Debug.Log($"[MainMenuManager] Selected Hand Mode: {mode}");
        }

        private void UpdateVisualSelection(RehabProfileSO.HandMode mode)
        {
            SetButtonHighlight(leftHandButton, mode == RehabProfileSO.HandMode.Left);
            SetButtonHighlight(bothHandsButton, mode == RehabProfileSO.HandMode.Both);
            SetButtonHighlight(rightHandButton, mode == RehabProfileSO.HandMode.Right);
        }

        private void SetButtonHighlight(Button button, bool isSelected)
        {
            if (button == null) return;

            // Update button graphic color tint
            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = isSelected ? selectedColor : normalColor;
            }
        }

        private void SetDuration(float seconds)
        {
            if (difficultyProfile != null)
            {
                difficultyProfile.sessionDuration = seconds;
                Debug.Log($"[MainMenuManager] Set Session Duration to: {seconds} seconds");
            }
            UpdateDurationVisuals(seconds);
        }

        private void UpdateDurationVisuals(float seconds)
        {
            SetButtonHighlight(duration1MinButton, seconds <= 90f);
            SetButtonHighlight(duration3MinButton, seconds > 90f && seconds <= 240f);
            SetButtonHighlight(duration5MinButton, seconds > 240f);
        }

        private void ToggleFakeMole()
        {
            isFakeMoleOn = !isFakeMoleOn;
            if (difficultyProfile != null)
            {
                difficultyProfile.distractorProbability = isFakeMoleOn ? 0.15f : 0f;
                Debug.Log($"[MainMenuManager] Set Fake Mole Probability to: {difficultyProfile.distractorProbability}");
            }
            UpdateToggleVisuals();
        }

        private void ToggleFlyMole()
        {
            isFlyMoleOn = !isFlyMoleOn;
            if (difficultyProfile != null)
            {
                difficultyProfile.birdSpawnProbability = isFlyMoleOn ? 0.5f : 0f;
                Debug.Log($"[MainMenuManager] Set Fly Mole Probability to: {difficultyProfile.birdSpawnProbability}");
            }
            UpdateToggleVisuals();
        }

        private void UpdateToggleVisuals()
        {
            if (fakeMoleToggleButton != null)
            {
                SetToggleText(fakeMoleToggleButton, isFakeMoleOn ? fakeMoleOnText : fakeMoleOffText);
                SetButtonHighlight(fakeMoleToggleButton, isFakeMoleOn);
            }
            if (flyMoleToggleButton != null)
            {
                SetToggleText(flyMoleToggleButton, isFlyMoleOn ? flyMoleOnText : flyMoleOffText);
                SetButtonHighlight(flyMoleToggleButton, isFlyMoleOn);
            }
        }

        private void SetToggleText(Button button, string textValue)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null)
            {
                tmp.text = textValue;
            }
            else
            {
                var txt = button.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.text = textValue;
                }
            }
        }

        private void OnPlayClicked()
        {
            Debug.Log($"[MainMenuManager] Playing game. Loading scene: {playSceneName}");
            SceneManager.LoadScene(playSceneName);
        }

        private void OnExitClicked()
        {
            if (!string.IsNullOrEmpty(exitSceneName))
            {
                Debug.Log($"[MainMenuManager] Exiting to target scene: {exitSceneName}");
                SceneManager.LoadScene(exitSceneName);
            }
            else
            {
                Debug.Log("[MainMenuManager] Exit scene name empty. Quitting Application.");
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }
    }
}
