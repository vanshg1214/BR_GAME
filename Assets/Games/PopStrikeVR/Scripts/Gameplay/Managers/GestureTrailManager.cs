using UnityEngine;
using PopstrikeVR.Interaction;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// Dynamically controls Trail Renderers based on the active gesture to ensure 
    /// a clean, professional look. Prevents trails from bunching up when making a fist.
    /// </summary>
    public class GestureTrailManager : MonoBehaviour
    {
        [Header("Trail Renderers")]
        [Tooltip("The trail attached specifically to the Index finger tip.")]
        public TrailRenderer IndexTrail;
        
        [Tooltip("The trail attached to the edge of the hand for slicing.")]
        public TrailRenderer BladeTrail;

        [Tooltip("Optional: The trail attached to the knuckles for punches.")]
        public TrailRenderer FistTrail;

        [Header("Hand Glow Materials (Secondary Rim)")]
        public Material fistGlowMaterial;
        public Material bladeGlowMaterial;
        public Material traceGlowMaterial;
        public Material tmtGlowMaterial;

        [Header("Configuration")]
        public bool isLeftHand;
        
        [Header("Velocity Settings")]
        [Tooltip("The minimum speed (m/s) the hand must move for trails to appear.")]
        public float minTrailVelocity = 0.15f;

        [Header("Glow Intensity (HDR)")]
        [Tooltip("Multiplies the final emission/glow intensity of the materials on this trail.")]
        public float glowIntensityMultiplier = 1.0f;
        private float lastGlowMultiplier = -1f;

        [Header("TMT Trail Options")]
        [Tooltip("The gradient of the index trail when a TMT balloon sequence is active.")]
        public Gradient tmtTrailGradient;
        private Gradient defaultIndexTrailGradient;
        private bool isTMTMode = false;

        private OVRSkeleton ovrSkeleton;
        private bool bonesInitialized = false;
        
        private Vector3 lastPosition;
        private float currentSpeed;

        private bool isFlashingWhite = false;
        private float whiteFlashTimer = 0f;
        
        private SkinnedMeshRenderer handMeshRenderer;
        private Material baseHandMaterial;
        private Material currentGlowMaterial;

        private void Awake()
        {
            ovrSkeleton = GetComponent<OVRSkeleton>();
            lastPosition = transform.position;

            if (IndexTrail != null)
            {
                defaultIndexTrailGradient = IndexTrail.colorGradient;
            }
        }

        private void TryFindHandMesh()
        {
            if (handMeshRenderer == null)
            {
                handMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                if (handMeshRenderer != null && handMeshRenderer.sharedMaterials.Length > 0)
                {
                    baseHandMaterial = handMeshRenderer.sharedMaterials[0];
                }
            }
        }

        private void SetHandGlowMaterial(Material glowMat)
        {
            if (handMeshRenderer == null || baseHandMaterial == null) return;
            
            // Only update if it actually changed
            if (currentGlowMaterial == glowMat) return;
            currentGlowMaterial = glowMat;
            
            if (glowMat == null)
            {
                handMeshRenderer.materials = new Material[] { baseHandMaterial };
            }
            else
            {
                handMeshRenderer.materials = new Material[] { baseHandMaterial, glowMat };
            }
        }

        public void SetTMTMode(bool active)
        {
            if (isTMTMode == active) return;
            isTMTMode = active;
            
            if (IndexTrail != null)
            {
                IndexTrail.colorGradient = isTMTMode ? tmtTrailGradient : defaultIndexTrailGradient;
            }

            ApplyGlowMultiplier();
        }

        public void TriggerWhiteFlash(float duration)
        {
            isFlashingWhite = true;
            whiteFlashTimer = duration;
            ApplyGlowMultiplier();
        }

        private void UpdateFlashTimer()
        {
            if (isFlashingWhite)
            {
                whiteFlashTimer -= Time.deltaTime;
                if (whiteFlashTimer <= 0f)
                {
                    isFlashingWhite = false;
                    ApplyGlowMultiplier();
                }
            }
        }

        private void Update()
        {
            UpdateFlashTimer();
            TryFindHandMesh();

            if (ovrSkeleton != null && ovrSkeleton.IsInitialized && !bonesInitialized)
            {
                AttachTrailsToBones();
                bonesInitialized = true;
            }

            if (GestureDetector.Instance == null) return;

            // Calculate Hand Speed
            float speed = (transform.position - lastPosition).magnitude / Time.deltaTime;
            lastPosition = transform.position;
            currentSpeed = Mathf.Lerp(currentSpeed, speed, Time.deltaTime * 10f);
            
            bool isMovingFastEnough = currentSpeed > minTrailVelocity;

            GestureState currentState = isLeftHand ? GestureDetector.Instance.LeftState : GestureDetector.Instance.RightState;

            // ENFORCE HAND TRACKING MODE: Force unused hands to appear idle (no trails, no glow)
            var mode = PopstrikeVR.Core.TemporarySessionData.HandMode;
            if (mode == PopstrikeVR.Core.HandTrackingMode.LeftHandOnly && !isLeftHand)
            {
                currentState = GestureState.UNKNOWN;
            }
            else if (mode == PopstrikeVR.Core.HandTrackingMode.RightHandOnly && isLeftHand)
            {
                currentState = GestureState.UNKNOWN;
            }

            switch (currentState)
            {
                case GestureState.INDEX_POINT:
                    SetTrailState(IndexTrail, isMovingFastEnough);
                    SetTrailState(BladeTrail, false);
                    SetTrailState(FistTrail, false);
                    SetHandGlowMaterial(isTMTMode ? tmtGlowMaterial : traceGlowMaterial);
                    break;
                    
                case GestureState.OPEN_BLADE:
                    SetTrailState(IndexTrail, false);
                    SetTrailState(BladeTrail, isMovingFastEnough);
                    SetTrailState(FistTrail, false);
                    SetHandGlowMaterial(bladeGlowMaterial);
                    break;
                    
                case GestureState.CLOSED_FIST:
                    SetTrailState(IndexTrail, false);
                    SetTrailState(BladeTrail, false);
                    SetTrailState(FistTrail, isMovingFastEnough);
                    SetHandGlowMaterial(fistGlowMaterial);
                    break;

                case GestureState.UNKNOWN:
                default:
                    // Turn all trails off when resting/idle to look professional
                    SetTrailState(IndexTrail, false);
                    SetTrailState(BladeTrail, false);
                    SetTrailState(FistTrail, false);
                    SetHandGlowMaterial(null);
                    break;
            }

            if (glowIntensityMultiplier != lastGlowMultiplier)
            {
                ApplyGlowMultiplier();
            }
        }

        private void ApplyGlowMultiplier()
        {
            lastGlowMultiplier = glowIntensityMultiplier;
            TrailRenderer[] allTrails = { IndexTrail, FistTrail, BladeTrail };
            foreach (var t in allTrails)
            {
                if (t == null) continue;
                
                var r = t.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    r.GetPropertyBlock(mpb);
                    
                    bool isIndex = (t == IndexTrail);

                    BoostHDRColor(r.sharedMaterial, mpb, "_EmissionColor", isIndex);
                    BoostHDRColor(r.sharedMaterial, mpb, "_RimColor", isIndex);
                    BoostHDRColor(r.sharedMaterial, mpb, "_BaseColor", isIndex);
                    BoostHDRColor(r.sharedMaterial, mpb, "_Color", isIndex);
                    BoostHDRColor(r.sharedMaterial, mpb, "_TintColor", isIndex);
                    
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        private void BoostHDRColor(Material mat, MaterialPropertyBlock mpb, string propertyName, bool isIndexTrail)
        {
            if (mat.HasProperty(propertyName))
            {
                Color c = mat.GetColor(propertyName);

                if (isFlashingWhite)
                {
                    c = new Color(1f, 1f, 1f, c.a);
                }

                // Multiply RGB for HDR bloom, but preserve the original Alpha (transparency)
                Color hdrColor = new Color(c.r * glowIntensityMultiplier, c.g * glowIntensityMultiplier, c.b * glowIntensityMultiplier, c.a);
                mpb.SetColor(propertyName, hdrColor);
            }
        }

        private void SetTrailState(TrailRenderer trail, bool enable)
        {
            if (trail == null) return;
            
            trail.emitting = enable;
            
            // Instantly clear the trail when disabled so it doesn't drag across the screen
            if (!enable && trail.positionCount > 0)
            {
                trail.Clear();
            }
        }

        private void AttachTrailsToBones()
        {
            // Search all child transforms to find the exact bones by name.
            // This is 100% robust across OVR and OpenXR hand rigs.
            Transform[] allBones = GetComponentsInChildren<Transform>();

            bool indexFound = false;
            bool fistFound = false;
            bool bladeFound = false;

            foreach (Transform bone in allBones)
            {
                string boneName = bone.name.ToLower();

                // 1. INDEX TRAIL -> Index Tip
                if (!indexFound && IndexTrail != null && boneName.Contains("indextip"))
                {
                    IndexTrail.transform.SetParent(bone, false);
                    IndexTrail.transform.localPosition = Vector3.zero;
                    IndexTrail.transform.localRotation = Quaternion.identity;
                    indexFound = true;
                }
                
                // 2. FIST TRAIL -> Index Intermediate (The front striking knuckles of the fist)
                if (!fistFound && FistTrail != null && boneName.Contains("index") && (boneName.Contains("intermediate") || boneName.Contains("2")))
                {
                    FistTrail.transform.SetParent(bone, false);
                    FistTrail.transform.localPosition = Vector3.zero;
                    FistTrail.transform.localRotation = Quaternion.identity;
                    fistFound = true; 
                }

                // 3. BLADE TRAIL -> Center of 4 fingers (Middle Proximal/Intermediate)
                if (!bladeFound && BladeTrail != null && boneName.Contains("middle") && (boneName.Contains("proximal") || boneName.Contains("intermediate") || boneName.Contains("1") || boneName.Contains("2")))
                {
                    BladeTrail.transform.SetParent(bone, false);
                    BladeTrail.transform.localPosition = Vector3.zero;
                    BladeTrail.transform.localRotation = Quaternion.identity;
                    bladeFound = true;
                }
            }
        }
    }
}
