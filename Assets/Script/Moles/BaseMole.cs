using System.Collections;
using UnityEngine;

namespace WhackAMole
{
    public abstract class BaseMole : MonoBehaviour, IHittable
    {
        protected enum EaseType { Linear, EaseIn, EaseOut, EaseInOut, Elastic }

        #region Inspector Fields
        [Header("Movement Depth Settings")]
        [Tooltip("Distance the mole retracts below the hole when hidden.")]
        protected float hideDepth = 0.6f;
        protected float visibleDepth = 0.0f;

        [Header("Position Correction")]
        [Tooltip("Offset applied to center the mesh in the hole.")]
        public Vector3 visualOffset = Vector3.zero;
        #endregion

        #region Runtime State
        protected bool isHit;
        protected float currentVisibleDuration;
        protected Vector3 spawnOrigin;
        protected Vector3 originalScale;
        private bool hasCachedScale;

        private bool lockXZPosition = false;
        private float lockedLocalX;
        private float lockedLocalZ;

        protected Vector3 currentDynamicScale;
        protected bool isScalingProgrammatically;

        public int AssignedHoleIndex { get; set; } = -1;

        public virtual bool IsFakeOrDecoy => false;
        protected virtual bool PlaysPopupSound => false;
        #endregion

        #region Unity Lifecycle
        protected virtual void Awake()
        {
            originalScale = transform.localScale;
            hasCachedScale = true;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null) m.renderQueue = 3000;
                }
            }
        }

        protected virtual bool UsesHoleSpawning => true;

        protected virtual void OnEnable()
        {
            isHit = false;

            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = true;

            if (!hasCachedScale)
            {
                originalScale = transform.localScale;
                hasCachedScale = true;
            }

            isScalingProgrammatically = false;
            transform.localScale = originalScale;

            SetCollidersEnabled(false);
            currentVisibleDuration = GetVisibleDuration();

            if (UsesHoleSpawning)
            {
                transform.localRotation = Quaternion.identity;
                transform.localPosition = Vector3.zero;

                AlignWithCamera();

                Vector3 correctedOffset = Quaternion.Inverse(transform.localRotation) * visualOffset;
                spawnOrigin = correctedOffset;

                lockedLocalX = correctedOffset.x;
                lockedLocalZ = correctedOffset.z;
                lockXZPosition = true;

                transform.localPosition = spawnOrigin + Vector3.down * hideDepth;

                StopAllCoroutines();
                StartCoroutine(MoleLifecycleRoutine());
            }
        }

        protected virtual void OnDisable()
        {
            isScalingProgrammatically = false;
            lockXZPosition = false;
            StopAllCoroutines();

            if (AssignedHoleIndex >= 0)
            {
                MoleSpawner spawner = FindFirstObjectByType<MoleSpawner>();
                if (spawner != null)
                {
                    spawner.FreeHole(AssignedHoleIndex);
                }
                AssignedHoleIndex = -1;
            }
        }

        protected virtual void LateUpdate()
        {
            if (isScalingProgrammatically)
            {
                transform.localScale = currentDynamicScale;
            }

            if (lockXZPosition && UsesHoleSpawning)
            {
                Vector3 p = transform.localPosition;
                transform.localPosition = new Vector3(lockedLocalX, p.y, lockedLocalZ);
            }
        }
        #endregion

        #region Lifecycle Routines
        protected virtual IEnumerator MoleLifecycleRoutine()
        {
            if (PlaysPopupSound && FeedbackManager.Instance != null)
            {
                FeedbackManager.Instance.PlayGroundPopup(transform.position);
            }

            Vector3 peekPosition = spawnOrigin;
            currentDynamicScale = GetTargetLocalScale(originalScale * 0.05f);
            isScalingProgrammatically = true;
            transform.localScale = currentDynamicScale;

            StartCoroutine(AnimateScale(originalScale * 1.1f, 0.35f));
            yield return StartCoroutine(AnimatePosition(peekPosition + Vector3.up * 0.02f, 0.35f, EaseType.EaseOut));

            StartCoroutine(AnimateScale(originalScale, 0.1f));
            yield return StartCoroutine(AnimatePosition(peekPosition, 0.1f, EaseType.EaseInOut));

            SetCollidersEnabled(true);

            float elapsed = 0f;
            while (elapsed < currentVisibleDuration && !isHit)
            {
                elapsed += Time.deltaTime;

                isScalingProgrammatically = true;
                float pulse = 1f + Mathf.Sin(elapsed * 4f) * 0.03f;
                currentDynamicScale = new Vector3(originalScale.x * pulse, originalScale.y * (2f - pulse), originalScale.z * pulse);

                float sway = Mathf.Sin(elapsed * 2.5f) * 0.005f;
                transform.localPosition = peekPosition + Vector3.right * sway;

                yield return null;
            }

            transform.localScale = originalScale;

            if (!isHit)
            {
                SetCollidersEnabled(false);

                if (ScoreManager.Instance != null && !IsFakeOrDecoy)
                {
                    ScoreManager.Instance.RegisterMiss();
                }

                yield return StartCoroutine(AnimatePosition(peekPosition + Vector3.up * 0.03f, 0.08f, EaseType.EaseOut));

                StartCoroutine(AnimateScale(originalScale * 0.05f, 0.45f));
                yield return StartCoroutine(AnimatePosition(spawnOrigin + Vector3.down * hideDepth, 0.45f, EaseType.EaseIn));
                gameObject.SetActive(false);
            }
        }

        protected IEnumerator HideSmoothlyRoutine()
        {
            SetCollidersEnabled(false);
            
            Vector3 peekPosition = spawnOrigin;
            yield return StartCoroutine(AnimatePosition(peekPosition + Vector3.up * 0.03f, 0.08f, EaseType.EaseOut));

            StartCoroutine(AnimateScale(originalScale * 0.05f, 0.4f));
            yield return StartCoroutine(AnimatePosition(spawnOrigin + Vector3.down * hideDepth, 0.4f, EaseType.EaseIn));
            gameObject.SetActive(false);
        }

        public void RetractIntoHole()
        {
            if (gameObject.activeInHierarchy)
            {
                StopAllCoroutines();
                StartCoroutine(HideSmoothlyRoutine());
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        #endregion

        #region Public Methods
        public void ExtendStayDuration(float extraSeconds)
        {
            currentVisibleDuration += extraSeconds;
        }
        #endregion

        #region Interpolation Utilities
        protected IEnumerator AnimatePosition(Vector3 targetLocalPos, float duration, EaseType ease)
        {
            Vector3 startPos = transform.localPosition;
            duration = Mathf.Max(duration, 0.01f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = ApplyEasing(Mathf.Clamp01(elapsed / duration), ease);
                transform.localPosition = Vector3.Lerp(startPos, targetLocalPos, t);
                yield return null;
            }

            transform.localPosition = targetLocalPos;
        }

        protected Vector3 GetTargetLocalScale(Vector3 baseScale)
        {
            return baseScale;
        }

        protected IEnumerator AnimateScale(Vector3 targetScale, float duration)
        {
            isScalingProgrammatically = true;
            Vector3 startScale = currentDynamicScale != Vector3.zero ? currentDynamicScale : transform.localScale;
            duration = Mathf.Max(duration, 0.01f);
            float elapsed = 0f;

            Vector3 compensatedTargetScale = GetTargetLocalScale(targetScale);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                currentDynamicScale = Vector3.Lerp(startScale, compensatedTargetScale, t);
                yield return null;
            }

            currentDynamicScale = compensatedTargetScale;
            
            if (targetScale == originalScale)
            {
                isScalingProgrammatically = false;
                transform.localScale = GetTargetLocalScale(originalScale);
            }
        }

        protected float ApplyEasing(float t, EaseType ease)
        {
            switch (ease)
            {
                case EaseType.EaseIn:    return t * t;
                case EaseType.EaseOut:   return 1f - (1f - t) * (1f - t);
                case EaseType.EaseInOut: return t * t * (3f - 2f * t);
                case EaseType.Elastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;
                default: return t;
            }
        }
        #endregion

        #region Collision & Hit Logic
        public virtual void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            if (isHit) return;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            isHit = true;
            StopAllCoroutines();

            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = false;

            SetCollidersEnabled(false);

            Vector3 vfxWorldPos;
            Renderer modelRenderer = GetComponentInChildren<Renderer>();
            if (modelRenderer != null)
            {
                vfxWorldPos = new Vector3(modelRenderer.bounds.center.x, modelRenderer.bounds.max.y, modelRenderer.bounds.center.z);
            }
            else
            {
                vfxWorldPos = transform.position + transform.up * 0.3f;
            }

            StartCoroutine(HitResponseRoutine(vfxWorldPos, velocity));
        }

        protected virtual IEnumerator HitResponseRoutine(Vector3 hitPosition, Vector3 velocity)
        {
            TriggerFeedback(hitPosition, velocity, AssignedHoleIndex);

            transform.localPosition = spawnOrigin + Vector3.down * hideDepth;
            transform.localScale = originalScale;

            gameObject.SetActive(false);
            
            yield break;
        }

        protected abstract void TriggerFeedback(Vector3 hitPosition, Vector3 velocity, int holeIndex);
        #endregion

        #region Helpers
        protected virtual float GetVisibleDuration()
        {
            return 5.0f;
        }

        private void AlignWithCamera()
        {
            if (transform.parent != null && !transform.parent.name.StartsWith("ProxyFlatSpawn"))
            {
                transform.localRotation = Quaternion.identity;
                return;
            }

            if (Camera.main != null)
            {
                Vector3 cameraDir = Camera.main.transform.position - transform.position;
                cameraDir.y = 0f;
                
                if (cameraDir.sqrMagnitude > 0.001f)
                {
                    Quaternion desiredWorldRot = Quaternion.LookRotation(cameraDir, Vector3.up);
                    
                    if (transform.parent != null)
                        transform.localRotation = Quaternion.Inverse(transform.parent.rotation) * desiredWorldRot;
                    else
                        transform.localRotation = desiredWorldRot;
                }
            }
        }

        protected void SetCollidersEnabled(bool state)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = state;
            }
        }
        #endregion
    }
}
