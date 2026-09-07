using TMPro;
using UnityEngine;

namespace WhackAMole.UI
{
    public class WhackAMoleBreakUIManager : MonoBehaviour
    {
        [Header("Shared UI Elements")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI timerText;

        private bool isSubscribed = false;
        private UIMetricsDisplay uiMetrics;

        private void Start()
        {
            if (titleText != null) titleText.text = "--";
            uiMetrics = FindFirstObjectByType<UIMetricsDisplay>();
        }

        private void Update()
        {
            // Subscribe as soon as the Director is ready — handles any Script Execution Order
            if (!isSubscribed && WhackAMoleLevelDirector.Instance != null)
            {
                WhackAMoleLevelDirector.Instance.OnBreakStarted      += HandleBreakStarted;
                WhackAMoleLevelDirector.Instance.OnBreakEnded        += HandleBreakEnded;
                WhackAMoleLevelDirector.Instance.OnBreakTimerUpdated += HandleBreakTimerUpdated;
                WhackAMoleLevelDirector.Instance.OnRoundTimerUpdated += HandleRoundTimerUpdated;
                isSubscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (WhackAMoleLevelDirector.Instance != null)
            {
                WhackAMoleLevelDirector.Instance.OnBreakStarted      -= HandleBreakStarted;
                WhackAMoleLevelDirector.Instance.OnBreakEnded        -= HandleBreakEnded;
                WhackAMoleLevelDirector.Instance.OnBreakTimerUpdated -= HandleBreakTimerUpdated;
                WhackAMoleLevelDirector.Instance.OnRoundTimerUpdated -= HandleRoundTimerUpdated;
            }
        }

        private void HandleBreakStarted()
        {
            if (titleText != null) titleText.text = "BREAK";
            TriggerUIRedraw();
        }

        private void HandleBreakEnded()
        {
            // Title text is updated by the first OnRoundTimerUpdated tick
        }

        private void HandleRoundTimerUpdated(float timeRemaining, int roundIndex)
        {
            if (titleText != null) titleText.text = $"Round {roundIndex}";

            if (timerText != null)
            {
                int minutes = Mathf.FloorToInt(timeRemaining / 60f);
                int seconds  = Mathf.FloorToInt(timeRemaining % 60f);
                timerText.text = $"{minutes:00}:{seconds:00}";
            }

            TriggerUIRedraw();
        }

        private void HandleBreakTimerUpdated(float timeRemaining)
        {
            if (timerText == null) return;

            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds  = Mathf.FloorToInt(timeRemaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";

            TriggerUIRedraw();
        }

        private void TriggerUIRedraw()
        {
            if (uiMetrics != null) uiMetrics.MarkDirty();
        }
    }
}
