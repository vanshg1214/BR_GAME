using System.Collections.Generic;
using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// A bulletproof physics utility that acts as a "Virtual Physics Layer".
    /// It permanently isolates the player's physical body from game objects (tables, moles, flying debris)
    /// without modifying Unity's actual LayerMatrix, which preserves Camera Culling Masks and Gaze Interactors.
    /// </summary>
    public static class CollisionIsolator
    {
        private static HashSet<Collider> playerColliders = new HashSet<Collider>();
        private static HashSet<Collider> hammerColliders = new HashSet<Collider>(); // Tracked separately so they can still hit Moles!

        /// <summary>
        /// Registers every single collider on the player's VR rig so they can be shielded from the game world.
        /// </summary>
        public static void RegisterPlayer(Transform playerRoot)
        {
            if (playerRoot == null) return;

            // Collect every active and inactive collider on the player
            Collider[] colliders = playerRoot.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in colliders)
            {
                if (col != null)
                {
                    // Detect if this collider belongs to the Hammer or Hands
                    bool isHammerOrHand = col.GetComponent<HandHammer>() != null || col.GetComponentInParent<HandHammer>() != null ||
                                          col.GetComponent<MouseHammer>() != null || col.GetComponentInParent<MouseHammer>() != null;

                    if (isHammerOrHand)
                    {
                        hammerColliders.Add(col);
                        continue;
                    }

                    // CRITICAL FIX: Do NOT isolate Triggers that belong to the player body (e.g. proximity detectors).
                    if (col.isTrigger) continue;

                    playerColliders.Add(col);
                }
            }
            Debug.Log($"<color=green>[CollisionIsolator] Registered {playerColliders.Count} solid player colliders and {hammerColliders.Count} hammer colliders.</color>");
        }

        /// <summary>
        /// Protects the player by telling the physics engine to completely ignore 
        /// collisions between the player and THIS specific game object.
        /// </summary>
        public static void IsolateObject(GameObject obj)
        {
            if (obj == null) return;

            Collider[] objColliders = obj.GetComponentsInChildren<Collider>(true);
            
            // 1. Always isolate solid player colliders from EVERYTHING (Tables, Moles, Broken Pieces)
            foreach (Collider pCol in playerColliders)
            {
                if (pCol == null) continue;
                foreach (Collider oCol in objColliders)
                {
                    if (oCol != null && pCol != oCol) Physics.IgnoreCollision(pCol, oCol, true);
                }
            }

            // (The hammer isolation rule for the ArcadeTable has been removed here,
            // so the hammer will now physically hit and bounce off the top of the table again!)
        }

        /// <summary>
        /// Isolates an array of rigidbodies (useful for cell-fractured broken pieces).
        /// </summary>
        public static void IsolateRigidbodies(Rigidbody[] rigidbodies)
        {
            if (rigidbodies == null) return;
            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb != null) IsolateObject(rb.gameObject);
            }
        }
    }
}
