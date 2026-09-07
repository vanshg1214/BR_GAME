using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Implement on any GameObject that should respond to the player's hand collisions.
    /// Keeps hit-response logic fully decoupled from the hand-tracking input layer.
    /// </summary>
    public interface IHittable
    {
        /// <param name="velocity">Hand velocity vector at the moment of impact.</param>
        /// <param name="hitPosition">World-space contact point.</param>
        void OnHit(Vector3 velocity, Vector3 hitPosition);
    }
}
