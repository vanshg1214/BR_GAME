using System.Collections;
using UnityEngine;

namespace WhackAMole
{
    public class CoconutPayload : MonoBehaviour, IHittable
    {
        [Header("Coconut Settings")]
        [Tooltip("The mesh of the intact coconut to hide on hit.")]
        [SerializeField] private GameObject intactVisual;

        [Tooltip("Optional: A parent object containing broken coconut physics pieces.")]
        [SerializeField] private GameObject brokenPrefab;

        [Tooltip("Optional: If only one half of the coconut should fall when hit, assign it here.")]
        [SerializeField] private GameObject fallingHalf;

        [Header("VFX & Sound")]
        [Tooltip("Optional: Custom particle effect prefab to spawn when hit.")]
        [SerializeField] private GameObject hitParticleVFX;

        [Tooltip("Optional: Water splash VFX to spawn when coconut pieces fall into water.")]
        [SerializeField] private GameObject waterSplashVFX;

        [Tooltip("Optional: An empty Transform placed between the two coconut shells. VFX will spawn here. If empty, spawns at hit point.")]
        [SerializeField] private Transform splashSpawnPoint;

        [Tooltip("Optional: Water splash audio clip to play with the splash VFX.")]
        [SerializeField] private AudioClip splashSoundFX;

        [Tooltip("Optional: Custom audio clip to play when hit.")]
        [SerializeField] private AudioClip hitSoundFX;

        private bool isHit = false;
        public bool IsHit => isHit;
        private Vector3 initialBrokenScale;

        private struct TransformData { public Vector3 pos; public Quaternion rot; }
        private System.Collections.Generic.Dictionary<Transform, TransformData> initialChildTransforms = new System.Collections.Generic.Dictionary<Transform, TransformData>();

        private Quaternion? initialVisualRot = null;

        /// <summary>
        /// Safely rotates the internal visual mesh so it doesn't get overridden by parent Animators.
        /// </summary>
        public void SetFacing(bool apply90DegTwist)
        {
            if (intactVisual != null)
            {
                if (initialVisualRot == null) initialVisualRot = intactVisual.transform.localRotation;
                intactVisual.transform.localRotation = initialVisualRot.Value * (apply90DegTwist ? Quaternion.Euler(0, 90f, 0) : Quaternion.identity);
            }
        }

        private void Awake()
        {
            if (brokenPrefab != null)
            {
                initialBrokenScale = brokenPrefab.transform.localScale;
                if (initialBrokenScale == Vector3.zero) initialBrokenScale = Vector3.one;

                // Setup rigidbodies for broken pieces
                MeshFilter[] filters = brokenPrefab.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter f in filters)
                {
                    f.gameObject.isStatic = false;
                    Rigidbody rb = f.GetComponent<Rigidbody>();
                    if (rb == null) rb = f.gameObject.AddComponent<Rigidbody>();
                    
                    Collider col = f.GetComponent<Collider>();
                    if (col == null)
                    {
                        MeshCollider mc = f.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = f.sharedMesh;
                        col = mc;
                    }
                    if (col is MeshCollider mcol) mcol.convex = true;
                    rb.isKinematic = true;

                    // Add DeactivateIfBelowTable component to handle deactivation if it falls below the table
                    if (f.gameObject.GetComponent<DeactivateIfBelowTable>() == null)
                    {
                        f.gameObject.AddComponent<DeactivateIfBelowTable>();
                    }
                }
            }

            // Cache initial positions of children so we can reset them if they drop
            foreach (Transform child in transform)
            {
                initialChildTransforms[child] = new TransformData { pos = child.localPosition, rot = child.localRotation };
            }
        }

        private void OnEnable()
        {
            isHit = false;
            
            // Restore child transforms (in case they dropped)
            foreach (Transform child in transform)
            {
                if (initialChildTransforms.ContainsKey(child))
                {
                    Rigidbody rb = child.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }

                    child.localPosition = initialChildTransforms[child].pos;
                    child.localRotation = initialChildTransforms[child].rot;
                }
            }

            // Reset visuals
            if (intactVisual != null) intactVisual.SetActive(true);
            
