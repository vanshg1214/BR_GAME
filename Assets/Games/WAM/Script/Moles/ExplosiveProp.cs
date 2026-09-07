using UnityEngine;
using System;
using WhackAMole; // IHittable, ScoreManager, FeedbackManager

namespace WhackAMole.Targets
{
    /// <summary>
    /// Attached directly to the BOMB or EXPLOSIVE BOTTLE. 
    /// Handles the explosion VFX and scoring logic cleanly.
    /// </summary>
    public class ExplosiveProp : MonoBehaviour, IHittable
    {
        [Header("Target Settings")]
        [Tooltip("If true (Bomb), hitting this deducts points. If false (Bottle), it gives bonus points.")]
        public bool isFakeTarget = true; 
        
        [Header("VFX & SFX")]
        [Tooltip("The massive explosion particle prefab to spawn when hit.")]
        public GameObject explosionVFXPrefab;
        [Tooltip("The sound effect to play when it explodes.")]
        public AudioClip explosionSound;

        // The parent Character listens to this so it can react and duck down
        public event Action OnTargetHit;

        private bool isDestroyed = false;

        public void OnHit(Vector3 velocity, Vector3 hitPosition)
        {
            if (isDestroyed) return;
            isDestroyed = true;

            // 1. Handle Scoring based on target type
            if (ScoreManager.Instance != null)
            {
                if (isFakeTarget)
                {
                    ScoreManager.Instance.RegisterFakeHit(); // Bomb = Bad
                }
                else
                {
                    ScoreManager.Instance.AddScore(50, velocity.magnitude); // Bottle = Massive Bonus
                }
            }

            // 2. Play explosion visuals and custom sound
            if (explosionVFXPrefab != null)
            {
                Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
            }
            if (explosionSound != null)
            {
                AudioSource.PlayClipAtPoint(explosionSound, transform.position);
            }
            else 
            {
                // Fallback to legacy manager if no custom clip is assigned
                FeedbackManager.Instance?.PlayFakeHit(hitPosition, -1);
            }

            // 3. Tell the Character (Hamster/Squirrel) that we exploded!
            OnTargetHit?.Invoke();
            
            // 4. Hide the prop
            gameObject.SetActive(false);
        }
    }
}
