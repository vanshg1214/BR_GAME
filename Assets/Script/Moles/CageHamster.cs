using System.Collections;
using UnityEngine;

namespace WhackAMole
{
    public class CageHamster : BaseMole
    {
        [Header("Cage Hamster Settings")]
        [SerializeField] private Vector3 spawnRotationOffset = new Vector3(-90f, 0, 0);
        [SerializeField] private float heightOffset = -0.015f;
        [SerializeField] private float visualScaleMultiplier = 0.5f;
        [SerializeField] private GameObject cageVisual;
        [SerializeField] private GameObject brokenCageParent;
        [SerializeField] private string happyAnimTrigger = "Happy";
        [SerializeField] private string runAnimBool = "IsRunning";
        [SerializeField] private float rollSpeed = 1.5f;
        [SerializeField] private float cageRotationMultiplier = 50f;
        [SerializeField] private float happyDuration = 2.5f;

        [Header("VFX & Sound")]
        [SerializeField] private GameObject hitParticleVFX;
        [SerializeField] private AudioClip hitSoundFX;

        [Header("Difficulty")]
        [SerializeField] private float velocityThresholdMultiplier = 1.2f;

        private Animator anim;
        private Vector3 initialBrokenCageScale;
        private bool isRolling;
        private Vector3 rollEntryWorldPos;
        private bool isHittable;
        
        private Transform[] brokenPieces;
        private Vector3[] initialBrokenPositions;
        private Quaternion[] initialBrokenRotations;

        protected override void Awake()
        {
            base.Awake();
            anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
            }

            if (brokenCageParent != null)
            {
                initialBrokenCageScale = brokenCageParent.transform.localScale;
                if (initialBrokenCageScale == Vector3.zero)
                {
                    initialBrokenCageScale = Vector3.one;
                }
                
                MeshFilter[] filters = brokenCageParent.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter f in filters)
                {
                    f.gameObject.isStatic = false;
                    Rigidbody rb = f.GetComponent<Rigidbody>();
                    if (rb == null) rb = f.gameObject.AddComponent<Rigidbody>();
                    
                    Collider col = f.GetComponent<Collider>();
                    if (col != null && col is MeshCollider) 
                    { 
                        DestroyImmediate(col); 
                        col = null; 
                    }
                    
                    if (col == null && f.GetComponent<BoxCollider>() == null)
                    {
                        BoxCollider bc = f.gameObject.AddComponent<BoxCollider>();
                        if (f.sharedMesh != null)
                        {
                            bc.center = f.sharedMesh.bounds.center;
                            bc.size = f.sharedMesh.bounds.size;
                        }
                    }
                    rb.isKinematic = true;
                    f.gameObject.AddComponent<DeactivateIfBelowTable>();
                }

                Rigidbody[] rbs = brokenCageParent.GetComponentsInChildren<Rigidbody>(true);
                brokenPieces = new Transform[rbs.Length];
                initialBrokenPositions = new Vector3[rbs.Length];
                initialBrokenRotations = new Quaternion[rbs.Length];

