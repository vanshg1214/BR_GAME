using UnityEngine;
using System.Collections.Generic;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.Mechanics
{
    /// <summary>
    /// Adaptive "Bullet Time" Manager for VR Rehab Volleyball.
    /// If the ball is approaching the player's side and the player's hands are too far
    /// from the predicted landing zone, this dynamically slows down the game (Time.timeScale)
    /// to give the patient an extra 1-2 seconds to react and reach the ball.
    /// </summary>
    public class VolleyballTimeManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the active volleyball.")]
        [SerializeField] private VolleyballBall activeBall;
        
        [Tooltip("Reference to the visualizer that predicts the landing spot.")]
        [SerializeField] private VolleyballLandingVisualizer landingVisualizer;

        [Header("Bullet Time Settings")]
        [Tooltip("Maximum distance (meters) the hand can be from the landing spot before Bullet Time kicks in.")]
        [SerializeField] private float safeZoneRadius = 0.8f;
        
        [Tooltip("How low the ball must be (Y height) before it considers triggering Bullet Time.")]
        [SerializeField] private float triggerHeight = 2.5f;

        [Tooltip("The lowest time scale allowed (e.g., 0.3 means 30% speed).")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float minTimeScale = 0.3f;
        
        [Tooltip("How fast time smooths in and out of slow motion.")]
        [SerializeField] private float timeLerpSpeed = 5.0f;

        private VolleyballHand[] playerHands;
        private float targetTimeScale = 1.0f;

        private float currentBallScale = 1.0f;

        private void Start()
        {
            // Find all hands in the scene
            playerHands = FindObjectsByType<VolleyballHand>(FindObjectsSortMode.None);
            if (playerHands.Length == 0)
            {
                Debug.LogWarning("[VolleyballTimeManager] No VolleyballHands found in the scene! Bullet time will not activate.");
            }
        }

        private void Update()
        {
            if (activeBall == null || landingVisualizer == null || playerHands.Length == 0) return;

            // Default target is normal speed
            targetTimeScale = 1.0f;

            // Check if Bullet Time should be activated
            if (ShouldActivateBulletTime())
            {
                targetTimeScale = minTimeScale;
            }

            // Smoothly interpolate the time scale for a polished feel
            if (Mathf.Abs(currentBallScale - targetTimeScale) > 0.01f)
            {
                currentBallScale = Mathf.Lerp(currentBallScale, targetTimeScale, Time.unscaledDeltaTime * timeLerpSpeed);
            }
            else
            {
                currentBallScale = targetTimeScale;
            }
            
            activeBall.SetTimeScale(currentBallScale);
        }

        private bool ShouldActivateBulletTime()
        {
            // USER REQUESTED: Disable the slowing logic for now so the ball behaves completely naturally.
            return false;
            
            // Only trigger if ball is active and was last hit by the AI (heading to player)
            if (!activeBall.IsBallActive || activeBall.LastHitter != BallHitter.AI) return false;

            // Enforce Z-position check: Bullet Time ONLY activates when the ball crosses the net into the player's side
            if (VolleyballGameManager.Instance != null && VolleyballGameManager.Instance.NetTransform != null)
            {
                // Give a 0.5m buffer so it doesn't wait too late
                if (activeBall.transform.position.z > VolleyballGameManager.Instance.NetTransform.position.z + 0.5f) return false;
            }

            // Only trigger if the prediction ring is active (means it's dropping into player court)
            if (!landingVisualizer.IsShowingRing) return false;

            // Only trigger if ball is dropping down below the trigger height
            if (activeBall.transform.position.y > triggerHeight) return false;
            
            // Check if ANY hand is inside the safe zone (close to the landing spot)
            Vector3 targetSpot = landingVisualizer.PredictedLandingSpot;
            targetSpot.y = 0; // Evaluate on the 2D floor plane

            foreach (var hand in playerHands)
            {
                if (hand == null) continue;
                
                Vector3 handPos = hand.transform.position;
                handPos.y = 0; // Evaluate on the 2D floor plane
                
                float dist = Vector3.Distance(handPos, targetSpot);
                if (dist <= safeZoneRadius)
                {
                    // A hand is close enough! No need for bullet time.
                    return false; 
                }
            }

            // Ball is dropping, heading to player, and no hands are near the landing spot.
            // Activate bullet time!
            return true;
        }
        
        private void OnDestroy()
        {
            if (activeBall != null)
                activeBall.SetTimeScale(1.0f);
        }
    }
}
