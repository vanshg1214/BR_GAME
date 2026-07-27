using UnityEngine;
using ExpObj; // Third-party namespace

namespace WhackAMole
{
    /// <summary>
    /// Attached to the destructible target prefab (e.g., Bottle).
    /// Bridges the VR Whack-a-Mole interaction layer (IHittable) with the third-party ExplosiveObject script.
    /// </summary>
    [RequireComponent(typeof(ExplosiveObject))]
    public class HittableExplosive : MonoBehaviour, IHittable
    {
        private ExplosiveObject explosiveObject;
        private BaseMole parentMole;

        private void Awake()
        {
            explosiveObject = GetComponent<ExplosiveObject>();
            parentMole = GetComponentInParent<BaseMole>();
        }

        public void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            // 1. Detonate the visual & audio explosion via the third-party script
            if (explosiveObject != null)
            {
                explosiveObject.Explode();
            }

            // 2. Relay the hit to the parent mole (triggers score, haptics, and mole retraction)
            if (parentMole != null)
            {
                parentMole.OnHit(velocity, hitPosition);
            }
        }
    }
}
