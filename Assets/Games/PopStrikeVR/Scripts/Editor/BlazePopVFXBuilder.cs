using UnityEngine;
using UnityEditor;

namespace PopstrikeVR.EditorTools
{
    public class BlazePopVFXBuilder : EditorWindow
    {
        [MenuItem("PopStrikeVR/VFX/Build Blaze Balloon Pop VFX")]
        public static void ShowWindow()
        {
            GetWindow<BlazePopVFXBuilder>("Blaze Pop VFX Builder").Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Blaze Balloon Explosion VFX (GDD Compliant)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Generates a premium explosion VFX matching the GDD:\n" +
                "  ✔  Orange-gold stretched confetti shards with gravity\n" +
                "  ✔  Additive glowing flame ring that EXPANDS outward\n" +
                "  ✔  Central bright flash burst\n" +
                "  ✔  Expanding shockwave ring (not a flat circle)\n" +
                "  ✔  Floating ember sparks rising upward\n" +
                "  ✔  Full fade-out in 0.4s\n\n" +
                "All materials are auto-created with ADDITIVE blending for glow!",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("⚡  Generate Blaze Pop VFX (Full Quality)", GUILayout.Height(50)))
            {
                GenerateVFX();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // MASTER ENTRY POINT
        // ─────────────────────────────────────────────────────────────────────
        private void GenerateVFX()
        {
            string prefabFolder = "Assets/Games/PopStrikeVR/Prefabs/VFX";
            string matFolder    = "Assets/Games/PopStrikeVR/Materials/VFX";
            System.IO.Directory.CreateDirectory(prefabFolder);
            System.IO.Directory.CreateDirectory(matFolder);
            AssetDatabase.Refresh();

            // ── Materials ────────────────────────────────────────────────────
            // ADDITIVE = particles blend additively so they GLOW through each other
            Material shardMat     = CreateAdditiveMaterial($"{matFolder}/BlazeShard_Mat.mat",     new Color(1f, 0.65f, 0.0f));
            Material flameMat     = CreateAdditiveMaterial($"{matFolder}/BlazeFlame_Mat.mat",     new Color(1f, 0.35f, 0.0f));
            Material flashMat     = CreateAdditiveMaterial($"{matFolder}/BlazeFlash_Mat.mat",     new Color(1f, 0.85f, 0.4f));
            Material shockMat     = CreateAdditiveMaterial($"{matFolder}/BlazeShockwave_Mat.mat", new Color(1f, 0.6f,  0.2f));
            Material emberMat     = CreateAdditiveMaterial($"{matFolder}/BlazeEmber_Mat.mat",     new Color(1f, 0.4f,  0.05f));

            // ── Root Particle System ─────────────────────────────────────────
            GameObject root = new GameObject("VFX_BlazePop");
            var rootPS = root.AddComponent<ParticleSystem>();
            var rootMain     = rootPS.main;
            rootMain.duration    = 1.2f;
            rootMain.loop        = false;
            rootMain.playOnAwake = true;
            rootMain.stopAction  = ParticleSystemStopAction.Destroy;
            var emission = rootPS.emission;
            emission.enabled = false;
            var shape = rootPS.shape;
            shape.enabled = false;

            // ── Sub-Systems ──────────────────────────────────────────────────
            BuildCentralFlash(root.transform, flashMat);     // Instant bright white-orange flash at center
            BuildShards(root.transform, shardMat);            // 60 stretched confetti shards with gravity
            BuildFlameRing(root.transform, flameMat);         // Ring of flames expanding outward
            BuildShockwaveRing(root.transform, shockMat, false); // Horizontal splashing ring disc
            BuildShockwaveRing(root.transform, shockMat, true);  // Vertical splashing ring disc
            BuildExpandingHaloRing(root.transform, flashMat, false); // Horizontal expanding dome ring
            BuildExpandingHaloRing(root.transform, flashMat, true);  // Vertical expanding dome ring
            BuildEmbers(root.transform, emberMat);            // Small sparks that float upward

            // ── Save Prefab ──────────────────────────────────────────────────
            string savePath    = $"{prefabFolder}/VFX_BlazePop.prefab";
            var    savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            DestroyImmediate(root);

            Debug.Log($"<color=orange><b>[BlazeVFX]</b></color> Saved → {savePath}");
            Selection.activeObject = savedPrefab;
            EditorUtility.FocusProjectWindow();
        }

        // ─────────────────────────────────────────────────────────────────────
        // MATERIAL FACTORY  –  Additive blending = Glow!
        // ─────────────────────────────────────────────────────────────────────
        private Material CreateAdditiveMaterial(string path, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Sprites/Default"));
            // Additive blend: dst = src + dst  ─→  bright pixels ADD together = GLOW
            mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.One);  // ONE = additive
            mat.SetInt("_ZWrite",    0);
            mat.renderQueue = 3000; // Transparent queue

            // Boost color HDR so Bloom picks it up
            mat.SetColor("_Color", color * 2.5f);
            mat.EnableKeyword("_EMISSION");

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. CENTRAL FLASH  –  Instant bright burst at point of impact
        // ─────────────────────────────────────────────────────────────────────
        private void BuildCentralFlash(Transform parent, Material mat)
        {
            var go  = new GameObject("Central_Flash");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;

            var ps  = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            var m = ps.main;
            m.duration       = 0.1f;
            m.loop           = false;
            m.startLifetime  = 0.15f;
            m.startSpeed     = 0f;
            m.startSize      = new ParticleSystem.MinMaxCurve(1.2f, 2.0f); // Large flash circle
            m.startColor     = new Color(1f, 0.95f, 0.7f, 1f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            var e = ps.emission;
            e.rateOverTime = 0;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = ps.shape;
            shape.enabled = false;

            // Shrinks and fades instantly
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(1f, AnimCurve(1f, 0f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color   = FadeOutGradient(new Color(1f, 0.9f, 0.5f));

            psr.renderMode    = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. SHARDS  –  Stretched orange-gold confetti with gravity
        // ─────────────────────────────────────────────────────────────────────
        private void BuildShards(Transform parent, Material mat)
        {
            var go  = new GameObject("OrangeGold_Shards");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;

            var ps  = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            var m = ps.main;
            m.duration       = 0.4f;
            m.loop           = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(5f, 12f);
            // Stretch: long dimension is along velocity = looks like flying shards
            m.startSize3D    = false;
            m.startSize      = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            m.gravityModifier = 2.5f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            // Randomise between ORANGE and GOLD
            m.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.45f, 0.0f),   // deep orange
                new Color(1f, 0.85f, 0.1f)    // bright gold
            );

            var e = ps.emission;
            e.rateOverTime = 0;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, 55) });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius    = 0.05f;

            // Fade out at end of life
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color   = FadeOutGradient(Color.white);

            // Rotation tumble – gives "flying shard" feel
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-720f * Mathf.Deg2Rad, 720f * Mathf.Deg2Rad);

