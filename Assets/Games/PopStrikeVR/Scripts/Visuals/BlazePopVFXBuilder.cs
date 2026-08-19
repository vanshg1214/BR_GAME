using UnityEngine;

namespace PopstrikeVR.Visuals
{
    /// <summary>
    /// Blaze Balloon Pop VFX — 5 fully-synced Particle System children.
    /// ALL layers are ParticleSystems so the parent Play button in Unity
    /// triggers and previews all of them simultaneously.
    ///
    ///  Ring_Inner     — Compact hot-white ring, expands from 4cm → 14cm fast
    ///  Ring_Outer     — Larger orange ring, expands from 6cm → 30cm slower
    ///  Flame_Burst    — Omnidirectional billboard flame particles
    ///  Confetti_Shards— Orange-gold mesh shards tumbling with gravity
    ///  Ember_Sparks   — Tiny hot sparks drifting downward
    ///
    /// HOW TO USE:
    ///  1. Create an empty GameObject → add this script.
    ///  2. Assign your cfxr ring mesh to "Shockwave Ring Mesh".
    ///  3. Click the 3-dot menu → "Preview (Overrides Previous)".
    ///  4. Select the root object in the Hierarchy → click Play in the
    ///     "Particle Effect" preview panel → all 5 layers play together.
    ///  5. Drag to Project → saves as Prefab → assign to FeedbackManager.
    /// </summary>
    public class BlazePopVFXBuilder : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  INSPECTOR
        // ──────────────────────────────────────────────

        [Header("Meshes")]
        [Tooltip("Assign your cfxr ring mesh here. Used for BOTH shockwave rings.")]
        public Mesh shockwaveRingMesh;
        [Tooltip("Optional small mesh for confetti shards. Leave empty for billboard quads.")]
        public Mesh confettiShardMesh;

        [Header("Materials (drag your VFX materials here)")]
        [Tooltip("Ring material — assign BlazeShockwave from your VFX folder.")]
        public Material innerRingMaterial;
        [Tooltip("Outer ring material — assign BlazeShockwave or BlazeFlash.")]
        public Material outerRingMaterial;
        [Tooltip("Flame Burst material — assign BlazeFlame.")]
        public Material flameMaterial;
        [Tooltip("Confetti material — assign BlazeShards.")]
        public Material confettiMaterial;
        [Tooltip("Ember Sparks material — assign BlazeEmbers.")]
        public Material emberMaterial;

        [Header("Colors")]
        public Color innerRingColor  = new Color(1.0f, 0.97f, 0.75f, 1.0f); // Hot white-gold
        public Color outerRingColor  = new Color(1.0f, 0.50f, 0.00f, 0.9f); // Bright orange
        public Color confettiColorA  = new Color(1.0f, 0.60f, 0.00f, 1.0f); // Orange
        public Color confettiColorB  = new Color(1.0f, 0.85f, 0.20f, 1.0f); // Gold
        public Color flameColor      = new Color(1.0f, 0.38f, 0.00f, 1.0f); // Deep flame
        public Color emberColor      = new Color(1.0f, 0.90f, 0.60f, 1.0f); // Hot white-gold

        [Header("Inner Ring (Small, Fast)")]
        [Range(0.01f, 0.10f)] public float innerRingStartScale  = 0.04f;  // 4cm start
        [Range(0.05f, 0.35f)] public float innerRingEndScale    = 0.16f;  // 16cm end
        [Range(0.05f, 0.5f)]  public float innerRingDuration    = 0.20f;  // fast
        [Range(0.5f,  6.0f)]  public float innerRingThickness   = 2.5f;   // Y scale multiplier

        [Header("Outer Ring (Large, Slower)")]
        [Range(0.02f, 0.15f)] public float outerRingStartScale  = 0.06f;  // 6cm start
        [Range(0.10f, 0.60f)] public float outerRingEndScale    = 0.30f;  // 30cm end
        [Range(0.10f, 0.6f)]  public float outerRingDuration    = 0.40f;  // lingers
        [Range(0.5f,  6.0f)]  public float outerRingThickness   = 1.8f;

        [Header("Confetti Shards")]
        [Range(20, 80)]        public int   confettiCount    = 40;
        [Range(0.3f, 3.0f)]   public float confettiSpeed    = 1.6f;
        [Range(0.3f, 1.5f)]   public float confettiLifetime = 0.7f;
        [Range(0.005f, 0.04f)] public float confettiSize    = 0.013f;

