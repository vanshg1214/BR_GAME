using UnityEngine;

namespace Rehab.Volleyball.Data
{
    /// <summary>
    /// Loads calibration values from SaveManager and injects them into the VolleyballRehabProfileSO.
    /// This keeps the game separate from the WhackAMole game systems while using the same saved data.
    /// </summary>
    public class VolleyballProfileDataLoader : MonoBehaviour
    {
        public static VolleyballProfileDataLoader Instance { get; private set; }

        [Header("Target Profile")]
        [Tooltip("Drag the VolleyballRehabProfile ScriptableObject here to dynamically inject save data.")]
        [SerializeField] private VolleyballRehabProfileSO targetProfileSO;

        [Header("Manual Fallback Settings")]
        [Tooltip("Check this if exercising the left arm (used to load corresponding arm save data).")]
        [SerializeField] private bool useLeftArm = true;

        private PlayerProfile currentPlayerProfile;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadAndInjectData();
        }

        /// <summary>
        /// Loads the patient profile from the SaveManager JSON and updates the ScriptableObject.
        /// </summary>
        public void LoadAndInjectData()
        {
            currentPlayerProfile = SaveManager.LoadPlayerProfile();

            if (currentPlayerProfile == null)
            {
                Debug.LogError("[VolleyballProfileDataLoader] Failed to load PlayerProfile from SaveManager. Using defaults.");
                return;
            }

            if (targetProfileSO == null)
            {
                Debug.LogError("[VolleyballProfileDataLoader] Target VolleyballRehabProfileSO is not assigned in the inspector!");
                return;
            }

            // Sync legacy arm flags
            targetProfileSO.patientName = currentPlayerProfile.name;
            targetProfileSO.isLeftArm = useLeftArm;

            // Select active arm data based on setting
            ArmData activeArm = useLeftArm 
                ? currentPlayerProfile.calibrationData.leftArmData 
                : currentPlayerProfile.calibrationData.rightArmData;

            // Inject the data
            targetProfileSO.armLength = activeArm.reachMax > 0.05f ? activeArm.reachMax : 0.6f;
            targetProfileSO.maxFlexion = activeArm.shoulderFlexionMax > 0f ? activeArm.shoulderFlexionMax : 120f;
            targetProfileSO.maxAbduction = activeArm.shoulderAbductionMax > 0f ? activeArm.shoulderAbductionMax : 90f;
            
            // Safe assignment for Vector2 values
            if (activeArm.shoulderHorizontalAdductionMax != Vector2.zero)
            {
                targetProfileSO.shoulderHorizontalAdductionMax = activeArm.shoulderHorizontalAdductionMax.y;
            }
            else
            {
                targetProfileSO.shoulderHorizontalAdductionMax = 90f;
            }

            Debug.Log($"[VolleyballProfileDataLoader] Injected JSON calibration data for {currentPlayerProfile.name} into VolleyballRehabProfileSO. Flexion: {targetProfileSO.maxFlexion}, Abduction: {targetProfileSO.maxAbduction}, Reach: {targetProfileSO.armLength}");
        }

        /// <summary>
        /// Updates the save file when the player scores hits (optional progress logging).
        /// </summary>
        public void AddArmRep()
        {
            if (currentPlayerProfile != null)
            {
                currentPlayerProfile.weekData.armRepCount++;
                SaveManager.SavePlayerProfile(currentPlayerProfile);
            }
        }
    }
}