            // STRETCHED billboard → looks like a piece of confetti / shard
            psr.renderMode        = ParticleSystemRenderMode.Stretch;
            psr.velocityScale     = 0.15f;
            psr.lengthScale       = 2.0f;
            psr.sharedMaterial    = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. FLAME RING  –  Particles shoot outward from a circle (ring shape)
        // ─────────────────────────────────────────────────────────────────────
        private void BuildFlameRing(Transform parent, Material mat)
        {
            var go  = new GameObject("Flame_Ring");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;

            var ps  = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            var m = ps.main;
            m.duration       = 0.4f;
            m.loop           = false;
            m.startLifetime  = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            m.startSpeed     = new ParticleSystem.MinMaxCurve(4f, 8f); // Shoot outward fast
            m.startSize      = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            m.startColor     = new Color(1f, 0.5f, 0.05f, 1f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            var e = ps.emission;
            e.rateOverTime = 0;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

            // CIRCLE shape + emit from EDGE = perfect expanding ring
            var sh = ps.shape;
            sh.shapeType    = ParticleSystemShapeType.Circle;
            sh.radius       = 0.15f;
            sh.radiusThickness = 0f; // Emit only from the edge of the circle
            sh.arcMode      = ParticleSystemShapeMultiModeValue.Random;

            // Colour fades orange → red → transparent
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.0f),  // yellow-white at birth
                    new GradientColorKey(new Color(1f, 0.2f, 0.0f),  0.7f),  // deep red
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0.0f),
                    new GradientAlphaKey(0f, 1.0f),
                }
            );
            col.color = grad;

            // Grows then shrinks — looks like a flame petal
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve flameCurve = new AnimationCurve();
            flameCurve.AddKey(0f,   0.3f);
            flameCurve.AddKey(0.3f, 1.0f);
            flameCurve.AddKey(1f,   0.0f);
            sol.size = new ParticleSystem.MinMaxCurve(1f, flameCurve);

            psr.renderMode    = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. SHOCKWAVE RING  –  A flat disc of stretched lines splashing out
        // ─────────────────────────────────────────────────────────────────────
        private void BuildShockwaveRing(Transform parent, Material mat, bool vertical)
        {
            var go  = new GameObject(vertical ? "Shockwave_Ring_Vertical" : "Shockwave_Ring_Horizontal");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            
            if (vertical)
                go.transform.localRotation = Quaternion.identity; // Vertical plane facing forward
            else
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Lay flat on XZ plane

            var ps  = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            var m = ps.main;
            m.duration        = 0.4f;
            m.loop            = false;
            m.startLifetime   = 0.35f;
            m.startSpeed      = new ParticleSystem.MinMaxCurve(8f, 14f); // Expand outward very fast
            m.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            m.startColor      = Color.white;
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            var e = ps.emission;
            e.rateOverTime = 0;
            // Dense burst of particles arranged in a ring = looks like a continuous splashing wave
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

            // CIRCLE edge emit = all particles shoot outward from a ring
            var sh = ps.shape;
            sh.shapeType       = ParticleSystemShapeType.Circle;
            sh.radius          = 0.05f;
            sh.radiusThickness = 0f; // Edge only
            sh.arcMode         = ParticleSystemShapeMultiModeValue.BurstSpread; // Evenly spread around the full 360°

            // Fade out
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.05f), 1.0f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0.0f),
                    new GradientAlphaKey(0f, 0.8f),
                }
            );
            col.color = g;

            // Stretched billboards shoot outward like shards/streaks
            psr.renderMode    = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.15f;
            psr.lengthScale   = 2.0f;
            psr.sharedMaterial = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4b. EXPANDING HALO RING  –  A smooth expanding ring texture/halo
        // ─────────────────────────────────────────────────────────────────────
        private void BuildExpandingHaloRing(Transform parent, Material mat, bool vertical)
        {
            var go  = new GameObject(vertical ? "Expanding_Halo_Vertical" : "Expanding_Halo_Horizontal");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            
            if (vertical)
                go.transform.localRotation = Quaternion.identity; // Vertical plane
            else
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Lay flat on XZ plane

            var ps  = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            var m = ps.main;
            m.duration        = 0.4f;
            m.loop            = false;
            m.startLifetime   = 0.35f;
            m.startSpeed      = 0f; // Doesn't move, just grows in size!
            m.startSize       = 0.1f;
            m.startColor      = new Color(1f, 0.65f, 0.1f, 0.8f);
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            var e = ps.emission;
            e.rateOverTime = 0;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var sh = ps.shape;
            sh.enabled = false; // Just spawn exactly at center

            // Expand size rapidly (explodes outward like a shockwave bubble)
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve scaleCurve = new AnimationCurve();
            scaleCurve.AddKey(0f, 0.2f);
            scaleCurve.AddKey(0.2f, 1.2f);
            scaleCurve.AddKey(1f, 3.5f); // Expands to 3.5m diameter!
            sol.size = new ParticleSystem.MinMaxCurve(1f, scaleCurve);

            // Fade out
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0f), new GradientAlphaKey(0f, 1.0f) }
            );
            col.color = g;

            psr.renderMode    = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. EMBERS  –  Tiny sparks that float upward after the explosion
        // ─────────────────────────────────────────────────────────────────────
        private void BuildEmbers(Transform parent, Material mat)
        {
            var go  = new GameObject("Embers");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;

            var ps  = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            var m = ps.main;
            m.duration        = 0.4f;
            m.loop            = false;
            m.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            m.startSpeed      = new ParticleSystem.MinMaxCurve(1f, 4f);
            m.startSize       = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            m.startColor      = new Color(1f, 0.7f, 0.1f, 1f);
            m.gravityModifier = -0.3f; // Negative gravity = floats UP like hot embers
            m.simulationSpace = ParticleSystemSimulationSpace.World;

            var e = ps.emission;
            e.rateOverTime = 0;
            e.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius    = 0.08f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.0f), 1.0f),
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0.0f),
                    new GradientAlphaKey(0f, 1.0f),
                }
            );
            col.color = grad;

            psr.renderMode    = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────
        private AnimationCurve AnimCurve(float start, float end)
        {
            var c = new AnimationCurve();
            c.AddKey(0f, start);
            c.AddKey(1f, end);
            return c;
        }

        private ParticleSystem.MinMaxGradient FadeOutGradient(Color startColor)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(startColor, 0f), new GradientColorKey(startColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            return new ParticleSystem.MinMaxGradient(g);
        }
    }
}