        [Header("Flame Burst")]
        [Range(10, 40)]        public int   flameCount      = 22;
        [Range(0.2f, 2.0f)]   public float flameSpeed      = 1.0f;
        [Range(0.15f, 0.6f)]  public float flameLifetime   = 0.38f;
        [Range(0.01f, 0.09f)] public float flameSize       = 0.048f;

        [Header("Ember Sparks")]
        [Range(10, 40)]        public int   emberCount      = 18;
        [Range(0.2f, 2.0f)]   public float emberSpeed      = 0.7f;
        [Range(0.5f, 2.0f)]   public float emberLifetime   = 1.0f;
        [Range(0.002f, 0.015f)] public float emberSize     = 0.005f;

        [Header("Timing")]
        public float effectLifetime = 1.6f;

        // ──────────────────────────────────────────────
        //  CONTEXT MENU
        // ──────────────────────────────────────────────

        [ContextMenu("Preview (Overrides Previous)")]
        public void GenerateVFXInEditor()
        {
            while (transform.childCount > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);

            // Remove old root PS if present
            var oldPS  = GetComponent<ParticleSystem>();
            if (oldPS  != null) DestroyImmediate(oldPS);
            var oldPSR = GetComponent<ParticleSystemRenderer>();
            if (oldPSR != null) DestroyImmediate(oldPSR);

            BuildRootPS();
            BuildRing("Ring_Inner", innerRingStartScale, innerRingEndScale, innerRingThickness, innerRingDuration, innerRingColor);
            BuildRing("Ring_Outer", outerRingStartScale, outerRingEndScale, outerRingThickness, outerRingDuration, outerRingColor);
            BuildFlameBurst();
            BuildConfettiShards();
            BuildEmberSparks();
        }

        // ──────────────────────────────────────────────
        //  RUNTIME
        // ──────────────────────────────────────────────

        private void Start()
        {
            if (transform.childCount == 0)
            {
                BuildRootPS();
                BuildRing("Ring_Inner", innerRingStartScale, innerRingEndScale, innerRingThickness, innerRingDuration, innerRingColor);
                BuildRing("Ring_Outer", outerRingStartScale, outerRingEndScale, outerRingThickness, outerRingDuration, outerRingColor);
                BuildFlameBurst();
                BuildConfettiShards();
                BuildEmberSparks();
            }
            Destroy(gameObject, effectLifetime);
        }

        // ──────────────────────────────────────────────
        //  ROOT ParticleSystem (silent master controller)
        //  This is the object the user selects and hits
        //  Play on — Unity will automatically play all
        //  child particle systems at the same time.
        // ──────────────────────────────────────────────

        private void BuildRootPS()
        {
            ParticleSystem root = gameObject.AddComponent<ParticleSystem>();
            var m = root.main;
            m.duration       = effectLifetime;
            m.loop           = false;
            m.playOnAwake    = true;
            m.startLifetime  = 0.001f; // near-zero so root emits nothing visible
            m.maxParticles   = 0;

            var e = root.emission;
            e.enabled = false;    // root emits ZERO particles — just keeps alive for timing

            var s = root.shape;
            s.enabled = false;

            // Hide the root renderer completely
            var r = gameObject.GetComponent<ParticleSystemRenderer>();
            if (r != null) r.enabled = false;
        }

        // ──────────────────────────────────────────────
        //  RING (single expanding Particle System mesh)
        //
        //  One particle is emitted at t=0 using the
        //  ring mesh. Its size grows from startScale
        //  to endScale over the duration via
        //  Size Over Lifetime, then fades out.
        // ──────────────────────────────────────────────

