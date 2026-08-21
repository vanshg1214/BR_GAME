using UnityEngine;

namespace ArcRoll.Gameplay.Frisbee
{
    public enum FrisbeeTargetType
    {
        Can,
        Balloon
    }

    [RequireComponent(typeof(Rigidbody))]
    public class FrisbeeTarget : MonoBehaviour
    {
        [Header("Target Configuration")]
        [Tooltip("Choose whether this acts like a physics Can or a popping Balloon.")]
        public FrisbeeTargetType targetType = FrisbeeTargetType.Can;

        [Header("Effects")]
        [Tooltip("Optional: Drag an empty GameObject here. If assigned, the VFX will spawn exactly at this object's position and become its child.")]
        [SerializeField] private Transform vfxSpawnPoint;
        
        [SerializeField] private ParticleSystem hitParticles;
        [SerializeField] private AudioSource hitAudio;
        [Tooltip("Impact sounds for hitting cans or drones.")]
        [SerializeField] private AudioClip[] impactSounds;

        // Legacy field kept for backwards compatibility during upgrade
        [HideInInspector]
        [SerializeField] private bool hideMeshOnHit = false;

        private void OnValidate()
        {
            // Seamless upgrade path for existing prefabs
            if (hideMeshOnHit)
            {
                targetType = FrisbeeTargetType.Balloon;
                hideMeshOnHit = false;
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }

        [Header("Fractured Balloon Settings")]
        [Tooltip("The intact visual GameObject (e.g. 'Balloon') that will be hidden on hit.")]
        [SerializeField] private GameObject intactVisuals;
        [Tooltip("The fractured group GameObject (e.g. 'Broken Ballon') that will be activated and exploded on hit.")]
        [SerializeField] private GameObject brokenVisuals;
        [SerializeField] private float explosionForce = 6f;
        [SerializeField] private float explosionRadius = 1.5f;
        [SerializeField] private float explosionUpwardModifier = 0.5f;
        [Tooltip("Scale multiplier for the shards. Reduces the size of the exploded shards (e.g. 0.1 is 10x smaller).")]
        [SerializeField] private float shardScaleMultiplier = 0.1f;

        [Header("Scoring")]
        [Tooltip("How many points this specific target is worth when successfully knocked down or popped.")]
        public int scoreValue = 1;
        
        [Tooltip("If true, balloon scores will be auto-calculated (1st row = 3, 2nd row = 5) based on height. Cans will only count if they hit the floor.")]
        public bool useAdvancedScoringRules = true;

        private FrisbeeFormation formation = null;
        private bool _isKnockedDown = false;
        private bool _hitEffectsPlayed = false;
        private float lastSoundTime = 0f;

        public bool IsKnockedDown => _isKnockedDown;

        private void Awake()
        {
            if (hitAudio == null) hitAudio = GetComponent<AudioSource>();
            if (hitAudio == null) hitAudio = gameObject.AddComponent<AudioSource>();

            if (hitAudio != null)
            {
                hitAudio.spatialBlend = 1.0f;
                hitAudio.playOnAwake = false;
            }

            // Auto-calculate balloon scores based on height
            if (useAdvancedScoringRules && targetType == FrisbeeTargetType.Balloon)
            {
                // Y > 1.8f is typically the "second row" above the player's natural chest level / bowling pin level
                if (transform.position.y > 1.8f)
                    scoreValue = 5;
                else
                    scoreValue = 3;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            if (brokenVisuals != null)
            {
                brokenVisuals.SetActive(false);
            }
        }

        public void SetFormation(FrisbeeFormation f)
        {
            formation = f;
        }

        private void Update()
        {
            if (_isKnockedDown) return;

            if (targetType == FrisbeeTargetType.Balloon) return; // Hovering targets (balloons) are popped instantly on collision, not by Update

            if (useAdvancedScoringRules)
            {
                // Advanced rule: Tin cans MUST fall off the table/pyramid and touch the floor (Y < 0.3) to count!
                if (transform.position.y < 0.3f || Vector3.Angle(transform.up, Vector3.up) > 75f && transform.position.y < 0.6f)
                {
                    _isKnockedDown = true;
                    PlayHitEffects();
                }
            }
            else
            {
                // Standard rule: A target is knocked down if it tips over 45 degrees, even if still on the table
                if (Vector3.Angle(transform.up, Vector3.up) > 45f)
                {
                    _isKnockedDown = true;
                    PlayHitEffects();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Advanced rule fallback: If it physically touches an object named "Floor", count it as knocked down!
            if (useAdvancedScoringRules && !_isKnockedDown && targetType != FrisbeeTargetType.Balloon)
            {
                if (collision.gameObject.name.ToLower().Contains("floor") || collision.gameObject.CompareTag("Floor"))
                {
                    _isKnockedDown = true;
                    PlayHitEffects();
                }
            }
            // Check if it's a Frisbee via script component or tag
            Frisbee frisbee = collision.gameObject.GetComponentInParent<Frisbee>();
            bool isFrisbee = frisbee != null
                || collision.gameObject.CompareTag("Frisbee")
                || (collision.transform.root != null && collision.transform.root.CompareTag("Frisbee"));

            // Play the impact sound: force full volume if it is a balloon pop (targetType == FrisbeeTargetType.Balloon)
            bool isPop = isFrisbee && targetType == FrisbeeTargetType.Balloon;
            PlayDynamicImpactSound(collision.relativeVelocity.magnitude, isPop);

            if (isFrisbee)
            {
                HandleFrisbeeHit(frisbee);
            }
        }

        // Secondary detection path: fires when balloon collider is set as a Trigger.
        // Unity only calls one of these (Collision or Trigger) based on the isTrigger flag,
        // so having both guarantees detection regardless of the balloon's setup in the prefab!
        private void OnTriggerEnter(Collider other)
        {
            if (_isKnockedDown) return; // Already handled

            Frisbee frisbee = other.GetComponentInParent<Frisbee>();
            bool isFrisbee = frisbee != null
                || other.CompareTag("Frisbee")
                || (other.transform.root != null && other.transform.root.CompareTag("Frisbee"));

            if (isFrisbee)
            {
                // Play pop sound at full volume via trigger path too
                PlayDynamicImpactSound(3.0f, targetType == FrisbeeTargetType.Balloon);
                HandleFrisbeeHit(frisbee);
            }
        }

        private void HandleFrisbeeHit(Frisbee frisbee)
        {
            _isKnockedDown = true;
            if (formation != null)
            {
                formation.NotifyFrisbeeTouched(frisbee);
            }
            PlayHitEffects();
        }

        private void PlayHitEffects()
        {
            if (_hitEffectsPlayed) return;
            _hitEffectsPlayed = true;

            // Get the actual visual center of the balloon using its collider bounds (handles offsets in GLB)
            Collider col = GetComponent<Collider>();
            if (col == null) col = GetComponentInChildren<Collider>();
            Vector3 centerPos = (col != null) ? col.bounds.center : transform.position;

            if (hitParticles != null)
            {
                Vector3 spawnPos = centerPos;
                Quaternion spawnRot = Quaternion.identity;

                if (vfxSpawnPoint != null)
                {
                    // Use the exact position and rotation of the spawn point
                    spawnPos = vfxSpawnPoint.position;
                    spawnRot = vfxSpawnPoint.rotation;
                }
                else
                {
                    bool isPrefab = hitParticles.gameObject.scene.rootCount == 0;
                    if (!isPrefab)
                    {
                        spawnPos = hitParticles.transform.position;
                        spawnRot = hitParticles.transform.rotation;
                    }
                }

                // Instantiate in world space (no parent) so it doesn't get hidden when the balloon mesh is disabled
                ParticleSystem psInstance = Instantiate(hitParticles, spawnPos, spawnRot);
                
                // Copy the prefab's localScale directly so it scales exactly as in the prefab
                psInstance.transform.localScale = hitParticles.transform.localScale;
                
                psInstance.gameObject.SetActive(true);
                psInstance.Play(true); // play with all child particle systems

                // DEBUG: Search and destroy any auto-destruction or cleanup scripts on the VFX clone.
                // Asset packs like Cartoon FX (CFXR) have built-in scripts that automatically hide/destroy the VFX.
                MonoBehaviour[] scripts = psInstance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var s in scripts)
                {
                    if (s == null) continue;
                    string sName = s.GetType().Name.ToLower();
                    if (sName.Contains("destruct") || sName.Contains("destroy") || sName.Contains("cleanup") || sName.Contains("effect"))
                    {
                        Debug.Log($"[FrisbeeTarget] Destroying auto-destruct component '{s.GetType().Name}' to keep VFX in hierarchy.");
                        Destroy(s);
                    }
                }
                
                Debug.Log($"[FrisbeeTarget] VFX spawned at {spawnPos} with scale {psInstance.transform.localScale}");
                
                // DEBUG: Destroy commented out so you can find the VFX in Hierarchy and see where it spawned!
                // float totalDuration = psInstance.main.duration + psInstance.main.startLifetime.constantMax;
                // Destroy(psInstance.gameObject, totalDuration > 0f ? totalDuration : 5f);
                
                // If the user assigned a child object rather than a prefab, hide the original so it doesn't duplicate
                if (hitParticles.gameObject.scene.rootCount != 0)
                {
                    hitParticles.gameObject.SetActive(false);
                }
            }
            
            if (targetType == FrisbeeTargetType.Balloon)
            {
                // Disable physics on this parent target object so it doesn't drop due to gravity
                Rigidbody parentRb = GetComponent<Rigidbody>();
                if (parentRb == null) parentRb = GetComponentInParent<Rigidbody>();
                if (parentRb != null)
                {
                    parentRb.isKinematic = true;
                    parentRb.useGravity = false;
                    parentRb.linearVelocity = Vector3.zero;
                    parentRb.angularVelocity = Vector3.zero;
                }

                if (brokenVisuals != null)
                {
                    // Match the local scale of the broken balloon group to the intact balloon visuals, scaled down by our multiplier
                    if (intactVisuals != null)
                    {
                        brokenVisuals.transform.localScale = intactVisuals.transform.localScale * shardScaleMultiplier;
                    }

                    // Hide the intact balloon mesh
                    if (intactVisuals != null) intactVisuals.SetActive(false);

                    // Show the fractured balloon pieces
                    brokenVisuals.SetActive(true);

                    // Explode all fractured shards outward from the center of the balloon
                    Transform[] childTransforms = brokenVisuals.GetComponentsInChildren<Transform>(true);
                    Vector3 explosionPos = centerPos;

                    // We collect the pieces we want to explode
                    System.Collections.Generic.List<Rigidbody> rigidbodiesToExplode = new System.Collections.Generic.List<Rigidbody>();

                    foreach (Transform t in childTransforms)
                    {
                        // Skip the root container itself
                        if (t == brokenVisuals.transform) continue;

                        // Only process visual mesh cells (ignore helper/empty transforms)
                        if (t.GetComponent<MeshFilter>() == null) continue;

                        // 1. Ensure it has a Collider so it bounces off the floor
                        Collider c = t.GetComponent<Collider>();
                        if (c == null)
                        {
                            MeshCollider mc = t.gameObject.AddComponent<MeshCollider>();
                            mc.convex = true;
                            c = mc;
                        }
                        c.enabled = true;

                        // 2. Ensure it has a Rigidbody so it responds to physics/gravity
                        Rigidbody rb = t.GetComponent<Rigidbody>();
                        if (rb == null)
                        {
                            rb = t.gameObject.AddComponent<Rigidbody>();
                            rb.mass = 0.1f; // Make them light so they fly away easily!
                        }
                        
                        rigidbodiesToExplode.Add(rb);
                    }

                    // Explode each shard
                    foreach (Rigidbody rb in rigidbodiesToExplode)
                    {
                        if (rb != null)
                        {
                            rb.gameObject.SetActive(true); // Failsafe: ensure the shard GameObject is active
                            rb.isKinematic = false;
                            rb.useGravity = true;
                            
                            // Deparent the shard to prevent it from inheriting the parent's kinematic status
                            rb.transform.SetParent(null, true);
                            
                            // Blow the shard away from the center of the balloon
                            rb.AddExplosionForce(explosionForce, explosionPos, explosionRadius, explosionUpwardModifier, ForceMode.Impulse);
 
                            // Add the cleanup component to smoothly shrink and remove the piece
                            var cleanup = rb.gameObject.GetComponent<ShardCleanup>();
                            if (cleanup == null)
                            {
                                cleanup = rb.gameObject.AddComponent<ShardCleanup>();
                            }
                            cleanup.delay = Random.Range(2.0f, 4.0f);
                        }
                    }
                }
                else
                {
                    // Fallback: hide all mesh renderers if no broken visuals are assigned
                    Renderer[] renderers = GetComponentsInChildren<Renderer>();
                    foreach (Renderer r in renderers)
                    {
                        r.enabled = false;
                    }
                }
                
                // Disable collision so the frisbee passes through the popped balloon
                Collider[] colliders = GetComponentsInChildren<Collider>();
                foreach (Collider c in colliders)
                {
                    // Critical: Do NOT disable the colliders of the broken shards, otherwise they'll fall through the ground!
                    if (brokenVisuals == null || !c.transform.IsChildOf(brokenVisuals.transform))
                    {
                        c.enabled = false;
                    }
                }
            }
        }

        private void PlayDynamicImpactSound(float velocity, bool forceFullVolume = false)
        {
            if (impactSounds == null || impactSounds.Length == 0) return;
            if (Time.time - lastSoundTime < 0.12f) return; // Safe cooldown to prevent overlapping audio blowouts
            if (!forceFullVolume && velocity < 0.3f) return; // Safe threshold to ignore quiet rolling ticks
            
            // Volume ranges safely between 0.4 and 0.8 for normal hits (pop remains 1.0f for crisp success feedback)
            float volume = forceFullVolume ? 1.0f : Mathf.Clamp(velocity / 2.0f, 0.4f, 0.8f);
            AudioClip clip = impactSounds[Random.Range(0, impactSounds.Length)];
            
            PlaySpatialSoundAtPoint(clip, transform.position, volume);
            lastSoundTime = Time.time;
        }

        private void PlaySpatialSoundAtPoint(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return;
            GameObject tempGO = new GameObject("TempSpatialAudio");
            tempGO.transform.position = position;
            
            AudioSource source = tempGO.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1.0f; // 100% 3D spatialized
            source.minDistance = 10.0f; // Increased to 10m so the player hears target impacts at full volume anywhere in the room
            source.maxDistance = 40.0f; // Limit hearing range
            source.rolloffMode = AudioRolloffMode.Linear; // Linear dropoff
            source.pitch = Random.Range(0.85f, 1.15f); // Pitch variation
            source.playOnAwake = false;
            
            source.Play();
            Destroy(tempGO, clip.length + 0.2f);
        }
    }

    /// <summary>
    /// Smoothly shrinks and destroys balloon shard objects after a delay.
    /// </summary>
    public class ShardCleanup : MonoBehaviour
    {
        [HideInInspector] public float delay = 3f;

        private System.Collections.IEnumerator Start()
        {
            yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            float duration = 1.0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / duration);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
