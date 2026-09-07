using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

namespace Rehab.Volleyball.Editor
{
    /// <summary>
    /// Editor window tool that generates a 3D volumetric dashed ring mesh asset, material,
    /// and instantiates a fully configured GameObject in the active scene.
    /// Access via the top menu bar: Tools > Volleyball > Dashed Ring Generator.
    /// </summary>
    #if UNITY_EDITOR
    public class VolleyballRingGeneratorWindow : EditorWindow
    {
        private float innerRadius = 0.42f;
        private float outerRadius = 0.50f;
        private float ringHeight = 0.03f; // 3cm depth
        private int numSegments = 8;
        private float gapFraction = 0.25f;
        private int subdivisionsPerDash = 8;
        private Color ringColor = Color.yellow;
        private string savePath = "Assets/Games/VolleyBall/Dashed3DRingMesh.asset";

        [MenuItem("Tools/Volleyball/Dashed Ring Generator")]
        public static void ShowWindow()
        {
            GetWindow<VolleyballRingGeneratorWindow>("Ring Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("3D Volumetric Dashed Ring Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            innerRadius = EditorGUILayout.FloatField("Inner Radius (meters)", innerRadius);
            outerRadius = EditorGUILayout.FloatField("Outer Radius (meters)", outerRadius);
            ringHeight = EditorGUILayout.FloatField("Ring Depth/Height (meters)", ringHeight);
            numSegments = EditorGUILayout.IntSlider("Number of Segments", numSegments, 3, 24);
            gapFraction = EditorGUILayout.Slider("Gap Fraction (Gaps size)", gapFraction, 0.01f, 0.99f);
            subdivisionsPerDash = EditorGUILayout.IntSlider("Curvature Smoothness", subdivisionsPerDash, 2, 30);
            ringColor = EditorGUILayout.ColorField("Ring Color", ringColor);
            savePath = EditorGUILayout.TextField("Asset Save Path", savePath);

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate and Spawn 3D Dashed Ring", GUILayout.Height(30)))
            {
                GenerateAndSpawn();
            }
        }

        private void GenerateAndSpawn()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Procedural_Dashed_3D_Ring";

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            float segmentAngle = 360f / numSegments;
            float dashAngle = segmentAngle * (1f - gapFraction);
            float halfHeight = ringHeight * 0.5f;

            for (int i = 0; i < numSegments; i++)
            {
                float startAngleDeg = i * segmentAngle;
                float endAngleDeg = startAngleDeg + dashAngle;

                // To ensure flat shading and sharp 3D edges, we generate separate vertex sets for each face.

                // ─────────────────────────────────────────────────────────────
                // 1. TOP FACE (Y = +halfHeight, facing up)
                // ─────────────────────────────────────────────────────────────
                int topStartIdx = vertices.Count;
                for (int s = 0; s <= subdivisionsPerDash; s++)
                {
                    float angleDeg = startAngleDeg + (s / (float)subdivisionsPerDash) * dashAngle;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angleRad);
                    float sin = Mathf.Sin(angleRad);

                    vertices.Add(new Vector3(cos * innerRadius, halfHeight, sin * innerRadius)); // inner
                    vertices.Add(new Vector3(cos * outerRadius, halfHeight, sin * outerRadius)); // outer

                    float u = s / (float)subdivisionsPerDash;
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, 1f));

                    if (s > 0)
                    {
                        int prevInner = topStartIdx + (s - 1) * 2;
                        int prevOuter = prevInner + 1;
                        int currInner = topStartIdx + s * 2;
                        int currOuter = currInner + 1;

                        // Winding: Clockwise looking from above (+Y)
                        triangles.Add(prevInner);
                        triangles.Add(currInner);
                        triangles.Add(prevOuter);

                        triangles.Add(prevOuter);
                        triangles.Add(currInner);
                        triangles.Add(currOuter);
                    }
                }

                // ─────────────────────────────────────────────────────────────
                // 2. BOTTOM FACE (Y = -halfHeight, facing down)
                // ─────────────────────────────────────────────────────────────
                int botStartIdx = vertices.Count;
                for (int s = 0; s <= subdivisionsPerDash; s++)
                {
                    float angleDeg = startAngleDeg + (s / (float)subdivisionsPerDash) * dashAngle;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angleRad);
                    float sin = Mathf.Sin(angleRad);

                    vertices.Add(new Vector3(cos * innerRadius, -halfHeight, sin * innerRadius)); // inner
                    vertices.Add(new Vector3(cos * outerRadius, -halfHeight, sin * outerRadius)); // outer

