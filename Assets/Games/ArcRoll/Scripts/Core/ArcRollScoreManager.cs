using UnityEngine;
using System;

namespace ArcRoll.Core
{
    public class ArcRollScoreManager : MonoBehaviour
    {
        public static ArcRollScoreManager Instance { get; private set; }

        [Header("Score State")]
        public int currentScore = 0;
        public int currentStreak = 0;
        public int highestStreak = 0;
        public float comboMultiplier = 1f;
        
        [Tooltip("How many consecutive successful throws are needed to increase the combo?")]
        public int throwsForNextCombo = 3;

        public int successfulThrows = 0;
        public int totalErrors = 0;

        public event Action<int> OnScoreChanged;
        public event Action<int> OnStreakChanged;
        public event Action<float> OnComboChanged;
        public event Action OnComboLost;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void ResetScore()
        {
            currentScore = 0;
            currentStreak = 0;
            highestStreak = 0;
            comboMultiplier = 1f;
            successfulThrows = 0;
            totalErrors = 0;
            
            OnScoreChanged?.Invoke(currentScore);
            OnStreakChanged?.Invoke(currentStreak);
            OnComboChanged?.Invoke(comboMultiplier);
        }

        public void RecordError()
        {
            if (ArcRollGameManager.Instance != null && !ArcRollGameManager.Instance.isGameActive) return;
            totalErrors++;
            ResetStreak();
        }

        public void IncrementStreak()
        {
            if (ArcRollGameManager.Instance != null && !ArcRollGameManager.Instance.isGameActive) return;

            currentStreak++;
            successfulThrows++;

            if (currentStreak > highestStreak) highestStreak = currentStreak;

            // Every 2 consecutive successful hits increases the multiplier by 0.5x, up to a max of 3.0x
            float newCombo = 1f + (currentStreak / 2) * 0.5f;
            if (newCombo > 3f) newCombo = 3f;
            
            if (newCombo > comboMultiplier)
            {
                comboMultiplier = newCombo;
                OnComboChanged?.Invoke(comboMultiplier);
            }

            OnStreakChanged?.Invoke(currentStreak);
        }

        public void ResetStreak()
        {
            if (currentStreak > 0)
            {
                currentStreak = 0;
                comboMultiplier = 1f;
                
                OnStreakChanged?.Invoke(currentStreak);
                OnComboChanged?.Invoke(comboMultiplier);
                OnComboLost?.Invoke();
            }
        }

        public void AddScore(int basePoints)
        {
            if (ArcRollGameManager.Instance != null && !ArcRollGameManager.Instance.isGameActive) return;

            // Use Mathf.RoundToInt to properly calculate float multiplier
            int totalPoints = Mathf.RoundToInt(basePoints * comboMultiplier);
            currentScore += totalPoints;
            
            OnScoreChanged?.Invoke(currentScore);
        }
    }
}
