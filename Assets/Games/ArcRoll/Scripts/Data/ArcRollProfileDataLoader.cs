using UnityEngine;

namespace ArcRoll.Core
{
    public class ArcRollProfileDataLoader : MonoBehaviour
    {
        public static ArcRollProfileDataLoader Instance { get; private set; }

        [Header("Target Profile ScriptableObject")]
        [Tooltip("Drag the ArcRollRehabProfileSO asset here so the JSON data can be injected into it at runtime.")]
        public ArcRollRehabProfileSO targetProfileSO;

        [Header("Session Settings")]
        [Tooltip("Check if the patient is using their left arm. Uncheck for right arm.")]
        public bool useLeftArm = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadAndInjectProfile();
        }

        /// <summary>
        /// Reads the JSON profile from persistent data and injects it into the referenced ScriptableObject.
        /// </summary>
        public void LoadAndInjectProfile()
        {
            ArcRollPlayerProfile currentPlayerProfile = ProfileLoader.LoadProfile();

            if (currentPlayerProfile == null)
            {
                Debug.LogWarning("[ArcRollProfileDataLoader] No profile JSON loaded, using defaults on ScriptableObject.");
                return;
            }

            if (targetProfileSO == null)
            {
                Debug.LogError("[ArcRollProfileDataLoader] Target Profile ScriptableObject reference is missing!");
                return;
            }

            // Extract correct arm data
            ArcRollArmData activeArm = useLeftArm 
                ? currentPlayerProfile.calibrationData.leftArmData 
                : currentPlayerProfile.calibrationData.rightArmData;

            // Inject the data directly into the ScriptableObject for use across scripts
            targetProfileSO.patientName = currentPlayerProfile.name;
            targetProfileSO.isLeftArm = useLeftArm;
            targetProfileSO.armLength = activeArm.reachMax > 0.1f ? activeArm.reachMax : 0.6f;
            targetProfileSO.maxFlexion = activeArm.shoulderFlexionMax;
            targetProfileSO.maxAbduction = activeArm.shoulderAbductionMax;
            targetProfileSO.maxAdduction = activeArm.shoulderHorizontalAdductionMax.x;

            Debug.Log($"[ArcRollProfileDataLoader] Successfully loaded JSON and injected details into {targetProfileSO.name}.");
        }
    }
}
