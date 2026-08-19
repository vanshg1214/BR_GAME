using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PopstrikeVR.Data;

namespace PopstrikeVR.Core
{
    public enum BalloonTaskType
    {
        Orange_Punch,
        Blue_Slash,
        Green_Trace,
        TMTA,
        TMTB
    }

    /// <summary>
    /// Represents a single parsed task line from the CSV.
    /// </summary>
    [System.Serializable]
    public class TaskRow
    {
        public BalloonTaskType TaskType;
        public List<Vector3> SphericalCoordinates = new List<Vector3>();
        public List<Vector3> ComputedWorldPositions = new List<Vector3>();
    }

    /// <summary>
    /// Core Level Parser responsible for translating the clinical CSV layouts into actionable 3D world space tasks.
    /// </summary>
    public static class CSVLevelParser
    {
        /// <summary>
        /// Reads the CSV file, parses the spherical coordinates, validates against GDD rules, 
        /// and converts them to world positions based on the patient's safe working radius.
        /// </summary>
        public static List<TaskRow> ParseSessionCSV(string csvFilePath, PatientProfileSO profile)
        {
            if (!File.Exists(csvFilePath))
            {
                Debug.LogError($"[CSVLevelParser] CRITICAL ERROR: File not found at path: {csvFilePath}");
                return new List<TaskRow>();
            }

            string csvContent = File.ReadAllText(csvFilePath);
            return ParseSessionCSVText(csvContent, profile);
        }

        public static List<TaskRow> ParseSessionCSVText(string csvContent, PatientProfileSO profile)
        {
            List<TaskRow> sessionTasks = new List<TaskRow>();

            if (string.IsNullOrWhiteSpace(csvContent)) return sessionTasks;

            string[] lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            float safeRadius = profile.GetSafeRadius();
            int lineNumber = 0;

            foreach (string line in lines)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("TaskType", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    TaskRow row = ParseRow(line, safeRadius);
                    if (ValidateRow(row, lineNumber))
                    {
                        sessionTasks.Add(row);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CSVLevelParser] Error parsing line {lineNumber}: {e.Message}");
                }
            }

            return sessionTasks;
        }

        private static TaskRow ParseRow(string line, float safeRadius)
        {
            TaskRow row = new TaskRow();
            
            // Remove whitespace and quotes for clean processing
            string cleanLine = line.Replace(" ", "").Replace("\"", "");
            int firstCommaIndex = cleanLine.IndexOf(',');
            if (firstCommaIndex == -1) throw new FormatException("Invalid row format. Missing commas.");

            // Extract Type
            string typeStr = cleanLine.Substring(0, firstCommaIndex);
            row.TaskType = ParseTaskType(typeStr);

            // Extract Coordinate Triplets
            string coordsString = cleanLine.Substring(firstCommaIndex + 1);
            
            // Splitting logic: e.g. (-20,10,0.8);(20,10,0.8)
            string[] coordTokens = coordsString.Split(';');
            
            foreach (string token in coordTokens)
            {
                if (string.IsNullOrEmpty(token)) continue;

                string cleanToken = token.Replace("(", "").Replace(")", "");
                string[] values = cleanToken.Split(',');
                
                if (values.Length != 2) throw new FormatException($"Invalid coordinate tuple: {cleanToken}");

                float az = float.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture);
                float el = float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture);

                row.SphericalCoordinates.Add(new Vector3(az, el, 0f)); // Z is unused
                row.ComputedWorldPositions.Add(SphericalToCartesian(az, el, safeRadius));
            }

            // Enforce minimum distance (Reduced to 0.08f/8cm to allow dense clusters without extreme overlap)
            float safeDist = PopstrikeVR.Gameplay.WorkspaceMapper.Instance != null ? PopstrikeVR.Gameplay.WorkspaceMapper.Instance.MinSafeDistance : 0.08f;
            EnforceNoOverlap(row.ComputedWorldPositions, safeDist);

            return row;
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
                            if (dir == Vector3.zero) dir = Vector3.right; // Fallback if exactly same position
                            
                            float overlap = minimumDistance - dist;
                            Vector3 correction = dir * (overlap * 0.51f); // Push slightly more than half to be safe
                            
                            // Since user requested Z to remain constant, we only push on X and Y axis
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

        private static BalloonTaskType ParseTaskType(string typeIdentifier)
        {
            switch (typeIdentifier.ToUpper())
            {
                case "O": 
                case "ORANGE_PUNCH": return BalloonTaskType.Orange_Punch;
                case "B": 
                case "BLUE_SLASH": return BalloonTaskType.Blue_Slash;
                case "G": 
                case "GREEN_TRACE": return BalloonTaskType.Green_Trace;
                case "A":
                case "TMTA": return BalloonTaskType.TMTA;
                case "T":
                case "TMTB": return BalloonTaskType.TMTB;
                default: throw new FormatException($"Unknown Task Type Identifier: {typeIdentifier}");
            }
        }

        private static bool ValidateRow(TaskRow row, int lineNumber)
        {
            switch (row.TaskType)
            {
                case BalloonTaskType.Orange_Punch:
                    if (row.SphericalCoordinates.Count < 1)
                    {
                        Debug.LogError($"[CSVLevelParser] Line {lineNumber}: Orange (Punch) task requires at least 1 coordinate.");
                        return false;
                    }
                    break;
                    
                case BalloonTaskType.Blue_Slash:
                    if (row.SphericalCoordinates.Count < 3)
                    {
                        Debug.LogError($"[CSVLevelParser] Line {lineNumber}: Blue (Slash) task requires at least 3 coordinates. Found {row.SphericalCoordinates.Count}.");
                        return false;
                    }
                    // Additional check for collinearity could be implemented here as per GDD.
                    break;
                    
                case BalloonTaskType.Green_Trace:
                    if (row.SphericalCoordinates.Count < 2 || row.SphericalCoordinates.Count > 5)
                    {
                        Debug.LogError($"[CSVLevelParser] Line {lineNumber}: Green (Trace) task requires 2 to 5 coordinates. Found {row.SphericalCoordinates.Count}.");
                        return false;
                    }
                    break;
                    
                case BalloonTaskType.TMTA:
                    if (row.SphericalCoordinates.Count < 2 || row.SphericalCoordinates.Count > 9)
                    {
                        Debug.LogError($"[CSVLevelParser] Line {lineNumber}: TMTA task requires 2 to 9 coordinates. Found {row.SphericalCoordinates.Count}.");
                        return false;
                    }
                    break;
                    
                case BalloonTaskType.TMTB:
                    if (row.SphericalCoordinates.Count % 2 != 0)
                    {
                        Debug.LogError($"[CSVLevelParser] Line {lineNumber}: TMTB task requires an even number of coordinates (pairs of Number/Letter). Found {row.SphericalCoordinates.Count}.");
                        return false;
                    }
                    break;
            }
            return true;
        }

        /// <summary>
        /// Converts Spherical coordinates to Cartesian World Space.
        /// User requested for full ROM to use only horizontal and vertical (flat wall), so Z is kept constant.
        /// </summary>
        public static Vector3 SphericalToCartesian(float azimuthDeg, float elevationDeg, float safeRadius)
        {
            float azRad = azimuthDeg * Mathf.Deg2Rad;
            float elRad = elevationDeg * Mathf.Deg2Rad;

            // X = horizontal, Y = vertical, Z = constant depth
            float x = safeRadius * Mathf.Sin(azRad);
            float y = safeRadius * Mathf.Sin(elRad);
            float z = safeRadius; 

            return new Vector3(x, y, z);
        }
    }
}
