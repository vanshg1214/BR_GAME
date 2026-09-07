using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace WhackAMole
{
    public class InGameMenuManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The parent GameObject containing the menu UI Canvas.")]
        [SerializeField] private GameObject menuCanvas;
        
        [Tooltip("The Player Canvas (HUD) that should be hidden while paused.")]
        [SerializeField] private GameObject playerCanvas;
        
        [Header("Hand Selection Buttons")]
        [SerializeField] private Button leftHandButton;
        [SerializeField] private Button bothHandsButton;
        [SerializeField] private Button rightHandButton;

        [Header("Button Visual Feedback")]
        [SerializeField] private Color selectedColor = new Color(0.917f, 0.768f, 0.207f, 1f); // Yellow
        [SerializeField] private Color normalColor = Color.white;

        [Header("Action Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button recenterButton;
        [SerializeField] private Button exitButton;
        
        [Header("Menu Configuration")]
        [Tooltip("If true, Time.timeScale will be set to 0 to freeze physics while paused.")]
        [SerializeField] private bool freezeTimeOnPause = true;

        private bool isPaused = false;
        private GameState previousState = GameState.Playing;

        private void Start()
        {
            // Do NOT forcefully set menuCanvas.SetActive(false) here.
            // If the canvas starts disabled in the Inspector, Start() won't run until the Pause button activates it.
            // If we turn it off here, it will instantly hide itself the exact same frame it turns on!

            // Optimize by binding button listeners directly in code instead of via Inspector UnityEvents
            if (leftHandButton != null) leftHandButton.onClick.AddListener(SetLeftHandOnly);
            if (rightHandButton != null) rightHandButton.onClick.AddListener(SetRightHandOnly);
            if (bothHandsButton != null) bothHandsButton.onClick.AddListener(SetBothHands);
            
            if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
            if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
            if (recenterButton != null) recenterButton.onClick.AddListener(RecenterTable);
            if (exitButton != null) exitButton.onClick.AddListener(ExitToMenu);
        }

        /// <summary>
        /// Toggles the menu on or off. Can be called via script or VR button.
        /// </summary>
        public void ToggleMenu()
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        /// <summary>
        /// Pauses the game, freezes time, and shows the menu.
        /// </summary>
        public void PauseGame()
        {
            if (isPaused) return;

            isPaused = true;

            if (menuCanvas != null)
            {
                // Teleport the menu to be in line with the Player Canvas, but exactly 1 meter closer to the player!
                if (playerCanvas != null)
                {
                    // playerCanvas.transform.forward points AWAY from the player (into the screen). 
                    // Subtracting it pulls the menu exactly 1 meter towards the player's face.
                    menuCanvas.transform.position = playerCanvas.transform.position - (playerCanvas.transform.forward * 1.0f);
                    menuCanvas.transform.rotation = playerCanvas.transform.rotation;
                }

                menuCanvas.SetActive(true);
            }

            if (playerCanvas != null)
            {
                playerCanvas.SetActive(false);
            }

            // Immediately hide all hammers while paused
            foreach (var hammer in FindObjectsOfType<HandHammer>(true))
            {
                hammer.gameObject.SetActive(false);
            }

            if (GameManager.Instance != null)
            {
                // Save the current state so we can return to it later
                previousState = GameManager.Instance.CurrentState;
                GameManager.Instance.UpdateState(GameState.Paused);
            }

            if (freezeTimeOnPause)
            {
                Time.timeScale = 0f;
            }

            // Update the button colors so the correct hand mode is highlighted when the menu opens
            if (GameManager.Instance != null && GameManager.Instance.RehabProfile != null)
            {
                UpdateButtonColors(GameManager.Instance.RehabProfile.handMode);
            }

            Debug.Log("[InGameMenu] Game Paused.");
        }

        /// <summary>
        /// Resumes the game, unfreezes time, and hides the menu.
        /// </summary>
        public void ResumeGame()
        {
            if (!isPaused) return;

            isPaused = false;

            if (menuCanvas != null)
            {
                menuCanvas.SetActive(false);
            }

            if (playerCanvas != null)
            {
                playerCanvas.SetActive(true);
            }

            if (freezeTimeOnPause)
            {
                Time.timeScale = 1f;
            }

            if (GameManager.Instance != null)
            {
                // Restore the hammers based on the current user profile settings
                GameManager.Instance.ApplyProfileHammers();

                // If the user changed hand modes, instantly shift the table laterally right before resuming!
                if (WorkspaceAutoPositioner.Instance != null)
                {
                    WorkspaceAutoPositioner.Instance.UpdateHandShiftOnly();
                }

                // Restore the state from before we paused (usually GameState.Playing)
                GameManager.Instance.UpdateState(previousState);
            }

            Debug.Log("[InGameMenu] Game Resumed.");
        }

        /// <summary>
        /// Snaps the Arcade Table back in front of the player's current position.
        /// </summary>
        public void RecenterTable()
        {
            if (WorkspaceAutoPositioner.Instance != null)
            {
                WorkspaceAutoPositioner.Instance.RepositionBoard();
                Debug.Log("[InGameMenu] Table Recentered via Menu.");
            }
            else
            {
                Debug.LogWarning("[InGameMenu] Cannot Recenter: WorkspaceAutoPositioner not found.");
            }
        }

        #region Hand Toggles

        public void SetLeftHandOnly()
        {
            ChangeHandMode(RehabProfileSO.HandMode.Left);
        }

        public void SetRightHandOnly()
        {
            ChangeHandMode(RehabProfileSO.HandMode.Right);
        }

        public void SetBothHands()
        {
            ChangeHandMode(RehabProfileSO.HandMode.Both);
        }

        private void ChangeHandMode(RehabProfileSO.HandMode newMode)
        {
            if (GameManager.Instance != null && GameManager.Instance.RehabProfile != null)
            {
                GameManager.Instance.RehabProfile.handMode = newMode;
                GameManager.Instance.RehabProfile.isLeftArm = (newMode == RehabProfileSO.HandMode.Left);
                
                // Update visual button feedback
                UpdateButtonColors(newMode);
                
                Debug.Log($"[InGameMenu] Changed Hand Mode to: {newMode}");
            }
            else
            {
                Debug.LogWarning("[InGameMenu] Cannot change hands: GameManager or RehabProfile is missing.");
            }
        }

        private void UpdateButtonColors(RehabProfileSO.HandMode currentMode)
        {
            if (leftHandButton != null && leftHandButton.GetComponent<Image>() != null) 
                leftHandButton.GetComponent<Image>().color = (currentMode == RehabProfileSO.HandMode.Left) ? selectedColor : normalColor;
                
            if (rightHandButton != null && rightHandButton.GetComponent<Image>() != null) 
                rightHandButton.GetComponent<Image>().color = (currentMode == RehabProfileSO.HandMode.Right) ? selectedColor : normalColor;
                
            if (bothHandsButton != null && bothHandsButton.GetComponent<Image>() != null) 
                bothHandsButton.GetComponent<Image>().color = (currentMode == RehabProfileSO.HandMode.Both) ? selectedColor : normalColor;
        }

        #endregion

        #region Scene Management

        /// <summary>
        /// Instantly reloads the current scene.
        /// </summary>
        public void RestartGame()
        {
            // Make sure time is unfrozen before switching scenes!
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Exits the game and loads the main menu scene.
        /// </summary>
        public void ExitToMenu()
        {
            // Make sure time is unfrozen before switching scenes!
            Time.timeScale = 1f;
            // Assuming the main menu is named "MenuScene" as per standard conventions
            SceneManager.LoadScene("MenuScene");
        }

        #endregion
    }
}
