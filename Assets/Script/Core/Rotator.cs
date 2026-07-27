using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Rotates an object and can automatically generate a perfect ring of stars.
    /// </summary>
    public class Rotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("The speed and axis of rotation in degrees per second.")]
        [SerializeField] private Vector3 rotationVelocity = new Vector3(0, 180f, 0);

        [Header("Auto-Generator Settings")]
        [SerializeField] private GameObject starPrefab;
        [SerializeField] private int starCount = 6;
        [SerializeField] private float orbitRadius = 0.4f;

        private void Start()
        {
            // If we have a prefab and no children have been generated yet, generate them on play.
            if (starPrefab != null && transform.childCount == 0)
            {
                GenerateStars();
            }
        }

        private void Update()
        {
            transform.Rotate(rotationVelocity * Time.deltaTime, Space.Self);
        }

        [ContextMenu("Preview / Generate Stars")]
        public void GenerateStars()
        {
            if (starPrefab == null)
            {
                Debug.LogWarning("[Rotator] Please assign a Star Prefab first!");
                return;
            }

            // Clean up existing children if regenerating
            // Use DestroyImmediate to allow running this from the Editor Context Menu
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            // Generate them in a perfect circle
            float angleStep = 360f / starCount;
            for (int i = 0; i < starCount; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = Mathf.Sin(angle) * orbitRadius;
                float z = Mathf.Cos(angle) * orbitRadius; // Use Z for horizontal orbit, Y for vertical

                GameObject newStar = Instantiate(starPrefab, transform);
                newStar.transform.localPosition = new Vector3(x, 0, z);
                
                // Optionally make the stars face outward or look pretty
                newStar.transform.localRotation = Quaternion.Euler(0, i * angleStep, 0);
            }

            Debug.Log($"[Rotator] Generated {starCount} stars in a ring!");
        }
    }
}

