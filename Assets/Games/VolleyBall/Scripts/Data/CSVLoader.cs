using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rehab.Volleyball.Data
{
    /// <summary>
    /// Handles loading and parsing opponent profile data from a CSV file.
    /// Assumes the CSV has exactly 3 headers: TargetAzimuth, TargetElevation, TargetDistance
    /// </summary>
    public static class CSVLoader
    {
        private const string DEFAULT_CSV_NAME = "Volleyball/opponent_profiles.csv";

        /// <summary>
        /// Loads the opponent profiles from the StreamingAssets folder.
        /// </summary>
        /// <param name="fileName">The name of the CSV file in StreamingAssets.</param>
        /// <returns>A dictionary mapping the wave index (0, 1, 2...) to the target profile configuration.</returns>
        public static Dictionary<int, OpponentProfile> LoadProfiles(string fileName = DEFAULT_CSV_NAME)
        {
            var profiles = new Dictionary<int, OpponentProfile>();
            string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[CSVLoader] Cannot find opponent profile CSV at: {filePath}. Returning empty dictionary.");
                return profiles;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                
                int waveIndex = 0;
                // Start at index 1 to skip the header row.
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    string[] columns = line.Split(',');

                    // Ensure we have the correct number of columns before parsing
                    if (columns.Length >= 3)
                    {
                        var profile = new OpponentProfile
                        {
                            TargetAzimuth = float.Parse(columns[0]),
                            TargetElevation = float.Parse(columns[1]),
                            TargetDistance = float.Parse(columns[2]),
                            HasTarget = true
                        };

                        profiles[waveIndex] = profile;
                        waveIndex++;
                    }
                    else
                    {
                        Debug.LogWarning($"[CSVLoader] Skipping invalid row at line {i + 1}: {line}");
                    }
                }
                
                Debug.Log($"[CSVLoader] Successfully loaded {profiles.Count} target waves from {fileName}.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CSVLoader] Error parsing CSV file at {filePath}: {ex.Message}");
            }

            return profiles;
        }

        /// <summary>
        /// Asynchronously loads the opponent profiles from StreamingAssets using UnityWebRequest.
        /// This is REQUIRED for Android builds where StreamingAssets are inside a .jar/.apk file.
        /// </summary>
        public static System.Collections.IEnumerator LoadProfilesAsync(System.Action<Dictionary<int, OpponentProfile>> onComplete, string fileName = DEFAULT_CSV_NAME)
        {
            var profiles = new Dictionary<int, OpponentProfile>();
            string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

            // On Android, we MUST use UnityWebRequest
            using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(filePath))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[CSVLoader] Error loading CSV via WebRequest: {request.error} at path {filePath}");
                    onComplete?.Invoke(profiles);
                    yield break;
                }

                string text = request.downloadHandler.text;
                string[] lines = text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

                int waveIndex = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    string[] columns = line.Split(',');
                    if (columns.Length >= 3)
                    {
                        try
                        {
                            var profile = new OpponentProfile
                            {
                                TargetAzimuth = float.Parse(columns[0]),
                                TargetElevation = float.Parse(columns[1]),
                                TargetDistance = float.Parse(columns[2]),
                                HasTarget = true
                            };

                            profiles[waveIndex] = profile;
                            waveIndex++;
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[CSVLoader] Error parsing row {i}: {ex.Message}");
                        }
                    }
                }

                Debug.Log($"[CSVLoader] Successfully loaded {profiles.Count} target waves asynchronously.");
                onComplete?.Invoke(profiles);
            }
        }
    }
}
