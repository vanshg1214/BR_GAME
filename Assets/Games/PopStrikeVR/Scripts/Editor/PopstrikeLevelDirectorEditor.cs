using UnityEditor;
using UnityEngine;
using PopstrikeVR.Core;

namespace PopstrikeVR.EditorScripts
{
    [CustomEditor(typeof(PopstrikeLevelDirector))]
    public class PopstrikeLevelDirectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Hide the script reference field for a cleaner look
                if (iterator.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator);
                    }
                    continue;
                }

                // Check for duration specific wave threshold fields
                if (iterator.name.Contains("_3min") || iterator.name.Contains("_5min"))
                {
                    var sessionDurationProp = serializedObject.FindProperty("sessionDuration");
                    if (sessionDurationProp != null)
                    {
                        int durationIndex = sessionDurationProp.enumValueIndex;
                        
                        // durationIndex 0 = ThreeMinutes
                        // durationIndex 1 = FiveMinutes
                        
                        // Only draw if the selected dropdown matches the field's intended duration
                        if (iterator.name.Contains("_3min") && durationIndex != 0) continue;
                        if (iterator.name.Contains("_5min") && durationIndex != 1) continue;
                    }
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
