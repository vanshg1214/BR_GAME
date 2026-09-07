using UnityEngine;
using Rehab.Volleyball.Data;

namespace Rehab.Volleyball.Core
{
    public partial class VolleyballGameManager
    {
        // ═══════════════════════════════════════════════════════
        // TARGETING & UTILITY
        // ═══════════════════════════════════════════════════════

        public Vector3 GetOpponentTargetPosition()
        {
            return opponentAI != null ? opponentAI.transform.position : Vector3.zero;
        }

        private bool IsPointInCourtBounds(Collider court, Vector3 point)
        {
            if (court == null) return false;
            Bounds b = court.bounds;
            b.Expand(0.3f); // Pro line-call: if any part of ball touches the line, it's IN
            return point.x >= b.min.x && point.x <= b.max.x &&
                   point.z >= b.min.z && point.z <= b.max.z;
        }

        /// <summary>
        /// Calculates adaptive target position based on patient ROM.
        /// </summary>
        public Vector3 GetPlayerTargetPosition()
        {
            Transform currentHead = headTransform != null ? headTransform : (Camera.main != null ? Camera.main.transform : playerTransform);
            if (currentHead == null) return Vector3.zero;

            Vector3 target = currentHead.position;

            float armLength = 0.6f;
            float maxFlexion = 120f;
            float maxAbduction = 90f;
            float safetyMargin = 0.15f;
            bool isLeftOnly = false;
            bool isRightOnly = false;

            if (rehabProfile != null)
            {
                armLength = rehabProfile.armLength;
                maxFlexion = rehabProfile.maxFlexion;
                maxAbduction = rehabProfile.maxAbduction;
                safetyMargin = rehabProfile.safetyMargin;
                
                // If we are using the Menu, override the rehab profile's hand mode constraint!
                isLeftOnly = serveSide == VolleyballRehabProfileSO.HandMode.Left;
                isRightOnly = serveSide == VolleyballRehabProfileSO.HandMode.Right;
            }

            // --- 0. Procedural JSON Targeting with Safety Clamping ---
            if (VolleyballLevelDirector.Instance != null && VolleyballLevelDirector.Instance.IsLevelRunning)
            {
                var levelConfig = VolleyballLevelDirector.Instance.CurrentLevelConfig;
                if (levelConfig != null && levelConfig.targetSpawnChances != null)
                {
                    TargetSpawnChance chances = levelConfig.targetSpawnChances;
                    
                    // --- Generate Azimuth ---
                    float roll = Random.value;
                    float targetAzimuth = 0f;
                    float sumAz = chances.azimuth0to30 + chances.azimuth30to60 + chances.azimuth60to90;
                    float pAz_1 = chances.azimuth0to30 / sumAz;
                    float pAz_2 = chances.azimuth30to60 / sumAz;
                    
                    if (roll <= pAz_1) targetAzimuth = Random.Range(0f, 30f);
                    else if (roll <= pAz_1 + pAz_2) targetAzimuth = Random.Range(30f, 60f);
                    else targetAzimuth = Random.Range(60f, 90f);
                    
                    // Determine Left/Right Side based on Hand Mode
                    if (isLeftOnly) 
                    {
                        targetAzimuth *= -1f; // Force Left
                    }
                    else if (!isRightOnly && Random.value > 0.5f) 
                    {
                        targetAzimuth *= -1f; // 50/50 split for Both Hands
                    }
                    // If isRightOnly is true, we leave it positive (Right).

                    // --- Generate Elevation ---
                    roll = Random.value;
                    float targetElevation = 0f;
                    float sumEl = chances.elevationNeg45toNeg15 + chances.elevationNeg15to15 + chances.elevation15to45;
                    float pEl_1 = chances.elevationNeg45toNeg15 / sumEl;
                    float pEl_2 = chances.elevationNeg15to15 / sumEl;
                    
                    if (roll <= pEl_1) targetElevation = Random.Range(-45f, -15f);
                    else if (roll <= pEl_1 + pEl_2) targetElevation = Random.Range(-15f, 15f);
                    else targetElevation = Random.Range(15f, 45f);

                    // --- Generate Distance ---
                    roll = Random.value;
                    float targetDistance = 0f;
                    float sumDist = chances.distance0to0_2 + chances.distance0_2to0_4 + chances.distance0_4to0_6;
                    float pDist_1 = chances.distance0to0_2 / sumDist;
                    float pDist_2 = chances.distance0_2to0_4 / sumDist;
                    
                    if (roll <= pDist_1) targetDistance = Random.Range(0f, 0.2f);
                    else if (roll <= pDist_1 + pDist_2) targetDistance = Random.Range(0.2f, 0.4f);
                    else targetDistance = Random.Range(0.4f, 0.6f);

                    // Convert Spherical to Cartesian manually
                    // Distance from JSON is a fraction of arm length (0.0 = at shoulder, 1.0 = full reach)
                    float worldDistance = targetDistance * armLength;
                    float azRad = targetAzimuth * Mathf.Deg2Rad;
                    float elRad = targetElevation * Mathf.Deg2Rad;
                    
                    float x = worldDistance * Mathf.Sin(azRad) * Mathf.Cos(elRad);
                    float y = worldDistance * Mathf.Sin(elRad);
                    float z = worldDistance * Mathf.Cos(azRad) * Mathf.Cos(elRad); 

                    Vector3 cartesianOffset = new Vector3(x, y, z);
                    
                    // ── ROM Safety Clamp ──────────────────────────────────────────────────
                    // Keeps EVERY AI-thrown ball strictly inside the patient's comfortable ROM.
                    float safeMultiplier    = 1.0f - safetyMargin;
                    float safeMaxHorizontal = armLength * Mathf.Clamp01(maxAbduction / 90f) * safeMultiplier;
                    float safeMaxVertical   = armLength * Mathf.Clamp01((maxFlexion - 90f) / 90f) * safeMultiplier;
                    float safeMaxForward    = armLength * Mathf.Clamp01(maxFlexion / 90f) * safeMultiplier;

                    // Block the side that the unselected hand is on
                    float clampMinX = isRightOnly ? 0f : -safeMaxHorizontal;
                    float clampMaxX = isLeftOnly  ? 0f :  safeMaxHorizontal;
                    
                    cartesianOffset.x = Mathf.Clamp(cartesianOffset.x, clampMinX, clampMaxX);
                    // Allow reaching ~30% below eye level for low shots, cap upward by the flexion ROM
                    cartesianOffset.y = Mathf.Clamp(cartesianOffset.y, -armLength * 0.3f, safeMaxVertical);
                    // Minimum 0.2m forward so the ball never lands behind/at the player's feet
                    cartesianOffset.z = Mathf.Clamp(cartesianOffset.z, 0.2f, safeMaxForward);
                    // ─────────────────────────────────────────────────────────────────────
                    
                    // Store offset so the landing visualizer can recompute position against LIVE headset each frame
                    LastCSVTargetOffset = cartesianOffset;
                    
                    target += cartesianOffset;
                    
                    Debug.Log($"[GameManager] JSON Target: Az={targetAzimuth:F1}° El={targetElevation:F1}° Dist={worldDistance:F2}m -> World={target}");

                    return target;
                }
            }

            // --- 1. Basic Fallback (If CSV is missing or row has no target) ---
            // Simply target the player's general chest/neck area so the game doesn't crash.
            target.y -= 0.2f; 
            Debug.Log($"[GameManager] No CSV Target found. Using basic fallback: {target}");

            return target;
        }
    }
}
