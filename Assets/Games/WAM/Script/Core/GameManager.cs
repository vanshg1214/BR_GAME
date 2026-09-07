using UnityEngine;

namespace WhackAMole
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        #region Inspector Fields

        [Header("Configuration")]
        [SerializeField] private RehabProfileSO rehabProfile;
        [SerializeField] private DifficultyProfileSO difficultyProfile;

        [Header("Background Music")]
        [SerializeField] private AudioClip bgMusicClip;
        [Range(0f, 1f)] [SerializeField] private float bgMusicVolume = 0.5f;

        [Header("Session State (read-only at runtime)")]
        [SerializeField] private GameState currentState = GameState.Calibration;

        #endregion

        #region Private State

        private GameState previousState;
        private float sessionTimer;
        private AudioSource bgMusicSource;

        // Fixed-size listener buffer — avoids List<T> heap traffic during iteration.
        // 16 slots is plenty for UI + Spawner + Score + Report + Dashboard etc.
        private const int MaxListeners = 16;
        private readonly IGameStateListener[] listeners = new IGameStateListener[MaxListeners];
        private int listenerCount;

        #endregion

        #region Public API

        public RehabProfileSO    RehabProfile      => rehabProfile;
        public DifficultyProfileSO DifficultyProfile => difficultyProfile;
        public GameState         CurrentState       => currentState;
        public float             SessionTimer       => sessionTimer;

        /// <summary>
        /// Transitions to <paramref name="newState"/> and broadcasts to all listeners.
        /// </summary>
        public void UpdateState(GameState newState)
        {
            GameState oldState = currentState;
            currentState  = newState;
            previousState = newState; // keep in sync so Update() doesn't re-trigger
            Debug.Log($"[GameManager] State -> {newState}");

            // Only reset the timer if we are starting fresh (not returning from a paused state)
            if (newState == GameState.Playing && oldState != GameState.Paused && difficultyProfile != null)
            {
                sessionTimer = difficultyProfile.sessionDuration;
            }

            // Iterate backwards so a listener can safely unregister itself during the callback
            for (int i = listenerCount - 1; i >= 0; i--)
            {
                if (listeners[i] != null)
                {
                    listeners[i].OnGameStateChanged(newState);
                }
            }
        }

        public void RegisterListener(IGameStateListener listener)
        {
            if (listener == null) return;

            // Duplicate check — O(n) but n <= 16
            for (int i = 0; i < listenerCount; i++)
            {
                if (listeners[i] == listener) return;
            }

            if (listenerCount < listeners.Length)
            {
                listeners[listenerCount++] = listener;
            }
            else
            {
                Debug.LogError("[GameManager] Listener buffer is full — increase MaxListeners.");
            }
        }

        public void UnregisterListener(IGameStateListener listener)
        {
            if (listener == null) return;

            for (int i = 0; i < listenerCount; i++)
            {
                if (listeners[i] != listener) continue;

                // Swap-remove: O(1) deletion, order doesn't matter for broadcast
                listeners[i] = listeners[listenerCount - 1];
                listeners[listenerCount - 1] = null;
                listenerCount--;
                return;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            currentState = GameState.Playing;

            // Keep legacy isLeftArm in sync with the selected handMode
            if (rehabProfile != null)
            {
                rehabProfile.isLeftArm = (rehabProfile.handMode == RehabProfileSO.HandMode.Left);
            }
        }

        private void Start()
        {
            ApplyProfileHammers();
            previousState = currentState;
            UpdateState(currentState);

            // Initialize and play background music if clip is assigned
            if (bgMusicClip != null)
            {
                bgMusicSource = gameObject.AddComponent<AudioSource>();
                bgMusicSource.clip = bgMusicClip;
                bgMusicSource.volume = bgMusicVolume;
                bgMusicSource.loop = true;
                bgMusicSource.spatialBlend = 0f; // 2D flat audio
                bgMusicSource.playOnAwake = false;
                bgMusicSource.Play();
            }
        }

        public void ApplyProfileHammers()
        {
            if (rehabProfile == null) return;

            bool enableLeft = rehabProfile.handMode == RehabProfileSO.HandMode.Left || rehabProfile.handMode == RehabProfileSO.HandMode.Both;
            bool enableRight = rehabProfile.handMode == RehabProfileSO.HandMode.Right || rehabProfile.handMode == RehabProfileSO.HandMode.Both;

            foreach (var hammer in FindObjectsOfType<HandHammer>(true))
            {
                if (hammer.ControllerSide == OVRInput.Controller.LTouch)
                {
                    hammer.gameObject.SetActive(enableLeft);
                }
                else if (hammer.ControllerSide == OVRInput.Controller.RTouch)
                {
                    hammer.gameObject.SetActive(enableRight);
                }
            }
            Debug.Log($"[GameManager] Hammers initialized at scene start. Left active: {enableLeft}, Right active: {enableRight} (Mode: {rehabProfile.handMode})");
        }

        private void Update()
        {
            // Sync background music volume in editor in real-time
            if (bgMusicSource != null)
            {
                bgMusicSource.volume = bgMusicVolume;
            }

            // Let designers change state from the Inspector during Play Mode for testing
            if (currentState != previousState)
            {
                UpdateState(currentState);
                previousState = currentState;
            }

            // Countdown timer (Only if NOT using procedural Level Director)
            if (currentState == GameState.Playing)
            {
                if (WhackAMoleLevelDirector.Instance == null)
                {
                    sessionTimer -= Time.deltaTime;
                    if (sessionTimer <= 0f)
                    {
                        sessionTimer = 0f;
                        UpdateState(GameState.Finished);
                    }
                }
            }
        }

        #endregion
    }
}
