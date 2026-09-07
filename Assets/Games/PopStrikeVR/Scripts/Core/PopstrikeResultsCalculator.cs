using UnityEngine;
using PopstrikeVR.Data;

namespace PopstrikeVR.Core
{
    /// <summary>
    /// Handles the end-of-game logic including calculating stars, accuracy, 
    /// and positioning the Level Results UI in front of the player.
    /// </summary>
    public class PopstrikeResultsCalculator : MonoBehaviour
    {
        [Header("UI Spawn Settings")]
        [Tooltip("How far in front of the player (in meters) to spawn the results screen.")]
        public float resultsUIDistance = 1.2f;
        [Tooltip("Height offset of the results UI relative to the player's head height. Positive is higher, negative is lower.")]
        public float resultsUIHeightOffset = 0f;

        [Header("Fail Thresholds")]
        [Tooltip("Minimum waves the player must clear in a 3-min EASY session to pass.")]
        public int minWaves_Easy_3min = 18;
        [Tooltip("Minimum waves the player must clear in a 3-min MEDIUM session to pass.")]
        public int minWaves_Medium_3min = 25;
        [Tooltip("Minimum waves the player must clear in a 3-min HARD session to pass.")]
        public int minWaves_Hard_3min = 35;

        [Tooltip("Minimum waves the player must clear in a 5-min EASY session to pass.")]
        public int minWaves_Easy_5min = 30;
        [Tooltip("Minimum waves the player must clear in a 5-min MEDIUM session to pass.")]
        public int minWaves_Medium_5min = 42;
        [Tooltip("Minimum waves the player must clear in a 5-min HARD session to pass.")]
        public int minWaves_Hard_5min = 50;

        /// <summary>
        /// Calculates the final score and displays the results UI.
        /// </summary>
        public void CalculateAndShowResults(
            string difficulty, 
            SessionDuration sessionDuration, 
            int totalWavesSpawned, 
            int totalWavesMissed, 
            int totalErrors,
            int theoreticalMaxScore)
        {
            Debug.Log("[PopstrikeResultsCalculator] Session Complete! Computing Results...");
            
            int maxCombo = PopstrikeVR.Gameplay.ComboManager.Instance != null ? 
                PopstrikeVR.Gameplay.ComboManager.Instance.HighestCombo : 0;
            
            // --- Read Final Score ---
            int finalScore = PopstrikeVR.UI.PopstrikeHUDController.Instance != null ?
                PopstrikeVR.UI.PopstrikeHUDController.Instance.CurrentScore : 0;

            // --- Dynamic Percentage Scoring (For Left Ring Fill) ---
            float scorePercentage = theoreticalMaxScore > 0 ? ((float)finalScore / theoreticalMaxScore) * 100f : 0f;
            scorePercentage = Mathf.Clamp(scorePercentage, 0f, 100f);

            // --- Physical Accuracy (For Center Ring) ---
            int totalCorrectActions = totalWavesSpawned - totalWavesMissed;
            float totalActions = totalWavesSpawned + totalErrors;
            float hitMissAccuracy = totalActions > 0 ? (float)totalCorrectActions / totalActions * 100f : 0f;
            hitMissAccuracy = Mathf.Clamp(hitMissAccuracy, 0f, 100f);

            float star2AccuracyThreshold = 80f; // Easy default
            int star3ErrorThreshold = Mathf.Max(1, Mathf.CeilToInt(totalWavesSpawned * 0.20f)); // 20% for Easy

            if (difficulty == "Medium")
            {
                star2AccuracyThreshold = 85f;
                star3ErrorThreshold = Mathf.Max(1, Mathf.CeilToInt(totalWavesSpawned * 0.15f)); // 15% for Medium
            }
            else if (difficulty == "Hard")
            {
                star2AccuracyThreshold = 90f;
                star3ErrorThreshold = Mathf.Max(1, Mathf.CeilToInt(totalWavesSpawned * 0.10f)); // 10% for Hard
            }

            // User requested: "we will show level cleared only everytime so first star will always be warded"
            bool levelPassed = true; 
            int starCount = 1; // 1st star guaranteed
            
            if (hitMissAccuracy >= star2AccuracyThreshold) starCount = 2;
            if (starCount == 2 && totalErrors <= star3ErrorThreshold) starCount = 3;

            Debug.Log($"[PopstrikeResultsCalculator] Final -> Difficulty: {difficulty} | Session: {sessionDuration} | Passed: {levelPassed} | Score %: {scorePercentage:0.0}% | Accuracy: {hitMissAccuracy:0.0}% (Target: {star2AccuracyThreshold}%) | Errors: {totalErrors} (Threshold: {star3ErrorThreshold}) | Streak: {maxCombo} | Score: {finalScore}/{theoreticalMaxScore} | Stars: {starCount}");

            // --- Spawn UI ---
            PopstrikeVR.UI.LevelResultsUI resultsUI = PopstrikeVR.UI.LevelResultsUI.Instance;
            if (resultsUI == null)
            {
                // If it's inactive, the singleton Awake hasn't run. Find it manually!
                PopstrikeVR.UI.LevelResultsUI[] allUIs = Resources.FindObjectsOfTypeAll<PopstrikeVR.UI.LevelResultsUI>();
                if (allUIs.Length > 0) resultsUI = allUIs[0];
            }

            if (resultsUI != null)
            {
                resultsUI.gameObject.SetActive(true); // Force activate the root just in case
                resultsUI.DisplayResults(
                    scorePercentage, 
                    hitMissAccuracy, 
                    maxCombo, 
                    starCount, 
                    finalScore, 
                    star2AccuracyThreshold, 
                    star3ErrorThreshold);
            }
            else
            {
                Debug.LogError("[PopstrikeResultsCalculator] CRITICAL ERROR: Could not find LevelResultsUI in the scene!");
            }
        }
    }
}
