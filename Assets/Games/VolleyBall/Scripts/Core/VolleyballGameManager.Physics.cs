using UnityEngine;
using Rehab.Volleyball.Mechanics;

namespace Rehab.Volleyball.Core
{
    public partial class VolleyballGameManager
    {
        // ═══════════════════════════════════════════════════════
        // PHYSICS MATH
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Validates that a ball launched from 'from' to 'to' in 'flightTime' seconds
        /// will physically clear the top of the net. Uses the same drag physics as VolleyballBall.
        /// Returns true if the shot is safe, false if it would clip or go under the net.
        /// </summary>
        public bool WillClearNet(Vector3 from, Vector3 to, float flightTime, float gravityOverride)
        {
            if (netTransform == null || activeBall == null) return true;

            float netZ = netTransform.position.z;
            float netTopY = 2.24f; // Standard volleyball net height
            Collider netCol = netTransform.GetComponentInChildren<Collider>();
            if (netCol != null) netTopY = netCol.bounds.max.y;

            float g  = gravityOverride;
            float k  = activeBall.AirResistance;
            float dt = Time.fixedDeltaTime;

            Vector3 displacement = to - from;
            Vector3 vel;
            if (k < 0.001f)
            {
                vel.x = displacement.x / flightTime;
                vel.y = (displacement.y + 0.5f * g * flightTime * flightTime) / flightTime;
                vel.z = displacement.z / flightTime;
            }
            else
            {
                float e_kT = Mathf.Exp(-k * flightTime);
                float term = (1f - e_kT) / k;
                vel.x = displacement.x / term;
                vel.z = displacement.z / term;
                vel.y = (displacement.y + (g / k) * flightTime - (g / k) * term) / term;
            }

            Vector3 simPos = from;
            Vector3 simVel = vel;
            float maxTime = flightTime + 1.0f;
            float t = 0f;

            while (t < maxTime)
            {
                Vector3 prevPos = simPos;
                Vector3 accel = (Vector3.down * g) - (simVel * k);
                simVel += accel * dt;
                simPos += simVel * dt;
                t += dt;

                bool crossedNet = (prevPos.z < netZ && simPos.z >= netZ) ||
                                  (prevPos.z > netZ && simPos.z <= netZ);
                if (crossedNet)
                {
                    float fraction = (netZ - prevPos.z) / (simPos.z - prevPos.z);
                    float yAtNet = Mathf.Lerp(prevPos.y, simPos.y, fraction);

                    return yAtNet >= netTopY + 0.2f;
                }
            }
            return true;
        }
    }
}
