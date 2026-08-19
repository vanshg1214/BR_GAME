using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PopstrikeVR.UI
{
    /// <summary>
    /// Animates 2D gesture icons in world space to visually demonstrate to the player 
    /// how to complete a specific task (Punch, Slash, Trace, TMT).
    /// </summary>
    public class TutorialGestureAnimator : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The child Transform holding the 2D UI Sprite representing the hand.")]
        public Transform handVisual;
        
        [Header("Animation Settings")]
        [Tooltip("How far back the hand starts before a punch (meters).")]
        public float punchDistance = 0.5f;
        
        [Tooltip("How long the slash animation is (meters).")]
        public float slashLength = 0.8f;
        
        [Tooltip("Speed for Trace and TMT animations (meters per second).")]
        public float movementSpeed = 0.6f; 

        private Coroutine currentAnimation;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            if (handVisual != null)
            {
                spriteRenderer = handVisual.GetComponent<SpriteRenderer>();
            }
        }

        private void Update()
        {
            // Ensure the root object always faces the camera so the 2D icon is readable
            if (Camera.main != null)
            {
                // UI Sprites usually need to look away from the camera to be visible (because their forward vector points backwards)
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }
        }

        public void StopTutorial()
        {
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
                currentAnimation = null;
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(1, 1, 1, 0); // Hide
            }
        }

        #region Punch (Orange)
        
        public void PlayPunchTutorial(Transform target)
        {
            StopTutorial();
            currentAnimation = StartCoroutine(PunchRoutine(target));
        }

        private IEnumerator PunchRoutine(Transform target)
        {
            while (true) 
            {
                if (target == null) yield break; // Safety check

                float elapsed = 0;
                float duration = 1.0f; // 1 second punch animation
                
                while (elapsed < duration)
                {
                    if (target == null) yield break;
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    
                    // Ease-in to a fast punch
                    float easeT = t * t;
                    
                    // Dynamically recalculate start position so it tracks if the target moves!
                    Vector3 currentStartPos = target.position;
                    if (Camera.main != null)
                    {
                        Vector3 dirToCam = (Camera.main.transform.position - target.position).normalized;
                        currentStartPos = target.position + (dirToCam * punchDistance);
                    }
                    else
                    {
                        currentStartPos = target.position + (Vector3.back * punchDistance);
                    }

                    transform.position = Vector3.Lerp(currentStartPos, target.position, easeT);
                    
                    FadeInOut(t);
                    yield return null;
                }
                
                yield return new WaitForSeconds(0.5f); // Pause before repeating
            }
        }

        #endregion

        #region Slash (Blue)

        public void PlaySlashTutorial(Transform target, Vector3 sliceDirection)
        {
            StopTutorial();
            currentAnimation = StartCoroutine(SlashRoutine(target, sliceDirection.normalized));
        }

        private IEnumerator SlashRoutine(Transform target, Vector3 sliceDir)
        {
            while (true)
            {
                if (target == null) yield break;

                float elapsed = 0;
                float duration = 0.8f; // Swift slash
                
                while (elapsed < duration)
                {
                    if (target == null) yield break;
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    
                    // Start at one edge, end at the other based on the slice direction (dynamically tracking!)
                    Vector3 startPos = target.position - (sliceDir * (slashLength / 2f));
                    Vector3 endPos = target.position + (sliceDir * (slashLength / 2f));

                    transform.position = Vector3.Lerp(startPos, endPos, t);
                    
                    FadeInOut(t);
                    yield return null;
                }
                
                yield return new WaitForSeconds(0.5f); 
            }
        }

        #endregion

        #region Slash Sequence (Blue)

        public void PlaySlashSequenceTutorial(Transform[] pathPoints)
        {
            if (pathPoints == null || pathPoints.Length < 2) return;
            StopTutorial();
            currentAnimation = StartCoroutine(SlashSequenceRoutine(pathPoints));
        }

        private IEnumerator SlashSequenceRoutine(Transform[] points)
        {
            while (true)
            {
                if (spriteRenderer != null) spriteRenderer.color = Color.white; // Keep visible
                if (points[0] == null) yield break;
                
                transform.position = points[0].position;
                
                for (int i = 0; i < points.Length - 1; i++)
                {
                    if (points[i] == null || points[i+1] == null) yield break;
                    Vector3 start = points[i].position;
                    Vector3 end = points[i + 1].position;
                    
                    float distance = Vector3.Distance(start, end);
                    // Fast slash through the sequence
                    float duration = distance / (movementSpeed * 2f); 
                    float elapsed = 0;

                    while (elapsed < duration)
                    {
                        if (points[i] == null || points[i+1] == null) yield break;
                        elapsed += Time.deltaTime;
                        float t = elapsed / duration;
                        transform.position = Vector3.Lerp(points[i].position, points[i + 1].position, t);
                        yield return null;
                    }
                }

                // Fade out at the end
                if (spriteRenderer != null)
                {
                    float fadeTimer = 0;
                    while (fadeTimer < 0.3f)
                    {
                        fadeTimer += Time.deltaTime;
                        Color c = spriteRenderer.color;
                        c.a = Mathf.Lerp(1, 0, fadeTimer / 0.3f);
                        spriteRenderer.color = c;
                        yield return null;
                    }
                }

                yield return new WaitForSeconds(0.6f); 
            }
        }

        #endregion

        #region Trace (Green)

        public void PlayTraceTutorial(Transform[] pathPoints)
        {
            if (pathPoints == null || pathPoints.Length < 2) return;
            StopTutorial();
            currentAnimation = StartCoroutine(TraceRoutine(pathPoints));
        }

        private IEnumerator TraceRoutine(Transform[] points)
        {
            while (true)
            {
                if (spriteRenderer != null) spriteRenderer.color = Color.white; // Keep visible during entire trace
                if (points[0] == null) yield break;
                
                transform.position = points[0].position;
                
                for (int i = 0; i < points.Length - 1; i++)
                {
                    if (points[i] == null || points[i+1] == null) yield break;
                    Vector3 start = points[i].position;
                    Vector3 end = points[i + 1].position;
                    
                    float distance = Vector3.Distance(start, end);
                    float duration = distance / movementSpeed;
                    float elapsed = 0;

                    while (elapsed < duration)
                    {
                        if (points[i] == null || points[i+1] == null) yield break;
                        elapsed += Time.deltaTime;
                        float t = elapsed / duration;
                        transform.position = Vector3.Lerp(points[i].position, points[i + 1].position, t);
                        yield return null;
                    }
                }

                // Fade out at the end
                if (spriteRenderer != null)
                {
                    float fadeTimer = 0;
                    while (fadeTimer < 0.3f)
                    {
                        fadeTimer += Time.deltaTime;
                        Color c = spriteRenderer.color;
                        c.a = Mathf.Lerp(1, 0, fadeTimer / 0.3f);
                        spriteRenderer.color = c;
                        yield return null;
                    }
                }

                yield return new WaitForSeconds(1.0f); // Longer pause before restarting complex trace
            }
        }

        #endregion

        #region TMT (White/Transparent)

        public void PlayTMTTutorial(Transform[] sequencePoints)
        {
            if (sequencePoints == null || sequencePoints.Length < 2) return;
            StopTutorial();
            currentAnimation = StartCoroutine(TMTRoutine(sequencePoints));
        }

        private IEnumerator TMTRoutine(Transform[] points)
        {
            while (true)
            {
                if (spriteRenderer != null) spriteRenderer.color = Color.white;
                
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] == null) yield break;
                    if (i == 0)
                    {
                        // Snap to first balloon and pause so the player sees it before it taps
                        transform.position = points[0].position;
                        yield return new WaitForSeconds(0.4f);
                    }
                    else
                    {
                        if (points[i-1] == null) yield break;
                        // Move to the next balloon smoothly
                        Vector3 start = points[i - 1].position;
                        Vector3 end = points[i].position;
                        
                        float distance = Vector3.Distance(start, end);
                        float duration = distance / movementSpeed; 
                        float elapsed = 0;

                        while (elapsed < duration)
                        {
                            if (points[i-1] == null || points[i] == null) yield break;
                            elapsed += Time.deltaTime;
                            float t = elapsed / duration;
                            transform.position = Vector3.Lerp(points[i - 1].position, points[i].position, t);
                            yield return null;
                        }
                    }

                    // Arrived at balloon: Simulate "Tap" (Physical 3D movement)
                    yield return StartCoroutine(SimulateTap());
                    
                    yield return new WaitForSeconds(0.2f); // Brief pause before moving to next
                }

                // Fade out
                if (spriteRenderer != null)
                {
                    float fadeTimer = 0;
                    while (fadeTimer < 0.3f)
                    {
                        fadeTimer += Time.deltaTime;
                        Color c = spriteRenderer.color;
                        c.a = Mathf.Lerp(1, 0, fadeTimer / 0.3f);
                        spriteRenderer.color = c;
                        yield return null;
                    }
                }

                yield return new WaitForSeconds(1.0f); 
            }
        }

        private IEnumerator SimulateTap()
        {
            if (handVisual == null) yield break;

            Vector3 origPos = handVisual.localPosition;
            // Move 5cm forward gently
            Vector3 tapPos = origPos + new Vector3(0, 0, 0.05f); 
            // Pull back 8cm
            Vector3 backPos = origPos + new Vector3(0, 0, -0.08f);

            // 1. Pull back slightly to prep (slow and soft)
            float t = 0;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                handVisual.localPosition = Vector3.Lerp(origPos, backPos, t / 0.2f);
                yield return null;
            }

            // 2. Strike forward (Gentle Tap)
            t = 0;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                handVisual.localPosition = Vector3.Lerp(backPos, tapPos, t / 0.2f);
                yield return null;
            }

            // 3. Pull back to resting position (slow recover)
            t = 0;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                handVisual.localPosition = Vector3.Lerp(tapPos, origPos, t / 0.2f);
                yield return null;
            }
            
            handVisual.localPosition = origPos;
        }

        #endregion

        private void FadeInOut(float t)
        {
            if (spriteRenderer != null)
            {
                // Fade in quickly for the first 20%, stay visible, fade out fast in the last 20%
                float alpha = 1f;
                if (t < 0.2f) alpha = Mathf.Lerp(0, 1, t / 0.2f);
                else if (t > 0.8f) alpha = Mathf.Lerp(1, 0, (t - 0.8f) / 0.2f);
                
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }
        }
    }
}
