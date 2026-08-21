using UnityEngine;
using TMPro;
using System.Collections;

namespace ArcRoll.UI
{
    /// <summary>
    /// Animates motivating feedback text (e.g. "Strike!", "Smooth!") for the VR player.
    /// Texts sweeps in from the left, holds in the center, and sweeps out to the right,
    /// giving a professional, arcade-style gamified feel.
    /// </summary>
    public class ArcRollFeedbackTextAnimator : MonoBehaviour
    {
        [Header("Animation Timings")]
        [Tooltip("How long it takes to sweep in from the left")]
        [SerializeField] private float slideInDuration = 0.4f;
        [Tooltip("How long it stays on screen in front of the player")]
        [SerializeField] private float holdDuration = 1.2f;
        [Tooltip("How long it takes to sweep out to the right")]
        [SerializeField] private float slideOutDuration = 0.4f;

        [Header("Sweep Distances")]
        [Tooltip("How far to the left the text starts (in Canvas units)")]
        [SerializeField] private float startXOffset = -800f;
        [Tooltip("How far to the right the text exits (in Canvas units)")]
        [SerializeField] private float endXOffset = 800f;

        [Header("Easing Curves (Make it pop!)")]
        [Tooltip("Curve for entering. A slight overshoot at the end makes it feel punchy.")]
        [SerializeField] private AnimationCurve slideInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Curve for exiting. Easing in looks best here.")]
        [SerializeField] private AnimationCurve slideOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Header("Target Text")]
        [Tooltip("Assign the TextMeshPro UI element you want to animate here.")]
        [SerializeField] private TextMeshProUGUI _textMesh;

        [Header("Gradient Color")]
        [Tooltip("If enabled, the text will use a gradient instead of a single solid color.")]
        [SerializeField] private bool useGradient = false;
        
        [Tooltip("The linear gradient to apply across the text.")]
        [SerializeField] private Gradient textGradient;
        
        [Tooltip("The angle of the gradient in degrees. 0 = Left to Right, 90 = Bottom to Top.")]
        [Range(0f, 360f)]
        [SerializeField] private float gradientAngle = 0f;

        private RectTransform _rectTransform;
        private Coroutine _currentAnimation;
        private Vector2 _originalAnchoredPosition;
        private Color _originalColor;
        private FontStyles _originalFontStyle;

