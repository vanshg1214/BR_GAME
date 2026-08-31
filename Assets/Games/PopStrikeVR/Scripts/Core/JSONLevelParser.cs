using System;
using System.Collections.Generic;
using UnityEngine;
using PopstrikeVR.Data;
using Newtonsoft.Json;

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
    /// Represents a single parsed task line, converted into 3D space.
    /// </summary>
    [System.Serializable]
    public class TaskRow
    {
        public BalloonTaskType TaskType;
        public List<Vector3> SphericalCoordinates = new List<Vector3>();
        public List<Vector3> ComputedWorldPositions = new List<Vector3>();
    }

    // --- PROCEDURAL JSON SERIALIZATION CLASSES ---
    
    [System.Serializable]
    public class BalloonSpawnChances
    {
        public float orangePunch;
        public float blueSlash;
        public float greenTrace;
        public float tmtA;
        public float tmtB;
    }

    [System.Serializable]
    public class PopstrikeRoundJSON
    {
        public float durationInMinutes;
    }

    [System.Serializable]
    public class PopstrikeBreakJSON
    {
        public float durationInMinutes;
    }

    [System.Serializable]
    public class PopstrikeLevelJSON
    {
        public List<PopstrikeRoundJSON> rounds;
        public List<PopstrikeBreakJSON> breaks;
        public BalloonSpawnChances spawnChances;
    }

    /// <summary>
    /// Deserializes the new Probability-Based Cloud JSON.
    /// </summary>
    public static class JSONLevelParser
    {
        public static PopstrikeLevelJSON ParseLevelJSON(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent)) return null;

            try
            {
                return JsonConvert.DeserializeObject<PopstrikeLevelJSON>(jsonContent);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JSONLevelParser] CRITICAL ERROR: Failed to parse JSON: {e.Message}");
                return null;
            }
        }

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