            if (brokenPrefab != null)
            {
                brokenPrefab.SetActive(false);
                brokenPrefab.transform.localScale = initialBrokenScale;
                
                Rigidbody[] rbs = brokenPrefab.GetComponentsInChildren<Rigidbody>(true);
                foreach (Rigidbody rb in rbs)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;

                    // Reactivate the child piece GameObject in case it fell off the table and was deactivated
                    rb.gameObject.SetActive(true);
                }
            }

            // Make sure our collider is enabled
            Collider myCol = GetComponent<Collider>();
            if (myCol != null) myCol.enabled = true;
        }

        public void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            if (isHit) return;
            isHit = true;

            // 1. Play Custom VFX & Sound
            if (hitParticleVFX != null)
            {
                Instantiate(hitParticleVFX, hitPosition, Quaternion.identity);
            }
            else if (FeedbackManager.Instance != null)
            {
                FeedbackManager.Instance.PlayHeavyHit(hitPosition, -1);
            }
            
            if (waterSplashVFX != null)
            {
                // Use dedicated splash point between shells if set, otherwise use the hit point
                Vector3 splashPos = (splashSpawnPoint != null) ? splashSpawnPoint.position : hitPosition;
                GameObject splashInstance = Instantiate(waterSplashVFX, splashPos, waterSplashVFX.transform.rotation);
                StartCoroutine(SplashSequence(splashInstance, splashPos));
            }

            if (hitSoundFX != null)
            {
                AudioSource.PlayClipAtPoint(hitSoundFX, hitPosition, 1.0f);
            }

            // 2. Score Points
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(50, velocity.magnitude);
            }

            // 3. Disable hit detection
            Collider myCol = GetComponent<Collider>();
            if (myCol != null) myCol.enabled = false;

            // 4. Break the coconut
            if (intactVisual == gameObject || intactVisual == null)
            {
                Transform closestHalf = FindClosestHalfToPlayer(transform);
                if (closestHalf != null)
                {
                    DropAndDestroyHalf(closestHalf, hitPosition);
                }
            }
            else
            {
                // Standard visual-toggle breaking flow
                if (intactVisual != null) intactVisual.SetActive(false);

                if (brokenPrefab != null)
                {
                    brokenPrefab.SetActive(true);
                    
                    // CRITICAL BUGFIX: Prevent broken coconut pieces from hitting the player!
                    CollisionIsolator.IsolateObject(brokenPrefab);

                    Transform closestHalf = FindClosestHalfToPlayer(brokenPrefab.transform);
                    if (closestHalf != null)
                    {
                        DropAndDestroyHalf(closestHalf, hitPosition);
                    }
                }
            }
        }

        private Transform FindClosestHalfToPlayer(Transform parent)
        {
            Transform closestHalf = null;
            float minDistance = float.MaxValue;
            Vector3 playerPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

            foreach (Transform child in parent)
            {
                // Only consider children with Renderers in themselves or their nested children (the actual visual halves)
                if (child.GetComponentInChildren<Renderer>() == null) continue;

                float dist = Vector3.Distance(child.position, playerPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestHalf = child;
                }
            }
            return closestHalf;
        }

        private IEnumerator SplashSequence(GameObject splashInstance, Vector3 position)
        {
            // Delay the splash sound slightly so the hit sound plays first
            yield return new WaitForSeconds(0.25f);

            if (splashSoundFX != null)
            {
                AudioSource.PlayClipAtPoint(splashSoundFX, position, 1.0f);
            }

            // Wait enough time for the water to arc and fall down, then clean it up to prevent clutter
            yield return new WaitForSeconds(3.0f);

            if (splashInstance != null)
            {
                Destroy(splashInstance);
            }
        }

        private void DropAndDestroyHalf(Transform half, Vector3 hitPosition)
        {
            // Detach the falling half from the parent
            half.SetParent(null, true);

            // Add or configure Rigidbody
            Rigidbody rb = half.GetComponent<Rigidbody>();
            if (rb == null) rb = half.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;

            // Ensure the falling half has a valid Convex MeshCollider
            Collider col = half.GetComponent<Collider>();
            if (col == null)
            {
                MeshFilter mf = half.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = half.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
            }
            else if (col is MeshCollider mcol)
            {
                mcol.convex = true;
            }

            // Apply a small push straight down so it detaches instantly, ignoring hammer hit direction
            rb.linearVelocity = Vector3.down * 2f;

            // Add DeactivateIfBelowTable component to handle deactivation if it falls below the table
            if (half.gameObject.GetComponent<DeactivateIfBelowTable>() == null)
            {
                half.gameObject.AddComponent<DeactivateIfBelowTable>();
            }

            // Clean up the falling half after 4 seconds
            Destroy(half.gameObject, 4f);
        }
    }
}
