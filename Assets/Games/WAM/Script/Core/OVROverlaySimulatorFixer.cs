using UnityEngine;

namespace WhackAMole
{
    [DefaultExecutionOrder(-1000)]
    public class OVROverlaySimulatorFixer : MonoBehaviour
    {
#if UNITY_EDITOR
        private void Awake()
        {
            System.Type overlayType = FindType("OVROverlayCanvas");
            if (overlayType == null) return;

            Object[] overlays = FindObjectsOfType(overlayType, true);
            if (overlays == null || overlays.Length == 0) return;

            int count = 0;
            foreach (Object obj in overlays)
            {
                var overlay = obj as MonoBehaviour;
                if (overlay == null) continue;

                overlay.enabled = false;

                foreach (Camera cam in overlay.GetComponentsInChildren<Camera>(true))
                {
                    if (cam.gameObject != overlay.gameObject)
                        cam.enabled = false;
                }

                Canvas canvas = overlay.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.enabled = true;
                    canvas.renderMode = RenderMode.WorldSpace;
                }
                count++;
            }

            if (count > 0)
                Debug.Log($"[OVROverlayFixer] Disabled {count} OVROverlayCanvas components.");
        }

        private static System.Type FindType(string typeName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }
#endif
    }
}
