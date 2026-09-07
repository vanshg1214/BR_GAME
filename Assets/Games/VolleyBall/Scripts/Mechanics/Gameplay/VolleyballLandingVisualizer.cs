using UnityEngine;
using Rehab.Volleyball.Core;

namespace Rehab.Volleyball.Mechanics
{
    /// <summary>
    /// Shows a tunnel of 4 rings (1 target + 3 ghost arcs) when the AI hits the ball.
    /// The rings arc along the ball's incoming parabolic trajectory.
    /// All rings disappear the moment the player hits the ball or misses.
    /// </summary>
    public class VolleyballLandingVisualizer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the Volleyball Ball in the scene.")]
        [SerializeField] private VolleyballBall targetBall;

        [Tooltip("Reference to the MeshRenderer of the landing ring. This is ring #4 (the final target).")]
        [SerializeField] private MeshRenderer ringRenderer;

        [Tooltip("REQUIRED for VR: Drag the CenterEyeAnchor transform here.")]
        [SerializeField] private Transform vrHeadTransform;

        [Header("Visualization Settings")]
        [Tooltip("How far below the headset Y the ring appears.")]
        [SerializeField] private float eyeLevelOffset = 0.15f;

        [Tooltip("The height level of the floor Y position.")]
        [SerializeField] private float floorY = 0.0f;

        [Header("Visualization Settings")]
        [Tooltip("How far forward (towards the net) to push the rings so they don't clip into the player's face/body.")]
        [SerializeField] private float forwardOffset = 0.3f;

        [Header("Tunnel Settings")]
        [Tooltip("The angle (in degrees from forward) beyond which the tunnel activates. Under this, only 1 ring shows.")]
        [SerializeField] private float tunnelActivationAngle = 60f;

        [Tooltip("How many ghost rings to place along the arc BEFORE the final ring. Set to 3 for 4 rings total.")]
        [SerializeField] private int ghostRingCount = 3;

        [Tooltip("How many meters apart each ring is spaced along the arc toward the dog.")]
        [SerializeField] private float ringSpacing = 0.7f;

        [Tooltip("The peak height of the arc above the straight line between target and dog launch point.")]
        [SerializeField] private float arcPeakHeight = 1.6f;

        // ── Internal state ──
        private bool hasRenderer = false;
        private Vector3 originalScale;
        private Color baseRingColor;

        // Ghost rings (3 rings placed along the arc, plus the main ring = 4 total)
        private System.Collections.Generic.List<GameObject> ghostRings =
            new System.Collections.Generic.List<GameObject>();

        // Calculated once per AI shot, then held steady
        private Vector3 lockedTargetPos;       // Where ring #4 (main) sits = ball's landing zone
        private Vector3 lockedLaunchPos;       // Where the dog was when it hit the ball
        private Vector3 lastProcessedTarget = Vector3.one * float.MaxValue;
        private bool showTunnelThisShot = false;

        public Vector3 PredictedLandingSpot { get; private set; } = Vector3.zero;
        public bool IsShowingRing { get; private set; } = false;

        // ───────────────────────────────────────────────────
        //  INIT
        // ───────────────────────────────────────────────────
        private void Start()
        {
            originalScale = transform.localScale;

            if (ringRenderer == null)
                ringRenderer = GetComponentInChildren<MeshRenderer>();

            if (ringRenderer != null)
            {
                hasRenderer = true;
                baseRingColor = ringRenderer.material != null ? ringRenderer.material.color : Color.white;

                // Build exactly ghostRingCount clones — no more, no less
                BuildGhostRings();
            }
            else
            {
                Debug.LogWarning("[LandingVisualizer] No MeshRenderer found! Assign it in the Inspector.");
            }

            // Auto-find head if not assigned
            if (vrHeadTransform == null && Camera.main != null)
            {
                vrHeadTransform = Camera.main.transform;
                Debug.Log("[LandingVisualizer] Auto-found Camera.main as head. Assign CenterEyeAnchor for best results.");
            }

            // Start hidden
            HideAll();
        }

