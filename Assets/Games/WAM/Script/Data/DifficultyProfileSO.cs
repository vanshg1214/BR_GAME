using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Difficulty preset for a rehab session.
    /// Therapists can swap presets or tweak values at runtime through the dashboard.
    /// Created via Assets > Create > WhackAMole > Data > Difficulty Profile.
    /// </summary>
    public enum GameDifficulty { Easy, Medium, Hard }

    [CreateAssetMenu(fileName = "NewDifficultyProfile", menuName = "WhackAMole/Data/Difficulty Profile")]
    public class DifficultyProfileSO : ScriptableObject
    {
        [Header("Global Difficulty")]
        [Tooltip("The difficulty level selected by the user in the main menu.")]
        public GameDifficulty selectedDifficulty = GameDifficulty.Medium;

        [Header("Spawn Timing")]
        [Tooltip("Minimum delay (seconds) between consecutive mole spawns.")]
        public float minSpawnInterval = 1.5f;

        [Tooltip("Maximum delay (seconds) between consecutive mole spawns.")]
        public float maxSpawnInterval = 3.0f;

        [Tooltip("Max moles visible on the board at the same time.")]
        public int maxActiveMoles = 1;

        [Header("Mole Behaviour")]
        [Tooltip("How long a mole stays up before auto-hiding (seconds).")]
        public float moleVisibleDuration = 3.0f;

        [Header("Session")]
        [Tooltip("Total session length in seconds.")]
        public float sessionDuration = 60.0f;

        [Header("Force Threshold")]
        [Tooltip("Minimum hand velocity (m/s) to register a hit — mainly affects HeavyMoles.")]
        public float minHitVelocity = 0.5f;

        [Header("Mole Type Probabilities")]
        [Tooltip("Chance (0–1) a spawned mole is a Fake Squirrel (distractor).")]
        [Range(0f, 1f)]
        public float distractorProbability = 0.1f;

        [Tooltip("Chance (0–1) a spawned mole is a Treasure Hamster (requires multiple hits).")]
        [Range(0f, 1f)] public float heavyMoleProbability = 0.2f;

        [Tooltip("Probability of a Bottle Dog spawning.")]
        [Range(0f, 1f)] public float dogMoleProbability = 0.1f;

        [Tooltip("Probability of a Cage Hamster spawning.")]
        [Range(0f, 1f)] public float cageHamsterProbability = 0.15f;

        [Header("Independent Spawns (Out of Pool)")]
        [Tooltip("Probability the flying bird will spawn during its interval. (0 = never, 1 = always)")]
        [Range(0f, 1f)] public float birdSpawnProbability = 0.5f;

        [Tooltip("Minimum seconds between bird spawns.")]
        public float birdMinSpawnInterval = 15f;
        
        [Tooltip("Maximum seconds between bird spawns.")]
        public float birdMaxSpawnInterval = 30f;

    }
}
