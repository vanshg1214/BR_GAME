using System.Collections.Generic;
using UnityEngine;
using PopstrikeVR.Interaction;
using PopstrikeVR.Core;

namespace PopstrikeVR.Gameplay
{
    public class TracePathManager : MonoBehaviour
    {
        public static TracePathManager Instance { get; private set; }

        [Header("Visual References")]
        [Tooltip("The LineRenderer representing the tube. If not assigned, will be created/found automatically.")]
        public LineRenderer vineRenderer;
        [Tooltip("The ParticleSystem for flowing leaves. If not assigned, will be created/found automatically.")]
        public ParticleSystem flowParticleSystem;

        [Header("Leaf Configurations")]
        [Tooltip("The 3D Leaf Mesh to flow inside the tube.")]
        public Mesh leafMesh;
        [Tooltip("Optional leaf material. If not assigned, a glowing green material is created.")]
        public Material leafMaterial;
        [Tooltip("How long it takes a leaf to travel the entire length of the tube (seconds). Lower number = Faster speed.")]
        public float leafTravelTime = 1.5f;
        [Tooltip("How many leaves spawn per second inside the tube.")]
        public float leavesPerSecond = 25f;

        [Header("Tube Configurations")]
        [Tooltip("Width of the transparent tube.")]
        public float tubeWidth = 0.08f; // 8cm wide transparent pipe
        [Tooltip("How strictly the player must stay within the path. 0.15 = 15cm wide corridor (lenient). 0.04 = 4cm (strict).")]
        public float corridorTolerance = 0.15f; 
        [Tooltip("The material to apply to the trace path tube.")]
        public Material tubeMaterial;
        
        [Tooltip("Optional secondary material if you want a layered or dual-material look.")]
        public Material secondaryTubeMaterial;

        [Tooltip("Visually adjusts the gap between the glass tube and the balloons. Increased to 0.045f per user request to subtract an extra 0.3cm.")]
        public float tubeLengthOffset = 0.045f;

        [Header("Tutorial System")]
        [Tooltip("The Tutorial Animator prefab containing the hand pointing icon.")]
        public PopstrikeVR.UI.TutorialGestureAnimator tutorialPrefab;
        private PopstrikeVR.UI.TutorialGestureAnimator spawnedTutorial;
        
        public static bool HasCompletedTutorial = false;

        private List<TraceBalloon> activeSequence = new List<TraceBalloon>();
        private int currentTargetIndex = 0;
        
        private bool isTracking = false;
        public bool IsTracking => isTracking;
        private Transform trackingHand;
        private bool trackingIsLeftHand = false; // Which hand started the trace

        public bool IsSequenceActive { get; private set; } = false;
        private int chancesLeft = 2;

        private ParticleSystem.Particle[] particles;
        private Vector3[] pathPositions;
        private float[] segmentLengths;
        private float totalPathLength = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 1. LineRenderer (Transparent Glass Connection Tube) Setup
            if (vineRenderer == null)
            {
                GameObject tubeObj = new GameObject("Trace_TubeRenderer");
                tubeObj.transform.SetParent(transform);
                tubeObj.transform.localPosition = Vector3.zero;
                vineRenderer = tubeObj.AddComponent<LineRenderer>();
            }
            vineRenderer.startWidth = tubeWidth;
            vineRenderer.endWidth = tubeWidth;
            vineRenderer.positionCount = 0;
            
            // Smooth edges instead of sharp corners
            vineRenderer.numCornerVertices = 8;
            vineRenderer.numCapVertices = 8;

            // Apply material to tube
            if (tubeMaterial != null)
            {
                if (secondaryTubeMaterial != null)
                {
                    // Apply BOTH materials exactly as the user authored them (no script overrides)
                    vineRenderer.materials = new Material[] { tubeMaterial, secondaryTubeMaterial };
                }
                else
                {
                    // Apply single material exactly as authored
                    vineRenderer.material = tubeMaterial;
                }
            }
            else if (vineRenderer.sharedMaterial == null)
            {
                Debug.LogWarning("[TracePathManager] No Tube Material assigned! Please assign one in the Inspector.");
            }

