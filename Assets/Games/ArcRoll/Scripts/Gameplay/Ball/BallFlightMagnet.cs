using UnityEngine;

namespace ArcRoll.Gameplay.Helpers
{
    public static class BallFlightMagnet
    {
        /// <summary>
        /// Applies a gentle magnetic acceleration toward the hoop if the ball is close and falling.
        /// Aligns horizontally first while the ball is high, then sucks it vertically downward 
        /// to ensure it enters the hoop cleanly without colliding with the rim.
        /// </summary>
        public static void ApplyMagneticPull(
            Rigidbody rb, 
            Vector3 trueTargetPosition, 
            float hoopHeightOffset, 
            float magneticPullStrength,
            float magneticPullRadius,
            float aimAssistConeAngle)
        {
            Vector3 hoopTarget = trueTargetPosition;
            // Target slightly ABOVE the rim so the magnet acts like a funnel dropping it in
            hoopTarget.y += hoopHeightOffset + 0.4f; 

            Vector3 ballPos = rb.position;
            Vector3 horizontalDifference = new Vector3(hoopTarget.x - ballPos.x, 0, hoopTarget.z - ballPos.z);
            float horizontalDistance = horizontalDifference.magnitude;
            
            float verticalDiff = ballPos.y - hoopTarget.y;
            Vector3 currentVel = rb.linearVelocity;
            float speed = currentVel.magnitude;

            if (speed > 0.05f)
            {
                // --- 1. Global Homing (The "Curve") ---
                // Gently bends the horizontal velocity toward the live hoop from ANY distance.
                // This creates the smooth curving tracking shot the user requested for moving targets.
                Vector3 flatVel = new Vector3(currentVel.x, 0, currentVel.z);
                float flatSpeed = flatVel.magnitude;
                
                if (flatSpeed > 0.1f)
                {
                    Vector3 desiredFlatDir = horizontalDifference.normalized;
                    float angleError = Vector3.Angle(flatVel.normalized, desiredFlatDir);
                    
                    // Only apply the global curve if they threw within the assist cone + 20 degrees!
                    if (angleError <= aimAssistConeAngle + 20f)
                    {
                        // A gentle, constant pull proportional to the magnetic strength
                        float globalPull = Time.fixedDeltaTime * (magneticPullStrength * 0.15f); 
                        
                        Vector3 newFlatVel = Vector3.Lerp(flatVel, desiredFlatDir * flatSpeed, globalPull);
                        rb.linearVelocity = new Vector3(newFlatVel.x, currentVel.y, newFlatVel.z);
                    }
                }

                // Refresh current velocity after global homing
                currentVel = rb.linearVelocity;

                // --- 2. Terminal Funnel (The Drop-in) ---
                // Sucks the ball directly into the net once it gets close and starts falling
                if (horizontalDistance < magneticPullRadius && currentVel.y <= 1.0f)
                {
                    if (verticalDiff > -0.2f)
                    {
                        Vector3 desiredHorizontalDir = horizontalDifference.normalized;
                        
                        float pullFactor = (1f - (horizontalDistance / magneticPullRadius)) * (magneticPullStrength * 0.04f);
                        pullFactor = Mathf.Clamp01(pullFactor);

                        Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0, currentVel.z);
                        float horizontalSpeed = currentHorizontalVel.magnitude;
                        
                        Vector3 desiredHorizontalVel = desiredHorizontalDir * horizontalSpeed;
                        Vector3 blendedHorizontalVel = Vector3.Lerp(currentHorizontalVel, desiredHorizontalVel, pullFactor);

                        float blendedYVel = currentVel.y;
                        if (horizontalDistance < 0.45f) // Once the ball is directly above the hoop
                        {
                            blendedYVel = Mathf.Lerp(currentVel.y, -4.0f, pullFactor * 1.5f);
                        }

                        rb.linearVelocity = new Vector3(blendedHorizontalVel.x, blendedYVel, blendedHorizontalVel.z);
                    }
                }
            }
        }
    }
}
