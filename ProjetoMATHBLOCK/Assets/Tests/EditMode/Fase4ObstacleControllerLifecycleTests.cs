using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Fase4ObstacleControllerLifecycleTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private UnityEngine.Object expression;
    private readonly Type controllerType = Type.GetType("Fase4ArenaObstacleController, Assembly-CSharp");
    private readonly Type managerType = Type.GetType("Fase4RoundManager, Assembly-CSharp");

    [Test]
    public void DisabledInitializationDefersCollisionPolicyAndReenableDisableShutdownReconcilePairs()
    {
        var player = CreatePlayer("LifecycleTest_Player", out CharacterController characterController, out Component movement);
        GameObject obstacleRoot = CreateObstacleFixture("LifecycleTest_Arena_Obstaculos", out Collider[] movingColliders);
        Behaviour controller = (Behaviour)obstacleRoot.AddComponent(controllerType);
        controller.enabled = false;

        object rounds = CreateRoundManager();
        Assert.That(InvokeInitialize(controller, rounds, movement), Is.True);
        foreach (Collider movingCollider in movingColliders)
            Assert.That(Physics.GetIgnoreCollision(characterController, movingCollider), Is.False,
                "Initialize enquanto desabilitado não pode alterar os pares de colisão.");

        controller.enabled = true;
        Invoke(controller, "OnEnable", BindingFlags.NonPublic);
        AssertThatPairsIgnored(characterController, movingColliders, false,
            "Os pistões parados devem preservar colisão sólida fora de um ciclo ativo.");
        ActivatePistons(rounds);
        foreach (Collider movingCollider in movingColliders)
            Assert.That(Physics.GetIgnoreCollision(characterController, movingCollider), Is.True);

        controller.enabled = false;
        Invoke(controller, "OnDisable", BindingFlags.NonPublic);
        foreach (Collider movingCollider in movingColliders)
            Assert.That(Physics.GetIgnoreCollision(characterController, movingCollider), Is.False);

        controller.enabled = true;
        Invoke(controller, "OnEnable", BindingFlags.NonPublic);
        AssertThatPairsIgnored(characterController, movingColliders, true,
            "Ao reabilitar durante uma rodada com pistões ativos, o controlador deve reconciliar o estado lógico.");
        foreach (Collider movingCollider in movingColliders)
            Assert.That(Physics.GetIgnoreCollision(characterController, movingCollider), Is.True);

        Invoke(controller, "Shutdown");
        foreach (Collider movingCollider in movingColliders)
            Assert.That(Physics.GetIgnoreCollision(characterController, movingCollider), Is.False);
        Assert.That(player, Is.Not.Null);
    }

    [Test]
    public void DisabledInitializationPreservesPairsIgnoredBeforeM6()
    {
        CreatePlayer("LifecycleTest_PreIgnoredPlayer", out CharacterController characterController, out Component movement);
        GameObject obstacleRoot = CreateObstacleFixture("LifecycleTest_PreIgnored_Arena_Obstaculos", out Collider[] movingColliders);
        Physics.IgnoreCollision(characterController, movingColliders[0], true);

        Behaviour controller = (Behaviour)obstacleRoot.AddComponent(controllerType);
        controller.enabled = false;
        Assert.That(InvokeInitialize(controller, CreateRoundManager(), movement), Is.True);
        controller.enabled = true;
        Invoke(controller, "OnEnable", BindingFlags.NonPublic);
        controller.enabled = false;
        Invoke(controller, "OnDisable", BindingFlags.NonPublic);

        Assert.That(Physics.GetIgnoreCollision(characterController, movingColliders[0]), Is.True);
        Assert.That(Physics.GetIgnoreCollision(characterController, movingColliders[1]), Is.False);
        Invoke(controller, "Shutdown");
        Assert.That(Physics.GetIgnoreCollision(characterController, movingColliders[0]), Is.True);
        Assert.That(Physics.GetIgnoreCollision(characterController, movingColliders[1]), Is.False);
    }

    [Test]
    public void InitializationDuringActivePistonAttemptReconcilesLogicalActivation()
    {
        CreatePlayer("LifecycleActiveInit_Player", out CharacterController characterController, out Component movement);
        GameObject obstacleRoot = CreateObstacleFixture("LifecycleActiveInit_Arena_Obstaculos", out Collider[] movingColliders);
        object rounds = CreateRoundManager();
        ActivatePistons(rounds);

        Behaviour controller = (Behaviour)obstacleRoot.AddComponent(controllerType);
        controller.enabled = false;
        Assert.That(InvokeInitialize(controller, rounds, movement), Is.True);
        Assert.That(controllerType.GetProperty("ArePistonsRepeating").GetValue(controller), Is.False,
            "Initialize em componente desabilitado não deve iniciar a agenda dos pistões.");
        AssertThatPairsIgnored(characterController, movingColliders, false,
            "Initialize desabilitado com a tentativa logicamente ativa não deve aplicar IgnoreCollision.");

        controller.enabled = true;
        Invoke(controller, "OnEnable", BindingFlags.NonPublic);
        Assert.That(controllerType.GetProperty("ArePistonsRepeating").GetValue(controller), Is.True,
            "OnEnable deve reconciliar a ativação lógica existente no Fase4RoundManager.");
        AssertThatPairsIgnored(characterController, movingColliders, true,
            "A política deve ser aplicada enquanto os pistões já estão ativos.");

        Invoke(controller, "Shutdown");
    }

    [Test]
    public void DisableDuringExtensionHomesPistonsAndReenableAllowsANewCycle()
    {
        CreatePlayer("LifecycleExtensionTest_Player", out CharacterController characterController, out Component movement);
        GameObject obstacleRoot = CreateObstacleFixture("LifecycleExtensionTest_Arena_Obstaculos", out Collider[] movingColliders);
        Behaviour controller = (Behaviour)obstacleRoot.AddComponent(controllerType);
        object rounds = CreateRoundManager();
        Assert.That(InvokeInitialize(controller, rounds, movement), Is.True);

        ActivatePistons(rounds);
        AdvancePistonSchedule(controller, 2.5f);

        Transform rod = obstacleRoot.transform.Find("PistaoHorizontal_01/Pistao_Haste");
        Assert.That(rod.localPosition, Is.Not.EqualTo(Vector3.zero),
            "O fixture confirma que o controlador está no meio de uma extensão antes de desabilitar.");

        controller.enabled = false;
        Invoke(controller, "OnDisable", BindingFlags.NonPublic);
        Assert.That(rod.localPosition, Is.EqualTo(Vector3.zero), "OnDisable deve recolher a haste.");
        foreach (Collider movingCollider in movingColliders)
            Assert.That(Physics.GetIgnoreCollision(characterController, movingCollider), Is.False);

        controller.enabled = true;
        Invoke(controller, "OnEnable", BindingFlags.NonPublic);
        AdvancePistonSchedule(controller, 2.5f);
        Assert.That(rod.localPosition, Is.Not.EqualTo(Vector3.zero),
            "Após reabilitar, um novo ciclo deve iniciar sem herdar a extensão interrompida.");

        Invoke(controller, "Shutdown");
        Assert.That(rod.localPosition, Is.EqualTo(Vector3.zero));
        foreach (Collider movingCollider in movingColliders)
            Assert.That(Physics.GetIgnoreCollision(characterController, movingCollider), Is.False);
    }

    private Component CreatePlayer(string name, out CharacterController characterController, out Component movement)
    {
        GameObject player = CreateObject(name);
        characterController = player.AddComponent<CharacterController>();
        Type movementType = Type.GetType("PlayerMovement, Assembly-CSharp");
        movement = player.AddComponent(movementType);
        movementType.GetField("controller").SetValue(movement, characterController);
        return movement;
    }

    private GameObject CreateObstacleFixture(string name, out Collider[] movingColliders)
    {
        GameObject obstacleRoot = CreateObject(name);
        GameObject bar = CreateChild("BarraGiratoria", obstacleRoot.transform);
        GameObject shaft = CreateChild("BarraGiratoria_HasteMetalica", bar.transform);
        shaft.AddComponent<BoxCollider>();

        var colliders = new List<Collider>();
        for (int index = 1; index <= 3; index++)
        {
            GameObject piston = CreateChild($"PistaoHorizontal_{index:00}", obstacleRoot.transform);
            colliders.Add(CreateChild("Pistao_Haste", piston.transform).AddComponent<BoxCollider>());
            colliders.Add(CreateChild("Pistao_CabecaImpacto", piston.transform).AddComponent<BoxCollider>());
        }
        movingColliders = colliders.ToArray();
        return obstacleRoot;
    }

    private object CreateRoundManager()
    {
        Type definitionType = Type.GetType("Fase4ExpressionDefinition, Assembly-CSharp");
        expression = ScriptableObject.CreateInstance(definitionType);
        Type operationType = Type.GetType("Fase4ArithmeticOperation, Assembly-CSharp");
        Array operations = Array.CreateInstance(operationType, 3);
        object add = Enum.Parse(operationType, "Add");
        for (int index = 0; index < operations.Length; index++) operations.SetValue(add, index);
        Type pairType = definitionType.GetNestedType("ParenthesisPair", BindingFlags.Public | BindingFlags.NonPublic);
        Array pairs = Array.CreateInstance(pairType, 1);
        pairs.SetValue(Activator.CreateInstance(pairType, 0, 1), 0);
        definitionType.GetMethod("Configure").Invoke(expression,
            new object[] { "lifecycle", new[] { 1, 2, 3, 4 }, operations, pairs });

        Type deckType = Type.GetType("Fase4ExpressionDeck, Assembly-CSharp");
        Array expressions = Array.CreateInstance(definitionType, 1);
        expressions.SetValue(expression, 0);
        object deck = Activator.CreateInstance(deckType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { expressions, new System.Random(1) }, null);
        return Activator.CreateInstance(managerType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new[] { deck }, null);
    }

    private static bool InvokeInitialize(Component controller, object rounds, Component movement)
    {
        MethodInfo initialize = controller.GetType().GetMethod("Initialize");
        return (bool)initialize.Invoke(controller, new[] { rounds, (object)movement });
    }

    private static void AdvancePistonSchedule(Component controller, float deltaTime)
    {
        controller.GetType().GetMethod("AdvancePistonSchedule", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(controller, new object[] { deltaTime });
    }

    private static void ActivatePistons(object rounds)
    {
        rounds.GetType().GetMethod("RequestAttempt").Invoke(rounds, null);
        rounds.GetType().GetMethod("NotifySpawnCompleted").Invoke(rounds, null);
        MethodInfo submit = rounds.GetType().GetMethod("SubmitAnswer", new[] { typeof(bool) });
        submit.Invoke(rounds, new object[] { false });
        submit.Invoke(rounds, new object[] { false });
    }

    private static void AssertThatPairsIgnored(CharacterController characterController, Collider[] movingColliders,
        bool expected, string message)
    {
        foreach (Collider movingCollider in movingColliders)
            Assert.That(Physics.GetIgnoreCollision(characterController, movingCollider), Is.EqualTo(expected), message);
    }

    private static void Invoke(Component controller, string methodName, BindingFlags visibility = BindingFlags.Public)
    {
        controller.GetType().GetMethod(methodName, visibility | BindingFlags.Instance).Invoke(controller, null);
    }

    private GameObject CreateChild(string name, Transform parent)
    {
        GameObject child = CreateObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private GameObject CreateObject(string name)
    {
        var instance = new GameObject(name);
        createdObjects.Add(instance);
        return instance;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject instance in createdObjects)
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        createdObjects.Clear();
        if (expression != null) UnityEngine.Object.DestroyImmediate(expression);
    }
}
