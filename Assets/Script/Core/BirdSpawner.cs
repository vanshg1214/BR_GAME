using System.Collections;
using UnityEngine;

namespace WhackAMole
{
    public class BirdSpawner : MonoBehaviour, IGameStateListener
    {
        [Header("Bird Settings")]
        [Tooltip("The Bird prefab (SK_SmallBird_lv3) to spawn.")]
        [SerializeField] private GameObject birdPrefab;

        [Header("Spawn Timings")]
        [Tooltip("Minimum seconds between bird spawns.")]
        [SerializeField] private float minSpawnInterval = 15f;
        
        [Tooltip("Maximum seconds between bird spawns.")]
        [SerializeField] private float maxSpawnInterval = 30f;

        private Coroutine spawnRoutine;

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterListener(this);
                Debug.Log("<color=cyan>[BirdSpawner] Successfully registered to GameManager.</color>");

                if (GameManager.Instance.CurrentState == GameState.Playing)
                {
                    Debug.Log("<color=cyan>[BirdSpawner] Game already playing on Start! Triggering spawn loop.</color>");
                    OnGameStateChanged(GameState.Playing);
                }
            }
            else
            {
                Debug.LogError("<color=red>[BirdSpawner] GameManager is missing on Start!</color>");
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterListener(this);
            }
        }

        public void OnGameStateChanged(GameState newState)
        {
            Debug.Log($"<color=cyan>[BirdSpawner] GameState changed to {newState}</color>");
            if (newState == GameState.Playing)
            {
                if (spawnRoutine == null)
                {
                    Debug.Log("<color=cyan>[BirdSpawner] Starting SpawnLoop Coroutine...</color>");
                    spawnRoutine = StartCoroutine(SpawnLoop());
                }
                else
                {
                    Debug.Log("<color=cyan>[BirdSpawner] SpawnLoop already running.</color>");
                }
            }
            else
            {
                if (spawnRoutine != null)
                {
                    StopCoroutine(spawnRoutine);
                    spawnRoutine = null;
                }
            }
        }

        private IEnumerator SpawnLoop()
        {
            float initialWait = Random.Range(3f, 5f);
            Debug.Log($"<color=cyan>[BirdSpawner] SpawnLoop started. Waiting {initialWait} seconds for first bird.</color>");
            
            // Initial delay before first bird (reduced for easier testing)
            yield return new WaitForSeconds(initialWait);

            while (true)
            {
                // Ensure the bird ONLY spawns if the probability is greater than 0 (i.e. Fly Mole is ON)
                DifficultyProfileSO difficulty = GameManager.Instance?.DifficultyProfile;
                if (difficulty != null && difficulty.birdSpawnProbability <= 0f)
                {
                    // Fly Mole is turned off in settings. Just wait and check again.
                    yield return new WaitForSeconds(5f);
                    continue;
                }

                // Enforce global active limit (Birds + Moles)
                if (difficulty != null && MoleSpawner.Instance != null)
                {
                    if (MoleSpawner.Instance.OccupiedHolesCount + BirdFlightController.ActiveBirdCount >= difficulty.maxActiveMoles)
                    {
                        Debug.Log("<color=cyan>[BirdSpawner] Skipped spawn: Max Active Moles reached across board and sky.</color>");
                        yield return new WaitForSeconds(3f);
                        continue;
                    }
                }

                Debug.Log("<color=cyan>[BirdSpawner] Attempting to spawn a bird...</color>");
                if (birdPrefab != null)
                {
                    GameObject bird = Instantiate(birdPrefab);
                    Debug.Log($"<color=cyan>[BirdSpawner] SUCCESS: Instantiated bird: {bird.name}</color>");
                    
                    // We destroy it after 15 seconds as a hard fallback to prevent leaks
                    Destroy(bird, 15f);
                }
                else
                {
                    Debug.LogWarning("[BirdSpawner] The Bird Prefab is missing! Please assign the SK_SmallBird_lv3 prefab in the Inspector on the BirdSpawner component.");
                }

                // Strictly wait 10 to 15 seconds before the next bird comes!
                float waitTime = Random.Range(10f, 15f);
                yield return new WaitForSeconds(waitTime);
            }
        }
    }
}
