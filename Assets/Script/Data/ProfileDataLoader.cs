using UnityEngine;

namespace WhackAMole.Data 
{
    public class ProfileDataLoader : MonoBehaviour
    {
        public static ProfileDataLoader Instance { get; private set; }

        // The raw profile data
        private PlayerProfile currentPlayerProfile;

        [Header("Target Profiles")]
        [Tooltip("Drag the RehabProfile ScriptableObject here so we can inject the JSON data into it.")]
        public RehabProfileSO targetProfileSO;
        
        [Tooltip("Drag the DifficultyProfile ScriptableObject here to dynamically set mole speed.")]
        public DifficultyProfileSO targetDifficultySO;

        [Header("Session Settings")]
        [Tooltip("Check this if the patient is using their left arm. Uncheck for right arm.")]
        public bool useLeftArm = true;

        // --- ESSENTIAL GAMEPLAY VARIABLES ---
        [Header("Calibration Data")]
        public float playerHeadToShoulder;
        public float playerShoulderWidth;
        
        [Header("Gameplay Limits")]
        public float playerMaxReach;
        public float playerReactionTime;

        [Header("Session Tracking")]
        public int currentArmReps;

        void Awake()
        {
            // Singleton setup so ScoreManager can find this easily
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Fetch the data from your existing SaveManager early (Awake)
            currentPlayerProfile = SaveManager.LoadPlayerProfile();
            ExtractEssentialData();
        }

        private void ExtractEssentialData()
        {
            if (currentPlayerProfile == null)
            {
                Debug.LogError("[ProfileData] No player profile loaded! Using defaults.");
                return;
            }

            // 1. Body Measurements (for setting up the table/holes)
            playerHeadToShoulder = currentPlayerProfile.calibrationData.headToShoulderLength;
            playerShoulderWidth = currentPlayerProfile.calibrationData.shoulderToShoulderLength;

            // Choose the correct arm data based on the therapist/player choice
            ArmData activeArm = useLeftArm 
                ? currentPlayerProfile.calibrationData.leftArmData 
                : currentPlayerProfile.calibrationData.rightArmData;

            // 2. Gameplay Limits (Reach and Reaction Speed)
            playerMaxReach = activeArm.reachMax; 
            playerReactionTime = activeArm.reactionTime; 

            // 3. Progress Tracking
            currentArmReps = currentPlayerProfile.weekData.armRepCount;

            // --- OPTION A INJECTION ---
            
            // A. Inject into RehabProfileSO (Table Dimensions & Limits)
            if (targetProfileSO != null)
            {
                targetProfileSO.patientName = currentPlayerProfile.name;
                targetProfileSO.isLeftArm = useLeftArm;
                
                targetProfileSO.armLength = playerMaxReach; 
                targetProfileSO.maxFlexion = activeArm.shoulderFlexionMax;
                targetProfileSO.maxAbduction = activeArm.shoulderAbductionMax;
                targetProfileSO.shoulderHorizontalAdductionMax = activeArm.shoulderHorizontalAdductionMax.y;

                // Set Table Height logic was moved to WorkspaceAutoPositioner dynamically based on headset height

                Debug.Log("[ProfileData] Successfully injected JSON data into RehabProfileSO!");
            }

            // B. Inject into DifficultyProfileSO (Reaction Time / Speeds)
            if (targetDifficultySO != null)
            {
                // We use their saved reaction time as the baseline for how long the mole stays visible.
                // You might want to add a buffer (e.g., +0.5s) so it's not impossible, but this scales perfectly!
                targetDifficultySO.moleVisibleDuration = playerReactionTime + 0.5f; 
                
                Debug.Log($"[ProfileData] Successfully injected Reaction Time ({playerReactionTime}s) into DifficultyProfileSO!");
            }
        }

        // Call this when a mole is hit to update the save data
        public void AddArmRep()
        {
            if (currentPlayerProfile != null)
            {
                currentPlayerProfile.weekData.armRepCount++;
                currentArmReps = currentPlayerProfile.weekData.armRepCount;
                
                SaveManager.SavePlayerProfile(currentPlayerProfile);
            }
        }
    }
}
