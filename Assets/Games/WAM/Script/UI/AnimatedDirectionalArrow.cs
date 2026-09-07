using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace WhackAMole.UI
{
    /// <summary>
    /// Attach this script to an Arrow (Sprite or UI Image).
    /// It animates the arrow moving in a direction and smoothly fading in/out (blinking).
    /// 
    /// TO CREATE TRACES/GHOSTS:
    /// 1. Duplicate your arrow object 2 or 3 times.
    /// 2. Change the "Start Delay" on the duplicates (e.g., Arrow1 = 0.0s, Arrow2 = 0.15s, Arrow3 = 0.3s).
    /// They will perfectly follow each other in a loop, creating a ghosting trace effect!
    /// </summary>
    [ExecuteAlways]
    public class AnimatedDirectionalArrow : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("The local direction the arrow should move. (e.g., Vector3.right for X-axis)")]
        public Vector3 localMoveDirection = Vector3.right;
        
        [Tooltip("How far the arrow travels before resetting.")]
        public float moveDistance = 0.5f;
        
        [Tooltip("How long it takes to complete one swipe motion.")]
        public float moveDuration = 1.0f;

        [Header("Timing & Traces")]
        [Tooltip("Delay before this specific arrow starts moving. INCREASE THIS ON DUPLICATES TO CREATE TRACES!")]
        public float startDelay = 0f;
        
        [Tooltip("Time to wait after the arrow vanishes before starting the next loop.")]
        public float loopDelay = 0.3f;

        [Header("Blinking & Scaling")]
        [Tooltip("If true, the arrow fades in at the start, and fades out as it reaches the end.")]
        public bool useFading = true;
        
        [Tooltip("If true, the arrow slightly grows as it moves, adding a pulsing feel.")]
        public bool pulseScale = true;
        public float maxScaleMultiplier = 1.2f;

        [Header("Editor Preview")]
        [Tooltip("Check this box to preview the animation loop perfectly in the Editor without having to press Play!")]
        public bool previewInEditor = false;

        private Vector3 startLocalPos;
        private Vector3 startLocalScale;
        
        // Supports both UI (CanvasGroup) and 3D Sprites (SpriteRenderer)
        private CanvasGroup canvasGroup;
        private SpriteRenderer spriteRenderer;
        private Renderer meshRenderer; // Supports 3D Models/Meshes!

        private float _timeAwakened;
        private bool _isInitialized = false;

        void Awake()
        {
            InitializeData();
        }

        void OnEnable()
        {
            InitializeData();
            ResetTimer();
        }

        private void InitializeData()
        {
            if (_isInitialized) return;

            startLocalPos = transform.localPosition;
            startLocalScale = transform.localScale;

            canvasGroup = GetComponent<CanvasGroup>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            meshRenderer = GetComponent<Renderer>();

            if (canvasGroup == null && GetComponent<Graphic>() != null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            _isInitialized = true;
        }

        [ContextMenu("Reset Animation Timer")]
        public void ResetTimer()
        {
            _timeAwakened = Application.isPlaying ? Time.time : GetEditorTime();
        }

        private float GetEditorTime()
        {
#if UNITY_EDITOR
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.time;
#endif
        }

        void Update()
        {
            // 2. Editor Preview logic
            if (!Application.isPlaying && !previewInEditor)
            {
                // Reset transforms so it isn't left in a weird state when disabled
                if (_isInitialized)
                {
                    transform.localPosition = startLocalPos;
                    transform.localScale = startLocalScale;
                    SetAlpha(1f);
                }
                return;
            }

            // 3. Animation Math
            float currentTime = Application.isPlaying ? Time.time : GetEditorTime();
            float elapsedSinceStart = currentTime - _timeAwakened;

            // Wait for start delay
            if (elapsedSinceStart < startDelay)
            {
                SetAlpha(0f);
            }
            else
            {
                float timeInLoop = (elapsedSinceStart - startDelay) % (moveDuration + loopDelay);

                if (timeInLoop <= moveDuration)
                {
                    // ANIMATING
                    float t = timeInLoop / moveDuration;

                    // Movement
                    transform.localPosition = startLocalPos + (localMoveDirection.normalized * (t * moveDistance));

                    // Fading
                    if (useFading)
                    {
                        float alpha = Mathf.Sin(t * Mathf.PI);
                        SetAlpha(alpha);
                    }
                    else
                    {
                        SetAlpha(1f);
                    }

                    // Scaling
                    if (pulseScale)
                    {
                        float scalePulse = Mathf.Lerp(1f, maxScaleMultiplier, Mathf.Sin(t * Mathf.PI));
                        transform.localScale = startLocalScale * scalePulse;
                    }
                }
                else
                {
                    // WAITING for loop delay
                    SetAlpha(0f);
                    transform.localPosition = startLocalPos;
                    transform.localScale = startLocalScale;
                }
            }

            // 4. Force Editor updates so it plays smoothly without moving the mouse
#if UNITY_EDITOR
            if (!Application.isPlaying && previewInEditor)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        /// <summary>
        /// Handles fading for Canvas UI elements, standard 3D Sprites, and 3D Meshes!
        /// </summary>
        private void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
            
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }

            // Support for 3D Arrow Models!
            if (meshRenderer != null && meshRenderer.material.HasProperty("_Color"))
            {
                Color c = meshRenderer.material.color;
                c.a = alpha;
                meshRenderer.material.color = c;
            }
        }
    }
}
