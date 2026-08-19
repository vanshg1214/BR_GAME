using UnityEngine;
using UnityEditor;

namespace PopstrikeVR.EditorTools
{
    /// <summary>
    /// Generates a single-system balloon dissolve VFX for Trail Balloons.
    /// Particles are emitted FROM THE SURFACE OF THE BALLOON MESH,
    /// then slowly drift upward and fade out — as if the balloon is dissolving in air.
    /// </summary>
    public class TrailBalloonDissolveVFXBuilder : EditorWindow
    {
        private Mesh balloonMesh;

        [MenuItem("PopStrikeVR/VFX/Build Trail Balloon Dissolve VFX")]
        public static void ShowWindow()
        {
            GetWindow<TrailBalloonDissolveVFXBuilder>("Trail Dissolve Builder").Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Trail Balloon — Dissolve VFX", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Creates a single particle system where:\n\n" +
                "  • Particles spawn across the surface of your balloon mesh\n" +
                "  • They slowly drift upward with gentle turbulence\n" +
                "  • They shrink and fade away — balloon dissolves in air\n\n" +
                "Assign your balloon mesh below, then click Generate.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            balloonMesh = (Mesh)EditorGUILayout.ObjectField(
                "Balloon Mesh", balloonMesh, typeof(Mesh), false);
            EditorGUILayout.Space(6);

            bool canGenerate = balloonMesh != null;
            if (!canGenerate)
            {
                EditorGUILayout.HelpBox("Please assign the Balloon Mesh above first.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!canGenerate))
            {
                if (GUILayout.Button("✨  Generate Dissolve VFX", GUILayout.Height(50)))
                {
                    GenerateVFX();
                }
            }
        }

        private void GenerateVFX()
        {
            string prefabFolder = "Assets/Games/PopStrikeVR/Prefabs/VFX";
            string matFolder    = "Assets/Games/PopStrikeVR/Materials/VFX";
            System.IO.Directory.CreateDirectory(prefabFolder);
            System.IO.Directory.CreateDirectory(matFolder);
            AssetDatabase.Refresh();

            // ── MATERIAL ──────────────────────────────────────────────────────
            // Additive white/silver sparkle — same as your Sparkles layer
            string matPath = $"{matFolder}/TrailDissolve_Sparkle_Mat.mat";
            Material sparkleMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (sparkleMat == null)
            {
                sparkleMat = new Material(Shader.Find("Sprites/Default"));
                sparkleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sparkleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive
                sparkleMat.SetInt("_ZWrite", 0);
                sparkleMat.renderQueue = 3100;
                // Warm silver-white glow, slightly gold tinted
                Color hdr = new Color(1f, 0.96f, 0.82f, 1f) * 2.5f;
                hdr.a = 1f;
                sparkleMat.SetColor("_Color", hdr);
                AssetDatabase.CreateAsset(sparkleMat, matPath);
                AssetDatabase.SaveAssets();
            }

            // ── PARTICLE SYSTEM ───────────────────────────────────────────────
            GameObject root = new GameObject("TrailBalloonDissolve_VFX");
            ParticleSystem ps = root.AddComponent<ParticleSystem>();

            // MAIN MODULE
            var main = ps.main;
            main.duration          = 1.2f;
            main.loop              = false;
            main.startDelay        = 0f;
            // Each particle lives 0.8–1.8s giving a staggered dissolve feel
            main.startLifetime     = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            // Very slow outward speed — they barely leave the surface
            main.startSpeed        = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
            // Tiny sparkle dots: 3–7mm — matches the Sparkles layer you saw
            main.startSize         = new ParticleSystem.MinMaxCurve(0.003f, 0.007f);
            main.startRotation3D   = false;
            // Silver/warm-white color variation
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.9f, 0.88f, 0.78f, 1f),
                new Color(1f, 1f, 1f, 1f)
            );
            // Slight negative gravity → natural upward drift
            main.gravityModifier   = -0.12f;
            main.simulationSpace   = ParticleSystemSimulationSpace.World;
            main.playOnAwake       = true;
            // Auto-destroy the GameObject when the system finishes
            main.stopAction        = ParticleSystemStopAction.Destroy;

