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

        public event Action OnGameStarted;
        public event Action OnGameEnded;
        public event Action<float> OnTimeUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Initialize game duration from the Menu Manager settings
            gameDuration = ArcRoll.UI.ArcRollMenuManager.SessionDuration;
            timeRemaining = gameDuration; // Initialize time so UI displays correct start time on scene load!

            // Setup Background Music
            if (backgroundMusic != null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.clip = backgroundMusic;
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.volume = 0.7f; // User requested volume
            }
        }

        private void Start()
        {
            // If the game is set to auto-start in the inspector for testing, play the music immediately!
            if (isGameActive && bgmSource != null && !bgmSource.isPlaying)
            {
                bgmSource.Play();
            }
        }

        public void StartGame()
        {
            isGameActive = true;
            timeRemaining = gameDuration;
            
            if (ArcRollScoreManager.Instance != null)
            {
                ArcRollScoreManager.Instance.ResetScore();
            }

            OnGameStarted?.Invoke();
            OnTimeUpdated?.Invoke(timeRemaining);

            if (bgmSource != null && !bgmSource.isPlaying)
            {
                bgmSource.Play();
            }

            Debug.Log("ArcRoll Game Started");
        }

        private void Update()
        {
            if (isGameActive)
            {
                timeRemaining -= Time.deltaTime;
                
                if (timeRemaining <= 0)
                {
                    timeRemaining = 0;
                    EndGame();
                }
                
                OnTimeUpdated?.Invoke(timeRemaining);
            }
        }

        public void EndGame()
        {
            isGameActive = false;
            OnGameEnded?.Invoke();
            
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.Stop();
            }

            int finalScore = ArcRollScoreManager.Instance != null ? ArcRollScoreManager.Instance.currentScore : 0;
            Debug.Log("ArcRoll Game Ended. Final Score: " + finalScore);
        }
    }
}
