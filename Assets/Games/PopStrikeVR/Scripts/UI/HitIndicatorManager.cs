using UnityEngine;
using System.Collections;

namespace PopstrikeVR.UI
{
    /// <summary>
    /// Singleton manager that spawns floating Tick and Cross indicators in world space 
    /// instead of using aggressive full-screen flashes.
    /// </summary>
    public class HitIndicatorManager : MonoBehaviour
    {
        public static HitIndicatorManager Instance { get; private set; }

        [Header("Prefabs")]
        [Tooltip("A prefab containing a SpriteRenderer with a Green Tick icon.")]
        public GameObject tickPrefab;
        
        [Tooltip("A prefab containing a SpriteRenderer with a Red Cross icon.")]
        public GameObject wrongPrefab;
        
        [Header("Animation Settings")]
        [Tooltip("How long the indicator stays on screen before fading out.")]
        public float animationDuration = 1.2f;
        
        [Tooltip("How far the indicator floats upwards.")]
        public float floatDistance = 0.3f;
        
        [Header("Optimization")]
        [Tooltip("Number of indicators to preload in the pool.")]
        public int poolSize = 10;

        private System.Collections.Generic.Queue<GameObject> tickPool = new System.Collections.Generic.Queue<GameObject>();
        private System.Collections.Generic.Queue<GameObject> wrongPool = new System.Collections.Generic.Queue<GameObject>();

        private Vector3 tickBaseScale = Vector3.one;
        private Vector3 wrongBaseScale = Vector3.one;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            if (tickPrefab != null) tickBaseScale = tickPrefab.transform.localScale;
            if (wrongPrefab != null) wrongBaseScale = wrongPrefab.transform.localScale;
            
            InitializePool(tickPrefab, tickPool);
            InitializePool(wrongPrefab, wrongPool);
        }

        private void InitializePool(GameObject prefab, System.Collections.Generic.Queue<GameObject> pool)
        {
            if (prefab == null) return;

            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                pool.Enqueue(obj);
            }
        }
        
        private GameObject GetFromPool(GameObject prefab, System.Collections.Generic.Queue<GameObject> pool, Vector3 position)
        {
            if (prefab == null) return null;

            GameObject obj;
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else
            {
                // Expand dynamically if we run out
                obj = Instantiate(prefab, transform);
            }

            obj.transform.position = position;
            obj.transform.rotation = Quaternion.identity;
            obj.SetActive(true);
            return obj;
        }

        /// <summary>
        /// Spawns a floating Green Tick at the specified position.
        /// </summary>
        public void ShowTick(Vector3 position)
        {
            if (tickPrefab == null)
            {
                Debug.LogWarning("[HitIndicatorManager] Tick Prefab is missing!");
                return;
            }
            GameObject obj = GetFromPool(tickPrefab, tickPool, position);
            if (obj != null) StartCoroutine(AnimateIndicator(obj, tickPool, tickBaseScale));
        }

        /// <summary>
        /// Spawns a floating Red Cross at the specified position.
        /// </summary>
        public void ShowWrong(Vector3 position)
        {
            if (wrongPrefab == null)
            {
                Debug.LogWarning("[HitIndicatorManager] Wrong Prefab is missing!");
                return;
            }
            GameObject obj = GetFromPool(wrongPrefab, wrongPool, position);
            if (obj != null) StartCoroutine(AnimateIndicator(obj, wrongPool, wrongBaseScale));
        }

        private IEnumerator AnimateIndicator(GameObject obj, System.Collections.Generic.Queue<GameObject> pool, Vector3 baseScale)
        {
            float elapsed = 0;
            Vector3 startPos = obj.transform.position;
            Vector3 targetPos = startPos + (Vector3.up * floatDistance);
            
            // Try to find a SpriteRenderer to fade its alpha
            SpriteRenderer spriteRenderer = obj.GetComponentInChildren<SpriteRenderer>();
            
            // Ensure it faces the camera exactly when it spawns
            if (Camera.main != null)
            {
                // Point away from camera to face it (if it's a quad/sprite)
                obj.transform.rotation = Quaternion.LookRotation(obj.transform.position - Camera.main.transform.position);
            }

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                
                // 1. Float upwards
                obj.transform.position = Vector3.Lerp(startPos, targetPos, t);
                
                // 2. Pop-Scale up quickly in the first 20%, then hold
                Vector3 targetPopScale = baseScale * 1.2f;
                
                if (t < 0.2f)
                {
                    obj.transform.localScale = Vector3.Lerp(Vector3.zero, targetPopScale, t / 0.2f);
                }
                else if (t < 0.4f)
                {
                    // Settle down to normal size
                    obj.transform.localScale = Vector3.Lerp(targetPopScale, baseScale, (t - 0.2f) / 0.2f);
                }
                else
                {
                    // Ensure it stays at base scale
                    obj.transform.localScale = baseScale;
                }
                
                // 3. Fade out in the last 40%
                if (t > 0.6f && spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                    spriteRenderer.color = c;
                }

                yield return null;
            }

            obj.transform.localScale = baseScale; // Reset scale for the pool
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = 1f; // Reset alpha for the pool
                spriteRenderer.color = c;
            }

            obj.SetActive(false);
            pool.Enqueue(obj); // Return to pool instead of destroying!
        }
    }
}
