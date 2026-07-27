using System.Collections;
using TMPro;
using UnityEngine;

namespace WhackAMole
{
    public class TreasureTurtle : BaseMole
    {
        [Header("Target Link (Chest)")]
        [SerializeField] private Animator chestAnimator;
        [SerializeField] private TMP_Text hitCountText;
        [SerializeField] private GameObject hammerSymbol;
        [SerializeField] private GameObject hammerCanvas;

        [Header("Turtle Movement")]
        [SerializeField] private float walkSpeed = 1.0f;

        [Header("Throw Settings")]
        [SerializeField] private float throwDistance = 0.15f;
        [SerializeField] private float throwScaleMultiplier = 1.2f;
        [SerializeField] private float throwDuration = 0.3f;

        [Header("Mid-Hit VFX & Sound (Coins)")]
        [SerializeField] private GameObject midHitVFX;
        [SerializeField] private AudioClip midHitSound;
        [SerializeField] private float extraTimePerHit = 2.5f;

        [Header("Final Hit VFX & Sound (Open Box)")]
        [SerializeField] private GameObject finalHitVFX;
        [SerializeField] private AudioClip finalHitSound;
        [SerializeField] private float finalAnimationWaitTime = 2.0f;

        [Header("Retrieval Settings")]
        [SerializeField] private float retrieveDuration = 0.5f;
        [SerializeField] private float hideDelay = 2.5f;

        [Header("Animation Triggers")]
        [SerializeField] private string happyAnimTrigger = "Happy";
        [SerializeField] private string walkAnimBool = "IsRunning";
        [SerializeField] private string chestOpenTrigger = "Open";
        [SerializeField] private string throwAnimTrigger = "Throw";
        [SerializeField] private string pickupAnimTrigger = "Pickup";

        private int maxHits;
        private int currentHits;

        private Vector3 targetWorldPos;
        private Vector3 walkEntryWorldPos;
        private bool isChestThrown;
        private bool isRetrieving;

        private Vector3 chestOriginalLocalPos;
        private Quaternion chestOriginalLocalRot;
        private Vector3 chestOriginalScale;

        private Vector3 chestLandingWorldPos;
        private Coroutine currentShakeRoutine;
        private Coroutine lifecycleCoroutine;
        private bool isReleaseTriggered = false;

        public void OnThrowRelease()
        {
            isReleaseTriggered = true;
        }

        public void NewEvent()
        {
            OnThrowRelease();
        }

        protected override void Awake()
        {
            base.Awake();
            if (chestAnimator != null)
            {
                chestOriginalLocalPos = chestAnimator.transform.localPosition;
                chestOriginalLocalRot = chestAnimator.transform.localRotation;
                chestOriginalScale = chestAnimator.transform.localScale;
            }
        }

        protected override bool UsesHoleSpawning => false;

        protected override void OnEnable()
        {
            walkEntryWorldPos = transform.position;

            isHit = false;
            isChestThrown = false;
            isRetrieving = false;

            maxHits = Random.Range(3, 6);
            currentHits = 0;

            if (chestAnimator != null)
            {
                chestAnimator.transform.localPosition = chestOriginalLocalPos;
                chestAnimator.transform.localRotation = chestOriginalLocalRot;
                chestAnimator.transform.localScale = chestOriginalScale;

                chestAnimator.speed = 1f;
                chestAnimator.enabled = false;
                chestAnimator.enabled = true;
                chestAnimator.ResetTrigger(chestOpenTrigger);
                chestAnimator.Rebind();
                chestAnimator.Update(0f);
            }

            SetChestColliderEnabled(false);
            
            if (hammerCanvas != null) hammerCanvas.SetActive(false);

            UpdateHitText();

            base.OnEnable();
            transform.localScale = originalScale;

            lifecycleCoroutine = StartCoroutine(MoveInFromGrass());
        }

        protected override void OnDisable()
        {
            if (chestAnimator != null)
            {
                chestAnimator.gameObject.SetActive(false);
            }
            base.OnDisable();
        }

        private IEnumerator ThrowChestRoutine()
        {
            if (chestAnimator == null) yield break;

            isReleaseTriggered = false;

            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && !string.IsNullOrEmpty(throwAnimTrigger))
            {
                anim.SetTrigger(throwAnimTrigger);
            }

            float waitTimer = 0f;
            while (!isReleaseTriggered && waitTimer < 0.4f)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }

