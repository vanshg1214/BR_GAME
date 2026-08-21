using UnityEngine;
using System.Collections.Generic;

namespace ArcRoll.Core
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ArcRollROMVisualizer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The player's head center anchor (e.g. CenterEyeAnchor).")]
        [SerializeField] private Transform playerHead;
        
        [Tooltip("The profile dictating the ROM values.")]
        [SerializeField] private ArcRollRehabProfileSO activeProfile;

        [Header("Right Arm Visuals")]
        [SerializeField] private bool showRightArm = true;
        [SerializeField] private Color rightArmColor = new Color(0f, 1f, 0.5f, 0.4f); // Greenish

        [Header("Left Arm Visuals")]
        [SerializeField] private bool showLeftArm = true;
        [SerializeField] private Color leftArmColor = new Color(0f, 0.7f, 1f, 0.4f); // Blueish

        [Header("General Visual Settings")]
        [Tooltip("This matches the cannon safety ratio so the demo balls sit exactly where they will stop.")]
        [SerializeField] private float safetyRatio = 0.85f;
        [Range(10, 50)]
        [SerializeField] private int horizontalResolution = 20;
        [Range(5, 25)]
        [SerializeField] private int verticalResolution = 10;

        [Header("Demo Balls")]
        [Tooltip("Shows where the ball will actually come and stop.")]
        [SerializeField] private bool showDemoBalls = true;
        
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        
        private List<GameObject> demoBalls = new List<GameObject>();
        private float lastUpdateTime = 0f;

        private void EnsureComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            if (meshRenderer != null && meshRenderer.sharedMaterial == null)
            {
                Material defaultMat = new Material(Shader.Find("Sprites/Default"));
                // Set the base material to white so it perfectly displays the per-vertex colors we assign
                defaultMat.color = Color.white; 
                meshRenderer.sharedMaterial = defaultMat;
            }
        }

        private void Update()
        {
            if (playerHead == null || activeProfile == null) return;
            
            EnsureComponents();

            // Limit rebuild rate in editor to save performance (every 0.5s)
            if (Time.realtimeSinceStartup - lastUpdateTime > 0.5f)
            {
                RebuildROMMesh();
                if (showDemoBalls) UpdateDemoBalls();
                else ClearDemoBalls();
                
                lastUpdateTime = Time.realtimeSinceStartup;
            }
        }

        private void RebuildROMMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "ROM_3D_Surface";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Color> colors = new List<Color>();

            Vector3 flatForward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;

            // Generate Right Arm 3D Surface
            if (showRightArm) 
            {
                Generate3DSurface(true, flatForward, vertices, triangles, colors, rightArmColor);
            }
            
            // Generate Left Arm 3D Surface
            if (showLeftArm) 
            {
                Generate3DSurface(false, flatForward, vertices, triangles, colors, leftArmColor);
            }

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            meshFilter.mesh = mesh;
        }

        private void Generate3DSurface(bool isRightSide, Vector3 flatForward, List<Vector3> verts, List<int> tris, List<Color> colors, Color surfaceColor)
        {
            Vector3 shoulderOffset = new Vector3(isRightSide ? 0.15f : -0.15f, -0.2f, 0f);
            Vector3 shoulderOrigin = playerHead.position + playerHead.TransformDirection(shoulderOffset);

            float maxReach = activeProfile.armLength > 0.1f ? activeProfile.armLength : 0.6f;
            float abductionAngle = activeProfile.maxAbduction > 5f ? activeProfile.maxAbduction : 45f;
            float adductionAngle = activeProfile.maxAdduction > 5f ? activeProfile.maxAdduction : 45f;
            
            // Clinical Flexion: 90 = forward, 180 = straight up.
            // Our Math: 0 = forward, 90 = straight up.
            // So we subtract 90 from the clinical value.
            float flexionAngle = (activeProfile.maxFlexion > 5f ? activeProfile.maxFlexion : 90f) - 90f;
            
            // Right arm: -adduction (across chest leftward) to +abduction (outward rightward)
            // Left arm: -abduction (outward leftward) to +adduction (across chest rightward)
            float startHorizAngle = isRightSide ? -adductionAngle : -abductionAngle;
            float endHorizAngle = isRightSide ? abductionAngle : adductionAngle;
            
            // Curve downwards slightly (-30) to represent bowling reach, up to maxFlexion
            float startVertAngle = -30f; 
            float endVertAngle = flexionAngle;

            int startIndex = verts.Count;

            // Generate Vertices in a 2D Grid (Latitude/Longitude)
            for (int v = 0; v <= verticalResolution; v++)
            {
                float vRatio = (float)v / verticalResolution;
                float currentVertAngle = Mathf.Lerp(startVertAngle, endVertAngle, vRatio);

                for (int h = 0; h <= horizontalResolution; h++)
                {
                    float hRatio = (float)h / horizontalResolution;
                    float currentHorizAngle = Mathf.Lerp(startHorizAngle, endHorizAngle, hRatio);

                    // 1. Rotate horizontally (Abduction)
                    Quaternion horizRot = Quaternion.AngleAxis(currentHorizAngle, Vector3.up);
                    Vector3 rotatedForward = horizRot * flatForward;
                    
                    // 2. Determine local right axis for vertical rotation
                    Vector3 rotatedRight = Vector3.Cross(Vector3.up, rotatedForward).normalized;
                    
                    // 3. Rotate vertically (Flexion/Elevation)
                    Quaternion vertRot = Quaternion.AngleAxis(-currentVertAngle, rotatedRight);
                    
                    // 4. Calculate final direction and position
                    Vector3 dir = (vertRot * rotatedForward).normalized;
                    Vector3 point = shoulderOrigin + dir * maxReach;
                    
                    // Add vertex in local space
                    verts.Add(transform.InverseTransformPoint(point));
                    
                    // Assign the specific arm color to this vertex
                    colors.Add(surfaceColor);
                }
            }

            // Generate Triangles (Quads)
            for (int v = 0; v < verticalResolution; v++)
            {
                for (int h = 0; h < horizontalResolution; h++)
                {
                    int current = startIndex + (v * (horizontalResolution + 1)) + h;
                    int next = current + 1;
                    int up = current + (horizontalResolution + 1);
                    int upNext = up + 1;

                    // Front face
                    tris.Add(current);
                    tris.Add(up);
                    tris.Add(next);

                    tris.Add(next);
                    tris.Add(up);
                    tris.Add(upNext);
                    
                    // Back face (so it renders from both sides)
                    tris.Add(current);
                    tris.Add(next);
                    tris.Add(up);

                    tris.Add(next);
                    tris.Add(upNext);
                    tris.Add(up);
                }
            }
        }

        private void UpdateDemoBalls()
        {
            ClearDemoBalls();

            Vector3 flatForward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;

            float maxReach = activeProfile.armLength > 0.1f ? activeProfile.armLength : 0.6f;
            float targetReach = maxReach * safetyRatio;
            float abductionAngle = activeProfile.maxAbduction > 5f ? activeProfile.maxAbduction : 45f;
            float adductionAngle = activeProfile.maxAdduction > 5f ? activeProfile.maxAdduction : 45f;
            
            // Apply the same clinical-to-local math conversion for the demo balls
            float flexionAngle = (activeProfile.maxFlexion > 5f ? activeProfile.maxFlexion : 90f) - 90f;

            // Target 1: Floor (Bowling) Center
            CreateDemoBall(false, 0f, -25f, flatForward, targetReach);
            
            // Only spawn left targets if Left Arm is shown
            if (showLeftArm)
            {
                CreateDemoBall(false, -abductionAngle / 1.5f, flexionAngle / 2f, flatForward, targetReach); // Outward
                CreateDemoBall(false, adductionAngle / 1.5f, flexionAngle / 2f, flatForward, targetReach); // Across Chest
            }
            
            // Only spawn right targets if Right Arm is shown
            if (showRightArm)
            {
                CreateDemoBall(true, abductionAngle / 1.5f, flexionAngle / 2f, flatForward, targetReach); // Outward
                CreateDemoBall(true, -adductionAngle / 1.5f, flexionAngle / 2f, flatForward, targetReach); // Across Chest
            }
            
            // Target 4: High Hoop (Center)
            CreateDemoBall(true, 0f, flexionAngle, flatForward, targetReach);
        }

        private void CreateDemoBall(bool isRightSide, float horizAngle, float vertAngle, Vector3 flatForward, float reach)
        {
            Vector3 shoulderOffset = new Vector3(isRightSide ? 0.15f : -0.15f, -0.2f, 0f);
            Vector3 shoulderOrigin = playerHead.position + playerHead.TransformDirection(shoulderOffset);

            Quaternion horizRot = Quaternion.AngleAxis(horizAngle, Vector3.up);
            Vector3 rotatedForward = horizRot * flatForward;
            Vector3 rotatedRight = Vector3.Cross(Vector3.up, rotatedForward).normalized;
            Quaternion vertRot = Quaternion.AngleAxis(-vertAngle, rotatedRight);
            
            Vector3 dir = (vertRot * rotatedForward).normalized;
            Vector3 targetPoint = shoulderOrigin + dir * reach;

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "DemoBall (Don't Save)";
            ball.transform.position = targetPoint;
            ball.transform.localScale = Vector3.one * 0.15f;
            
            ball.hideFlags = HideFlags.HideAndDontSave;
            
            MeshRenderer renderer = ball.GetComponent<MeshRenderer>();
            Material yellowMat = new Material(Shader.Find("Sprites/Default"));
            yellowMat.color = Color.yellow;
            renderer.sharedMaterial = yellowMat;

            DestroyImmediate(ball.GetComponent<Collider>());
            demoBalls.Add(ball);
        }

        private void ClearDemoBalls()
        {
            foreach (var ball in demoBalls)
            {
                if (ball != null) DestroyImmediate(ball);
            }
            demoBalls.Clear();
        }

        private void OnDisable()
        {
            ClearDemoBalls();
        }

        private void OnDestroy()
        {
            ClearDemoBalls();
        }
    }
}
