using UnityEngine;
using System.Collections;
using WhackAMole; // Assuming IHittable is in this namespace

namespace WhackAMole.Squirrel
{
    public class AcornProp : MonoBehaviour, IHittable
    {
        [Header("Mesh Setup")]
        [Tooltip("The solid, unbroken acorn/object. Must have a Collider on it (e.g. Box or Sphere).")]
        public GameObject solidMesh;
        [Tooltip("The parent object containing all the cell-fractured broken pieces.")]
        public GameObject brokenMeshParent;

        [Header("Explosion Settings")]
        [Tooltip("How violently the pieces fly apart when hit.")]
        public float explosionForce = 300f;
        [Tooltip("The radius of the explosion effect.")]
        public float explosionRadius = 1.5f;
        [Tooltip("Upward modifier to make pieces fly slightly up into the air.")]
        public float upwardModifier = 1.0f;
        [Tooltip("Minimum hammer velocity required to break the acorn.")]
        public float minHitVelocity = 0.0f;

        [Header("Performance Cleanup")]
        [Tooltip("Time before the broken pieces start shrinking to disappear.")]
        public float timeBeforeCleanup = 0.5f; // Decreased drastically from 3.0s
        [Tooltip("How fast the pieces shrink before being destroyed.")]
        public float shrinkDuration = 0.5f; // Sped up from 1.0s

        [Header("VFX & SFX")]
        [Tooltip("Optional particle system to spawn at the hit location (like wood splinters or dust).")]
        public GameObject hitParticlesPrefab;
        [Tooltip("The sound effect to play when the prop shatters.")]
        public AudioClip shatterSound;

        [Header("Seeds Logic")]
        [Tooltip("The seeds object that appears when the acorn breaks.")]
        public GameObject seedsObject;

        public event System.Action OnTargetDestroyed;

        public bool IsBroken => isBroken;

        private bool isBroken = false;
        private bool isThrown = false;
        private Rigidbody[] brokenPiecesRigidbodies;
        private TransformData[] initialPieceTransforms;
        private Vector3 initialParentScale;

        // Where the acorn originally sat in the Squirrel's hand before being thrown
        private Transform initialHandParent;
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;
        private Vector3 initialAcornScale;

        // Caching for seeds so they can be reset after being picked up
        private Transform initialSeedsParent;
        private Vector3 initialSeedsLocalPos;
        private Vector3 initialSeedsScale;
        
        public System.Action OnShattered;
        private bool seedsPickedUp = false;
        private Vector3 initialSeedsWorldScale; // TRUE world scale of seeds cached at Awake, accounting for ALL parent bones
        private Transform leftHandBoneRef;
        private Transform rightHandBoneRef;
        private Vector3 seedsShatteredWorldScale = new Vector3(0.1f, 0.1f, 0.1f);

