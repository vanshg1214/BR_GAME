using System;
using System.IO;
using UnityEngine;

public static class SaveManager {
    private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "PlayerProfile.json");

    public static void SavePlayerProfile(PlayerProfile playerProfile) {
        try {
            string json = JsonUtility.ToJson(playerProfile, true);

            File.WriteAllText(SavePath, json);

            Debug.Log($"Player profile saved at: {SavePath}");
        }
        catch (Exception e) {
            Debug.LogError($"Failed to save player profile.\n{e}");
        }
    }

    public static PlayerProfile LoadPlayerProfile() {
        try {
            if (!File.Exists(SavePath)) {
                Debug.LogWarning("No save file found.");
                return new PlayerProfile();
            }

            string json = File.ReadAllText(SavePath);

            PlayerProfile playerProfile = JsonUtility.FromJson<PlayerProfile>(json);

            if (playerProfile == null) {
                return new PlayerProfile();
            }

            Debug.Log("Player profile loaded successfully.");

            return playerProfile;
        }
        catch (Exception e) {
            Debug.LogError($"Failed to load player profile.\n{e}");
            return new PlayerProfile();
        }
    }
}


[Serializable]
public class PlayerProfile {
    public string name = "Player";
    public CalibrationData calibrationData = new();
    public WeekData weekData = new();
    public string lastWeeklyResetDate = "";
}

[Serializable]
public class CalibrationData {
    public float headToShoulderLength;
    public float shoulderToShoulderLength;
    public ArmData leftArmData = new();
    public ArmData rightArmData = new();
}

[Serializable]
public class ArmData {
    public float shoulderFlexionComfort;
    public float shoulderFlexionMax;

    public float shoulderAbductionComfort;
    public float shoulderAbductionMax;

    public Vector2 shoulderHorizontalAdductionComfort;
    public Vector2 shoulderHorizontalAdductionMax;

    public Vector2 shoulderInternalExternalRotationComfort;
    public Vector2 shoulderInternalExternalRotationMax;

    public float reachComfort;
    public float reachMax;

    public float reactionTime;
}

[Serializable]
public class WeekData {
    public bool[] weeklyLogin = new bool[7]; // Sunday -> Saturday
    public int armRepCount = 0;
    public int handRepCount = 0;
    public int legRepCount = 0;
    public int cognitiveRepCount = 0;
}
