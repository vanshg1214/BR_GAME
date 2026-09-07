using UnityEngine;
using TMPro;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.UI
{
    /// <summary>
    /// Handles the visual representation of the scoreboard.
    /// Separates UI concerns from the core game logic by listening to GameManager events.
    /// </summary>
    public class VolleyballScoreboardUI : MonoBehaviour
    {
        [Header("Score Fields")]
        [Tooltip("Text field to display the Player's score.")]
        [SerializeField] private TextMeshProUGUI playerScoreText;
        [Tooltip("Text field to display the AI's score.")]
        [SerializeField] private TextMeshProUGUI aiScoreText;
        
        [Header("Stat Fields (Live Updates)")]
        [Tooltip("Text field to display the Current Rally count.")]
        [SerializeField] private TextMeshProUGUI bestRallyText;
        [Tooltip("Text field to display the Best Win Streak throughout the game.")]
        [SerializeField] private TextMeshProUGUI bestWinStreakText;
        
        [Header("Feedback Field")]
        [Tooltip("Text field to show match end messages (e.g. 'PLAYER WINS'). Optional.")]
        [SerializeField] private TextMeshProUGUI messageText;

        private void OnEnable()
        {
            // Subscribe to GameManager events if it already exists
            if (VolleyballGameManager.Instance != null)
            {
                SubscribeToEvents();
            }
        }

        private void Start()
        {
            // Fallback subscription in case GameManager initializes after UI
            if (VolleyballGameManager.Instance != null)
            {
                SubscribeToEvents();
            }
            
            if (messageText != null) messageText.text = "";
        }

        private void OnDisable()
        {
            if (VolleyballGameManager.Instance != null)
            {
                VolleyballGameManager.Instance.OnScoreUpdated -= UpdateScoreboard;
                VolleyballGameManager.Instance.OnMatchOver -= DisplayMatchMessage;
            }
        }

        private void SubscribeToEvents()
        {
            // Prevent double-subscription
            VolleyballGameManager.Instance.OnScoreUpdated -= UpdateScoreboard;
            VolleyballGameManager.Instance.OnScoreUpdated += UpdateScoreboard;
            
            VolleyballGameManager.Instance.OnMatchOver -= DisplayMatchMessage;
            VolleyballGameManager.Instance.OnMatchOver += DisplayMatchMessage;
            
            // Force an initial UI update to sync with current state
            UpdateScoreboard();
        }

        /// <summary>
        /// Reads the current state from the GameManager and updates the text fields.
        /// </summary>
        private void UpdateScoreboard()
        {
            VolleyballGameManager gm = VolleyballGameManager.Instance;
            if (gm == null) return;

            if (playerScoreText != null) playerScoreText.text = gm.PlayerScore.ToString();
            if (aiScoreText != null) aiScoreText.text = gm.AIScore.ToString();
            if (bestRallyText != null) bestRallyText.text = gm.CurrentRallyCount.ToString();
            if (bestWinStreakText != null) bestWinStreakText.text = gm.BestWinStreak.ToString();
        }

        private void DisplayMatchMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
            else
            {
                // Fallback: show the win message in the score fields if no message text is provided
                if (playerScoreText != null) playerScoreText.text = message;
                if (aiScoreText != null) aiScoreText.text = "";
            }
        }
    }
}
