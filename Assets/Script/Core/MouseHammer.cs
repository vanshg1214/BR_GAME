using UnityEngine;

namespace WhackAMole
{
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public class MouseHammer : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed    = 15f;
        [SerializeField] private float hoverHeight  = 0.5f;

        [Header("Hit Cooldown")]
        [SerializeField] private float hitCooldown  = 0.15f;

        private Camera  mainCam;
        private Vector3 targetPosition;
        private Vector3 previousPosition;
        private Vector3 velocity;
        private float   lastHitTime = -1f;

        #region Unity Lifecycle

        private void Start()
        {
            mainCam = Camera.main;

            // Auto-configure physics for trigger-based hit detection
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = 0.1f;

            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        private void Update()
        {
            if (mainCam == null) return;

            // Don't move the hammer while the mouse is over UI elements (e.g. dashboard sliders)
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                previousPosition = transform.position;
                return;
            }

            // Track velocity for HeavyMole threshold checks
            if (Time.deltaTime > 0f)
            {
                velocity = (transform.position - previousPosition) / Time.deltaTime;
            }
            previousPosition = transform.position;

            // Raycast to the floor plane to find the mouse world position
            Ray   ray   = mainCam.ScreenPointToRay(Input.mousePosition);
            Plane floor = new Plane(Vector3.up, Vector3.zero);

            if (floor.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                float   yTarget  = Input.GetMouseButton(0) ? -0.2f : hoverHeight;
                targetPosition   = new Vector3(hitPoint.x, yTarget, hitPoint.z);
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time - lastHitTime < hitCooldown) return;

            IHittable target = other.GetComponentInParent<IHittable>();
            if (target != null)
            {
                lastHitTime = Time.time;
                target.OnHit(velocity, transform.position);
            }
        }

        #endregion
    }
}
