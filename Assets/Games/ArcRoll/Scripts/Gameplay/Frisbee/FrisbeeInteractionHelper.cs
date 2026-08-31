using UnityEngine;

namespace ArcRoll.Gameplay.Helpers
{
    /// <summary>
    /// Tracks hand movement to derive a clean throw velocity for Frisbee grabs.
    ///
    /// RESILIENCE NOTE: The Meta ISDK can throw DllNotFoundException: InteractionSdk on some
    /// devices/editor builds, preventing HandGrabInteractable.State from reaching "Select".
    /// This helper handles that gracefully via a 3-layer detection system:
    ///   Layer 1: ISDK State == "Select" (confirmed grab, best case)
    ///   Layer 2: Minimum grab frame guard (prevents immediate-release when ISDK is broken)
    ///   Layer 3: Proximity fallback (hand-to-ball distance) for release detection
    /// </summary>
    public class FrisbeeInteractionHelper
    {
        // ── Tracking Buffer ───────────────────────────────────────────────────
        private const int HistorySize = 20;
        private Vector3[] positionHistory = new Vector3[HistorySize];
        private float[] timeHistory = new float[HistorySize];
        private int historyIndex = 0;
        private int totalFramesRecorded = 0;

        // ── Peak Velocity Tracking ────────────────────────────────────────────
        private Vector3 peakVelocity = Vector3.zero;

        // ── ISDK Grab State ───────────────────────────────────────────────────
        private System.Collections.Generic.List<MonoBehaviour> interactables = new System.Collections.Generic.List<MonoBehaviour>();
        private Transform rawHandTracker; // Tracks the unlagged physical controller
        private Vector3 _lastKnownObjectPosition; // Tracks the actual object pos (for proximity fallback)

        // ── Release Guard State ───────────────────────────────────────────────
        private bool _isdkGrabWasConfirmed = false;
        private int _framesHeld = 0;
        private const int MinGrabFrames = 20; // ~0.28s at 72fps — prevents immediate release

        public void Initialize(System.Collections.Generic.List<MonoBehaviour> grabInteractables, Vector3 initialPosition)
        {
            this.interactables = grabInteractables;
        }

        public void SetInteractableEnabled(bool isEnabled)
        {
            if (interactables != null)
            {
                foreach (var interactable in interactables)
                {
                    if (interactable != null && interactable.enabled != isEnabled)
                    {
                        interactable.enabled = isEnabled;
                    }
                }
            }
        }