        private void BuildRing(string goName, float startScale, float endScale,
                                float thickness, float duration, Color color)
        {
            GameObject go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            // ── Main ────────────────────────────────
            var m = ps.main;
            m.duration         = duration + 0.05f;   // slightly longer than lifetime so PS stays alive
            m.loop             = false;
            m.startLifetime    = duration;
            m.startSpeed       = 0f;                 // ring does NOT move — size curve handles expansion

            // Use 3D start size so we can independently control Y (thickness)
            // startSize is set to the MAXIMUM (end) scale.
            // The Size Over Lifetime curve then multiplies from (start/end) → 1.0
            m.startSize3D      = true;
            m.startSizeX       = new ParticleSystem.MinMaxCurve(endScale);
            m.startSizeY       = new ParticleSystem.MinMaxCurve(endScale * thickness);
            m.startSizeZ       = new ParticleSystem.MinMaxCurve(endScale);

            m.startColor       = color;
            m.simulationSpace  = ParticleSystemSimulationSpace.World;
            m.maxParticles     = 1;
            m.playOnAwake      = true;

            // Lay ring flat (horizontal) — rotate 90° around X so it faces up
            m.startRotation3D  = true;
            m.startRotationX   = new ParticleSystem.MinMaxCurve(90f * Mathf.Deg2Rad);
            m.startRotationY   = new ParticleSystem.MinMaxCurve(0f);
            m.startRotationZ   = new ParticleSystem.MinMaxCurve(0f);

            // ── Emission: one single particle at t=0 ────
            var e = ps.emission;
            e.enabled       = true;
            e.rateOverTime  = 0f;
            e.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, 1) });

            var sh = ps.shape;
            sh.enabled = false;  // point emitter — ring expands via size, not velocity

            // ── Size Over Lifetime: grow from startScale → endScale ──
            // The curve multiplies the startSize (which is endScale).
            // So curve value at t=0 should be (startScale/endScale).
            float startRatio = Mathf.Clamp(startScale / Mathf.Max(endScale, 0.0001f), 0.01f, 0.99f);
            var sol = ps.sizeOverLifetime;
            sol.enabled        = true;
            sol.separateAxes   = false;   // one curve drives all axes uniformly
            sol.size           = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0.00f, startRatio, 0f, 4f),   // start small, initial burst of speed
                new Keyframe(0.50f, 0.90f,       2f, 0.5f), // mostly expanded by halfway
                new Keyframe(1.00f, 1.00f,       0f, 0f)    // full size at the end
            ));

            // ── Color Over Lifetime: opaque → transparent ──
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]{
                    new GradientColorKey(new Color(color.r * 1.4f, color.g * 1.4f, color.b * 1.4f), 0f),
                    new GradientColorKey(color, 0.3f),
                    new GradientColorKey(color, 1f)
                },
                new GradientAlphaKey[]{
                    new GradientAlphaKey(color.a,         0.0f),
                    new GradientAlphaKey(color.a,         0.4f),
                    new GradientAlphaKey(color.a * 0.4f, 0.75f),
                    new GradientAlphaKey(0f,              1.0f)
                }
            );
            col.color = g;

            // ── Renderer ────────────────────────────
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (shockwaveRingMesh != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Mesh;
                psr.mesh       = shockwaveRingMesh;
            }
            else
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
            }
            // Use the assigned material — fallback to code-generated one only if nothing is assigned
            Material ringMat = (goName == "Ring_Inner") ? innerRingMaterial : outerRingMaterial;
            psr.material    = ringMat != null ? ringMat : CreateAdditiveMaterial(color);
            psr.sortingOrder = 1;
        }

        // ──────────────────────────────────────────────
        //  FLAME BURST
        // ──────────────────────────────────────────────

        private void BuildFlameBurst()
        {
            GameObject go = new GameObject("Flame_Burst");
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var m = ps.main;
            m.duration        = 0.05f;
            m.loop            = false;
            m.startLifetime   = flameLifetime;
            m.startSpeed      = new ParticleSystem.MinMaxCurve(flameSpeed * 0.5f, flameSpeed);
            m.startSize       = new ParticleSystem.MinMaxCurve(flameSize  * 0.5f, flameSize);
            m.startColor      = flameColor;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = -0.25f;
            m.maxParticles    = flameCount;
            m.playOnAwake     = true;

            var e = ps.emission;
            e.enabled = true;
            e.rateOverTime = 0f;
            e.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, flameCount) });

            var sh = ps.shape;
            sh.enabled    = true;
            sh.shapeType  = ParticleSystemShapeType.Sphere;
            sh.radius     = 0.015f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f,   0.3f),
                new Keyframe(0.2f, 1.3f),
                new Keyframe(1f,   0.0f)
            ));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]{
                    new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                    new GradientColorKey(flameColor,                  0.3f),
                    new GradientColorKey(new Color(0.9f, 0.1f, 0f),  1f)
                },
                new GradientAlphaKey[]{
                    new GradientAlphaKey(1f,   0f),
                    new GradientAlphaKey(0.85f, 0.35f),
                    new GradientAlphaKey(0f,   1f)
                }
            );
            col.color = g;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material   = flameMaterial != null ? flameMaterial : CreateAdditiveMaterial(flameColor);
        }

        // ──────────────────────────────────────────────
        //  CONFETTI SHARDS
        // ──────────────────────────────────────────────

        private void BuildConfettiShards()
        {
            GameObject go = new GameObject("Confetti_Shards");
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var m = ps.main;
            m.duration         = 0.05f;
            m.loop             = false;
            m.startLifetime    = new ParticleSystem.MinMaxCurve(confettiLifetime * 0.6f, confettiLifetime);
            m.startSpeed       = new ParticleSystem.MinMaxCurve(confettiSpeed * 0.4f,    confettiSpeed);
            m.startSize        = new ParticleSystem.MinMaxCurve(confettiSize   * 0.6f,   confettiSize);
            m.startRotation3D  = true;
            m.startRotationX   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.startRotationY   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.startRotationZ   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            m.startColor       = new ParticleSystem.MinMaxGradient(confettiColorA, confettiColorB);
            m.simulationSpace  = ParticleSystemSimulationSpace.World;
            m.gravityModifier  = 1.8f;
            m.maxParticles     = confettiCount;
            m.playOnAwake      = true;

            var e = ps.emission;
            e.enabled = true;
            e.rateOverTime = 0f;
            e.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, confettiCount) });

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius    = 0.015f;

            var rot = ps.rotationOverLifetime;
            rot.enabled        = true;
            rot.separateAxes   = true;
            rot.xMultiplier    = 9f;
            rot.yMultiplier    = 7f;
            rot.zMultiplier    = 11f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f,   1f),
                new Keyframe(0.65f, 0.75f),
                new Keyframe(1f,   0f)
            ));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[]{ new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = g;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (confettiShardMesh != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Mesh;
                psr.mesh       = confettiShardMesh;
            }
            else
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
            }
            psr.material = confettiMaterial != null ? confettiMaterial : CreateAdditiveMaterial(confettiColorA);
        }

        // ──────────────────────────────────────────────
        //  EMBER SPARKS
        // ──────────────────────────────────────────────

        private void BuildEmberSparks()
        {
            GameObject go = new GameObject("Ember_Sparks");
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var m = ps.main;
            m.duration        = 0.05f;
            m.loop            = false;
            m.startLifetime   = new ParticleSystem.MinMaxCurve(emberLifetime * 0.5f, emberLifetime);
            m.startSpeed      = new ParticleSystem.MinMaxCurve(emberSpeed * 0.2f,    emberSpeed);
            m.startSize       = new ParticleSystem.MinMaxCurve(emberSize  * 0.5f,    emberSize);
            m.startColor      = emberColor;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 1.1f;
            m.maxParticles    = emberCount;
            m.playOnAwake     = true;

            var e = ps.emission;
            e.enabled = true;
            e.rateOverTime = 0f;
            e.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, emberCount) });

            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius    = 0.01f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.5f, 0.55f), new Keyframe(1f, 0f)
            ));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]{
                    new GradientColorKey(emberColor,                0f),
                    new GradientColorKey(new Color(1f,0.5f,0.1f),  0.4f),
                    new GradientColorKey(new Color(1f,0.1f,0f),    1f)
                },
                new GradientAlphaKey[]{ new GradientAlphaKey(1f,0f), new GradientAlphaKey(0.8f,0.5f), new GradientAlphaKey(0f,1f) }
            );
            col.color = g;

            var noise = ps.noise;
            noise.enabled     = true;
            noise.strength    = 0.4f;
            noise.frequency   = 4f;
            noise.scrollSpeed = 1.5f;
            noise.damping     = true;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material   = emberMaterial != null ? emberMaterial : CreateAdditiveMaterial(emberColor);
        }

        // ──────────────────────────────────────────────
        //  UTILITY: Additive glowing material
        // ──────────────────────────────────────────────

        private Material CreateAdditiveMaterial(Color baseColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);  // Additive
            mat.SetInt("_ZWrite",   0);
            mat.renderQueue = 3500;

            Color hdr = baseColor * 2.5f;  // HDR boost so bloom picks it up
            mat.SetColor("_BaseColor", hdr);
            mat.SetColor("_Color",     hdr);

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend",   1f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return mat;
        }
    }
}
