using UnityEngine;
using PopstrikeVR.Interaction;
using PopstrikeVR.Core;
using System.Collections;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// The Blue Blade Balloon.
    /// Requires the OPEN_BLADE gesture. Relies on BladeSlashManager to enforce
    /// continuous sequential motion and 80ms cascade popping.
    /// </summary>
    public class BladeBalloon : BaseBalloon
    {
        [Header("Materials")]
        public Material defaultMaterial;
        public Material slicedMaterial;
        public Material errorMaterial;

        [Header("Tutorial System")]
        [Tooltip("The direction the player should swipe to cut this balloon.")]
        public Vector3 requiredSlashDirection = Vector3.right;
        
        public static bool HasCompletedTutorial = false;

        public bool IsSliced { get; private set; } = false;

        private MeshRenderer meshRenderer;
        private Quaternion hitRotation = Quaternion.identity;
        private Coroutine flashRoutine;

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
        }

        public override void Setup(Vector3 spawnPosition)
        {
            base.Setup(spawnPosition);
            ResetState();

            // Trigger approach pulse (Two splashes of Blue on the Vignette border)
            if (PopstrikeVR.Visuals.ScreenEffectsController.Instance != null)
            {
                float intensity = 0.6f;
                Color spawnColor = new Color(0f, 0.5f, 1f, 1f);
                if (PopstrikeVR.Core.PopstrikeFeedbackManager.Instance != null)
                {
                    intensity = PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.SpawnVignetteIntensity;
                    spawnColor = PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.BladeSpawnColor;
                }
                PopstrikeVR.Visuals.ScreenEffectsController.Instance.TriggerSpawnWarning(spawnColor, intensity);
            }
            
            // Wait exactly 0.5s for the flashes to finish before animating from Scale 0
            spawnDelay = 0.5f;
        }

        public void ResetState()
        {
            if (flashRoutine != null) 
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
            IsSliced = false;
            if (meshRenderer != null && defaultMaterial != null)
                meshRenderer.material = defaultMaterial;
            
            // Snap back to 0 immediately if reset mid-shake
            Transform shakeTarget = GetShakeTarget();
            if (shakeTarget != null)
            {
                shakeTarget.localPosition = Vector3.zero;
            }
        }

        private Transform GetShakeTarget()
        {
            if (VisualsParent != null && VisualsParent != transform) return VisualsParent;
            if (meshRenderer != null && meshRenderer.transform != transform) return meshRenderer.transform;
            return null;
        }

        public void MarkSliced()
        {
            if (isPopped || IsSliced) return;
            
            IsSliced = true;
            if (meshRenderer != null && slicedMaterial != null)
                meshRenderer.material = slicedMaterial;
        }

        public void FlashErrorAndReset(bool playSound = true, bool reportError = true)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            
            if (reportError)
            {
                bool canReport = false;
                if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
                    canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();
                
                if (!canReport) 
                {
                    ResetState();
                    return; // Ignore completely during cooldown
                }
            }
            
            if (playSound && PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayErrorTone();
                
            flashRoutine = StartCoroutine(FlashRedRoutine());
        }

        private IEnumerator FlashRedRoutine()
        {
            if (meshRenderer != null)
            {
                if (errorMaterial != null)
                {
                    meshRenderer.material = errorMaterial;
                }
                else
                {
                    meshRenderer.enabled = false;
                }
            }
            
            // Vibrate / Shake the balloon to visually indicate error
            float shakeDuration = 0.35f;
            float elapsed = 0f;
            Transform shakeTarget = GetShakeTarget();

            if (shakeTarget != null)
            {
                Vector3 originalLocalPos = shakeTarget.localPosition;
                float shakeMagnitude = 0.04f; // 4cm shake amplitude

                while (elapsed < shakeDuration)
                {
                    elapsed += Time.deltaTime;
                    shakeTarget.localPosition = originalLocalPos + UnityEngine.Random.insideUnitSphere * shakeMagnitude;
                    yield return null;
                }

                shakeTarget.localPosition = originalLocalPos;
            }
            else
            {
                yield return new WaitForSeconds(shakeDuration);
            }
            
            if (meshRenderer != null) meshRenderer.enabled = true;
            ResetState();
        }

        protected override bool ShouldPlayTutorial()
        {
            // Slash tutorials are now handled by the BladeSlashManager across the entire sequence!
            return false;
        }

        public override void StartTutorial()
        {
            base.StartTutorial();
            if (spawnedTutorial != null)
            {
                spawnedTutorial.PlaySlashTutorial(transform, requiredSlashDirection);
            }
        }

        public void TriggerFinalPop(Vector3 hitPoint = default)
        {
            Pop(hitPoint);
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"<color=blue>[BladeBalloon]</color> COLLISION DETECTED with: {other.gameObject.name}");

            if (isPopped || IsSliced) return;

            // ONLY accept collisions from our custom bone hitboxes
            if (!other.name.StartsWith("Hitbox_"))
            {
                Debug.Log($"<color=yellow>[BladeBalloon] IGNORING HIT: {other.gameObject.name} is not a bone Hitbox.</color>");
                return;
            }

            if (!IsValidHand(other))
            {
                Debug.Log($"<color=yellow>[BladeBalloon] IGNORING HIT: Hand Tracking Mode restricts this hand.</color>");
                return;
            }

            if (!IsPhysicallyTouching(other)) return;

            var handVelocity = other.GetComponentInParent<IHandVelocityProvider>();
            
            if (handVelocity == null)
            {
                Debug.LogWarning($"<color=yellow>[BladeBalloon] IGNORING HIT: Could not find IHandVelocityProvider!</color>");
            }
            else
            {
                // Verify Gesture (Using Global Gesture Lock Support)
                GestureState handGesture = GetHandGesture(other);
                if (PopstrikeVR.Core.TemporarySessionData.DisableGestures)
                {
                    handGesture = GestureState.OPEN_BLADE;
                }
                
                bool isLeft = IsLeftHand(other);

                // Forgive tracking drops: Fast slashes blur the camera, causing the Meta SDK to often return UNKNOWN on the exact frame of impact.
                // We only explicitly reject the slash if the SDK definitively detects a WRONG gesture (like CLOSED_FIST).
                if (handGesture != GestureState.OPEN_BLADE && handGesture != GestureState.UNKNOWN)
                {
                    if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
                    {
                        if (!PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError())
                        {
                            return; // Cooldown active, ignore completely
                        }
                    }

                    if (BladeSlashManager.Instance != null && BladeSlashManager.Instance.IsFirstInChain(this))
                    {
                        Debug.Log($"<color=yellow>[BladeBalloon] Silently ignoring WRONG GESTURE on the very first balloon so player isn't punished for resting hand.</color>");
                        return; // Silently ignore so they can adjust
                    }

                    Debug.Log($"<color=yellow>[BladeBalloon] IGNORING SLASH: Hand is not an Open Blade.</color>");
                    
                    PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(transform.position);
                    FlashErrorAndReset(true, false);
                    return;
                }

                // Verify Speed dynamically based on difficulty
                float minSpeed = 0.5f; // Default Medium
                string diff = PopstrikeVR.Core.TemporarySessionData.Difficulty;
                if (!string.IsNullOrEmpty(diff))
                {
                    if (diff.Equals("Easy", System.StringComparison.OrdinalIgnoreCase)) minSpeed = 0.3f;
                    else if (diff.Equals("Medium", System.StringComparison.OrdinalIgnoreCase)) minSpeed = 0.5f;
                    else if (diff.Equals("Hard", System.StringComparison.OrdinalIgnoreCase)) minSpeed = 0.8f;
                }

                // --- ACCESSIBILITY: Disable velocity requirement if gestures are disabled ---
                if (PopstrikeVR.Core.TemporarySessionData.DisableGestures)
                {
                    minSpeed = 0f;
                }

                float strikeSpeed = handVelocity.GetVelocity().magnitude;
                Debug.Log($"<color=cyan>[BladeBalloon]</color> BLADE DETECTED! Slash Speed: {strikeSpeed:F2} m/s (Needs {minSpeed:F2} m/s)");
                
                if (strikeSpeed >= minSpeed)
                {
                    Debug.Log("<color=green>[BladeBalloon] SUCCESS! POPPING BALLOON!</color>");
                    HasCompletedTutorial = true;
                    
                    Vector3 contactPoint = other.ClosestPoint(transform.position);
                    Vector3 velocityDir = handVelocity.GetVelocity().normalized;
                    if (velocityDir.sqrMagnitude > 0 && Camera.main != null)
                    {
                        // Calculate perfect cinematic rotation for CFXR slashes
                        Vector3 Y = velocityDir;
                        Vector3 toCamera = (Camera.main.transform.position - transform.position).normalized;
                        Vector3 X = Vector3.Cross(Y, toCamera).normalized;
                        Vector3 Z = Vector3.Cross(X, Y).normalized;
                        
                        hitRotation = Quaternion.LookRotation(Z, Y);
                    }

                    bool isValidHit = true;
                    if (BladeSlashManager.Instance != null)
                    {
                        isValidHit = BladeSlashManager.Instance.TrySlashBalloon(this, velocityDir, isLeft);
                    }

                    // IMMEDIATELY play the slash VFX on this specific balloon, BUT ONLY if it was a valid hit!
                    if (isValidHit && PopstrikeVR.Core.PopstrikeFeedbackManager.Instance != null)
                    {
                        PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.PlayBladeVFX(transform.position, hitRotation);
                    }
                }
                else
                {
                    if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
                    {
                        if (!PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError())
                        {
                            return; // Cooldown active, ignore completely
                        }
                    }

                    if (BladeSlashManager.Instance != null && BladeSlashManager.Instance.IsFirstInChain(this))
                    {
                        Debug.Log($"<color=yellow>[BladeBalloon] Silently ignoring SLOW HIT on the very first balloon so player isn't punished for resting hand.</color>");
                        return; // Silently ignore
                    }

                    Debug.Log($"<color=yellow>[BladeBalloon] IGNORING HIT: Too slow! (Needs 0.4 m/s)</color>");
                    PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(transform.position);
                    FlashErrorAndReset(true, false);
                }
            }
        }

        protected override void PlayPopVFX(Vector3 hitPoint)
        {
            // Individual VFX is now disabled!
            // BladeSlashManager will play a single massive VFX covering all balloons!

            // GDD Rule: Hand trail lights up brilliant white
            var trails = FindObjectsOfType<GestureTrailManager>();
            foreach (var trail in trails)
            {
                trail.TriggerWhiteFlash(0.5f);
            }
        }

        protected override void PlayPopSFX()
        {
            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayBladeHum();
        }
    }
}
