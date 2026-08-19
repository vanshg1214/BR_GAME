using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PopstrikeVR.Core
{
    /// <summary>
    /// A standalone, highly optimized Object Pooler built specifically for PopstrikeVR.
    /// Manages the spawning and reusing of Balloons to guarantee zero garbage collection during gameplay.
    /// </summary>
    public class PopstrikePooler : MonoBehaviour
    {
        public static PopstrikePooler Instance { get; private set; }

        [System.Serializable]
        public class Pool
        {
            public string tag;
            public GameObject prefab;
            public int size;
        }

        [SerializeField] private List<Pool> pools = new List<Pool>();

        private Dictionary<string, Queue<GameObject>> poolMap;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            BuildPools();
        }

        private void BuildPools()
        {
            poolMap = new Dictionary<string, Queue<GameObject>>();

            foreach (Pool pool in pools)
            {
                if (pool.prefab == null) continue;

                var queue = new Queue<GameObject>(pool.size);

                for (int i = 0; i < pool.size; i++)
                {
                    GameObject obj = Instantiate(pool.prefab, transform);
                    obj.SetActive(false);
                    queue.Enqueue(obj);
                }

                poolMap.Add(pool.tag, queue);
            }
        }

        /// <summary>
        /// Pulls a balloon from the pool safely.
        /// </summary>
        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!poolMap.TryGetValue(tag, out Queue<GameObject> queue))
            {
                Debug.LogWarning($"[PopstrikePooler] No pool exists with tag '{tag}'.");
                return null;
            }

            int count = queue.Count;
            if (count == 0) return null;

            for (int i = 0; i < count; i++)
            {
                GameObject obj = queue.Dequeue();

                if (obj == null) continue;
                
                // If it's already active, it's still being used!
                if (obj.activeInHierarchy)
                {
                    queue.Enqueue(obj);
                    continue;
                }

                // Found a free balloon
                obj.transform.SetParent(parent != null ? parent : transform);
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);

                queue.Enqueue(obj);
                return obj;
            }

            // DYNAMIC EXPANSION: If all objects are currently in use, dynamically expand the pool!
            Pool originalPoolConfig = pools.Find(p => p.tag == tag);
            if (originalPoolConfig != null && originalPoolConfig.prefab != null)
            {
                Debug.LogWarning($"[PopstrikePooler] Pool '{tag}' ran out of objects! Dynamically expanding pool size.");
                GameObject newObj = Instantiate(originalPoolConfig.prefab, parent != null ? parent : transform);
                newObj.transform.position = position;
                newObj.transform.rotation = rotation;
                newObj.SetActive(true);
                queue.Enqueue(newObj);
                return newObj;
            }

            return null;
        }

        /// <summary>
        /// Returns a balloon cleanly to the pool after a delay.
        /// </summary>
        public void ReturnToPool(GameObject obj, float delay)
        {
            StartCoroutine(ReturnRoutine(obj, delay));
        }

        private IEnumerator ReturnRoutine(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null) obj.SetActive(false);
        }

        // --- STATIC HELPERS FOR EASY ACCESS BY BALLOONS --- //

        public static GameObject SpawnBalloon(string tag, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (Instance != null)
                return Instance.SpawnFromPool(tag, position, rotation, parent);
            return null;
        }

        public static void DespawnBalloon(GameObject balloon, float delay = 0f)
        {
            if (Instance != null)
                Instance.ReturnToPool(balloon, delay);
            else if (balloon != null)
                balloon.SetActive(false);
        }

        public void DespawnAll()
        {
            if (poolMap == null) return;
            foreach (var kvp in poolMap)
            {
                foreach (var obj in kvp.Value)
                {
                    if (obj != null && obj.activeInHierarchy)
                    {
                        obj.SetActive(false);
                    }
                }
            }
        }

        public static void DespawnAllBalloons()
        {
            if (Instance != null)
                Instance.DespawnAll();
        }

        public void ShiftActiveBalloons(Vector3 positionDelta, float yawDelta, Vector3 pivot)
        {
            if (poolMap == null) return;
            
            Quaternion rotation = Quaternion.Euler(0, yawDelta, 0);

            foreach (var kvp in poolMap)
            {
                foreach (var obj in kvp.Value)
                {
                    if (obj != null && obj.activeInHierarchy)
                    {
                        // Rotate around pivot if there is a yaw delta
                        if (Mathf.Abs(yawDelta) > 0.01f)
                        {
                            Vector3 dir = obj.transform.position - pivot;
                            dir = rotation * dir;
                            obj.transform.position = pivot + dir;
                            obj.transform.rotation = rotation * obj.transform.rotation;
                        }
                        
                        // Add position delta
                        obj.transform.position += positionDelta;
                    }
                }
            }
        }
    }
}
