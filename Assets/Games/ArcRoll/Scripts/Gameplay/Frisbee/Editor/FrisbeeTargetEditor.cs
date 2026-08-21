#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ArcRoll.Gameplay.Frisbee;

namespace ArcRoll.Gameplay.Frisbee.Editor
{
    [CustomEditor(typeof(FrisbeeTarget))]
    [CanEditMultipleObjects]
    public class FrisbeeTargetEditor : UnityEditor.Editor
    {
        private SerializedProperty targetTypeProp;
        
        // Shared
        private SerializedProperty vfxSpawnPointProp;
        private SerializedProperty hitParticlesProp;
        private SerializedProperty hitAudioProp;
        private SerializedProperty impactSoundsProp;
        private SerializedProperty scoreValueProp;
        private SerializedProperty useAdvancedScoringRulesProp;

        // Balloon Only
        private SerializedProperty intactVisualsProp;
        private SerializedProperty brokenVisualsProp;
        private SerializedProperty explosionForceProp;
        private SerializedProperty explosionRadiusProp;
        private SerializedProperty explosionUpwardModifierProp;
        private SerializedProperty shardScaleMultiplierProp;

        private void OnEnable()
        {
            targetTypeProp = serializedObject.FindProperty("targetType");
            
            vfxSpawnPointProp = serializedObject.FindProperty("vfxSpawnPoint");
            hitParticlesProp = serializedObject.FindProperty("hitParticles");
            hitAudioProp = serializedObject.FindProperty("hitAudio");
            impactSoundsProp = serializedObject.FindProperty("impactSounds");
            scoreValueProp = serializedObject.FindProperty("scoreValue");
            useAdvancedScoringRulesProp = serializedObject.FindProperty("useAdvancedScoringRules");

            intactVisualsProp = serializedObject.FindProperty("intactVisuals");
            brokenVisualsProp = serializedObject.FindProperty("brokenVisuals");
            explosionForceProp = serializedObject.FindProperty("explosionForce");
            explosionRadiusProp = serializedObject.FindProperty("explosionRadius");
            explosionUpwardModifierProp = serializedObject.FindProperty("explosionUpwardModifier");
            shardScaleMultiplierProp = serializedObject.FindProperty("shardScaleMultiplier");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(targetTypeProp);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shared Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(vfxSpawnPointProp);
            EditorGUILayout.PropertyField(hitParticlesProp);
            EditorGUILayout.PropertyField(hitAudioProp);
            EditorGUILayout.PropertyField(impactSoundsProp);
            EditorGUILayout.PropertyField(scoreValueProp);
            EditorGUILayout.PropertyField(useAdvancedScoringRulesProp);

            // Conditional drawing based on enum
            if (targetTypeProp.enumValueIndex == (int)FrisbeeTargetType.Balloon)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Balloon Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(intactVisualsProp);
                EditorGUILayout.PropertyField(brokenVisualsProp);
                EditorGUILayout.PropertyField(explosionForceProp);
                EditorGUILayout.PropertyField(explosionRadiusProp);
                EditorGUILayout.PropertyField(explosionUpwardModifierProp);
                EditorGUILayout.PropertyField(shardScaleMultiplierProp);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
