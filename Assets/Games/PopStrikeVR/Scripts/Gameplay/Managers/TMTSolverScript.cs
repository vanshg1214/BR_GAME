using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PopstrikeVR.Core;
using PopstrikeVR.Gameplay;

public class TMTSolverScript : MonoBehaviour
{
    public static TMTSolverScript Instance { get; private set; }

    private List<GameObject> activeSequence = new List<GameObject>();
    private int currentTargetIndex = 0;
    private int currentSequenceId = 0;
    
    private LineRenderer lineRenderer;

    public bool IsSequenceActive { get; private set; } = false;
    private bool isTMTB = false;
    private int chancesLeft = 3;
    private Coroutine timeoutRoutine = null;

    [Header("Tube Visuals")]
    public float tubeWidth = 0.04f;
    public Color trailTubeColor = new Color(0.5f, 0.8f, 1.0f, 1f);
    [Tooltip("Controls how bright/glowy the connecting tube is for Trail Balloons.")]
    [Range(0f, 5f)] public float trailTubeGlowIntensity = 1.5f;
    [Tooltip("Optional custom tube material. If empty, a transparent glowing tube is auto-generated.")]
    public Material trailTubeMaterial;
    [Tooltip("Optional secondary material (like an outer rim glow) layered on top of the first material.")]
    public Material secondaryTubeMaterial;

    [Header("Tutorial System")]
    [Tooltip("The Tutorial Animator prefab containing the hand pointing icon.")]
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

        // 1. LineRenderer (Transparent Glass Connection Tube) Setup
        if (lineRenderer == null)
        {
            GameObject tubeObj = new GameObject("TMT_TubeRenderer");
            tubeObj.transform.SetParent(transform);
            tubeObj.transform.localPosition = Vector3.zero;
            lineRenderer = tubeObj.AddComponent<LineRenderer>();
        }
        
        lineRenderer.startWidth = tubeWidth;
        lineRenderer.endWidth = tubeWidth;
        lineRenderer.positionCount = 0;
        
        // Smooth edges just like TracePathManager
        lineRenderer.numCornerVertices = 8;
        lineRenderer.numCapVertices = 8;

