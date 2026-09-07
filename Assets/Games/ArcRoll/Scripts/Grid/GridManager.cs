using UnityEngine;

namespace ArcRoll.Grid
{
    /// <summary>
    /// Calculates the 3D world position for a given Grid (Row, Col).
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Arc Settings")]
        [Tooltip("The distance from the player to the targets (Radius of the arc).")]
        [SerializeField] private float arcRadius = 3.0f;
        
        [Tooltip("The exact angles for the 5 columns (left to right).")]
        [SerializeField] private float[] columnAngles = new float[] { -60f, -30f, 0f, 30f, 60f };

        [Header("Row Heights")]
        [SerializeField] private float row0Height = 0.05f; // Floor level (Bowling)
        [SerializeField] private float row1Height = 1.5f;  // Mid Hoop
        [SerializeField] private float row2Height = 2.5f;  // High Hoop

        // ── Public Accessors for LevelDirector ────────────────────────────────
        public float ArcRadius => arcRadius;
        public float[] ColumnAngles => columnAngles;
        public float Row0Height => row0Height;
        public float Row1Height => row1Height;
        public float Row2Height => row2Height;

        [Header("Editor Preview")]
        [Tooltip("Assign your basketball hoop prefab here to preview it.")]
        public GameObject previewBasketballPrefab;
        [Tooltip("Since some 3D models import sideways, add a rotation offset here (e.g. Y=90 or Y=-90) to face it forward.")]
        public Vector3 previewHoopRotationOffset = new Vector3(0, -90f, 0);

        [Tooltip("Assign your bowling pin/lane prefab here to preview it.")]
        public GameObject previewBowlingPrefab;
        
        // Hidden container to hold all the preview objects so they don't clutter the hierarchy
        [HideInInspector] public GameObject previewContainer;

        public Vector3 GetWorldPosition(int row, int col)
        {
            // Safeguard against bad column data
            if (col < 0) col = 0;
            if (col >= columnAngles.Length) col = columnAngles.Length - 1;

            float angleDegrees = columnAngles[col];
            
            // In Unity, Forward is +Z and Right is +X.
            // A 0 degree angle means straight forward (+Z).
            // A positive angle (30 deg) means to the right (+X).
            // A negative angle (-30 deg) means to the left (-X).
            float xOffset = Mathf.Sin(angleDegrees * Mathf.Deg2Rad) * arcRadius;
            float zOffset = Mathf.Cos(angleDegrees * Mathf.Deg2Rad) * arcRadius;
            
            float yOffset = row0Height;
            if (row == 1) yOffset = row1Height;
            else if (row >= 2) yOffset = row2Height;

            // Apply offsets relative to the GridManager's transform position
            return transform.position + new Vector3(xOffset, yOffset, zOffset);
        }

#if UNITY_EDITOR
        [ContextMenu("Generate Preview")]
        public void GeneratePreview()
        {
            ClearPreview(); // Clear existing ones first

            previewContainer = new GameObject("--- ARC GRID PREVIEW ---");
            previewContainer.transform.SetParent(transform);
            previewContainer.transform.localPosition = Vector3.zero;
            previewContainer.transform.localRotation = Quaternion.identity;

            Vector3 playerHeadPos = new Vector3(0, 1.6f, 0);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < columnAngles.Length; col++)
                {
                    Vector3 pos = GetWorldPosition(row, col);
                    
                    if (row == 0 && previewBowlingPrefab != null)
                    {
                        // Spawn a full 10-pin bowling triangle!
                        // First, create a parent for the lane so we can rotate the whole triangle
                        GameObject laneParent = new GameObject($"Lane {col} Pins");
                        laneParent.transform.position = pos;
                        laneParent.transform.SetParent(previewContainer.transform);

                        // Rotate the entire triangle to face the player!
                        Vector3 lookPos = playerHeadPos;
                        lookPos.y = pos.y; // Keep it flat on the floor
                        laneParent.transform.LookAt(lookPos);

                        // Standard pin spacing is about 12 inches (0.3 meters)
                        float pinSpacing = 0.3f;
                        int pinCount = 1;
                        for (int pinRow = 0; pinRow < 4; pinRow++)
                        {
                            for (int i = 0; i <= pinRow; i++)
                            {
                                // Calculate offset from the front center pin.
                                // Because the parent is looking AT the player (+Z is forward),
                                // the rows behind the front pin go backwards in the -Z direction!
                                float xOffset = (i * pinSpacing) - ((pinRow * pinSpacing) / 2f);
                                float zOffset = -pinRow * pinSpacing; 

                                Vector3 localPinPos = new Vector3(xOffset, 0, zOffset);
                                
                                GameObject pin = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(previewBowlingPrefab);
                                if (pin == null) pin = Instantiate(previewBowlingPrefab);
                                
                                // Set parent with worldPositionStays = false to preserve the prefab's built-in rotation!
                                pin.transform.SetParent(laneParent.transform, false);
                                pin.transform.localPosition = localPinPos;
                                
                                pin.name = $"Pin {pinCount}";
                                pinCount++;
                            }
                        }
                    }
                    else if (row != 0 && previewBasketballPrefab != null)
                    {
                        // Safely instantiate basketball hoop
                        GameObject spawned = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(previewBasketballPrefab);
                        if (spawned == null) spawned = Instantiate(previewBasketballPrefab); 
                        
                        spawned.transform.position = pos;
                        spawned.transform.SetParent(previewContainer.transform);
                        
                        // Look at the player's head (0, 1.6, 0) instead of the floor center (0,0,0)
                        Vector3 lookPos = playerHeadPos;
                        lookPos.y = spawned.transform.position.y; // Keep it perfectly vertical
                        spawned.transform.LookAt(lookPos);
                        
                        // Apply the rotation offset to fix sideways-imported 3D models!
                        spawned.transform.Rotate(previewHoopRotationOffset, Space.Self);
                    }
                }
            }
        }

        [ContextMenu("Clear Preview")]
        public void ClearPreview()
        {
            if (previewContainer != null)
            {
                DestroyImmediate(previewContainer);
            }
            
            // Failsafe cleanup in case the reference was lost
            Transform oldContainer = transform.Find("--- ARC GRID PREVIEW ---");
            if (oldContainer != null)
            {
                DestroyImmediate(oldContainer.gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            // Draw the 15-target Arc Grid in the Scene View!
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < columnAngles.Length; col++)
                {
                    Vector3 pos = GetWorldPosition(row, col);

                    if (row == 0)
                    {
                        // Row 0 is Bowling (draw a flat box on the floor)
                        Gizmos.color = new Color(0f, 1f, 0f, 0.5f); // Semi-transparent green
                        Gizmos.DrawCube(pos + Vector3.up * 0.1f, new Vector3(0.5f, 0.2f, 0.5f));
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireCube(pos + Vector3.up * 0.1f, new Vector3(0.5f, 0.2f, 0.5f));
                    }
                    else
                    {
                        // Rows 1 and 2 are Basketball (draw a hoop circle)
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Semi-transparent orange
                        Gizmos.DrawSphere(pos, 0.3f);
                        Gizmos.color = new Color(1f, 0.5f, 0f, 1f); // Solid orange
                        Gizmos.DrawWireSphere(pos, 0.3f);
                    }
                }
            }
        }
#endif
    }
}
