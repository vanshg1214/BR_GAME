using UnityEngine;
using Rehab.Volleyball.Data;
using Rehab.Volleyball.Core;
using System.Collections;

namespace Rehab.Volleyball.Mechanics
{
    /// <summary>
    /// Controls the AI opponent on the other side of the net.
    /// Uses data from the active OpponentProfile (loaded from CSV) to determine movement speed and reaction times.
    /// </summary>
    public class VolleyballOpponent : MonoBehaviour
    {
        // ─── Animation Sync ──────────────────────────────────
        [Header("Animation Sync")]
        [Tooltip("The HitIndex for the Kick animation. When this animation is chosen, the spatial alignment offset is applied.")]
        [SerializeField] private int kickHitIndex = 1;
        
        [Tooltip("The HitIndex values for head-hit animations (0=Dog Success, 3=Dog Damage). Used when the dog is near the net.")]
        [SerializeField] private int[] headHitIndices = new int[] { 0, 3 };
        
        [Tooltip("When the dog is closer than this distance (in Z) to the net, it will ONLY use head-hit animations instead of kicks.")]
        [SerializeField] private float nearNetThreshold = 2.0f;

        [Header("References")]
        [SerializeField] private VolleyballBall targetBall;
        
        [Header("Serving Limbs & Offsets")]
        [Tooltip("The Transform of the Dog's left hand. The ball will snap here for a Throw serve, or halfway between both hands if the right hand is also assigned.")]
        [SerializeField] private Transform servingHand; 
        
        [Tooltip("The Transform of the Dog's right hand. If assigned, the ball will snap exactly halfway between the two hands during a Throw serve.")]
        [SerializeField] private Transform servingHandRight; 
        
        [Tooltip("Adjust exactly where the ball sits in the hands so it doesn't clip.")]
        [SerializeField] private Vector3 serveHandOffset = Vector3.zero;

        [Tooltip("The Transform of the Dog's foot. The ball will snap here for a Kick serve.")]
        [SerializeField] private Transform kickingFoot;
        [Tooltip("Adjust exactly where the ball sits on the foot so it doesn't clip into the ankle.")]
        [SerializeField] private Vector3 serveFootOffset = Vector3.zero; 
        
        [Header("Serve Release Timing")]
        [Tooltip("Seconds to wait before launching the ball during a Kick serve.")]
        [SerializeField] private float kickServeReleaseDelay = 0.4f;
        [Tooltip("Seconds to wait before launching the ball during a Throw serve. Tune this so the ball leaves the hands at the very peak (e.g. 80%) of the animation!")]
        [SerializeField] private float throwServeReleaseDelay = 1.0f;

        // Audio is now centrally handled by VolleyballEffectsManager!

        [Header("Out of Bounds Behavior")]
        [SerializeField] private float outOfBoundsAttemptDistance = 0.7f;
        [SerializeField] private float outOfBoundsAttemptSpeed = 2.0f;

        [Header("Spatial Alignment")]
        [Tooltip("The height where a normal volley makes contact with the ball (e.g. mouth/chest height).")]
        [SerializeField] private float normalContactHeight = 1.2f;
        [Tooltip("The height where a kick makes contact. Lower this to let the ball fall to the toe!")]
        [SerializeField] private float kickContactHeight = 0.2f;
        [Tooltip("Offsets the AI's target position backwards (away from the net) in meters.")]
        [SerializeField] private float hitPositionOffsetZ = 0.0f;

        // ─── Internal AI State ───────────────────────────────
        private OpponentProfile currentProfile;
        public OpponentProfile CurrentProfile => currentProfile;
        private bool isTracking = false;
        private Vector3 startPosition;
        private bool isAttempting = false;
        private Vector3 attemptTarget;
        private float attemptDistanceCovered = 0f;
        private float hitRecoveryTimer = 0f;
        
        // Animation Sync State
        private bool hasFiredHitAnimation = false;
        private bool hasPhysicallyHitBall = false;
        private int nextHitIndex = 0;
        private VolleyballOpponentAnimator dogAnimator;

        private float GetTargetContactHeight()
        {
            return (nextHitIndex == kickHitIndex) ? kickContactHeight : normalContactHeight;
        }

        public bool IsMoving { get; private set; }
        public float CurrentMoveSpeed { get; private set; }
        
