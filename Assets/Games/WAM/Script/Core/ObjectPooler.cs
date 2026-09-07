using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WhackAMole
{
    public class ObjectPooler : MonoBehaviour
    {
        public static ObjectPooler Instance { get; private set; }

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

        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!poolMap.TryGetValue(tag, out Queue<GameObject> queue)) return null;

            int initialCount = queue.Count;
            if (initialCount == 0) return null;

            for (int i = 0; i < initialCount; i++)
            {
                GameObject obj = queue.Dequeue();
                if (obj == null) continue;

                if (obj.activeInHierarchy)
                {
                    queue.Enqueue(obj);
                    continue;
                }

                if (parent != null)
                {
                    obj.transform.SetParent(parent, false);
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    obj.transform.SetPositionAndRotation(position, rotation);
                    obj.transform.SetParent(transform);
                }
                
                obj.SetActive(true);
                queue.Enqueue(obj);
                return obj;
            }
            return null;
        }

        public GameObject SpawnOrAddPool(string tag, GameObject prefab, int initialSize, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!poolMap.TryGetValue(tag, out Queue<GameObject> queue))
            {
                if (prefab == null) return null;
                queue = new Queue<GameObject>(initialSize);
                for (int i = 0; i < initialSize; i++) queue.Enqueue(InstantiateInactive(prefab));
                poolMap.Add(tag, queue);
            }

            GameObject spawned = SpawnFromPool(tag, position, rotation, parent);

            if (spawned == null && prefab != null)
            {
                spawned = Instantiate(prefab);
                if (parent != null)
                {
                    spawned.transform.SetParent(parent, false);
                    spawned.transform.localPosition = Vector3.zero;
                    spawned.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    spawned.transform.SetPositionAndRotation(position, rotation);
                    spawned.transform.SetParent(transform);
                }
                spawned.SetActive(true);
                queue.Enqueue(spawned);
            }
            return spawned;
        }

        public void ReturnToPool(GameObject obj, float delay) => StartCoroutine(ReturnRoutine(obj, delay));

        private IEnumerator ReturnRoutine(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null) obj.SetActive(false);
        }

        private GameObject InstantiateInactive(GameObject prefab)
        {
            GameObject tempParent = new GameObject("TempInactiveParent");
            tempParent.SetActive(false);
            tempParent.transform.SetParent(transform);

            GameObject obj = Instantiate(prefab, tempParent.transform);
            obj.SetActive(false);
            obj.transform.SetParent(transform);
            Destroy(tempParent);
            
            return obj;
        }

        private void BuildPools()
        {
            poolMap = new Dictionary<string, Queue<GameObject>>();
            foreach (Pool pool in pools)
            {
                if (pool.prefab == null || poolMap.ContainsKey(pool.tag)) continue;
                var queue = new Queue<GameObject>(pool.size);
                for (int i = 0; i < pool.size; i++) queue.Enqueue(InstantiateInactive(pool.prefab));
                poolMap.Add(pool.tag, queue);
            }
        }
    }
}
