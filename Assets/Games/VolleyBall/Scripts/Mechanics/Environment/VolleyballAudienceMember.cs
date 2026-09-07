using UnityEngine;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.Mechanics
{
    public enum AudienceTeam
    {
        Player,   // Cheers only when the Player scores
        AI,       // Cheers only when the AI (Dog) scores
        Both      // Cheers whenever anyone scores (Neutral/Excited fan)
    }

    /// <summary>
    /// Attach this script to any animated animal in the audience bleachers.
    /// It automatically listens to the VolleyballGameManager and plays a cheering
    /// animation when its assigned team scores a point!
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class VolleyballAudienceMember : MonoBehaviour
    {
        [Header("Audience Settings")]
        [Tooltip("Which team does this animal cheer for?")]
        [SerializeField] private AudienceTeam supportedTeam = AudienceTeam.Player;

        [Tooltip("The exact name of the Animation Trigger to play when they cheer (e.g. 'Jump', 'Success', 'Cheer'). Check the Animator window to find the right name!")]
        [SerializeField] private string successTriggerName = "Jump";

        private Animator animator;
        private int lastPlayerScore = 0;
        private int lastAIScore = 0;
        private bool isSubscribed = false;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Start()
        {
            // Try to subscribe immediately if GameManager already exists
            TrySubscribe();
        }

        private void Update()
        {
            // If GameManager spawned later, keep trying until we connect
            if (!isSubscribed)
            {
                TrySubscribe();
            }
        }

        private void TrySubscribe()
        {
            if (VolleyballGameManager.Instance != null && !isSubscribed)
            {
                // Sync the initial scores so we don't cheer for past points
                lastPlayerScore = VolleyballGameManager.Instance.PlayerScore;
                lastAIScore = VolleyballGameManager.Instance.AIScore;

                // Subscribe to the score event
                VolleyballGameManager.Instance.OnScoreUpdated += HandleScoreUpdated;
                isSubscribed = true;
            }
        }

        private void OnDestroy()
        {
            // Always unsubscribe to prevent memory leaks!
            if (VolleyballGameManager.Instance != null && isSubscribed)
            {
                VolleyballGameManager.Instance.OnScoreUpdated -= HandleScoreUpdated;
            }
        }

        private void HandleScoreUpdated()
        {
            if (animator == null || string.IsNullOrEmpty(successTriggerName)) return;

            int currentPlayerScore = VolleyballGameManager.Instance.PlayerScore;
            int currentAIScore = VolleyballGameManager.Instance.AIScore;

            bool playerScored = currentPlayerScore > lastPlayerScore;
            bool aiScored = currentAIScore > lastAIScore;

            // Update our records
            lastPlayerScore = currentPlayerScore;
            lastAIScore = currentAIScore;

            // Check if our team scored
            bool shouldCheer = false;

            if (supportedTeam == AudienceTeam.Player && playerScored)
            {
                shouldCheer = true;
            }
            else if (supportedTeam == AudienceTeam.AI && aiScored)
            {
                shouldCheer = true;
            }
            else if (supportedTeam == AudienceTeam.Both && (playerScored || aiScored))
            {
                shouldCheer = true;
            }

            if (shouldCheer)
            {
                animator.SetTrigger(successTriggerName);
            }
        }
    }
}
