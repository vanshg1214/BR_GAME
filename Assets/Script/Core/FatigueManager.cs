using UnityEngine;

namespace WhackAMole
{
    public class FatigueManager : MonoBehaviour, IGameStateListener
    {
        public static FatigueManager Instance { get; private set; }

        [Header("Fatigue Settings")]
        [Tooltip("Number of consecutive misses before fatigue is triggered.")]
        [SerializeField] private int fatigueMissThreshold = 3;

        private int consecutiveMisses = 0;

        public bool IsFatigued => consecutiveMisses >= fatigueMissThreshold;
        
        // Increases by 0.5 seconds for every miss past the threshold
        public float FatigueDelayModifier => IsFatigued ? (consecutiveMisses - fatigueMissThreshold + 1) * 0.5f : 0f;
        
        // Increases how long the mole stays visible by 0.5 seconds progressively
        public float FatigueDurationModifier => IsFatigued ? (consecutiveMisses - fatigueMissThreshold + 1) * 0.5f : 0f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
                ScoreManager.Instance.OnMissRegistered += HandleMissRegistered;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterListener(this);
            }
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
                ScoreManager.Instance.OnMissRegistered -= HandleMissRegistered;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterListener(this);
            }
        }

        private void HandleScoreChanged(int currentScore)
        {
            // A hit resets fatigue
            if (consecutiveMisses > 0)
            {
                consecutiveMisses = 0;
            }
        }

        private void HandleMissRegistered()
        {
            consecutiveMisses++;
        }

        public void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Calibration ||
                newState == GameState.Ready ||
                newState == GameState.Playing)
            {
                consecutiveMisses = 0;
            }
        }
    }
}
