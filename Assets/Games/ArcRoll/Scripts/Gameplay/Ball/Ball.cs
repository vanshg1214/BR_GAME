using UnityEngine;
using ArcRoll.Gameplay.Helpers;

namespace ArcRoll.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Ball : MonoBehaviour
    {
        public enum BallType
        {
            Basketball,
            BowlingBall
        }
        
        public enum BallState
        {
            InRack,
            TravelingToTarget,
            AtRestPosition,
            Grabbed,
            Thrown,
            Missed,
            Dead
        }

        [Header("Script")]
        [SerializeField] private BallType type;
        public BallType Type => type;

        [Header("Cleanup")]
        [Tooltip("How many seconds after hitting the floor should the BALL disappear?")]
        [SerializeField] private float floorDespawnDelay = 6.0f;
        
        [SerializeField] private string floorTag = "Floor";

        [Header("Rehab Settings")]
        [Tooltip("General power multiplier for when the patient throws it.")]
        [SerializeField] private float throwPowerMultiplier = 1.5f;

        [Tooltip("1.0 = Perfect Auto-Aim. 0.0 = Pure manual physics. 0.8 is recommended for rehab.")]
        [Range(0f, 1f)]
        [SerializeField] private float aimAssistStrength = 0.8f;

        private float EffectiveAimAssistStrength
        {
            get
            {
                string diff = ArcRoll.UI.ArcRollMenuManager.Difficulty;
                if (type == BallType.Basketball) return diff switch { "Easy" => 1.0f, "Hard" => 0.8f, _ => 0.9f };
                if (type == BallType.BowlingBall) return diff switch { "Easy" => 0.5f, "Hard" => 0.3f, _ => 0.4f };
                return ArcRoll.UI.ArcRollMenuManager.AimAssistStrength;
            }
        }
        
        [Tooltip("How far off the player can throw (in degrees) and still get assisted.")]
        [Range(10f, 90f)]
        [SerializeField] private float aimAssistConeAngle = 45.0f;

        [Header("Parabolic Auto-Lob (Basketball)")]
        [Tooltip("Enable mathematically perfect parabolas for the basketball. If false, it acts like a laser.")]
        [SerializeField] private bool enableAutoLob = false;

        [Tooltip("How much higher than the target point should the arc peak?")]
        [SerializeField] private float hoopHeightOffset = 0f;

        [Tooltip("How long the perfect lob should take to fly through the air. 1.2s creates a beautiful, satisfying arc.")]
        [SerializeField] private float lobTimeOfFlight = 1.2f;

        [Header("In-Flight Magnetic Assist")]
        [Tooltip("If enabled, the basketball will act like a magnet when it gets close to the hoop, gently pulling itself into the net even if the arc was slightly off.")]
        [SerializeField] private bool enableInFlightMagnet = true;
        
        [Tooltip("How strongly the hoop pulls the ball. Higher values look like magic, lower values look natural. 15 is great for rehab.")]
        [Range(0f, 20f)]
        [SerializeField] private float magneticPullStrength = 15.0f;

        [Tooltip("The radius (in meters) around the hoop where the magnet activates. Lower it if it ruins your throw!")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float magneticPullRadius = 2.5f;

        public event System.Action<Ball, BallState> OnStateChanged;

        // ── State ─────────────────────────────────────────────────────────────
        private BallState _state = BallState.InRack;
        public BallState State => _state;

        private Rigidbody rb;
        private bool isTravelingToTarget;
        private Vector3 targetDestination;
        private Vector3 startTravelPosition;
        private float travelProgress;
        private float totalTravelTime;
        public bool hasScored = false; // Tracks if the ball successfully hit a target
        public bool HasTargetAssigned { get; private set; } = false;

        private bool originalIsKinematic;
        private bool originalUseGravity;

        // ── Unity & Helpers ───────────────────────────────────────────────────
        private GameObject hoverAnchor;
        private FixedJoint hoverJoint;
        private Collider ballCollider;
        
        private Vector3 trueTargetHoopPosition;
        private BallInteractionHelper interactionHelper;
        private AutoGrabInteractable autoGrabHelper;
        private GameObject spawnedRomRing;
        private float timeSinceThrown = 0f;
        private float stoppedTimer = 0f;
        private bool hasTouchedFloor = false;
        
        // Auto-grab lock offsets (cached once at grab time for rock-solid holding)
        private Vector3 _cachedLocalOffset = Vector3.zero;
        private Quaternion _cachedLocalRotOffset = Quaternion.identity;
        private bool _autoGrabLocked = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ballCollider = GetComponent<Collider>();
            originalIsKinematic = rb.isKinematic;
            originalUseGravity  = rb.useGravity;
            
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            
            // Initialize both interaction helpers
            interactionHelper = new BallInteractionHelper();
            
            autoGrabHelper = GetComponent<AutoGrabInteractable>();
            if (autoGrabHelper == null)
            {
                autoGrabHelper = gameObject.AddComponent<AutoGrabInteractable>();
            }
            autoGrabHelper.Initialize(transform.position);
            
            // Look for ALL manual grab interactables on this object or any of its children
            var grabInteractables = new System.Collections.Generic.List<MonoBehaviour>();
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string tName = mb.GetType().Name;
                if (tName != "AutoGrabInteractable" && (tName.Contains("Interactable") || tName.Contains("Grabbable") || tName.Contains("Grabber")))
                {
                    grabInteractables.Add(mb);
                }
            }
            interactionHelper.Initialize(grabInteractables, transform.position);
            
            // Prevent the ball from physically bouncing off the player's head or body when they wind up a throw!
            if (Camera.main != null)
            {
                foreach (Collider pc in Camera.main.transform.root.GetComponentsInChildren<Collider>())
                {
                    if (pc != null && !pc.isTrigger && pc != ballCollider) Physics.IgnoreCollision(ballCollider, pc, true);
                }
            }
            else Debug.LogWarning("[Ball] Camera.main is NULL! Cannot automatically ignore player collisions.");
        }

        private void FixedUpdate()
        {
            if (!isTravelingToTarget)
            {
                // ── In-Flight Magnetic Assist ──
                if (_state == BallState.Thrown && enableInFlightMagnet && HasTargetAssigned)
                {
                    if (type == BallType.Basketball)
                    {
                        BallFlightMagnet.ApplyMagneticPull(rb, TrueTargetHoopPosition, hoopHeightOffset, magneticPullStrength, magneticPullRadius, aimAssistConeAngle);
                    }
                    else if (type == BallType.BowlingBall && EffectiveAimAssistStrength > 0f)
                    {
                        // Homing for the bowling ball as it rolls down the lane towards the pins.
                        Vector3 targetPos = TrueTargetHoopPosition;
                        
                        Vector3 currentVel = rb.linearVelocity;
                        float speed = currentVel.magnitude;
                        
                        // Only steer if it's moving and still airborne
                        if (speed > 0.5f)
                        {
                            // Compare flat (horizontal) vectors only — ignore the Y drop
                            Vector3 flatCurrentVel = new Vector3(currentVel.x, 0, currentVel.z).normalized;
                            Vector3 flatTargetDir = new Vector3(targetPos.x - rb.position.x, 0, targetPos.z - rb.position.z).normalized;
                            float angleError = Vector3.Angle(flatCurrentVel, flatTargetDir);

                            // ONLY assist if the throw is within the allowed cone angle!
                            if (angleError <= aimAssistConeAngle)
                            {
                                // Steer flat only — preserve vertical velocity so the arc is natural
                                Vector3 flatDesiredDir = flatTargetDir;
                                Vector3 flatCurrent = new Vector3(currentVel.x, 0, currentVel.z);
                                float flatSpeed = flatCurrent.magnitude;
                                
                                float blendFactor = EffectiveAimAssistStrength * (1.0f - (angleError / aimAssistConeAngle));
                                Vector3 steeredFlat = Vector3.Lerp(flatCurrent, flatDesiredDir * flatSpeed, Time.fixedDeltaTime * (magneticPullStrength * blendFactor));
                                
                                // Re-combine: steered horizontal + original vertical
                                rb.linearVelocity = new Vector3(steeredFlat.x, currentVel.y, steeredFlat.z);
                            }
                        }
                    }
                }
                return;
            }

            travelProgress += Time.fixedDeltaTime;
            float t = travelProgress / totalTravelTime;
            
            if (t >= 1.0f)
            {
                transform.position  = targetDestination;
                isTravelingToTarget = false;
                
                rb.isKinematic      = false;
                rb.useGravity       = true;
                rb.linearVelocity   = Vector3.zero;
                rb.angularVelocity  = Vector3.zero;

                hoverAnchor = new GameObject("HoverAnchor_" + gameObject.name);
                hoverAnchor.transform.position = targetDestination;
                var anchorRb = hoverAnchor.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
                
                hoverJoint = gameObject.AddComponent<FixedJoint>();
                hoverJoint.connectedBody = anchorRb;
                hoverJoint.breakForce = 500f; 

                SetState(BallState.AtRestPosition);
            }
            else
            {
                // Linear position
                Vector3 currentPos = Vector3.Lerp(startTravelPosition, targetDestination, t);
                
                // Add Parabolic Arc! 
                // Mathf.Sin(t * PI) gives a beautiful curve that starts at 0, peaks at 1 in the middle, and ends at 0.
                float arcHeight = 1.2f; // Maximum height of the arc in meters
                currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                
                rb.MovePosition(currentPos);
                
                // Spin naturally in the air
                rb.MoveRotation(transform.rotation * Quaternion.Euler(180f * Time.fixedDeltaTime, 0, 0));
            }
        }

        private void Update()
        {
            bool isAutoGrab = autoGrabHelper != null && autoGrabHelper.enableAutoGrab && ArcRoll.UI.ArcRollMenuManager.IsAutoGrabMode;

            // Enforce strict mutual exclusivity: 
            // If Auto-Grab is ON, turn OFF manual ISDK grab. If Auto-Grab is OFF, turn ON manual ISDK grab.
            if (interactionHelper != null)
            {
                interactionHelper.SetInteractableEnabled(!isAutoGrab);
            }

            // GLOBAL GRAB CHECK: Allow the player to catch a bouncing/thrown ball at any time!
            if (_state != BallState.Dead && _state != BallState.Grabbed)
            {
                bool shouldGrab = isAutoGrab 
                    ? autoGrabHelper.CheckForAutoGrab(transform.position) 
                    : interactionHelper.HasUserGrabbedBall();

                if (shouldGrab)
                {
                    if (hoverJoint != null) Destroy(hoverJoint);
                    if (hoverAnchor != null) Destroy(hoverAnchor);

                    NotifyGrabbed();
                }
            }

            if (_state == BallState.AtRestPosition)
            {
                // Failsafe fallback: if they somehow move the ball away from rest position without ISDK triggering Select
                if (Vector3.Distance(transform.position, targetDestination) > 0.15f)
                {
                    float dist = Vector3.Distance(transform.position, targetDestination);
                    Debug.LogWarning($"[Ball] *** FAILSAFE TRIGGERED *** Ball moved {dist:F2}m from rest position '{targetDestination}'. Current pos: {transform.position}. Triggering NotifyGrabbed().");
                    
                    if (hoverJoint != null) Destroy(hoverJoint);
                    if (hoverAnchor != null) Destroy(hoverAnchor);

                    // Reset velocity tracking so the hand proximity fallback starts fresh
                    interactionHelper.ResetVelocityTracking(transform.position);
                    NotifyGrabbed();
                }
            }
            else if (_state == BallState.Grabbed)
            {
                if (isAutoGrab)
                {
                    // Just record velocity for throw detection — position is locked in LateUpdate
                    autoGrabHelper.RecordVelocity(transform.position, Time.time);

                    if (autoGrabHelper.HasUserReleasedBall(transform.position))
                    {
                        Vector3 bestVelocity = autoGrabHelper.GetAverageThrowVelocity();
                        ApplyThrowVelocity(bestVelocity);
                    }
                }
                else
                {
                    // Normal ISDK/OVRGrabber tracking
                    interactionHelper.RecordVelocity(transform.position, Time.time);

                    if (interactionHelper.HasUserReleasedBall())
                    {
                        Vector3 bestVelocity = interactionHelper.GetAverageThrowVelocity();
                        float handSpeed = bestVelocity.magnitude;
                        float physicsSpeed = rb.linearVelocity.magnitude;
                        
                        if (handSpeed > 0.05f) bestVelocity = bestVelocity.normalized * Mathf.Max(handSpeed, physicsSpeed);
                        else if (physicsSpeed > 0.3f) bestVelocity = rb.linearVelocity;

                        Debug.Log($"[Ball Debug] Normal Grab Released! Raw throw velocity from InteractionHelper: {bestVelocity} (Magnitude: {bestVelocity.magnitude})");
                        ApplyThrowVelocity(bestVelocity);
                    }
                }
            }
            else if (_state == BallState.Thrown)
            {
                // Wait until it touches the floor AND actually stops moving (rolling/bouncing)!
                if (hasTouchedFloor)
                {
                    // If the ball is basically stopped (velocity near 0)
                    if (rb.linearVelocity.sqrMagnitude < 0.05f)
                    {
                        stoppedTimer += Time.deltaTime;
                        if (stoppedTimer >= 1.5f) // Wait 1.5 seconds of being stopped
                        {
                            TriggerDespawn(0f); // Already waited, shrink now!
                        }
                    }
                    else
                    {
                        stoppedTimer = 0f;
                    }
                }
                
                // Failsafe for all balls (in case they fall off the map)
                timeSinceThrown += Time.deltaTime;
                if (timeSinceThrown > 12.0f)
                {
                    TriggerDespawn(0f);
                }
            }
        }

        private void LateUpdate()
        {
            // Rock-solid lock: apply cached offset to hand every frame AFTER all physics
            if (_autoGrabLocked && _state == BallState.Grabbed)
            {
                Transform hand = autoGrabHelper.GetHandTracker();
                if (hand != null)
                {
                    transform.position = hand.TransformPoint(_cachedLocalOffset);
                    transform.rotation = hand.rotation * _cachedLocalRotOffset;
                }
            }
        }

        private void ApplyThrowVelocity(Vector3 bestVelocity)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            _autoGrabLocked = false;

            if (bestVelocity.magnitude > 0.05f)
            {
                Vector3 finalVelocity = bestVelocity;
                
                if (HasTargetAssigned)
                {
                    finalVelocity = BallPhysicsHelper.CalculateAssistedThrowVelocity(
                        rawVelocity: bestVelocity,
                        ballPosition: transform.position,
                        trueTargetPosition: TrueTargetHoopPosition,
                        ballType: type,
                        aimAssistStrength: EffectiveAimAssistStrength,
                        aimAssistConeAngle: aimAssistConeAngle,
                        enableAutoLob: enableAutoLob,
                        hoopHeightOffset: hoopHeightOffset,
                        lobTimeOfFlight: lobTimeOfFlight,
                        throwPowerMultiplier: throwPowerMultiplier
                    );
                    Debug.Log($"[Ball Debug] ApplyThrowVelocity (Assisted): Raw={bestVelocity} | Final={finalVelocity} | Target={TrueTargetHoopPosition} | AssistStrength={EffectiveAimAssistStrength}");
                }
                else
                {
                    // If NO target is assigned (e.g. ball was grabbed off a table), just apply raw physics!
                    finalVelocity = bestVelocity * throwPowerMultiplier;
                    if (type == BallType.BowlingBall)
                    {
                        finalVelocity.y = 0f; // Keep it on the ground
                        finalVelocity *= 1.6f; // Standard physics boost for bowling
                    }
                    Debug.Log($"[Ball Debug] ApplyThrowVelocity (Raw/No Target): Raw={bestVelocity} | Final={finalVelocity}");
                }

                rb.linearVelocity = finalVelocity;
                rb.angularVelocity *= throwPowerMultiplier * 0.5f;
            }

            SetState(BallState.Thrown);
            timeSinceThrown = 0f;
        }

        private void SetState(BallState newState)
        {
            _state = newState;
            OnStateChanged?.Invoke(this, _state);
            
            if (_state == BallState.Dead)
            {
                rb.isKinematic = originalIsKinematic;
                rb.useGravity = originalUseGravity;
                
                if (hoverAnchor != null)
                {
                    Destroy(hoverAnchor);
                }

                // Failsafe: Destroy the ROM Ring if it was never grabbed
                if (spawnedRomRing != null)
                {
                    Destroy(spawnedRomRing);
                    spawnedRomRing = null;
                }
            }
        }

        private void OnCollisionEnter(Collision col)
        {
            // NEW DEBUG LOG: Print exactly what we hit!
            Debug.Log($"[Ball] COLLISION: '{gameObject.name}' hit '{col.gameObject.name}' (Tag: {col.gameObject.tag}) | State: {_state}");

            if (_state != BallState.Thrown && _state != BallState.Missed) return;
            if (!col.gameObject.CompareTag(floorTag)) return;

            if (!hasTouchedFloor)
            {
                hasTouchedFloor = true;
                
                if (type == BallType.Basketball)
                {
                    // Immediately register the miss logically so the next target can spawn,
                    // without deleting the physical bouncy ball!
                    if (!hasScored && ArcRoll.Core.ArcRollScoreManager.Instance != null)
                    {
                        ArcRoll.Core.ArcRollScoreManager.Instance.RecordError();
                    }

                    if (_state != BallState.Missed) 
                    {
                        SetState(BallState.Missed);
                        // Let it bounce realistically for 1.5 seconds, then shrink and pop to keep the floor clean!
                        StartCoroutine(ShrinkAndDestroyRoutine(1.5f));
                    }
                }
            }
        }
        
        private void TriggerDespawn(float delay = 0f)
        {
            if (_state == BallState.Dead) return;

            // Streak Logic: If the bowling ball despawns (because it rested on the floor or fell off map) and never scored, it's a MISS!
            // We ensure _state != Missed because the Basketball already recorded its error in OnCollisionEnter!
            if (!hasScored && _state != BallState.Missed && ArcRoll.Core.ArcRollScoreManager.Instance != null)
            {
                ArcRoll.Core.ArcRollScoreManager.Instance.RecordError();
            }

            SetState(BallState.Dead);
            StartCoroutine(ShrinkAndDestroyRoutine(delay));
        }

        private System.Collections.IEnumerator ShrinkAndDestroyRoutine(float initialDelay)
        {
            if (initialDelay > 0)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            float duration = 0.5f;
            float elapsed = 0f;
            Vector3 initialScale = transform.localScale;
            
            // Disable physics so it doesn't glitch while shrinking
            rb.isKinematic = true;
            if (ballCollider != null) ballCollider.enabled = false;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed / duration);
                yield return null;
            }
            
            Destroy(gameObject);
        }

        private Transform targetHoopTransform;

        public Vector3 TrueTargetHoopPosition
        {
            get
            {
                if (targetHoopTransform != null)
                {
                    if (targetHoopTransform.TryGetComponent<BasketballHoop>(out var hoop))
                    {
                        return hoop.TargetPoint;
                    }
                    return targetHoopTransform.position;
                }
                return trueTargetHoopPosition;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void FireToTarget(Vector3 destination, Vector3 actualHoopPosition, float speed, GameObject romRing = null, Transform liveTargetTransform = null)
        {
            startTravelPosition    = transform.position;
            targetDestination      = destination;
            trueTargetHoopPosition = actualHoopPosition;
            targetHoopTransform    = liveTargetTransform;
            isTravelingToTarget    = true;
            spawnedRomRing         = romRing;
            HasTargetAssigned      = true;

            float distance = Vector3.Distance(startTravelPosition, targetDestination);
            totalTravelTime = distance / speed;
            travelProgress = 0f;

            rb.isKinematic = true;
            rb.useGravity  = false;
            
            if (ballCollider != null) ballCollider.isTrigger = true;

            SetState(BallState.TravelingToTarget);
        }

        public void NotifyGrabbed()
        {
            // Abort the incoming travel loop if the player catches it early!
            isTravelingToTarget = false;
            
            bool isAutoGrab = autoGrabHelper != null && autoGrabHelper.enableAutoGrab && ArcRoll.UI.ArcRollMenuManager.IsAutoGrabMode;

            if (isAutoGrab)
            {
                autoGrabHelper.SnapObjectToHand(transform);

                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                Transform hand = autoGrabHelper.GetHandTracker();
                if (hand != null)
                {
                    _cachedLocalOffset = hand.InverseTransformPoint(transform.position);
                    _cachedLocalRotOffset = Quaternion.Inverse(hand.rotation) * transform.rotation;
                }
                _autoGrabLocked = true;
            }
            else
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                _autoGrabLocked = false;
            }

            if (ballCollider != null) ballCollider.isTrigger = false;
            SetState(BallState.Grabbed);

            // Reset velocity tracking so this throw starts with a clean slate.
            interactionHelper.ResetVelocityTracking(transform.position);
            autoGrabHelper.ResetVelocityTracking(transform.position);

            // The player picked up the ball! Destroy the Ring 1 second later.
            if (spawnedRomRing != null)
            {
                Destroy(spawnedRomRing, 1.0f);
                spawnedRomRing = null;
            }
        }

        public void ReleaseAfterScore()
        {
            isTravelingToTarget = false;
            enableInFlightMagnet = false;

            if (ballCollider != null)
            {
                ballCollider.isTrigger = false;
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.down * 3.5f;
            rb.angularVelocity = Random.insideUnitSphere * 2.0f;

            SetState(BallState.Thrown);
        }
    }
}
