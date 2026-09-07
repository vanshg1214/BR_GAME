using UnityEngine;

namespace WhackAMole
{
    /// <summary>
    /// Attach this script to your Hand Prefab or OVRHand object.
    /// It ensures the visual hand doesn't clip through the arcade table.
    /// Requirements:
    /// - The Hand object MUST have a Collider (e.g. SphereCollider, isTrigger = true is fine).
    /// </summary>
    public class HandGhosting : MonoBehaviour
    {
        private Transform arcadeTable;
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;
        private Collider myCol;

        private float tableSearchTimer = 0f;
        private RaycastHit[] raycastResults = new RaycastHit[20];

        private void Start()
        {
            // Save the default local position (usually 0,0,0 if it's a direct child of the tracking anchor)
            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;

            myCol = GetComponent<Collider>();
            if (myCol == null)
            {
                Debug.LogWarning("HandGhosting requires a Collider (e.g. SphereCollider) on the hand object to work!");
            }

            // Find the table
            GameObject table = GameObject.Find("ArcadeTable") ?? GameObject.Find("Cube");
            if (table != null) arcadeTable = table.transform;
        }

        private void LateUpdate()
        {
            // 1. Reset to strict tracked position (Zero Lag)
            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;

            // 2. Prevent passing through the table!
            ResolveTablePenetration();
        }

        private void ResolveTablePenetration()
        {
            if (arcadeTable == null)
            {
                if (Time.time < tableSearchTimer) return;
                tableSearchTimer = Time.time + 1.0f; // Only search once per second to prevent massive lag!

                GameObject table = GameObject.Find("ArcadeTable") ?? GameObject.Find("Cube");
                if (table != null) arcadeTable = table.transform;
                else return; // Still not found
            }

            Collider tableCol = arcadeTable.GetComponentInChildren<Collider>();
            if (tableCol == null) return;

            Collider[] myCols = GetComponentsInChildren<Collider>();
            float maxPushUp = 0f;

            foreach (Collider myCol in myCols)
            {
                if (myCol.isTrigger && myCol.gameObject != this.gameObject) 
                {
                    // Allow child triggers if they are explicitly part of the hand/hammer, but usually we just process all of them
                }

                Vector3 center = myCol.bounds.center;
                
                // BUGFIX: bounds.min.y causes massive popping when the hand or hammer rotates!
                // We must use ClosestPoint to find the true lowest vertex.
                Vector3 lowestPoint = myCol.ClosestPoint(new Vector3(center.x, center.y - 100f, center.z));
                float lowestY = lowestPoint.y;

                // Cast a ray straight down from high above to detect the table surface
                int hitCount = Physics.RaycastNonAlloc(new Vector3(center.x, center.y + 2f, center.z), Vector3.down, raycastResults, 4f);
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = raycastResults[i];
                    if (hit.collider == tableCol || hit.collider.transform.IsChildOf(arcadeTable))
                    {
                        float tableTopY = hit.point.y;
                        if (lowestY < tableTopY)
                        {
                            float pushUp = tableTopY - lowestY;
                            if (pushUp > maxPushUp) maxPushUp = pushUp;
                        }
                        break; // Found the table for this collider
                    }
                }
            }

            if (maxPushUp > 0)
            {
                // Push the entire hand (and attached hammer) strictly UP
                transform.position += new Vector3(0, maxPushUp, 0);
            }
        }
    }
}
