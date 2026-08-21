using System;
using System.IO;
using UnityEngine;

namespace ArcRoll.Core
{
    [Serializable]
    public class ArcRollPlayerProfile
    {
        public string name = "Player";
        public ArcRollCalibrationData calibrationData = new();
    }

    [Serializable]
    public class ArcRollCalibrationData
    {
        public float headToShoulderLength;
        public float shoulderToShoulderLength;
        public ArcRollArmData leftArmData = new();
        public ArcRollArmData rightArmData = new();
    }

    [Serializable]
    public class ArcRollArmData
    {
        public float shoulderFlexionComfort;
        public float shoulderFlexionMax;
        public float shoulderAbductionComfort;
        public float shoulderAbductionMax;
        public Vector2 shoulderHorizontalAdductionComfort;
        public Vector2 shoulderHorizontalAdductionMax;
        public float reachComfort;
        public float reachMax;
    }

    public static class ProfileLoader
    {
        private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "PlayerProfile.json");

        public static ArcRollPlayerProfile LoadProfile()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("ArcRoll ProfileLoader: No player profile JSON found. Creating safe default values.");
                return CreateDefaultProfile();
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                ArcRollPlayerProfile profile = JsonUtility.FromJson<ArcRollPlayerProfile>(json);
                if (profile == null)
                {
                    return CreateDefaultProfile();
                }
                Debug.Log("ArcRoll ProfileLoader: Calibrated JSON loaded successfully.");
                return profile;
            }
            catch (Exception e)
            {
                Debug.LogError($"ArcRoll ProfileLoader: Failed to load player profile from json.\n{e.Message}");
                return CreateDefaultProfile();
            }
        }

        private static ArcRollPlayerProfile CreateDefaultProfile()
        {
            ArcRollPlayerProfile defaultProfile = new ArcRollPlayerProfile();
            // Default clinical values for fallbacks (e.g. 60cm reach, 45 degree abduction)
            defaultProfile.calibrationData.leftArmData.reachMax = 0.6f;
            defaultProfile.calibrationData.leftArmData.shoulderAbductionMax = 45f;
            
            defaultProfile.calibrationData.rightArmData.reachMax = 0.6f;
            defaultProfile.calibrationData.rightArmData.shoulderAbductionMax = 45f;
            return defaultProfile;
        }
    }
}