        /// <summary>
        /// Returns true if the given Z coordinate is within nearNetThreshold distance of the net.
        /// Used to force head-hit animations instead of kicks when the ball will land close to the net.
        /// </summary>
        private bool IsZNearNet(float zPos)
        {
            if (VolleyballGameManager.Instance == null || VolleyballGameManager.Instance.NetTransform == null) 
                return false;
            float netZ = VolleyballGameManager.Instance.NetTransform.position.z;
            float distToNet = Mathf.Abs(zPos - netZ);
            return distToNet < nearNetThreshold;
        }
        
        // ─── Lifecycle ───────────────────────────────────────
        private void Awake()
        {
            dogAnimator = GetComponentInChildren<VolleyballOpponentAnimator>();
        }

        private void Start()
        {
            startPosition = transform.position;
        }

        public void SetProfile(OpponentProfile newProfile)
        {
            currentProfile = newProfile;
        }

        private void Update()
        {
            if (targetBall == null || currentProfile == null) return;

            if (VolleyballGameManager.Instance != null && !VolleyballGameManager.Instance.IsRallyActive)
            {
                isTracking = false;
                isAttempting = false;
            }

            if (hitRecoveryTimer > 0f)
            {
                hitRecoveryTimer -= Time.deltaTime;
                IsMoving = false;
                CurrentMoveSpeed = 0f;
                Quaternion targetRot = Quaternion.LookRotation(Vector3.back);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
                return;
            }

            if (isAttempting)
            {
                float stepSize = outOfBoundsAttemptSpeed * Time.deltaTime;
                
                Vector3 moveDir = (attemptTarget - transform.position).normalized;
                moveDir.y = 0;
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);
                }
                
                transform.position = Vector3.MoveTowards(transform.position, attemptTarget, stepSize);
                attemptDistanceCovered += stepSize;
                IsMoving = true;
                CurrentMoveSpeed = outOfBoundsAttemptSpeed;
                
                if (attemptDistanceCovered >= outOfBoundsAttemptDistance || 
                    Vector3.Distance(transform.position, attemptTarget) < 0.05f)
                {
                    isAttempting = false;
                }
                return;
            }

            if (!isTracking)
            {
                // A massive safe zone (3.0 meters). The dog will only walk back if it is at the extreme corners of the court!
                float innerCourtRadius = 3.0f; 
                float distToBase = Vector3.Distance(transform.position, startPosition);
                IsMoving = distToBase > innerCourtRadius;
                
                if (IsMoving)
                {
                    float returnStep = (VolleyballGameManager.Instance.CurrentDifficulty.moveSpeed * 0.8f) * Time.deltaTime;
                    CurrentMoveSpeed = VolleyballGameManager.Instance.CurrentDifficulty.moveSpeed * 0.8f;
                    
                    Vector3 moveDir = (startPosition - transform.position).normalized;
                    moveDir.y = 0;
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(moveDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);
                    }
                    
                    // Only move towards start position until we enter the inner court
                    transform.position = Vector3.MoveTowards(transform.position, startPosition, returnStep);
                }
                else
                {
                    CurrentMoveSpeed = 0f;
                    Quaternion targetRot = Quaternion.LookRotation(Vector3.back); // Face the net
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
                }
                
