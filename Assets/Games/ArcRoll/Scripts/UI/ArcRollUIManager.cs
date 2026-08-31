using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcRoll.Core;
using System.Collections;

namespace ArcRoll.UI
{
    public class ArcRollUIManager : MonoBehaviour
    {
        [Header("Score UI Elements")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private TextMeshProUGUI streakText;
        
        [Header("Combo Meter")]
        [SerializeField] private Image comboMeterFill;
        [SerializeField] private float meterAnimationSpeed = 5f;

        [Header("Animation Settings")]
        [SerializeField] private float popDuration = 0.15f;
        [SerializeField] private Vector3 popScaleMultiplier = new Vector3(1.4f, 1.4f, 1.4f);

        [Header("Meter Colors")]
        [SerializeField] private Color meterColorEmpty = Color.white;
        [SerializeField] private Color meterColorX1_5 = Color.cyan;
        [SerializeField] private Color meterColorX2 = Color.green;
        [SerializeField] private Color meterColorX3 = Color.yellow;
        [SerializeField] private Color meterColorFinal = new Color(1f, 0.9f, 0.1f);

        [Header("Timer UI Element")]
        [SerializeField] private TextMeshProUGUI timerText;

        private int lastScore = 0;
        private float lastCombo = 1f;

        public static ArcRollUIManager Instance { get; private set; }
        private bool isBreakActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (ArcRollScoreManager.Instance != null)
            {
                ArcRollScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
                ArcRollScoreManager.Instance.OnComboChanged += HandleComboChanged;
                ArcRollScoreManager.Instance.OnStreakChanged += HandleStreakChanged;
                
                UpdateScoreText(ArcRollScoreManager.Instance.currentScore);
                UpdateComboText(ArcRollScoreManager.Instance.comboMultiplier);
                UpdateStreakText(ArcRollScoreManager.Instance.currentStreak);

                if (comboText != null)
                {
                    comboText.color = GetMeterColorForMultiplier(ArcRollScoreManager.Instance.comboMultiplier);
                }

                // Set initial ring color (matches current tier)
                if (comboMeterFill != null)
                {
                    comboMeterFill.color = GetMeterColorForMultiplier(ArcRollScoreManager.Instance.comboMultiplier);
                    comboMeterFill.fillAmount = 0f;
                }
            }

            if (ArcRollGameManager.Instance != null)
            {
                ArcRollGameManager.Instance.OnTimeUpdated += HandleTimeUpdated;
                HandleTimeUpdated(ArcRollGameManager.Instance.timeRemaining);
            }
        }

        private void OnDestroy()
        {
            if (ArcRollScoreManager.Instance != null)
            {
                ArcRollScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
                ArcRollScoreManager.Instance.OnComboChanged -= HandleComboChanged;
                ArcRollScoreManager.Instance.OnStreakChanged -= HandleStreakChanged;
            }
            if (ArcRollGameManager.Instance != null)
            {
                ArcRollGameManager.Instance.OnTimeUpdated -= HandleTimeUpdated;
            }
        }

        private void HandleScoreChanged(int newScore)
        {
            int pointsEarned = newScore - lastScore;
            lastScore = newScore;
            UpdateScoreText(newScore);

            if (pointsEarned > 0)
            {
                StartCoroutine(PunchTextScale(scoreText, popScaleMultiplier, popDuration));
            }
        }

        private void HandleComboChanged(float newCombo)
        {
            UpdateComboText(newCombo);
            
            if (comboText != null)
            {
                comboText.color = GetMeterColorForMultiplier(newCombo);
            }

            if (newCombo > lastCombo)
            {
                // Exciting punch when combo increases!
                StartCoroutine(PunchTextScale(comboText, popScaleMultiplier, popDuration));
            }
            lastCombo = newCombo;
        }

        private Coroutine meterCoroutine;

        private void HandleStreakChanged(int newStreak)
        {
            UpdateStreakText(newStreak);

            if (comboMeterFill != null && ArcRollScoreManager.Instance != null)
            {
                // Max multiplier is 5.0x. From 1.0x to 5.0x is exactly 8 steps of 0.5x.
                // Since it increases on every single hit, 8 hits equals 100% full.
                int maxStreakToMaxCombo = 8;
                
                float targetFill = (float)newStreak / maxStreakToMaxCombo;
                if (targetFill > 1f) targetFill = 1f;
                if (newStreak == 0) targetFill = 0f;

                if (meterCoroutine != null) StopCoroutine(meterCoroutine);
                meterCoroutine = StartCoroutine(AnimateMeterRoutine(targetFill));
            }
        }

        private Color GetMeterColorForMultiplier(float multiplier)
        {
            if (multiplier < 1.5f) return meterColorEmpty;
            if (multiplier < 2.0f) return meterColorX1_5;
            if (multiplier < 2.5f) return meterColorX2;
            if (multiplier < 3.0f) return meterColorX3;
            return meterColorFinal;
        }

        private IEnumerator AnimateMeterRoutine(float targetFill)
        {
            if (comboMeterFill == null) yield break;

            float currentMultiplier = ArcRollScoreManager.Instance != null ? ArcRollScoreManager.Instance.comboMultiplier : 1f;
            Color currentColor = GetMeterColorForMultiplier(currentMultiplier);

            // If dropping back to 0 (streak broken), just snap it to feel punchy
            if (targetFill == 0f)
            {
                comboMeterFill.fillAmount = 0f;
                comboMeterFill.color = GetMeterColorForMultiplier(1f);
                yield break;
            }

            float startFill = comboMeterFill.fillAmount;
            float elapsed = 0f;
            float duration = 0.3f; // Fast, snappy fill

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                comboMeterFill.fillAmount = Mathf.Lerp(startFill, targetFill, elapsed / duration);
                comboMeterFill.color = currentColor;
                yield return null;
            }

            comboMeterFill.fillAmount = targetFill;
            comboMeterFill.color = currentColor;
        }

        private void HandleTimeUpdated(float timeRemaining)
        {
            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(timeRemaining / 60f);
                int seconds = Mathf.FloorToInt(timeRemaining % 60f);
                timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

                if (timeRemaining <= 10f && timeRemaining > 0f)
                {
                    timerText.color = Color.red;
                }
                else
                {
                    timerText.color = Color.white;
                }
            }
        }

        private void UpdateScoreText(int score)
        {
            if (scoreText != null) scoreText.text = score.ToString();
        }

        private void UpdateComboText(float combo)
        {
            if (comboText != null)
            {
                comboText.text = $"{combo:0.#}x";
                comboText.gameObject.SetActive(true);
            }
        }

        private void UpdateStreakText(int streak)
        {
            if (isBreakActive) return;
            if (streakText != null) streakText.text = streak.ToString();
        }

        public void SetBreakText(string text)
        {
            isBreakActive = true;
            if (streakText != null) 
                streakText.text = text;
        }

        public void EndBreakText()
        {
            isBreakActive = false;
            if (ArcRollScoreManager.Instance != null)
                UpdateStreakText(ArcRollScoreManager.Instance.currentStreak);
        }

        private IEnumerator PunchTextScale(TextMeshProUGUI tmpText, Vector3 targetScale, float duration)
        {
            if (tmpText == null) yield break;
            
            Vector3 originalScale = Vector3.one;
            float elapsed = 0f;

            // Scale up
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                tmpText.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
                yield return null;
            }

            elapsed = 0f;
            // Scale down
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                tmpText.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
                yield return null;
            }

            tmpText.transform.localScale = originalScale;
        }
    }
}