        // Build 1D gradient for the hollow 3D tube illusion (transparent center, glowing edges)
        Texture2D tubeTex = new Texture2D(1, 64, TextureFormat.ARGB32, false);
        tubeTex.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < 64; i++)
        {
            float tv = i / 63f;
            float dist = Mathf.Abs(tv - 0.5f) * 2f; 
            float alpha = Mathf.Pow(dist, 2.5f); // Edge glow curve for 3D illusion
            tubeTex.SetPixel(0, i, new Color(1f, 1f, 1f, alpha));
        }
        tubeTex.Apply();

        if (trailTubeMaterial != null)
        {
            Material instancedPrimary = new Material(trailTubeMaterial);
            instancedPrimary.mainTexture = tubeTex;
            if (instancedPrimary.HasProperty("_MainTex")) instancedPrimary.SetTexture("_MainTex", tubeTex);
            if (instancedPrimary.HasProperty("_BaseMap")) instancedPrimary.SetTexture("_BaseMap", tubeTex);

            if (secondaryTubeMaterial != null)
            {
                Material instancedSecondary = new Material(secondaryTubeMaterial);
                instancedSecondary.mainTexture = tubeTex;
                if (instancedSecondary.HasProperty("_MainTex")) instancedSecondary.SetTexture("_MainTex", tubeTex);
                if (instancedSecondary.HasProperty("_BaseMap")) instancedSecondary.SetTexture("_BaseMap", tubeTex);
                
                lineRenderer.materials = new Material[] { instancedPrimary, instancedSecondary };
            }
            else
            {
                lineRenderer.material = instancedPrimary;
            }
        }
        else if (lineRenderer.sharedMaterial == null)
        {
            // Build the perfect round tube illusion
            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); 
            lineMat.SetInt("_ZWrite", 0);
            lineMat.renderQueue = 3000;

            Color finalColor = trailTubeColor * trailTubeGlowIntensity;
            finalColor.a = trailTubeColor.a;
            lineMat.SetColor("_Color", finalColor);
            lineMat.mainTexture = tubeTex;

            lineRenderer.material = lineMat;
        }
    }

    private void Update()
    {
        if (activeSequence.Count > 0)
        {
            // Check if any balloon has been despawned by LevelDirector
            bool anyDespawned = false;
            foreach (var b in activeSequence)
            {
                if (b == null || !b.gameObject.activeInHierarchy)
                {
                    anyDespawned = true; break;
                }
            }
            if (anyDespawned)
            {
                ClearSequence();
            }
        }
    }

    public void RegisterSequence(List<GameObject> sequenceBalloons, bool isTMTB_Mode)
    {
        currentSequenceId++;
        activeSequence.Clear();
        activeSequence.AddRange(sequenceBalloons);
        currentTargetIndex = 0;
        chancesLeft = PopstrikeLevelDirector.Instance != null ? PopstrikeLevelDirector.Instance.maxAttemptsAllowed : 3;
        isTMTB = isTMTB_Mode;
        IsSequenceActive = true;
        
        if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
        
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
            
        Debug.Log($"[TMTSolver] Registered new sequence with {activeSequence.Count} targets. Chances: {chancesLeft}");
        
        // Automatically start the tutorial!
        PlayTutorialSequence();
    }

    public void PlayTutorialSequence()
    {
        if (!HasCompletedTutorial && tutorialPrefab != null && activeSequence != null && activeSequence.Count > 0)
        {
            if (spawnedTutorial == null)
            {
                spawnedTutorial = Instantiate(tutorialPrefab, activeSequence[0].transform.position, Quaternion.identity);
            }
            
            Transform[] points = new Transform[activeSequence.Count];
            for(int i = 0; i < activeSequence.Count; i++)
            {
                points[i] = activeSequence[i].transform;
            }
            spawnedTutorial.PlayTMTTutorial(points);
        }
    }

    public bool ValidateHit(GameObject struckBalloon)
    {
        if (!IsSequenceActive || activeSequence.Count == 0 || currentTargetIndex >= activeSequence.Count)
            return false;

        if (struckBalloon == activeSequence[currentTargetIndex])
        {
            currentTargetIndex++;
            Debug.Log($"[TMTSolver] Correct! Progress: {currentTargetIndex}/{activeSequence.Count}");

            // Draw line to this balloon
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = currentTargetIndex;
                lineRenderer.SetPosition(currentTargetIndex - 1, struckBalloon.transform.position);
                UpdatePathLengths();
            }

            if (currentTargetIndex >= activeSequence.Count)
            {
                Debug.Log("[TMTSolver] Sequence Complete!");
                if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
                
                if (PopstrikeVR.Core.PopstrikeFeedbackManager.Instance != null)
                {
                    if (isTMTB)
                        PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.PlayTMTBScaleNote(currentTargetIndex - 1, true);
                    else
                        PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.PlayTMTAScaleNote(currentTargetIndex - 1, true);
                }
                
                StartCoroutine(CompleteSequenceRoutine(currentSequenceId));
            }
            else
            {
                // Play intermediate scale note
                if (PopstrikeVR.Core.PopstrikeFeedbackManager.Instance != null)
                {
                    if (isTMTB)
                        PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.PlayTMTBScaleNote(currentTargetIndex - 1, false);
                    else
                        PopstrikeVR.Core.PopstrikeFeedbackManager.Instance.PlayTMTAScaleNote(currentTargetIndex - 1, false);
                }

                // Patient successfully hit one, start the 3-second timer to reach the next one
                if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
                timeoutRoutine = StartCoroutine(ConnectionTimeoutRoutine());
            }
            
            return true;
        }
        else
        {
            // Try to report the error. If cooldown is active, it returns false, so we ignore the mistake!
            bool canReport = false;
            if (PopstrikeVR.Core.PopstrikeLevelDirector.Instance != null)
            {
                canReport = PopstrikeVR.Core.PopstrikeLevelDirector.Instance.TryReportError();
            }
            
            if (canReport)
            {
                Debug.LogWarning("[TMTSolver] Wrong balloon hit! Error recorded.");
            }
            
            // Still return false so the balloon knows it was the wrong one, 
            // but the balloon script itself will check TryReportError() before playing its error sound.
            return false;
        }
    }

    private void UpdatePathLengths()
    {
        // Intentionally left blank. Originally used for calculating flowing particle path lengths.
    }

    private IEnumerator ConnectionTimeoutRoutine()
    {
        float timeoutDelay = activeSequence.Count < 5 ? 3.0f : 4.0f;
        yield return new WaitForSeconds(timeoutDelay);

        Debug.LogWarning($"[TMTSolver] LINK BROKEN: Patient took longer than {timeoutDelay} seconds.");
        chancesLeft--;

        currentTargetIndex = 0;
        if (lineRenderer != null) lineRenderer.positionCount = 0;
        UpdatePathLengths(); // will turn off emission

        foreach(var obj in activeSequence)
        {
            if (obj != null && obj.TryGetComponent<TrailBalloon>(out var trail))
            {
                trail.ResetVisualState(); 
            }
        }

        if (chancesLeft <= 0)
        {
            Debug.LogError("[TMTSolver] FAILED! Patient ran out of chances.");
            foreach(var obj in activeSequence)
            {
                if (obj != null && obj.TryGetComponent<TrailBalloon>(out var trail))
                {
                    trail.DeflateForcefully();
                }
            }
            ClearSequence();
        }
        else
        {
            Debug.Log($"[TMTSolver] Try again! Chances remaining: {chancesLeft}");
        }
    }

    private IEnumerator CompleteSequenceRoutine(int expectedSequenceId)
    {
        HasCompletedTutorial = true;
        PopstrikeVR.Gameplay.ComboManager.Instance?.RegisterHit(100);

        // Start Micro-Staggered Pop Routine to prevent VR CPU spikes
        StartCoroutine(MicroStaggerPopRoutine(new List<GameObject>(activeSequence)));

        yield return new WaitForSeconds(0.5f); 
        
        // Prevent race condition: only clear if a new sequence hasn't already started!
        if (currentSequenceId == expectedSequenceId)
        {
            ClearSequence();
        }
    }

    private IEnumerator MicroStaggerPopRoutine(List<GameObject> poppingSequence)
    {
        foreach (var obj in poppingSequence)
        {
            if (obj != null && obj.TryGetComponent<TrailBalloon>(out var trail))
            {
                trail.TriggerFinalPop(silent: true);
                // MICRO-STAGGER: Wait exactly 1 frame (~11ms in VR) between each pop.
                // This is completely imperceptible to the human eye/ear (they look like they burst at exactly the same time),
                // but it spreads the heavy VFX Instantiations across multiple frames, completely eliminating the frame drop!
                yield return null; 
            }
        }
    }

    public bool HasSequenceStarted()
    {
        return IsSequenceActive && currentTargetIndex > 0;
    }

    public void ClearSequence()
    {
        activeSequence.Clear();
        currentTargetIndex = 0;
        IsSequenceActive = false; 
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
            
        UpdatePathLengths(); // turn off particles
            
        if (spawnedTutorial != null)
        {
            spawnedTutorial.StopTutorial();
            Destroy(spawnedTutorial.gameObject);
            spawnedTutorial = null;
        }

        if (timeoutRoutine != null) StopCoroutine(timeoutRoutine);
    }
}
