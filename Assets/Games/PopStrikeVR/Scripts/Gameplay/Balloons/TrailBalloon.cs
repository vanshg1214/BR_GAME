using UnityEngine;
using TMPro;
using PopstrikeVR.Interaction;
using PopstrikeVR.Core;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// The Transparent Trail Making Test (TMT) Balloon.
    /// Requires the INDEX_POINT gesture. 
    /// Used for TMT-A (1, 2, 3...) and TMT-B (1, A, 2, B...).
    /// </summary>
    public class TrailBalloon : BaseBalloon
    {
        [Header("TMT Specifics")]
        [Tooltip("The text component inside the frosted glass sphere.")]
        public TMP_Text LabelText; 

        public string CurrentLabel { get; private set; }
        public bool IsConnected { get; private set; } = false;

        [Header("Materials")]
        public Material defaultMaterial;
        public Material errorMaterial;
        [Tooltip("Material used when the balloon is successfully touched/connected.")]
        public Material connectedMaterial;
        
        private MeshRenderer meshRenderer;
        private Transform mainCameraTransform;

        // Cached arrays to avoid garbage collection on material swaps
        private Material[] defaultMatArray;
        private Material[] connectedMatArray;
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
                errorMatArray = new Material[] { errorMaterial };
                
                if (connectedMaterial != null)
                {
                    // The connected array keeps the base material and adds the glowing rim as a second overlay!
                    connectedMatArray = new Material[] { defaultMaterial, connectedMaterial };
                }
            }
        }

        private void Start()
        {
            if (Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            // Make the number inside the balloon always face the player's headset
            if (LabelText != null && mainCameraTransform != null)
            {
                LabelText.transform.rotation = Quaternion.LookRotation(LabelText.transform.position - mainCameraTransform.position);
            }
        }

        protected override bool ShouldPlayTutorial()
        {
            // TMT tutorials are handled by the TMTSolverScript!
            return false;
        }

        public void SetupTMT(Vector3 spawnPosition, string label)
        {
            base.Setup(spawnPosition);
            CurrentLabel = label;
            IsConnected = false;

            if (meshRenderer != null && defaultMatArray != null)
                meshRenderer.materials = defaultMatArray;

            if (LabelText != null)
            {
                LabelText.text = label;
            }
            
            // Trigger approach pulse (Two splashes on the Vignette border)
            if (PopstrikeVR.Visuals.ScreenEffectsController.Instance != null)
            {
                float intensity = 0.6f;
                Color spawnColor = new Color(1f, 1f, 1f, 1f);
                if (PopstrikeVR.Core.PopstrikeFeedbackManager.Instance != null)
                {
                    intensity = PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.SpawnVignetteIntensity;
                    spawnColor = PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.TrailSpawnColor;
                }
                PopstrikeVR.Visuals.ScreenEffectsController.Instance.TriggerSpawnWarning(spawnColor, intensity);
            }
            
            spawnDelay = 0.5f;
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"<color=silver>[TrailBalloon]</color> COLLISION DETECTED with: {other.gameObject.name}");

            if (isPopped || IsConnected) return;

            // Trail balloons have a wider forgiveness margin when gestures are disabled (22cm) to make hitting them with a fist much easier.
            float forgiveness = PopstrikeVR.Core.TemporarySessionData.DisableGestures ? 0.22f : 0.10f;
            if (!IsPhysicallyTouching(other, forgiveness)) return;

            // ONLY accept collisions from the Index Tip by default! 
            // This prevents accidental knuckle or palm bumps from triggering false positive errors.
            // BUT if gestures are disabled, allow any part of the hand to trigger it.
            if (!PopstrikeVR.Core.TemporarySessionData.DisableGestures)
            {
                if (!other.name.Contains("Index3") && !other.name.Contains("IndexTip"))
                {
                    Debug.Log($"<color=yellow>[TrailBalloon] IGNORING HIT: {other.gameObject.name} is not the Index Tip.</color>");
                    return;
                }
            }
            else
            {
                if (!other.name.StartsWith("Hitbox_")) return;
            }

            if (!IsValidHand(other))
            {
                Debug.Log($"<color=yellow>[TrailBalloon] IGNORING HIT: Hand Tracking Mode restricts this hand.</color>");
                return;
            }

            // Verify Gesture (With Gesture Lock Support)
            GestureState handGesture = GetHandGesture(other);
            
            // GESTURE LOCK: If they are in the middle of a TMT sequence, assume the camera glitched and they are still pointing
            if (TMTSolverScript.Instance != null && TMTSolverScript.Instance.HasSequenceStarted())
            {
                handGesture = GestureState.INDEX_POINT;
            }

            // ACCESSIBILITY BYPASS: If gestures are disabled, force it to pass
            if (PopstrikeVR.Core.TemporarySessionData.DisableGestures)
            {
                handGesture = GestureState.INDEX_POINT;
            }

            if (handGesture != GestureState.INDEX_POINT)
            {
                bool isLeft = IsLeftHand(other);
                Debug.Log($"<color=yellow>[TrailBalloon] IGNORING TOUCH: Hand is not Pointing.</color>");
                
                SetErrorState();
                return; 
            }

            Debug.Log($"<color=green>[TrailBalloon] SUCCESS! Touched Trail balloon {CurrentLabel}.</color>");
            
            if (TMTSolverScript.Instance != null)
            {
                bool isValid = TMTSolverScript.Instance.ValidateHit(gameObject);
                if (isValid)
                {
                    IsConnected = true; 
                    
                    // SWAP TO GLOWING OVERLAY MATERIAL
                    if (meshRenderer != null && connectedMatArray != null)
                    {
                        meshRenderer.materials = connectedMatArray;
                    }
                    
                    // Note: Audio is now fully handled by TMTSolverScript to properly play ascending scales!
                }
                else
                {
                    // Only play the buzzer and flash red if the sequence has actually started!
                    if (TMTSolverScript.Instance.HasSequenceStarted())
                    {
                        SetErrorState();
                    }
                }
            }
        }

        private bool skipSFX = false;

        public void TriggerFinalPop(bool silent = false)
        {
            skipSFX = silent;
            Pop();
        }

        public override void Pop(Vector3 hitPoint = default)
        {
            if (isPopped) return;
            isPopped = true;

            if (InteractionCollider != null)
                InteractionCollider.enabled = false;

            if (hitPoint == default) hitPoint = transform.position;

            // Custom professional dissolve sequence: Fade/shrink first, THEN play VFX
            StartCoroutine(DissolvePopRoutine(hitPoint));
        }

        private System.Collections.IEnumerator DissolvePopRoutine(Vector3 hitPoint)
        {
            // 1. Gently "fade/shrink" the balloon over 0.25 seconds
            float fadeDuration = 0.25f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = startScale * 0.7f; // Shrink to 70% before turning to dust
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                // Smooth ease-out curve
                float easeT = t * (2f - t); 
                transform.localScale = Vector3.Lerp(startScale, targetScale, easeT);
                yield return null;
            }

            // 2. Hide the balloon mesh completely
            transform.localScale = Vector3.zero;

            // 3. Play the beautiful dissolve VFX and the chime sound!
            // The VFX Particle System reads the original Mesh shape, so it perfectly matches
            // the 10cm balloon size, even though we just shrank the actual GameObject!
            PlayPopVFX(hitPoint);
            PlayPopSFX();

            // 4. Safely return to the object pool
            PopstrikePooler.DespawnBalloon(this.gameObject, 0.2f);
        }

        public void SetErrorState()
        {
            // This is ONLY called when the player touches the WRONG balloon in the sequence.
            
            bool canReport = false;
            if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
            {
                canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();
            }

            if (!canReport) return; // Completely ignore if cooldown is active

            IsConnected = false;
            StartCoroutine(FlashRedRoutine());
            PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(transform.position);
            
            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayErrorTone();
        }

        public void ResetVisualState()
        {
            // Silent reset without the annoying buzzer (used when the 3-second link breaks)
            IsConnected = false;
            if (meshRenderer != null && defaultMatArray != null)
                meshRenderer.materials = defaultMatArray;
        }

        public void DeflateForcefully()
        {
            // Called when the patient fails 3 times
            isPopped = true;
            gameObject.SetActive(false); 
        }

        private System.Collections.IEnumerator FlashRedRoutine()
        {
            if (meshRenderer != null && errorMatArray != null)
                meshRenderer.materials = errorMatArray;
            
            yield return new WaitForSeconds(0.3f);
            
            if (meshRenderer != null && defaultMatArray != null)
                meshRenderer.materials = defaultMatArray;
        }

        protected override void PlayPopVFX(Vector3 hitPoint)
        {
            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayTrailVFX(hitPoint);
        }

        protected override void PlayPopSFX()
        {
            if (skipSFX) return;

            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayTrailFlourish();
        }
    }
}
