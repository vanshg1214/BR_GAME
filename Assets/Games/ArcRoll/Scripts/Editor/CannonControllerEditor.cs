using UnityEngine;
using UnityEditor;

namespace ArcRoll.Editor
{
    [CustomEditor(typeof(ArcRoll.Gameplay.CannonController))]
    public class CannonControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ArcRoll.Gameplay.CannonController cannon = (ArcRoll.Gameplay.CannonController)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🎯 FirePoint Alignment Helpers", EditorStyles.boldLabel);

            if (GUILayout.Button("Center FirePoint Horizontally (X = 0)", GUILayout.Height(30)))
            {
                SerializedObject serializedObj = new SerializedObject(cannon);
                SerializedProperty fpProp = serializedObj.FindProperty("firePoint");
                Transform fp = fpProp.objectReferenceValue as Transform;

                if (fp != null)
                {
                    Undo.RecordObject(fp, "Center FirePoint X");
                    Vector3 pos = fp.localPosition;
                    pos.x = 0f;
                    fp.localPosition = pos;
                    EditorUtility.SetDirty(fp);
                    Debug.Log($"[CannonControllerEditor] Centered FirePoint local X to 0. New local pos: {fp.localPosition}");
                }
                else
                {
                    Debug.LogWarning("[CannonControllerEditor] FirePoint is not assigned in CannonController!");
                }
            }
        }
    }
}
