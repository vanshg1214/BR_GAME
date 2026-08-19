using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PopstrikeVR.Visuals
{
    /// <summary>
    /// Controls URP Global Volume overrides for Screen-Space Feedback.
    /// Requires a Volume component on the same GameObject with Vignette and ColorAdjustments overrides added.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class ScreenEffectsController : MonoBehaviour
    {
        public static ScreenEffectsController Instance { get; private set; }

        private Volume globalVolume;
        private Vignette vignette;
        private ColorAdjustments colorAdjustments;

        private Coroutine flashRoutine;
        private Coroutine saturationRoutine;
        private Coroutine vignetteRoutine;
        private Coroutine fadeRoutine;

        // Cache the default states so we always return to normal, even if interrupted!
        private Color defaultColorFilter = Color.white;
        private float defaultSaturation = 0f;
        private Color defaultVignetteColor = Color.black;
        private float defaultVignetteIntensity = 0f;
        private float defaultVignetteSmoothness = 0.2f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            globalVolume = GetComponent<Volume>();

            // Attempt to fetch overrides
            if (globalVolume.profile != null)
            {
                if (globalVolume.profile.TryGet(out vignette))
                {
                    defaultVignetteColor = vignette.color.value;
                    defaultVignetteIntensity = vignette.intensity.value;
                    defaultVignetteSmoothness = vignette.smoothness.value;
                }
                
                if (globalVolume.profile.TryGet(out colorAdjustments))
                {
                    defaultColorFilter = colorAdjustments.colorFilter.value;
                    defaultSaturation = colorAdjustments.saturation.value;
                }
            }
        }

        public void FadeToBlack(float duration = 1.0f)
        {
            if (colorAdjustments != null)
            {
                if (fadeRoutine != null) StopCoroutine(fadeRoutine);
                fadeRoutine = StartCoroutine(FadeRoutine(Color.black, duration));
            }
        }

        public void FadeFromBlack(float duration = 1.0f)
        {
            if (colorAdjustments != null)
            {
                // Instantly set to black first
                colorAdjustments.colorFilter.value = Color.black;
                if (fadeRoutine != null) StopCoroutine(fadeRoutine);
                fadeRoutine = StartCoroutine(FadeRoutine(defaultColorFilter, duration));
            }
        }

        public void FlashScreen(Color flashColor, float duration = 0.1f)
        {
            if (colorAdjustments != null)
            {
                if (flashRoutine != null) StopCoroutine(flashRoutine);
                flashRoutine = StartCoroutine(ColorFlashRoutine(flashColor, duration));
            }
        }

        public void DesaturateMiss()
        {
            if (colorAdjustments != null)
            {
                if (saturationRoutine != null) StopCoroutine(saturationRoutine);
                saturationRoutine = StartCoroutine(SaturationPulseRoutine(-100f, 0.4f));
            }
        }

        public void TriggerErrorVignette(Color vignetteColor, float intensity = 0.85f)
        {
            if (vignette != null)
            {
                if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);
                vignetteRoutine = StartCoroutine(VignettePulseRoutine(vignetteColor, intensity, 0.5f));
            }
        }

        public void TriggerEdgeFlash(Color edgeColor, float intensity = 0.35f, float duration = 0.4f)
        {
            if (vignette != null)
            {
                if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);
                vignetteRoutine = StartCoroutine(VignettePulseRoutine(edgeColor, intensity, duration));
            }
        }

        public void TriggerSpawnWarning(Color warningColor, float intensity = 0.6f)
        {
            if (vignette != null)
            {
                if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);
                vignetteRoutine = StartCoroutine(DoubleVignettePulseRoutine(warningColor, intensity));
            }
        }

        private IEnumerator ColorFlashRoutine(Color flashColor, float duration)
        {
            colorAdjustments.colorFilter.value = flashColor;
            
            yield return new WaitForSeconds(duration);
            
            // ALWAYS revert to the default normal color, not whatever it was a millisecond ago
            colorAdjustments.colorFilter.value = defaultColorFilter;
            flashRoutine = null;
        }

        private IEnumerator FadeRoutine(Color targetColor, float duration)
        {
            Color startColor = colorAdjustments.colorFilter.value;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                colorAdjustments.colorFilter.value = Color.Lerp(startColor, targetColor, elapsed / duration);
                yield return null;
            }
            colorAdjustments.colorFilter.value = targetColor;
            fadeRoutine = null;
        }

        private IEnumerator SaturationPulseRoutine(float targetSat, float duration)
        {
            colorAdjustments.saturation.value = targetSat;
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                colorAdjustments.saturation.value = Mathf.Lerp(targetSat, defaultSaturation, elapsed / duration);
                yield return null;
            }
            colorAdjustments.saturation.value = defaultSaturation;
            saturationRoutine = null;
        }

        private IEnumerator VignettePulseRoutine(Color flashColor, float targetIntensity, float duration)
        {
            vignette.color.value = flashColor;
            vignette.intensity.value = targetIntensity;
            
            // Push smoothness to maximum so the color encroaches deeper into the center of the screen.
            // This guarantees it is visible inside the physical lenses of a VR headset (which crop the edges).
            vignette.smoothness.value = 1.0f; 

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                vignette.intensity.value = Mathf.Lerp(targetIntensity, defaultVignetteIntensity, elapsed / duration);
                yield return null;
            }
            
            vignette.intensity.value = defaultVignetteIntensity;
            vignette.color.value = defaultVignetteColor;
            vignette.smoothness.value = defaultVignetteSmoothness; // Restore normal smoothness
            vignetteRoutine = null;
        }

        private IEnumerator DoubleVignettePulseRoutine(Color warningColor, float targetIntensity)
        {
            // Push smoothness deep into VR FOV for both splashes
            vignette.smoothness.value = 1.0f;

            // First Splash (Fast)
            vignette.color.value = warningColor;
            vignette.intensity.value = targetIntensity;
            yield return new WaitForSeconds(0.1f);
            vignette.intensity.value = defaultVignetteIntensity;
            yield return new WaitForSeconds(0.1f);

            // Second Splash (Slightly longer fade)
            vignette.color.value = warningColor;
            float elapsed = 0f;
            float duration = 0.3f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                vignette.intensity.value = Mathf.Lerp(targetIntensity, defaultVignetteIntensity, elapsed / duration);
                yield return null;
            }
            vignette.intensity.value = defaultVignetteIntensity;
            vignette.color.value = defaultVignetteColor;
            vignette.smoothness.value = defaultVignetteSmoothness; // Restore
            vignetteRoutine = null;
        }
    }
}
