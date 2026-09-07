using UnityEngine;
using System.Collections;

namespace PopstrikeVR.Core
{
    /// <summary>
    /// Rotates the skybox on startup so that a specific feature in the skybox texture
    /// (e.g., the Moon) is always positioned directly in front of the player's initial
    /// real-world physical orientation.
    /// 
    /// Usage: Drop this onto any empty GameObject in any scene, and adjust the 'targetAngleOffset'
    /// so the moon lines up exactly with the center of your view.
    /// </summary>
    public class SkyboxAligner : MonoBehaviour
    {
        [Tooltip("The angle offset required to align the main visual feature. Set to 0 if the skybox natively faces exactly where you want.")]
        public float targetAngleOffset = 0f;

        [Tooltip("Drag your CenterEyeAnchor (or Main Camera) here so the script knows exactly where you are looking.")]
        public Transform centerEyeTransform;

        [Tooltip("Wait a few frames for VR Headset tracking to initialize before reading the camera rotation.")]
        public bool waitFramesForVRTracking = true;

        private void Start()
        {
            StartCoroutine(AlignSkyboxRoutine());
        }

        private IEnumerator AlignSkyboxRoutine()
        {
            if (waitFramesForVRTracking)
            {
                // In VR, the camera's true world rotation often takes a few frames to populate from the headset SDK
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
            }

            if (centerEyeTransform == null)
            {
                if (Camera.main != null)
                {
                    centerEyeTransform = Camera.main.transform;
                    Debug.Log("[SkyboxAligner] centerEyeTransform was not assigned. Automatically using Camera.main.");
                }
                else
                {
                    Debug.LogWarning("[SkyboxAligner] No CenterEyeTransform assigned and no MainCamera found! Cannot align skybox.");
                    yield break;
                }
            }

            if (RenderSettings.skybox != null)
            {
                // Get the player's true physical yaw from the center eye
                float playerYaw = centerEyeTransform.eulerAngles.y;
                
                // Set the rotation to match the player's yaw (plus any minor manual tweak offset)
                RenderSettings.skybox.SetFloat("_Rotation", playerYaw + targetAngleOffset);
                
                Debug.Log($"[SkyboxAligner] Successfully aligned Skybox to player view! Player Yaw: {playerYaw} | Final Skybox Rotation: {playerYaw + targetAngleOffset}");
            }
            else
            {
                Debug.LogWarning("[SkyboxAligner] No Skybox Material assigned in Window > Rendering > Lighting!");
            }
        }
    }
}
