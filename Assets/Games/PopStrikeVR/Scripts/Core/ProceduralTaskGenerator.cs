using System.Collections.Generic;
using UnityEngine;

namespace PopstrikeVR.Core
{
    public static class ProceduralTaskGenerator
    {
        public static TaskRow GenerateNextTask(BalloonSpawnChances chances, float safeRadius)
        {
            TaskRow row = new TaskRow();
            row.TaskType = SelectRandomTaskType(chances);

            int coordinateCount = GetRequiredCoordinateCount(row.TaskType);
            
            // Randomly generate Azimuth and Elevation within comfortable bounds (-40 to +40 az, -30 to +30 el)
            for (int i = 0; i < coordinateCount; i++)
            {
                float az = Random.Range(-40f, 40f);
                float el = Random.Range(-30f, 30f);
                row.SphericalCoordinates.Add(new Vector3(az, el, 0f));
                row.ComputedWorldPositions.Add(JSONLevelParser.SphericalToCartesian(az, el, safeRadius));
            }

            // Enforce minimum distance (Reduced to 0.08f/8cm to allow dense clusters without extreme overlap)
            float safeDist = PopstrikeVR.Gameplay.WorkspaceMapper.Instance != null ? PopstrikeVR.Gameplay.WorkspaceMapper.Instance.MinSafeDistance : 0.08f;
            EnforceNoOverlap(row.ComputedWorldPositions, safeDist);

            return row;
        }

        private static BalloonTaskType SelectRandomTaskType(BalloonSpawnChances chances)
        {
            float tmtAChance = chances.tmtA;
            float tmtBChance = chances.tmtB;

            // In Easy Mode, TMT balloons are too complex and should not spawn at all.
            string difficulty = TemporarySessionData.Difficulty ?? "Medium";
            if (difficulty == "Easy")
            {
                tmtAChance = 0f;
                tmtBChance = 0f;
            }

            float totalWeight = chances.orangePunch + chances.blueSlash + chances.greenTrace + tmtAChance + tmtBChance;
            if (totalWeight <= 0) return BalloonTaskType.Orange_Punch; // Fallback

            float randomRoll = Random.Range(0f, totalWeight);

            if (randomRoll < chances.orangePunch) return BalloonTaskType.Orange_Punch;
            randomRoll -= chances.orangePunch;

            if (randomRoll < chances.blueSlash) return BalloonTaskType.Blue_Slash;
            randomRoll -= chances.blueSlash;

            if (randomRoll < chances.greenTrace) return BalloonTaskType.Green_Trace;
            randomRoll -= chances.greenTrace;

            if (randomRoll < tmtAChance) return BalloonTaskType.TMTA;
            
            return BalloonTaskType.TMTB;
        }

        private static int GetRequiredCoordinateCount(BalloonTaskType taskType)
        {
            string difficulty = TemporarySessionData.Difficulty ?? "Medium";
            int count = 1;

            switch (taskType)
            {
                case BalloonTaskType.Orange_Punch:
                    if (difficulty == "Hard") count = Random.Range(1, 5); // 1 to 4
                    else if (difficulty == "Medium") count = Random.Range(1, 3); // 1 to 2
                    else count = 1; // Easy
                    break;
                
                case BalloonTaskType.Blue_Slash:
                    if (difficulty == "Hard") count = Random.Range(3, 7); // 3 to 6
                    else if (difficulty == "Medium") count = Random.Range(2, 5); // 2 to 4
                    else count = Random.Range(2, 4); // 2 to 3
                    break;

                case BalloonTaskType.Green_Trace:
                    if (difficulty == "Hard") count = Random.Range(4, 9); // 4 to 8
                    else if (difficulty == "Medium") count = Random.Range(3, 6); // 3 to 5
                    else count = Random.Range(2, 4); // 2 to 3
                    break;

                case BalloonTaskType.TMTA:
                    if (difficulty == "Hard") count = Random.Range(5, 9); // 5 to 8
                    else if (difficulty == "Medium") count = Random.Range(3, 6); // 3 to 5
                    else count = Random.Range(3, 4); // 3
                    break;

                case BalloonTaskType.TMTB:
                    // TMTB must ALWAYS be an even number (1->A->2->B)
                    if (difficulty == "Hard") count = Random.value > 0.5f ? 6 : 8; 
                    else if (difficulty == "Medium") count = Random.value > 0.5f ? 4 : 6;
                    else count = 4;
                    break;
            }
            
            return count;
        }

        private static void EnforceNoOverlap(List<Vector3> positions, float minimumDistance)
        {
            // Iterative relaxation to push apart any overlapping balloons
            int maxIterations = 50;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool moved = false;
                for (int i = 0; i < positions.Count; i++)
                {
                    for (int j = i + 1; j < positions.Count; j++)
                    {
                        float dist = Vector3.Distance(positions[i], positions[j]);
                        if (dist < minimumDistance)
                        {
                            Vector3 dir = (positions[i] - positions[j]).normalized;
                            if (dir == Vector3.zero) dir = Vector3.right;
                            
                            float overlap = minimumDistance - dist;
                            Vector3 correction = dir * (overlap * 0.51f);
                            correction.z = 0f;

                            positions[i] += correction;
                            positions[j] -= correction;
                            moved = true;
                        }
                    }
                }
                if (!moved) break;
            }
        }

        public static int GetBaseScoreForTask(BalloonTaskType taskType)
        {
            switch (taskType)
            {
                case BalloonTaskType.Orange_Punch: return 50;
                case BalloonTaskType.Blue_Slash: return 80;
                case BalloonTaskType.Green_Trace: return 100;
                case BalloonTaskType.TMTA: return 100;
                case BalloonTaskType.TMTB: return 100;
                default: return 50;
            }
        }
    }
}