        private struct TransformData
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        private void Awake()
        {
            Debug.Log($"<color=yellow>[AcornProp] Awake initialization started.</color> GameObjectName: {gameObject.name}");
            
            initialHandParent = transform.parent;
            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;
            initialAcornScale = transform.localScale;
            Debug.Log($"<color=yellow>[AcornProp]</color> initialHandParent: {(initialHandParent != null ? initialHandParent.name : "None (Root of Scene)")}, localPosition: {initialLocalPos}, initialScale: {initialAcornScale}");

            Debug.Log($"<color=yellow>[AcornProp]</color> solidMesh: {(solidMesh != null ? solidMesh.name : "NULL")}");
            Debug.Log($"<color=yellow>[AcornProp]</color> brokenMeshParent: {(brokenMeshParent != null ? brokenMeshParent.name : "NULL")}");
            Debug.Log($"<color=yellow>[AcornProp]</color> seedsObject: {(seedsObject != null ? seedsObject.name : "NULL")}");

            // Verify hierarchy setup to prevent child deactivation bugs
            if (brokenMeshParent != null && solidMesh != null && brokenMeshParent.transform.IsChildOf(solidMesh.transform))
            {
                Debug.LogError($"<color=red>[AcornProp] CRITICAL CONFIGURATION ERROR: brokenMeshParent '{brokenMeshParent.name}' is a child of solidMesh '{solidMesh.name}'! Since solidMesh is disabled on hit, the broken pieces will also be hidden and invisible in Unity!</color>");
            }
            if (seedsObject != null && solidMesh != null && seedsObject.transform.IsChildOf(solidMesh.transform))
            {
                Debug.LogError($"<color=red>[AcornProp] CRITICAL CONFIGURATION ERROR: seedsObject '{seedsObject.name}' is a child of solidMesh '{solidMesh.name}'! Since solidMesh is disabled on hit, the seeds will also be hidden and invisible in Unity!</color>");
            }

            if (seedsObject != null) 
            {
                // AUTO-FIX: If seeds are a child of solidMesh, move them out so they don't get hidden when solidMesh deactivates!
                if (solidMesh != null && seedsObject.transform.IsChildOf(solidMesh.transform))
                {
                    Debug.LogWarning("<color=orange>[AcornProp]</color> Auto-fixing: Moving seedsObject out of solidMesh hierarchy to prevent deactivation on hit.");
                    seedsObject.transform.SetParent(solidMesh.transform.parent, true);
                }

                // Force it to be small from the very beginning (increased by 10%)
                seedsObject.transform.localScale = new Vector3(0.11f, 0.11f, 0.11f);

                initialSeedsParent = seedsObject.transform.parent;
                initialSeedsLocalPos = seedsObject.transform.localPosition;
                initialSeedsScale = seedsObject.transform.localScale;

                // CRITICAL: Cache the TRUE world scale NOW, while seeds are still inside
                // the full parent bone hierarchy. This accounts for ALL bone/skeleton scaling
                // and is far more reliable than any formula.
                initialSeedsWorldScale = seedsObject.transform.lossyScale;
                
                seedsObject.SetActive(false);
                Debug.Log($"<color=yellow>[AcornProp]</color> Cached seedsObject. Parent: {(initialSeedsParent != null ? initialSeedsParent.name : "None")}, localPosition: {initialSeedsLocalPos}, localScale: {initialSeedsScale}, WorldScale={initialSeedsWorldScale}");
            }

            // Ensure the main acorn object is kinematic and gravity is disabled so it floats perfectly in the air
            Rigidbody rootRb = GetComponent<Rigidbody>();
            if (rootRb != null)
            {
                rootRb.isKinematic = true;
                rootRb.useGravity = false;
            }

            // Ensure the broken pieces are hidden at the start to save performance
            if (brokenMeshParent != null)
            {
                if (brokenMeshParent.isStatic)
                {
                    Debug.LogWarning($"<color=yellow>[AcornProp] WARNING: brokenMeshParent '{brokenMeshParent.name}' is marked as STATIC in the editor! Deactivating static status so physics works.</color>");
                    brokenMeshParent.isStatic = false;
                }

                initialParentScale = brokenMeshParent.transform.localScale;
                // No zero scale check
                
                // CRITICAL FIX: Do NOT deactivate brokenMeshParent yet! 
                // If we add MeshColliders while it is inactive, Unity defers cooking the convex hulls until the acorn is smashed,
                // causing a massive lag spike during gameplay. We leave it active while adding colliders so they cook during load!
                
                // AUTO-FIX: Automatically add Rigidbody and MeshCollider to any broken pieces that don't have them!
                // This saves the user from having to manually configure 50 physics objects.
                MeshFilter[] filters = brokenMeshParent.GetComponentsInChildren<MeshFilter>(true);
                Debug.Log($"<color=yellow>[AcornProp]</color> Found {filters.Length} MeshFilters inside brokenMeshParent '{brokenMeshParent.name}'.");
                
                System.Collections.Generic.List<Rigidbody> rbs = new System.Collections.Generic.List<Rigidbody>();
                int staticCount = 0;
                
                foreach (MeshFilter f in filters)
                {
                    if (f.gameObject.isStatic)
                    {
                        staticCount++;
                        Debug.LogWarning($"<color=yellow>[AcornProp] WARNING:</color> Piece '{f.gameObject.name}' is marked as STATIC in the editor! Deactivating static status so physics works.");
                        f.gameObject.isStatic = false; // Force it to non-static!
                    }

                    Rigidbody rb = f.GetComponent<Rigidbody>();
                    if (rb == null) rb = f.gameObject.AddComponent<Rigidbody>();
                    
                    Collider col = f.GetComponent<Collider>();
                    
                    // PERFORMANCE FIX (Lag Spike Eradicator): 
                    // To completely eliminate the massive VR freeze that occurs on the very first hit,
                    // we MUST use BoxColliders instead of MeshColliders for the shattered pieces.
                    // Unity's MeshCollider convex hull cooking blocks the main thread for 50+ objects!
                    if (col != null && col is MeshCollider) 
                    { 
                        DestroyImmediate(col); 
                        col = null; 
                    }
                    
                    if (col == null && f.GetComponent<BoxCollider>() == null)
                    {
                        col = f.gameObject.AddComponent<BoxCollider>();
                    }
                    
                    rb.isKinematic = true; // Ensure they don't fall apart early!
                    f.gameObject.AddComponent<DeactivateIfBelowTable>();
                    rbs.Add(rb);
                }
                
                brokenPiecesRigidbodies = rbs.ToArray();
                Debug.Log($"<color=yellow>[AcornProp]</color> Configured {brokenPiecesRigidbodies.Length} rigidbodies. (Found static pieces: {staticCount})");
                
                // Cache the exact initial positions of all pieces so we can reset them later for Object Pooling!
                initialPieceTransforms = new TransformData[brokenPiecesRigidbodies.Length];
                for (int i = 0; i < brokenPiecesRigidbodies.Length; i++)
                {
                    Rigidbody rb = brokenPiecesRigidbodies[i];
                    initialPieceTransforms[i] = new TransformData
                    {
                        localPosition = rb.transform.localPosition,
                        localRotation = rb.transform.localRotation
                    };
                }

                // Now that all colliders are added and baked, we can safely deactivate the parent
                brokenMeshParent.SetActive(false);
                Debug.Log($"<color=yellow>[AcornProp]</color> Deactivated brokenMeshParent '{brokenMeshParent.name}' after baking colliders.");
            }
            else
            {
                Debug.LogError("<color=red>[AcornProp] ERROR: brokenMeshParent is not assigned in the inspector!</color>");
            }
        }

