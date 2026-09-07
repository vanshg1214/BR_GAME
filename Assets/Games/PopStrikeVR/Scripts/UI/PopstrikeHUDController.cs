using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace PopstrikeVR.UI
{
    /// <summary>
    /// Controls the gameplay HUD in VR, displaying the current Score, Combo Multiplier,
    /// and a circular Combo Meter filling gauge.
    /// </summary>
    public class PopstrikeHUDController : MonoBehaviour
    {
        public static PopstrikeHUDController Instance { get; private set; }

        public int CurrentScore => currentScore;

        [Header("UI Elements")]
        [Tooltip("The TextMeshPro component displaying the countdown timer (MM:SS).")]
        public TextMeshProUGUI timerText;
        [Tooltip("The TextMeshPro component displaying the player's total score.")]
        public TextMeshProUGUI scoreText;
        [Tooltip("The TextMeshPro component displaying the active combo multiplier (e.g. x2).")]
        public TextMeshProUGUI multiplierText;
        [Tooltip("Optional label text (e.g. 'COMBO') that will also tint to match the meter.")]
        public TextMeshProUGUI comboLabelText;
        [Tooltip("The Image (set to Filled type) representing the circular combo meter gauge.")]
        public Image comboMeterFill;

        [Header("Animation Settings")]
        [Tooltip("How fast the multiplier text pops when it changes.")]
        public float popDuration = 0.15f;
        [Tooltip("The target scale size during the pop animation.")]
        public Vector3 popScaleMultiplier = new Vector3(1.4f, 1.4f, 1.4f);

        [Header("Meter Colors")]
        public Color meterColorEmpty = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Dim grey
        public Color meterColorX1_5 = new Color(0.9f, 0.9f, 0.9f, 1f); // Silver / White
        public Color meterColorX2 = new Color(0f, 1f, 1f, 1f); // Cyan
        public Color meterColorX3 = new Color(0f, 1f, 0f, 1f); // Neon Green
        public Color meterColorFinal = new Color(1f, 0.84f, 0f, 1f); // Gold

        private int currentScore = 0;
        private Vector3 multiplierOriginalScale = Vector3.one;
        private Coroutine popCoroutine;

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
            if (multiplierText != null)
            {
                multiplierOriginalScale = multiplierText.transform.localScale;
            }

            // Bind to ComboManager events
            if (PopstrikeVR.Gameplay.ComboManager.Instance != null)
            {
                PopstrikeVR.Gameplay.ComboManager.Instance.OnComboChanged += HandleComboChanged;
            }

            ResetHUD();
        }

        private void OnDestroy()
        {
            if (PopstrikeVR.Gameplay.ComboManager.Instance != null)
            {
                PopstrikeVR.Gameplay.ComboManager.Instance.OnComboChanged -= HandleComboChanged;
            }
        }

        /// <summary>
        /// Resets the HUD variables to start values.
        /// </summary>
        public void ResetHUD()
        {
            currentScore = 0;
            UpdateScoreUI();
            UpdateMultiplierUI(1f);
            UpdateComboMeter(0);
        }

        private void HandleComboChanged(int currentCombo, float currentMultiplier, int basePoints)
        {
            // 1. Calculate and add score if combo increased
            if (currentCombo > 0 && basePoints > 0)
            {
                int pointsGained = Mathf.RoundToInt(basePoints * currentMultiplier);
                currentScore += pointsGained;
                UpdateScoreUI();
            }

            // 2. Update Multiplier Text (with pop animation if it goes up)
            UpdateMultiplierUI(currentMultiplier);

            // 3. Update Combo Meter Fill
            UpdateComboMeter(currentCombo);
        }

        public void UpdateScoreUI()
        {
            if (scoreText != null)
            {
                scoreText.text = currentScore.ToString();
            }
        }

        public void UpdateTimerUI(float remainingSeconds)
        {
            if (timerText != null)
            {
                // Round up so that 59.9 seconds shows as 1:00 instead of dropping to 0:59 instantly.
                // This keeps it perfectly synced with the center-screen 'CeilToInt' countdown.
                int totalSeconds = Mathf.CeilToInt(remainingSeconds);
                int minutes = Mathf.FloorToInt(totalSeconds / 60f);
                int seconds = totalSeconds % 60;
                timerText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        private void UpdateMultiplierUI(float multiplier)
        {
            if (multiplierText != null)
            {
                string newText = $"x{multiplier:0.0}";
                
                // If multiplier is 1.0, we can hide it or keep it simple
                multiplierText.text = newText;

                // Play satisfying pop scaling animation
                if (gameObject.activeInHierarchy)
                {
                    if (popCoroutine != null) StopCoroutine(popCoroutine);
                    popCoroutine = StartCoroutine(PopMultiplierAnimation());
                }
            }
        }

        private void UpdateComboMeter(int combo)
        {
            if (comboMeterFill != null)
            {
                // Combo meter fills based on progression towards the next multiplier.
                // Combo 1-2: Multiplier x1 (fill builds toward 3)
                // Combo 3-4: Multiplier x1.5 (fill builds toward 5)
                // Combo 5-9: Multiplier x2 (fill builds toward 10)
                // Combo 10-14: Multiplier x3 (fill builds toward 15)
                // Combo 15-19: Multiplier x4 (fill builds toward 20)
                // Combo 20+: Max multiplier x5 (fully filled)
                
                float fillAmount = 0f;
                Color targetColor = meterColorEmpty;

                if (combo >= 20)
                {
                    fillAmount = 1f;
                    targetColor = meterColorFinal; // Final is Orange!
                }
                else if (combo >= 15)
                {
                    // Scale between 15 and 20 (Top 20% of the circle)
                    fillAmount = Mathf.Lerp(0.8f, 1f, (combo - 15) / 5f);
                    targetColor = meterColorFinal; // Orange
                }
                else if (combo >= 10)
                {
                    // Scale between 10 and 15 (60% to 80% of the circle)
                    fillAmount = Mathf.Lerp(0.6f, 0.8f, (combo - 10) / 5f);
                    targetColor = meterColorX3;
                }
                else if (combo >= 5)
                {
                    // Scale between 5 and 10 (40% to 60% of the circle)
                    fillAmount = Mathf.Lerp(0.4f, 0.6f, (combo - 5) / 5f);
                    targetColor = meterColorX2;
                }
                else if (combo >= 3)
                {
                    // Scale between 3 and 5 (20% to 40% of the circle)
                    fillAmount = Mathf.Lerp(0.2f, 0.4f, (combo - 3) / 2f);
                    targetColor = meterColorX1_5;
                }
                else if (combo > 0)
                {
                    // Scale between 0 and 3 (0% to 20% of the circle)
                    fillAmount = Mathf.Lerp(0f, 0.2f, combo / 3f);
                    // Fade color in from empty to Cyan
                    targetColor = Color.Lerp(meterColorEmpty, meterColorX1_5, fillAmount / 0.2f);
                }

                comboMeterFill.fillAmount = fillAmount;
                comboMeterFill.color = targetColor;
                
                // Also tint the multiplier text to match!
                if (multiplierText != null)
                {
                    multiplierText.color = combo > 0 ? targetColor : meterColorEmpty;
                }
                
                // Tint the optional COMBO label text
                if (comboLabelText != null)
                {
                    comboLabelText.color = combo > 0 ? targetColor : meterColorEmpty;
                }
            }
        }

        private IEnumerator PopMultiplierAnimation()
        {
            float elapsed = 0f;
            Transform t = multiplierText.transform;

            // Phase 1: Scale Up
            float halfDuration = popDuration * 0.4f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(multiplierOriginalScale, Vector3.Scale(multiplierOriginalScale, popScaleMultiplier), elapsed / halfDuration);
                yield return null;
            }

            // Phase 2: Scale Down to normal
            elapsed = 0f;
            float restDuration = popDuration * 0.6f;
            while (elapsed < restDuration)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(Vector3.Scale(multiplierOriginalScale, popScaleMultiplier), multiplierOriginalScale, elapsed / restDuration);
                yield return null;
            }

            t.localScale = multiplierOriginalScale;
        }
    }
}
