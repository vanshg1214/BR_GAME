using UnityEngine;
using System;
using PopstrikeVR.Core;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// Manages the combo and streak system for PopstrikeVR.
    /// Tracks consecutive hits, increases multipliers, and broadcasts streak events.
    /// </summary>
    public class ComboManager : MonoBehaviour
    {
        public static ComboManager Instance { get; private set; }

        public int CurrentCombo { get; private set; }
        public int HighestCombo { get; private set; }
        public float CurrentMultiplier { get; private set; } = 1f;

        public event Action<int, float, int> OnComboChanged;
        public event Action<string> OnStreakEvent;

        [Header("Multiplier Settings")]
        [Tooltip("The maximum multiplier reachable.")]
        public float MaxMultiplier = 5f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void RegisterHit(int basePoints)
        {
            CurrentCombo++;
            if (CurrentCombo > HighestCombo)
            {
                HighestCombo = CurrentCombo;
            }
            UpdateMultiplier();

            OnComboChanged?.Invoke(CurrentCombo, CurrentMultiplier, basePoints);

            // Streak Events (Rehab-friendly + Arcade mix)
            if (CurrentCombo == 3)
            {
                TriggerStreakEvent("Great Movement!");
            }
            else if (CurrentCombo == 5)
            {
                TriggerStreakEvent("Keep Going!");
            }
            else if (CurrentCombo == 10)
            {
                TriggerStreakEvent("Doing Great!");
            }
            else if (CurrentCombo == 15)
            {
                TriggerStreakEvent("Unstoppable!");
            }
            else if (CurrentCombo == 20)
            {
                TriggerStreakEvent("Flawless!");
            }
        }

        public static float GetMultiplierForCombo(int combo, float maxMultiplier)
        {
            if (combo >= 20) return Mathf.Min(5f, maxMultiplier);
            if (combo >= 15) return Mathf.Min(4f, maxMultiplier);
            if (combo >= 10) return Mathf.Min(3f, maxMultiplier);
            if (combo >= 5)  return Mathf.Min(2f, maxMultiplier);
            if (combo >= 3)  return Mathf.Min(1.5f, maxMultiplier);
            return 1f;
        }

        public void BreakCombo()
        {
            if (CurrentCombo == 0) return;

            CurrentCombo = 0;
            CurrentMultiplier = 1f;

            OnComboChanged?.Invoke(CurrentCombo, CurrentMultiplier, 0);
            Debug.Log("[ComboManager] Combo Broken!");
        }

        private void UpdateMultiplier()
        {
            // MaxMultiplier is set in the Inspector (e.g. 5)
            if (CurrentCombo >= 20)
            {
                CurrentMultiplier = Mathf.Min(5f, MaxMultiplier);
            }
            else if (CurrentCombo >= 15)
            {
                CurrentMultiplier = Mathf.Min(4f, MaxMultiplier);
            }
            else if (CurrentCombo >= 10)
            {
                CurrentMultiplier = Mathf.Min(3f, MaxMultiplier);
            }
            else if (CurrentCombo >= 5)
            {
                CurrentMultiplier = Mathf.Min(2f, MaxMultiplier);
            }
            else if (CurrentCombo >= 3)
            {
                CurrentMultiplier = Mathf.Min(1.5f, MaxMultiplier);
            }
            else
            {
                CurrentMultiplier = 1f;
            }
        }

        private void TriggerStreakEvent(string message)
        {
            Debug.Log($"[ComboManager] STREAK EVENT: {message}");
            OnStreakEvent?.Invoke(message);
            
            // PopstrikeFeedbackManager will listen to this event to play VFX/SFX
            if (PopstrikeFeedbackManager.Instance != null)
            {
                PopstrikeFeedbackManager.Instance.PlayStreakFeedback(message);
            }
        }
    }
}