            Vector3 startPos = chestAnimator.transform.position; 
            Quaternion startRot = chestAnimator.transform.rotation;

            chestLandingWorldPos = targetWorldPos + transform.forward * throwDistance + Vector3.up * 0.03f;
            
            if (Camera.main != null)
            {
                Vector3 headPos = Camera.main.transform.position;
                Vector3 toLanding = chestLandingWorldPos - headPos;
                toLanding.y = 0;
                if (toLanding.magnitude < 0.25f)
                {
                    chestLandingWorldPos = targetWorldPos + transform.forward * Mathf.Max(0.05f, throwDistance * 0.5f) + Vector3.up * 0.03f;
                }
            }

            isChestThrown = true;

            float elapsed = 0f;
            Vector3 targetScale = chestOriginalScale * throwScaleMultiplier;

            while (elapsed < throwDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = elapsed / throwDuration;

                float height = Mathf.Sin(ratio * Mathf.PI) * 0.2f;
                Vector3 currentPos = Vector3.Lerp(startPos, chestLandingWorldPos, ratio);
                currentPos.y += height;

                chestAnimator.transform.position = currentPos;
                
                chestAnimator.transform.localScale = Vector3.Lerp(chestOriginalScale, targetScale, ratio);

                chestAnimator.transform.rotation = Quaternion.Lerp(startRot, transform.rotation, ratio);

                yield return null;
            }

            chestAnimator.transform.position = chestLandingWorldPos;
            chestAnimator.transform.localScale = targetScale;
            chestAnimator.transform.rotation = transform.rotation;

            SetChestColliderEnabled(true);
            
            if (hammerCanvas != null) hammerCanvas.SetActive(true);

