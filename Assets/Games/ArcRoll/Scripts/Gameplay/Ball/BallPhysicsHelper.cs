using UnityEngine;
using ArcRoll.Gameplay; // To access Ball.BallType

namespace ArcRoll.Gameplay.Helpers
{
    public static class BallPhysicsHelper
    {
        // ── Constants ──────────────────────────────────────────────────────────
        private const float Gravity = 9.81f;

        /// <summary>
        /// NEW APPROACH: "Guided Projectile" System
        ///
        /// Instead of blindly blending directions, we:
        ///   1. Use real projectile physics to compute the EXACT launch velocity to reach the hoop.
        ///   2. Preserve the player's real throw SPEED as the driving force — they control power.
        ///   3. Blend only the DIRECTION toward the mathematically perfect arc.
        ///   4. The blend strength fades naturally based on how far off their aim was.
        ///   5. This means the ball always follows a REAL gravity arc, never feels forced.
        /// </summary>
        public static Vector3 CalculateAssistedThrowVelocity(
            Vector3 rawVelocity,
            Vector3 ballPosition,
            Vector3 trueTargetPosition,
            Ball.BallType ballType,
            float aimAssistStrength,
            float aimAssistConeAngle,
            bool enableAutoLob,
            float hoopHeightOffset,
            float lobTimeOfFlight,
            float throwPowerMultiplier)
        {
            float rawSpeed = rawVelocity.magnitude * throwPowerMultiplier;
            Vector3 throwDir = rawVelocity.normalized;

            Vector3 flatThrowDir   = new Vector3(throwDir.x, 0, throwDir.z).normalized;
            Vector3 flatTargetDir  = new Vector3(trueTargetPosition.x - ballPosition.x, 0, trueTargetPosition.z - ballPosition.z).normalized;
            float horizontalError = Vector3.Angle(flatThrowDir, flatTargetDir);

            bool withinCone = horizontalError <= aimAssistConeAngle;

            if (ballType == Ball.BallType.Basketball && enableAutoLob)
            {
                // If the player throws the ball downwards (to bounce it on the floor), disable aim assist.
                // -0.2f is roughly 11.5 degrees below horizontal. 
                bool isSpikingToFloor = throwDir.y < -0.2f;

                if (withinCone && !isSpikingToFloor)
                {
                    // 1. Calculate a Professional Parabolic Arc
                    // The arc peak dynamically scales with distance to ensure it clears any obstacles gracefully!
                    Vector3 delta = trueTargetPosition - ballPosition;
                    Vector3 deltaHorizontal = new Vector3(delta.x, 0, delta.z);
                    float distance = deltaHorizontal.magnitude;
                    
                    // Peak height is proportional to throw distance, ensuring beautiful obstacle clearance.
                    float dynamicPeakH = Mathf.Clamp(distance * 0.35f, 1.0f, 4.0f);
                    if (hoopHeightOffset > 0) dynamicPeakH = Mathf.Max(dynamicPeakH, hoopHeightOffset);
                    
                    // Calculate required vertical velocity to reach the peak
                    float peakY = Mathf.Max(delta.y + 0.5f, dynamicPeakH);
                    float vV = Mathf.Sqrt(2f * Gravity * peakY);
                    float tUp = vV / Gravity;
                    
                    // Calculate required time to fall from peak to target
                    float yFall = peakY - delta.y;
                    float tDown = Mathf.Sqrt(2f * Mathf.Max(0.01f, yFall) / Gravity);
                    
                    float T = tUp + tDown; // Total time of flight
                    
                    // Horizontal velocity to travel the distance in time T
                    float vH = distance / T;
                    
                    Vector3 perfectVelocity = deltaHorizontal.normalized * vH + Vector3.up * vV;
                    float perfectSpeed = perfectVelocity.magnitude;

                    // 2. Zone-Based Speed Assist
                    // Check if the player's effort is within the "Success Window"
                    float ratio = rawSpeed / perfectSpeed;
                    float speedAssistBlend = 0f;

                    if (ratio >= 0.70f && ratio <= 1.40f)
                    {
                        // Player threw with good power, snap to perfect speed
                        speedAssistBlend = 1.0f; 
                    }
                    else if (ratio > 0.5f && ratio < 0.70f)
                    {
                        // Weak throw, fade assist so it drops short naturally
                        speedAssistBlend = Mathf.InverseLerp(0.5f, 0.70f, ratio); 
                    }
                    else if (ratio > 1.40f && ratio < 1.6f)
                    {
                        // Overthrow, fade assist so it flies past the hoop
                        speedAssistBlend = Mathf.InverseLerp(1.6f, 1.40f, ratio); 
                    }

                    // Apply global strength slider
                    speedAssistBlend *= aimAssistStrength;
                    float finalSpeed = Mathf.Lerp(rawSpeed, perfectSpeed, speedAssistBlend);

                    // 3. Zone-Based Direction Assist
                    float dirAssistBlend = 0f;
                    if (horizontalError <= 20f)
                    {
                        dirAssistBlend = 1.0f; // Perfect lock-on for any reasonably accurate throw
                    }
                    else if (horizontalError <= aimAssistConeAngle)
                    {
                        dirAssistBlend = Mathf.InverseLerp(aimAssistConeAngle, 20f, horizontalError);
                    }
                    
                    dirAssistBlend *= aimAssistStrength;
                    Vector3 finalDirection = Vector3.Lerp(throwDir, perfectVelocity.normalized, dirAssistBlend).normalized;
                    
                    return finalDirection * finalSpeed;
                }
                else
                {
                    // Way off target -> raw throw
                    return rawVelocity * throwPowerMultiplier;
                }
            }
            else if (ballType == Ball.BallType.BowlingBall)
            {
                // ── BOWLING BALL ──
                // Add a flat 1.6x speed boost to offset floor friction and VR throw limitations
                float boostedRawSpeed = rawSpeed * 1.6f;
                
                // If aimAssistStrength is 0, honour the raw throw completely — no clamping, no steering!
                // If aimAssist is ON, ensure a minimum speed (4.0f) so it reaches the pins, but ALLOW UNLIMITED TOP SPEED!
                float safeSpeed = aimAssistStrength > 0f
                    ? Mathf.Max(boostedRawSpeed, 4.0f)   // No artificial ceiling! Throw as hard as you want.
                    : boostedRawSpeed;                   // pure raw speed when assist is off

                if (withinCone && aimAssistStrength > 0f)
                {
                    Vector3 flatTarget = (trueTargetPosition - ballPosition);
                    flatTarget.y = 0f;
                    
                    // The better they aim, the stronger the assist (no hidden 0.6f multiplier!)
                    float assistBlend = aimAssistStrength;
                    if (horizontalError > 15f)
                    {
                        assistBlend *= (1f - (horizontalError - 15f) / (aimAssistConeAngle - 15f));
                    }
                    
                    Vector3 blendedDir = Vector3.Lerp(flatThrowDir, flatTarget.normalized, assistBlend).normalized;
                    Vector3 finalV = blendedDir * safeSpeed;
                    
                    // Kill upward momentum so it rolls
                    if (finalV.y > 0) finalV.y *= 0.1f;
                    finalV.y -= 4.0f;
                    return finalV;
                }
                else
                {
                    // No assist — pure raw throw direction
                    Vector3 finalV = throwDir * safeSpeed;
                    if (finalV.y > 0) finalV.y *= 0.1f;
                    finalV.y -= 4.0f;
                    return finalV;
                }
            }

            // Fallback
            return rawVelocity * throwPowerMultiplier;
        }
    }
}
