using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PopstrikeVR.Core;

namespace PopstrikeVR.Gameplay
{
    /// <summary>
    /// Enforces the continuous slash logic for Blue Blade balloons.
    /// Strictly Unidirectional: The player MUST start at the first balloon in the sequence and slice continuously.
    /// </summary>
    public class BladeSlashManager : MonoBehaviour
    {
        public static BladeSlashManager Instance { get; private set; }

        private List<BladeBalloon> activeChain = new List<BladeBalloon>();
        private int currentTargetIndex = 0;
        private float lastHitTime = 0f;
        private Vector3 lastSlashVelocity = Vector3.up;
        
        // Made dynamic instead of const to allow difficulty scaling
        private float maxTimeBetweenHits = 0.35f; 
        
        private bool isTracking = false;
        public bool IsTracking => isTracking;
        public bool IsSequenceActive() => activeChain.Count > 0;

        [Header("Tutorial System")]
        [Tooltip("The Tutorial Animator prefab containing the slash gesture icon.")]
        public PopstrikeVR.UI.TutorialGestureAnimator tutorialPrefab;
        private PopstrikeVR.UI.TutorialGestureAnimator spawnedTutorial;
        
        public static bool HasCompletedTutorial = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // Enforce continuous motion: If started slicing but paused too long, reset the chain.
            if (isTracking && activeChain.Count > 0)
            {
                if (Time.time - lastHitTime > maxTimeBetweenHits)
                {
                    FailChain(null);
                }
            }

            // Clean up the tutorial if the balloons timed out and despawned naturally
            if (activeChain.Count > 0)
            {
                // If the first balloon is dead or despawned, the whole chain is gone
                if (activeChain[0] == null || !activeChain[0].gameObject.activeInHierarchy || activeChain[0].IsPopped)
                {
                    ClearSequence();
                }
            }
        }

        public void ClearSequence()
        {
            isTracking = false;
            activeChain.Clear();
            currentTargetIndex = 0;

            if (spawnedTutorial != null)
            {
                spawnedTutorial.StopTutorial();
                Destroy(spawnedTutorial.gameObject);
                spawnedTutorial = null;
            }
        }

        public void RegisterSequence(List<GameObject> balloons)
        {
            ClearSequence(); // Safely clean up any existing sequence first
            
            // Dynamic Difficulty Forgiveness for Zig-Zag patterns
            maxTimeBetweenHits = 1.0f; // Default Medium
            string diff = PopstrikeVR.Core.TemporarySessionData.Difficulty;
            if (!string.IsNullOrEmpty(diff))
            {
                if (diff.Equals("Easy", System.StringComparison.OrdinalIgnoreCase)) maxTimeBetweenHits = 1.5f; 
                else if (diff.Equals("Medium", System.StringComparison.OrdinalIgnoreCase)) maxTimeBetweenHits = 1.0f;
                else if (diff.Equals("Hard", System.StringComparison.OrdinalIgnoreCase)) maxTimeBetweenHits = 0.7f; 
            }

            foreach (var b in balloons)
            {
                if (b.TryGetComponent<BladeBalloon>(out var blade))
                {
                    activeChain.Add(blade);
                    blade.ResetState(); // Ensure they are clean
                }
            }

            // Trigger the visualizer on the leader balloon
            if (activeChain.Count > 0)
            {
                var visualizer = activeChain[0].GetComponent<BladePathVisualizer>();
                if (visualizer != null)
                {
                    List<Vector3> points = new List<Vector3>();
                    foreach (var balloon in activeChain) points.Add(balloon.transform.position);
                    visualizer.ShowPath(points);
                }
            }

            PlayTutorialSequence();
        }

        public void PlayTutorialSequence()
        {
            if (!HasCompletedTutorial && tutorialPrefab != null && activeChain != null && activeChain.Count > 1)
            {
                if (spawnedTutorial == null)
                {
                    spawnedTutorial = Instantiate(tutorialPrefab, activeChain[0].transform.position, Quaternion.identity);
                }
                
                Transform[] points = new Transform[activeChain.Count];
                for (int i = 0; i < activeChain.Count; i++)
                {
                    points[i] = activeChain[i].transform;
                }
                
                spawnedTutorial.PlaySlashSequenceTutorial(points);
            }
        }

        public bool IsFirstInChain(BladeBalloon balloon)
        {
            return activeChain != null && activeChain.Count > 0 && activeChain[0] == balloon;
        }

        public bool TrySlashBalloon(BladeBalloon hitBalloon, Vector3 velocityDir, bool isLeft)
        {
            if (activeChain == null || activeChain.Count == 0) return false;

            if (velocityDir != default && velocityDir.sqrMagnitude > 0)
            {
                lastSlashVelocity = velocityDir.normalized;
            }

            if (!isTracking)
            {
                if (hitBalloon == activeChain[0])
                {
                    // Start Normal (Strictly unidirectional)
                    isTracking = true;
                    lastHitTime = Time.time;
                    hitBalloon.MarkSliced();
                    
                    if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
                        PopstrikeVR.Interaction.GestureDetector.Instance.LockGesture(isLeft, PopstrikeVR.Interaction.GestureState.OPEN_BLADE);
                        
                    CheckCompletion();
                    return true;
                }
                else
                {
                    // Hit a balloon out of order or wrong direction
                    FailChain(hitBalloon);
                    return false;
                }
            }

            // We are currently tracking a slash. 
            // In fast VR slashes, different hand bones can trigger collisions out of order (e.g. 1 -> 3 -> 2).
            // As long as the balloon is part of the active chain (and unsliced, which is filtered in BladeBalloon),
            // we slice it and check if the entire chain is complete.
            if (activeChain.Contains(hitBalloon))
            {
                lastHitTime = Time.time;
                hitBalloon.MarkSliced();
                CheckCompletion();
                return true;
            }
            else if (!hitBalloon.IsPopped && !hitBalloon.IsSliced)
            {
                // If it's part of the chain but hit out of order, fail
                FailChain(hitBalloon);
                return false;
            }
            return false;
        }