                return;
            }

            float timeToLand = 0f;
            Vector3 predictedLanding = GetPredictedLandingSpot(out timeToLand);

            if (VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.aiCourtBounds != null)
            {
                Bounds bounds = VolleyballGameManager.Instance.aiCourtBounds.bounds;
                Bounds slightlyExpandedBounds = bounds;
                slightlyExpandedBounds.Expand(0.3f);

                if (predictedLanding.x < slightlyExpandedBounds.min.x || predictedLanding.x > slightlyExpandedBounds.max.x ||
                    predictedLanding.z < slightlyExpandedBounds.min.z || predictedLanding.z > slightlyExpandedBounds.max.z)
                {
                    isTracking = false;
                    Vector3 towardBall = (predictedLanding - transform.position).normalized;
                    attemptTarget = transform.position + towardBall * outOfBoundsAttemptDistance;
                    attemptDistanceCovered = 0f;
                    isAttempting = true;
                    return;
                }
                
                predictedLanding.x = Mathf.Clamp(predictedLanding.x, bounds.min.x, bounds.max.x);
                predictedLanding.z = Mathf.Clamp(predictedLanding.z, bounds.min.z, bounds.max.z);
            }

            float distToTarget = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                new Vector3(predictedLanding.x, 0, predictedLanding.z));
            
            float currentSpeed = VolleyballGameManager.Instance.CurrentDifficulty.moveSpeed;
            if (targetBall.Rb != null)
            {
                if (timeToLand > 0.1f)
                {
                    float requiredSpeed = distToTarget / timeToLand;
                    // Allow the dog to move as fast as necessary to reach the ball (up to a very high cap) to prevent it from missing and hitting the air!
                    currentSpeed = Mathf.Clamp(requiredSpeed * 1.05f, VolleyballGameManager.Instance.CurrentDifficulty.moveSpeed, VolleyballGameManager.Instance.CurrentDifficulty.moveSpeed * 4.0f);
                }
                
                // --- PREDICTIVE ANIMATION TRIGGER ---
                // Fire the animation early based on the animator's lead time
                float leadTime = dogAnimator != null ? dogAnimator.HitAnimationLeadTime : 0.35f;
                if (timeToLand <= leadTime && !hasFiredHitAnimation && distToTarget < 1.0f)
                {
                    // LAST-SECOND CHECK: Now we KNOW where the dog actually is.
                    // If the dog ended up near the net, force a head-hit regardless of the early prediction!
                    if (IsZNearNet(transform.position.z) && nextHitIndex == kickHitIndex && headHitIndices.Length > 0)
                    {
                        nextHitIndex = headHitIndices[Random.Range(0, headHitIndices.Length)];
                        Debug.Log($"[VolleyballOpponent] Near-net override! Switched from kick to head-hit index {nextHitIndex}");
                    }
                    
                    hasFiredHitAnimation = true;
                    dogAnimator?.FirePreselectedHitAnimation(nextHitIndex);
                }
            }
            
            IsMoving = distToTarget > 0.05f;
            
            // Hit Freeze: If we are winding up the hit animation, significantly slow down to prevent sliding while kicking!
            if (hasFiredHitAnimation && timeToLand < 0.2f)
            {
                currentSpeed *= 0.1f; 
            }
            
            CurrentMoveSpeed = IsMoving ? currentSpeed : 0f;
            
            // Rotation Logic
            if (IsMoving && currentSpeed > 0.1f && !hasFiredHitAnimation)
            {
                Vector3 moveDir = (predictedLanding - transform.position).normalized;
                moveDir.y = 0;
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);
                }
            }
            else
            {
                // When stationary or hitting, face backwards towards the player/net (since AI is at positive Z)
                Quaternion targetRot = Quaternion.LookRotation(Vector3.back);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }
            
            transform.position = Vector3.MoveTowards(transform.position, predictedLanding, currentSpeed * Time.deltaTime);
        }

        private Vector3 GetPredictedLandingSpot(out float timeToLand)
        {
            timeToLand = 0f;
            Vector3 pos = targetBall.transform.position;
            if (targetBall.Rb == null) return new Vector3(pos.x, transform.position.y, pos.z);
            
            Vector3 vel = targetBall.Rb.linearVelocity;
            if (targetBall.CurrentBallTimeScale > 0.01f) 
                vel /= targetBall.CurrentBallTimeScale;
                
            float contactY = GetTargetContactHeight();
            float targetY = transform.position.y + contactY;

            float g = targetBall.CustomGravity;
            float k = targetBall.AirResistance;
            
            float dt = 0.02f; // Simulate at 50fps
            float maxTime = 5.0f; // Failsafe
            
            // PHASE 1: If the ball is still rising (e.g. a serve), simulate until it peaks
            // This is critical — without this, serves caused the dog to run to the ball's CURRENT position!
            while (vel.y > 0 && timeToLand < maxTime)
            {
                Vector3 accel = (Vector3.down * g) - (vel * k);
                vel += accel * dt;
                pos += vel * dt;
                timeToLand += dt;
            }
            
            // PHASE 2: Now the ball is falling — simulate until it drops to the contact height
            while (pos.y > targetY && timeToLand < maxTime)
            {
                Vector3 accel = (Vector3.down * g) - (vel * k);
                vel += accel * dt;
                pos += vel * dt;
                timeToLand += dt;
            }
            
            float px = pos.x;
            float pz = pos.z;
            
            // Removed artificial Z-offset so the dog stands perfectly centered on the ball's landing spot, just like before!
            
            return new Vector3(px, transform.position.y, pz);
        }

        public void OnPlayerHitBall(bool isServe = false)
        {
            StartCoroutine(ReactionDelayRoutine(isServe));
        }

        private IEnumerator ReactionDelayRoutine(bool isServe)
        {
            isTracking = false;
            hasFiredHitAnimation = false; // Reset animation trigger for this rally
            hasPhysicallyHitBall = false; // Reset physics trigger for this rally
            
            // PRE-SELECT NEXT VOLLEY ANIMATION so we can position accordingly
            if (dogAnimator != null)
            {
                // To know if we should use a head-hit or a kick, we need to know WHERE the ball will land.
                // We run a quick physics prediction using a temporary default head-hit index.
                int tempIndex = nextHitIndex;
                nextHitIndex = headHitIndices.Length > 0 ? headHitIndices[0] : 0; 
                
                Vector3 roughLanding = GetPredictedLandingSpot(out _);
                bool isLandingNearNet = IsZNearNet(roughLanding.z);
                
                // Now that we know where the ball will land, make the final animation choice!
                if (isLandingNearNet && headHitIndices.Length > 0)
                {
                    nextHitIndex = headHitIndices[Random.Range(0, headHitIndices.Length)];
                }
                else
                {
                    nextHitIndex = dogAnimator.ChooseNextVolleyIndex();
                }
            }
            
            // If it's a serve, the dog is already anticipating it, so reaction delay is instantly 0!
            float delay = (currentProfile != null && !isServe) ? VolleyballGameManager.Instance.CurrentDifficulty.reactionDelay : 0.0f;
            
            if (delay > 0f) yield return new WaitForSeconds(delay);
            
            isTracking = true;
        }

        // --- NEW SERVE HAND-OFF METHODS ---
        private Coroutine holdServeCoroutine;

        public void PrepareServe(VolleyballBall ball)
        {
            // Pick the serve animation now so we know where to put the ball!
            if (dogAnimator != null) nextHitIndex = dogAnimator.ChooseNextServeIndex();

            Transform primarySnapTarget = (nextHitIndex == kickHitIndex) ? kickingFoot : servingHand;
            Vector3 offset = (nextHitIndex == kickHitIndex) ? serveFootOffset : serveHandOffset;

            if (primarySnapTarget != null && ball.Rb != null)
            {
                ball.Rb.isKinematic = true;
                ball.transform.parent = null; // Unparent because we might dynamically update its position between two hands!
                
                if (holdServeCoroutine != null) StopCoroutine(holdServeCoroutine);
                holdServeCoroutine = StartCoroutine(HoldServeRoutine(ball, primarySnapTarget, offset, nextHitIndex != kickHitIndex));
            }
            else
            {
                // Fallback if no transforms assigned
                ball.transform.position = transform.position + Vector3.up * 1.5f + Vector3.forward * 0.5f;
            }
        }
        
        private IEnumerator HoldServeRoutine(VolleyballBall ball, Transform primarySnapTarget, Vector3 offset, bool isThrow)
        {
            while (ball != null && ball.Rb != null && ball.Rb.isKinematic)
            {
                if (isThrow && servingHand != null && servingHandRight != null)
                {
                    // Snap exactly between both hands!
                    Vector3 midpoint = Vector3.Lerp(servingHand.position, servingHandRight.position, 0.5f);
                    // Use the dog's rotation as the forward direction for the offset so it pushes out from the chest properly
                    ball.transform.position = midpoint + transform.TransformDirection(offset);
                }
                else
                {
                    // Snap to the single limb (kick or one-handed throw)
                    ball.transform.position = primarySnapTarget.position + primarySnapTarget.TransformDirection(offset);
                }
                yield return null; // Update every frame to follow the animation
            }
        }

        public void PlayServeAnimation()
        {
            if (dogAnimator != null)
            {
                // Play the animation we already selected in PrepareServe
                dogAnimator.FirePreselectedHitAnimation(nextHitIndex);
            }
        }

        public void ExecuteServe(VolleyballBall ball)
        {
            if (holdServeCoroutine != null) StopCoroutine(holdServeCoroutine);
            
            if (ball.Rb != null)
            {
                ball.transform.parent = null;
                ball.Rb.isKinematic = false;
            }
        }

        public float GetServeReleaseDelay()
        {
            return (nextHitIndex == kickHitIndex) ? kickServeReleaseDelay : throwServeReleaseDelay;
        }

        // --- PHYSICS LAUNCH (Decoupled from animation) ---
        private void OnTriggerStay(Collider other)
        {
            if (hasPhysicallyHitBall) return;
            
            if (other.TryGetComponent(out VolleyballBall ball))
            {
                if (currentProfile == null) return;
                
                // If game is not active (e.g. we are serving), ignore trigger
                if (VolleyballGameManager.Instance != null && !VolleyballGameManager.Instance.IsRallyActive) return;

                // 1. Calculate the exact visual contact point on the dog's body
                float contactY = GetTargetContactHeight();
                Vector3 contactPoint = transform.position;
                
                // 2. Wait until the ball drops to the correct vertical height
                float heightAboveGround = ball.transform.position.y - transform.position.y;
                if (heightAboveGround > contactY + 0.25f) return;
                
                // 3. Wait until the ball is horizontally OVERLAPPING the dog's body!
                Vector3 ballXZ = new Vector3(ball.transform.position.x, 0, ball.transform.position.z);
                Vector3 contactXZ = new Vector3(contactPoint.x, 0, contactPoint.z);
                float horizontalDist = Vector3.Distance(ballXZ, contactXZ);
                
                // If the dog is desperately diving, allow a slightly larger reach. Otherwise require strict physical contact.
                float maxAllowedReach = isAttempting ? 1.2f : 0.45f;
                
                // A kick visually sweeps a much larger area (the leg extends forward), so we give it extra physical tolerance!
                if (nextHitIndex == kickHitIndex) maxAllowedReach += 0.4f;
                
                if (horizontalDist > maxAllowedReach) return;

                hasPhysicallyHitBall = true;
                isTracking = false;
                isAttempting = false; // Prevent it from trying to save its own hit!
                hitRecoveryTimer = 0.5f; // Stand still for 0.5s to finish the hit animation cleanly
                
                // 3) FORCE/SPEED (Reads from global difficulty settings)
                float aiForceFactor = VolleyballGameManager.Instance.CurrentDifficulty.forceFactor;

                // SPECIAL RULE: Limit the power of the dog's first serve in a rally so the patient isn't overwhelmed!
                if (VolleyballGameManager.Instance.CurrentRallyCount < 4)
                {
                    aiForceFactor = Mathf.Min(aiForceFactor, 0.8f); // Force a gentle return for the first few hits
                }

                // AI "Accuracy" chance to flub the shot
                bool hitCleanly = Random.value <= VolleyballGameManager.Instance.CurrentDifficulty.hitAccuracy;

                // Failsafe: if the ball came too fast and prediction failed to fire early, fire now
                if (!hasFiredHitAnimation)
                {
                    hasFiredHitAnimation = true;
                    dogAnimator?.FirePreselectedHitAnimation(nextHitIndex);
                }

                // --- RALLY BREAKER ---
                // Limit rallies to a natural competitive length (16-24 hits)
                int currentRally = VolleyballGameManager.Instance != null ? VolleyballGameManager.Instance.CurrentRallyCount : 0;
                float forcedMissChance = 0f;
                if (currentRally >= 16)
                {
                    // Starts at a gentle 12.5% chance at hit 16, increasing by 12.5% every hit.
                    // This creates a natural unpredictable breaking range between 16 and 24 hits (guaranteed fail at 24).
                    forcedMissChance = Mathf.Min(1.0f, (currentRally - 15) * 0.125f); 
                }

                float actualAccuracy = 1.0f; // AI is natively 100% accurate until forced to fail
                bool isCleanHit = Random.value <= actualAccuracy && Random.value > forcedMissChance;
                
                if (isCleanHit)
                {
                    // Regular hit targeting the patient
                    Vector3 playerTargetZone = VolleyballGameManager.Instance.GetPlayerTargetPosition();
                    
                    // In rehab, we NEVER want the ball to fly out of bounds (out of ROM).
                    // We removed the AI "miss" noise so it always targets the safe zone.
                    
                    float flightTime = 2.2f;

                    // ── FAST LOW SHOT ──
                    bool isFastShot = false;
                    float? chosenGravity = null;
                    // Only allow fast shots if the rally is well underway (>= 4 hits).
                    // This prevents the dog from jump-scaring the player with a 100mph serve or first return!
                    if (VolleyballGameManager.Instance != null && 
                        Random.value < VolleyballGameManager.Instance.fastShotChance && 
                        VolleyballGameManager.Instance.ConsecutiveFastShots < 2 &&
                        currentRally >= 4)
                    {
                        isFastShot = true;
                        VolleyballGameManager.Instance.ConsecutiveFastShots++;
                        flightTime = VolleyballGameManager.Instance.fastShotFlightTime;
                        
                        // CRITICAL FIX: To avoid the ball plummeting unnaturally to the floor due to high gravity,
                        // we use the ball's natural gravity. If the shot is too fast and flat to clear the net,
                        // we slightly increase the flight time until it clears safely!
                        float currentGravity = ball.CustomGravity;
                        for (int i = 0; i < 20; i++)
                        {
                            if (VolleyballGameManager.Instance.WillClearNet(transform.position, playerTargetZone, flightTime, currentGravity)) break;
                            flightTime += 0.05f; // Add a tiny bit of flight time to create a naturally safer arc
                        }

                        chosenGravity = null; // Use natural gravity
                        Debug.Log($"[VolleyballOpponent] Firing FAST LOW SHOT in rally! FlightTime={flightTime:F2}s, Gravity=Natural");
                    }
                    else
                    {
                        if (VolleyballGameManager.Instance != null)
                        {
                            VolleyballGameManager.Instance.ConsecutiveFastShots = 0;
                        }
                        
                        // ── NORMAL SHOT ──
                        // Base flight time from the global difficulty ForceFactor
                        float globalForce = VolleyballGameManager.Instance != null ? VolleyballGameManager.Instance.CurrentDifficulty.forceFactor : 1.0f;
                        float baseFlightTime = Mathf.Max(1.0f, 2.0f / Mathf.Max(0.5f, globalForce));
                        
                        flightTime = baseFlightTime;
                        
                        if (VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.NetTransform != null)
                        {
                            float distanceToNet = Mathf.Abs(transform.position.z - VolleyballGameManager.Instance.NetTransform.position.z);
                            
                            // Close to net (< 2m): Add +1s to flight time (High lob)
                            if (distanceToNet < 2.0f)
                            {
                                flightTime = baseFlightTime + 1.0f;
                            }
                            // Far from net (> 4m): Reduce flight time by 20% (Fast, flat shot)
                            else if (distanceToNet > 4.0f)
                            {
                                flightTime = baseFlightTime * 0.8f; 
                            }
                            
                            // Rally Escalation for AI
                            if (currentRally > 2)
                            {
                                float t = (currentRally - 5) / 15.0f;
                                float percentageReduction = Mathf.Clamp(Mathf.LerpUnclamped(0.25f, 0.75f, t), 0f, 0.75f);
                                flightTime *= (1.0f - percentageReduction);
                            }
                            
                            // MATHEMATICAL NET SAFETY CHECK
                            float netTopY = 2.43f; // Standard volleyball net height
                            Collider netCol = VolleyballGameManager.Instance.NetTransform.GetComponentInChildren<Collider>();
                            if (netCol != null) netTopY = netCol.bounds.max.y;
                            
                            float safetyMargin = 0.4f; // Clear the net by at least 40cm
                            float requiredApexHeight = (netTopY + safetyMargin) - transform.position.y;
                            if (requiredApexHeight > 0)
                            {
                                float minSafeFlightTime = Mathf.Sqrt(8f * requiredApexHeight / ball.CustomGravity);
                                if (flightTime < minSafeFlightTime)
                                {
                                    flightTime = minSafeFlightTime;
                                }
                            }
                        }
                    }

                    // ADD CURVE AT HIGHER DIFFICULTIES (After long rallies)
                    float curve = 0f;
                    if (VolleyballGameManager.Instance != null)
                    {
                        int rallyCount = VolleyballGameManager.Instance.CurrentRallyCount;
                        var mode = VolleyballGameManager.Instance.difficultyMode;
                        
                        bool shouldCurve = false;
                        
                        // Hard difficulty
                        if (mode == VolleyballGameManager.DifficultyMode.Hard && rallyCount >= Random.Range(12, 17)) // Randomly triggers between 12 and 16
                        {
                            shouldCurve = true;
                        }
                        // Medium difficulty
                        else if (mode == VolleyballGameManager.DifficultyMode.Medium && rallyCount >= Random.Range(16, 21)) // Randomly triggers between 16 and 20
                        {
                            shouldCurve = true;
                        }

                        if (shouldCurve)
                        {
                            // Smooth, slight lateral acceleration between 0.8 and 1.5 m/s^2, randomly left or right
                            curve = Random.Range(0.8f, 1.5f) * (Random.value > 0.5f ? 1f : -1f);
                            Debug.Log($"[VolleyballOpponent] Mode {mode}, Rally {rallyCount}: Applying Curveball of {curve}");
                        }
                    }

                    // --- GUARANTEE NET CLEARANCE (0.2m above net top) ---
                    // Final safety check for ALL dog shots — both fast smashes and normal returns.
                    // If the computed trajectory still clips the net, nudge flight time up until it clears safely.
                    if (VolleyballGameManager.Instance != null)
                    {
                        float clearGravity = chosenGravity ?? ball.CustomGravity;
                        for (int i = 0; i < 30; i++)
                        {
                            if (VolleyballGameManager.Instance.WillClearNet(transform.position, playerTargetZone, flightTime, clearGravity)) break;
                            flightTime += 0.04f;
                        }
                    }

                    // Perform the actual physics launch (Sound & VFX handled here!)
                    ball.LaunchToTarget(playerTargetZone, flightTime, BallHitter.AI, null, 1.0f, curve, chosenGravity);
                    
                    VolleyballGameManager.Instance.HandleOpponentStrike();
                }
                else
                {
                    float globalForceError = VolleyballGameManager.Instance != null ? VolleyballGameManager.Instance.CurrentDifficulty.forceFactor : 1.0f;
                    float errorFlightTime = Mathf.Max(1.0f, 2.0f / Mathf.Max(0.5f, globalForceError));
                    Vector3 errorVector = new Vector3(Random.Range(-2f, 2f), Random.Range(1f, 3f), Random.Range(2f, 4f));
                    Vector3 errorTarget = transform.position - Vector3.forward * 3f + errorVector;
                    ball.LaunchToTarget(errorTarget, errorFlightTime, BallHitter.AI);
                    
                    VolleyballGameManager.Instance.HandleOpponentStrike();
                }
            }
        }

        private bool SimulateTrajectoryPassesNet(VolleyballBall ball, Vector3 startPos, Vector3 targetPos, float timeToTarget, float netZ, float requiredHeightY)
        {
            float k = ball.AirResistance;
            float g = ball.CustomGravity;
            Vector3 displacement = targetPos - startPos;
            
            float vy;
            if (k < 0.001f)
            {
                vy = (displacement.y + 0.5f * g * timeToTarget * timeToTarget) / timeToTarget;
            }
            else
            {
                float e_kT = Mathf.Exp(-k * timeToTarget);
                float term = (1f - e_kT) / k;
                vy = (displacement.y + (g / k) * timeToTarget - (g / k) * term) / term;
            }

            float vz = displacement.z / timeToTarget;
            if (k >= 0.001f)
            {
                float term = (1f - Mathf.Exp(-k * timeToTarget)) / k;
                vz = displacement.z / term;
            }

            float distToNetZ = Mathf.Abs(netZ - startPos.z);
            float totalDistZ = Mathf.Abs(displacement.z);
            
            if (totalDistZ < 0.01f) return true;

            float timeToNet = 0f;
            if (k < 0.001f)
            {
                timeToNet = distToNetZ / Mathf.Abs(vz);
            }
            else
            {
                float term = 1f - (k * distToNetZ) / Mathf.Abs(vz);
                if (term <= 0f) return false;
                timeToNet = -Mathf.Log(term) / k;
            }

            float heightAtNet;
            if (k < 0.001f)
            {
                heightAtNet = startPos.y + vy * timeToNet - 0.5f * g * timeToNet * timeToNet;
            }
            else
            {
                float e_kT = Mathf.Exp(-k * timeToNet);
                heightAtNet = startPos.y + (vy + g / k) * (1f - e_kT) / k - (g / k) * timeToNet;
            }

            return heightAtNet >= requiredHeightY;
        }

        public void ResetPosition()
        {
            transform.position = startPosition;
            isTracking = false;
            isAttempting = false;
            hasFiredHitAnimation = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Ball"))
            {
                VolleyballBall ball = collision.gameObject.GetComponent<VolleyballBall>();
                if (ball == null) ball = collision.gameObject.GetComponentInParent<VolleyballBall>();
                
                if (ball != null)
                {
                    ball.LastHitter = BallHitter.AI;
                }
            }
        }
    }
}
