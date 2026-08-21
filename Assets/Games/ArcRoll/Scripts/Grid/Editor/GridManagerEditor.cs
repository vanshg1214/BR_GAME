using UnityEngine;
using UnityEditor;
using ArcRoll.Grid;

namespace ArcRoll.Editor
{
    [CustomEditor(typeof(GridManager))]
    public class GridManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector (so you can still see Arc Radius, etc.)
            DrawDefaultInspector();

            GridManager gridManager = (GridManager)target;

            GUILayout.Space(15);
            EditorGUILayout.LabelField("Environment Design Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Click Generate to spawn physical prefabs into the scene so you can build your environment around them without having to hit Play.", MessageType.Info);

            GUILayout.BeginHorizontal();
            
            // Generate Button
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f); // Green button
            if (GUILayout.Button("Generate Preview", GUILayout.Height(30)))
            {
                gridManager.GeneratePreview();
            }

            // Clear Button
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f); // Red button
            if (GUILayout.Button("Clear Preview", GUILayout.Height(30)))
            {
                gridManager.ClearPreview();
            }
            
            GUILayout.EndHorizontal();
            
            // Reset color back to normal
            GUI.backgroundColor = Color.white;
        }
    }
}
