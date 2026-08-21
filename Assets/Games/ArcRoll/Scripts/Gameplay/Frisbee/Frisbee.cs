using UnityEngine;
using ArcRoll.Gameplay.Helpers;

namespace ArcRoll.Gameplay.Frisbee
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Frisbee : MonoBehaviour
    {
        public enum FrisbeeState
        {
            InRack,
            TravelingToTarget,
            AtRestPosition,
            Grabbed,
            Thrown,
            Missed,
            Dead
        }

        [Header("Cleanup")]
        [SerializeField] private float floorDespawnDelay = 6.0f;
        [SerializeField] private string floorTag = "Floor";

        [Header("Aerodynamics (Rehab Tuned)")]
        [Tooltip("How strongly the frisbee glides based on its forward velocity.")]
        [SerializeField] private float liftCoefficient = 1.2f;
        [Tooltip("Reduces speed over time. A lower value lets it fly much further.")]
        [SerializeField] private float dragCoefficient = 0.05f;
        [Tooltip("General power multiplier for when the patient throws it.")]
        [SerializeField] private float throwPowerMultiplier = 1.5f;
        [Tooltip("Helps keep the frisbee perfectly flat even if the wrist was tilted on release.")]
        [SerializeField] private bool autoLevelRotation = true;

        [Header("Aim Assist")]
        [Tooltip("1.0 = Perfect Auto-Aim. 0.0 = Pure manual physics. 0.6 is recommended.")]
        [Range(0f, 1f)]
        [SerializeField] private float aimAssistStrength = 0.2f;
        [Tooltip("How far off the player can throw (in degrees) and still get assisted.")]
        [Range(10f, 90f)]
        [SerializeField] private float aimAssistConeAngle = 30.0f;

        public event System.Action<Frisbee, FrisbeeState> OnStateChanged;

        // ── State ─────────────────────────────────────────────────────────────
        private FrisbeeState _state = FrisbeeState.InRack;
        public FrisbeeState State => _state;

        private Rigidbody rb;
        private bool isTravelingToTarget;
        private Vector3 targetDestination;
        private Vector3 startTravelPosition;
        private float travelProgress;
        private float totalTravelTime;
        public bool hasScored = false;

        private GameObject hoverAnchor;
        private FixedJoint hoverJoint;
        private Collider frisbeeCollider;
        
        private Vector3 trueTargetPosition;
        private FrisbeeInteractionHelper interactionHelper;
        private AutoGrabInteractable autoGrabHelper;
        private float timeSinceThrown = 0f;
        private float stoppedTimer = 0f;
        private bool hasTouchedFloor = false;

        // Recorded when the disc arrives at the hover spot — defines how "flat" looks
        // for THIS specific prefab regardless of its baked-in rotation.
        private Vector3 _discFaceAxis = Vector3.up;   // the axis pointing "up" out of the disc face in world coordinates
        private Vector3 _localFaceAxis = Vector3.up;  // the axis pointing "up" out of the disc face in local coordinates
        private Quaternion _restRotation;              // the exact rotation when disc was flat at player
        private Quaternion _qTilt;
        private float _spinRate;
        private float _spinAngle;
        private float _effectiveAimAssist;
        private bool _faceAxisRecorded = false;
        
        // Auto-grab lock offsets (cached once at grab time for rock-solid holding)
        private Vector3 _cachedLocalOffset = Vector3.zero;
        private Quaternion _cachedLocalRotOffset = Quaternion.identity;
        private bool _autoGrabLocked = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            frisbeeCollider = GetComponent<Collider>();
            
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _restRotation = transform.rotation;  // Save the prefab's initial flat rotation
            _localFaceAxis = transform.InverseTransformDirection(Vector3.up);
            
            interactionHelper = new FrisbeeInteractionHelper();
            
            autoGrabHelper = GetComponent<AutoGrabInteractable>();
            if (autoGrabHelper == null)
            {
                autoGrabHelper = gameObject.AddComponent<AutoGrabInteractable>();
            }
            autoGrabHelper.Initialize(transform.position);

            // Look for ALL manual grab interactables on this object or any of its children
            System.Collections.Generic.List<MonoBehaviour> grabInteractables = new System.Collections.Generic.List<MonoBehaviour>();
            MonoBehaviour[] allBehaviors = GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in allBehaviors)
            {
                if (mb != null)
                {
                    string tName = mb.GetType().Name;
                    if (tName.Contains("Interactable") || tName.Contains("Grabbable") || tName.Contains("Grabber"))
                    {
                        // Ignore our own custom AutoGrab script
                        if (tName != "AutoGrabInteractable")
                        {
                            grabInteractables.Add(mb);
                        }
                    }
                }
            }
            
            interactionHelper.Initialize(grabInteractables, transform.position);
            
            if (Camera.main != null)
            {
                Collider[] playerColliders = Camera.main.transform.root.GetComponentsInChildren<Collider>();
                foreach (Collider pc in playerColliders)
                {
                    if (pc != null && !pc.isTrigger && pc != frisbeeCollider)
                        Physics.IgnoreCollision(frisbeeCollider, pc, true);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!isTravelingToTarget)
            {
                if (_state == FrisbeeState.Thrown)
                {
                    // Aerodynamic Glide Physics
                    Vector3 velocity = rb.linearVelocity;
                    float speed = velocity.magnitude;

                    if (speed > 0.1f)
                    {
                        // 1. Aerodynamic Lift (Rehab Tuned)
                        Vector3 forwardVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
                        float forwardSpeed = forwardVelocity.magnitude;
                        
                        // Calculate a glide factor: 0.0 at rest, 1.0 at 2m/s (very gentle throw)
                        float glideFactor = Mathf.Clamp01(forwardSpeed / 2.0f);
                        
                        // We apply an upward force that perfectly cancels gravity, scaled by the glide factor and lift coefficient.
                        // If liftCoefficient is 1.0, a 2m/s throw will fly perfectly horizontal and never drop.
                        Vector3 counterGravity = -Physics.gravity;
                        Vector3 liftForce = counterGravity * (glideFactor * liftCoefficient);
                        
                        rb.AddForce(liftForce, ForceMode.Acceleration);

                        // 2. Drag
                        Vector3 dragForce = -velocity.normalized * (speed * speed * dragCoefficient);
                        rb.AddForce(dragForce, ForceMode.Acceleration);

                        // 3. In-Flight Aim Assist (Curve towards target)
                        float bankAngle = 0f;
                        if (_effectiveAimAssist > 0f)
                        {
                            Vector3 currentDir = velocity.normalized;
                            Vector3 targetDir = (trueTargetPosition - rb.position).normalized;
                            float angleError = Vector3.Angle(currentDir, targetDir);

                            // No dead zone — gently curve towards the target continuously as long as it's within the wide cone.
                            if (angleError > 0.5f && angleError <= aimAssistConeAngle)
                            {
                                // Normalise so assist is stronger when farther off target
                                float normalizedError = angleError / aimAssistConeAngle;
                                float blendFactor = _effectiveAimAssist * normalizedError;
                                
                                // Gentle steer — keeps natural feel, no sudden snaps
                                float steerSpeed = 3.0f;
                                Vector3 steeredDir = Vector3.Lerp(currentDir, targetDir, blendFactor * Time.fixedDeltaTime * steerSpeed).normalized;
                                rb.linearVelocity = steeredDir * speed;

                                // Calculate bank angle for realism: if turning right, bank right.
                                Vector3 flatCurrent = new Vector3(currentDir.x, 0, currentDir.z).normalized;
                                Vector3 flatTarget = new Vector3(targetDir.x, 0, targetDir.z).normalized;
                                float signedAngleToTarget = Vector3.SignedAngle(flatCurrent, flatTarget, Vector3.up);
                                
                                // Bank up to ~35 degrees based on how sharp the turn is
                                bankAngle = -signedAngleToTarget * blendFactor * 1.5f;
                            }
                        }

                        // 4. Rotation — FULLY MANUAL, no physics angular velocity.
                        // A real frisbee only spins on its face axis (the green axis). It never tumbles.
                        rb.angularVelocity = Vector3.zero;

                        // Calculate flat target orientation facing flight direction
                        Vector3 forwardDir = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
                        if (forwardDir.sqrMagnitude < 0.01f)
                            forwardDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                        if (forwardDir.sqrMagnitude < 0.01f)
                            forwardDir = Vector3.forward;

                        Vector3 defaultForward = Vector3.ProjectOnPlane(_restRotation * Vector3.forward, Vector3.up).normalized;
                        if (defaultForward.sqrMagnitude < 0.01f)
                            defaultForward = Vector3.ProjectOnPlane(_restRotation * Vector3.right, Vector3.up).normalized;

                        float angle = Vector3.SignedAngle(defaultForward, forwardDir, Vector3.up);
                        Quaternion qFlat = Quaternion.AngleAxis(angle, Vector3.up) * _restRotation;

                        // Apply aerodynamic banking (tilt) to the flat rotation
                        Quaternion qBank = Quaternion.AngleAxis(bankAngle, forwardDir);
                        Quaternion qTargetTilt = qBank * qFlat;

                        // Slerp the tilt towards flat + bank
                        _qTilt = Quaternion.Slerp(_qTilt, qTargetTilt, Time.fixedDeltaTime * 6f);

                        // Accumulate spin
                        _spinAngle += _spinRate * Time.fixedDeltaTime;

                        // Apply tilt and spin around the local face axis
                        Quaternion qSpin = Quaternion.AngleAxis(_spinAngle, _localFaceAxis);
                        rb.MoveRotation(_qTilt * qSpin);
                    }
                }
                return;
            }

            // Spawning Animation
            travelProgress += Time.fixedDeltaTime;
            float t = travelProgress / totalTravelTime;
            
            if (t >= 1.0f)
            {
                transform.position = targetDestination;
                isTravelingToTarget = false;
                
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // ── Record the disc face axis NOW while the disc is perfectly flat ──
                // transform.up is the axis pointing out of the disc face at this moment.
                // We use this for ALL future spin and auto-level calculations so the
                // prefab's baked-in rotation (e.g. Z=-90) is correctly accounted for.
                if (!_faceAxisRecorded)
                {
                    _discFaceAxis = transform.up;
                    _restRotation = transform.rotation;  // Update with the final settled rotation
                    _spinAngle = 0f;                      // Reset spin for the throw phase
                    _faceAxisRecorded = true;
                }

                hoverAnchor = new GameObject("HoverAnchor_" + gameObject.name);
                hoverAnchor.transform.position = targetDestination;
                var anchorRb = hoverAnchor.AddComponent<Rigidbody>();
                anchorRb.isKinematic = true;
                
                hoverJoint = gameObject.AddComponent<FixedJoint>();
                hoverJoint.connectedBody = anchorRb;
                hoverJoint.breakForce = 500f; 

                SetState(FrisbeeState.AtRestPosition);
            }
            else
            {
                // Frisbees glide in completely flat
                Vector3 currentPos = Vector3.Lerp(startTravelPosition, targetDestination, t);
                rb.MovePosition(currentPos);
                
                // Manual spin during travel — 1080 deg/sec looks good for the glide-in
                _spinAngle += 1080f * Time.fixedDeltaTime;
                // Spin around the disc's local face axis
                Quaternion spinRot = Quaternion.AngleAxis(_spinAngle, _localFaceAxis);
                rb.MoveRotation(_restRotation * spinRot);
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

            // GLOBAL GRAB CHECK: Allow catching a flying/bouncing frisbee at any time!
            if (_state != FrisbeeState.Dead && _state != FrisbeeState.Grabbed)
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

            if (_state == FrisbeeState.AtRestPosition)
            {
                if (Vector3.Distance(transform.position, targetDestination) > 0.15f)
                {
                    if (hoverJoint != null) Destroy(hoverJoint);
                    if (hoverAnchor != null) Destroy(hoverAnchor);
                    // Reset velocity tracking so the hand proximity fallback starts fresh
                    interactionHelper.ResetVelocityTracking(transform.position);
                    autoGrabHelper.ResetVelocityTracking(transform.position);
                    NotifyGrabbed();
                }
            }
            if (_state == FrisbeeState.Grabbed)
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
                        // No rb.linearVelocity fallback — that raw physics value is junk
                        // from ISDK's internal joint system and sends the frisbee randomly.
                        ApplyThrowVelocity(bestVelocity);
                    }
                }
            }
            else if (_state == FrisbeeState.Thrown)
            {
                if (hasTouchedFloor)
                {
                    if (rb.linearVelocity.sqrMagnitude < 0.05f)
                    {
                        stoppedTimer += Time.deltaTime;
                        if (stoppedTimer >= 1.5f) TriggerDespawn(0f);
                    }
                    else
                    {
                        stoppedTimer = 0f;
                    }
                }
                
                timeSinceThrown += Time.deltaTime;
                if (timeSinceThrown > 12.0f) TriggerDespawn(0f);
            }
        }

        private void ApplyThrowVelocity(Vector3 bestVelocity)
        {
            // Restore physics
            rb.isKinematic = false;
            rb.useGravity = true;
            _autoGrabLocked = false;

            // Get difficulty-specific settings for aim assist
            string diff = ArcRoll.UI.ArcRollMenuManager.Difficulty;
            float reqMinVel = 3.0f; // Medium
            float baseAssist = 0.6f;

            if (diff == "Easy") 
            {
                reqMinVel = 2.0f;
                baseAssist = 0.7f;
            }
            else if (diff == "Hard") 
            {
                reqMinVel = 4.0f;
                baseAssist = 0.4f;
            }

            // SPEED FIX: ISDK hand tracking often reports a lower speed than the physics joint snap.
            // We take the max of hand speed and physics speed to get the "good speed like before",
            // while completely ignoring the physics joint's random/terrible direction.
            float handSpeed = bestVelocity.magnitude;
            float physicsSpeed = rb.linearVelocity.magnitude;
            float rawSpeed = Mathf.Max(handSpeed, physicsSpeed);

            // AIM ASSIST AT RELEASE: Prevent wrist jerks from changing the horizontal direction.
            // We only blend the HORIZONTAL direction. The player's vertical lift (Y axis) is kept 100% raw.
            Vector3 playerDir = bestVelocity.normalized;
            Vector3 targetDir = (trueTargetPosition - transform.position).normalized;
            
            Vector3 flatPlayerDir = new Vector3(playerDir.x, 0, playerDir.z).normalized;
            Vector3 flatTargetDir = new Vector3(targetDir.x, 0, targetDir.z).normalized;

            float throwAngleError = Vector3.Angle(flatPlayerDir, flatTargetDir);
            bool isWithinCone = throwAngleError <= aimAssistConeAngle;

            // Blend horizontal direction ONLY if thrown within the allowed cone
            float finalAssistStr = 0f;
            if (isWithinCone)
            {
                finalAssistStr = Mathf.Clamp01(baseAssist + 0.1f);
            }
            
            Vector3 assistedFlatDir = Vector3.Lerp(flatPlayerDir, flatTargetDir, finalAssistStr).normalized;
            
            // Recombine with the player's raw vertical aim
            Vector3 finalAssistedDir = new Vector3(assistedFlatDir.x, playerDir.y, assistedFlatDir.z).normalized;

            // Apply throw multiplier
            Vector3 finalVelocity = finalAssistedDir * rawSpeed * throwPowerMultiplier;
            


            
            // Enforce minimum velocity
            if (finalVelocity.magnitude < reqMinVel)
            {
                if (finalVelocity.magnitude < 0.1f)
                {
                    finalVelocity = transform.forward * 1.5f; // Gentle toss forward if dropped
                }
                else
                {
                    // Add a small boost so it feels like a real throw instead of dropping like a rock
                    finalVelocity = finalVelocity.normalized * (finalVelocity.magnitude + 1.5f);
                }
                
                // Severely reduce aim assist for failed/weak throws so they don't magically curve
                _effectiveAimAssist = isWithinCone ? Mathf.Min(baseAssist, 0.15f) : 0f;
            }
            else
            {
                _effectiveAimAssist = isWithinCone ? baseAssist : 0f;

                // DISTANCE / SPEED ASSIST: 
                // If the throw met the minimum speed, and the player aimed generally towards the target,
                // we give a gentle speed boost if it looks like the throw might fall short.
                float angleError = Vector3.Angle(playerDir, targetDir);
                if (angleError < 45f)
                {
                    float distToTarget = Vector3.Distance(transform.position, trueTargetPosition);
                    
                    // A simple empirical formula for how much speed is needed to reach a target X meters away
                    float idealSpeedToReach = distToTarget * 0.8f; 
                    
                    if (finalVelocity.magnitude < idealSpeedToReach)
                    {
                        // Blend their current speed upwards so it easily glides to the target
                        float boostedSpeed = Mathf.Lerp(finalVelocity.magnitude, idealSpeedToReach, 0.5f);
                        finalVelocity = finalVelocity.normalized * boostedSpeed;
                    }
                }
            }
            
            rb.linearVelocity = finalVelocity;
            
            float spinDirection = Vector3.Dot(bestVelocity, transform.right) > 0 ? 1f : -1f;
            _qTilt = transform.rotation;
            _spinRate = spinDirection * 720f;
            _spinAngle = 0f;
            rb.angularVelocity = Vector3.zero;

            SetState(FrisbeeState.Thrown);
            timeSinceThrown = 0f;
        }

        private void LateUpdate()
        {
            // Rock-solid lock: apply cached offset to hand every frame AFTER all physics
            // This runs last so physics simulation cannot interfere.
            if (_autoGrabLocked && _state == FrisbeeState.Grabbed)
            {
                Transform hand = autoGrabHelper.GetHandTracker();
                if (hand != null)
                {
                    transform.position = hand.TransformPoint(_cachedLocalOffset);
                    transform.rotation = hand.rotation * _cachedLocalRotOffset;
                }
            }
        }

        private void NotifyGrabbed()
        {
            // Abort the incoming travel loop if the player catches it early!
            isTravelingToTarget = false;
            
            bool isAutoGrab = autoGrabHelper != null && autoGrabHelper.enableAutoGrab && ArcRoll.UI.ArcRollMenuManager.IsAutoGrabMode;

            if (isAutoGrab)
            {
                // Snap to hand first so offset is calculated from the correct position
                autoGrabHelper.SnapObjectToHand(transform);

                // Make fully kinematic: WE own the position, physics has no say
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Cache the local offset from hand to object root ONCE
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

            if (frisbeeCollider != null) frisbeeCollider.isTrigger = false;
            SetState(FrisbeeState.Grabbed);
            
            interactionHelper.ResetVelocityTracking(transform.position);
            autoGrabHelper.ResetVelocityTracking(transform.position);
        }

        public void ShootToTarget(Vector3 catchPosition, Vector3 ultimateTargetPosition, Transform actualTargetTransform = null)
        {
            startTravelPosition = transform.position;
            targetDestination = catchPosition;
            trueTargetPosition = ultimateTargetPosition;
            
            float distance = Vector3.Distance(startTravelPosition, targetDestination);
            totalTravelTime = Mathf.Clamp(distance * 0.2f, 0.8f, 1.5f);
            travelProgress = 0f;

            rb.isKinematic = true;
            rb.useGravity = false;
            
            isTravelingToTarget = true;
            SetState(FrisbeeState.TravelingToTarget);
        }

        private void OnCollisionEnter(Collision col)
        {
            if (_state != FrisbeeState.Thrown && _state != FrisbeeState.Missed) return;
            if (!col.gameObject.CompareTag(floorTag)) return;

            if (!hasTouchedFloor)
            {
                hasTouchedFloor = true;
            }
        }

        private void TriggerDespawn(float delay = 0f)
        {
            if (_state == FrisbeeState.Dead) return;

            if (!hasScored && _state != FrisbeeState.Missed && ArcRoll.Core.ArcRollScoreManager.Instance != null)
            {
                ArcRoll.Core.ArcRollScoreManager.Instance.RecordError();
            }

            SetState(FrisbeeState.Dead);
            StartCoroutine(ShrinkAndDestroyRoutine(delay));
        }

        private System.Collections.IEnumerator ShrinkAndDestroyRoutine(float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);

            if (frisbeeCollider != null) frisbeeCollider.enabled = false;
            rb.isKinematic = true;

            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / duration);
                yield return null;
            }

            Destroy(gameObject);
        }

        public void SetState(FrisbeeState newState)
        {
            if (_state == newState) return;
            _state = newState;

            OnStateChanged?.Invoke(this, _state);
        }
    }
}