        private void OnDisable()
        {
            if (seedsObject != null)
            {
                seedsObject.SetActive(false);
            }
        }

        // This is called automatically by your HandHammer.cs script!
        public void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            Debug.Log($"<color=orange>[AcornProp]</color> OnHit received! Velocity: {velocity.magnitude:F3}");
            if (isBroken) 
            {
                Debug.Log("<color=orange>[AcornProp]</color> Already broken, ignoring hit.");
                return;
            }

            // Ignore light bumps (e.g., if it spawns into a resting hammer)
            if (velocity.magnitude < minHitVelocity)
            {
                Debug.LogWarning($"<color=orange>[AcornProp]</color> Hit velocity ({velocity.magnitude:F3}) too low to break! Needs {minHitVelocity}");
                return;
            }

            Debug.Log("<color=red>[AcornProp] SHATTERING!</color>");
            isBroken = true;

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(10, velocity.magnitude); // Standard acorn = 10 points
            }

            // CRITICAL FIX: Since the acorn was detached from the squirrel during the throw, 
            // SendMessageUpwards will fail! We must use the direct Action delegate!
            if (OnShattered != null)
            {
                OnShattered.Invoke();
            }

            Shatter(hitPosition);
        }

        private void Shatter(Vector3 hitPosition)
        {
            Debug.Log($"<color=red>[AcornProp] Shatter method called!</color> hitPosition: {hitPosition}");
            
            // 1. Swap the meshes
            if (solidMesh != null) 
            {
                if (solidMesh == gameObject)
                {
                    // Programmatic safety: do NOT deactivate the root GameObject! Just turn off its visual and physical components.
                    Renderer r = GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                    Collider c = GetComponent<Collider>();
                    if (c != null) c.enabled = false;
                    Debug.LogWarning("<color=orange>[AcornProp] WARNING: solidMesh was assigned to root GameObject. Disabled root Renderer and Collider instead of deactivating GameObject to prevent stopping the script.</color>");
                }
                else
                {
                    solidMesh.SetActive(false);
                    Debug.Log($"<color=red>[AcornProp]</color> Deactivated solidMesh '{solidMesh.name}'");
                }
            }
            else
            {
                Debug.LogWarning("<color=orange>[AcornProp]</color> solidMesh is null during Shatter.");
            }

            // Fallback/Safety: Also disable root Renderer and Collider on the script holder itself 
            // to guarantee the main solid acorn goes invisible.
            Renderer rootRenderer = GetComponent<Renderer>();
            if (rootRenderer != null && rootRenderer.enabled)
            {
                rootRenderer.enabled = false;
                Debug.Log("<color=red>[AcornProp]</color> Disabled root Renderer component on hit.");
            }
            Collider rootCollider = GetComponent<Collider>();
            if (rootCollider != null && rootCollider.enabled)
            {
                rootCollider.enabled = false;
                Debug.Log("<color=red>[AcornProp]</color> Disabled root Collider component on hit.");
            }

            // 2. Activate the broken version
            if (brokenMeshParent != null) 
            {
                // FORCE the broken pieces to be the EXACT same transform as the solid mesh!
                if (solidMesh != null)
                {
                    brokenMeshParent.transform.localPosition = solidMesh.transform.localPosition;
                    brokenMeshParent.transform.localRotation = solidMesh.transform.localRotation;
                    brokenMeshParent.transform.localScale = solidMesh.transform.localScale;
                }
                else
                {
                    brokenMeshParent.transform.localScale = initialParentScale;
                }
                
                brokenMeshParent.SetActive(true);
                Debug.Log($"<color=red>[AcornProp]</color> Activated brokenMeshParent '{brokenMeshParent.name}'. LocalScale is {brokenMeshParent.transform.localScale}, position: {brokenMeshParent.transform.position}, ActiveInHierarchy: {brokenMeshParent.activeInHierarchy}");
                if (!brokenMeshParent.activeInHierarchy)
                {
                    Debug.LogError($"<color=red>[AcornProp] CRITICAL WARNING: brokenMeshParent '{brokenMeshParent.name}' activeSelf is true but activeInHierarchy is FALSE! A parent GameObject of this object is disabled, making it completely invisible!</color>");
                }
                
                // CRITICAL BUGFIX: Prevent broken acorn pieces from hitting the player!
                CollisionIsolator.IsolateRigidbodies(brokenPiecesRigidbodies);
            }
            else
            {
                Debug.LogError("<color=red>[AcornProp] ERROR:</color> brokenMeshParent is null during Shatter!");
            }

            // 1b. Reveal the seeds!
            if (seedsObject != null) 
            {
                seedsObject.transform.SetParent(null, true);
                Vector3 handScale = initialHandParent != null ? initialHandParent.lossyScale : Vector3.one;
                seedsObject.transform.localScale = new Vector3(0.11f * handScale.x, 0.11f * handScale.y, 0.11f * handScale.z);
                seedsShatteredWorldScale = seedsObject.transform.localScale; // Cache ground scale!
                seedsObject.SetActive(true);
                Debug.Log($"<color=red>[AcornProp]</color> Activated seedsObject '{seedsObject.name}' at ground scale {seedsShatteredWorldScale}. ActiveInHierarchy: {seedsObject.activeInHierarchy}");
                if (!seedsObject.activeInHierarchy)
                {
                    Debug.LogError($"<color=red>[AcornProp] CRITICAL WARNING: seedsObject '{seedsObject.name}' activeSelf is true but activeInHierarchy is FALSE! A parent GameObject of this object is disabled, making it completely invisible!</color>");
                }
            }
            else
            {
                Debug.LogWarning("<color=orange>[AcornProp]</color> seedsObject is null during Shatter.");
            }

            // Notify the parent character that the target was successfully destroyed!
            if (OnTargetDestroyed != null)
            {
                OnTargetDestroyed.Invoke();
                Debug.Log("<color=red>[AcornProp]</color> Fired OnTargetDestroyed event.");
            }

            // 2. Spawn dust/splinters and play sound
            if (hitParticlesPrefab != null)
            {
                // PERFORMANCE OPTIMIZATION: Dynamically pool the VFX to prevent Garbage Collection spikes!
                // FIX: Spawn perfectly at the center of the acorn (transform.position) to hide the jagged breaks!
                GameObject newVFX = ObjectPooler.Instance.SpawnOrAddPool(hitParticlesPrefab.name, hitParticlesPrefab, 5, transform.position, Quaternion.identity);
                // Make the puff 50% larger to fully obscure the initial shatter!
                newVFX.transform.localScale = transform.lossyScale * 1.5f;
                ParticleSystem mainPS = newVFX.GetComponent<ParticleSystem>();
                if (mainPS != null) mainPS.Play(true);
                
                ObjectPooler.Instance.ReturnToPool(newVFX, 3f);
                Debug.Log($"<color=red>[AcornProp]</color> Spawned pooled hit particles: '{hitParticlesPrefab.name}'");
            }
            else
            {
                Debug.LogWarning("<color=orange>[AcornProp]</color> hitParticlesPrefab is null during Shatter.");
            }

            if (shatterSound != null)
            {
                AudioSource.PlayClipAtPoint(shatterSound, hitPosition);
                Debug.Log($"<color=red>[AcornProp]</color> Playing shatterSound: '{shatterSound.name}'");
            }
            else
            {
                Debug.LogWarning("<color=orange>[AcornProp]</color> shatterSound is null during Shatter.");
            }

            // 3. Apply physics explosion to all pieces instantly
            if (brokenPiecesRigidbodies != null && brokenPiecesRigidbodies.Length > 0)
            {
                Debug.Log($"<color=red>[AcornProp]</color> Applying physics explosion to {brokenPiecesRigidbodies.Length} rigidbodies...");
                int appliedCount = 0;
                foreach (Rigidbody rb in brokenPiecesRigidbodies)
                {
                    if (rb != null)
                    {
                        rb.isKinematic = false; // CRITICAL FIX: They must be non-kinematic to explode!
                        rb.useGravity = true;
                        
                        // GUARANTEED PERFECT EXPLOSION: Ignore the hammer's chaotic hit position!
                        // Calculate a perfect uniform outward burst from the exact center of the acorn.
                        Vector3 outwardDir = (rb.transform.position - transform.position).normalized;
                        if (outwardDir == Vector3.zero) outwardDir = Random.onUnitSphere;
                        
                        // Force a beautiful, consistent arc and massive randomized spin to blur jagged edges
                        rb.linearVelocity = outwardDir * 3f + Vector3.up * 3f;
                        rb.angularVelocity = new Vector3(Random.Range(-30f, 30f), Random.Range(-30f, 30f), Random.Range(-30f, 30f));
                        appliedCount++;
                    }
                }
                Debug.Log($"<color=red>[AcornProp]</color> Done applying velocity to {appliedCount} active rigidbodies.");
            }
            else
            {
                Debug.LogWarning("<color=orange>[AcornProp]</color> brokenPiecesRigidbodies array is empty or null! No explosion applied.");
            }

            // 4. Start the performance cleanup routine
            StartCoroutine(CleanupRoutine());
        }

        private IEnumerator CleanupRoutine()
        {
            // Wait for the pieces to fly and settle on the ground
            yield return new WaitForSeconds(timeBeforeCleanup);

            if (brokenMeshParent == null) yield break;

            Vector3 initialScale = brokenMeshParent.transform.localScale;
            float elapsed = 0f;

            // Smoothly shrink all pieces down to 0 over 1 second so they don't pop out abruptly
            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shrinkDuration;
                brokenMeshParent.transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
                yield return null;
            }

            // Just hide the broken pieces, NOT the entire gameObject!
            // The parent SquirrelCharacter still needs this object alive.
            brokenMeshParent.SetActive(false);
        }

        /// <summary>
        /// Call this when the Squirrel gets pulled from the Object Pool to respawn.
        /// It perfectly reassembles the broken acorn and puts it back in the hand!
        /// </summary>
        public void ResetProp()
        {
            Debug.Log("<color=yellow>[AcornProp] ResetProp called!</color>");
            isBroken = false;
            isThrown = false;
            gameObject.SetActive(true);

            // Re-enable root Renderer and Collider components if they exist
            Renderer rootRenderer = GetComponent<Renderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = true;
                Debug.Log("<color=yellow>[AcornProp]</color> Re-enabled root Renderer component.");
            }
            Collider rootCollider = GetComponent<Collider>();
            if (rootCollider != null)
            {
                rootCollider.enabled = true;
                Debug.Log("<color=yellow>[AcornProp]</color> Re-enabled root Collider component.");
            }

            // Put it back in the hand!
            if (initialHandParent != null)
            {
                transform.SetParent(initialHandParent);
                transform.localPosition = initialLocalPos;
                transform.localRotation = initialLocalRot;
                transform.localScale = initialAcornScale; // Reset scale
                Debug.Log($"<color=yellow>[AcornProp]</color> Re-parented to hand: '{initialHandParent.name}'. LocalPos: {initialLocalPos}");
            }
            else
            {
                Debug.LogWarning("<color=orange>[AcornProp] ResetProp warning: initialHandParent is NULL!</color>");
            }

            if (solidMesh != null) 
            {
                if (solidMesh == gameObject)
                {
                    // Already handled by the root Renderer/Collider above
                }
                else
                {
                    solidMesh.SetActive(true);
                    Debug.Log($"<color=yellow>[AcornProp]</color> Re-activated solidMesh '{solidMesh.name}'");
                }
            }
            
            if (seedsObject != null) 
            {
                // Detach from hands, return to initial parent and reset transform/variables
                seedsObject.transform.SetParent(null, false);
                seedsObject.transform.SetParent(initialSeedsParent);
                seedsObject.transform.localPosition = initialSeedsLocalPos;
                seedsObject.transform.localScale = initialSeedsScale;
                seedsObject.SetActive(false);
                seedsPickedUp = false;
                leftHandBoneRef = null;
                rightHandBoneRef = null;
                Debug.Log($"<color=yellow>[AcornProp]</color> Reset seedsObject parent and deactivated.");
            }
            
            if (brokenMeshParent != null)
            {
                brokenMeshParent.SetActive(false);
                brokenMeshParent.transform.localScale = initialParentScale;
                Debug.Log($"<color=yellow>[AcornProp]</color> Deactivated brokenMeshParent and reset scale to {initialParentScale}");

                if (brokenPiecesRigidbodies != null)
                {
                    // Snap all 50 physics pieces back together like a puzzle!
                    for (int i = 0; i < brokenPiecesRigidbodies.Length; i++)
                    {
                        Rigidbody rb = brokenPiecesRigidbodies[i];
                        if (rb != null)
                        {
                            if (!rb.isKinematic)
                            {
                                rb.linearVelocity = Vector3.zero;
                                rb.angularVelocity = Vector3.zero;
                                rb.isKinematic = true; // Lock them back together!
                            }
                            rb.transform.localPosition = initialPieceTransforms[i].localPosition;
                            rb.transform.localRotation = initialPieceTransforms[i].localRotation;
                            
                            // Reactivate the child piece GameObject in case it fell off the table and was deactivated
                            rb.gameObject.SetActive(true);
                        }
                    }
                    Debug.Log($"<color=yellow>[AcornProp]</color> Reset positions and velocities of {brokenPiecesRigidbodies.Length} broken piece rigidbodies.");
                }
            }
        }

        public void ThrowForward()
        {
            if (isThrown) return;
            isThrown = true;
            StartCoroutine(ThrowRoutine());
        }

        private IEnumerator ThrowRoutine()
        {
            // Get the squirrel character to find the table/ground surface Y position BEFORE detaching from hand
            SquirrelCharacter squirrel = GetComponentInParent<SquirrelCharacter>();
            float tableWorldY = transform.position.y; // Fallback
            if (squirrel != null)
            {
                tableWorldY = squirrel.transform.position.y;
            }

            // Detach from the squirrel hand
            transform.SetParent(null);
            
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation; // It is currently slanted from being in the hand
            
            // The squirrel should throw the acorn exactly forward relative to herself
            Vector3 throwDirection = squirrel != null ? squirrel.transform.forward : transform.forward;
            Vector3 squirrelCenter = squirrel != null ? squirrel.transform.position : startPos;

            // Target rotation: upright, facing forward, but with a natural tilt so it looks like it lies on the ground
            // We rotate by exactly 60 degrees on the X-axis, roll by -15 to 15 degrees on Z-axis, and exactly 90 degrees on Y-axis
            float randomTiltX = 60f;
            if (Random.value > 0.5f) randomTiltX = -60f; // tilt forward or backward
            float randomTiltZ = Random.Range(-15f, 15f);
            float randomTiltY = 90f; // Exactly 90 degrees on Y axis
            Quaternion tiltRotation = Quaternion.Euler(randomTiltX, randomTiltY, randomTiltZ);
            Quaternion targetRot = Quaternion.LookRotation(throwDirection) * tiltRotation;

            // Throw it exactly 0.25m forward horizontally from the SQUIRREL'S CENTER.
            Vector3 targetPos = squirrelCenter + (throwDirection * 0.25f); 
            
            // We shift it very slightly to the left so the whole body visually centers on the squirrel.
            // Reduced to 0.02f to shift it slightly back to the right to perfect the alignment.
            if (squirrel != null)
            {
                targetPos -= squirrel.transform.right * 0.02f;
            }

            targetPos.y = tableWorldY + 0.11f; // Lifted more to prevent clipping into ground
            
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = startScale * 2.4f; // Decreased by 20% (from 3.0x to 2.4x)

            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Add a small arc to the throw using Sin
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * 0.2f; 

                transform.position = currentPos;
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                
                // Smoothly twist it so it lands perfectly straight!
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                
                yield return null;
            }

            transform.position = targetPos;
            transform.localScale = targetScale;
            transform.rotation = targetRot;
        }

        public void MoveSeedsToTarget(Vector3 targetPos, float duration, bool useArc = true)
        {
            if (seedsObject != null && seedsObject.activeSelf)
            {
                StartCoroutine(MoveRoutine(seedsObject.transform, targetPos, duration, useArc));
            }
        }

        public void MoveAcornToTarget(Vector3 targetPos, float duration, bool useArc = true)
        {
            if (isThrown && !isBroken)
            {
                StartCoroutine(MoveRoutine(transform, targetPos, duration, useArc));
            }
        }

        private IEnumerator MoveRoutine(Transform objToMove, Vector3 targetPos, float duration, bool useArc)
        {
            Debug.Log($"<color=magenta>[AcornProp]</color> MoveRoutine START. objToMove={objToMove.name}. WorldScale={objToMove.lossyScale}, LocalScale={objToMove.localScale}");
            
            objToMove.SetParent(null, true);
            
            Vector3 startPos = objToMove.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
                if (useArc) 
                {
                    currentPos.y += Mathf.Sin(t * Mathf.PI) * 0.1f;
                }
                
                objToMove.position = currentPos;
                yield return null;
            }
            objToMove.position = targetPos;
        }

        /// <summary>
        /// Animates the seeds smoothly from the table into the middle of both hands
        /// </summary>
        public void AnimateSeedsToHands(Transform leftHand, Transform rightHand)
        {
            if (seedsObject == null) 
            {
                Debug.LogError("<color=red>[AcornProp] AnimateSeedsToHands FAILED: seedsObject is null!");
                return;
            }
            if (leftHand == null)
            {
                Debug.LogError("<color=red>[AcornProp] AnimateSeedsToHands FAILED: leftHand is null! Assign leftHandBone in Inspector.");
                return;
            }

            seedsPickedUp = true;
            leftHandBoneRef = leftHand;
            rightHandBoneRef = rightHand;
            seedsObject.SetActive(true);

            // Parent to the squirrel root so it shrinks when the squirrel shrinks!
            Transform squirrelRoot = leftHand.GetComponentInParent<BaseMole>()?.transform;
            if (squirrelRoot != null)
            {
                seedsObject.transform.SetParent(squirrelRoot, true);
                // The seeds' world scale will be maintained by the true flag, but since 
                // we want it to perfectly match seedsShatteredWorldScale on the ground,
                // we calculate the correct local scale based on the squirrel root's current scale.
                seedsObject.transform.localScale = new Vector3(
                    seedsShatteredWorldScale.x / squirrelRoot.lossyScale.x,
                    seedsShatteredWorldScale.y / squirrelRoot.lossyScale.y,
                    seedsShatteredWorldScale.z / squirrelRoot.lossyScale.z
                );
            }
            else
            {
                seedsObject.transform.SetParent(null, false);
                seedsObject.transform.localScale = seedsShatteredWorldScale;
            }

            // Snap immediately to midpoint so there is no 1-frame pop
            if (rightHand != null)
            {
                seedsObject.transform.position = (leftHand.position + rightHand.position) / 2f;
            }
            else
            {
                seedsObject.transform.position = leftHand.position;
            }

            Debug.Log($"<color=lime>[AcornProp]</color> Seeds placed at hand midpoint. Scale={seedsShatteredWorldScale}");
        }

        private void LateUpdate()
        {
            if (seedsPickedUp && seedsObject != null)
            {
                if (leftHandBoneRef != null && rightHandBoneRef != null)
                {
                    Vector3 midpointWorld = (leftHandBoneRef.position + rightHandBoneRef.position) / 2f;

                    // Shift slightly down and forward so seeds sit IN the hands
                    Transform squirrelRoot = leftHandBoneRef.GetComponentInParent<BaseMole>()?.transform;
                    Vector3 forward = squirrelRoot != null ? squirrelRoot.forward : Vector3.forward;

                    midpointWorld += Vector3.down * 0.015f;      // slightly down (raised from 0.04f)
                    midpointWorld += forward * 0.03f;            // a little forward

                    seedsObject.transform.position = midpointWorld;
                }
                else if (leftHandBoneRef != null)
                {
                    seedsObject.transform.position = leftHandBoneRef.position;
                }
            }

            // Always ensure the Z-axis (blue arrow) of the nuts faces the player
            // This is extremely lightweight and will NOT affect performance.
            if (seedsObject != null && seedsObject.activeSelf && Camera.main != null)
            {
                Vector3 toCam = Camera.main.transform.position - seedsObject.transform.position;
                toCam.y = 0; // Keep it perfectly flat horizontally
                if (toCam.sqrMagnitude > 0.001f)
                {
                    seedsObject.transform.rotation = Quaternion.LookRotation(toCam);
                }
            }
        }

        /// <summary>
        /// Animates the UNBROKEN acorn smoothly back into the hands if the player ignores it!
        /// </summary>
        public void AnimateAcornToHands(Transform leftHand, Transform rightHand)
        {
            if (isThrown && !isBroken)
            {
                StartCoroutine(FollowHandsRoutine(transform, leftHand, rightHand, initialAcornScale));
            }
        }

        private IEnumerator FollowHandsRoutine(Transform objToMove, Transform leftHand, Transform rightHand, Vector3? targetScale = null)
        {
            Debug.Log($"<color=yellow>[AcornProp]</color> FollowHandsRoutine START. objToMove={objToMove.name}.");

            // Cache starting world position and rotation (which is tilted on the ground)
            Vector3 startWorldPos = objToMove.position;
            Quaternion startWorldRot = objToMove.rotation;

            // Parent to the squirrel's root transform (not the hand bone) to prevent Animator bone rotations from slanting it!
            Transform squirrelRoot = leftHand.GetComponentInParent<BaseMole>()?.transform;
            if (squirrelRoot != null)
            {
                objToMove.SetParent(squirrelRoot, true);
            }
            else
            {
                objToMove.SetParent(leftHand, true); // Fallback
            }
            
            Vector3 startScale = objToMove.localScale;       // Capture AFTER parenting!
            Vector3 finalScale = targetScale ?? startScale;

            float scaleDuration = 0.2f;
            float elapsed = 0f;

            // Smoothly move to the middle of both hands while Slerping the rotation back to the hand orientation
            while (objToMove != null && objToMove.gameObject.activeInHierarchy)
            {
                Vector3 middleHandPos = (leftHand.position + rightHand.position) / 2f;

                // Calculate the target world rotation based on the hand bone and the cached initial local rotation
                Quaternion targetHandWorldRot = initialHandParent != null ? (initialHandParent.rotation * initialLocalRot) : Quaternion.identity;

                if (elapsed < scaleDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / scaleDuration;
                    
                    objToMove.position = Vector3.Lerp(startWorldPos, middleHandPos, t);
                    if (targetScale.HasValue) objToMove.localScale = Vector3.Lerp(startScale, finalScale, t);
                    
                    // Smoothly transition from the landing tilt rotation to the hand's current holding rotation
                    objToMove.rotation = Quaternion.Slerp(startWorldRot, targetHandWorldRot, t);
                }
                else
                {
                    objToMove.position = middleHandPos;
                    if (targetScale.HasValue) objToMove.localScale = finalScale;
                    objToMove.rotation = targetHandWorldRot;
                }
                
                yield return null;
            }
        }
    }
}