        private void Awake()
        {
            InitializeReferences();
            
            // Start completely invisible/off-screen
            _textMesh.alpha = 0f;
            _rectTransform.anchoredPosition = _originalAnchoredPosition + new Vector2(startXOffset, 0f);

            // Add a default "Pop" overshoot curve for Slide In if the user hasn't set one up
            if (slideInCurve.keys.Length <= 2 && slideInCurve.keys[1].value == 1f)
            {
                slideInCurve = new AnimationCurve(
                    new Keyframe(0f, 0f, 0f, 2f),
                    new Keyframe(0.7f, 1.1f, 0f, 0f), // Overshoot to 1.1
                    new Keyframe(1f, 1f, -1f, 0f)
                );
            }

            // Provide a default gradient if none is assigned in the inspector
            if (textGradient == null)
            {
                textGradient = new Gradient();
                textGradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.9f, 0f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0f), 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }
        }

        private void InitializeReferences()
        {
            if (_textMesh == null) return;
            
            if (_rectTransform == null) 
            {
                _rectTransform = _textMesh.rectTransform;
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
                _originalColor = _textMesh.color;
                _originalFontStyle = _textMesh.fontStyle;
            }
        }

        /// <summary>
        /// Call this from any GameManager or Scoring Script to trigger the text animation!
        /// </summary>
        public void ShowFeedback(string feedbackMsg)
        {
            if (_currentAnimation != null)
            {
                StopCoroutine(_currentAnimation);
            }
            
            // Re-align to the player's face right before we show the popup!
            VRAlignToCamera aligner = GetComponent<VRAlignToCamera>();
            if (aligner == null) aligner = GetComponentInParent<VRAlignToCamera>();
            if (aligner != null)
            {
                aligner.AlignNow();
            }

            _textMesh.text = feedbackMsg;

            ApplyGradient();

            _currentAnimation = StartCoroutine(AnimateFeedbackRoutine());
        }

        [ContextMenu("▶ Test Play Animation")]
        public void TestPlayAnimation()
        {
            if (Application.isPlaying)
            {
                ShowFeedback("BOOM!");
            }
            else
            {
                Debug.LogWarning("[ArcRollFeedbackTextAnimator] You must be in Play Mode to test the animation!");
            }
        }

        private void ApplyGradient()
        {
            if (_textMesh == null) return;

            if (useGradient && textGradient != null)
            {
                // Calculate directional gradient mapping
                float angleRad = gradientAngle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
                float maxProj = 0.5f * (Mathf.Abs(dir.x) + Mathf.Abs(dir.y));
                
                // Avoid division by zero
                if (maxProj < 0.0001f) maxProj = 0.0001f;

                System.Func<Vector2, float> GetGradientTime = (corner) => 
                {
                    float proj = Vector2.Dot(corner, dir);
                    return Mathf.Clamp01((proj + maxProj) / (2f * maxProj));
                };

                // Evaluate the gradient at the 4 corners of the text's bounding box
                Color tl = textGradient.Evaluate(GetGradientTime(new Vector2(-0.5f, 0.5f)));
                Color tr = textGradient.Evaluate(GetGradientTime(new Vector2(0.5f, 0.5f)));
                Color bl = textGradient.Evaluate(GetGradientTime(new Vector2(-0.5f, -0.5f)));
                Color br = textGradient.Evaluate(GetGradientTime(new Vector2(0.5f, -0.5f)));

                // Enable vertex gradient and apply the 4 corner colors
                _textMesh.enableVertexGradient = true;
                _textMesh.colorGradient = new VertexGradient(tl, tr, bl, br);
                // Use white as the base so the gradient colors show through accurately
                // Only override color if we are playing so it doesn't get permanently saved as white
                if (Application.isPlaying) _textMesh.color = Color.white;
            }
            else
            {
                _textMesh.enableVertexGradient = false;
                if (Application.isPlaying && _originalColor != default(Color))
                {
                    _textMesh.color = _originalColor;
                }
            }
        }

        private void OnValidate()
        {
            ApplyGradient();
        }

        private IEnumerator AnimateFeedbackRoutine()
        {
            InitializeReferences();
            
            // 1. Setup Positions
            _textMesh.alpha = 0f;
            Vector2 startPos = _originalAnchoredPosition + new Vector2(startXOffset, 0f);
            Vector2 centerPos = _originalAnchoredPosition;
            Vector2 endPos = _originalAnchoredPosition + new Vector2(endXOffset, 0f);

            // Turn ON Italic (Inertia effect) while sliding in
            _textMesh.fontStyle = _originalFontStyle | FontStyles.Italic;

            // 2. SLIDE IN (Left -> Center)
            float t = 0f;
            while (t < slideInDuration)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(t / slideInDuration);
                
                // Evaluate the curve to get the spring/pop effect
                float curvedTime = slideInCurve.Evaluate(normalizedTime);
                
                _rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, centerPos, curvedTime);
                
                // Fade in quickly during the first half of the slide
                _textMesh.alpha = Mathf.Lerp(0f, 1f, normalizedTime * 2f);
                
                yield return null;
            }
            _rectTransform.anchoredPosition = centerPos;
            // Force full opacity (applies to both solid and gradient modes)
            _textMesh.alpha = 1f;

            // 3. HOLD (Stay in center so player can read it)
            // Turn OFF Italic (Straighten out) while holding
            _textMesh.fontStyle = _originalFontStyle & ~FontStyles.Italic;
            
            float holdTime = 0f;
            while (holdTime < holdDuration)
            {
                holdTime += Time.deltaTime;
                yield return null;
            }

            // 4. SLIDE OUT (Center -> Right)
            // Turn ON Italic (Inertia effect) while sliding out
            _textMesh.fontStyle = _originalFontStyle | FontStyles.Italic;
            
            t = 0f;
            while (t < slideOutDuration)
            {
                t += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(t / slideOutDuration);
                float curvedTime = slideOutCurve.Evaluate(normalizedTime);
                
                _rectTransform.anchoredPosition = Vector2.LerpUnclamped(centerPos, endPos, curvedTime);
                
                // Fade out smoothly as it leaves
                _textMesh.alpha = Mathf.Lerp(1f, 0f, normalizedTime * 1.5f);
                
                yield return null;
            }

            // Cleanup and hide
            _rectTransform.anchoredPosition = _originalAnchoredPosition; 
            
            // Restore gradient/color settings back to defaults
            if (useGradient)
            {
                _textMesh.enableVertexGradient = false;
            }

            Color transparentColor = _originalColor;
            transparentColor.a = 0f;
            _textMesh.color = transparentColor;

            _textMesh.fontStyle = _originalFontStyle;
            _currentAnimation = null;
        }