            lifecycleCoroutine = StartCoroutine(TurtleLifecycleRoutine());
        }

        private IEnumerator TurtleLifecycleRoutine()
        {
            yield return new WaitForSeconds(currentVisibleDuration);

            if (!isHit)
            {
                yield return StartCoroutine(RetrieveChestRoutine());
                yield return StartCoroutine(MoveOutToGrass());
            }
        }

        private IEnumerator RetrieveChestRoutine()
        {
            if (chestAnimator == null) yield break;

            isRetrieving = true;
            SetChestColliderEnabled(false);

            if (hitCountText != null) hitCountText.gameObject.SetActive(false);
            if (hammerSymbol != null) hammerSymbol.SetActive(false);
            if (hammerCanvas != null) hammerCanvas.SetActive(false);

            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && !string.IsNullOrEmpty(pickupAnimTrigger))
            {
                anim.SetTrigger(pickupAnimTrigger);
                yield return new WaitForSeconds(0.3f);
            }

            Vector3 startPos = chestAnimator.transform.position;
            Quaternion startRot = chestAnimator.transform.rotation;

            Vector3 handWorldPos = transform.TransformPoint(chestOriginalLocalPos);
            Quaternion handWorldRot = transform.rotation * chestOriginalLocalRot;

            float elapsed = 0f;

            while (elapsed < retrieveDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = elapsed / retrieveDuration;

                chestAnimator.transform.position = Vector3.Lerp(startPos, handWorldPos, ratio);
                chestAnimator.transform.localScale = Vector3.Lerp(chestAnimator.transform.localScale, chestOriginalScale, ratio);
                chestAnimator.transform.rotation = Quaternion.Lerp(startRot, handWorldRot, ratio);

                yield return null;
            }

            chestAnimator.transform.localPosition = chestOriginalLocalPos;
            chestAnimator.transform.localRotation = chestOriginalLocalRot;
            chestAnimator.transform.localScale = chestOriginalScale;

            if (anim != null && !string.IsNullOrEmpty(pickupAnimTrigger))
            {
                float remainingWait = 1.2f - 0.3f - retrieveDuration;
                if (remainingWait > 0f)
                {
                    yield return new WaitForSeconds(remainingWait);
                }
            }

            isRetrieving = false;
        }

        private IEnumerator MoveInFromGrass()
        {
            if (chestAnimator != null)
            {
                chestAnimator.gameObject.SetActive(true);
            }

            yield return null;

            if (AssignedHoleIndex >= 0 && MoleSpawner.Instance != null && MoleSpawner.Instance.LayoutGenerator != null)
            {
                Transform targetHole = MoleSpawner.Instance.LayoutGenerator.SpawnPoints[AssignedHoleIndex];
                targetWorldPos = targetHole.position;
            }
            else
            {
                targetWorldPos = transform.position; 
            }

            if (walkEntryWorldPos != targetWorldPos)
            {
                Vector3 lookDir = targetWorldPos - walkEntryWorldPos;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }

                Animator anim = GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    if (!string.IsNullOrEmpty(walkAnimBool))
                    {
                        anim.SetBool(walkAnimBool, true);
                    }
                }

                float distance = Vector3.Distance(transform.position, targetWorldPos);
                float duration = distance / Mathf.Max(walkSpeed, 0.1f);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(walkEntryWorldPos, targetWorldPos, elapsed / duration);
                    yield return null;
                }

                if (anim != null && !string.IsNullOrEmpty(walkAnimBool))
                {
                    anim.SetBool(walkAnimBool, false);
                }
            }

            transform.position = targetWorldPos;

            FaceCamera();

            yield return StartCoroutine(ThrowChestRoutine());
        }

        private IEnumerator MoveOutToGrass()
        {
            if (walkEntryWorldPos != targetWorldPos)
            {
                Vector3 lookDir = walkEntryWorldPos - transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }

                Animator anim = GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    if (!string.IsNullOrEmpty(happyAnimTrigger)) anim.ResetTrigger(happyAnimTrigger);
                    if (!string.IsNullOrEmpty(pickupAnimTrigger)) anim.ResetTrigger(pickupAnimTrigger);

                    if (!string.IsNullOrEmpty(walkAnimBool))
                    {
                        anim.SetBool(walkAnimBool, true);
                    }

                    anim.Play("Turtle Walk", 0, 0f);
                }

                float distance = Vector3.Distance(transform.position, walkEntryWorldPos);
                float duration = distance / Mathf.Max(walkSpeed, 0.1f);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(targetWorldPos, walkEntryWorldPos, elapsed / duration);
                    yield return null;
                }

                if (anim != null && !string.IsNullOrEmpty(walkAnimBool))
                {
                    anim.SetBool(walkAnimBool, false);
                }
            }

            gameObject.SetActive(false);
        }

        public override void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            if (isHit || !isChestThrown || isRetrieving) return;

            float velocityThreshold = 0.6f;
            if (GameManager.Instance != null && GameManager.Instance.DifficultyProfile != null)
            {
                velocityThreshold = GameManager.Instance.DifficultyProfile.minHitVelocity * 0.7f;
            }

            if (velocity.magnitude < velocityThreshold)
            {
                #if !UNITY_EDITOR
                return;
                #endif
            }

            currentHits++;
            UpdateHitText();

            if (currentHits < maxHits)
            {
                ExtendStayDuration(extraTimePerHit);

                if (midHitVFX != null)
                {
                    GameObject vfx = ObjectPooler.Instance.SpawnOrAddPool(midHitVFX.name, midHitVFX, 5, hitPosition, Quaternion.identity);
                    ParticleSystem[] pSystems = vfx.GetComponentsInChildren<ParticleSystem>();
                    foreach(ParticleSystem ps in pSystems)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Clear(true);

                        var rotOverLifetime = ps.rotationOverLifetime;
                        rotOverLifetime.enabled = true;
                        rotOverLifetime.separateAxes = true;
                        rotOverLifetime.xMultiplier = 15f;
                        rotOverLifetime.yMultiplier = 15f;
                        rotOverLifetime.zMultiplier = 15f;

                        var main = ps.main;
                        main.startSizeMultiplier = 6.0f;
                        main.startSpeedMultiplier = 7.0f;
                        main.gravityModifierMultiplier = 3.0f;
                        
                        var shape = ps.shape;
                        shape.shapeType = ParticleSystemShapeType.Cone;
                        shape.angle = 60f;
                        shape.rotation = new Vector3(-90f, 0f, 0f);
                        
                        var emission = ps.emission;
                        emission.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, 40, 60) });
                        
                        ps.Play(true);
                    }
                    ObjectPooler.Instance.ReturnToPool(vfx, 4f);
                }

                if (midHitSound != null)
                {
                    AudioSource.PlayClipAtPoint(midHitSound, hitPosition);
                }

                if (chestAnimator != null)
                {
                    if (currentShakeRoutine != null) StopCoroutine(currentShakeRoutine);
                    currentShakeRoutine = StartCoroutine(ShakeRoutine(chestAnimator.transform, 0.3f, 0.05f));
                }
            }
            else
            {
                isHit = true;
                StopAllCoroutines();
                SetChestColliderEnabled(false);
                StartCoroutine(FinalChestSequence(hitPosition, velocity));
            }
        }

        private IEnumerator FinalChestSequence(Vector3 hitPosition, Vector3 velocity)
        {
            if (finalHitSound != null)
            {
                AudioSource.PlayClipAtPoint(finalHitSound, chestLandingWorldPos);
            }

            Transform target = chestAnimator != null ? chestAnimator.transform : transform;
            Vector3 beforeJumpPos = target.position;

            float elapsed = 0f;
            float duration = 0.8f;
            float shakeMagnitude = 0.08f; 
            float jumpHeight = 0.17f;     

            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float ratio = elapsed / duration;

                float x = Random.Range(-1f, 1f) * shakeMagnitude * (1f - ratio);
                float z = Random.Range(-1f, 1f) * shakeMagnitude * (1f - ratio);
                float rotZ = Random.Range(-8f, 8f) * (1f - ratio); 
                float yOffset = Mathf.Sin(ratio * Mathf.PI) * jumpHeight;

                target.position = beforeJumpPos + new Vector3(x, yOffset, z);
                target.rotation = transform.rotation * Quaternion.Euler(0, 0, rotZ);
                yield return null;
            }

            if (target != null)
            {
                target.position = beforeJumpPos;
                target.rotation = transform.rotation;
            }

            if (finalHitVFX != null)
            {
                GameObject vfx = ObjectPooler.Instance.SpawnOrAddPool(finalHitVFX.name, finalHitVFX, 3, chestLandingWorldPos, Quaternion.identity);
                ParticleSystem[] pSystems = vfx.GetComponentsInChildren<ParticleSystem>();
                foreach(ParticleSystem ps in pSystems) ps.Play(true);
                ObjectPooler.Instance.ReturnToPool(vfx, 5f);
            }

            if (chestAnimator != null)
            {
                chestAnimator.speed = 2.5f; 
                chestAnimator.SetTrigger(chestOpenTrigger);
                chestAnimator.Play("Scene", -1, 0f);
            }

            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && !string.IsNullOrEmpty(happyAnimTrigger))
            {
                anim.SetTrigger(happyAnimTrigger);
            }

            TriggerFeedback(hitPosition, velocity, AssignedHoleIndex);

            yield return new WaitForSeconds(finalAnimationWaitTime);

            yield return StartCoroutine(RetrieveChestRoutine());

            if (chestAnimator != null)
            {
                chestAnimator.ResetTrigger(chestOpenTrigger);
                chestAnimator.Rebind();
                chestAnimator.Update(0f);
            }

            yield return new WaitForSeconds(hideDelay);

            yield return StartCoroutine(MoveOutToGrass());
        }

        private IEnumerator ShakeRoutine(Transform target, float duration, float magnitude)
        {
            float elapsed = 0f;
            Vector3 originalPos = target.localPosition;
            Quaternion originalRot = target.localRotation;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float x = Random.Range(-1f, 1f) * magnitude;
                float z = Random.Range(-1f, 1f) * magnitude;
                float rotZ = Random.Range(-5f, 5f) * (magnitude * 10f);

                target.localPosition = originalPos + new Vector3(x, 0, z);
                target.localRotation = originalRot * Quaternion.Euler(0, 0, rotZ);
                yield return null;
            }

            if (target != null)
            {
                target.localPosition = originalPos;
                target.localRotation = originalRot;
            }
        }

        private void UpdateHitText()
        {
            int remainingHits = maxHits - currentHits;
            if (hitCountText != null)
            {
                hitCountText.text = "x" + remainingHits;
                hitCountText.gameObject.SetActive(remainingHits > 0);
            }
            
            if (hammerSymbol != null)
            {
                hammerSymbol.SetActive(remainingHits > 0);
            }
        }

        private void SetChestColliderEnabled(bool state)
        {
            if (chestAnimator != null)
            {
                Collider[] colliders = chestAnimator.GetComponentsInChildren<Collider>();
                foreach (Collider c in colliders)
                {
                    c.enabled = state;
                }
            }
        }

        private void FaceCamera()
        {
            if (Camera.main != null)
            {
                Vector3 lookDir = Camera.main.transform.position - transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }

        protected override float GetVisibleDuration()
        {
            return 5.0f;
        }

        protected override void TriggerFeedback(Vector3 hitPosition, Vector3 velocity, int holeIndex)
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(50, velocity.magnitude);
            }
        }
    }
}
