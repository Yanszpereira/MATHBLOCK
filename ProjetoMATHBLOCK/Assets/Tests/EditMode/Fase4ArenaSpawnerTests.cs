using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Fase4ArenaSpawnerTests
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

    [Test]
    public void Scene_HasSixRegionsAndEightUniqueWalkableSpawnPointsPerRegion()
    {
        const string scenePath = "Assets/Scenes/Fase 4.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        GameObject blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/MathBlock.prefab");
        BoxCollider blockCollider = blockPrefab != null ? blockPrefab.GetComponent<BoxCollider>() : null;
        Assert.That(blockCollider, Is.Not.Null, "Prefab MathBlock sem BoxCollider.");
        float blockHalfHeight = blockCollider.size.y * blockPrefab.transform.lossyScale.y * 0.5f;
        float blockWidth = blockCollider.size.x * blockPrefab.transform.lossyScale.x;
        float blockDepth = blockCollider.size.z * blockPrefab.transform.lossyScale.z;
        float conservativeSeparation = Mathf.Sqrt(blockWidth * blockWidth + blockDepth * blockDepth);

        try
        {
            Transform spawners = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Fase4_SegundaMetade")
                {
                    spawners = FindDescendant(root.transform, "Arena_Spawners");
                    break;
                }
            }

            Assert.That(spawners, Is.Not.Null, "Arena_Spawners ausente na cena Fase 4.");
            Component[] regions = spawners.GetComponentsInChildren(Type("Fase4SpawnRegion"), true);
            Assert.That(regions, Has.Length.EqualTo(6));

            foreach (Component region in regions)
            {
                Transform[] points = (Transform[])GetProperty(region, "SpawnPoints");
                Assert.That(points, Has.Length.EqualTo(8), region.name);
                foreach (Transform point in points)
                {
                    Assert.That(point, Is.Not.Null, region.name);
                    Assert.That(point.position.y, Is.GreaterThan(0f), point.name);
                    RaycastHit[] hits = Physics.RaycastAll(
                        point.position + Vector3.up * 10f,
                        Vector3.down,
                        20f,
                        ~0,
                        QueryTriggerInteraction.Ignore);
                    bool foundArenaFloor = false;
                    float nearestFloorDistance = float.PositiveInfinity;
                    RaycastHit nearestFloor = default;
                    foreach (RaycastHit hit in hits)
                    {
                        if (hit.collider == null ||
                            (!hit.collider.name.StartsWith("Arena_AnelInterno_Setor_") &&
                             !hit.collider.name.StartsWith("Arena_AnelExterno_Setor_")) ||
                            hit.distance >= nearestFloorDistance)
                            continue;

                        nearestFloor = hit;
                        nearestFloorDistance = hit.distance;
                        foundArenaFloor = true;
                    }

                    Assert.That(foundArenaFloor, Is.True, point.name + " sem piso de arena sob o ponto.");
                    Assert.That(
                        point.position.y,
                        Is.EqualTo(nearestFloor.point.y + blockHalfHeight).Within(0.01f),
                        point.name + " não considera a meia-extensão real do collider.");

                    Collider[] overlaps = Physics.OverlapBox(
                        point.position,
                        new Vector3(blockWidth * 0.5f, blockHalfHeight, blockDepth * 0.5f),
                        point.rotation,
                        ~0,
                        QueryTriggerInteraction.Ignore);
                    foreach (Collider overlap in overlaps)
                    {
                        if (overlap == null ||
                            overlap.name.StartsWith("Arena_AnelInterno_Setor_") ||
                            overlap.name.StartsWith("Arena_AnelExterno_Setor_"))
                            continue;

                        Assert.Fail(point.name + " sobrepõe collider não pertencente ao piso: " + overlap.name);
                    }
                }

                for (int i = 0; i < points.Length; i++)
                    for (int j = i + 1; j < points.Length; j++)
                        Assert.That(
                            Vector3.Distance(points[i].position, points[j].position),
                            Is.GreaterThan(conservativeSeparation),
                            region.name + " possui pontos próximos demais para o collider do MathBlock.");
            }
        }
        finally
        {
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void RegionSelection_UsesAllSixAndNeverRepeatsImmediately()
    {
        MethodInfo choose = Type("Fase4SpawnSelection").GetMethod("ChooseRegionIndex", PublicStatic);
        Assert.That(choose, Is.Not.Null);
        var firstSelectionRandom = new System.Random(74021);
        var firstSelections = new HashSet<int>();
        for (int i = 0; i < 500; i++)
            firstSelections.Add((int)choose.Invoke(null, new object[] { 6, -1, firstSelectionRandom }));
        Assert.That(firstSelections, Has.Count.EqualTo(6), "O primeiro sorteio precisa alcançar todas as seis regiões.");

        var random = new System.Random(74022);
        int previous = -1;
        var seen = new HashSet<int>();

        for (int i = 0; i < 500; i++)
        {
            int selected = (int)choose.Invoke(null, new object[] { 6, previous, random });
            Assert.That(selected, Is.InRange(0, 5));
            if (previous >= 0) Assert.That(selected, Is.Not.EqualTo(previous));
            seen.Add(selected);
            previous = selected;
        }

        Assert.That(seen, Has.Count.EqualTo(6));
    }

    [Test]
    public void ValuePool_ContainsAllFourOperandsAndTwoOrThreeUniqueDistractors()
    {
        UnityEngine.Object bank = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Data/Fase4/Fase4ExpressionBank.asset");
        Assert.That(bank, Is.Not.Null, "Banco único do M3 ausente.");
        IList expressions = (IList)GetProperty(bank, "Expressions");
        MethodInfo build = Type("Fase4SpawnSelection").GetMethod("BuildValues", PublicStatic);
        Assert.That(expressions, Has.Count.EqualTo(20));

        foreach (object expression in expressions)
        {
            string expressionId = (string)GetProperty(expression, "ExpressionId");
            int[] operands = (int[])GetProperty(expression, "Operands");
            object[] args = { expression, new System.Random(expressionId.GetHashCode()), null };
            int[] values = (int[])build.Invoke(null, args);
            int[] distractors = (int[])args[2];
            Assert.That(distractors.Length, Is.InRange(2, 3), expressionId);
            Assert.That(values.Length, Is.EqualTo(4 + distractors.Length), expressionId);
            CollectionAssert.AreEquivalent(operands, ValuesWithoutDistractors(values, distractors));
            Assert.That(new HashSet<int>(distractors).Count, Is.EqualTo(distractors.Length));
            foreach (int distractor in distractors)
            {
                Assert.That(distractor, Is.InRange(1, 50));
                Assert.That(Array.IndexOf(operands, distractor), Is.EqualTo(-1));
            }
        }
    }

    [Test]
    public void ArenaPrefab_UsesInteractablePhysicsAndExistingSpawnAnimation()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/MathBlock.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent(Type("MathBlockValue")), Is.Not.Null);
        Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null);
        Assert.That(Type("BlockSpawnPop").GetMethod("Play", PublicStatic), Is.Not.Null);
        Assert.That(Type("GravityInteract").GetProperty("GrabbedObject"), Is.Not.Null);
    }

    private static int[] ValuesWithoutDistractors(int[] values, int[] distractors)
    {
        var remaining = new List<int>(values);
        foreach (int distractor in distractors)
            Assert.That(remaining.Remove(distractor), Is.True, "Distrator ausente do conjunto instanciável.");
        return remaining.ToArray();
    }

    private static Type Type(string name)
    {
        Type type = System.Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, "Tipo ausente: " + name);
        return type;
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDescendant(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    private static object GetProperty(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, "Propriedade ausente: " + name);
        return property.GetValue(target);
    }
}
