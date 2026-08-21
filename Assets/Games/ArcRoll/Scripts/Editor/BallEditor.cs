using UnityEditor;
using UnityEngine;
using ArcRoll.Gameplay;

namespace ArcRoll.Editor
{
    [CustomEditor(typeof(Ball))]
    public class BallEditor : UnityEditor.Editor
    {
        private SerializedProperty typeProp;
        private SerializedProperty floorDespawnDelayProp;
        private SerializedProperty floorTagProp;
        private SerializedProperty throwPowerMultiplierProp;
        private SerializedProperty aimAssistStrengthProp;
        private SerializedProperty aimAssistConeAngleProp;
        
        private SerializedProperty enableAutoLobProp;
        private SerializedProperty hoopHeightOffsetProp;
        private SerializedProperty lobTimeOfFlightProp;
        
        private SerializedProperty enableInFlightMagnetProp;
        private SerializedProperty magneticPullStrengthProp;
        private SerializedProperty magneticPullRadiusProp;

        private void OnEnable()
        {
            typeProp = serializedObject.FindProperty("type");
            floorDespawnDelayProp = serializedObject.FindProperty("floorDespawnDelay");
            floorTagProp = serializedObject.FindProperty("floorTag");
            
            throwPowerMultiplierProp = serializedObject.FindProperty("throwPowerMultiplier");
            aimAssistStrengthProp = serializedObject.FindProperty("aimAssistStrength");
            aimAssistConeAngleProp = serializedObject.FindProperty("aimAssistConeAngle");
            
            enableAutoLobProp = serializedObject.FindProperty("enableAutoLob");
            hoopHeightOffsetProp = serializedObject.FindProperty("hoopHeightOffset");
            lobTimeOfFlightProp = serializedObject.FindProperty("lobTimeOfFlight");
            
            enableInFlightMagnetProp = serializedObject.FindProperty("enableInFlightMagnet");
            magneticPullStrengthProp = serializedObject.FindProperty("magneticPullStrength");
            magneticPullRadiusProp = serializedObject.FindProperty("magneticPullRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(typeProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cleanup", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(floorDespawnDelayProp);
            EditorGUILayout.PropertyField(floorTagProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rehab Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(throwPowerMultiplierProp);
            
            // Aim assist applies to both, but maybe you only want it for basketball?
            // Actually, we use aim assist for bowling too (to help it go straight down the lane)
            EditorGUILayout.PropertyField(aimAssistStrengthProp);
            EditorGUILayout.PropertyField(aimAssistConeAngleProp);

            // ONLY show Basketball settings if Type is Basketball!
            if (typeProp.enumValueIndex == (int)Ball.BallType.Basketball)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Parabolic Auto-Lob (Basketball)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(enableAutoLobProp);
                if (enableAutoLobProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(hoopHeightOffsetProp);
                    EditorGUILayout.PropertyField(lobTimeOfFlightProp);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("In-Flight Magnetic Assist (Basketball)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(enableInFlightMagnetProp);
                if (enableInFlightMagnetProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(magneticPullStrengthProp);
                    EditorGUILayout.PropertyField(magneticPullRadiusProp);
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
