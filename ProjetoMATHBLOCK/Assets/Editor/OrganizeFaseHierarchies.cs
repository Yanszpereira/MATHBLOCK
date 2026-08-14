#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OrganizeFaseHierarchies
{
    private const string SessionKey = "MATHBLOCK.HierarchyOrganizer.2026-08-13.v1";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Fase 1.unity",
        "Assets/Scenes/Fase 2.unity"
    };

    private static readonly string[] FolderNames =
    {
        "00_Sistemas",
        "01_Jogador_e_Cameras",
        "02_Cenario_e_Geometria",
        "03_MathBlocks_e_Puzzles",
        "04_Operadores",
        "05_Portas_e_Progressao",
        "06_Efeitos_Luzes_e_Audio",
        "07_Interface",
        "99_Outros"
    };

    static OrganizeFaseHierarchies()
    {
        EditorApplication.delayCall += TryOrganize;
    }

    [MenuItem("MATHBLOCK/Organizar Hierarchy/Fases 1 e 2")]
    public static void OrganizeFromMenu()
    {
        SessionState.EraseBool(SessionKey);
        TryOrganize();
    }

    private static void TryOrganize()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        SessionState.SetBool(SessionKey, true);
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                OrganizeScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("MATHBLOCK: Hierarchy da Fase 1 e Fase 2 organizada com sucesso.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.EraseBool(SessionKey);
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
    }

    private static void OrganizeScene(Scene scene)
    {
        Dictionary<string, Transform> folders = CreateFolders(scene);
        List<GameObject> candidates = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null || IsOrganizationFolder(root.name))
                continue;

            if (IsEnvironmentContainer(root.name))
            {
                for (int index = root.transform.childCount - 1; index >= 0; index--)
                    candidates.Add(root.transform.GetChild(index).gameObject);

                if (root.transform.childCount == 0)
                    UnityEngine.Object.DestroyImmediate(root);
            }
            else
            {
                candidates.Add(root);
            }
        }

        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || IsOrganizationFolder(candidate.name))
                continue;

            string category = GetCategory(candidate);
            Undo.SetTransformParent(candidate.transform, folders[category], "Organizar Hierarchy MATHBLOCK");
        }

        foreach (string folderName in FolderNames)
            folders[folderName].SetSiblingIndex(Array.IndexOf(FolderNames, folderName));
    }

    private static Dictionary<string, Transform> CreateFolders(Scene scene)
    {
        Dictionary<string, Transform> result = new Dictionary<string, Transform>();
        Dictionary<string, GameObject> existing = new Dictionary<string, GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
            existing[root.name] = root;

        foreach (string folderName in FolderNames)
        {
            GameObject folder;
            if (!existing.TryGetValue(folderName, out folder))
            {
                folder = new GameObject(folderName);
                SceneManager.MoveGameObjectToScene(folder, scene);
                Undo.RegisterCreatedObjectUndo(folder, "Criar pasta de Hierarchy MATHBLOCK");
            }
            result[folderName] = folder.transform;
        }
        return result;
    }

    private static string GetCategory(GameObject target)
    {
        string normalized = target.name.ToLowerInvariant();

        if (ContainsAny(normalized, "eventsystem", "manager", "spawn", "checkpoint", "desfazer", "sistema"))
            return "00_Sistemas";
        if (ContainsAny(normalized, "player", "camera", "pencil", "jogador") || HasComponent(target, "PlayerMovement", "GravityInteract", "PlayerInput"))
            return "01_Jogador_e_Cameras";
        if (ContainsAny(normalized, "mathblock", "conta", "numero", "pad", "puzzle") || HasComponent(target, "MathBlockValue", "PadMathBlockDetector"))
            return "03_MathBlocks_e_Puzzles";
        if (ContainsAny(normalized, "operador", "operator", "op_") || HasComponent(target, "OperatorsScript", "opItem"))
            return "04_Operadores";
        if (ContainsAny(normalized, "porta", "door", "progress", "pedestal") || HasComponent(target, "DoorValueVerifier", "DoorOpener"))
            return "05_Portas_e_Progressao";
        if (ContainsAny(normalized, "light", "luz", "cloud", "particle", "efeito", "effect", "audio", "som", "confete") ||
            target.GetComponentInChildren<Light>(true) != null || target.GetComponentInChildren<ParticleSystem>(true) != null)
            return "06_Efeitos_Luzes_e_Audio";
        if (ContainsAny(normalized, "canvas", "ui", "hud", "texto", "text", "crosshair") || target.GetComponentInChildren<Canvas>(true) != null)
            return "07_Interface";
        if (ContainsAny(normalized, "cenario", "environment", "enviroment", "chao", "floor", "wall", "parede", "mesh", "mapa"))
            return "02_Cenario_e_Geometria";

        // Objetos com renderização/colisão, mas sem comportamento de jogo,
        // são tratados como geometria. O restante fica em Outros.
        if (target.GetComponentInChildren<Renderer>(true) != null || target.GetComponentInChildren<Collider>(true) != null)
            return "02_Cenario_e_Geometria";
        return "99_Outros";
    }

    private static bool HasComponent(GameObject target, params string[] componentNames)
    {
        Component[] components = target.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null)
                continue;
            string typeName = component.GetType().Name;
            foreach (string expected in componentNames)
                if (typeName == expected)
                    return true;
        }
        return false;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (string term in terms)
            if (value.Contains(term))
                return true;
        return false;
    }

    private static bool IsEnvironmentContainer(string name)
    {
        return name.Equals("Environment", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Enviroment", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOrganizationFolder(string name)
    {
        return Array.IndexOf(FolderNames, name) >= 0;
    }
}
#endif