        /// <summary>
        /// Call this when the object enters the Grabbed state to reset tracking.
        /// </summary>
        public void ResetVelocityTracking(Vector3 currentPosition)
        {
            // Find the true physical hand anchor to track, bypassing any ISDK object lag caused by HandGrabPoses.
            OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
            if (rig != null && rig.leftHandAnchor != null && rig.rightHandAnchor != null)
            {
                float leftDist = Vector3.Distance(rig.leftHandAnchor.position, currentPosition);
                float rightDist = Vector3.Distance(rig.rightHandAnchor.position, currentPosition);
                rawHandTracker = leftDist < rightDist ? rig.leftHandAnchor : rig.rightHandAnchor;
            }

            peakVelocity = Vector3.zero;
            _lastKnownObjectPosition = currentPosition;
            _isdkGrabWasConfirmed = false;
            _framesHeld = 0;
            historyIndex = 0;
            totalFramesRecorded = 0;
            for (int i = 0; i < HistorySize; i++)
            {
                // If we found the physical hand, seed the history with its position instead of the object's
                positionHistory[i] = rawHandTracker != null ? rawHandTracker.position : currentPosition;
                timeHistory[i] = Time.time;
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Call every Update when the frisbee is Grabbed.
        /// NOTE: This ALWAYS records — it does NOT gate on HasUserGrabbedBall().
        /// Frisbee.cs only calls this when already in Grabbed state, so that guard is redundant
        /// and harmful when ISDK State is broken (DllNotFoundException scenario).
        /// </summary>
        public void RecordVelocity(Vector3 frisbeePosition, float time)
        {
            // Always track the object's real world position for proximity fallback
            _lastKnownObjectPosition = frisbeePosition;
            _framesHeld++;

            // Check if ISDK is confirming this grab (best case)
            if (!_isdkGrabWasConfirmed && interactables != null)
            {
                foreach (var interactable in interactables)
                {
                    try {
                        if (interactable.GetType().GetProperty("State")?.GetValue(interactable)?.ToString() == "Select")
                        {
                            _isdkGrabWasConfirmed = true;
                            break;
                        }
                    } catch { }
                }
            }

            // Use the unlagged physical hand position if we found it, otherwise fallback to object position
            Vector3 trackPos = rawHandTracker != null ? rawHandTracker.position : frisbeePosition;

            positionHistory[historyIndex] = trackPos;
            timeHistory[historyIndex] = time;

            // Calculate a stable velocity for this frame by comparing to 3 frames ago
            if (totalFramesRecorded >= 3)
            {
                int pastIndex = (historyIndex - 3 + HistorySize) % HistorySize;
                float dt = time - timeHistory[pastIndex];
                if (dt > 0.001f)
                {
                    Vector3 frameVel = (trackPos - positionHistory[pastIndex]) / dt;
                    // NOTE: No forward-direction filter here — frisbee is thrown SIDEWAYS, not forward!
                    // Filtering by player's forward direction would kill all valid frisbee throws.
                    if (frameVel.magnitude > peakVelocity.magnitude && frameVel.magnitude < 25f)
                    {
                        peakVelocity = frameVel;
                    }
                }
            }

            historyIndex = (historyIndex + 1) % HistorySize;
            totalFramesRecorded++;
        }

        public bool HasUserGrabbedBall()
        {
            if (interactables != null && interactables.Count > 0)
            {
                foreach (var interactable in interactables)
                {
                    try { if (interactable.GetType().GetProperty("State")?.GetValue(interactable)?.ToString() == "Select") return true; }
                    catch { }
                }
                return false;
            }
            return (rawHandTracker != null && _lastKnownObjectPosition != Vector3.zero) && Vector3.Distance(rawHandTracker.position, _lastKnownObjectPosition) < 0.20f;
        }

        public bool HasUserReleasedBall()
        {
            if (_framesHeld < MinGrabFrames) return false;

            if (_isdkGrabWasConfirmed && interactables != null)
            {
                foreach (var interactable in interactables)
                {
                    try { if (interactable.GetType().GetProperty("State")?.GetValue(interactable)?.ToString() == "Select") return false; }
                    catch { }
                }
                return true; 
            }

            if (rawHandTracker != null && _lastKnownObjectPosition != Vector3.zero)
                return Vector3.Distance(rawHandTracker.position, _lastKnownObjectPosition) > 0.35f;

            return true;
        }

        public string GetRawInteractableState()
        {
            if (interactables == null || interactables.Count == 0) return "NULL_INTERACTABLE";
            try
            {
                string stateStr = "";
                foreach (var interactable in interactables)
                {
                    System.Reflection.PropertyInfo stateProp = interactable.GetType().GetProperty("State");
                    if (stateProp != null) stateStr += stateProp.GetValue(interactable).ToString() + " ";
                }
                return stateStr.Trim();
            }
            catch (System.Exception e)
            {
                return $"EXCEPTION: {e.GetType().Name}";
            }
        }

        /// <summary>
        /// Returns the peak throw velocity — the fastest hand movement recorded during the swing.
        /// 
        /// DESIGN: This mirrors AutoGrabInteractable.GetAverageThrowVelocity() exactly.
        /// Using peak velocity (both speed AND direction from the best moment of the swing)
        /// gives far more consistent, natural-feeling throws than trying to compute a
        /// release-frame average, which is corrupted by finger-opening jitter from ISDK.
        /// </summary>
        public Vector3 GetAverageThrowVelocity()
        {
            return peakVelocity;
        }
    }
}
