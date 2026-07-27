using UnityEngine;

namespace WhackAMole
{
    public class HoleMask : MonoBehaviour
    {
        [Tooltip("Height of the invisible mask cylinder below the hole (metres).")]
        [SerializeField] private float maskDepth = 3f;

        [Tooltip("Radius multiplier — slightly under 1.0 to avoid edge bleed.")]
        [SerializeField] private float maskRadiusScale = 0.95f;

        private GameObject maskObject;

        private void Awake()
        {
            CreateMask();
        }

        private void OnDestroy()
        {
            if (maskObject != null) Destroy(maskObject);
        }

        private void CreateMask()
        {
            maskObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            maskObject.name = "DepthMask";
            maskObject.transform.SetParent(transform, false);

            // Unity cylinders are 2 units tall by default, so we halve the depth for local scale
            maskObject.transform.localPosition = new Vector3(0f, -(maskDepth * 0.5f), 0f);
            maskObject.transform.localScale    = new Vector3(maskRadiusScale, maskDepth * 0.5f, maskRadiusScale);

            // Remove the auto-generated collider — we don't want the mask blocking hits
            Collider col = maskObject.GetComponent<Collider>();
            if (col != null) Destroy(col);

            ApplyDepthShader();
        }

        private void ApplyDepthShader()
        {
            Shader depthShader = Shader.Find("WhackAMole/DepthMask");
            if (depthShader == null)
            {
                Debug.LogError("[HoleMask] Shader 'WhackAMole/DepthMask' not found — check Assets/Shaders/.");
                return;
            }

            Renderer rend = maskObject.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(depthShader);
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows    = false;
            }
        }
    }
}
