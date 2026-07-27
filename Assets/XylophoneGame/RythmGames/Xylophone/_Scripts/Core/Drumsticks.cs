using UnityEngine;

namespace XM {
    public class Drumsticks : MonoBehaviour {
        [SerializeField] private bool isLeftController;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Transform tip;

        // 🔒 Internal tuning (not exposed)
        private float sphereRadius = 0.02f;
        private float minHitVelocity = 0.2f;
        private float hitCooldown = 0.08f;
        private float minTrailSpeed = 0.3f;

        private Vector3 lastTipPos;
        private Vector3 velocity;
        private float lastHitTime;

        private void Start() {
            lastTipPos = tip.position;
        }

        private void Update() {
            // Calculate velocity
            velocity = (tip.position - lastTipPos) / Time.deltaTime;

            Vector3 direction = tip.position - lastTipPos;
            float distance = direction.magnitude;

            // 🚀 SphereCast sweep (main detection)
            if (distance > 0f) {
                RaycastHit hit;
                if (Physics.SphereCast(lastTipPos, sphereRadius, direction.normalized, out hit, distance)) {
                    TryHit(hit.collider);
                }
            }

            lastTipPos = tip.position;

            // Trail control
            float speed = velocity.magnitude;
            if (trail != null) {
                trail.emitting = speed > minTrailSpeed;
            }
        }

        private void TryHit(Collider col) {
            Drum drum = col.GetComponent<Drum>();
            if (drum == null) return;

            if (Time.time - lastHitTime < hitCooldown) return;

            if (velocity.y >= -minHitVelocity) return;

            lastHitTime = Time.time;
            drum.DrumHit();
        }

        // Optional fallback
        private void OnTriggerEnter(Collider other) {
            TryHit(other);
        }
    }
}
