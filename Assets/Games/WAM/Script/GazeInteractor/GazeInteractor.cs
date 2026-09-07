using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using WhackAMole.Gaze;

namespace WhackAMole
{
    public class GazeInteractor : MonoBehaviour {
        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        [Header("Settings")]
        [SerializeField] private float maxDistance = 25f; // Increased default to prevent distance-based gaze issues
        [SerializeField] private float dwellTime = 2f;
        [SerializeField] private LayerMask uiLayer;

        private GameObject currentTarget;
        private float gazeTimer;

        private float clickCooldown = 1.5f;
        private float cooldownTimer = 0f;
        private bool isOnCooldown = false;

        private float gracePeriod = 0.3f;
        private float graceTimerState = 0f;
        private bool isGracePeriodActive = false;

        void Update() {
            if (cameraTransform == null) return;

            // Handle cooldown
            if (isOnCooldown) {
                cooldownTimer -= Time.unscaledDeltaTime;
                if (cooldownTimer <= 0f) {
                    isOnCooldown = false;
                }
                return;
            }

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit[] hits;
            GameObject hitObj = null;

            // CRITICAL UX FIX: Use RaycastAll to pierce through any accidental invisible "glass walls" 
            // (like the giant Canvas BoxCollider) and find the actual button hiding behind it!
            hits = Physics.RaycastAll(ray, maxDistance, uiLayer);
            
            // Sort hits by distance so we hit the closest valid button first
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits) {
                Button btn = hit.collider.GetComponentInParent<Button>();
                if (btn != null && btn.interactable) {
                    hitObj = btn.gameObject;
                    break; // Stop looking once we find the first valid button
                }
            }

            if (hitObj != null) {
                // We are looking directly at a valid button!
                if (currentTarget != hitObj) {
                    // We looked at a BRAND NEW button. Reset everything.
                    ResetCurrent();
                    currentTarget = hitObj;
                    isGracePeriodActive = false;

                    bool instant = hitObj.TryGetComponent<InstantGazeClick>(out _);
                    if (!instant) StartZoom(currentTarget);
                }
                else {
                    // We are looking at the SAME button we were already zooming!
                    
                    // If we were in a "grace period" (raycast slipped off temporarily), cancel it!
                    if (isGracePeriodActive) {
                        isGracePeriodActive = false;
                    }

                    bool instant = hitObj.TryGetComponent<InstantGazeClick>(out _);
                    if (instant) {
                        TriggerClick(hitObj);
                        StartCooldown();
                        ResetCurrent();
                        return;
                    }

                    gazeTimer += Time.unscaledDeltaTime;

                    if (gazeTimer >= dwellTime) {
                        TriggerClick(currentTarget);
                        StartCooldown();
                        ResetCurrent();
                    }
                }
            }
            else {
                // We are looking at NOTHING (or the raycast slipped off the button).
                if (currentTarget != null) {
                    // Instead of instantly canceling the zoom, give the player a 0.3 second Grace Period!
                    // This completely solves the jittering/flickering issue for thin raycasts.
                    if (!isGracePeriodActive) {
                        isGracePeriodActive = true;
                        graceTimerState = gracePeriod;
                    }
                    else {
                        graceTimerState -= Time.unscaledDeltaTime;
                        if (graceTimerState <= 0f) {
                            ResetCurrent();
                        }
                    }
                }
            }
        }

        void StartCooldown() {
            isOnCooldown = true;
            cooldownTimer = clickCooldown;
        }

        void StartZoom(GameObject target) {
            target.transform.DOKill();
            target.transform.localScale = Vector3.one;

            target.transform.DOScale(1.2f, dwellTime)
                .SetEase(Ease.Linear).SetUpdate(true);
        }

        void TriggerClick(GameObject target) {
            if (target.TryGetComponent<Button>(out Button btn)) btn.onClick.Invoke();
        }

        void ResetCurrent() {
            if (currentTarget != null) {
                currentTarget.transform.DOKill();
                currentTarget.transform.DOScale(1f, 0.2f).SetUpdate(true);
                currentTarget = null;
            }

            gazeTimer = 0f;
            isGracePeriodActive = false;
        }
    }
}
