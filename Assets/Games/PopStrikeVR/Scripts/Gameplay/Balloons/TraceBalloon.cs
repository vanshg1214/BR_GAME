using UnityEngine;
using PopstrikeVR.Interaction;
using PopstrikeVR.Core;
using System.Collections;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// The Green Trace Balloon.
    /// Requires the INDEX_POINT gesture. 
    /// Managed by TracePathManager to ensure the player stays within the 4cm corridor.
    /// </summary>
    public class TraceBalloon : BaseBalloon
    {
        public bool IsTraced { get; private set; } = false;

        [Header("Materials")]
        public Material defaultMaterial;
        public Material completedMaterial;
        public Material errorMaterial;

        private MeshRenderer meshRenderer;

        // Cached arrays to avoid garbage collection on material swaps
        private Material[] defaultMatArray;
        private Material[] completedMatArray;
        private Material[] errorMatArray;

        private void Awake()
        {
            // Prioritize the root object if the user replaced the mesh with a simple Unity Sphere
            meshRenderer = GetComponent<MeshRenderer>();
            
            if (meshRenderer == null)
            {
                var renderers = GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    if (r.gameObject.name == "Object_7")
                    {
                        meshRenderer = r;
                        break;
                    }
                }
            }

            if (meshRenderer == null)
                meshRenderer = GetComponentInChildren<MeshRenderer>();

            if (defaultMaterial != null)
            {
                defaultMatArray = new Material[] { defaultMaterial };
                errorMatArray = new Material[] { errorMaterial != null ? errorMaterial : defaultMaterial };
                
                if (completedMaterial != null && completedMaterial.shader != null 
                    && completedMaterial.shader.name != "Hidden/InternalErrorShader")
                {
                    // Only use the 2-slot overlay if the completed material is valid
                    completedMatArray = new Material[] { defaultMaterial, completedMaterial };
                }
                else
                {
                    // Fallback: just use default material alone to avoid pink
                    completedMatArray = new Material[] { defaultMaterial };
                    if (completedMaterial != null)
                        Debug.LogWarning($"<color=yellow>[TraceBalloon] completedMaterial has a broken shader! Using defaultMaterial only to avoid pink.</color>");
                }
            }
        }

        protected override bool ShouldPlayTutorial()
        {
            // Trace tutorials are handled by the TracePathManager!
            return false;
        }

        public override void Setup(Vector3 spawnPosition)
        {
            base.Setup(spawnPosition);
            ResetState();
            
            // Trigger approach pulse (Two splashes of Green on the Vignette border)
            if (PopstrikeVR.Visuals.ScreenEffectsController.Instance != null)
            {
                float intensity = 0.6f;
                Color spawnColor = new Color(0f, 1f, 0f, 1f);
                if (PopstrikeVR.Core.PopstrikeFeedbackManager.Instance != null)
                {
                    intensity = PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.SpawnVignetteIntensity;
                    spawnColor = PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.TraceSpawnColor;
                }
                PopstrikeVR.Visuals.ScreenEffectsController.Instance.TriggerSpawnWarning(spawnColor, intensity);
            }
            
            spawnDelay = 0.5f;
        }

        private Coroutine flashRoutine;

        public void ResetState()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
            IsTraced = false;
            if (meshRenderer != null && defaultMatArray != null)
                meshRenderer.materials = defaultMatArray;
        }

        public void MarkCompleted()
        {
            IsTraced = true;
            if (meshRenderer != null && completedMatArray != null)
                meshRenderer.materials = completedMatArray;
        }

        protected override void PlayPopVFX(Vector3 hitPoint)
        {
            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayTraceVFX(hitPoint);
        }

        public void FlashErrorAndReset()
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRedRoutine());
        }

        private IEnumerator FlashRedRoutine()
        {
            if (meshRenderer != null)
            {
                if (errorMatArray != null && errorMaterial != null)
                {
                    meshRenderer.materials = errorMatArray;
                }
                else
                {
                    // Fallback visual flash if Error Material is missing in inspector!
                    meshRenderer.enabled = false;
                }
            }
            
            yield return new WaitForSeconds(0.2f);
            
            if (meshRenderer != null) meshRenderer.enabled = true;
            ResetState();
        }

        private bool skipSFX = false;

        public void TriggerFinalPop(bool silent = false)
        {
            skipSFX = silent;
            Pop();
        }

        private float touchCooldown = 0f;

        private void Update()
        {
            base.Update();
            if (touchCooldown > 0f) touchCooldown -= Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHandTouch(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Continuously re-check while hand is inside — this catches:
            // 1. Gesture that wasn't INDEX_POINT on the first frame of entry
            // 2. Player's hand still inside after a FailTrace reset
            TryHandTouch(other);
        }

        private void TryHandTouch(Collider other)
        {
            if (IsTraced || isPopped) return;
            if (touchCooldown > 0f) return; // Prevent rapid-fire retriggers

            // Trace balloons have a wider forgiveness margin so the player doesn't have to be perfectly inside it.
            // When gestures are disabled, we make it even more relaxed (22cm) for accessibility.
            float forgiveness = PopstrikeVR.Core.TemporarySessionData.DisableGestures ? 0.22f : 0.15f;
            if (!IsPhysicallyTouching(other, forgiveness)) return;

            // ONLY accept collisions from the Index Tip by default (prevents accidental knuckle bumps).
            // BUT if gestures are disabled (patient can't point), allow ANY part of the hand to trigger it.
            if (!PopstrikeVR.Core.TemporarySessionData.DisableGestures)
            {
                if (!other.name.Contains("Index3") && !other.name.Contains("IndexTip")) return;
            }
            else
            {
                if (!other.name.StartsWith("Hitbox_")) return;
            }

            if (!IsValidHand(other)) return;

            GestureState handGesture = GetHandGesture(other);
            if (PopstrikeVR.Core.TemporarySessionData.DisableGestures)
            {
                handGesture = GestureState.INDEX_POINT;
            }

            if (handGesture != GestureState.INDEX_POINT)
            {
                // Play error only for definite WRONG gestures (fist, blade)
                if (handGesture == GestureState.CLOSED_FIST || handGesture == GestureState.OPEN_BLADE)
                {
                    if (touchCooldown <= 0f) // Don't spam error sounds
                    {
                        bool canReport = false;
                        if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
                            canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();
                        
                        if (!canReport) return; // Completely ignore if cooldown is active

                        Debug.Log($"<color=yellow>[TraceBalloon] WRONG GESTURE: {handGesture}. Playing error.</color>");
                        if (PopstrikeFeedbackManager.Instance != null)
                            PopstrikeFeedbackManager.Instance.PlayErrorTone();
                        PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(transform.position);
                        FlashErrorAndReset();
                        touchCooldown = 0.2f; // Quick cooldown so they can fix their hand instantly
                    }
                }
                return; 
            }

            Debug.Log("<color=green>[TraceBalloon] SUCCESS! Index touch registered.</color>");
            
            // --- ACCESSIBILITY: Lock Gesture on first hit for Hard Mode ---
            if (PopstrikeVR.Core.TemporarySessionData.Difficulty == "Hard")
            {
                if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
                {
                    PopstrikeVR.Interaction.GestureDetector.Instance.LockGesture(IsLeftHand(other), PopstrikeVR.Interaction.GestureState.INDEX_POINT);
                }
            }
            
            touchCooldown = 0.3f; // Prevent double-registering
            TracePathManager.Instance?.OnBalloonHit(this, other.transform);
        }



        protected override void PlayPopSFX()
        {
            if (skipSFX) return;

            // Warm musical puzzle-solved chord
            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayTraceChime();
        }
    }
}
