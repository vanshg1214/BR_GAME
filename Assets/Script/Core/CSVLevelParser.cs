using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WhackAMole
{
    public struct CSVSpawnTarget
    {
        public float Azimuth;
        public float Elevation;
        public float Distance;
        public string CharacterTag;
    }

    public static class CSVLevelParser
    {
        public static Queue<CSVSpawnTarget> ParseCSVText(string csvText)
        {
            Queue<CSVSpawnTarget> queue = new Queue<CSVSpawnTarget>();
            
            if (string.IsNullOrWhiteSpace(csvText))
            {
                Debug.LogWarning("[CSVLevelParser] CSV text is null or empty.");
                return queue;
            }

            using (StringReader reader = new StringReader(csvText))
            {
                string headerLine = reader.ReadLine(); // Skip header
                string line;
                int lineNumber = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] values = line.Split(',');
                    if (values.Length < 4)
                    {
                        Debug.LogWarning($"[CSVLevelParser] Line {lineNumber} does not have at least 4 columns (Character, Azimuth, Elevation, Distance). Skipping.");
                        continue;
                    }

                    if (float.TryParse(values[1], out float azimuth) &&
                        float.TryParse(values[2], out float elevation) &&
                        float.TryParse(values[3], out float distance))
                    {
                        string charCode = values[0].Trim().ToUpper();
                        string charTag = "Standard"; // Default
                        switch (charCode)
                        {
                            case "H": charTag = "Heavy"; break; // Cage Hamster
                            case "S": charTag = "Standard"; break; // Standard Squirrel
                            case "F": charTag = "Fake"; break; // Fake Squirrel
                            case "B": charTag = "Bird"; break; // Fly Mole
                            case "T": charTag = "Treasure"; break; // Treasure (Turtle/Hamster)
                            case "D": charTag = "Dog"; break; // Dog Mole
                            default:
                                Debug.LogWarning($"[CSVLevelParser] Line {lineNumber} unknown character code '{charCode}'. Defaulting to 'Standard'.");
                                break;
                        }

                        queue.Enqueue(new CSVSpawnTarget
                        {
                            Azimuth = azimuth,
                            Elevation = elevation,
                            Distance = distance,
                            CharacterTag = charTag
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"[CSVLevelParser] Line {lineNumber} has malformed float data. Skipping. Raw line: {line}");
                    }
                }
            }
            
            Debug.Log($"[CSVLevelParser] Successfully parsed {queue.Count} targets from CSV.");
            return queue;
        }
    }
}