            // EMISSION — one short burst that fills the mesh surface
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                // 120–160 particles is enough to visually coat a 10cm balloon
                new ParticleSystem.Burst(0f, 120, 160, 1, 0.05f)
            });

            // SHAPE — balloon mesh surface
            // This makes every particle spawn ON the surface of your balloon
            var shape = ps.shape;
            shape.shapeType    = ParticleSystemShapeType.Mesh;
            shape.mesh         = balloonMesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.useMeshColors = false;
            shape.normalOffset  = 0.0f;     // Emit from the surface itself, not inside
            shape.randomDirectionAmount = 0.3f; // Slight random direction for natural scatter

            // VELOCITY OVER LIFETIME — slow upward drift + gentle random turbulence
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.World;
            // Drift upward gently
            vel.y = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            // Very subtle random sideways drift — not symmetrical, feels organic
            vel.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            // SIZE OVER LIFETIME — hold for a moment, then shrink to zero
            var sizeOL = ps.sizeOverLifetime;
            sizeOL.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(new Keyframe(0.00f, 0.8f));   // Start slightly below full
            sizeCurve.AddKey(new Keyframe(0.15f, 1.0f));   // Pop to full size quickly
            sizeCurve.AddKey(new Keyframe(0.55f, 0.9f));   // Hold...
            sizeCurve.AddKey(new Keyframe(1.00f, 0.0f));   // Shrink to nothing
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // COLOR OVER LIFETIME — gentle twinkle + fade out
            var colOL = ps.colorOverLifetime;
            colOL.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.80f), 0.00f), // Warm gold at birth
                    new GradientColorKey(new Color(1f, 1f, 1f),       0.30f), // Brighten to white
                    new GradientColorKey(new Color(0.85f, 0.90f, 1f), 0.70f), // Cool slightly
                    new GradientColorKey(new Color(0.85f, 0.90f, 1f), 1.00f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.00f), // Fade in at start
                    new GradientAlphaKey(1.0f, 0.12f), // Fully visible quickly
                    new GradientAlphaKey(0.9f, 0.50f), // Gentle hold
                    new GradientAlphaKey(0.0f, 1.00f)  // Fade out at end
                }
            );
            colOL.color = grad;

            // NOISE — subtle random turbulence makes the drift feel natural, not mechanical
            var noise = ps.noise;
            noise.enabled        = true;
            noise.strength       = 0.04f;   // Very subtle — just enough to break symmetry
            noise.frequency      = 0.8f;
            noise.scrollSpeed    = 0.3f;
            noise.damping        = true;
            noise.quality        = ParticleSystemNoiseQuality.Medium;

            // RENDERER
            var rend = root.GetComponent<ParticleSystemRenderer>();
            rend.renderMode  = ParticleSystemRenderMode.Billboard;
            rend.material    = sparkleMat;
            rend.sortingFudge = -10f; // Draw on top of the balloon geometry

            // ── SAVE PREFAB ───────────────────────────────────────────────────
            string savePath = $"{prefabFolder}/TrailBalloonDissolve_VFX.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            DestroyImmediate(root);

            Debug.Log($"<color=silver><b>[TrailDissolveVFX]</b></color> Saved → {savePath}");
            Selection.activeObject = savedPrefab;
            EditorUtility.FocusProjectWindow();
            EditorUtility.DisplayDialog("Done!",
                $"Balloon Dissolve VFX created!\n\n" +
                $"Particles spawn from your balloon mesh surface,\n" +
                $"then slowly drift upward and fade away.\n\n" +
                $"Saved at:\n{savePath}\n\n" +
                $"Drag this prefab into:\nFeedbackManager → Trail Confetti VFX",
                "Great!");
        }
    }
}
