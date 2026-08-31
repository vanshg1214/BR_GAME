using UnityEngine;
using System.Collections.Generic;
using PopstrikeVR.Data;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// Safely positions balloons and tasks in 3D world space relative to the player's head.
    /// It enforces physical limits (Azimuth, Elevation, Reach) dictated by the PatientProfileSO.
    /// </summary>
    public class WorkspaceMapper : MonoBehaviour
    {
        public static WorkspaceMapper Instance { get; private set; }

        [Tooltip("The central anchor for mapping, typically the VR Camera tracking the player's head.")]
        public Transform HeadOrigin;

        [Tooltip("The profile used to clamp the spawning angles to the patient's safe limits.")]
        public PatientProfileSO ActiveProfile;

        [Tooltip("Minimum distance (in meters) between balloon centers to prevent overlapping. Set to 0.08 for 6cm balloons with a 2cm gap.")]
        public float MinSafeDistance = 0.08f;

        private Vector3? cachedForward = null;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose += OnRecentered;
            }
        }

        private void OnDisable()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose -= OnRecentered;
            }
        }

        private void OnRecentered()
        {
            Debug.Log("[WorkspaceMapper] Headset recenter event received. Recalibrating workspace...");
            
            if (HeadOrigin != null)
            {
                Vector3 oldPos = HeadOrigin.position;
                float oldYaw = HeadOrigin.rotation.eulerAngles.y;
                StartCoroutine(DelayedRecalibrate(oldPos, oldYaw));
            }
        }

        private System.Collections.IEnumerator DelayedRecalibrate(Vector3 oldPos, float oldYaw)
        {
            yield return new WaitForSeconds(0.5f);
            
            if (HeadOrigin != null)
            {
                Vector3 posDelta = HeadOrigin.position - oldPos;
                float yawDelta = HeadOrigin.rotation.eulerAngles.y - oldYaw;
                
                if (PopstrikeVR.Core.PopstrikePooler.Instance != null)
                {
                    PopstrikeVR.Core.PopstrikePooler.Instance.ShiftActiveBalloons(posDelta, yawDelta, oldPos);
                }
            }

            RecalibrateForward();

            // Rotate Environment_Root to match the player's new recenter rotation
            GameObject envRoot = GameObject.Find("Environment_Root");
            if (envRoot != null && HeadOrigin != null)
            {
                Vector3 euler = HeadOrigin.rotation.eulerAngles;
                envRoot.transform.rotation = Quaternion.Euler(0, euler.y, 0);
                Debug.Log($"[WorkspaceMapper] Environment_Root auto-rotated to match recenter forward: {euler.y} degrees.");
            }
        }

        /// <summary>
        /// Call this to manually reset the forward direction (e.g., if the user recenters their headset).
        /// </summary>
        public void RecalibrateForward()
        {
            if (HeadOrigin != null)
            {
                Vector3 forwardLevel = Vector3.ProjectOnPlane(HeadOrigin.forward, Vector3.up).normalized;
                if (forwardLevel == Vector3.zero) forwardLevel = HeadOrigin.up; // Fallback if looking straight down
                cachedForward = forwardLevel;
                Debug.Log($"[WorkspaceMapper] Forward direction calibrated to: {cachedForward}");
            }
        }

        /// <summary>
        /// Converts the parsed spherical coordinates into absolute world positions, ensuring
        /// the layout spawns accurately within the patient's calibrated safety boundaries (Angles and Reach).
        /// Optionally takes a list of already spawned positions to perform collision avoidance.
        /// </summary>
        public Vector3 GetWorldPositionSafely(Vector3 sphericalCoords, PatientProfileSO profile, List<Vector3> existingPositions = null, bool relaxCollision = true, float depthOffset = 0f)
        {
            if (HeadOrigin == null)
            {
                Debug.LogError("[WorkspaceMapper] CRITICAL ERROR: HeadOrigin not assigned! Cannot map layout coordinates.");
                return Vector3.zero;
            }

            // Lock the forward direction on the very first spawn so the room doesn't drift
            if (cachedForward == null)
            {
                RecalibrateForward();
            }

            float safeRadius = profile != null ? profile.GetSafeRadius() : 0.8f;
            float rawAzimuth = sphericalCoords.x;
            float rawElevation = sphericalCoords.y;
            
            float finalAzimuth = rawAzimuth;
            float finalElevation = rawElevation;
            float finalDistance = safeRadius;

            // Define fallback profile limits if null
            float pMinAzim = profile != null ? profile.MinAzimuth : -60f;
            float pMaxAzim = profile != null ? profile.MaxAzimuth : 60f;
            float pMinElev = profile != null ? profile.MinElevation : -60f;
            float pMaxElev = profile != null ? profile.MaxElevation : 60f;

            // --- ASYMMETRIC ELEVATION REMAPPING ---
            // CSV canonical elevation is approximately -60 to +60.
            float normalizedElevation = rawElevation / 60.0f; // -1 to 1
            float elevPercentage = (normalizedElevation + 1.0f) / 2.0f; // 0 to 1
            
            // Cap the max elevation at 60 for absolute physical comfort, but allow MinElevation to dictate the floor
            float safeMaxElev = Mathf.Clamp(pMaxElev, -60f, 60f);
            float safeMinElev = Mathf.Clamp(pMinElev, -60f, safeMaxElev);
            
            finalElevation = Mathf.Lerp(safeMinElev, safeMaxElev, elevPercentage);
            
            // --- DYNAMIC ASYMMETRIC AZIMUTH REMAPPING ---
            // The CSV Canonical range is -60 to +60 (120 degrees wide).
            float normalizedAzimuth = rawAzimuth / 60.0f; // Range: -1.0 to 1.0
            float azimPercentage = (normalizedAzimuth + 1.0f) / 2.0f; // Range 0.0 to 1.0
            
            PopstrikeVR.Core.HandTrackingMode mode = PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null ? 
                PopstrikeVR.Core.PopstrikeLevelDirector.Instance.activeHandMode : PopstrikeVR.Core.HandTrackingMode.BothHands;

            if (mode == PopstrikeVR.Core.HandTrackingMode.BothHands)
            {
                // Full patient range: MinAzimuth to MaxAzimuth
                finalAzimuth = Mathf.Lerp(pMinAzim, pMaxAzim, azimPercentage);
            }
            else if (mode == PopstrikeVR.Core.HandTrackingMode.RightHandOnly)
            {
                // Right hand forces Abduction to the right (positive azimuth)
                // 33% chance to force an extreme edge stretch (MaxAzimuth - 5 to MaxAzimuth)
                if (UnityEngine.Random.value <= 0.33f)
                {
                    float stretchMin = Mathf.Max(0, pMaxAzim - 5f);
                    finalAzimuth = Mathf.Lerp(stretchMin, pMaxAzim, azimPercentage);
                }
                else
                {
                    // Regular zone: 0 to pMaxAzim - 5
                    float regularMax = Mathf.Max(0, pMaxAzim - 5f);
                    finalAzimuth = Mathf.Lerp(0, regularMax, azimPercentage);
                }
            }
            else if (mode == PopstrikeVR.Core.HandTrackingMode.LeftHandOnly)
            {
                // Left hand forces Abduction to the left (negative azimuth)
                // 33% chance to force an extreme edge stretch (MinAzimuth to MinAzimuth + 5)
                if (UnityEngine.Random.value <= 0.33f)
                {
                    float stretchMax = Mathf.Min(0, pMinAzim + 5f);
                    finalAzimuth = Mathf.Lerp(pMinAzim, stretchMax, azimPercentage);
                }
                else
                {
                    // Regular zone: pMinAzim + 5 to 0
                    float regularMin = Mathf.Min(0, pMinAzim + 5f);
                    finalAzimuth = Mathf.Lerp(regularMin, 0, azimPercentage);
                }
            }
            
            // Final safety clamp just in case
            finalAzimuth = Mathf.Clamp(finalAzimuth, pMinAzim, pMaxAzim);
            finalElevation = Mathf.Clamp(finalElevation, safeMinElev, safeMaxElev);

            // Step 2: Convert to Flat Cartesian (Local Space relative to Head Origin)
            // The user explicitly requested to completely strip out the 3rd axis (Depth).
            // We use Sin to map angles to X and Y, and lock Z to a constant safeRadius.
            float azRad = finalAzimuth * Mathf.Deg2Rad;
            float elRad = finalElevation * Mathf.Deg2Rad;

            float localX = safeRadius * Mathf.Sin(azRad);
            float localY = safeRadius * Mathf.Sin(elRad);
            float localZ = safeRadius - depthOffset; // STRICTLY CONSTANT DEPTH (2-AXIS ONLY), minus depthOffset

            Vector3 localPos = new Vector3(localX, localY, localZ);

            // Step 3: Convert to World Space (Pivoted from the Shoulder)
            Quaternion yawRotation = Quaternion.LookRotation(cachedForward.Value, Vector3.up);
            
            // The user's arm pivots from the shoulder, which is lower and slightly behind the eyes.
            // Shifting the origin down 20cm and back 15cm ensures the reach radius is comfortable and accurate.
            Vector3 shoulderOffset = new Vector3(0f, -0.20f, -0.15f);
            Vector3 shoulderOrigin = HeadOrigin.position + (yawRotation * shoulderOffset);
            
            Vector3 worldPos = shoulderOrigin + (yawRotation * localPos);

            // --- COLLISION AVOIDANCE (2D RELAXATION) ---
            // Ensure this balloon does not physically overlap with any already spawned balloons.
            // Uses MinSafeDistance to define how closely balloons can sit together.
            if (relaxCollision && existingPositions != null)
            {
                int maxIterations = 50;
                for (int iter = 0; iter < maxIterations; iter++)
                {
                    bool overlapFound = false;
                    for (int i = 0; i < existingPositions.Count; i++)
                    {
                        float dist = Vector3.Distance(worldPos, existingPositions[i]);
                        if (dist < MinSafeDistance)
                        {
                            overlapFound = true;
                            
                            // Calculate push direction in world space
                            Vector3 dir = (worldPos - existingPositions[i]).normalized;
                            if (dir == Vector3.zero) dir = yawRotation * Vector3.right;
                            
                            // Convert direction to local space so we can ZERO OUT the Z axis push
                            Vector3 localDir = Quaternion.Inverse(yawRotation) * dir;
                            localDir.z = 0f; // NO DEPTH PUSHING!
                            
                            // Convert back to world space
                            dir = (yawRotation * localDir).normalized;
                            
                            float overlap = MinSafeDistance - dist;
                            Vector3 correction = dir * (overlap + 0.01f); // Push slightly past safe distance
                            
                            worldPos += correction;
                        }
                    }
                    if (!overlapFound) break; // All clear!
                }
            }

            return worldPos;
        }
    }
}