            // Force render behind balloons
            if (vineRenderer.sharedMaterial != null)
            {
                vineRenderer.sharedMaterial.renderQueue = 2990;
            }

            // We are using 3D Cylinders now instead of the LineRenderer to support proper 3D materials!
            vineRenderer.enabled = false; 

            // 2. ParticleSystem (Flowing Particles) Setup
            if (flowParticleSystem == null)
            {
                flowParticleSystem = GetComponent<ParticleSystem>();
                if (flowParticleSystem == null)
                {
                    flowParticleSystem = gameObject.AddComponent<ParticleSystem>();
                }
            }

            var main = flowParticleSystem.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = leafTravelTime; // Tunable travel speed
            main.startSpeed = 0f; // Controlled by script interpolation
            
            // Set size appropriate for meshes
            if (leafMesh != null)
                main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f); // Much smaller mesh size multiplier
            else
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.02f); // Billboard size
                
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = flowParticleSystem.emission;
            emission.enabled = false; // Only emit when a path is active
            emission.rateOverTime = leavesPerSecond; // Tunable leaf density

            var shape = flowParticleSystem.shape;
            shape.enabled = false; // Script positions particles

            // Tumbling rotation for 3D leaf models
            var rot = flowParticleSystem.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.xMultiplier = 3f;
            rot.yMultiplier = 5f;
            rot.zMultiplier = 3f;

            var col = flowParticleSystem.colorOverLifetime;
            col.enabled = true;
            Gradient flowGrad = new Gradient();
            flowGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(1.0f, 0.15f), new GradientAlphaKey(1.0f, 0.85f), new GradientAlphaKey(0f, 1.0f) }
            );
            col.color = flowGrad;

            // Particle renderer setup
            ParticleSystemRenderer psr = flowParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (psr == null)
            {
                psr = flowParticleSystem.gameObject.AddComponent<ParticleSystemRenderer>();
            }
            
            if (leafMesh != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Mesh;
                psr.mesh = leafMesh;
            }
            else
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            if (leafMaterial != null)
            {
                psr.material = leafMaterial;
            }
            else if (psr.sharedMaterial == null)
            {
                Material leafMat = new Material(Shader.Find("Sprites/Default"));
                leafMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                leafMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive glowing green
                leafMat.SetInt("_ZWrite", 0);
                leafMat.renderQueue = 2991; // Render behind balloons but above tube
                leafMat.SetColor("_Color", new Color(0.2f, 1.0f, 0.3f, 1f) * 2.0f); // Glowing Green HDR
                psr.material = leafMat;
            }
            
            // Force leaves to render behind balloons
            if (psr.sharedMaterial != null)
            {
                psr.sharedMaterial.renderQueue = 2991;
            }
        }

        private void Update()
        {
            if (activeSequence.Count > 0)
            {
                // Check if any balloon has been despawned/timed out by LevelDirector
                bool anyDespawned = false;
                foreach(var b in activeSequence)
                {
                    if (b == null || !b.gameObject.activeInHierarchy)
                    {
                        anyDespawned = true; break;
                    }
                }
                if (anyDespawned)
                {
                    ClearPath();
                    return;
                }
            }

            if (!isTracking || trackingHand == null || activeSequence.Count == 0) return;

            // 1. Verify Gesture — CHECK ONLY THE TRACING HAND (With Gesture Lock)
            GestureState tracingHandState = trackingIsLeftHand 
                ? GestureDetector.Instance.LeftState 
                : GestureDetector.Instance.RightState;
                
            // GESTURE LOCK: If they are actively inside the tube (isTracking = true), assume camera glitches are false and they are still pointing
            tracingHandState = GestureState.INDEX_POINT;
                
            if (tracingHandState != GestureState.INDEX_POINT)
            {
                FailTrace();
                return;
            }

            // 2. Verify Corridor Deviation
            Vector3 handPos = trackingHand.position;
            
            // Only forward trace is allowed (direction of the leaves)
            Vector3 segmentStart = activeSequence[currentTargetIndex - 1].transform.position;
            Vector3 segmentEnd = activeSequence[currentTargetIndex].transform.position;

            float distanceToPath = DistancePointToSegment(handPos, segmentStart, segmentEnd, out float distAlongSegment);
            float distToStart = Vector3.Distance(handPos, segmentStart);
            float distToEnd = Vector3.Distance(handPos, segmentEnd);

            // A small forgiveness radius (15cm) around the exact center of the balloons to account for the balloon's physical size
            float balloonForgivenessRadius = 0.15f; 

            // Enforce a minimum realistic human tolerance of 12cm.
            float actualTolerance = Mathf.Max(corridorTolerance, 0.12f);

            // If they are not inside the balloon volume, they MUST be inside the trace tube corridor!
            if (distToStart > balloonForgivenessRadius && distToEnd > balloonForgivenessRadius)
            {
                if (distanceToPath > actualTolerance)
                {
                    FailTrace();
                    return;
                }
                
                // Optional: Prevent them from pulling their hand backwards far away from the start
                if (distAlongSegment < -0.1f)
                {
                    FailTrace();
                    return;
                }
            }
        }

        private void LateUpdate()
        {
            if (activeSequence.Count < 2 || flowParticleSystem == null || pathPositions == null || pathPositions.Length < 2) return;

            int maxParticles = flowParticleSystem.main.maxParticles;
            if (particles == null || particles.Length < maxParticles)
            {
                particles = new ParticleSystem.Particle[maxParticles];
            }

            int numParticlesAlive = flowParticleSystem.GetParticles(particles);

            for (int i = 0; i < numParticlesAlive; i++)
            {
                // Calculate progress (0 = start of line, 1 = end of line)
                float t = 1f - (particles[i].remainingLifetime / particles[i].startLifetime);

                Vector3 pathPos = GetPositionOnPath(t, out Vector3 dir);

                // Build coordinate basis perpendicular to the path direction
                Vector3 ortho = Vector3.Cross(dir, Vector3.up).normalized;
                if (ortho.sqrMagnitude < 0.01f)
                {
                    ortho = Vector3.Cross(dir, Vector3.forward).normalized;
                }
                Vector3 binormal = Vector3.Cross(dir, ortho).normalized;

                // Create a dynamic, swirling "water flow" effect
                // Unique phase offset per particle based on its ID
                float idOffset = (particles[i].randomSeed % 1000) / 1000f; 
                
                // Angle revolves over time and distance, making them spiral down the tube
                float angle = (Time.time * 3f) + (t * Mathf.PI * 4f) + (idOffset * Mathf.PI * 2f);
                
                // Radius oscillates organically but STRICTLY stays within the tube boundaries.
                // tubeWidth is the diameter. Max safe offset is tubeWidth * 0.25f to ensure the physical leaf meshes don't clip outside the glass.
                float maxR = tubeWidth * 0.25f; 
                float r = maxR * Mathf.Abs(Mathf.Sin((Time.time * 2f) + (idOffset * 10f)));

                // Add drifting offset inside the tube
                Vector3 offset = (ortho * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * r;
                particles[i].position = pathPos + offset;
            }

            flowParticleSystem.SetParticles(particles, numParticlesAlive);
        }

        private Vector3 GetPositionOnPath(float t, out Vector3 direction)
        {
            direction = Vector3.up;
            if (pathPositions == null || pathPositions.Length == 0) return Vector3.zero;
            if (pathPositions.Length == 1) return pathPositions[0];

            float targetDist = t * totalPathLength;
            float currentDist = 0f;

            for (int i = 0; i < pathPositions.Length - 1; i++)
            {
                float segLen = segmentLengths[i];
                if (currentDist + segLen >= targetDist)
                {
                    float segT = (targetDist - currentDist) / segLen;
                    direction = (pathPositions[i + 1] - pathPositions[i]).normalized;
                    return Vector3.Lerp(pathPositions[i], pathPositions[i + 1], segT);
                }
                currentDist += segLen;
            }

            if (pathPositions.Length >= 2)
            {
                direction = (pathPositions[pathPositions.Length - 1] - pathPositions[pathPositions.Length - 2]).normalized;
            }
            return pathPositions[pathPositions.Length - 1];
        }

        private List<GameObject> cylinderSegments = new List<GameObject>();

        public void RegisterSequence(List<GameObject> balloons)
        {
            ClearPath();
            chancesLeft = 2; // They get 1 retry
            IsSequenceActive = true;
            vineRenderer.positionCount = balloons.Count;
            pathPositions = new Vector3[balloons.Count];

            for (int i = 0; i < balloons.Count; i++)
            {
                if (balloons[i].TryGetComponent<TraceBalloon>(out var trace))
                {
                    activeSequence.Add(trace);
                    trace.ResetState();
                    Vector3 pos = trace.transform.position;
                    vineRenderer.SetPosition(i, pos);
                    pathPositions[i] = pos;
                }
            }

            // Calculate segment lengths and spawn true 3D Cylinder meshes!
            if (balloons.Count > 1)
            {
                segmentLengths = new float[balloons.Count - 1];
                totalPathLength = 0f;
                for (int i = 0; i < balloons.Count - 1; i++)
                {
                    segmentLengths[i] = Vector3.Distance(pathPositions[i], pathPositions[i + 1]);
                    totalPathLength += segmentLengths[i];

                    // Spawn a true 3D cylindrical mesh to connect the balloons
                    Vector3 start = pathPositions[i];
                    Vector3 end = pathPositions[i + 1];
                    
                    GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    cyl.name = $"TraceCylinder_{i}";
                    cyl.transform.SetParent(this.transform);
                    
                    // Position at the exact midpoint
                    cyl.transform.position = Vector3.Lerp(start, end, 0.5f);
                    
                    // Rotate to point from start to end (Cylinders face UP by default)
                    cyl.transform.up = (end - start).normalized;
                    
                    // The user can adjust tubeLengthOffset in the Inspector to perfectly match their visual balloon size!
                    float balloonRadius = tubeLengthOffset; 
                    
                    float meshLength = segmentLengths[i] - (balloonRadius * 2f);
                    if (meshLength < 0.01f) meshLength = 0.01f; // Failsafe
                    
                    // Scale: Target scale for the fully grown tube
                    Vector3 targetScale = new Vector3(tubeWidth, meshLength / 2f, tubeWidth);
                    
                    // Start at scale 0 so we can animate it!
                    cyl.transform.localScale = Vector3.zero;
                    StartCoroutine(AnimateTubeSpawn(cyl.transform, targetScale, 0.5f));
                    
                    // Remove collider so it doesn't block the hand interactions
                    Destroy(cyl.GetComponent<Collider>());
                    
                    // Apply Materials EXACTLY as requested by the user
                    MeshRenderer mr = cyl.GetComponent<MeshRenderer>();
                    if (tubeMaterial != null)
                    {
                        if (secondaryTubeMaterial != null)
                        {
                            mr.materials = new Material[] { tubeMaterial, secondaryTubeMaterial };
                        }
                        else
                        {
                            mr.material = tubeMaterial;
                        }
                    }
                    else if (vineRenderer.sharedMaterial != null)
                    {
                        mr.material = vineRenderer.sharedMaterial;
                    }
                    
                    cylinderSegments.Add(cyl);
                }
                
                // Turn on particle emission
                if (flowParticleSystem != null)
                {
                    var emission = flowParticleSystem.emission;
                    emission.enabled = true;
                }
            }
            
            // Automatically play the Trace tutorial!
            PlayTutorialSequence();
        }

        private System.Collections.IEnumerator AnimateTubeSpawn(Transform tubeTransform, Vector3 targetScale, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (tubeTransform == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Optional: Use a slight ease-out curve for a smoother pop-in
                float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
                
                tubeTransform.localScale = Vector3.Lerp(Vector3.zero, targetScale, smoothT);
                yield return null;
            }
            if (tubeTransform != null) tubeTransform.localScale = targetScale;
        }

        public void PlayTutorialSequence()
        {
            if (!HasCompletedTutorial && tutorialPrefab != null && activeSequence != null && activeSequence.Count > 0)
            {
                if (spawnedTutorial == null)
                {
                    spawnedTutorial = Instantiate(tutorialPrefab, activeSequence[0].transform.position, Quaternion.identity);
                }
                
                Transform[] points = new Transform[activeSequence.Count];
                for (int i = 0; i < activeSequence.Count; i++)
                {
                    points[i] = activeSequence[i].transform;
                }
                spawnedTutorial.PlayTraceTutorial(points);
            }
        }

        public void OnBalloonHit(TraceBalloon hitBalloon, Transform handTransform)
        {
            if (activeSequence.Count == 0) return;

            // Start Logic (Strictly Forward Only)
            if (!isTracking)
            {
                if (hitBalloon == activeSequence[0])
                {
                    // Start Normal
                    trackingHand = handTransform;
                    trackingIsLeftHand = IsLeftHandHitbox(handTransform);
                    isTracking = true;
                    currentTargetIndex = 1;
                    hitBalloon.MarkCompleted();
                    
                    if (PopstrikeFeedbackManager.Instance != null)
                        PopstrikeFeedbackManager.Instance.PlayTraceChime();
                        
                    if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
                        PopstrikeVR.Interaction.GestureDetector.Instance.LockGesture(trackingIsLeftHand, PopstrikeVR.Interaction.GestureState.INDEX_POINT);
                        
                    Debug.Log($"<color=green>[TracePathManager] Started tracing FORWARD with {(trackingIsLeftHand ? "LEFT" : "RIGHT")} hand.</color>");
                    return;
                }
                else
                {
                    // Try to report the error. If cooldown is active, it returns false, so we ignore the mistake!
                    bool canReport = false;
                    if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
                        canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();

                    if (!canReport) return; // Completely ignore if cooldown is active

                    // Hit wrong balloon first (middle or end balloon)
                    if (PopstrikeFeedbackManager.Instance != null)
                        PopstrikeFeedbackManager.Instance.PlayErrorTone();
                        
                    PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(hitBalloon.transform.position);
                    hitBalloon.FlashErrorAndReset();
                    return;
                }
            }

            // Reject hits from the non-tracking hand to prevent "just tapping" the end balloon
            if (trackingHand != null && handTransform != trackingHand)
            {
                return;
            }

            // We are already tracking. Is this the CORRECT NEXT balloon?
            if (hitBalloon == activeSequence[currentTargetIndex])
            {
                hitBalloon.MarkCompleted();
                
                if (PopstrikeFeedbackManager.Instance != null)
                    PopstrikeFeedbackManager.Instance.PlayTraceChime();

                currentTargetIndex++;
                if (currentTargetIndex >= activeSequence.Count) CompleteTrace();
            }
            else
            {
                // Hit wrong balloon while tracking -> Enforce strict sequencing and fail
                PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(hitBalloon.transform.position);
                hitBalloon.FlashErrorAndReset();
                FailTrace();
            }
        }

        private void CompleteTrace()
        {
            HasCompletedTutorial = true;
            PopstrikeVR.Gameplay.ComboManager.Instance?.RegisterHit(100);

            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayTraceFlourish();

            foreach (var b in activeSequence)
            {
                if (b != null) b.TriggerFinalPop(silent: true);
            }

            ClearPath();
        }

        private void ClearPath()
        {
            if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
            {
                PopstrikeVR.Interaction.GestureDetector.Instance.UnlockGesture(true);
                PopstrikeVR.Interaction.GestureDetector.Instance.UnlockGesture(false);
            }

            isTracking = false;
            trackingHand = null;
            currentTargetIndex = 0;
            IsSequenceActive = false;
            activeSequence.Clear();
            
            if (spawnedTutorial != null)
            {
                spawnedTutorial.StopTutorial();
                Destroy(spawnedTutorial.gameObject);
                spawnedTutorial = null;
            }

            if (vineRenderer != null)
            {
                vineRenderer.positionCount = 0;
            }

            // Despawn 3D Cylinder meshes
            foreach (var cyl in cylinderSegments)
            {
                if (cyl != null) Destroy(cyl.gameObject);
            }
            cylinderSegments.Clear();

            if (flowParticleSystem != null)
            {
                var emission = flowParticleSystem.emission;
                emission.enabled = false;
                flowParticleSystem.Clear();
            }
            pathPositions = null;
            segmentLengths = null;
            totalPathLength = 0f;
        }

        private void FailTrace()
        {
            // Try to report the error. If cooldown is active, it returns false, so we ignore the mistake!
            bool canReport = false;
            if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
            {
                canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();
            }

            Debug.LogWarning($"[TracePathManager] Trace failed! canReport={canReport}. Hand deviated from corridor or lost index gesture.");
            
            isTracking = false;
            trackingHand = null;
            currentTargetIndex = 0;

            // Spawn the floating Red Cross where the player's hand deviated
            if (trackingHand != null)
            {
                PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(trackingHand.position);
            }
            else if (activeSequence.Count > 0 && activeSequence[0] != null)
            {
                PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(activeSequence[0].transform.position);
            }
            
            // Give them another chance visually
            foreach (var b in activeSequence)
            {
                if (b != null) b.FlashErrorAndReset();
            }

            if (!canReport) return; // Cooldown is active. Free pass! No sound, no chance deducted!

            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayErrorTone();

            chancesLeft--;
            
            if (chancesLeft <= 0)
            {
                Debug.LogError("[TracePathManager] FAILED! Player ran out of chances.");
                foreach (var b in activeSequence)
                {
                    if (b != null)
                    {
                        b.AnimateDespawn(0.5f);
                        PopstrikePooler.DespawnBalloon(b.gameObject, 0.5f);
                    }
                }
                ClearPath();
                return;
            }
        }

        private float DistancePointToSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd, out float distAlongSegment)
        {
            Vector3 line = lineEnd - lineStart;
            float len = line.magnitude;
            line.Normalize();

            Vector3 v = point - lineStart;
            float d = Vector3.Dot(v, line);
            d = Mathf.Clamp(d, 0f, len);
            distAlongSegment = d;
            
            Vector3 closestPoint = lineStart + line * d;
            return Vector3.Distance(point, closestPoint);
        }

        /// <summary>
        /// Determines if a Hitbox transform belongs to the left hand.
        /// Uses the same detection pattern as BaseBalloon.IsLeftHand.
        /// </summary>
        private bool IsLeftHandHitbox(Transform hitbox)
        {
            // Check for our custom component first
            var forwarder = hitbox.GetComponent<PopstrikeVR.Interaction.HandColliderForwarder>();
            if (forwarder != null && forwarder.VelocityProvider != null)
            {
                return forwarder.VelocityProvider.isLeftHand;
            }

            // Check parent MetaHandIntegrator
            var integrator = hitbox.GetComponentInParent<PopstrikeVR.Interaction.MetaHandIntegrator>();
            if (integrator != null)
            {
                return integrator.isLeftHand;
            }

            // Fallback to name parsing
            string nameLower = hitbox.gameObject.name.ToLower();
            Transform parent = hitbox.parent;
            string fullPath = parent != null ? parent.gameObject.name.ToLower() : "";
            
            return nameLower.Contains("left") || nameLower.Contains("_l_") 
                || fullPath.Contains("left") || fullPath.Contains("_l_");
        }
    }
}
