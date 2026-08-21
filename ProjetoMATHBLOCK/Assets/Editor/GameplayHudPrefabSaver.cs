using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameplayHudPrefabSaver
{
    private const string PrefabPath = "Assets/Prefab/HUD/Gameplay HUD.prefab";
    private const string RequestPath = "Assets/Editor/SaveGameplayHud.request";

    static GameplayHudPrefabSaver()
    {
        if (File.Exists(Path.GetFullPath(RequestPath)))
            EditorApplication.delayCall += SaveWhenReady;
    }

    [MenuItem("MATHBLOCK/HUD/Salvar alteracoes na HUD global")]
    public static void SaveFromMenu()
    {
        SaveWhenReady();
    }

    private static void SaveWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += SaveWhenReady;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("HUD global: saia do Play Mode antes de salvar alteracoes no prefab.");
            return;
        }

        try
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath.Equals(PrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                System.Reflection.MethodInfo savePrefab = typeof(PrefabStage).GetMethod(
                    "SavePrefab",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                if (savePrefab != null)
                    savePrefab.Invoke(stage, null);
                else
                    EditorSceneManager.SaveScene(stage.scene);
            }
            else
            {
                GameObject instance = FindGameplayHudInstance(SceneManager.GetActiveScene());
                if (instance == null)
                    throw new InvalidOperationException("Nenhuma instancia editavel de Gameplay HUD foi encontrada na cena ativa.");

                GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(instance);
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
                if (!sourcePath.Equals(PrefabPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"A HUD selecionada pertence a outro prefab: {sourcePath}");

                PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
                EditorSceneManager.SaveScene(instanceRoot.scene);
            }

            AssetDatabase.SaveAssets();
            RemoveRequest();
            Debug.Log("HUD global: alteracoes aplicadas com sucesso em Gameplay HUD.prefab.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static GameObject FindGameplayHudInstance(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.Equals("Gameplay HUD", StringComparison.OrdinalIgnoreCase) &&
                PrefabUtility.IsPartOfPrefabInstance(root))
                return root;
        }
        return null;
    }

    private static void RemoveRequest()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RequestPath) != null)
            AssetDatabase.DeleteAsset(RequestPath);
        else if (File.Exists(Path.GetFullPath(RequestPath)))
            File.Delete(Path.GetFullPath(RequestPath));
    }
}
