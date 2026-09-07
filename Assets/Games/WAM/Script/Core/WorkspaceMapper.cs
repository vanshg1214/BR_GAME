using UnityEngine;

namespace WhackAMole
{
    public class WorkspaceMapper : MonoBehaviour
    {
        public static WorkspaceMapper Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Calculates the board's width, depth, and lateral offset from the patient's ROM.
        /// </summary>
        public void GetWorkspaceDimensions(out float width, out float depth, out float xShift)
        {
            // Defaults if no profile is loaded yet
            width  = 1.5f;
            depth  = 1.0f;
            xShift = 0f;

            if (GameManager.Instance == null || GameManager.Instance.RehabProfile == null) return;

            RehabProfileSO profile = GameManager.Instance.RehabProfile;

            depth  = profile.armLength * Mathf.Clamp01(profile.maxFlexion   / 90f);
            width  = profile.armLength * Mathf.Clamp01(profile.maxAbduction / 90f) * 2f;
            if (profile.handMode == RehabProfileSO.HandMode.Left)
            {
                xShift = -(width * 0.25f);
            }
            else if (profile.handMode == RehabProfileSO.HandMode.Right)
            {
                xShift = (width * 0.25f);
            }
            else // Both
            {
                xShift = 0f;
            }
        }

        /// <summary>
        /// Calculates the physical space between each column and row based on the board dimensions.
        /// </summary>
        public void GetGridSpacing(int rows, int columns, out float spaceX, out float spaceZ)
        {
            GetWorkspaceDimensions(out float width, out float depth, out float xShift);
            
            // Calculate spacing (avoid division by zero)
            spaceX = columns > 1 ? width / (columns - 1) : width;
            spaceZ = rows > 1 ? depth / (rows - 1) : depth;
        }

        /// <summary>
        /// Maps normalised grid coordinates (–0.5 to +0.5) to a world-space point
        /// on the board, accounting for rotation and lateral offset.
        /// </summary>
        public Vector3 GetPointInWorkspace(float xRatio, float zRatio, Vector3 centerOrigin)
        {
            GetWorkspaceDimensions(out float boardWidth, out float boardDepth, out float xShift);

            float xOffset = (xRatio * boardWidth) + xShift;
            float zOffset = (zRatio * boardDepth) + (boardDepth * 0.5f);

            // Offsets are relative to this transform's orientation so rotating the
            // SpatialManager rotates the whole grid with it.
            return centerOrigin
                 + transform.right   * xOffset
                 + transform.forward * zOffset;
        }
    }
}
