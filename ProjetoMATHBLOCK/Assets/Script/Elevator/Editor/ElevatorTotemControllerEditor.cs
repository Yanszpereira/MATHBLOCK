#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ElevatorTotemController))]
public sealed class ElevatorTotemControllerEditor : Editor
{
    private SerializedProperty finalDestination;

    private void OnEnable()
    {
        finalDestination = serializedObject.FindProperty("finalDestination");
    }

    private void OnSceneGUI()
    {
        ElevatorTotemController controller = (ElevatorTotemController)target;
        if (controller == null || finalDestination == null || Application.isPlaying)
            return;

        EditorGUI.BeginChangeCheck();
        Vector3 nextPosition = Handles.PositionHandle(finalDestination.vector3Value, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(controller, "Mover PFinal do elevador");
        finalDestination.vector3Value = nextPosition;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
    }
}
#endif
