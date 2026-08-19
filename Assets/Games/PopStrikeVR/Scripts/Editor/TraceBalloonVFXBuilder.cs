using UnityEngine;
using UnityEditor;

namespace PopstrikeVR.EditorTools
{
    public class TraceBalloonVFXBuilder : EditorWindow
    {
        private Mesh leafMesh;

        [MenuItem("PopStrikeVR/VFX/Build Trace Balloon VFX")]
        public static void ShowWindow()
        {
            GetWindow<TraceBalloonVFXBuilder>("Trace VFX Builder").Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Trace Balloon Sparkles & Leaves VFX", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Generates the Green Sparkles & Tumbling Leaves VFX matching the GDD:\n" +
                "  ✔  Bright green-white sparkle burst (Root)\n" +
                "  ✔  Gently falling green leaf particles that rotate as they tumble (Child)\n" +
                "  ✔  Quest/Android safe Sprites/Default materials\n" +
                "  ✔  Drag-and-drop a 3D leaf model mesh below to make actual leaf particles!",
                MessageType.Info);

            EditorGUILayout.Space(10);
            leafMesh = (Mesh)EditorGUILayout.ObjectField("Leaf Mesh (Optional)", leafMesh, typeof(Mesh), false);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("🌿  Generate Trace Balloon VFX", GUILayout.Height(50)))
            {
                GenerateVFX();
            }
        }

        private void GenerateVFX()
        {
            string prefabFolder = "Assets/Games/PopStrikeVR/Prefabs/VFX";
            string matFolder    = "Assets/Games/PopStrikeVR/Materials/VFX";
            System.IO.Directory.CreateDirectory(prefabFolder);
            System.IO.Directory.CreateDirectory(matFolder);
            AssetDatabase.Refresh();

            // 1. Create materials
            Material sparkleMat = CreateAdditiveMaterial($"{matFolder}/TraceSparkle_Mat.mat", new Color(0.2f, 1f, 0.3f, 1f));
            Material leafMat = CreateAdditiveMaterial($"{matFolder}/TraceLeaf_Mat.mat", new Color(0.1f, 0.8f, 0.2f, 1f));

            // 2. Setup Root Object (Sparkle Burst)
            GameObject root = new GameObject("VFX_TraceLeaves");
            ParticleSystem sparklePs = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer sparklePsr = root.GetComponent<ParticleSystemRenderer>();

            // Sparkle Main Module
            var sparkleMain = sparklePs.main;
            sparkleMain.duration = 1.0f;
            sparkleMain.loop = false;
            sparkleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            sparkleMain.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.0f);
            sparkleMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            sparkleMain.startColor = Color.white;
            sparkleMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sparkleMain.playOnAwake = true;
            sparkleMain.stopAction = ParticleSystemStopAction.Destroy;

            // Sparkle Emission Module
            var sparkleEmission = sparklePs.emission;
            sparkleEmission.rateOverTime = 0f;
            sparkleEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25, 40) });

            // Sparkle Shape Module
            var sparkleShape = sparklePs.shape;
            sparkleShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkleShape.radius = 0.1f;

            // Sparkle Color Over Lifetime
            var sparkleColor = sparklePs.colorOverLifetime;
            sparkleColor.enabled = true;
            Gradient sparkleGrad = new Gradient();
            sparkleGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0.2f, 1f, 0.3f), 0.5f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            sparkleColor.color = sparkleGrad;

            // Sparkle Renderer Module
            sparklePsr.material = sparkleMat;
            sparklePsr.renderMode = ParticleSystemRenderMode.Billboard;

            // 3. Setup Leaf Child System
            GameObject leavesObj = new GameObject("Leaves");
            leavesObj.transform.SetParent(root.transform);
            leavesObj.transform.localPosition = Vector3.zero;

            ParticleSystem leafPs = leavesObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer leafPsr = leavesObj.GetComponent<ParticleSystemRenderer>();

            // Leaf Main Module
            var leafMain = leafPs.main;
            leafMain.duration = 1.5f;
            leafMain.loop = false;
            leafMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            leafMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            
            // Scaled larger for mesh particles to be visible
            if (leafMesh != null)
                leafMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            else
                leafMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);

            leafMain.startColor = Color.white;
            leafMain.startRotation3D = true;
            leafMain.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            leafMain.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            leafMain.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            
            // Positive gravity so they fall slowly downwards like leaves
            leafMain.gravityModifier = 0.15f; 
            leafMain.simulationSpace = ParticleSystemSimulationSpace.World;
            leafMain.playOnAwake = true;

            // Leaf Emission Module
            var leafEmission = leafPs.emission;
            leafEmission.rateOverTime = 0f;
            leafEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12, 20) });

            // Leaf Shape Module
            var leafShape = leafPs.shape;
            leafShape.shapeType = ParticleSystemShapeType.Sphere;
            leafShape.radius = 0.08f;

            // Leaf Rotation Over Lifetime (Slow tumble)
            var leafRot = leafPs.rotationOverLifetime;
            leafRot.enabled = true;
            leafRot.separateAxes = true;
            leafRot.xMultiplier = 2f;
            leafRot.yMultiplier = 3f;
            leafRot.zMultiplier = 2f;

            // Leaf Size Over Lifetime
            var leafSize = leafPs.sizeOverLifetime;
            leafSize.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.8f);
            sizeCurve.AddKey(0.2f, 1.0f);
            sizeCurve.AddKey(0.8f, 0.8f);
            sizeCurve.AddKey(1.0f, 0.0f);
            leafSize.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            // Leaf Color Over Lifetime
            var leafColor = leafPs.colorOverLifetime;
            leafColor.enabled = true;
            Gradient leafGrad = new Gradient();
            leafGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0.1f, 0.8f, 0.2f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            leafColor.color = leafGrad;

            // Leaf Renderer Module
            leafPsr.material = leafMat;
            if (leafMesh != null)
            {
                leafPsr.renderMode = ParticleSystemRenderMode.Mesh;
                leafPsr.mesh = leafMesh;
            }
            else
            {
                leafPsr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            // Save Prefab
            string savePath = $"{prefabFolder}/VFX_TraceLeaves.prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            DestroyImmediate(root);

            Debug.Log($"<color=green><b>[TraceVFX]</b></color> Saved → {savePath}");
            Selection.activeObject = savedPrefab;
            EditorUtility.FocusProjectWindow();
            EditorUtility.DisplayDialog("Success", $"Trace Balloon VFX generated successfully!\n\nSaved at: {savePath}", "Awesome");
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

            mat.SetColor("_Color", color * 2.0f);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
