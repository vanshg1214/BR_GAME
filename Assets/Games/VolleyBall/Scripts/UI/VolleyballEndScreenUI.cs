using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.UI
{
    public class VolleyballEndScreenUI : MonoBehaviour
    {
        [Header("End Screen Elements")]
        [Tooltip("The text that will say PLAYER WON! or OPPONENT WON!")]
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button menuBtn;
        [SerializeField] private Button nextBtn;

        [Header("Scene Navigation")]
        [Tooltip("The exact name of your Menu Scene to load when returning to menu.")]
        [SerializeField] private string menuSceneName = "Menu Scene";

        private void Start()
        {
            // Bind button listeners
            if (menuBtn != null) menuBtn.onClick.AddListener(OnMenuClicked);
            if (nextBtn != null) nextBtn.onClick.AddListener(OnNextClicked);

            // Subscribe to game over event
            if (VolleyballGameManager.Instance != null)
            {
                VolleyballGameManager.Instance.OnMatchOver -= HandleMatchOver;
                VolleyballGameManager.Instance.OnMatchOver += HandleMatchOver;
            }
        }

        private void OnDestroy()
        {
            if (VolleyballGameManager.Instance != null)
            {
                VolleyballGameManager.Instance.OnMatchOver -= HandleMatchOver;
            }
        }

        private void HandleMatchOver(string winMessage)
        {
            // Set result text
            if (resultText != null)
            {
                resultText.text = winMessage;
            }
        }

        private void OnMenuClicked()
        {
            if (string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(0); // Fallback to build index 0
            }
            else
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }

        private void OnNextClicked()
        {
            if (VolleyballGameManager.Instance != null)
            {
                // Increase difficulty slightly
                VolleyballGameManager.Instance.BumpDifficulty();
                
                // Restart the match immediately without reloading the scene
                VolleyballGameManager.Instance.RestartMatch();
            }
        }
    }
}
