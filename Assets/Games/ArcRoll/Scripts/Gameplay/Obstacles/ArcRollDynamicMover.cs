using System.Collections;
using UnityEngine;

namespace ArcRoll.Gameplay.Obstacles
{
    /// <summary>
    /// Discrete step-and-hold movement script for basketball hoops.
    /// Smoothly glides to one of 3 random discrete positions, then STOPS and HOLDS for 2 seconds!
    /// </summary>
    public class ArcRollDynamicMover : MonoBehaviour
    {
        public enum MovementType
        {
            Sideways,
            UpDown,
            Wavy
        }

        private MovementType movementType = MovementType.Sideways;

        private Vector3 startPos;
        private Vector3 trueRightDir;
        private bool isConfigured = false;

        [Header("Discrete Settings")]
        [Tooltip("Increased distance length for horizontal movement.")]
        [SerializeField] private float slideDistanceX = 1.5f;
        
        [Tooltip("Increased distance length for vertical movement.")]
        [SerializeField] private float slideDistanceY = 0.8f;
        
        [Tooltip("Speed of the smooth transition to the step position.")]
        [SerializeField] private float moveDuration = 0.7f;
        
        [Tooltip("How long to stay completely stationary at each step (2 seconds).")]
        [SerializeField] private float holdDuration = 2.0f;

        /// <summary>
        /// Call this immediately after adding the component to configure its behavior!
        /// </summary>
        public void Setup(MovementType type)
        {
            movementType = type;
            startPos = transform.position;

            // Calculate a PERFECT left/right direction relative to the player, ignoring the 3D model's rotation
            Vector3 playerPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Vector3 flatPlayerPos = new Vector3(playerPos.x, startPos.y, playerPos.z);
            Vector3 dirToPlayer = (flatPlayerPos - startPos).normalized;
            trueRightDir = Vector3.Cross(Vector3.up, dirToPlayer).normalized;

            isConfigured = true;
            StartCoroutine(DiscreteMovementRoutine());
        }

        private IEnumerator DiscreteMovementRoutine()
        {
            int lastPosIndex = -1;
            
            // Cache hoop reference once
            BasketballHoop hoop = GetComponent<BasketballHoop>();

            while (isConfigured)
            {
                // ── 1. HIDE TIMER: Hoop is about to move ────────────────────
                if (hoop != null) hoop.SetTimerVisible(false);

                // Randomly pick one of 3 discrete positions (0, 1, 2)
                int posIndex;
                do
                {
                    posIndex = Random.Range(0, 3);
                } while (posIndex == lastPosIndex);
                
                lastPosIndex = posIndex;

                Vector3 targetOffset = Vector3.zero;

                switch (movementType)
                {
                    case MovementType.Sideways:
                        float stepX = (posIndex == 0) ? -1.0f : ((posIndex == 2) ? 1.0f : 0f);
                        targetOffset = trueRightDir * (stepX * slideDistanceX);
                        break;

                    case MovementType.UpDown:
                        float stepY = (posIndex == 0) ? -0.5f : ((posIndex == 2) ? 1.0f : 0f);
                        targetOffset = Vector3.up * (stepY * slideDistanceY);
                        break;

                    case MovementType.Wavy:
                        float wavyX = (posIndex == 0) ? -1.0f : ((posIndex == 2) ? 1.0f : 0f);
                        float wavyY = (posIndex == 1) ? 1.0f : -0.4f;
                        targetOffset = (trueRightDir * (wavyX * slideDistanceX)) + (Vector3.up * (wavyY * slideDistanceY));
                        break;
                }

                Vector3 targetPos = startPos + targetOffset;
                Vector3 currentPos = transform.position;

                // ── 2. GLIDE: Timer stays hidden while moving ───────────────
                float elapsed = 0f;
                while (elapsed < moveDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                    transform.position = Vector3.Lerp(currentPos, targetPos, t);
                    yield return null;
                }
                transform.position = targetPos;

                // ── 3. HOLD: Show countdown, hoop is stationary ─────────────
                if (hoop != null) hoop.SetTimerVisible(true);

                float holdRemaining = 3.0f;
                while (holdRemaining > 0f)
                {
                    if (hoop != null)
                    {
                        int seconds = Mathf.CeilToInt(holdRemaining);
                        hoop.SetTimerText(seconds.ToString(), seconds <= 1 ? Color.red : Color.white);
                    }
                    yield return null;
                    holdRemaining -= Time.deltaTime;
                }

                // ── 4. MOVING: Hide before glide ────────────────────────────
                if (hoop != null)
                {
                    hoop.SetTimerVisible(false);
                }
            }
        }

        private void OnDestroy()
        {
            // Ensure timer is cleaned up if hoop gets destroyed mid-countdown
            BasketballHoop hoop = GetComponent<BasketballHoop>();
            if (hoop != null) hoop.SetTimerVisible(false);
        }
    }
}
