using UnityEngine;

namespace PopstrikeVR.Interaction
{
    /// <summary>
    /// Attached dynamically to the tiny bone colliders on the hand (Index tip, Palm, etc.).
    /// When a balloon is hit by this specific bone collider, it requests velocity.
    /// This script simply forwards that request up to the main MetaHandIntegrator.
    /// </summary>
    public class HandColliderForwarder : MonoBehaviour, IHandVelocityProvider
    {
        public MetaHandIntegrator VelocityProvider;

        public Vector3 GetVelocity()
        {
            if (VelocityProvider != null)
            {
                return VelocityProvider.GetVelocity();
            }
            return Vector3.zero;
        }
    }
}
