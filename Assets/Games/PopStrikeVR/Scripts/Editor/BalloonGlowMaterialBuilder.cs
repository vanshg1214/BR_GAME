using UnityEngine;
using UnityEditor;

namespace PopstrikeVR.EditorTools
{
    /// <summary>
    /// One-click Editor tool to generate a vivid glowing material for the Trail Balloon.
    /// Go to: PopStrikeVR -> Materials -> Build Balloon Glow Material
    /// </summary>
    public class BalloonGlowMaterialBuilder : EditorWindow
    {
        // --- Settings ---
        private Color glowColor     = new Color(0.0f, 1.0f, 0.5f, 1f); // Bright cyan-green default
        private float glowIntensity = 3.0f;   // HDR multiplier (>1 = physically glows in URP)
        private float transparency  = 0.35f;  // 0 = fully transparent, 1 = fully opaque
        private string materialName = "BalloonGlow_Connected";

        [MenuItem("PopStrikeVR/Materials/Build Balloon Glow Material")]
        public static void ShowWindow()
        {
            GetWindow<BalloonGlowMaterialBuilder>("Balloon Glow Builder");
        }

        private void OnGUI()
        {
            GUILayout.Label("Balloon Glow Material Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates a vivid glowing URP material for the Trail Balloon's 'Connected' state.\n" +
                "Uses HDR Emission so Bloom will make it physically glow in the scene.", MessageType.Info);

            GUILayout.Space(8);

            materialName  = EditorGUILayout.TextField("Material Name", materialName);
            glowColor     = EditorGUILayout.ColorField(new GUIContent("Glow Colour"), glowColor, true, true, true); // HDR picker
            glowIntensity = EditorGUILayout.Slider("Glow Intensity (HDR)", glowIntensity, 1f, 8f);
            transparency  = EditorGUILayout.Slider("Balloon Transparency", transparency, 0f, 1f);

            GUILayout.Space(12);
            EditorGUILayout.HelpBox("Tip: Set Glow Intensity to 3-5 for best results with Post-Processing Bloom.", MessageType.None);
            GUILayout.Space(8);

            if (GUILayout.Button("Generate Glow Material", GUILayout.Height(40)))
            {
                GenerateMaterial();
            }
        }

        private void GenerateMaterial()
        {
            // -------------------------------------------------------
            // 1. Find the correct URP shader
            // -------------------------------------------------------
            // Try URP Lit first, fall back to URP Unlit
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Standard"); // Built-in fallback

            if (shader == null)
            {
                EditorUtility.DisplayDialog("Shader Not Found",
                    "Could not find a URP or Standard shader. Make sure URP is installed.", "OK");
                return;
            }

            // -------------------------------------------------------
            // 2. Create the Material
            // -------------------------------------------------------
            Material mat = new Material(shader);
            mat.name = materialName;

            // -------------------------------------------------------
            // 3. Configure Surface — Transparent so the balloon stays see-through
            // -------------------------------------------------------
            if (shader.name.Contains("Lit"))
            {
                // URP Lit transparent setup
                mat.SetFloat("_Surface", 1f);                            // 1 = Transparent
                mat.SetFloat("_Blend", 0f);                              // Alpha blend mode
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetShaderPassEnabled("ShadowCaster", false);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHABLEND_ON");

                // Base colour with transparency
                Color baseCol = glowColor;
                baseCol.a = transparency;
                mat.SetColor("_BaseColor", baseCol);

                // -------------------------------------------------------
                // 4. Enable Emission (this is what makes it GLOW)
                // -------------------------------------------------------
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                // HDR emission colour — multiply by intensity so Bloom picks it up
                Color hdrEmission = new Color(
                    glowColor.r * glowIntensity,
                    glowColor.g * glowIntensity,
                    glowColor.b * glowIntensity,
                    1f
                );
                mat.SetColor("_EmissionColor", hdrEmission);

                // Smooth, slightly glossy surface so the glow reflects nicely
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.6f);
            }
            else if (shader.name.Contains("Unlit"))
            {
                // URP Unlit transparent setup — always bright regardless of lighting
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                Color baseCol = new Color(
                    glowColor.r * glowIntensity,
                    glowColor.g * glowIntensity,
                    glowColor.b * glowIntensity,
                    transparency
                );
                mat.SetColor("_BaseColor", baseCol);
            }
            else
            {
                // Standard shader fallback
                mat.SetFloat("_Mode", 3f); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                Color baseCol = glowColor;
                baseCol.a = transparency;
                mat.SetColor("_Color", baseCol);

                mat.EnableKeyword("_EMISSION");
                Color hdrEmission = new Color(
                    glowColor.r * glowIntensity,
                    glowColor.g * glowIntensity,
                    glowColor.b * glowIntensity, 1f);
                mat.SetColor("_EmissionColor", hdrEmission);
            }

            // -------------------------------------------------------
            // 5. Save to disk
            // -------------------------------------------------------
            string dir = "Assets/Games/PopStrikeVR/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/Games/PopStrikeVR"))
                AssetDatabase.CreateFolder("Assets/Games", "PopStrikeVR");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Games/PopStrikeVR", "Materials");

            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{materialName}.mat");
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Highlight it in the Project window
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = mat;

            EditorUtility.DisplayDialog("Success!",
                $"Glow Material created at:\n{path}\n\n" +
                "1. Assign this to your TrailBalloon prefab's 'Connected Material' slot.\n" +
                "2. Make sure Post-Processing Bloom is enabled in your URP Camera for the glow to shine!\n" +
                "3. You can change the colour anytime by selecting the material and adjusting Emission Color.",
                "Awesome!");
        }
    }
}
