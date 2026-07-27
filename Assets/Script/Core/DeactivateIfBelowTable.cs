using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Automatically disables collision, mesh rendering, and physics for broken pieces
    /// when they fall below the arcade table, preventing them from landing on the virtual floor
    /// and causing physics drift on the VR player's height.
    /// </summary>
    public class DeactivateIfBelowTable : MonoBehaviour
    {
        private Collider col;
        private Rigidbody rb;
        private MeshRenderer meshRenderer;

        private bool originalColEnabled;
        private bool originalMeshEnabled;
        private bool originalKinematic;

        private bool isDeactivated = false;

        private void Awake()
        {
            col = GetComponent<Collider>();
            rb = GetComponent<Rigidbody>();
            meshRenderer = GetComponent<MeshRenderer>();

            if (col != null) originalColEnabled = col.enabled;
            if (meshRenderer != null) originalMeshEnabled = meshRenderer.enabled;
            if (rb != null) originalKinematic = rb.isKinematic;
        }

        private void OnEnable()
        {
            // Restore initial states on spawn/activation
            isDeactivated = false;
            if (col != null) col.enabled = originalColEnabled;
            if (meshRenderer != null) meshRenderer.enabled = originalMeshEnabled;
            if (rb != null) rb.isKinematic = originalKinematic;
        }

        private void OnDisable()
        {
            // Always restore states when deactivated/reset so the pooler works correctly
            if (col != null) col.enabled = originalColEnabled;
            if (meshRenderer != null) meshRenderer.enabled = originalMeshEnabled;
            if (rb != null) rb.isKinematic = originalKinematic;
        }

        private void Update()
        {
            if (isDeactivated) return;

            // Get the current height of the table from the WorkspaceAutoPositioner
            float tableY = 0f;
            if (WorkspaceAutoPositioner.Instance != null)
            {
                tableY = WorkspaceAutoPositioner.Instance.transform.position.y;
            }
            else
            {
                // Fallback if WorkspaceAutoPositioner isn't initialized yet
                return;
            }

            // If the piece falls 0.02 meters below the table surface, disable it
            if (transform.position.y < (tableY - 0.02f))
            {
                isDeactivated = true;

                // Stop physics calculations completely
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // Deactivate the entire GameObject (which disables all child colliders instantly)
                gameObject.SetActive(false);
            }
        }
    }
}
