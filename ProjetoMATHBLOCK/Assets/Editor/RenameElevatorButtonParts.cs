using UnityEditor;
using UnityEngine;

internal static class RenameElevatorButtonParts
{
    private const string PrefabPath = "Assets/Prefab/Botao.prefab";

    [InitializeOnLoadMethod]
    private static void RenamePartsOnEditorLoad()
    {
        EditorApplication.delayCall += RenameParts;
    }

    private static void RenameParts()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
            return;

        bool changed = false;
        Transform basePart = FindChildRecursive(prefabRoot.transform, "Cylinder");
        Transform buttonPart = FindChildRecursive(prefabRoot.transform, "Cylinder.001");

        if (basePart != null && basePart.name != "base")
        {
            basePart.name = "base";
            changed = true;
        }

        if (buttonPart != null && buttonPart.name != "botao")
        {
            buttonPart.name = "botao";
            changed = true;
        }

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Debug.Log("Prefab Botao: partes renomeadas para base e botao.");
        }

        PrefabUtility.UnloadPrefabContents(prefabRoot);
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