        private void CheckCompletion()
        {
            bool allSliced = true;
            foreach (var balloon in activeChain)
            {
                if (balloon != null && !balloon.IsSliced)
                {
                    allSliced = false;
                    break;
                }
            }

            if (allSliced)
            {
                ClearChain();
            }
        }

        private void ClearChain()
        {
            if (PopstrikeVR.Interaction.GestureDetector.Instance != null)
            {
                PopstrikeVR.Interaction.GestureDetector.Instance.UnlockGesture(true);
                PopstrikeVR.Interaction.GestureDetector.Instance.UnlockGesture(false);
            }

            isTracking = false;
            HasCompletedTutorial = true; // They successfully learned to slash a chain!
            
            if (spawnedTutorial != null)
            {
                spawnedTutorial.StopTutorial();
                Destroy(spawnedTutorial.gameObject);
                spawnedTutorial = null;
            }

            if (activeChain.Count > 0)
            {
                // Hide Visualizer
                var visualizer = activeChain[0].GetComponent<BladePathVisualizer>();
                if (visualizer != null) visualizer.HidePath();

                // 1. Calculate Center Point
                Vector3 centerPoint = Vector3.zero;
                foreach (var balloon in activeChain)
                {
                    centerPoint += balloon.transform.position;
                }
                centerPoint /= activeChain.Count;

                // 2. Play Audio/Haptics for the final shatter (optional)
                // PopstrikeFeedbackManager.Instance?.PlayShatterSFX();

                // 3. Pop them in a cascading chain reaction (80ms intervals)!
                StartCoroutine(CascadePopRoutine(new List<BladeBalloon>(activeChain)));
            }

            PopstrikeVR.Gameplay.ComboManager.Instance?.RegisterHit(80);
            activeChain.Clear();
            currentTargetIndex = 0;
        }

        private IEnumerator CascadePopRoutine(List<BladeBalloon> poppingChain)
        {

            foreach (var balloon in poppingChain)
            {
                if (balloon != null && !balloon.IsPopped)
                {
                    balloon.TriggerFinalPop(balloon.transform.position);
                    // 80ms interval as per GDD
                    yield return new WaitForSeconds(0.08f); 
                }
            }
        }

        private void FailChain(BladeBalloon errorBalloon = null)
        {
            // Try to report the error. If cooldown is active, it returns false, so we ignore the mistake!
            bool canReport = false;
            if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
            {
                canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();
            }
            
            if (!canReport) return; // Completely ignore if cooldown is active
            
            Debug.LogWarning($"[BladeSlashManager] Chain Failed! canReport={canReport}. Slashed out of order or paused too long.");
            
            if (errorBalloon != null)
            {
                PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(errorBalloon.transform.position);
            }
            
            isTracking = false;
            currentTargetIndex = 0;

            if (spawnedTutorial != null)
            {
                spawnedTutorial.StopTutorial();
                Destroy(spawnedTutorial.gameObject);
                spawnedTutorial = null;
            }
            
            if (PopstrikeFeedbackManager.Instance != null)
                PopstrikeFeedbackManager.Instance.PlayErrorTone();

            foreach (var balloon in activeChain)
            {
                if (balloon != null)
                {
                    if (errorBalloon == null && balloon.IsSliced)
                    {
                        // Timeout failure: flash the ones we already sliced
                        balloon.FlashErrorAndReset(false, false);
                    }
                    else if (balloon != errorBalloon)
                    {
                        balloon.ResetState(); // Reset silently without vibrating
                    }
                }
            }
            
            if (errorBalloon != null)
            {
                PopstrikeVR.UI.HitIndicatorManager.Instance?.ShowWrong(errorBalloon.transform.position);
                errorBalloon.FlashErrorAndReset(false, false);
            }

            // Restore visualizer after the balloons finish their error flash/shake
            StartCoroutine(RestoreVisualizerRoutine());
        }

        private IEnumerator RestoreVisualizerRoutine()
        {
            if (activeChain.Count > 0)
            {
                var visualizer = activeChain[0].GetComponent<BladePathVisualizer>();
                if (visualizer != null) visualizer.HidePath(); // Hide during error flash
            }

            yield return new WaitForSeconds(0.4f); // Wait for the balloons to finish shaking and flashing

            if (activeChain.Count > 0)
            {
                var visualizer = activeChain[0].GetComponent<BladePathVisualizer>();
                if (visualizer != null)
                {
                    List<Vector3> points = new List<Vector3>();
                    foreach (var balloon in activeChain) points.Add(balloon.transform.position);
                    visualizer.ShowPath(points);
                }
            }
        }
    }
}
