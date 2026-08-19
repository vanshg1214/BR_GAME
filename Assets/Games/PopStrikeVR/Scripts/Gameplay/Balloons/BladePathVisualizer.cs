using System.Collections.Generic;
using UnityEngine;

namespace PopstrikeVR.Gameplay
{
    public class BladePathVisualizer : MonoBehaviour
    {
        [Header("Wire Settings")]
        [Tooltip("Material for the connecting line. Will use a basic solid color if empty.")]
        public Material wireMaterial;
        public float wireWidth = 0.02f;
        public Color wireColor = new Color(0f, 0.5f, 1f, 0.5f);
        [Tooltip("Controls how bright/glowy the connecting wire is.")]
        [Range(0f, 5f)] public float wireGlowIntensity = 1.5f;

        [Header("Arrow Settings")]
        [Tooltip("Assign an Arrow PREFAB here. Rotate the child mesh inside the prefab so its tip faces the Blue Z-Axis.")]
        public GameObject arrowPrefab;
        public float arrowSpeed = 0.5f;
        public float arrowSpacing = 0.2f;
        [Tooltip("Multiplies the final size of the arrow prefab.")]
        public float arrowScale = 1.0f;

        private LineRenderer lineRenderer;
        private readonly List<GameObject> activeArrows = new List<GameObject>();
        private readonly List<Vector3> pathPoints = new List<Vector3>();
        private float[] segmentLengths;
        private float totalPathLength;

        public void ShowPath(List<Vector3> points)
        {
            if (points == null || points.Count < 2) return;

            pathPoints.Clear();
            pathPoints.AddRange(points);

            // Calculate segment lengths for the whole path
            segmentLengths = new float[pathPoints.Count - 1];
            totalPathLength = 0f;
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                segmentLengths[i] = Vector3.Distance(pathPoints[i], pathPoints[i + 1]);
                totalPathLength += segmentLengths[i];
            }

            // Setup LineRenderer (created once, reused)
            if (lineRenderer == null)
            {
                GameObject lineObj = new GameObject("BladeWire");
                lineObj.transform.SetParent(transform);
                lineRenderer = lineObj.AddComponent<LineRenderer>();

                lineRenderer.useWorldSpace = true;

                if (wireMaterial != null)
                    lineRenderer.material = wireMaterial;
                else
                {
                    var mat = new Material(Shader.Find("Sprites/Default"));
                    mat.renderQueue = 2990;
                    lineRenderer.material = mat;
                }

                lineRenderer.numCapVertices = 8;
                lineRenderer.numCornerVertices = 8;
            }

            // Update wire width and positions every time
            Color finalWireColor = wireColor * wireGlowIntensity;
            finalWireColor.a = wireColor.a; // preserve original alpha transparency
            
            lineRenderer.startColor = finalWireColor;
            lineRenderer.endColor = finalWireColor;
            
            lineRenderer.startWidth = wireWidth;
            lineRenderer.endWidth = wireWidth;
            lineRenderer.positionCount = pathPoints.Count;
            lineRenderer.SetPositions(pathPoints.ToArray());
            lineRenderer.gameObject.SetActive(true);

            // Number of arrows based on path length
            int numArrows = Mathf.Max(1, Mathf.FloorToInt(totalPathLength / arrowSpacing));

            // Spawn or recycle arrows
            for (int i = 0; i < numArrows; i++)
            {
                if (i >= activeArrows.Count)
                {
                    GameObject arrow;
                    if (arrowPrefab != null)
                    {
                        arrow = Instantiate(arrowPrefab, transform);
                    }
                    else
                    {
                        // Fallback to a built-in Cube if the user forgot to assign a prefab
                        Debug.LogWarning("BladePathVisualizer: No Arrow Prefab assigned! Using a default Cube.");
                        arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        arrow.transform.SetParent(transform, false);
                        arrow.transform.localScale = Vector3.one * 0.05f; // Small cube
                        Destroy(arrow.GetComponent<Collider>()); // Don't want physics collisions on visual arrows
                    }
                    
                    arrow.name = "BladeArrow_" + i;
                    activeArrows.Add(arrow);
                }

                var arrowGO = activeArrows[i];
                arrowGO.transform.localScale = Vector3.one * arrowScale;
                arrowGO.SetActive(true);
            }

            // Hide surplus arrows from previous shorter chains
            for (int i = numArrows; i < activeArrows.Count; i++)
                activeArrows[i].SetActive(false);
        }

        public void HidePath()
        {
            if (lineRenderer != null)
                lineRenderer.gameObject.SetActive(false);

            foreach (var a in activeArrows)
                if (a != null) a.SetActive(false);

            // Clear so arrows don't drift when path is gone
            pathPoints.Clear();
        }

        private void Update()
        {
            if (pathPoints.Count < 2 || totalPathLength <= 0f) return;

            for (int i = 0; i < activeArrows.Count; i++)
            {
                var arrowGO = activeArrows[i];
                if (!arrowGO.activeSelf) continue;

                // Each arrow is offset by its index * spacing along the path
                // Time.time * arrowSpeed scrolls all arrows forward continuously
                float dist = ((i * arrowSpacing) + (Time.time * arrowSpeed)) % totalPathLength;

                GetPositionAndDirectionOnPath(dist, out Vector3 worldPos, out Vector3 dir);

                // Place arrow exactly on the wire centerline
                arrowGO.transform.position = worldPos;

                // Rotate to face the direction of travel
                if (dir.sqrMagnitude > 0.001f)
                {
                    arrowGO.transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        /// <summary>
        /// Walks the path segments and returns the exact world position and forward direction
        /// for a given distance along the total path.
        /// </summary>
        private void GetPositionAndDirectionOnPath(float dist, out Vector3 position, out Vector3 direction)
        {
            float accumulated = 0f;

            for (int s = 0; s < segmentLengths.Length; s++)
            {
                float segLen = segmentLengths[s];

                if (dist <= accumulated + segLen || s == segmentLengths.Length - 1)
                {
                    // Clamp t to [0,1] so the last segment edge case doesn't overshoot
                    float t = Mathf.Clamp01((dist - accumulated) / Mathf.Max(segLen, 0.0001f));
                    position  = Vector3.Lerp(pathPoints[s], pathPoints[s + 1], t);
                    direction = (pathPoints[s + 1] - pathPoints[s]).normalized;
                    return;
                }

                accumulated += segLen;
            }

            // Fallback to end of path
            position  = pathPoints[pathPoints.Count - 1];
            direction = (pathPoints[pathPoints.Count - 1] - pathPoints[pathPoints.Count - 2]).normalized;
        }

        private void OnDisable()
        {
            HidePath();
        }
    }
}
