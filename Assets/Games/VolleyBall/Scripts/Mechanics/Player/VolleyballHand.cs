using UnityEngine;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.Mechanics
{
    /// <summary>
    /// Attached to tracked hand objects (e.g., hand bones, palms, or custom hand-tracking prefabs).
    /// Tracks hand movement velocity and detects collision with the volleyball to trigger a strike.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VolleyballHand : MonoBehaviour
    {
        public enum HandSide { Left, Right }

        [Header("Hand Settings")]
        [SerializeField] private HandSide side;
        
        [Tooltip("Minimum speed required to count as a hit (prevents accidental bumps).")]
        [SerializeField] private float minimumStrikeSpeed = 0.5f;

        [Header("Grab & Serve Mechanics")]
        [Tooltip("Reference to the OVRHand component to detect fist gesture.")]
        [SerializeField] private OVRHand ovrHand;
        
        [Tooltip("Reference to the OVRSkeleton component for bone-based fist detection.")]
        [SerializeField] private OVRSkeleton ovrSkeleton;
        
        [Tooltip("Offset from the hand when holding the ball.")]
        [SerializeField] private Vector3 grabOffset = new Vector3(0, 0.15f, 0);
        
        [Tooltip("How many fingers (out of 4: Index, Middle, Ring, Pinky) must be curled to count as a fist. Default: 4")]
        [Range(1, 4)]
        [SerializeField] private int fingersRequiredForFist = 4;
        
        [Tooltip("Bone curl angle threshold (degrees). A finger is considered curled if its middle bone bends beyond this. Default: 80 (very strict fist)")]
        [SerializeField] private float fingerCurlThreshold = 80f;

        [Header("Debug")]
        [Tooltip("Enable to print hand collision and strike data to the Unity Console.")]
        [SerializeField] private bool showDebugLogs = true;

        private Vector3 lastPosition;
        private Vector3 currentVelocity;
        
        // Grab state
        private VolleyballBall grabbedBall;
        private bool isFistClosed = false;

        public HandSide Side => side;

        private void Start()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            // Calculate real-world frame-by-frame velocity universally (unaffected by bullet time)
            if (Time.unscaledDeltaTime > 0f)
            {
                currentVelocity = (transform.position - lastPosition) / Time.unscaledDeltaTime;
            }
            lastPosition = transform.position;

            // [GRAB LOGIC COMMENTED OUT FOR NOW - SERVE USES OFFSET INSTEAD]
            // HandleGrabLogic();
        }

        private void HandleGrabLogic()
        {
            bool currentFistState = DetectFist();

            if (currentFistState && !isFistClosed)
            {
                if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] Fist Closed! Attempting to grab...");
                TryGrabBall();
            }
            else if (!currentFistState && isFistClosed)
            {
                if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] Fist Opened! Releasing ball...");
                ReleaseBall();
            }

            isFistClosed = currentFistState;

            // If we are holding the ball, stick it to the hand with offset
            if (grabbedBall != null)
            {
                grabbedBall.transform.position = transform.position + transform.TransformDirection(grabOffset);
            }
        }

        /// <summary>
        /// Detects a closed fist using OVRSkeleton bone angles.
        /// Strictly requires the fingers to be curled past the fingerCurlThreshold (80+ degrees).
        /// </summary>
        private bool DetectFist()
        {
            if (ovrSkeleton != null && ovrSkeleton.IsInitialized && ovrSkeleton.Bones != null && ovrSkeleton.Bones.Count > 0)
            {
                // Finger middle-joint bone IDs for Index, Middle, Ring, Pinky
                // These are the "proximal" bones that show the most rotation when curling
                var fingerMiddleJoints = new[]
                {
                    OVRSkeleton.BoneId.Hand_Index2,   // Index middle
                    OVRSkeleton.BoneId.Hand_Middle2,  // Middle middle
                    OVRSkeleton.BoneId.Hand_Ring2,    // Ring middle
                    OVRSkeleton.BoneId.Hand_Pinky2    // Pinky middle
                };

                int curledCount = 0;
                foreach (var boneId in fingerMiddleJoints)
                {
                    OVRBone bone = FindBone(ovrSkeleton, boneId);
                    if (bone != null)
                    {
                        // Get the local x-rotation of this joint — this is the curl axis
                        float curl = Mathf.Abs(bone.Transform.localEulerAngles.x);
                        // Normalize 270-360 to negative (so wrap-around angles work correctly)
                        if (curl > 180f) curl = 360f - curl;
                        
                        if (curl > fingerCurlThreshold)
                            curledCount++;
                        
                        if (showDebugLogs && Time.frameCount % 60 == 0)
                            Debug.Log($"[VolleyballHand - {side}] Bone {boneId} curl: {curl:F1}°");
                    }
                }

                if (showDebugLogs && Time.frameCount % 60 == 0)
                    Debug.Log($"[VolleyballHand - {side}] Curled fingers: {curledCount}/{fingerMiddleJoints.Length} (need {fingersRequiredForFist})");

                return curledCount >= fingersRequiredForFist;
            }

            return false; // No tracking available
        }

        private OVRBone FindBone(OVRSkeleton skeleton, OVRSkeleton.BoneId boneId)
        {
            foreach (var bone in skeleton.Bones)
                if (bone.Id == boneId) return bone;
            return null;
        }


        private void TryGrabBall()
        {
            if (grabbedBall != null) return;

            // Jedi Force Grab: Search the entire scene for the volleyball
            VolleyballBall[] balls = FindObjectsByType<VolleyballBall>(FindObjectsSortMode.None);
            foreach (var ball in balls)
            {
                // Only grab it if it's currently inactive (waiting to be served)
                if (!ball.IsBallActive) 
                {
                    grabbedBall = ball;
                    if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] Force-grabbed ball from anywhere!");
                    break;
                }
            }
        }

        private void ReleaseBall()
        {
            if (grabbedBall != null)
            {
                if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] Released ball. It is now floating static.");
                grabbedBall = null;
            }
        }

        // STATIC variable ensures that if one hand hits the ball, the OTHER hand is also blocked
        // from instantly double-hitting it if the player claps the ball or keeps both hands close.
        private static float globalLastHitTime = 0f;

        private void OnTriggerEnter(Collider other)
        {
            TryProcessHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryProcessHit(other);
        }

        private void TryProcessHit(Collider other)
        {
            // Prevent rapid-fire hits from ANY hand lingering in the collider
            if (Time.time - globalLastHitTime < 0.5f) return;

            // Check if we hit the volleyball
            if (other.TryGetComponent(out VolleyballBall ball))
            {
                // DO NOT strike the ball if we are actively grabbing it!
                // if (grabbedBall == ball) return; // Commented out alongside grab logic
                
                float handSpeed = currentVelocity.magnitude;
                float incomingBallSpeed = ball.GetComponent<Rigidbody>().linearVelocity.magnitude;
                
                // For serves, enforce a gentle minimum hand speed so the patient doesn't struggle to serve
                float requiredServeSpeed = Mathf.Max(minimumStrikeSpeed, 1.0f);
                bool isServe = incomingBallSpeed < 1.0f;
                
                if (isServe && handSpeed < requiredServeSpeed)
                {
                    if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] Hand speed too low for SERVE ({handSpeed:F2} < {requiredServeSpeed}). Ignoring hit.");
                    return;
                }

                // First: Ask GameManager if a strike is even allowed right now.
                // We do this AFTER the speed check so we don't accidentally start a rally on a failed weak tap!
                if (VolleyballGameManager.Instance == null) return;
                if (!VolleyballGameManager.Instance.TryPlayerStrike())
                {
                    // Only log rejected strikes occasionally to prevent spam in OnTriggerStay
                    if (showDebugLogs && Time.frameCount % 60 == 0) Debug.Log($"[VolleyballHand - {side}] Strike rejected — game not in a hittable state.");
                    return;
                }
                
                globalLastHitTime = Time.time;
                if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] Collided with Volleyball! Hand Speed: {handSpeed:F2}");
                
                float activeMultiplier = isServe ? ball.PlayerServeMultiplier : ball.PlayerBlockMultiplier;
                
                // Effective speed incorporates inspector multipliers!
                float effectiveSpeed = (handSpeed * activeMultiplier) + (incomingBallSpeed * ball.BlockRestitution);
                
                // Get the horizontal direction the player's hand is swinging
                Vector3 horizontalHandDir = new Vector3(currentVelocity.x, 0, currentVelocity.z).normalized;
                
                // Safety: Ensure the ball is always hit generally forward towards the net
                if (horizontalHandDir.z < 0.2f) 
                {
                    horizontalHandDir.z = 0.2f;
                    horizontalHandDir.Normalize();
                }

                // ── FEATURE 3: SMART AIM ASSIST (NEAR RING) ──
                bool nearRing = false;
                VolleyballLandingVisualizer vis = FindFirstObjectByType<VolleyballLandingVisualizer>();
                if (vis != null && vis.IsShowingRing)
                {
                    // Check distance in 3D to see if hand is near the predicted landing spot (the ring)
                    float distToRing = Vector3.Distance(transform.position, vis.PredictedLandingSpot);
                    nearRing = distToRing <= 0.6f; // 0.6m radius (generous enough for VR rehab, but requires intentional positioning)
                    if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] Ring distance: {distToRing:F2}m (Near Ring: {nearRing})");
                }

                // 1. Raw Player Aim (1:1 Physics)
                // Determine how deep into the court the ball will land based on EFFECTIVE physical speed.
                // We lower the minimum clamp to 1.5f to allow weak hits to fail to cross the net naturally.
                float hitDistance = Mathf.Clamp(effectiveSpeed * 1.0f, 1.5f, 18.0f);
                Vector3 playerAimTarget = ball.transform.position + horizontalHandDir * hitDistance;
                playerAimTarget.y = 0; // Ground level

                // NEW: Guarantee the ball crosses the net if it's a serve, OR if it's a hard rally hit!
                // This ensures that even a gentle 2m/s serve successfully starts the rally.
                bool shouldForceClearNet = isServe || effectiveSpeed >= 4.0f;
                if (shouldForceClearNet && VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.NetTransform != null)
                {
                    float minZ = VolleyballGameManager.Instance.NetTransform.position.z + 1.0f;
                    if (playerAimTarget.z < minZ)
                    {
                        playerAimTarget.z = minZ; // Push it deep enough to guarantee it enters the AI court
                    }
                }
                
                // If it's a serve, we also enforce a minimum hit distance so it doesn't just drop right behind the net.
                if (isServe)
                {
                    float distToTarget = Vector3.Distance(ball.transform.position, playerAimTarget);
                    if (distToTarget < 6.0f)
                    {
                        playerAimTarget = ball.transform.position + horizontalHandDir * 6.0f;
                    }
                }
                
                Vector3 targetPos = playerAimTarget;

                if (nearRing)
                {
                    // 2. Magnetic Assist (Only if near ring)
                    Vector3 aiTargetPos = VolleyballGameManager.Instance.GetOpponentTargetPosition();
                    targetPos = Vector3.Lerp(playerAimTarget, aiTargetPos, ball.AssistWeight);
                }
                else
                {
                    if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] Missed Ring! Using raw 1:1 physics with no magnetic assist.");
                }

                // (Inner-court clamp moved to the end of the method after smash logic)

                // --- FLIGHT TIME (ARC) LOGIC ---
                float g = ball.CustomGravity;
                if (g < 0.1f) g = 0.1f;
                
                float verticalAngle = currentVelocity.normalized.y;
                float distance = Vector3.Distance(ball.transform.position, targetPos);
                
                // Restore the original pronounced lob so the ball safely and cleanly crosses the net every time!
                float desiredApexHeight = Mathf.Max(2.0f, distance / 3.0f);
                
                // If this is a serve, add a graceful, high lob so the player has time to recover!
                if (isServe) {
                    desiredApexHeight += 1.0f;
                }
                
                // If they swing upwards (uppercut), give it even MORE height!
                if (verticalAngle > 0.2f) {
                    desiredApexHeight += Mathf.Lerp(0f, 2.0f, (verticalAngle - 0.2f) / 0.8f);
                }
                // If they swing downwards (spike), flatten the arc for a fast shot
                else if (verticalAngle < -0.1f) {
                    // CRITICAL FIX: If they spike a serve, don't let it become a 0.2m flat laser beam! 
                    // Give it at least 1.5m of height so the dog has enough time to run and catch it.
                    float minSpikeHeight = isServe ? 1.5f : 0.2f; 
                    desiredApexHeight = Mathf.Lerp(desiredApexHeight, minSpikeHeight, -verticalAngle);
                }
                
                // NEW: Escalating Rally Difficulty (Faster & Lower over time)
                if (VolleyballGameManager.Instance != null && !isServe)
                {
                    int rallyHits = VolleyballGameManager.Instance.CurrentRallyCount;
                    if (rallyHits > 2)
                    {
                        // Every hit after the 2nd reduces the apex height
                        // A lower apex naturally results in a much faster, flatter shot!
                        float perHit = VolleyballGameManager.Instance.heightReductionPerHit;
                        float max = VolleyballGameManager.Instance.maxHeightReduction;
                        float heightReduction = Mathf.Min((rallyHits - 2) * perHit, max);
                        desiredApexHeight = Mathf.Max(0.5f, desiredApexHeight - heightReduction);
                    }
                }
                
                float heightFromFloor = ball.transform.position.y;
                float totalApexFromFloor = heightFromFloor + desiredApexHeight;
                
                float timeUp = Mathf.Sqrt(2f * desiredApexHeight / g);
                float timeDown = Mathf.Sqrt(2f * totalApexFromFloor / g);
                float flightTime = timeUp + timeDown;
                
                float? chosenGravity = null;

                // ── PLAYER FAST SMASH ──
                // If the player hits with great speed and the cooldown is ready, smash the ball deep!
                bool canSmash = VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.HitsSinceLastSmash >= VolleyballGameManager.Instance.playerSmashCooldownHits;
                float speedThreshold = VolleyballGameManager.Instance != null ? VolleyballGameManager.Instance.playerSmashSpeedThreshold : 8.0f;

                if (effectiveSpeed > speedThreshold && !isServe && canSmash)
                {
                    if (VolleyballGameManager.Instance != null) VolleyballGameManager.Instance.HitsSinceLastSmash = 0; // Reset cooldown

                    // 1. AIM DEEP! Override the magnetic assist so the smash actually goes to the back of the court.
                    if (VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.aiCourtBounds != null)
                    {
                        Bounds bounds = VolleyballGameManager.Instance.aiCourtBounds.bounds;
                        // Push Z to 85% deep into the AI court!
                        targetPos.z = Mathf.Lerp(bounds.center.z, bounds.max.z, 0.85f);
                        distance = Vector3.Distance(ball.transform.position, targetPos);
                    }

                    // 2. PRO SMASH TRAJECTORY! 
                    // Let the ball fly with natural gravity, but simply use a fast flight time to make it a flat laser!
                    flightTime = distance / effectiveSpeed;
                    flightTime = Mathf.Clamp(flightTime, 0.35f, 0.85f); // Super fast to fast
                    
                    // We no longer calculate an artificially high gravity. It will use the balloon's natural customGravity.
                    
                    if (showDebugLogs) Debug.Log($"[VolleyballHand - {side}] PLAYER PRO SMASH! Speed={effectiveSpeed:F1}, FlightTime={flightTime:F2}s, Gravity=Natural");
                    // NOTE: net clearance loop below will increase flightTime further if the arc clips the net.
                }
                else if (!isServe && VolleyballGameManager.Instance != null)
                {
                    VolleyballGameManager.Instance.HitsSinceLastSmash++;
                }
                
                if (showDebugLogs && chosenGravity == null) Debug.Log($"[VolleyballHand - {side}] Hit! Dist: {hitDistance:F1}m | Apex: {desiredApexHeight:F1}m | Flight Time: {flightTime:F2}s");

                // 3. Guaranteed Inner-Court Clamp (If ball has enough force/direction to cross the net)
                // We do this at the very end to guarantee safety even if smash logic altered the target!
                float netZ = 5.0f;
                if (VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.NetTransform != null)
                {
                    netZ = VolleyballGameManager.Instance.NetTransform.position.z;
                }
                if (targetPos.z > netZ)
                {
                    if (VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.aiCourtBounds != null)
                    {
                        Bounds bounds = VolleyballGameManager.Instance.aiCourtBounds.bounds;
                        float paddingX = bounds.size.x * 0.20f;
                        float paddingZ = bounds.size.z * 0.20f;
                        targetPos.x = Mathf.Clamp(targetPos.x, bounds.min.x + paddingX, bounds.max.x - paddingX);
                        targetPos.z = Mathf.Clamp(targetPos.z, bounds.min.z + paddingZ, bounds.max.z - paddingZ);
                    }
                }
                else
                {
                    if (VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.playerCourtBounds != null)
                    {
                        Bounds bounds = VolleyballGameManager.Instance.playerCourtBounds.bounds;
                        targetPos.x = Mathf.Clamp(targetPos.x, bounds.min.x, bounds.max.x);
                        targetPos.z = Mathf.Clamp(targetPos.z, bounds.min.z, bounds.max.z);
                    }
                }

                // --- GUARANTEE NET CLEARANCE (0.2m above net top) ---
                // This runs for ALL hits. If the current trajectory clips or goes under the net,
                // we incrementally increase flight time so the ball arcs safely over by 0.2m.
                // This is the final safety net — it catches both smashes and normal hits that get
                // too flat due to rally escalation.
                if (VolleyballGameManager.Instance != null)
                {
                    float clearGravity = chosenGravity ?? ball.CustomGravity;
                    for (int i = 0; i < 30; i++)
                    {
                        if (VolleyballGameManager.Instance.WillClearNet(ball.transform.position, targetPos, flightTime, clearGravity)) break;
                        flightTime += 0.04f; // Nudge flight time up so the ball arcs higher over the net
                    }
                }

                // Deflect the ball using precise drag physics calculations, blended with actual hand direction!
                ball.LaunchToTarget(targetPos, flightTime, BallHitter.Player, currentVelocity, ball.AssistWeight, 0f, chosenGravity);
            }
        }
    }
}
