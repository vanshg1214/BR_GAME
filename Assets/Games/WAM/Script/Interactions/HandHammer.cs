using UnityEngine;
using System.Collections;

namespace WhackAMole
{
    [RequireComponent(typeof(Rigidbody))]
    public class HandHammer : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Controller")]
        [Tooltip("Which hand drives this hammer.")]
        [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;

        [Header("Haptics")]
        [Range(0, 1)]
        [SerializeField] private float hapticStrength  = 0.7f;
        [SerializeField] private float hapticDuration  = 0.15f;

        [Header("Tracking")]
        [Tooltip("Velocity smoothing factor — higher = smoother but laggier.")]
        [SerializeField] private float velocitySmoothing = 10f;

        [Header("Hit Cooldown")]
        [Tooltip("Min seconds between consecutive hits to prevent double-triggers.")]
        [SerializeField] private float hitCooldown = 0.15f;

        [Header("Swing Tracking")]
        [Tooltip("Minimum downward velocity to count as a physical swing rep.")]
        [SerializeField] private float minSwingVelocity = 1.0f;
        [Tooltip("Cooldown to prevent double-counting a single physical swing.")]
        [SerializeField] private float swingCooldown = 0.5f;

        [Header("Table Penetration")]
        [Tooltip("Extra height (metres) above the table origin where the hammer rests. Increase if the hammer still clips slightly into the table surface.")]
        [SerializeField] private float tableSurfaceOffset = 0.02f;

        [Tooltip("Maximum horizontal distance (metres) from the table center where penetration correction applies. Set larger than half the table width.")]
        [SerializeField] private float tableRadius = 0.8f;

        #endregion

        private Vector3   lastPosition;
        private Rigidbody rb;
        private float     lastHitTime = -1f;
        private float     lastSwingTime = -1f;

        private Transform arcadeTable;

        // Cached so we don't allocate a new WaitForSeconds every vibration
        private WaitForSeconds hapticWait;

        private Vector3   initialLocalPos;
        private Quaternion initialLocalRot;
        
        private float tableSearchTimer = 0f;

        // Pre-allocated arrays to eliminate VR Garbage Collection freezes
        private RaycastHit[] sphereCastResults = new RaycastHit[20];

        #region Public Accessors

        public Vector3 CurrentVelocity { get; private set; }
        public OVRInput.Controller ControllerSide => controller;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            hapticWait = new WaitForSeconds(hapticDuration);

            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            // CRITICAL FIX: If the user's 3D hammer model doesn't have a collider, 
            // the penetration physics will completely ignore it and let it sink!
            Collider[] colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) 
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                // Approximate a standard hammer size so it bounces off the table
                box.size = new Vector3(0.1f, 0.3f, 0.1f); 
                box.isTrigger = true;
            }
            else
            {
                // Ensure EVERY collider on the hammer is a trigger, 
                // otherwise a solid head collider will literally push the table down if it has a Rigidbody!
                foreach (Collider c in colliders)
                {
                    c.isTrigger = true;
                }
            }

            // Save the exact grip offset you configured in the Unity Editor
            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;

            // Find the arcade table for penetration correction
            GameObject table = GameObject.Find("ArcadeTable") ?? GameObject.Find("Cube");
            if (table != null) arcadeTable = table.transform;
            
            lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            // 1. VISUAL GHOSTING: Reset to the true tracking position + grip offset every frame
            // This ensures ZERO lag, as the hammer is strictly bound to the Meta hand anchor.
            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;

            // 2. Compute true physical velocity BEFORE penetration blocks it.
            // This ensures fast swings still register full velocity even if the visual hammer stops at the table!
            Vector3 frameVelocity = (transform.position - lastPosition) / Time.deltaTime;
            CurrentVelocity = Vector3.Lerp(CurrentVelocity, frameVelocity, Time.deltaTime * velocitySmoothing);

            // 3. ANTI-TUNNELING HIT DETECTION
            // If you swing with "full force" in VR, the hammer teleports past thin colliders (like Cages) between frames.
            // We use a SphereCast from where the hammer WAS to where it IS to guarantee we never miss a fast hit!
            Vector3 movement = transform.position - lastPosition;
            float moveDistance = movement.magnitude;
            if (moveDistance > 0.005f)
            {
                // Cast a thick 5cm sphere along the path of movement.
                // Uses NonAlloc to prevent Garbage Collection freezing in VR!
                int hitCount = Physics.SphereCastNonAlloc(lastPosition, 0.05f, movement.normalized, sphereCastResults, moveDistance);
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = sphereCastResults[i];

                    // PERFORMANCE OPTIMIZATION: Do NOT search the giant ArcadeTable hierarchy for IHittable every frame!
                    if (arcadeTable != null && hit.collider.transform.IsChildOf(arcadeTable)) continue;

                    IHittable target = hit.collider.GetComponentInParent<IHittable>();
                    if (target != null && (Time.time - lastHitTime >= hitCooldown))
                    {
                        Debug.Log($"<color=magenta>[HandHammer]</color> Anti-Tunneling SphereCast caught a high-speed hit on: {hit.collider.gameObject.name}!");
                        lastHitTime = Time.time;
                        target.OnHit(CurrentVelocity, hit.point);
                        StartCoroutine(HapticPulse());
                    }
                }
            }

            lastPosition = transform.position;



            DetectPhysicalSwing();
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"<color=cyan>[HandHammer]</color> OnTriggerEnter collided with: {other.gameObject.name}");

            if (Time.time - lastHitTime < hitCooldown) 
            {
                Debug.Log("<color=cyan>[HandHammer]</color> Hit ignored due to cooldown.");
                return;
            }

            IHittable target = other.GetComponentInParent<IHittable>();
            if (target == null) 
            {
                Debug.Log($"<color=cyan>[HandHammer]</color> No IHittable script found on {other.gameObject.name} or its parents.");
                return;
            }

            Debug.Log($"<color=green>[HandHammer]</color> Valid IHittable target found! Calling OnHit.");
            lastHitTime = Time.time;

            Collider myCol = GetComponent<Collider>();
            Vector3 contactPoint = other.ClosestPoint(
                myCol != null ? myCol.bounds.center : transform.position
            );

            target.OnHit(CurrentVelocity, contactPoint);
            StartCoroutine(HapticPulse());
        }

        #endregion

        #region Internals

        /// <summary>
        /// Detects if the player is making a strong downward physical motion and logs it to the save file.
        /// </summary>
        private void DetectPhysicalSwing()
        {
            // Only count if enough time has passed since the last tracked swing
            if (Time.time - lastSwingTime < swingCooldown) return;

            // If the hammer is moving DOWNWARD faster than the threshold
            // Note: Down is negative Y, so we check if CurrentVelocity.y < -minSwingVelocity
            if (CurrentVelocity.y < -minSwingVelocity)
            {
                lastSwingTime = Time.time;
                
                if (Data.ProfileDataLoader.Instance != null)
                {
                    Data.ProfileDataLoader.Instance.AddArmRep();
                }
            }
        }



        private IEnumerator HapticPulse()
        {
            OVRInput.SetControllerVibration(1f, hapticStrength, controller);
            yield return hapticWait;
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }

        #endregion
    }
}
