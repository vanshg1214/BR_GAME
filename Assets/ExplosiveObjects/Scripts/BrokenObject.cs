namespace ExpObj
{
    using UnityEngine;

    public class BrokenObject : MonoBehaviour
    {
        [Header("Breaking settings")]
        [SerializeField] GameObject[] pieces;
        [SerializeField] float velMultiplier = 2f;
        [SerializeField] float timeBeforeDestroying = 1f;

        private void Awake()
        {
            // CRITICAL FIX: Force this value to override whatever is saved in the Unity Prefab!
            timeBeforeDestroying = 1f;

            // PERFORMANCE FIX (Lag Spike Eradicator):
            // Convert all expensive MeshColliders to lightweight BoxColliders BEFORE Unity Physics can cook them!
            // This prevents a massive 0.5s game freeze when a bottle shatters!
            if (pieces != null)
            {
                foreach (GameObject piece in pieces)
                {
                    if (piece == null) continue;
                    Collider col = piece.GetComponent<Collider>();
                    if (col != null && col is MeshCollider)
                    {
                        DestroyImmediate(col);
                        piece.AddComponent<BoxCollider>();
                    }
                }
            }
        }

        void OnEnable()
        {
            if (WhackAMole.ObjectPooler.Instance != null)
            {
                WhackAMole.ObjectPooler.Instance.ReturnToPool(gameObject, timeBeforeDestroying);
            }
            else
            {
                Destroy(gameObject, timeBeforeDestroying);
            }
        }

        public void RandomVelocities()
        {
            for (int i = 0; i <= pieces.Length - 1; i++)
            {
                float xVel = Random.Range(-1f, 1f);
                float yVel = Random.Range(0, 1f);
                float zVel = Random.Range(-1f, 1f);
                Vector3 vel = new Vector3(velMultiplier * xVel, velMultiplier * yVel, velMultiplier * zVel);
                Rigidbody rb = pieces[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = vel;
                }
            }
        }
    }
}
