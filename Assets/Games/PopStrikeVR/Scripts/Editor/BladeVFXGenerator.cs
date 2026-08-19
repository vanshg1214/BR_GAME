using UnityEngine;
using UnityEditor;

namespace PopstrikeVR.EditorTools
{
    public class BladeVFXGenerator : EditorWindow
    {
        [MenuItem("PopStrikeVR/VFX/Build Blade Slash VFX")]
        public static void ShowWindow()
        {
            GetWindow<BladeVFXGenerator>("Blade Slash Builder").Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Blade Slash (Lightning) VFX", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Generates the Blue Blade Slash Lightning & Spark VFX matching the GDD:\n" +
                "  ✔  Bright blue-white lightning billboard flash\n" +
                "  ✔  High velocity electric blue spark fragments\n" +
                "  ✔  Quest/Android safe Sprites/Default additive materials\n" +
                "  ✔  Zero drag-and-drop required!",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("⚡  Generate Blade Slash VFX", GUILayout.Height(50)))
            {
                GeneratePrefab();
            }
        }

        public static void GeneratePrefab()
        {
            string prefabFolder = "Assets/Games/PopStrikeVR/Prefabs/VFX";
            string matFolder    = "Assets/Games/PopStrikeVR/Materials/VFX";
            System.IO.Directory.CreateDirectory(prefabFolder);
            System.IO.Directory.CreateDirectory(matFolder);
            AssetDatabase.Refresh();

            string prefabPath = prefabFolder + "/VFX_BladeLightning.prefab";

            // 1. Create materials
            Material lightningMat = CreateAdditiveMaterial($"{matFolder}/BladeLightning_Mat.mat", new Color(0f, 0.8f, 1f, 1f));
            Material sparkMat = CreateAdditiveMaterial($"{matFolder}/BladeSpark_Mat.mat", new Color(0f, 0.9f, 1f, 1f));

            // Create root object
            GameObject root = new GameObject("VFX_BladeLightning");

            // Setup Particle System
            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = root.GetComponent<ParticleSystemRenderer>();
            
            var main = ps.main;
            main.duration = 1.0f;
            main.loop = false;
            // Short lifetime for a fast slash
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f); // Stays in place, expands via size
            main.startSize = new ParticleSystem.MinMaxCurve(2.0f, 3.0f);
            main.startColor = Color.white; // Handled by HDR material color
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 1) });

            var shape = ps.shape;
            shape.enabled = false; // Disable shape, just spawn at center

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0f, 0.8f, 1f), 0.2f), new GradientColorKey(Color.blue, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.7f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curveX = new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(0.2f, 1.0f), new Keyframe(1.0f, 1.2f));
            AnimationCurve curveY = new AnimationCurve(new Keyframe(0.0f, 0.1f), new Keyframe(0.2f, 0.3f), new Keyframe(1.0f, 0.0f));
            AnimationCurve curveZ = new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 1.0f));
            
            sizeOverLifetime.separateAxes = true;
            sizeOverLifetime.x = new ParticleSystem.MinMaxCurve(1.0f, curveX);
            sizeOverLifetime.y = new ParticleSystem.MinMaxCurve(1.0f, curveY);
            sizeOverLifetime.z = new ParticleSystem.MinMaxCurve(1.0f, curveZ);

            psr.material = lightningMat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            
            // Add a secondary spark explosion
            GameObject sparks = new GameObject("Sparks");
            sparks.transform.SetParent(root.transform);
            sparks.transform.localPosition = Vector3.zero;
            
            ParticleSystem sparkPs = sparks.AddComponent<ParticleSystem>();
            ParticleSystemRenderer sparkPsr = sparks.GetComponent<ParticleSystemRenderer>();
            
            var sparkMain = sparkPs.main;
            sparkMain.duration = 1.0f;
            sparkMain.loop = false;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(5.0f, 12.0f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            sparkMain.startColor = Color.white;
            sparkMain.playOnAwake = true;
            
            var sparkEmission = sparkPs.emission;
            sparkEmission.rateOverTime = 0;
            sparkEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, new ParticleSystem.MinMaxCurve(15, 30)) });
            
            var sparkShape = sparkPs.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = 0.1f;
            
            var sparkColor = sparkPs.colorOverLifetime;
            sparkColor.enabled = true;
            Gradient sparkGrad = new Gradient();
            sparkGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0f, 0.5f, 1f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            sparkColor.color = sparkGrad;
            
            sparkPsr.material = sparkMat;
            sparkPsr.renderMode = ParticleSystemRenderMode.Stretch;
            sparkPsr.velocityScale = 0.05f;
            sparkPsr.lengthScale = 2.0f;

            // Save as Prefab
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            Debug.Log($"<color=cyan>Successfully generated Clean Anime Slash VFX at {prefabPath}!</color>");
            Selection.activeObject = savedPrefab;
            EditorUtility.FocusProjectWindow();
            EditorUtility.DisplayDialog("Success", $"Blade Slash VFX generated successfully!\n\nSaved at: {prefabPath}", "Awesome");
        }

        private static Material CreateAdditiveMaterial(string path, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Sprites/Default"));
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;

            // Boost color HDR for glowing/bloom effect
            mat.SetColor("_Color", color * 2.5f);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
