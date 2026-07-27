using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace WhackAMole.UI
{
    public class CanvasStateManager : MonoBehaviour, IGameStateListener
    {
        [Header("UI Canvas Panels")]
        [Tooltip("The main patient HUD showing real-time metrics during gameplay.")]
        [SerializeField] private GameObject playerCanvas;

        [Tooltip("The clinical session report panel visible upon completion.")]
        [SerializeField] private GameObject reportCanvas;

        [Tooltip("A simple 'Thanks!' canvas shown at the end instead of the report.")]
        [SerializeField] private GameObject thanksCanvas;

        [Tooltip("The canvas containing Restart and Main Menu buttons, shown after the game ends.")]
        [SerializeField] private GameObject restartCanvas;

        [Header("End Game Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Button Visual Feedback")]
        [SerializeField] private Color selectedColor = new Color(0.917f, 0.768f, 0.207f, 1f); // Yellow
        [SerializeField] private Color normalColor = Color.white;

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip thanksAudio;
        [Tooltip("Optional: Assign an AudioSource to play the thanks sound. If null, we will create one.")]
        [SerializeField] private AudioSource audioSource;

        #region Unity Lifecycle

        private void Start()
        {
            if (audioSource == null && thanksAudio != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(RestartGame);
                
                // Configure Unity UI ColorBlock transitions just in case
                var cb = restartButton.colors;
                cb.normalColor = normalColor;
                cb.highlightedColor = selectedColor;
                cb.selectedColor = selectedColor;
                restartButton.colors = cb;
            }
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(ExitToMenu);

                // Configure Unity UI ColorBlock transitions just in case
                var cb = mainMenuButton.colors;
                cb.normalColor = normalColor;
                cb.highlightedColor = selectedColor;
                cb.selectedColor = selectedColor;
                mainMenuButton.colors = cb;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterListener(this);
                OnGameStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterListener(this);
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
                case GameState.Playing:
                    SetPanelVisibility(playerActive: true, reportActive: false, thanksActive: false, restartActive: false);
                    break;

                case GameState.Finished:
                    // Keep the player canvas active after the game ends, as requested.
                    SetPanelVisibility(playerActive: true, reportActive: false, thanksActive: true, restartActive: true);
                    
                    if (thanksAudio != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(thanksAudio);
                    }
                    break;
            }
        }

        #endregion

        #region Button Actions

        /// <summary>
        /// Instantly reloads the current scene to restart the game.
        /// </summary>
        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Returns to the main menu scene.
        /// </summary>
        public void ExitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MenuScene");
        }

        #endregion

        #region Helpers

        private void Update()
        {
            // Continuously update button colors based on their scale (since GazeInteractor scales hovered buttons up to 1.2f)
            UpdateButtonVisuals(restartButton);
            UpdateButtonVisuals(mainMenuButton);
        }

        private void UpdateButtonVisuals(Button button)
        {
            if (button == null) return;
            
            Image img = button.GetComponent<Image>();
            if (img == null) return;

            // If the GazeInteractor is currently hovering (zooming) the button, scale will be > 1.05f
            bool isHovered = button.transform.localScale.x > 1.05f;
            img.color = isHovered ? selectedColor : normalColor;
        }

        private void SetPanelVisibility(bool playerActive, bool reportActive, bool thanksActive, bool restartActive)
        {
            if (playerCanvas != null)
            {
                playerCanvas.SetActive(playerActive);
            }
            if (reportCanvas != null)
            {
                // Keeping this false as per user request to disable the full report for now
                reportCanvas.SetActive(reportActive);
            }
            if (thanksCanvas != null)
            {
                thanksCanvas.SetActive(thanksActive);
            }
            if (restartCanvas != null)
            {
                restartCanvas.SetActive(restartActive);
            }
        }

        #endregion
    }
}
