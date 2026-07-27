using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Add this script directly to your Hammer model (warhammer_final_LP)
    /// to mathematically stop its head from passing through the table.
    /// This works independently of the hand ghosting script!
    /// </summary>
    public class ObjectPenetrationBlocker : MonoBehaviour
    {
        [Tooltip("How far above the table's absolute surface the object should rest. (e.g. 0.00 for flush)")]
        public float tableSurfaceOffset = 0.00f;

        [Tooltip("The radius around the center of the table where this block applies.")]
        public float tableRadius = 0.8f;

        private Transform arcadeTable;
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;

        private void Start()
        {
            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;
        }

        private void LateUpdate()
        {
            // Always reset to our default local position relative to the hand FIRST
            // so we don't accumulate "push ups" every frame and fly away!
            transform.localPosition = initialLocalPos;
            
            if (arcadeTable == null)
            {
                if (WorkspaceAutoPositioner.Instance != null)
                {
                    arcadeTable = WorkspaceAutoPositioner.Instance.transform;
                }
                else
                {
                    return; // Table not initialized yet
                }
            }

            Collider[] myCols = GetComponentsInChildren<Collider>();
            if (myCols.Length == 0) return;

            Vector3 tablePos = arcadeTable.position;

            // Check if within the table's horizontal radius
            float dx = transform.position.x - tablePos.x;
            float dz = transform.position.z - tablePos.z;
            if (dx * dx + dz * dz > tableRadius * tableRadius) return;

            // Find the lowest point among all colliders on this object
            float globalLowestY = float.MaxValue;
            foreach (Collider col in myCols)
            {
                Vector3 center = col.bounds.center;
                // Raycast downwards mathematically using ClosestPoint
                Vector3 lowestPoint = col.ClosestPoint(new Vector3(center.x, center.y - 100f, center.z));
                if (lowestPoint.y < globalLowestY)
                {
                    globalLowestY = lowestPoint.y;
                }
            }

            // Find the table's true surface height
            float surfaceY = tablePos.y + tableSurfaceOffset;
            Collider tableCol = arcadeTable.GetComponentInChildren<Collider>();
            if (tableCol != null)
            {
                surfaceY = tableCol.bounds.max.y;
            }

            // If the absolute lowest point is below the table surface, push it up!
            if (globalLowestY < surfaceY)
            {
                float pushUp = surfaceY - globalLowestY;
                transform.position += new Vector3(0, pushUp, 0);
            }
        }
    }
}
