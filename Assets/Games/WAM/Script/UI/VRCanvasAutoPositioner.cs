using System.Collections;
using UnityEngine;

namespace WhackAMole.UI
{
    /// <summary>
    /// Snaps a UI Canvas directly in front of the VR player's headset when the scene loads.
    /// It actively waits for the VR tracking to initialize so the canvas doesn't spawn on the floor.
    /// </summary>
    public class VRCanvasAutoPositioner : MonoBehaviour
    {
        [Header("Positioning Settings")]
        [Tooltip("Distance in front of the headset to place the canvas (metres).")]
        [SerializeField] private float distance = 1.5f;

        [Tooltip("Height offset relative to the headset. Positive values move it up, negative move it down.")]
        [SerializeField] private float heightOffset = 0.2f;

        [Tooltip("If true, the canvas will tilt up/down to perfectly face the camera. If false, it stays perfectly vertical (flat against the wall).")]
        [SerializeField] private bool tiltToFaceCamera = false;

        [Tooltip("If true, the canvas will spawn aligned with the Arcade Table's forward direction instead of wherever your head happens to be looking. Perfect for Pause Menus!")]
        [SerializeField] private bool alignToWorkspaceTable = false;

        private Canvas[] childCanvases;

        private void Awake()
        {
            // Cache the canvases once
            childCanvases = GetComponentsInChildren<Canvas>(true);
        }

        private void OnEnable()
        {
            // Do NOT hide the UI! Ensure it is instantly visible so the player isn't staring at a blank space.
            if (childCanvases != null)
            {
                foreach (Canvas c in childCanvases)
                {
                    if (c != null) c.enabled = true;
                }
            }

            StartCoroutine(PositionRoutine());
        }

        private IEnumerator PositionRoutine()
        {
            // Position it instantly on Frame 1
            Reposition();

            // Wait for VR tracking to initialize (camera must be off the floor)
            // While tracking is booting up, we continuously Reposition() so it tracks the player's head perfectly.
            float timeElapsed = 0f;
            while (Camera.main != null && Camera.main.transform.position.y < 0.5f && timeElapsed < 5f)
            {
                timeElapsed += Time.deltaTime;
                Reposition();
                yield return null;
            }

            if (timeElapsed > 0f)
            {
                yield return new WaitForSeconds(0.2f);
                Reposition(); // One final snap when tracking settles
            }
            else
            {
                yield return null; 
            }
        }

        public void Reposition()
        {
            if (Camera.main == null) return;

            Transform head = Camera.main.transform;

            // Always use the current headset direction
            Vector3 flatForward = head.forward;

            // Override with Arcade Table direction if requested (e.g., for Pause Menus)
            if (alignToWorkspaceTable && WorkspaceAutoPositioner.Instance != null)
            {
                flatForward = WorkspaceAutoPositioner.Instance.transform.forward;
            }

            flatForward.y = 0f;
            
            // Fallback if the user is looking straight down/up
            if (flatForward.sqrMagnitude < 0.001f) 
            {
                flatForward = Vector3.forward;
            }
            flatForward.Normalize();

            // Position it exactly in front of the head, plus the requested height offset
            Vector3 targetPos = head.position + (flatForward * distance);
            targetPos.y = head.position.y + heightOffset;

            transform.position = targetPos;

            // Rotate the canvas to face the player
            if (tiltToFaceCamera)
            {
                // Look perfectly at the head in 3D space
                transform.rotation = Quaternion.LookRotation(transform.position - head.position);
            }
            else
            {
                // Strict vertical rotation (like a wall poster)
                transform.rotation = Quaternion.LookRotation(flatForward);
            }
        }

#if UNITY_EDITOR
        // This magic Unity function runs automatically whenever you change a value in the Inspector!
        private void OnValidate()
        {
            // If we are actively in Play Mode, tweaking sliders, and the component is enabled, instantly update the Canvas!
            if (Application.isPlaying && enabled)
            {
                Reposition();
            }
        }
#endif
    }
}
