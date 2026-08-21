using UnityEngine;

namespace ArcRoll.Gameplay.Helpers
{
    /// <summary>
    /// Attach this to any interactable (Ball/Frisbee).
    /// Handles grabbing and throwing based purely on proximity and hand velocity.
    /// Exposes settings in the Inspector for easy tweaking.
    /// </summary>
    public class AutoGrabInteractable : MonoBehaviour
    {
        [Header("Master Switch")]
        [Tooltip("If checked, Auto-Grab is enabled for this object (as long as the global menu setting is also on). If unchecked, it falls back to professional manual grabbing.")]
        public bool enableAutoGrab = true;

        [Header("Auto-Grab Settings")]
        [Tooltip("Hand must get within this many meters to auto-grab")]
        public float grabDistance = 0.15f;
        
        [Tooltip("Assign a child Transform on the prefab that marks the exact grip point (where the palm should touch). If left empty, defaults to the object's center.")]
        public Transform snapPoint;

        [Header("Auto-Throw Settings")]
        [Tooltip("Hand must reach this speed (m/s) to count as a throw swing")]
        public float throwThresholdSpeed = 1.0f;
        
        [Tooltip("Velocity must drop to this percentage (0.0 to 1.0) of peak speed to trigger release. Higher means it releases easier.")]
        [Range(0.1f, 1.0f)]
        public float releaseDropRatio = 0.80f;
        
        [Tooltip("Minimum frames object must be held before it can be released")]
        public int minHoldFrames = 45;

        // ── Tracking Buffer ───────────────────────────────────────────────────
        private const int HistorySize = 20;
        private Vector3[] positionHistory = new Vector3[HistorySize];
        private float[] timeHistory = new float[HistorySize];
        private int historyIndex = 0;
        private int totalFramesRecorded = 0;

        // ── State ─────────────────────────────────────────────────────────────
        private Transform rawHandTracker; 
        private Vector3 peakVelocity = Vector3.zero;
        private bool hasMetThrowThreshold = false;
        private int framesHeld = 0;

        public void Initialize(Vector3 initialPosition)
        {
            ResetVelocityTracking(initialPosition);
        }

        public bool CheckForAutoGrab(Vector3 objectPosition)
        {
            OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig != null && rig.leftHandAnchor != null && rig.rightHandAnchor != null)
            {
                string handMode = ArcRoll.UI.ArcRollMenuManager.HandMode;
                bool checkLeft = handMode == "Left" || handMode == "Both";
                bool checkRight = handMode == "Right" || handMode == "Both";

                float leftDist = checkLeft ? Vector3.Distance(rig.leftHandAnchor.position, objectPosition) : float.MaxValue;
                float rightDist = checkRight ? Vector3.Distance(rig.rightHandAnchor.position, objectPosition) : float.MaxValue;
                
                if (leftDist <= grabDistance || rightDist <= grabDistance)
                {
                    rawHandTracker = leftDist < rightDist ? rig.leftHandAnchor : rig.rightHandAnchor;
                    return true;
                }
            }
            return false;
        }

        public void ResetVelocityTracking(Vector3 currentPosition)
        {
            if (rawHandTracker == null)
            {
                OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
                if (rig != null && rig.leftHandAnchor != null && rig.rightHandAnchor != null)
                {
                    string handMode = ArcRoll.UI.ArcRollMenuManager.HandMode;
                    bool checkLeft = handMode == "Left" || handMode == "Both";
                    bool checkRight = handMode == "Right" || handMode == "Both";

                    float leftDist = checkLeft ? Vector3.Distance(rig.leftHandAnchor.position, currentPosition) : float.MaxValue;
                    float rightDist = checkRight ? Vector3.Distance(rig.rightHandAnchor.position, currentPosition) : float.MaxValue;
                    rawHandTracker = leftDist < rightDist ? rig.leftHandAnchor : rig.rightHandAnchor;
                }
            }

            peakVelocity = Vector3.zero;
            hasMetThrowThreshold = false;
            framesHeld = 0;
            historyIndex = 0;
            totalFramesRecorded = 0;

            for (int i = 0; i < HistorySize; i++)
            {
                positionHistory[i] = rawHandTracker != null ? rawHandTracker.position : currentPosition;
                timeHistory[i] = Time.time;
            }
        }