        private void OnDestroy()
        {
            // Clean up ghost ring GameObjects when the script is destroyed
            foreach (var g in ghostRings)
            {
                if (g != null) Destroy(g);
            }
            ghostRings.Clear();
        }

        // ───────────────────────────────────────────────────
        //  UPDATE
        // ───────────────────────────────────────────────────
        private void Update()
        {
            if (targetBall == null || !hasRenderer) return;

            // === HIDE CASE 1: Game is not in an active rally or ball is inactive ===
            if (VolleyballGameManager.Instance == null
                || !VolleyballGameManager.Instance.IsRallyActive
                || !targetBall.IsBallActive)
            {
                HideAll();
                return;
            }

            // === HIDE CASE 2: Player just hit the ball ===
            if (targetBall.LastHitter == BallHitter.Player)
            {
                HideAll();
                return;
            }

            // === HIDE CASE 3: Ball is traveling toward the AI (not toward the player) ===
            // This catches the moment AFTER the player hits and before the AI hits next
            if (targetBall.LastHitter != BallHitter.AI)
            {
                HideAll();
                return;
            }

            // === SHOW: AI hit the ball — show rings ===
            Transform liveHead = vrHeadTransform != null ? vrHeadTransform
                                : (Camera.main != null ? Camera.main.transform : null);

            // Detect a brand new AI shot
            bool isNewShot = targetBall.LastTargetPosition != lastProcessedTarget;
            if (isNewShot)
            {
                // Capture the dog's exact position at the moment of the hit
                lockedLaunchPos = targetBall.transform.position;
                lastProcessedTarget = targetBall.LastTargetPosition;

                // Calculate where the final ring goes (locked in world space)
                // The GameManager already calculated this target based on the headset position at the moment of the shot
                lockedTargetPos = targetBall.LastTargetPosition;

                // Determine if we should show the tunnel based on the angle
                if (liveHead != null)
                {
                    Vector3 headForward = liveHead.forward;
                    headForward.y = 0;
                    headForward.Normalize();

                    Vector3 toTarget = lockedTargetPos - liveHead.position;
                    toTarget.y = 0;
                    toTarget.Normalize();

                    float angleToTarget = Vector3.Angle(headForward, toTarget);
                    showTunnelThisShot = angleToTarget >= tunnelActivationAngle;
                }
                else
                {
                    showTunnelThisShot = true; // Fallback
                }
            }

            // Apply forward offset so it's visible in front of the player
            Vector3 finalDisplayPos = lockedTargetPos + new Vector3(0, 0, forwardOffset);
            PredictedLandingSpot = finalDisplayPos;

            // Position the main (final) ring
            transform.position = finalDisplayPos;
            transform.localScale = originalScale;
            if (ringRenderer != null) ringRenderer.enabled = true;
            IsShowingRing = true;

            // Position the 3 ghost rings along the arc ONLY if angle is high enough
            if (showTunnelThisShot)
            {
                // Temporarily override lockedTargetPos for ghost calculations so the arc matches the visible ring
                Vector3 tempRealTarget = lockedTargetPos;
                lockedTargetPos = finalDisplayPos; 
                PositionGhostRings();
                lockedTargetPos = tempRealTarget; // Restore true target memory
            }
            else
            {
                // Ensure they stay hidden if angle is < 60
                foreach (var g in ghostRings)
                {
                    if (g != null && g.activeSelf) g.SetActive(false);
                }
            }
        }

