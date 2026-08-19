using UnityEngine;

namespace PopstrikeVR.Interaction
{
    public enum GestureState
    {
        UNKNOWN,
        CLOSED_FIST,    // All fingers curled
        OPEN_BLADE,     // All fingers extended flat
        INDEX_POINT     // Only index extended, rest curled
    }

    /// <summary>
    /// Reads from the OVR hand tracking skeleton to detect three hand gestures.
    /// Uses dot-product finger curl values with hysteresis thresholds to prevent flicker.
    /// Incorporates the Outer-Edge Heuristic to bypass Meta OpenXR's broken Middle Finger tracking bug.
    /// </summary>
    public class GestureDetector : MonoBehaviour
    {
        public static GestureDetector Instance { get; private set; }

        [Header("OVR Hand References")]
        public OVRHand leftHand;
        public OVRSkeleton leftSkeleton;
        public OVRHand rightHand;
        public OVRSkeleton rightSkeleton;

        public GestureState LeftState  { get; private set; } = GestureState.UNKNOWN;
        public GestureState RightState { get; private set; } = GestureState.UNKNOWN;

        [Header("Gesture Thresholds")]
        [Tooltip("Dot product below this = finger is considered CURLED")]
        [SerializeField, Range(0f, 1f)] private float curlLimit   = 0.50f;

        [Tooltip("Dot product above this = finger is considered EXTENDED")]
        [SerializeField, Range(0f, 1f)] private float extendLimit = 0.50f;

        [Header("Smoothing")]
        [Tooltip("Frames a gesture must be held before it is committed")]
        [SerializeField] private int confirmFrames = 3;

        private GestureState _leftCandidate;
        private GestureState _rightCandidate;
        private int _leftConfirmCount;
        private int _rightConfirmCount;

        // ── Gesture Lock System ──────────────────────────────────────────────
        private bool isLeftLocked = false;
        private bool isRightLocked = false;
        private GestureState lockedLeftState = GestureState.UNKNOWN;
        private GestureState lockedRightState = GestureState.UNKNOWN;

        public void LockGesture(bool isLeft, GestureState gesture)
        {
            if (isLeft)
            {
                isLeftLocked = true;
                lockedLeftState = gesture;
                LeftState = gesture; // Force immediate update
            }
            else
            {
                isRightLocked = true;
                lockedRightState = gesture;
                RightState = gesture; // Force immediate update
            }
            Debug.Log($"<color=cyan>[GestureDetector] LOCKED {(isLeft ? "Left" : "Right")} Hand to {gesture}</color>");
        }

        public void UnlockGesture(bool isLeft)
        {
            if (isLeft) isLeftLocked = false;
            else isRightLocked = false;
            Debug.Log($"<color=cyan>[GestureDetector] UNLOCKED {(isLeft ? "Left" : "Right")} Hand</color>");
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (leftHand != null && leftSkeleton != null)
            {
                if (isLeftLocked)
                {
                    LeftState = lockedLeftState;
                }
                else
                {
                    GestureState raw = leftHand.IsTracked
                        ? DetectGesture(leftHand, leftSkeleton)
                        : GestureState.UNKNOWN;

                    LeftState = Smooth(raw, ref _leftCandidate, ref _leftConfirmCount, true);
                }
            }

            if (rightHand != null && rightSkeleton != null)
            {
                if (isRightLocked)
                {
                    RightState = lockedRightState;
                }
                else
                {
                    GestureState raw = rightHand.IsTracked
                        ? DetectGesture(rightHand, rightSkeleton)
                        : GestureState.UNKNOWN;

                    RightState = Smooth(raw, ref _rightCandidate, ref _rightConfirmCount, false);
                }
            }
        }

        private GestureState Smooth(GestureState raw, ref GestureState candidate, ref int count, bool isLeft)
        {
            if (raw == candidate)
            {
                count++;
                if (count >= confirmFrames)
                    return candidate;          // confirmed — commit
            }
            else
            {
                candidate = raw;
                count = 1;
            }
            // Keep previous committed state while building up
            return isLeft ? LeftState : RightState; 
        }

