using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MATHBLOCKFase4FinalSetupWindow : EditorWindow
{
    private const string Fase4ScenePath = "Assets/Scenes/Fase 4.unity";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string ElevatorAssetPath = "Assets/3D_Models/Fase4/ElevadorVertical.fbx";
    private const string PortalAssetPath = "Assets/3D_Models/Fase4/PortaFinal.fbx";

    [SerializeField] private Transform arenaRoot;
    [SerializeField] private Transform elevatorPlacementMarker;
    [SerializeField] private Transform elevatorPlatform;
    [SerializeField] private Transform elevatorTopMarker;
    [SerializeField] private Transform portalPlacementMarker;
    [SerializeField] private Fase4ArenaRoundCoordinator coordinator;
    [SerializeField] private VoidRespawner voidRespawner;
    [SerializeField] private TMP_Text completionLabel;

    [MenuItem("Tools/MATHBLOCK/Configurar final da Fase 4")]
    private static void Open()
    {
        GetWindow<MATHBLOCKFase4FinalSetupWindow>("Final Fase 4");
    }

    [MenuItem("Tools/MATHBLOCK/Auto-configurar elevador e portal da Fase 4")]
    private static void AutoConfigureActiveFase4()
    {
        GetWindow<MATHBLOCKFase4FinalSetupWindow>("Final Fase 4").ConfigureAutomatically();
    }

    private void ConfigureAutomatically()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Play Mode ativo", "Saia do Play Mode antes de configurar os objetos da cena.", "OK");
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != Fase4ScenePath)
        {
            EditorUtility.DisplayDialog("Cena incorreta", "Abra Assets/Scenes/Fase 4.unity antes de executar este comando.", "OK");
            return;
        }

        Transform phaseRoot = FindInScene(scene, "Fase4_SegundaMetade");
        Transform arena = FindInSubtree(phaseRoot, "Arena_Root");
        Transform baseMarker = FindInSubtree(phaseRoot, "Elevador_Base");
        Transform platform = FindInSubtree(phaseRoot, "PlataformaFinal");
        Transform topMarker = FindInSubtree(phaseRoot, "Elevador_Alvo_Superior");
        Transform portalMarker = FindInSubtree(phaseRoot, "Portal_Marcador");
        Fase4ArenaRoundCoordinator roundCoordinator = FindComponentInScene<Fase4ArenaRoundCoordinator>(scene);
        VoidRespawner respawner = FindComponentInScene<VoidRespawner>(scene);

        string missing = BuildMissingList(
            (phaseRoot, "Fase4_SegundaMetade"),
            (arena, "Fase4_SegundaMetade/Arena_Root"),
            (baseMarker, "Elevador_Base"),
            (platform, "PlataformaFinal"),
            (topMarker, "Elevador_Alvo_Superior"),
            (portalMarker, "Portal_Marcador"),
            (roundCoordinator, "Fase4ArenaRoundCoordinator"),
            (respawner, "VoidRespawner"));
        if (!string.IsNullOrEmpty(missing))
        {
            EditorUtility.DisplayDialog(
                "Configuração incompleta — nenhuma alteração feita",
                "Não encontrei estes objetos/referências na cena aberta:\n\n" + missing +
                "\nConfirme se a cena foi salva/recarregada e se os nomes estão corretos. O comando não cria marcadores nem escolhe posições por conta própria.",
                "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null)
        {
            EditorUtility.DisplayDialog("Menu ausente — nenhuma alteração feita", "Não encontrei " + MenuScenePath + ". O portal não será configurado sem um destino real.", "OK");
            return;
        }

        arenaRoot = arena;
        elevatorPlacementMarker = baseMarker;
        elevatorPlatform = platform;
        elevatorTopMarker = topMarker;
        portalPlacementMarker = portalMarker;
        coordinator = roundCoordinator;
        voidRespawner = respawner;

        List<GameObject> elevatorCanonical = FindAllInSubtree(arena, "ElevadorVertical");
        List<GameObject> elevatorLegacy = FindAllInSubtree(arena, "ElevadorVertical_Emergencial");
        List<GameObject> portalCanonical = FindAllInSubtree(arena, "PortaFinal");
        List<GameObject> portalLegacy = FindAllInSubtree(arena, "PortalFinal_Emergencial");
        if (elevatorCanonical.Count > 1 || portalCanonical.Count > 1 ||
            (elevatorCanonical.Count == 0 && elevatorLegacy.Count > 1) ||
            (portalCanonical.Count == 0 && portalLegacy.Count > 1))
        {
            EditorUtility.DisplayDialog("Modelos duplicados ambíguos — nenhuma alteração feita", "Há mais de uma ocorrência com o mesmo nome canônico/emergencial. O comando não escolhe uma cópia arbitrariamente.", "OK");
            return;
        }

        Transform elevatorModel = elevatorCanonical.Count > 0
            ? elevatorCanonical[0].transform
            : elevatorLegacy.Count > 0 ? elevatorLegacy[0].transform : null;
        Transform portalModel = portalCanonical.Count > 0
            ? portalCanonical[0].transform
            : portalLegacy.Count > 0 ? portalLegacy[0].transform : null;
        var elevatorDuplicates = new List<GameObject>();
        var portalDuplicates = new List<GameObject>();
        if (elevatorCanonical.Count > 0)
            elevatorDuplicates.AddRange(elevatorLegacy.FindAll(candidate => candidate.activeInHierarchy));
        if (portalCanonical.Count > 0)
            portalDuplicates.AddRange(portalLegacy.FindAll(candidate => candidate.activeInHierarchy));

        if (elevatorModel == null && AssetDatabase.LoadAssetAtPath<GameObject>(ElevatorAssetPath) == null)
        {
            EditorUtility.DisplayDialog("FBX ausente — nenhuma alteração feita", "Não encontrei ElevadorVertical na hierarquia nem o FBX " + ElevatorAssetPath, "OK");
            return;
        }
        if (portalModel == null && AssetDatabase.LoadAssetAtPath<GameObject>(PortalAssetPath) == null)
        {
            EditorUtility.DisplayDialog("FBX ausente — nenhuma alteração feita", "Não encontrei PortaFinal na hierarquia nem o FBX " + PortalAssetPath, "OK");
            return;
        }

        foreach (GameObject duplicate in elevatorDuplicates)
        {
            if (!IsProvenGeneratedDuplicate(duplicate, elevatorModel.gameObject, "PlataformaFinal", scene))
            {
                EditorUtility.DisplayDialog("Elevador duplicado ambíguo — nenhuma alteração feita", "Existe ElevadorVertical_Emergencial, mas sua identidade não corresponde comprovadamente ao modelo ElevadorVertical, ou ele contém componentes/referências em uso. Nenhum objeto foi desativado.", "OK");
                return;
            }
        }
        foreach (GameObject duplicate in portalDuplicates)
        {
            if (!IsProvenGeneratedDuplicate(duplicate, portalModel.gameObject, null, scene))
            {
                EditorUtility.DisplayDialog("Portal duplicado ambíguo — nenhuma alteração feita", "Existe PortalFinal_Emergencial, mas sua identidade não corresponde comprovadamente ao modelo PortaFinal, ou ele contém componentes/referências em uso. Nenhum objeto foi desativado.", "OK");
                return;
            }
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Configurar elevador e portal da Fase 4");
        int undoGroup = Undo.GetCurrentGroup();

        if (elevatorModel == null)
        {
            GameObject created = InstantiateAtMarker(ElevatorAssetPath, "ElevadorVertical", baseMarker);
            if (created == null)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return;
            }
            elevatorModel = created.transform;
        }
        if (portalModel == null)
        {
            GameObject created = InstantiateAtMarker(PortalAssetPath, "PortaFinal", portalMarker);
            if (created == null)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return;
            }
            portalModel = created.transform;
        }
        else
            MoveToMarker(portalModel, portalMarker);

        GameObject boardingZone = FindInSubtree(arena, "Elevador_Embarque_Trigger")?.gameObject;
        if (boardingZone == null)
            boardingZone = CreateTriggerZone("Elevador_Embarque_Trigger", platform, new Vector3(4f, 1.5f, 4f), new Vector3(0f, 0.75f, 0f));
        else
            ConfigureTriggerZone(boardingZone, platform, new Vector3(4f, 1.5f, 4f), new Vector3(0f, 0.75f, 0f));

        Transform portalTrigger = FindInSubtree(arena, "PortalFinal_Completion_Trigger");
        GameObject portalTriggerObject;
        if (portalTrigger != null)
        {
            portalTriggerObject = portalTrigger.gameObject;
            ConfigureTriggerZone(portalTriggerObject, portalModel, new Vector3(3.2f, 3.2f, 0.8f), Vector3.zero);
        }
        else
        {
            portalTriggerObject = CreateTriggerZone("PortalFinal_Completion_Trigger", portalModel, new Vector3(3.2f, 3.2f, 0.8f), Vector3.zero);
        }

        if (boardingZone == null || portalTriggerObject == null)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            EditorUtility.DisplayDialog("Não foi possível criar os triggers", "Uma referência necessária está ausente; confira a Console. Nenhuma alteração foi salva automaticamente.", "OK");
            return;
        }

        Fase4EmergencyElevator lift = GetOrAdd<Fase4EmergencyElevator>(boardingZone);
        bool liftLinked = SetSerialized(lift, "coordinator", roundCoordinator) &
                          SetSerialized(lift, "movingPlatform", platform) &
                          SetSerialized(lift, "topPosition", topMarker) &
                          SetSerialized(lift, "voidRespawner", respawner);

        Fase4PortalCompletionTrigger completion = GetOrAdd<Fase4PortalCompletionTrigger>(portalTriggerObject);
        bool menuReady = EnsureMenuInBuildSettings();
        if (!liftLinked || !menuReady)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            EditorUtility.DisplayDialog("Vinculação incompleta — alterações revertidas", "Não foi possível serializar todas as referências do elevador ou incluir a cena MainMenu. As alterações desta operação foram desfeitas.", "OK");
            return;
        }

        foreach (GameObject duplicate in elevatorDuplicates)
            SetActiveWithUndo(duplicate, false);
        foreach (GameObject duplicate in portalDuplicates)
            SetActiveWithUndo(duplicate, false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = boardingZone;
        Debug.Log("Fase 4: configuração do elevador e portal aplicada. PlataformaFinal preservada; revise visualmente os triggers antes de salvar.");
        EditorUtility.DisplayDialog(
            "Configuração aplicada",
            "Elevador e portal vinculados à cena. A posição/rotação/escala de PlataformaFinal foi preservada. A cena está alterada e NÃO foi salva. Confira os triggers na Scene View e salve manualmente se estiver correto.",
            "OK");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "A ferramenta só altera a cena quando você aciona os comandos. A configuração automática procura os marcadores existentes, preserva PlataformaFinal e aborta se faltarem referências. A cena nunca é salva automaticamente.",
            MessageType.Info);

        arenaRoot = (Transform)EditorGUILayout.ObjectField("Arena_Root", arenaRoot, typeof(Transform), true);
        coordinator = (Fase4ArenaRoundCoordinator)EditorGUILayout.ObjectField("Coordenador M5", coordinator, typeof(Fase4ArenaRoundCoordinator), true);
        voidRespawner = (VoidRespawner)EditorGUILayout.ObjectField("VoidRespawner", voidRespawner, typeof(VoidRespawner), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Elevador", EditorStyles.boldLabel);
        elevatorPlacementMarker = (Transform)EditorGUILayout.ObjectField("Marcador de base", elevatorPlacementMarker, typeof(Transform), true);
        elevatorPlatform = (Transform)EditorGUILayout.ObjectField("Plataforma móvel", elevatorPlatform, typeof(Transform), true);
        elevatorTopMarker = (Transform)EditorGUILayout.ObjectField("Marcador de topo", elevatorTopMarker, typeof(Transform), true);
        using (new EditorGUI.DisabledScope(elevatorTopMarker != null || elevatorPlacementMarker == null || arenaRoot == null))
            if (GUILayout.Button("Criar marcador inicial 6 m acima da base"))
                CreateTopMarker();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Portal", EditorStyles.boldLabel);
        portalPlacementMarker = (Transform)EditorGUILayout.ObjectField("Marcador do portal", portalPlacementMarker, typeof(Transform), true);
        completionLabel = (TMP_Text)EditorGUILayout.ObjectField("Texto de conclusão (opcional)", completionLabel, typeof(TMP_Text), true);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!CanPlaceModels()))
            if (GUILayout.Button("Localizar ou instanciar elevador e portal nos marcadores"))
                PlaceMissingModels();

        using (new EditorGUI.DisabledScope(!CanConfigure()))
            if (GUILayout.Button("Configurar triggers e componentes"))
                ConfigureTriggers();

        using (new EditorGUI.DisabledScope(EditorBuildSettings.scenes.Length == 0))
            if (GUILayout.Button("Verificar cena MainMenu no Build Settings"))
                EnsureMenuInBuildSettings();
    }

    private bool CanPlaceModels()
    {
        return IsFase4Active() && arenaRoot != null && elevatorPlacementMarker != null && portalPlacementMarker != null;
    }

    private bool CanConfigure()
    {
        return IsFase4Active() && arenaRoot != null && coordinator != null && voidRespawner != null &&
               elevatorPlatform != null && elevatorTopMarker != null;
    }

    private static bool IsFase4Active()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        return active.IsValid() && active.path == Fase4ScenePath;
    }

    private void PlaceMissingModels()
    {
        if (!IsFase4Active())
        {
            EditorUtility.DisplayDialog("Cena incorreta", "Abra Assets/Scenes/Fase 4.unity antes de continuar.", "OK");
            return;
        }

        GameObject elevator = FindExistingModel(arenaRoot, "ElevadorVertical", "ElevadorVertical_Emergencial")?.gameObject;
        if (elevator == null)
            elevator = InstantiateAtMarker(ElevatorAssetPath, "ElevadorVertical_Emergencial", elevatorPlacementMarker);

        GameObject portal = FindExistingModel(arenaRoot, "PortaFinal", "PortalFinal_Emergencial")?.gameObject;
        if (portal == null)
            portal = InstantiateAtMarker(PortalAssetPath, "PortalFinal_Emergencial", portalPlacementMarker);
        else if (portalPlacementMarker != null)
            MoveToMarker(portal.transform, portalPlacementMarker);

        EditorGUIUtility.PingObject(elevator != null ? elevator : portal);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Fase 4: modelos localizados/instanciados nos marcadores escolhidos pelo usuário. Confira a plataforma móvel do FBX e atribua-a no campo correspondente.");
    }

    private GameObject InstantiateAtMarker(string assetPath, string instanceName, Transform marker)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            Debug.LogError($"FBX não encontrado: {assetPath}", this);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, EditorSceneManager.GetActiveScene());
        if (instance == null)
        {
            Debug.LogError($"Não foi possível instanciar {assetPath}.", this);
            return null;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Instanciar modelo final Fase 4");
        instance.name = instanceName;
        Undo.SetTransformParent(instance.transform, arenaRoot, "Organizar modelo final Fase 4");
        MoveToMarker(instance.transform, marker);
        return instance;
    }

    private static void MoveToMarker(Transform target, Transform marker)
    {
        Undo.RecordObject(target, "Posicionar modelo no marcador da Fase 4");
        target.SetPositionAndRotation(marker.position, marker.rotation);
    }

    private void CreateTopMarker()
    {
        var markerObject = new GameObject("Elevador_Alvo_Superior");
        Undo.RegisterCreatedObjectUndo(markerObject, "Criar marcador superior do elevador");
        Undo.SetTransformParent(markerObject.transform, arenaRoot, "Organizar marcador do elevador");
        markerObject.transform.SetPositionAndRotation(
            elevatorPlacementMarker.position + Vector3.up * 6f,
            elevatorPlacementMarker.rotation);
        elevatorTopMarker = markerObject.transform;
        Selection.activeGameObject = markerObject;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private void ConfigureTriggers()
    {
        ConfigureAutomatically();
    }

    private GameObject CreateTriggerZone(string name, Transform anchor, Vector3 size, Vector3 center)
    {
        if (anchor == null)
        {
            EditorUtility.DisplayDialog("Referência ausente", "Atribua a plataforma móvel ou o modelo do portal antes de criar os triggers.", "OK");
            return null;
        }

        var zone = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(zone, "Criar trigger final Fase 4");
        Undo.SetTransformParent(zone.transform, arenaRoot, "Organizar trigger final Fase 4");
        zone.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        BoxCollider box = Undo.AddComponent<BoxCollider>(zone);
        box.isTrigger = true;
        box.size = size;
        box.center = center;
        Rigidbody body = Undo.AddComponent<Rigidbody>(zone);
        body.isKinematic = true;
        body.useGravity = false;
        return zone;
    }

    private bool EnsureMenuInBuildSettings()
    {
        string guid = AssetDatabase.AssetPathToGUID(MenuScenePath);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"Cena de menu não encontrada: {MenuScenePath}", this);
            return false;
        }

        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path != MenuScenePath)
                continue;
            if (!current[i].enabled)
            {
                current[i].enabled = true;
                EditorBuildSettings.scenes = current;
            }
            return IsMenuEnabledInBuildSettings();
        }

        Array.Resize(ref current, current.Length + 1);
        current[current.Length - 1] = new EditorBuildSettingsScene(MenuScenePath, true);
        EditorBuildSettings.scenes = current;
        Debug.Log($"Adicionada ao Build Settings: {MenuScenePath}");
        return IsMenuEnabledInBuildSettings();
    }

    private static bool IsMenuEnabledInBuildSettings()
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == MenuScenePath)
                return scene.enabled;
        }
        return false;
    }

    private static GameObject FindNamedChild(Transform root, string exactName)
    {
        if (root == null)
            return null;
        if (root.name == exactName)
            return root.gameObject;
        foreach (Transform child in root)
        {
            GameObject result = FindNamedChild(child, exactName);
            if (result != null)
                return result;
        }
        return null;
    }

    private static Transform FindInScene(Scene scene, string exactName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindNamedChild(root.transform, exactName)?.transform;
            if (found != null)
                return found;
        }
        return null;
    }

    private static Transform FindInSubtree(Transform root, string exactName)
    {
        return FindNamedChild(root, exactName)?.transform;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }

    private static string BuildMissingList(params (UnityEngine.Object value, string label)[] entries)
    {
        string result = string.Empty;
        foreach (var entry in entries)
        {
            if (entry.value == null)
                result += "• " + entry.label + "\n";
        }
        return result;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static Transform FindExistingModel(Transform root, string canonicalName, string legacyName)
    {
        Transform canonical = FindInSubtree(root, canonicalName);
        return canonical != null ? canonical : FindInSubtree(root, legacyName);
    }

    private static List<GameObject> FindAllInSubtree(Transform root, string exactName)
    {
        var matches = new List<GameObject>();
        if (root == null)
            return matches;
        if (root.name == exactName)
            matches.Add(root.gameObject);
        foreach (Transform child in root)
            matches.AddRange(FindAllInSubtree(child, exactName));
        return matches;
    }

    private static bool IsProvenGeneratedDuplicate(GameObject candidate, GameObject canonical, string protectedChildName, Scene scene)
    {
        if (candidate == null || canonical == null || candidate == canonical || candidate.scene != scene)
            return false;
        if (candidate.transform.localScale != canonical.transform.localScale)
            return false;
        if (protectedChildName != null && FindInSubtree(candidate.transform, protectedChildName) != null)
            return false;
        if (candidate.GetComponentInChildren<Fase4EmergencyElevator>(true) != null ||
            candidate.GetComponentInChildren<Fase4PortalCompletionTrigger>(true) != null)
            return false;
        foreach (Component component in candidate.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || component is MeshFilter || component is MeshRenderer ||
                component is SkinnedMeshRenderer || component is Collider)
                continue;
            return false;
        }
        if (candidate.GetComponentInChildren<Renderer>(true) == null)
            return false;
        if (candidate.GetComponentsInChildren<Collider>(true).Length > 0)
            return false;

        UnityEngine.Object candidateSource = PrefabUtility.GetCorrespondingObjectFromSource(candidate);
        UnityEngine.Object canonicalSource = PrefabUtility.GetCorrespondingObjectFromSource(canonical);
        bool sameSource = candidateSource != null && candidateSource == canonicalSource;
        return (sameSource || HasMatchingModelHierarchy(candidate.transform, canonical.transform, true)) &&
               !HasExternalSerializedReferences(candidate, scene);
    }

    private static bool HasMatchingModelHierarchy(Transform candidate, Transform canonical, bool isRoot)
    {
        if (!isRoot && candidate.name != canonical.name)
            return false;
        if (candidate.childCount != canonical.childCount)
            return false;

        MeshFilter candidateFilter = candidate.GetComponent<MeshFilter>();
        MeshFilter canonicalFilter = canonical.GetComponent<MeshFilter>();
        if ((candidateFilter == null) != (canonicalFilter == null) ||
            (candidateFilter != null && candidateFilter.sharedMesh != canonicalFilter.sharedMesh))
            return false;

        Renderer candidateRenderer = candidate.GetComponent<Renderer>();
        Renderer canonicalRenderer = canonical.GetComponent<Renderer>();
        if ((candidateRenderer == null) != (canonicalRenderer == null))
            return false;
        if (candidateRenderer != null)
        {
            if (candidateRenderer.GetType() != canonicalRenderer.GetType())
                return false;
            Material[] candidateMaterials = candidateRenderer.sharedMaterials;
            Material[] canonicalMaterials = canonicalRenderer.sharedMaterials;
            if (candidateMaterials.Length != canonicalMaterials.Length)
                return false;
            for (int i = 0; i < candidateMaterials.Length; i++)
            {
                if (candidateMaterials[i] != canonicalMaterials[i])
                    return false;
            }
        }

        for (int i = 0; i < candidate.childCount; i++)
        {
            Transform candidateChild = candidate.GetChild(i);
            Transform canonicalChild = canonical.GetChild(i);
            if (candidateChild.localPosition != canonicalChild.localPosition ||
                candidateChild.localRotation != canonicalChild.localRotation ||
                candidateChild.localScale != canonicalChild.localScale ||
                !HasMatchingModelHierarchy(candidateChild, canonicalChild, false))
                return false;
        }
        return true;
    }

    private static bool HasExternalSerializedReferences(GameObject candidate, Scene scene)
    {
        var candidateObjects = new System.Collections.Generic.HashSet<UnityEngine.Object>();
        foreach (Transform child in candidate.GetComponentsInChildren<Transform>(true))
        {
            candidateObjects.Add(child);
            candidateObjects.Add(child.gameObject);
            foreach (Component component in child.GetComponents<Component>())
            {
                if (component != null)
                    candidateObjects.Add(component);
            }
        }

        foreach (Component component in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (component == null || component.gameObject.scene != scene || candidateObjects.Contains(component))
                continue;

            try
            {
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue != null && candidateObjects.Contains(property.objectReferenceValue))
                        return true;
                }
            }
            catch (System.Exception)
            {
                // If a component cannot be inspected, do not claim the target is unreferenced.
                return true;
            }
        }
        return false;
    }

    private void ConfigureTriggerZone(GameObject zone, Transform anchor, Vector3 size, Vector3 center)
    {
        if (anchor == null)
            return;

        Undo.RecordObject(zone.transform, "Atualizar trigger da Fase 4");
        zone.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        BoxCollider box = zone.GetComponent<BoxCollider>();
        if (box == null)
            box = Undo.AddComponent<BoxCollider>(zone);
        Undo.RecordObject(box, "Configurar volume do trigger da Fase 4");
        box.isTrigger = true;
        box.size = size;
        box.center = center;
        Rigidbody body = zone.GetComponent<Rigidbody>();
        if (body == null)
            body = Undo.AddComponent<Rigidbody>(zone);
        Undo.RecordObject(body, "Configurar Rigidbody do trigger da Fase 4");
        body.isKinematic = true;
        body.useGravity = false;
    }

    private static void SetActiveWithUndo(GameObject target, bool active)
    {
        Undo.RecordObject(target, "Desativar duplicata de emergência da Fase 4");
        target.SetActive(active);
    }

    private static bool SetSerialized(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"Campo serializado não encontrado: {propertyName}", target);
            return false;
        }
        Undo.RecordObject(target, "Vincular componente final da Fase 4");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return property.objectReferenceValue == value;
    }
}
