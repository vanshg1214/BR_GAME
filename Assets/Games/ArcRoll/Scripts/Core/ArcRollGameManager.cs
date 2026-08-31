using UnityEngine;
using System;

namespace ArcRoll.Core
{
    public class ArcRollGameManager : MonoBehaviour
    {
        public static ArcRollGameManager Instance { get; private set; }

        [Header("Game State")]
        public bool isGameActive = false;
        public float gameDuration = 180f; // 3 minutes
        public float timeRemaining { get; private set; }

        [Header("Audio")]
        [Tooltip("Background music to play during the game.")]
        public AudioClip backgroundMusic;
        private AudioSource bgmSource;

        public event Action OnGameStarted, OnGameEnded;
        public event Action<float> OnTimeUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            
            // Initialize game duration from the Menu Manager settings
            gameDuration = ArcRoll.UI.ArcRollMenuManager.SessionDuration;
            timeRemaining = gameDuration; 

            // Setup Background Music
            if (backgroundMusic != null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.clip = backgroundMusic;
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.volume = 0.7f;
            }
        }

        private void Start()
        {
            // Auto-start music if game is active in inspector for testing
            if (isGameActive && bgmSource != null && !bgmSource.isPlaying) bgmSource.Play();
        }

        public void StartGame()
        {
            isGameActive = true;
            timeRemaining = gameDuration;
            
            ArcRollScoreManager.Instance?.ResetScore();
            OnGameStarted?.Invoke();
            OnTimeUpdated?.Invoke(timeRemaining);

            if (bgmSource != null && !bgmSource.isPlaying) bgmSource.Play();
            Debug.Log("ArcRoll Game Started");
        }

        private void Update()
        {
            if (!isGameActive) return;
            
            timeRemaining = Mathf.Max(0, timeRemaining - Time.deltaTime);
            if (timeRemaining == 0) EndGame();
            
            OnTimeUpdated?.Invoke(timeRemaining);
        }

        public void EndGame()
        {
            isGameActive = false;
            OnGameEnded?.Invoke();
            
            if (bgmSource?.isPlaying == true) bgmSource.Stop();

            Debug.Log($"ArcRoll Game Ended. Final Score: {ArcRollScoreManager.Instance?.currentScore ?? 0}");
        }
    }
}