        public void RecordVelocity(Vector3 objectPosition, float time)
        {
            framesHeld++;

            Vector3 trackPos = rawHandTracker != null ? rawHandTracker.position : objectPosition;

            positionHistory[historyIndex] = trackPos;
            timeHistory[historyIndex] = time;
            
            if (totalFramesRecorded >= 3)
            {
                int pastIndex = (historyIndex - 3 + HistorySize) % HistorySize;
                float dt = time - timeHistory[pastIndex];
                if (dt > 0.001f)
                {
                    Vector3 frameVel = (trackPos - positionHistory[pastIndex]) / dt;
                    
                    // Ensure we only count FORWARD swings (ignore backward wind-ups)
                    bool isMovingForward = true;
                    OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
                    if (rig != null && rig.centerEyeAnchor != null)
                    {
                        // Dot product > -0.2f gives a very generous 100+ degree cone in front of the player.
                        // Anything behind them (like winding up) is ignored.
                        isMovingForward = Vector3.Dot(frameVel.normalized, rig.centerEyeAnchor.forward) > -0.2f;
                    }

                    if (isMovingForward)
                    {
                        if (frameVel.magnitude > peakVelocity.magnitude && frameVel.magnitude < 25f)
                        {
                            peakVelocity = frameVel;
                        }

                        if (!hasMetThrowThreshold && frameVel.magnitude >= throwThresholdSpeed)
                        {
                            hasMetThrowThreshold = true;
                        }
                    }
                    else
                    {
                        // Reset if they are winding up so an old forward peak doesn't accidentally trigger a drop
                        peakVelocity = Vector3.zero;
                        hasMetThrowThreshold = false;
                    }
                }
            }

            historyIndex = (historyIndex + 1) % HistorySize;
            totalFramesRecorded++;
        }

        public void SnapObjectToHand(Transform objectTransform)
        {
            if (rawHandTracker == null) return;

            if (snapPoint != null)
            {
                // 1. ALIGN ROTATION FIRST! 
                // We MUST calculate the local rotation difference, not world space.
                // World space would cause it to snap randomly depending on how it was spinning!
                Quaternion localRotOffset = Quaternion.Inverse(snapPoint.rotation) * objectTransform.rotation;
                objectTransform.rotation = rawHandTracker.rotation * localRotOffset;

                // 2. ALIGN POSITION SECOND!
                // Now that rotation is locked, we can safely calculate the offset and move the parent
                // so the child snap point lands exactly on the palm.
                Vector3 snapOffset = objectTransform.position - snapPoint.position;
                objectTransform.position = rawHandTracker.position + snapOffset;
            }
            else
            {
                // Fallback: snap the object center directly to the hand
                objectTransform.position = rawHandTracker.position;
                objectTransform.rotation = rawHandTracker.rotation;
            }
        }

        public Transform GetHandTracker()
        {
            return rawHandTracker;
        }

        public bool HasUserReleasedBall(Vector3 objectPosition)
        {
            if (framesHeld < minHoldFrames) return false;
            if (rawHandTracker == null) return false;
            if (!hasMetThrowThreshold) return false;

            if (totalFramesRecorded >= 3)
            {
                int pastIndex = (historyIndex - 3 + HistorySize) % HistorySize;
                float dt = Time.time - timeHistory[pastIndex];
                if (dt > 0.001f)
                {
                    Vector3 frameVel = (rawHandTracker.position - positionHistory[pastIndex]) / dt;
                    
                    if (frameVel.magnitude <= peakVelocity.magnitude * releaseDropRatio)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public Vector3 GetAverageThrowVelocity()
        {
            return peakVelocity;
        }

        private void OnDrawGizmosSelected()
        {
            // This allows you to visually see the exact size of the Auto-Grab sphere in the Scene view!
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Cyan
            Gizmos.DrawWireSphere(transform.position, grabDistance);
        }
    }
}
