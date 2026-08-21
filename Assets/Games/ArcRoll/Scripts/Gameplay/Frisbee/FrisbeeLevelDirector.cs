using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using ArcRoll.Core;
using ArcRoll.Grid;

namespace ArcRoll.Gameplay.Frisbee
{
    public class FrisbeeLevelDirector : MonoBehaviour
    {
        [System.Serializable]
        public struct FrisbeeEvent
        {
            public float delay;
            public int row;
            public int col;
            public string targetType; // e.g. "CanPyramid", "Balloon"
        }

        [Header("Setup")]
        [SerializeField] private string levelFileName = "frisbee.csv";
        [SerializeField] private Transform playerHead;
        [SerializeField] private GridManager gridManager;
        
        [Header("Prefabs")]
        [SerializeField] private Frisbee frisbeePrefab;
        [SerializeField] private GameObject canPyramidPrefab;
        [SerializeField] private GameObject balloonPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 frisbeeSpawnOffset = new Vector3(0.3f, -0.2f, 0.4f);

        private readonly List<FrisbeeEvent> levelEvents = new List<FrisbeeEvent>();
        private int currentEventIndex = 0;
        private bool levelStarted = false;
        private bool levelFinished = false;
        private Vector3 calibratedForward;

        private void Start()
        {
            StartCoroutine(LoadLevelAndPlay());
        }

        private IEnumerator LoadLevelAndPlay()
        {
            if (ArcRollGameManager.Instance != null)
                ArcRollGameManager.Instance.StartGame();

            calibratedForward = Vector3.ProjectOnPlane(playerHead.forward, Vector3.up).normalized;
            if (calibratedForward.sqrMagnitude < 0.001f) calibratedForward = Vector3.forward;

            yield return StartCoroutine(ParseCSVFromStreamingAssets(levelFileName));

            if (levelEvents.Count > 0)
            {
                yield return new WaitForSeconds(3.0f);
                levelStarted = true;
                RequestNextShot();
            }
        }

        public void RequestNextShot()
        {
            if (!levelStarted || levelFinished || (ArcRollGameManager.Instance != null && !ArcRollGameManager.Instance.isGameActive)) return;
            
            if (currentEventIndex >= levelEvents.Count)
            {
                Debug.Log("[FrisbeeLevelDirector] Level Complete!");
                levelFinished = true;
                return;
            }

            StartCoroutine(WaitAndSpawn(levelEvents[currentEventIndex]));
            currentEventIndex++;
        }

        private IEnumerator WaitAndSpawn(FrisbeeEvent evt)
        {
            yield return new WaitForSeconds(evt.delay);
            SpawnTargetAndFrisbee(evt);
        }

        private void SpawnTargetAndFrisbee(FrisbeeEvent evt)
        {
            if (playerHead == null || gridManager == null) return;

            // Get target position from GridManager
            Vector3 targetPos = gridManager.GetWorldPosition(evt.row, evt.col);
            
            // If row is 0, align exactly with floorY (cans go on the floor)
            if (evt.row == 0)
            {
                GameObject floorObj = GameObject.FindGameObjectWithTag("Floor");
                float floorY = floorObj != null ? floorObj.transform.position.y : 0f;
                targetPos.y = floorY;
            }

            GameObject prefabToSpawn = canPyramidPrefab;
            if (evt.targetType.ToLower() == "balloon") prefabToSpawn = balloonPrefab;
            
            if (prefabToSpawn != null)
            {
                // ONLY rotate horizontally to face the player. Don't tilt up/down!
                Vector3 lookDir = playerHead.position - targetPos;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f) lookDir.Normalize();
                else lookDir = Vector3.forward;

                // PRESERVE the prefab's built-in local rotation (just like hoops & pins)
                Quaternion finalTargetRot = Quaternion.LookRotation(lookDir) * prefabToSpawn.transform.rotation;
                GameObject spawnedTarget = Instantiate(prefabToSpawn, targetPos, finalTargetRot);
                
                // Compensate for any model visual/collider pivot offsets (e.g. Balloon 3D model Z-offset)
                if (evt.targetType.ToLower() == "balloon")
                {
                    Collider col = spawnedTarget.GetComponentInChildren<Collider>();
                    if (col != null)
                    {
                        Vector3 localCenter = spawnedTarget.transform.InverseTransformPoint(col.bounds.center);
                        spawnedTarget.transform.position = targetPos - finalTargetRot * localCenter;
                    }
                }

                FrisbeeFormation formation = spawnedTarget.GetComponent<FrisbeeFormation>();
                if (formation == null)
                {
                    formation = spawnedTarget.AddComponent<FrisbeeFormation>();
                }
                
                // The spot where the frisbee will hover for the player to grab
                Vector3 hoverAnchorPos = playerHead.position + playerHead.TransformDirection(frisbeeSpawnOffset);
                
                // Spawn it off to the right and slightly forward so it gracefully glides in!
                Vector3 spawnPos = hoverAnchorPos + playerHead.right * 4.0f + playerHead.forward * 2.0f;
                Frisbee frisbee = Instantiate(frisbeePrefab, spawnPos, frisbeePrefab.transform.rotation);
                
                if (formation != null)
                {
                    formation.RegisterFrisbee(frisbee);
                }

                // Setup the frisbee so it knows where to aim (aim assist)
                Vector3 aimPos = targetPos;
                if (evt.row == 0) aimPos.y += 0.5f; // Aim slightly above the ground for Can Pyramids
                frisbee.ShootToTarget(hoverAnchorPos, aimPos, spawnedTarget.transform);
                
                // Tell the frisbee to trigger RequestNextShot when it dies!
                frisbee.OnStateChanged += (f, state) =>
                {
                    if (state == Frisbee.FrisbeeState.Dead || state == Frisbee.FrisbeeState.Missed)
                    {
                        RequestNextShot();
                    }
                };
            }
        }

        private IEnumerator ParseCSVFromStreamingAssets(string fileName)
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "ArcRoll", fileName);
            string csvText = "";

            if (filePath.Contains("://") || filePath.Contains(":///"))
            {
                using (UnityWebRequest www = UnityWebRequest.Get(filePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                        csvText = www.downloadHandler.text;
                    else
                        yield break;
                }
            }
            else
            {
                if (File.Exists(filePath))
                    csvText = File.ReadAllText(filePath);
                else
                    yield break;
            }

            string[] lines = csvText.Split('\n');
            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] cols = line.Split(',');
                if (cols.Length >= 4)
                {
                    float.TryParse(cols[0], out float delay);
                    int.TryParse(cols[1], out int row);
                    int.TryParse(cols[2], out int col);
                    string targetType = cols[3].Trim();

                    levelEvents.Add(new FrisbeeEvent
                    {
                        delay = delay,
                        row = row,
                        col = col,
                        targetType = targetType
                    });
                }
            }
        }
    }
}
