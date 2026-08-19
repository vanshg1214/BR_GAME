using System.IO;
using UnityEngine;
using System;

namespace PopstrikeVR.Data
{
    [Serializable]
    public class SessionResults
    {
        public string timestamp;
        public int totalTasks;
        public int correctHits;
        public int missedOrErrors;
        public float finalAccuracyPercent;
        public int highestCombo;
    }

    /// <summary>
    /// Serializes end-of-session data into a secure local JSON format.
    /// </summary>
    public static class SessionLogger
    {
        public static void SaveSession(SessionResults results)
        {
            results.timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string json = JsonUtility.ToJson(results, true);
            
            string folderPath = Path.Combine(Application.persistentDataPath, "PopstrikeLogs");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, $"Session_{results.timestamp}.json");
            File.WriteAllText(filePath, json);

            Debug.Log($"[SessionLogger] Successfully saved session data to: {filePath}");
        }
    }
}
