using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcRoll.Core;

namespace ArcRoll.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class BasketballHoop : MonoBehaviour
    {
        [Header("Effects")]
        [Tooltip("Drag the Basketball_Score_VFX prefab here!")]
        [SerializeField] private GameObject scoreParticlesPrefab;
        [SerializeField] private ParticleSystem scoreParticles;
        [SerializeField] private AudioSource scoreAudio;
        [Tooltip("Drag your swish/score audio clip here!")]
        [SerializeField] private AudioClip scoreClip;

        [Header("Vacuum Magic")]
        [Tooltip("Place an empty GameObject exactly in the center of the ring and drag it here.")]
        [SerializeField] private Transform hoopCenter;
        
        public Vector3 TargetPoint => hoopCenter != null ? hoopCenter.position : transform.position;
        
        [Tooltip("How fast the ball gets sucked into the center before dropping (in seconds).")]
        [SerializeField] private float vacuumDuration = 0.15f;

        [Header("Cleanup")]
        [Tooltip("Seconds after a score (or a miss) before the hoop cleans itself up.")]
        [SerializeField] private float despawnDelay = 2.0f;

        [Header("UI References")]
        [Tooltip("Drag your TextMeshProUGUI element here for the countdown timer!")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Custom Trigger")]
        [Tooltip("Drag the child GameObject with the scoring trigger collider here. If left empty, it will search for a child named 'Vaccum suction zone'.")]
        [SerializeField] private Collider scoreTrigger;

        private int scoreValue = 3;
        public void SetScoreValue(int points) => scoreValue = points;

        private bool hasScored = false;
        private Ball associatedBall = null;

        // Ball tracking to ensure it crosses entirely from top to bottom
        private Ball activeEnteringBall = null;
        private Rigidbody activeEnteringRb = null;
        private bool hasEnteredFromTop = false;

        private void Awake()
        {
            // Auto-detect custom trigger if not explicitly assigned
            if (scoreTrigger == null)
            {
                Transform t = transform.Find("Vaccum suction zone");
                if (t == null) t = FindChildRecursive(transform, "Vaccum suction zone");
                if (t != null) scoreTrigger = t.GetComponent<Collider>();
            }

            if (scoreTrigger != null)
            {
                scoreTrigger.isTrigger = true;
                var listener = scoreTrigger.gameObject.GetComponent<BasketballScoreTrigger>();
                if (listener == null)
                {
                    listener = scoreTrigger.gameObject.AddComponent<BasketballScoreTrigger>();
                }
                listener.onTriggerEnterEvent = HandleScoreTriggerEnter;
                listener.onTriggerExitEvent = HandleScoreTriggerExit;
            }
            else
            {
                Collider col = GetComponent<Collider>();
                if (col != null && !col.isTrigger)
                {
                    Debug.LogWarning("[BasketballHoop] Collider was not set to Trigger! Forcing isTrigger = true.");
                    col.isTrigger = true;
                }
            }

            // Auto-detect the HoopCenter transform if not explicitly dragged in the Inspector
            if (hoopCenter == null)
            {
                hoopCenter = transform.Find("HoopCenter");
                if (hoopCenter == null)
                {
                    hoopCenter = FindChildRecursive(transform, "HoopCenter");
                }
            }

            // Ensure AudioSource exists and is properly configured for VR 3D audio
            if (scoreAudio == null)
            {
                scoreAudio = GetComponent<AudioSource>();
                if (scoreAudio == null)
                {
                    scoreAudio = gameObject.AddComponent<AudioSource>();
                }
            }

            scoreAudio.spatialBlend = 1.0f; // 100% 3D Audio
            scoreAudio.rolloffMode = AudioRolloffMode.Linear; // Linear dropoff so it's clearly audible
            scoreAudio.minDistance = 3.0f;
            scoreAudio.maxDistance = 30.0f;
            scoreAudio.playOnAwake = false;

            SetTimerVisible(false);
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        public void SetTimerText(string text, Color color)
        {
            if (timerText != null)
            {
                timerText.text = text;
                timerText.color = color;
            }
        }

        public void SetTimerVisible(bool visible)
        {
            if (timerText != null)
            {
                timerText.gameObject.SetActive(visible);
            }
        }

        public void RegisterBall(Ball ball)
        {
            if (ball == null) return;
            
            associatedBall = ball;
            associatedBall.OnStateChanged += OnBallStateChanged;
        }

        private void OnBallStateChanged(Ball ball, Ball.BallState state)
        {
            if ((state == Ball.BallState.Dead || state == Ball.BallState.Missed) && !hasScored)
            {
                // The player missed! The ball hit the floor. Clean up the hoop immediately so the next one can spawn.
                if (associatedBall != null)
                {
                    associatedBall.OnStateChanged -= OnBallStateChanged;
                }
                
                GameObject objectToDestroy = transform.parent != null ? transform.root.gameObject : gameObject;
                Destroy(objectToDestroy, despawnDelay);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // If we are using a custom scoreTrigger child, ignore parent trigger events
            if (scoreTrigger != null) return;

            HandleScoreTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (scoreTrigger != null) return;

            HandleScoreTriggerExit(other);
        }

        private void HandleScoreTriggerEnter(Collider other)
        {
            if (hasScored) return;

            Ball ball = other.GetComponentInParent<Ball>();
            if (ball != null && ball.Type == Ball.BallType.Basketball && ball.State != Ball.BallState.Dead)
            {
                // FAKE GOAL PREVENTION: 
                // Ensure the ball is falling DOWNWARDS through the hoop, not popping up from below!
                Rigidbody rb = ball.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (rb.linearVelocity.y > 0.1f) 
                    {
                        Debug.Log("[BasketballHoop] Fake Goal Rejected: Ball is moving upwards through the hoop!");
                        return; 
                    }

                    float ballY = ball.transform.position.y;
                    float triggerY = hoopCenter != null ? hoopCenter.position.y : transform.position.y;
                    
                    // Ball must enter from above the hoop center plane
                    if (ballY > triggerY - 0.1f)
                    {
                        activeEnteringBall = ball;
                        hasEnteredFromTop = true;
                        StartCoroutine(SuckToCenter(rb)); // Apply gentle nudge down and centered
                        Debug.Log("[BasketballHoop] Ball entered from top. Waiting for complete cross to exit bottom.");
                    }
                }
            }
            else if (other.CompareTag("Basketball"))
            {
                // Fallback for non-scripted basketballs
                Rigidbody rb = other.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    if (rb.linearVelocity.y > 0.1f) return;

                    float ballY = rb.position.y;
                    float triggerY = hoopCenter != null ? hoopCenter.position.y : transform.position.y;

                    if (ballY > triggerY - 0.1f)
                    {
                        activeEnteringRb = rb;
                        hasEnteredFromTop = true;
                        StartCoroutine(SuckToCenter(rb));
                    }
                }
            }
        }

        private void HandleScoreTriggerExit(Collider other)
        {
            if (hasScored) return;

            Ball ball = other.GetComponentInParent<Ball>();
            if (ball != null && ball == activeEnteringBall && hasEnteredFromTop)
            {
                float exitY = ball.transform.position.y;
                float triggerY = hoopCenter != null ? hoopCenter.position.y : transform.position.y;

                // Properly crossed: exit position is below the hoop center plane
                if (exitY < triggerY + 0.1f)
                {
                    hasScored = true;
                    ball.hasScored = true;
                    ball.ReleaseAfterScore(); // Restore gravity and release assists
                    Score();
                    Debug.Log("[BasketballHoop] Goal Confirmed: Ball successfully crossed from top to bottom.");
                }
                
                // Clear tracking state
                activeEnteringBall = null;
                hasEnteredFromTop = false;
            }
            else if (other.CompareTag("Basketball"))
            {
                Rigidbody rb = other.GetComponentInParent<Rigidbody>();
                if (rb != null && rb == activeEnteringRb && hasEnteredFromTop)
                {
                    float exitY = rb.position.y;
                    float triggerY = hoopCenter != null ? hoopCenter.position.y : transform.position.y;

                    if (exitY < triggerY + 0.1f)
                    {
                        hasScored = true;
                        rb.isKinematic = false;
                        rb.useGravity = true;
                        var col = rb.GetComponent<Collider>();
                        if (col != null) col.isTrigger = false;
                        rb.linearVelocity = Vector3.down * 3.5f;
                        Score();
                    }

                    activeEnteringRb = null;
                    hasEnteredFromTop = false;
                }
            }
        }

        private System.Collections.IEnumerator SuckToCenter(Rigidbody ballRb)
        {
            if (ballRb != null && hoopCenter != null)
            {
                // Capture the ball to ensure a mathematically perfect, rattle-free swish!
                ballRb.isKinematic = true; 
                
                // Temporarily disable solid collision so it passes cleanly through the rim without bouncing out!
                var col = ballRb.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
                
                Vector3 startPos = ballRb.position;
                Vector3 endPos = hoopCenter.position;
                
                // The vacuum perfectly centers the ball over vacuumDuration
                float elapsed = 0f;
                while (elapsed < vacuumDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / vacuumDuration;
                    
                    // Smooth step for a buttery smooth suction
                    t = t * t * (3f - 2f * t); 
                    
                    ballRb.MovePosition(Vector3.Lerp(startPos, endPos, t));
                    yield return new WaitForFixedUpdate();
                }
                
                ballRb.position = endPos;
                
                // Release the ball straight down for the satisfying exit
                ballRb.isKinematic = false;
                
                // Re-enable solid collision now that it is safely inside the net
                if (col != null) col.isTrigger = false; 

                ballRb.linearVelocity = Vector3.down * 4.5f; // Shoot it cleanly down through the net
            }
        }

        private void Score()
        {
            Vector3 soundPos = hoopCenter != null ? hoopCenter.position : transform.position;

            // Instantiate Prefab at hoopCenter level
            if (scoreParticlesPrefab != null)
            {
                GameObject vfxObj = Instantiate(scoreParticlesPrefab, soundPos, Quaternion.identity);
                var pSystems = vfxObj.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in pSystems)
                {
                    ps.Play();
                }
                Destroy(vfxObj, 3.0f);
            }
            else if (scoreParticles != null)
            {
                scoreParticles.Play();
            }

            if (scoreAudio != null && scoreClip != null)
            {
                scoreAudio.PlayOneShot(scoreClip, 1.0f);
            }
            else if (scoreClip != null)
            {
                AudioSource.PlayClipAtPoint(scoreClip, soundPos, 1.0f);
            }
            else if (scoreAudio != null)
            {
                scoreAudio.Play();
            }

            if (ArcRollScoreManager.Instance != null)
            {
                ArcRollScoreManager.Instance.IncrementStreak(); // Increase combo streak!
                ArcRollScoreManager.Instance.AddScore(scoreValue);
            }
            else
            {
                Debug.Log($"[BasketballHoop] Scored {scoreValue} points! (No ScoreManager found)");
            }

            if (ArcRollMotivationalManager.Instance != null)
            {
                // Without physical rim collision detection, we just pass true/false randomly for Swish 
                // or you can implement distance checks later.
                bool randomPerfect = UnityEngine.Random.value > 0.6f;
                ArcRollMotivationalManager.Instance.ReportScore(ArcRollMotivationalManager.SportType.Basketball, randomPerfect);
            }

            if (associatedBall != null)
            {
                associatedBall.OnStateChanged -= OnBallStateChanged;
            }

            // Immediately destroy the hoop a few seconds after scoring
            GameObject objectToDestroy = transform.parent != null ? transform.root.gameObject : gameObject;
            Destroy(objectToDestroy, despawnDelay);
        }
    }

    /// <summary>
    /// Redirects trigger events on a child GameObject to the parent BasketballHoop manager.
    /// </summary>
    public class BasketballScoreTrigger : MonoBehaviour
    {
        public System.Action<Collider> onTriggerEnterEvent;
        public System.Action<Collider> onTriggerExitEvent;

        private void OnTriggerEnter(Collider other)
        {
            onTriggerEnterEvent?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            onTriggerExitEvent?.Invoke(other);
        }
    }
}