                    float u = s / (float)subdivisionsPerDash;
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, 1f));

                    if (s > 0)
                    {
                        int prevInner = botStartIdx + (s - 1) * 2;
                        int prevOuter = prevInner + 1;
                        int currInner = botStartIdx + s * 2;
                        int currOuter = currInner + 1;

                        // Winding: Clockwise looking from below (-Y) -> counter-clockwise from above
                        triangles.Add(prevInner);
                        triangles.Add(prevOuter);
                        triangles.Add(currInner);

                        triangles.Add(prevOuter);
                        triangles.Add(currOuter);
                        triangles.Add(currInner);
                    }
                }

                // ─────────────────────────────────────────────────────────────
                // 3. OUTER WALL (facing outwards)
                // ─────────────────────────────────────────────────────────────
                int outStartIdx = vertices.Count;
                for (int s = 0; s <= subdivisionsPerDash; s++)
                {
                    float angleDeg = startAngleDeg + (s / (float)subdivisionsPerDash) * dashAngle;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angleRad);
                    float sin = Mathf.Sin(angleRad);

                    vertices.Add(new Vector3(cos * outerRadius, halfHeight, sin * outerRadius));  // top outer
                    vertices.Add(new Vector3(cos * outerRadius, -halfHeight, sin * outerRadius)); // bottom outer

                    float u = s / (float)subdivisionsPerDash;
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, 1f));

                    if (s > 0)
                    {
                        int prevTop = outStartIdx + (s - 1) * 2;
                        int prevBot = prevTop + 1;
                        int currTop = outStartIdx + s * 2;
                        int currBot = currTop + 1;

                        // Winding: Clockwise from outside
                        triangles.Add(currTop);
                        triangles.Add(prevTop);
                        triangles.Add(prevBot);

                        triangles.Add(currTop);
                        triangles.Add(prevBot);
                        triangles.Add(currBot);
                    }
                }

                // ─────────────────────────────────────────────────────────────
                // 4. INNER WALL (facing inwards)
                // ─────────────────────────────────────────────────────────────
                int inStartIdx = vertices.Count;
                for (int s = 0; s <= subdivisionsPerDash; s++)
                {
                    float angleDeg = startAngleDeg + (s / (float)subdivisionsPerDash) * dashAngle;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angleRad);
                    float sin = Mathf.Sin(angleRad);

                    vertices.Add(new Vector3(cos * innerRadius, halfHeight, sin * innerRadius));  // top inner
                    vertices.Add(new Vector3(cos * innerRadius, -halfHeight, sin * innerRadius)); // bottom inner

                    float u = s / (float)subdivisionsPerDash;
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, 1f));

                    if (s > 0)
                    {
                        int prevTop = inStartIdx + (s - 1) * 2;
                        int prevBot = prevTop + 1;
                        int currTop = inStartIdx + s * 2;
                        int currBot = currTop + 1;

                        // Winding: Clockwise from inside center
                        triangles.Add(prevTop);
                        triangles.Add(currTop);
                        triangles.Add(currBot);

                        triangles.Add(prevTop);
                        triangles.Add(currBot);
                        triangles.Add(prevBot);
                    }
                }

                // ─────────────────────────────────────────────────────────────
                // 5. START CAP (At s = 0, facing clockwise backward)
                // ─────────────────────────────────────────────────────────────
                {
                    float angleRad = startAngleDeg * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angleRad);
                    float sin = Mathf.Sin(angleRad);

                    int capStart = vertices.Count;
                    vertices.Add(new Vector3(cos * innerRadius, halfHeight, sin * innerRadius));  // top inner (0)
                    vertices.Add(new Vector3(cos * outerRadius, halfHeight, sin * outerRadius));  // top outer (1)
                    vertices.Add(new Vector3(cos * innerRadius, -halfHeight, sin * innerRadius)); // bot inner (2)
                    vertices.Add(new Vector3(cos * outerRadius, -halfHeight, sin * outerRadius)); // bot outer (3)

                    uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                    uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));

                    // Winding: Clockwise looking at the start face from outside
                    triangles.Add(capStart + 0);
                    triangles.Add(capStart + 1);
                    triangles.Add(capStart + 3);

                    triangles.Add(capStart + 0);
                    triangles.Add(capStart + 3);
                    triangles.Add(capStart + 2);
                }

                // ─────────────────────────────────────────────────────────────
                // 6. END CAP (At s = subdivisions, facing counter-clockwise forward)
                // ─────────────────────────────────────────────────────────────
                {
                    float angleRad = endAngleDeg * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angleRad);
                    float sin = Mathf.Sin(angleRad);

                    int capEnd = vertices.Count;
                    vertices.Add(new Vector3(cos * innerRadius, halfHeight, sin * innerRadius));  // top inner (0)
                    vertices.Add(new Vector3(cos * outerRadius, halfHeight, sin * outerRadius));  // top outer (1)
                    vertices.Add(new Vector3(cos * innerRadius, -halfHeight, sin * innerRadius)); // bot inner (2)
                    vertices.Add(new Vector3(cos * outerRadius, -halfHeight, sin * outerRadius)); // bot outer (3)

                    uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                    uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));

                    // Winding: Clockwise looking at the end face from outside
                    triangles.Add(capEnd + 1);
                    triangles.Add(capEnd + 0);
                    triangles.Add(capEnd + 2);

                    triangles.Add(capEnd + 1);
                    triangles.Add(capEnd + 2);
                    triangles.Add(capEnd + 3);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Save Mesh to Project
            string directory = System.IO.Path.GetDirectoryName(savePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(mesh, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Create GameObject in scene
            GameObject ringGo = new GameObject("Volleyball3DDashedRing");
            MeshFilter filter = ringGo.AddComponent<MeshFilter>();
            MeshRenderer renderer = ringGo.AddComponent<MeshRenderer>();

            filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(savePath);

            // Create default material using Sprites/Default (so it supports tinting & alpha transparency)
            Shader defaultShader = Shader.Find("Sprites/Default");
            Material ringMat = defaultShader != null ? new Material(defaultShader) : new Material(Shader.Find("Unlit/Color"));
            ringMat.color = ringColor;

            // Save material in the same directory as the mesh
            string matPath = savePath.Replace(".asset", "_Material.mat");
            AssetDatabase.CreateAsset(ringMat, matPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            // Register Undo so the user can easily undo this generation step
            Undo.RegisterCreatedObjectUndo(ringGo, "Create 3D Dashed Ring");
            Selection.activeGameObject = ringGo;

            Debug.Log($"[VolleyballDashedRing] Successfully spawned 3D volumetric ring in scene! Mesh saved to: '{savePath}' and Material saved to: '{matPath}'");
        }
    }
    #endif
}
