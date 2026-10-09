#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(ExpressionPuzzleController))]
public class ExpressionPuzzleControllerEditor : Editor
{
    private readonly BoxBoundsHandle boundsHandle = new BoxBoundsHandle();

    private SerializedProperty layoutCenterProperty;
    private SerializedProperty layoutSizeProperty;

    private void OnEnable()
    {
        layoutCenterProperty = serializedObject.FindProperty("layoutCenter");
        layoutSizeProperty = serializedObject.FindProperty("layoutSize");
    }

public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        ExpressionPuzzleController controller = (ExpressionPuzzleController)target;

        if (changed && controller.ApplyLayoutInEditMode)
        {
            RecordPadTransforms(controller, "Atualizar layout dos pads");
            controller.ApplyLayout();
            EditorUtility.SetDirty(controller);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Aplicar layout dos pads"))
        {
            Undo.RecordObject(controller, "Aplicar layout dos pads");
            RecordPadTransforms(controller, "Aplicar layout dos pads");
            controller.ApplyLayout();
            EditorUtility.SetDirty(controller);
        }

        if (Application.isPlaying && GUILayout.Button("Randomizar puzzle"))
        {
            controller.RandomizePuzzle();
        }
    }

    private void OnSceneGUI()
    {
        ExpressionPuzzleController controller = (ExpressionPuzzleController)target;

        serializedObject.Update();

        boundsHandle.center = layoutCenterProperty.vector3Value;
        boundsHandle.size = layoutSizeProperty.vector3Value;

        using (new Handles.DrawingScope(controller.transform.localToWorldMatrix))
        {
            EditorGUI.BeginChangeCheck();
            boundsHandle.DrawHandle();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Redimensionar area dos pads");
                RecordPadTransforms(controller, "Redimensionar area dos pads");

                layoutCenterProperty.vector3Value = boundsHandle.center;
                layoutSizeProperty.vector3Value = new Vector3(
                    Mathf.Max(0.01f, Mathf.Abs(boundsHandle.size.x)),
                    Mathf.Max(0.01f, Mathf.Abs(boundsHandle.size.y)),
                    Mathf.Max(0.01f, Mathf.Abs(boundsHandle.size.z)));

                serializedObject.ApplyModifiedProperties();
                controller.ApplyLayout();
                EditorUtility.SetDirty(controller);
            }
        }
    }

    private static void RecordPadTransforms(
        ExpressionPuzzleController controller,
        string undoName)
    {
        Transform[] padTransforms = controller.GetPadTransforms();

        foreach (Transform padTransform in padTransforms)
        {
            if (padTransform != null)
                Undo.RecordObject(padTransform, undoName);
        }
    }
}
#endif