                for (int i = 0; i < rbs.Length; i++)
                {
                    brokenPieces[i] = rbs[i].transform;
                    initialBrokenPositions[i] = rbs[i].transform.localPosition;
                    initialBrokenRotations[i] = rbs[i].transform.localRotation;
                }
            }
        }

        private void Update()
        {
            if (isRolling && cageVisual != null)
            {
                cageVisual.transform.Rotate(Vector3.right * rollSpeed * cageRotationMultiplier * Time.deltaTime, Space.Self);
            }
        }

        protected override bool UsesHoleSpawning => false;

        protected override void OnEnable()
        {
            isHittable = false;
            rollEntryWorldPos = transform.position + Vector3.up * heightOffset;

            base.OnEnable();
            
            spawnOrigin = Vector3.up * heightOffset;
            transform.localScale = originalScale * visualScaleMultiplier;

            if (cageVisual != null) cageVisual.SetActive(true);
            
            if (brokenCageParent != null)
            {
                brokenCageParent.SetActive(false);
                brokenCageParent.transform.localScale = initialBrokenCageScale;
                
                if (brokenPieces != null)
                {
                    for (int i = 0; i < brokenPieces.Length; i++)
                    {
                        if (brokenPieces[i] != null)
                        {
                            brokenPieces[i].localPosition = initialBrokenPositions[i];
                            brokenPieces[i].localRotation = initialBrokenRotations[i];
                            brokenPieces[i].gameObject.SetActive(true);
                            
                            Rigidbody rb = brokenPieces[i].GetComponent<Rigidbody>();
                            if (rb != null)
                            {
                                rb.isKinematic = true;
                                rb.linearVelocity = Vector3.zero;
                                rb.angularVelocity = Vector3.zero;
                            }
                        }
                    }
                }
            }

            StartCoroutine(RollLifecycleRoutine());
        }

        private Vector3 SweepForClearEntryPos(Vector3 targetWorldPos, Vector3 centerWorldPos)
        {
            Vector3 dirFromCenter = (targetWorldPos - centerWorldPos);
            dirFromCenter.y = 0f;
            if (dirFromCenter.magnitude < 0.01f) dirFromCenter = transform.forward;
            dirFromCenter.Normalize();

            float entryDistance = 1.5f; 
            float[] angleOffsets = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f };
            
            foreach(float angle in angleOffsets)
            {
                Vector3 testDir = Quaternion.Euler(0, angle, 0) * dirFromCenter;
                Vector3 testEntryPos = targetWorldPos + testDir * entryDistance;
                
                Vector3 rayDir = targetWorldPos - testEntryPos;
                float dist = rayDir.magnitude;
                
                RaycastHit[] hits = Physics.SphereCastAll(testEntryPos + Vector3.up * 0.1f, 0.15f, rayDir.normalized, dist);
                bool blocked = false;
                foreach(var hit in hits)
                {
                    BaseMole otherMole = hit.collider.GetComponentInParent<BaseMole>();
                    if (otherMole != null && otherMole != this && otherMole.gameObject.activeInHierarchy)
                    {
                        blocked = true;
                        break;
                    }
                }
                
                if (!blocked)
                {
                    return testEntryPos;
                }
            }
            
            return targetWorldPos + dirFromCenter * entryDistance;
        }

        private IEnumerator RollLifecycleRoutine()
        {
            yield return null;

            Vector3 targetWorldPos = transform.position;
            if (AssignedHoleIndex >= 0 && MoleSpawner.Instance != null && MoleSpawner.Instance.LayoutGenerator != null)
            {
                Transform targetHole = MoleSpawner.Instance.LayoutGenerator.SpawnPoints[AssignedHoleIndex];
                targetWorldPos = targetHole.position;
            }

            targetWorldPos += Vector3.up * heightOffset;
            
            transform.position = rollEntryWorldPos;
            
            Vector3 lookDir = targetWorldPos - rollEntryWorldPos;
            lookDir.y = 0;
            if (lookDir != Vector3.zero) 
            {
                transform.rotation = Quaternion.LookRotation(lookDir) * Quaternion.Euler(spawnRotationOffset);
            }

            SetCollidersEnabled(false);
            if (anim != null)
            {
                anim.SetBool(runAnimBool, true);
            }
            isRolling = true;

            float distance = Vector3.Distance(transform.position, targetWorldPos);
            float duration = distance / Mathf.Max(rollSpeed, 0.1f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(rollEntryWorldPos, targetWorldPos, elapsed / duration);
                yield return null;
            }

            transform.position = targetWorldPos;
            isRolling = false;

            if (Camera.main != null)
            {
                Vector3 cameraDir = Camera.main.transform.position - transform.position;
                cameraDir.y = 0f;
                if (cameraDir.sqrMagnitude > 0.001f)
                {
                    float yawToCamera = Quaternion.LookRotation(cameraDir).eulerAngles.y;
                    transform.rotation = Quaternion.Euler(
                        spawnRotationOffset.x,
                        yawToCamera + spawnRotationOffset.y,
                        spawnRotationOffset.z
                    );
                }
            }

            if (anim != null)
            {
                anim.SetBool(runAnimBool, false);
            }
            
            SetCollidersEnabled(true);
            isHittable = true;

            float waitTimer = 0f;
            while (waitTimer < currentVisibleDuration && !isHit)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }

            if (!isHit)
            {
                SetCollidersEnabled(false);
                isHittable = false;
                if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterMiss();

                lookDir = rollEntryWorldPos - targetWorldPos;
                lookDir.y = 0;
                if (lookDir != Vector3.zero) 
                {
                    transform.rotation = Quaternion.LookRotation(lookDir) * Quaternion.Euler(spawnRotationOffset);
                }

                if (anim != null)
                {
                    anim.SetBool(runAnimBool, true);
                }
                isRolling = true;

                elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(targetWorldPos, rollEntryWorldPos, elapsed / duration);
                    yield return null;
                }

                isRolling = false;
                gameObject.SetActive(false);
            }
        }

        public override void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            if (isHit || !isHittable) return;
            isHittable = false;

            float velocityThreshold = 0.6f;
            if (GameManager.Instance != null && GameManager.Instance.DifficultyProfile != null)
            {
                velocityThreshold = GameManager.Instance.DifficultyProfile.minHitVelocity * velocityThresholdMultiplier;
            }

            if (velocity.magnitude < velocityThreshold)
            {
                if (FeedbackManager.Instance != null)
                {
                    FeedbackManager.Instance.PlayStandardHit(hitPosition, AssignedHoleIndex);
                }
                
                #if !UNITY_EDITOR
                return;
                #endif
            }

            isHit = true;
            isHittable = false;
            StopAllCoroutines();
            SetCollidersEnabled(false);

            if (cageVisual != null) cageVisual.SetActive(false);

            if (brokenCageParent != null)
            {
                brokenCageParent.SetActive(true);
                CollisionIsolator.IsolateObject(brokenCageParent);

                Rigidbody[] rbs = brokenCageParent.GetComponentsInChildren<Rigidbody>(true);
                foreach (Rigidbody rb in rbs)
                {
                    rb.isKinematic = false;
                    rb.AddExplosionForce(velocity.magnitude * 2f, hitPosition, 1f, 0.5f, ForceMode.Impulse);
                }
            }
            
            TriggerFeedback(hitPosition, velocity, AssignedHoleIndex);

            if (anim != null)
            {
                anim.enabled = true; 
                anim.SetTrigger(happyAnimTrigger);
            }

            StartCoroutine(HappyDropSequence());
        }

        private IEnumerator HappyDropSequence()
        {
            yield return new WaitForSeconds(happyDuration);

            if (brokenCageParent != null)
            {
                brokenCageParent.SetActive(false);
            }

            Vector3 currentLocalPos = transform.localPosition;
            yield return StartCoroutine(AnimatePosition(currentLocalPos + Vector3.up * 0.15f, 0.15f, EaseType.EaseOut));

            if (anim != null)
            {
                anim.enabled = true;
                anim.SetBool(runAnimBool, true);
                anim.Play("Hamster Roll", 0, 0f);
            }

            yield return new WaitForSeconds(0.6f);

            isScalingProgrammatically = true;
            StartCoroutine(AnimateScale(originalScale * 0.05f, 0.45f));
            yield return StartCoroutine(AnimatePosition(currentLocalPos + Vector3.down * hideDepth, 0.45f, EaseType.EaseIn));

            yield return new WaitForSeconds(0.15f);
            gameObject.SetActive(false);
        }

        protected override void TriggerFeedback(Vector3 hitPosition, Vector3 velocity, int holeIndex)
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(20, velocity.magnitude);
            }

            if (hitParticleVFX != null)
            {
                Instantiate(hitParticleVFX, hitPosition, Quaternion.identity);
            }

            if (hitSoundFX != null)
            {
                AudioSource.PlayClipAtPoint(hitSoundFX, hitPosition, 1.0f);
            }
            else
            {
                FeedbackManager.Instance?.PlayHeavyHit(hitPosition, holeIndex);
            }
        }
        
        private void SetCollidersEnabled(bool state)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = state;
            }
        }
    }
}
