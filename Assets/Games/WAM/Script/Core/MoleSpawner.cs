using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WhackAMole
{
    public class MoleSpawner : MonoBehaviour, IGameStateListener
    {
        public static MoleSpawner Instance { get; private set; }

        [SerializeField] private HoleLayoutGenerator layoutGenerator;
        public HoleLayoutGenerator LayoutGenerator => layoutGenerator;

        private bool isSpawning;
        private Coroutine spawnCoroutine;
        private readonly HashSet<int> occupiedHoles = new HashSet<int>();
        private bool isFirstSpawnOfRound = true;

        public int OccupiedHolesCount => occupiedHoles.Count;

        public void FreeHole(int holeIndex) => occupiedHoles.Remove(holeIndex);

        public void OnGameStateChanged(GameState newState)
        {
            if (newState == GameState.Playing)
            {
                StartSpawning();
            }
            else
            {
                StopSpawning();
                if (newState == GameState.Ready || newState == GameState.Finished)
                    occupiedHoles.Clear();
            }
        }

        private void Awake() => Instance = this;

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterListener(this);
                if (GameManager.Instance.CurrentState == GameState.Playing)
                    OnGameStateChanged(GameState.Playing);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.UnregisterListener(this);
        }

        private void StartSpawning()
        {
            if (layoutGenerator == null) return;
            StopSpawning();
            spawnCoroutine = StartCoroutine(InitializeAndSpawnLoop());
        }

        private void StopSpawning()
        {
            isSpawning = false;
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }
        }

        private IEnumerator InitializeAndSpawnLoop()
        {
            isSpawning = true;
            isFirstSpawnOfRound = true;

            layoutGenerator.GenerateLayoutIfNeeded();

            yield return StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            isSpawning = true;
            DifficultyProfileSO difficulty = GameManager.Instance?.DifficultyProfile;
            if (difficulty == null) yield break;

            while (isSpawning && IsPlaying())
            {
                while (WhackAMoleLevelDirector.Instance != null && WhackAMoleLevelDirector.Instance.IsBreakPhase)
                {
                    yield return null;
                }
                
                if (!isSpawning || !IsPlaying()) break;
                
                float interval = Random.Range(difficulty.minSpawnInterval, difficulty.maxSpawnInterval);
                if (FatigueManager.Instance != null && FatigueManager.Instance.IsFatigued)
                    interval += FatigueManager.Instance.FatigueDelayModifier;

                yield return new WaitForSeconds(interval);

                if (!isSpawning || !IsPlaying()) break;
                if (WhackAMoleLevelDirector.Instance != null && WhackAMoleLevelDirector.Instance.IsBreakPhase) continue;
                if (layoutGenerator.SpawnPoints.Count == 0) continue;
                if (occupiedHoles.Count + BirdFlightController.ActiveBirdCount >= difficulty.maxActiveMoles) continue;

                List<int> available = new List<int>();
                for (int i = 0; i < layoutGenerator.SpawnPoints.Count; i++)
                {
                    if (!occupiedHoles.Contains(i)) available.Add(i);
                }

                if (available.Count == 0) continue;

                string moleTag;
                int chosenIndex;

                if (isFirstSpawnOfRound)
                {
                    isFirstSpawnOfRound = false;
                    moleTag = "Standard";
                    chosenIndex = GetFurthestAvailableHole(available);
                }
                else
                {
                    ProceduralSpawnData data = WhackAMoleLevelDirector.Instance != null ? 
                        WhackAMoleLevelDirector.Instance.GetProceduralTarget() : new ProceduralSpawnData { characterTag = "Standard", azimuth = 0, distance = 0.4f };
                    
                    moleTag = data.characterTag;
                    chosenIndex = GetNearestAvailableHole(data.azimuth, data.distance, available);
                }

                if (chosenIndex == -1)
                {
                    if (moleTag == "Dog")
                    {
                        List<int> safeHoles = new List<int>();
                        foreach (int idx in available)
                        {
                            if (!IsDogSpawnTooCloseToFace(layoutGenerator.SpawnPoints[idx])) safeHoles.Add(idx);
                        }
                        if (safeHoles.Count > 0) chosenIndex = GetFurthestAvailableHole(safeHoles);
                    }
                    else if (moleTag == "Treasure")
                    {
                        List<int> farHoles = new List<int>();
                        float minDist = float.MaxValue, maxDist = float.MinValue;
                        Vector3 orig = Camera.main != null ? Camera.main.transform.position : layoutGenerator.transform.position;
                        orig.y = 0;

                        foreach (var sp in layoutGenerator.SpawnPoints)
                        {
                            if (sp == null) continue;
                            Vector3 pos = sp.position;
                            pos.y = 0;
                            float d = Vector3.Distance(pos, orig);
                            if (d < minDist) minDist = d;
                            if (d > maxDist) maxDist = d;
                        }

                        if (maxDist > minDist)
                        {
                            foreach (int idx in available)
                            {
                                Vector3 pos = layoutGenerator.SpawnPoints[idx].position;
                                pos.y = 0;
                                float norm = (Vector3.Distance(pos, orig) - minDist) / (maxDist - minDist);
                                if (norm >= 0.45f) farHoles.Add(idx);
                            }
                        }
                        if (farHoles.Count > 0) chosenIndex = GetFurthestAvailableHole(farHoles);
                    }

                    if (chosenIndex == -1) chosenIndex = GetFurthestAvailableHole(available);
                }

                Transform spawnPoint = layoutGenerator.SpawnPoints[chosenIndex];
                occupiedHoles.Add(chosenIndex);
                if (ObjectPooler.Instance == null) continue;

                Vector3 spawnPos = spawnPoint.position;
                Quaternion spawnRot = spawnPoint.rotation;
                Transform actualParentPoint = spawnPoint;

                if ((moleTag == "Dog" || moleTag == "Heavy" || moleTag == "Treasure") && layoutGenerator.ShrubSpawnPoints?.Count > 0)
                {
                    Transform shrubPt = layoutGenerator.ShrubSpawnPoints[Random.Range(0, layoutGenerator.ShrubSpawnPoints.Count)];
                    spawnPos = shrubPt.position;
                    spawnRot = shrubPt.rotation;
                    actualParentPoint = null; 
                }

                GameObject mole = ObjectPooler.Instance.SpawnFromPool(moleTag, spawnPos, spawnRot, actualParentPoint);
                if (mole != null)
                {
                    CollisionIsolator.IsolateObject(mole);
                    BaseMole moleScript = mole.GetComponent<BaseMole>();
                    if (moleScript != null) moleScript.AssignedHoleIndex = chosenIndex;

                    if (moleTag == "Standard" || moleTag == "Fake")
                        FeedbackManager.Instance?.PlayMoleSpawn(spawnPoint.position);
                }
                else
                {
                    occupiedHoles.Remove(chosenIndex);
                }
            }

            isSpawning = false;
            spawnCoroutine = null;
        }



        private static bool IsPlaying() => GameManager.Instance?.CurrentState == GameState.Playing;

        private int GetFurthestAvailableHole(List<int> candidates)
        {
            if (candidates == null || candidates.Count == 0 || layoutGenerator?.SpawnPoints.Count == 0) return -1;

            List<float> minToOcc = new List<float>(candidates.Count);
            float minOcc = float.MaxValue, maxOcc = float.MinValue;

            if (occupiedHoles.Count > 0)
            {
                foreach (int c in candidates)
                {
                    float minD = float.MaxValue;
                    Vector3 pos = layoutGenerator.SpawnPoints[c].position;
                    foreach (int occ in occupiedHoles)
                    {
                        float d = Vector3.Distance(pos, layoutGenerator.SpawnPoints[occ].position);
                        if (d < minD) minD = d;
                    }
                    minToOcc.Add(minD);
                    if (minD < minOcc) minOcc = minD;
                    if (minD > maxOcc) maxOcc = minD;
                }
            }

            List<float> weights = new List<float>(candidates.Count);
            float totalWt = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform pt = layoutGenerator.SpawnPoints[candidates[i]];
                bool isEdge = pt.GetComponent<MoleScaleHint>()?.IsEdgeColumn ?? false;
                float cWt = isEdge ? 0.02f : 1.0f;
                float sWt = 1.0f;
                
                if (occupiedHoles.Count > 0)
                {
                    float norm = (maxOcc > minOcc) ? (minToOcc[i] - minOcc) / (maxOcc - minOcc) : 1f;
                    sWt = Mathf.Lerp(0.001f, 1.0f, Mathf.Pow(norm, 4.0f));
                }
                
                float wt = cWt * sWt;
                weights.Add(wt);
                totalWt += wt;
            }

            if (totalWt <= 0f) return candidates[Random.Range(0, candidates.Count)];

            float roll = Random.Range(0f, totalWt);
            float cum = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (roll <= (cum += weights[i])) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private int GetNearestAvailableHole(float azimuth, float distance, List<int> candidates)
        {
            if (candidates == null || candidates.Count == 0 || layoutGenerator?.SpawnPoints.Count == 0) return -1;

            Transform center = layoutGenerator.transform;
            Vector3 target = center.position + Quaternion.Euler(0, azimuth, 0) * Vector3.forward * distance;
            
            int best = -1;
            float minSqr = float.MaxValue;

            foreach (int c in candidates)
            {
                float sqr = (layoutGenerator.SpawnPoints[c].position - target).sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    best = c;
                }
            }
            return best;
        }

        private bool IsDogSpawnTooCloseToFace(Transform pt)
        {
            if (Camera.main == null) return false;
            Vector3 toHole = pt.position - Camera.main.transform.position;
            toHole.y = 0;
            
            if (toHole.magnitude < 0.55f)
            {
                Vector3 fwd = WorkspaceAutoPositioner.Instance != null ? WorkspaceAutoPositioner.Instance.transform.forward : Vector3.forward;
                if (Vector3.Angle(fwd, toHole.normalized) < 30f) return true;
            }
            return false;
        }
    }
}