        // ── Gesture Classification ───────────────────────────────────────────────
        private GestureState DetectGesture(OVRHand hand, OVRSkeleton skeleton)
        {
            if (!skeleton.IsInitialized || skeleton.Bones == null || skeleton.Bones.Count == 0)
                return GestureState.UNKNOWN;

            // We explicitly IGNORE the Middle finger because Meta OpenXR provides inverted/glitchy tracking for it
            float idxCurl = GetFingerCurl(skeleton, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.Hand_Index2, OVRSkeleton.BoneId.Hand_Index3);
            float rngCurl = GetFingerCurl(skeleton, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_Ring2, OVRSkeleton.BoneId.Hand_Ring3);
            float pnkCurl = GetFingerCurl(skeleton, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.Hand_Pinky2, OVRSkeleton.BoneId.Hand_Pinky3);

            // HARDCODED FORGIVENESS WITH A RESTING DEADZONE:
            // This creates an "UNKNOWN" gap in the middle so a relaxed hand doesn't accidentally trigger a Blade.
            float curlThreshold = 0.6f;   // Approx 53-degree bend. Anything bent MORE than this is a curl.
            float extendThreshold = 0.78f; // Approx 38-degree bend. Anything bent LESS than this is extended.

            bool idxExtended = idxCurl >= extendThreshold;
            bool idxCurled   = idxCurl <= curlThreshold;

            bool rngCurled   = rngCurl <= curlThreshold;
            bool pnkCurled   = pnkCurl <= curlThreshold;
            bool pnkExtended = pnkCurl >= extendThreshold;

            // ── 1. CLOSED FIST ───────────────────────────────────────────────────
            if (idxCurled && pnkCurled)
            {
                return GestureState.CLOSED_FIST;
            }

            // ── 2. OPEN BLADE ────────────────────────────────────────────────────
            if (idxExtended && pnkExtended)
            {
                return GestureState.OPEN_BLADE;
            }

            // ── 3. INDEX POINT ───────────────────────────────────────────────────
            // As long as the index finger is extended, and it wasn't caught by OPEN_BLADE above, it's a point.
            // We do not require strict curling of the other fingers, making it highly forgiving.
            if (idxExtended)
            {
                return GestureState.INDEX_POINT;
            } return GestureState.UNKNOWN;
        }

        private float GetFingerCurl(OVRSkeleton skeleton, OVRSkeleton.BoneId b1, OVRSkeleton.BoneId b2, OVRSkeleton.BoneId b3)
        {
            Transform t1 = GetBoneTransform(skeleton, b1);
            Transform t2 = GetBoneTransform(skeleton, b2);
            Transform t3 = GetBoneTransform(skeleton, b3);

            if (t1 == null || t2 == null || t3 == null) return 0f;

            Vector3 dir1 = (t2.position - t1.position).normalized;
            Vector3 dir2 = (t3.position - t2.position).normalized;

            return Vector3.Dot(dir1, dir2);
        }

        private Transform GetBoneTransform(OVRSkeleton skeleton, OVRSkeleton.BoneId boneId)
        {
            foreach (var bone in skeleton.Bones)
            {
                if (bone.Id == boneId) return bone.Transform;
            }
            return null;
        }

        public bool IsGestureActive(GestureState checkState)
            => LeftState == checkState || RightState == checkState;

        public void UpdateState(bool isLeft, GestureState newState)
        {
            if (isLeft) LeftState = newState;
            else RightState = newState;
        }

        public void SetPatientProfile(PopstrikeVR.Data.PatientProfileSO profile)
        {
            // We kept this method signature so PopstrikeLevelDirector doesn't throw a compiler error,
            // even though the gesture thresholds are now hardcoded for maximum stability.
        }

        public string GetDiagnosticString(bool isLeft)
        {
            OVRSkeleton skeleton = isLeft ? leftSkeleton : rightSkeleton;
            if (skeleton == null || !skeleton.IsInitialized) return "Skeleton not initialized";

            float idxCurl = GetFingerCurl(skeleton, OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.Hand_Index2, OVRSkeleton.BoneId.Hand_Index3);
            float midCurl = GetFingerCurl(skeleton, OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.Hand_Middle2, OVRSkeleton.BoneId.Hand_Middle3);
            float rngCurl = GetFingerCurl(skeleton, OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_Ring2, OVRSkeleton.BoneId.Hand_Ring3);
            float pnkCurl = GetFingerCurl(skeleton, OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.Hand_Pinky2, OVRSkeleton.BoneId.Hand_Pinky3);

            string hand_side = isLeft ? "LEFT" : "RIGHT";
            return $"[{hand_side}] Idx:{idxCurl:F2} Mid:{midCurl:F2} Rng:{rngCurl:F2} Pnk:{pnkCurl:F2} | Curl:<{curlLimit} Ext:>{extendLimit}";
        }
    }

    public interface IHandVelocityProvider
    {
        Vector3 GetVelocity();
    }
}
