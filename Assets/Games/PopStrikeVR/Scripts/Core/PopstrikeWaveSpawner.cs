using System.Collections.Generic;
using UnityEngine;
using PopstrikeVR.Data;
using PopstrikeVR.Gameplay;

namespace PopstrikeVR.Core
{
    /// <summary>
    /// Handles spawning logic for balloons based on parsed TaskRows.
    /// Extracts complex instantiation logic out of the LevelDirector.
    /// </summary>
    public static class PopstrikeWaveSpawner
    {
        private static float shapeExpansionMultiplier = 2.2f; // Scales authored shapes out to fit large balloons without overlapping

        public static List<Vector3> ExpandAuthoredCoordinates(List<Vector3> coords, float expansionMultiplier)
        {
            if (coords == null || coords.Count <= 1) return new List<Vector3>(coords);

            Vector3 center = Vector3.zero;
            foreach (var c in coords) center += c;
            center /= coords.Count;

            List<Vector3> expanded = new List<Vector3>();
            foreach (var c in coords)
            {
                float newAz = center.x + (c.x - center.x) * expansionMultiplier;
                float newEl = center.y + (c.y - center.y) * expansionMultiplier;
                expanded.Add(new Vector3(newAz, newEl, c.z));
            }
            return expanded;
        }
        
        public static List<GameObject> SpawnTask(TaskRow task, PatientProfileSO patientProfile)
        {
            List<GameObject> spawned = new List<GameObject>();
            List<Vector3> mappedPositions = new List<Vector3>();

            switch (task.TaskType)
            {
                case BalloonTaskType.Orange_Punch:
                    foreach(var spherical in task.SphericalCoordinates)
                    {
                        // True for relaxation: Push these apart if they overlap!
                        Vector3 pos = WorkspaceMapper.Instance.GetWorldPositionSafely(spherical, patientProfile, mappedPositions, true);
                        mappedPositions.Add(pos);
                        GameObject obj = PopstrikePooler.SpawnBalloon("BlazeBalloon", pos, Quaternion.identity);
                        if (obj != null)
                        {
                            if (obj.TryGetComponent<PopstrikeVR.Gameplay.BlazeBalloon>(out var blaze))
                            {
                                blaze.Setup(pos);
                                blaze.Initialize(patientProfile);
                            }
                            spawned.Add(obj);
                        }
                    }
                    break;

                case BalloonTaskType.Blue_Slash:
                    {
                        var expandedCoords = ExpandAuthoredCoordinates(task.SphericalCoordinates, shapeExpansionMultiplier);
                        foreach(var spherical in expandedCoords)
                        {
                            // True for relaxation: Prevent overlap using the new MinSafeDistance
                            Vector3 pos = WorkspaceMapper.Instance.GetWorldPositionSafely(spherical, patientProfile, mappedPositions, true);
                            mappedPositions.Add(pos);
                            GameObject obj = PopstrikePooler.SpawnBalloon("BladeBalloon", pos, Quaternion.identity);
                            if (obj != null) 
                            {
                                if (obj.TryGetComponent<PopstrikeVR.Gameplay.BaseBalloon>(out var baseB)) baseB.Setup(pos);
                                spawned.Add(obj);
                            }
                        }
                    }
                    
                    if (PopstrikeVR.Gameplay.BladeSlashManager.Instance == null)
                    {
                        var go = new GameObject("BladeSlashManager");
                        go.AddComponent<PopstrikeVR.Gameplay.BladeSlashManager>();
                    }
                    PopstrikeVR.Gameplay.BladeSlashManager.Instance.RegisterSequence(spawned);
                    break;

                case BalloonTaskType.Green_Trace:
                    {
                        var expandedCoords = ExpandAuthoredCoordinates(task.SphericalCoordinates, shapeExpansionMultiplier);
                        foreach(var spherical in expandedCoords)
                        {
                            // Pull the Green Trace task 15cm closer to the patient for easier depth perception!
                            float traceDepthOffset = 0.15f; 
                            
                            // True for relaxation: Prevent overlap using the new MinSafeDistance
                            Vector3 pos = WorkspaceMapper.Instance.GetWorldPositionSafely(spherical, patientProfile, mappedPositions, true, traceDepthOffset);
                            mappedPositions.Add(pos);
                            GameObject obj = PopstrikePooler.SpawnBalloon("TraceBalloon", pos, Quaternion.identity);
                            if (obj != null) 
                            {
                                if (obj.TryGetComponent<PopstrikeVR.Gameplay.BaseBalloon>(out var baseB)) baseB.Setup(pos);
                                spawned.Add(obj);
                            }
                        }
                    }
                    
                    if (PopstrikeVR.Gameplay.TracePathManager.Instance == null)
                    {
                        var go = new GameObject("TracePathManager");
                        go.AddComponent<PopstrikeVR.Gameplay.TracePathManager>();
                    }
                    PopstrikeVR.Gameplay.TracePathManager.Instance.RegisterSequence(spawned);
                    break;

                case BalloonTaskType.TMTA:
                case BalloonTaskType.TMTB:
                    {
                        // Spawn Transparent balloons for the Trail Making Test
                        List<GameObject> tmtSequence = new List<GameObject>();
                        var expandedCoords = ExpandAuthoredCoordinates(task.SphericalCoordinates, shapeExpansionMultiplier);
                        foreach(var spherical in expandedCoords)
                        {
                            // True for relaxation: Prevent overlap using the new MinSafeDistance
                            Vector3 pos = WorkspaceMapper.Instance.GetWorldPositionSafely(spherical, patientProfile, mappedPositions, true);
                            mappedPositions.Add(pos);
                            GameObject obj = PopstrikePooler.SpawnBalloon("TrailBalloon", pos, Quaternion.identity);
                            if (obj != null) 
                            {
                                Debug.Log($"<color=cyan>[PopstrikeWaveSpawner] SUCCESSFULLY SPAWNED TrailBalloon at Pos: {pos}, Scale: {obj.transform.localScale}, Active: {obj.activeInHierarchy}</color>");
                                tmtSequence.Add(obj);
                                spawned.Add(obj);
                            }
                            else
                            {
                                Debug.LogError($"<color=red>[PopstrikeWaveSpawner] FAILED TO SPAWN TrailBalloon at Pos: {pos}. SpawnBalloon returned null!</color>");
                            }
                        }
                    // Assign Labels based on Task Type
                    int number = 1;
                    char letter = 'A';
                    for(int i = 0; i < tmtSequence.Count; i++)
                    {
                        var obj = tmtSequence[i];
                        if(obj.TryGetComponent<PopstrikeVR.Gameplay.TrailBalloon>(out var trail))
                        {
                            if (task.TaskType == BalloonTaskType.TMTA)
                            {
                                trail.SetupTMT(obj.transform.position, number.ToString());
                                number++;
                            }
                            else if (task.TaskType == BalloonTaskType.TMTB)
                            {
                                if (i % 2 == 0)
                                {
                                    trail.SetupTMT(obj.transform.position, number.ToString());
                                    number++;
                                }
                                else
                                {
                                    trail.SetupTMT(obj.transform.position, letter.ToString());
                                    letter++;
                                }
                            }
                        }
                    }

                    if (TMTSolverScript.Instance != null && tmtSequence.Count > 0)
                        TMTSolverScript.Instance.RegisterSequence(tmtSequence, task.TaskType == BalloonTaskType.TMTB);
                    }
                    break;
            }
            return spawned;
        }
    }
}
