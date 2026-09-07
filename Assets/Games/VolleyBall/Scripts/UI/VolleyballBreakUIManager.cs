using UnityEngine;
using TMPro;

namespace Rehab.Volleyball.UI
{
    public class VolleyballBreakUIManager : MonoBehaviour
    {
        public static VolleyballBreakUIManager Instance { get; private set; }

        [Header("Break UI Elements")]
        [Tooltip("The GameObject containing the 'BREAK' text graphic. Set active during breaks.")]
        public GameObject breakTextObject;
        
        [Tooltip("The TextMeshProUGUI component that displays the countdown timer.")]
        public TextMeshProUGUI countdownTimerText;
        
        [Tooltip("Other GameObjects (like decorations, panels) to show ONLY during breaks.")]
        public GameObject[] objectsToShowDuringBreak;

        [Header("Gameplay UI Elements")]
        [Tooltip("The main scoreboard GameObject. Hidden during breaks.")]
        public GameObject scoreboardGameObject;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Ensure Break UI is hidden when the game starts
            HideBreakUI();
        }

        public void ShowBreakUI()
        {
            if (scoreboardGameObject != null) scoreboardGameObject.SetActive(false);
            
            if (breakTextObject != null) breakTextObject.SetActive(true);
            
            if (countdownTimerText != null) 
            {
                countdownTimerText.gameObject.SetActive(true);
                countdownTimerText.text = "";
            }

            if (objectsToShowDuringBreak != null)
            {
                foreach (var obj in objectsToShowDuringBreak)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }
        }

        public void HideBreakUI()
        {
            if (scoreboardGameObject != null) scoreboardGameObject.SetActive(true);
            
            if (breakTextObject != null) breakTextObject.SetActive(false);
            if (countdownTimerText != null) countdownTimerText.gameObject.SetActive(false);

            if (objectsToShowDuringBreak != null)
            {
                foreach (var obj in objectsToShowDuringBreak)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }
        }

        public void UpdateCountdownText(float timeRemaining)
        {
            if (countdownTimerText != null)
            {
                if (timeRemaining <= 3.0f && timeRemaining > 0f)
                {
                    // Just show the single digit for the 3.. 2.. 1.. countdown
                    countdownTimerText.text = Mathf.CeilToInt(timeRemaining).ToString();
                }
                else
                {
                    // Show standard MM:SS format for the rest of the break
                    int minutes = Mathf.FloorToInt(timeRemaining / 60f);
                    int seconds = Mathf.FloorToInt(timeRemaining % 60f);
                    countdownTimerText.text = $"{minutes:00}:{seconds:00}";
                }
            }
        }
    }
}
