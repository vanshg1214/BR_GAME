using UnityEngine;
using System;

namespace WhackAMole
{
    public class ScoreManager : MonoBehaviour, IGameStateListener
    {
        public static ScoreManager Instance { get; private set; }

        #region Events

        public event Action<int>       OnScoreChanged;
        public event Action<float>     OnAverageVelocityChanged;
        public event Action<int, float> OnComboChanged; // (combo count, multiplier)
        public event Action            OnMissRegistered;

        #endregion

        #region Session Metrics

        [Header("Session Metrics (read-only at runtime)")]
        [SerializeField] private int   score;
        [SerializeField] private int   totalHits;
        [SerializeField] private int   totalMisses;
        [SerializeField] private int   totalFakeHits;
        [SerializeField] private float averageHitVelocity;

        [Header("Combo")]
        [SerializeField] private int   combo;
        [SerializeField] private int   bestCombo;
        [SerializeField] private float multiplier = 1f;

        private float cumulativeVelocity;
        private GameState previousState = GameState.Calibration;

        #endregion

        #region Public Accessors

        public int   CurrentScore      => score;
        public int   TotalHits         => totalHits;
        public int   TotalMisses       => totalMisses;
        public int   TotalFakeHits     => totalFakeHits;
        public float AverageHitVelocity => averageHitVelocity;
        public int   CurrentCombo      => combo;
        public int   MaxCombo          => bestCombo;
        public float ScoreMultiplier   => multiplier;

        #endregion

        #region Scoring API

        /// <summary>
        /// Call when the player successfully whacks a valid mole.
        /// Combo multiplier increases by 0.5x every 3 consecutive hits, capped at 3x.
        /// </summary>
        public void AddScore(int basePoints, float hitVelocity, bool incrementsCombo = true)
        {
            if (incrementsCombo)
            {
                combo++;
                if (combo > bestCombo) bestCombo = combo;

                // +0.5x every 3 hits, max 3x
                multiplier = 1f + Mathf.Floor(combo / 3f) * 0.5f;
                multiplier = Mathf.Min(multiplier, 3f);
            }

            score += Mathf.RoundToInt(basePoints * multiplier);
            totalHits++;

            cumulativeVelocity += hitVelocity;
            averageHitVelocity = cumulativeVelocity / totalHits;

            OnScoreChanged?.Invoke(score);
            OnAverageVelocityChanged?.Invoke(averageHitVelocity);
            
            if (incrementsCombo)
            {
                OnComboChanged?.Invoke(combo, multiplier);
            }
        }

        /// <summary>
        /// Call when a mole auto-hides without being hit — resets the combo streak.
        /// </summary>
        public void RegisterMiss()
        {
            totalMisses++;
            combo = 0;
            multiplier = 1f;

            OnComboChanged?.Invoke(combo, multiplier);
            OnMissRegistered?.Invoke();
        }

        /// <summary>
        /// Call when the patient strikes a FakeMole — penalises score and breaks combo.
        /// </summary>
        public void RegisterFakeHit()
        {
            totalFakeHits++;
            score = Mathf.Max(0, score - 5); // small penalty, never goes negative
            combo = 0;
            multiplier = 1f;

            OnScoreChanged?.Invoke(score);
            OnComboChanged?.Invoke(combo, multiplier);
        }

        #endregion

        #region State Listener

        public void OnGameStateChanged(GameState newState)
        {
            // Wipe metrics whenever a new session begins or the board resets
            // CRITICAL: Do NOT reset the score if we are just unpausing the game!
            if (newState == GameState.Calibration ||
                newState == GameState.Ready)
            {
                ResetSession();
            }
            else if (newState == GameState.Playing && previousState != GameState.Paused)
            {
                // If we enter Playing from any state OTHER than paused (e.g. from Ready), reset!
                ResetSession();
            }

            previousState = newState;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterListener(this);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterListener(this);
            }
        }

        #endregion

        #region Internals

        private void ResetSession()
        {
            score              = 0;
            totalHits          = 0;
            totalMisses        = 0;
            totalFakeHits      = 0;
            cumulativeVelocity = 0f;
            averageHitVelocity = 0f;
            combo              = 0;
            bestCombo          = 0;
            multiplier         = 1f;

            OnScoreChanged?.Invoke(score);
            OnAverageVelocityChanged?.Invoke(averageHitVelocity);
            OnComboChanged?.Invoke(combo, multiplier);
        }

        #endregion
    }
}