#if UNITY_EDITOR
        private double _editorAnimStartTime;

        [ContextMenu("▶ Test Edit Mode Animation (No Play Required)")]
        public void TestEditModeAnimation()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[ArcRollFeedbackTextAnimator] You are in Play Mode. Use 'Test Play Animation' instead.");
                return;
            }
            
            if (_textMesh == null)
            {
                Debug.LogWarning("[ArcRollFeedbackTextAnimator] Assign the Target Text first!");
                return;
            }

            InitializeReferences();

            _textMesh.text = "BOOM!";
            _textMesh.color = _originalColor;
            
            UnityEditor.EditorApplication.update -= EditorAnimationTick;
            _editorAnimStartTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update += EditorAnimationTick;
        }
        
        private void EditorAnimationTick()
        {
            if (_textMesh == null || Application.isPlaying)
            {
                UnityEditor.EditorApplication.update -= EditorAnimationTick;
                return;
            }

            float t = (float)(UnityEditor.EditorApplication.timeSinceStartup - _editorAnimStartTime);
            
            float totalSlideIn = slideInDuration;
            float totalHold = totalSlideIn + holdDuration;
            float totalSlideOut = totalHold + slideOutDuration;

            Vector2 startPos = _originalAnchoredPosition + new Vector2(startXOffset, 0f);
            Vector2 centerPos = _originalAnchoredPosition;
            Vector2 endPos = _originalAnchoredPosition + new Vector2(endXOffset, 0f);

            if (t < totalSlideIn)
            {
                _textMesh.fontStyle = _originalFontStyle | FontStyles.Italic;
                float norm = Mathf.Clamp01(t / slideInDuration);
                _rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, centerPos, slideInCurve.Evaluate(norm));
                _textMesh.alpha = Mathf.Lerp(0f, 1f, norm * 2f);
            }
            else if (t < totalHold)
            {
                _textMesh.fontStyle = _originalFontStyle & ~FontStyles.Italic;
                _rectTransform.anchoredPosition = centerPos;
                _textMesh.alpha = 1f;
            }
            else if (t < totalSlideOut)
            {
                _textMesh.fontStyle = _originalFontStyle | FontStyles.Italic;
                float norm = Mathf.Clamp01((t - totalHold) / slideOutDuration);
                _rectTransform.anchoredPosition = Vector2.LerpUnclamped(centerPos, endPos, slideOutCurve.Evaluate(norm));
                _textMesh.alpha = Mathf.Lerp(1f, 0f, norm * 1.5f);
            }
            else
            {
                // Reset position back to the exact reference hold position you set in the editor!
                _rectTransform.anchoredPosition = _originalAnchoredPosition; 
                
                Color transparentColor = _originalColor;
                transparentColor.a = 0f;
                _textMesh.color = transparentColor;
                
                _textMesh.fontStyle = _originalFontStyle;
                UnityEditor.EditorApplication.update -= EditorAnimationTick;
            }
            
            // Force the editor to redraw the Canvas so we can see the animation in real-time
            UnityEditor.EditorUtility.SetDirty(_rectTransform);
            UnityEditor.EditorUtility.SetDirty(_textMesh);
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
