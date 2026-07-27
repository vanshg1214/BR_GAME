using UnityEngine;
using System.Collections;

namespace WhackAMole
{
    public class DogCharacter : BaseMole
    {
        [Header("Target Link")]
        public ExplosiveBottleProp bottleTarget;

        [Header("Animation Links")]
        public Transform leftHandBone;
        public Transform rightHandBone;

        [Header("Timing")]
        public float hitWindow = 6f;
        public float hideDelay = 0.0f;

        [Header("Dog Movement")]
        [SerializeField] private float walkSpeed = 1.8f;

        private Animator anim;
        private Vector3 targetWorldPos;
        private Vector3 walkEntryWorldPos;
        private bool isPickingUp = false;
        private Coroutine lifecycleCoroutine;

        #region Unity Lifecycle
        protected override void Awake()
        {
            base.Awake();
            anim = GetComponentInChildren<Animator>();

            DisableBodyColliders();

            if (bottleTarget == null || !bottleTarget.transform.IsChildOf(transform))
            {
                ExplosiveBottleProp childBottle = GetComponentInChildren<ExplosiveBottleProp>(true);
                if (childBottle != null)
                {
                    bottleTarget = childBottle;
                }
                else
                {
                    Debug.LogError($"[DogCharacter] No ExplosiveBottleProp found in children of '{gameObject.name}'!");
                }
            }

            if (bottleTarget != null)
            {
                bottleTarget.OnTargetDestroyed += HandleBottleSmashed;
            }
        }

        protected override bool UsesHoleSpawning => false;

        protected override void OnEnable()
        {
            walkEntryWorldPos = transform.position;

            isHit = false;
            isPickingUp = false;

            if (anim != null)
            {
                anim.enabled = true;
                anim.ResetTrigger("Throw");
                anim.ResetTrigger("Pickup");
            }

            DisableBodyColliders();
            SetCollidersEnabled(false); 

            transform.localScale = originalScale;

            if (bottleTarget != null)
            {
                bottleTarget.gameObject.SetActive(true);
                bottleTarget.ResetProp();
                if (bottleTarget.ballObject != null)
                {
                    bottleTarget.ballObject.SetActive(false); 
                }
            }

            StopAllCoroutines();
            lifecycleCoroutine = StartCoroutine(MoveInFromShrub());
        }

        protected override void OnDisable()
        {
            if (bottleTarget != null)
            {
                bottleTarget.gameObject.SetActive(false);
                if (bottleTarget.ballObject != null)
                {
                    bottleTarget.ballObject.SetActive(false);
                }
            }

            base.OnDisable();
        }
        #endregion

        #region Core Lifecycle
        private IEnumerator MoveInFromShrub()
        {
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

                if (anim != null)
                {
                    anim.Play("Dog Carry Move");
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
            }

            transform.position = targetWorldPos;

            FaceCamera();

            if (anim != null)
            {
                anim.Play("New State");
            }

            yield return new WaitForSeconds(0.1f);
            if (anim != null)
            {
                anim.SetTrigger("Throw");
            }

            SetCollidersEnabled(true);
            DisableBodyColliders(); 

            lifecycleCoroutine = StartCoroutine(DogLifecycle());
        }

        private IEnumerator MoveOutToShrub()
        {
            if (walkEntryWorldPos != targetWorldPos)
            {
                Vector3 lookDir = walkEntryWorldPos - transform.position;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }

                if (anim != null)
                {
                    anim.Play("Dog Carry Move");
                }

                float distance = Vector3.Distance(transform.position, walkEntryWorldPos);
                float retreatSpeed = Mathf.Max(walkSpeed * 0.8f, 0.1f);
                float duration = distance / retreatSpeed;
                
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(targetWorldPos, walkEntryWorldPos, elapsed / duration);
                    yield return null;
                }
            }

            gameObject.SetActive(false);
        }

        private IEnumerator DogLifecycle()
        {
            yield return new WaitForSeconds(hitWindow);

            if (bottleTarget != null && !bottleTarget.IsBroken)
            {
                if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterMiss();
                
                isPickingUp = true;
                isHit = true; 

                bottleTarget.DisableHittable();

                Vector3 currentPos = bottleTarget.transform.position;
                Vector3 slidePos = transform.position + (transform.forward * 0.08f);
                slidePos.y = transform.position.y + 0.01f;
                bottleTarget.MoveBottleToTarget(slidePos, 0.25f, false);
                yield return new WaitForSeconds(0.25f);

                if (anim != null) anim.SetTrigger("Pickup");

                yield return new WaitForSeconds(0.4f);

                bottleTarget.AttachToInitialHand();

                yield return new WaitForSeconds(0.1f);

                yield return StartCoroutine(MoveOutToShrub());
            }
        }
        #endregion

        #region Animation Events
        public void OnThrowRelease()
        {
            if (bottleTarget != null)
            {
                bottleTarget.ThrowForward();
                if (bottleTarget.ballObject != null)
                {
                    bottleTarget.ballObject.SetActive(false); 
                }
            }
        }

        public void NewEvent()
        {
            OnThrowRelease();
        }

        public void OnPickupComplete()
        {
        }
        #endregion

        #region Bottle Smashed Handler
        private void HandleBottleSmashed()
        {
            if (lifecycleCoroutine != null)
            {
                StopCoroutine(lifecycleCoroutine);
            }

            isPickingUp = true;
            isHit = true; 

            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(SmashedPickupSequence());
            }
        }

        private IEnumerator SmashedPickupSequence()
        {
            if (bottleTarget != null)
            {
                Vector3 currentPos = bottleTarget.transform.position; 
                Vector3 dropPos = new Vector3(currentPos.x, transform.position.y + 0.01f, currentPos.z);
                bottleTarget.MoveBallToTarget(dropPos, 0.15f, false);
                yield return new WaitForSeconds(0.15f);

                Vector3 slidePos = transform.position + (transform.forward * 0.08f);
                slidePos.y = transform.position.y + 0.01f;
                bottleTarget.MoveBallToTarget(slidePos, 0.25f, false);
                yield return new WaitForSeconds(0.25f); 

                if (anim != null) anim.SetTrigger("Pickup");

                yield return new WaitForSeconds(0.4f);
            }

            if (bottleTarget != null && leftHandBone != null && rightHandBone != null)
            {
                bottleTarget.AnimateBallToHands(leftHandBone, rightHandBone);
            }

            yield return new WaitForSeconds(0.1f);
            yield return StartCoroutine(MoveOutToShrub());
        }
        #endregion

        #region Helpers
        private void DisableBodyColliders()
        {
            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (Collider c in cols)
            {
                if (bottleTarget == null || !c.transform.IsChildOf(bottleTarget.transform))
                {
                    c.enabled = false;
                }
            }
        }

        private void FaceCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 lookDir = cam.transform.position - transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }

        protected override float GetVisibleDuration()
        {
            return 99f;
        }

        public override void OnHit(Vector3 velocity, Vector3 hitPosition) { }
        protected override void TriggerFeedback(Vector3 hitPosition, Vector3 velocity, int holeIndex) { }
        #endregion
    }
}
