using System.Collections.Generic;
using UnityEngine;

namespace PopstrikeVR.Core
{
    public enum HandTrackingMode
    {
        BothHands,
        LeftHandOnly,
        RightHandOnly
    }

    public enum SessionDuration
    {
        ThreeMinutes,
        FiveMinutes
    }

    /// <summary>
    /// Static class storing user selections temporarily during play sessions.
    /// Does not use PlayerPrefs (not saved to disk) so settings clear when the game exits.
    /// </summary>
    public static class TemporarySessionData
    {
        public static HandTrackingMode HandMode = HandTrackingMode.BothHands;
        public static string Difficulty = "Medium"; // "Easy", "Medium", "Hard"
        public static SessionDuration Duration = SessionDuration.ThreeMinutes;
        public static string JsonFileName = "level1.json"; // Used as fallback or single-level mode
        public static string LevelSubFolder = "PopStrikeVR"; // The exact StreamingAssets subfolder — preserved across scene reloads
        
        // Settings for accessibility
        public static bool DisableGestures = false;
        
        // Environment
        public static bool IsMorningScene = false;
        
        // --- New Level Progression Tracking ---
        public static int CurrentLevelIndex = 1;
        public static List<string> CurrentLevelSequence = new List<string>();
        
        public static string MenuSceneName = "";
        public static string GameSceneName = "";
        
        // Track whether the player has configured settings or we are running editor defaults
        public static bool IsConfigured = false;
        public static bool IsRetry = false;

        /// <summary>
        /// Generates a randomized 6-level sequence. 
        /// Phase 1 (Levels 1-3) randomly uses level1, level2, level3.
        /// Phase 2 (Levels 4-6) randomly uses level4, level5, level6.
        /// </summary>
        public static void GenerateLevelSequence()
        {
            CurrentLevelSequence.Clear();

            List<string> phase1 = new List<string> { "level1.json", "level2.json", "level3.json" };
            List<string> phase2 = new List<string> { "level4.json", "level5.json", "level6.json" };

            // Remove random shuffling so the progression is linear!
            // This guarantees Level 1 is always level1.json, Level 4 is always level4.json, etc.

            CurrentLevelSequence.AddRange(phase1);
            CurrentLevelSequence.AddRange(phase2);
            
            Debug.Log($"[TemporarySessionData] Generated Level Sequence: {string.Join(", ", CurrentLevelSequence)}");
        }
    }
}