        // ───────────────────────────────────────────────────
        //  GHOST RING CREATION
        // ───────────────────────────────────────────────────
        private void BuildGhostRings()
        {
            // Safety: destroy any stale ghosts first
            foreach (var old in ghostRings)
            {
                if (old != null) Destroy(old);
            }
            ghostRings.Clear();

            for (int i = 0; i < ghostRingCount; i++)
            {
                // Clone only the ringRenderer's own GameObject
                GameObject ghost = Instantiate(ringRenderer.gameObject);
                ghost.name = $"TunnelRing_{i}";

                // Detach from everything — place at scene root so it's fully independent
                ghost.transform.SetParent(null, worldPositionStays: false);

                // Destroy ALL MonoBehaviours so no clone ever runs Start() or spawns more rings
                foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
                {
                    mb.enabled = false;
                    Destroy(mb);
                }

                // Give it a slightly transparent tint (rings closer to the dog are more faded)
                // i=0 is nearest the target (most visible), i=2 is nearest the dog (most faded)
                float fade = Mathf.Lerp(0.85f, 0.35f, (float)i / Mathf.Max(ghostRingCount - 1, 1));
                var mr = ghost.GetComponent<MeshRenderer>() ?? ghost.GetComponentInChildren<MeshRenderer>();
                if (mr != null)
                {
                    mr.enabled = true; // Ensure the clone's renderer is forced ON
                    if (mr.sharedMaterial != null)
                    {
                        var mat = new Material(mr.sharedMaterial);
                        Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") :
                                  mat.HasProperty("_Color") ? mat.color : Color.white;
                        c.a = Mathf.Clamp01(c.a * fade);
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                        if (mat.HasProperty("_Color")) mat.color = c;
                        mr.material = mat;
                    }
                }

                ghost.SetActive(false);
                ghostRings.Add(ghost);
            }

            Debug.Log($"[LandingVisualizer] Built {ghostRings.Count} ghost rings. Total rings in tunnel = {ghostRings.Count + 1}.");
        }

        // ───────────────────────────────────────────────────
        //  GHOST RING POSITIONING (ARC)
        // ───────────────────────────────────────────────────
        private void PositionGhostRings()
        {
            if (ghostRings.Count == 0) return;

            // Direction from TARGET (ring 4) back toward the DOG (launch position)
            Vector3 towardDog = lockedLaunchPos - lockedTargetPos;
            float totalDist = towardDog.magnitude;
            if (totalDist < 0.01f) totalDist = 0.01f;
            Vector3 dirToDog = towardDog.normalized;

            // Place ghost rings at 1x, 2x, 3x spacing along the arc toward the dog
            for (int i = 0; i < ghostRings.Count; i++)
            {
                if (ghostRings[i] == null) continue;

                // How far along the path from target toward dog this ring sits
                float dist = ringSpacing * (i + 1);

                // Linear XZ base position
                Vector3 basePos = lockedTargetPos + dirToDog * dist;

                // Parabolic height: arc peaks halfway between target and dog
                // t=0 at target, t=1 at dog. Peak is at t=0.5 → 4*H*t*(1-t)
                float t = Mathf.Clamp01(dist / totalDist);
                float heightOffset = 4f * arcPeakHeight * t * (1f - t);

                ghostRings[i].transform.position = basePos + Vector3.up * heightOffset;
                ghostRings[i].transform.rotation = ringRenderer.transform.rotation;
                ghostRings[i].transform.localScale = ringRenderer.transform.lossyScale;
                ghostRings[i].SetActive(true);
            }
        }

        // ───────────────────────────────────────────────────
        //  HIDE EVERYTHING
        // ───────────────────────────────────────────────────
        private void HideAll()
        {
            IsShowingRing = false;

            // Use .enabled instead of .SetActive(false) so we don't accidentally turn off the script's own Update loop!
            if (ringRenderer != null && ringRenderer.enabled)
                ringRenderer.enabled = false;

            foreach (var g in ghostRings)
            {
                if (g != null && g.activeSelf)
                    g.SetActive(false);
            }

            // CRITICAL: Reset the sentinel so the NEXT AI hit will properly capture the dog's new launch position!
            // Without this, if the dog serves to the exact same spot twice, it doesn't update the arc origin!
            lastProcessedTarget = Vector3.one * float.MaxValue;
        }
    }
}
