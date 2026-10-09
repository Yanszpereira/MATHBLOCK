using System.Collections;
using System.Reflection;
using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class Fase4ObstacleImpactPlayModeTests
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    private GameObject playerObject;
    private GameObject pistonObject;
    private GameObject obstacleRoot;
    private UnityEngine.Object expressionAsset;

    [UnityTest]
    public IEnumerator ImpactReceiverMovesCharacterControllerWithoutAccessingHeldBlockState()
    {
        Type receiverType = Type.GetType("Fase4ObstaclePushReceiver, Assembly-CSharp");
        Assert.That(receiverType, Is.Not.Null, "O adaptador de empurrão da Fase 4 deve existir.");

        playerObject = new GameObject("Fase4_ImpactReceiver_TestPlayer");
        CharacterController controller = playerObject.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        Component receiver = playerObject.AddComponent(receiverType);
        MethodInfo apply = receiverType.GetMethod("ApplyImpact", PublicInstance);
        Assert.That(apply, Is.Not.Null);

        Vector3 initialPosition = playerObject.transform.position;
        apply.Invoke(receiver, new object[] { Vector3.right, 5f, 0.3f });
        for (int frame = 0; frame < 30; frame++)
            yield return null;

        Assert.That(playerObject.transform.position.x, Is.GreaterThan(initialPosition.x + 0.1f));
        Assert.That(controller.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator ImpulseDistanceDoesNotUseInitialSpeedForAnEntireLongFrame()
    {
        Type receiverType = Type.GetType("Fase4ObstaclePushReceiver, Assembly-CSharp");
        Assert.That(receiverType, Is.Not.Null);
        playerObject = new UnityEngine.GameObject("Fase4_LongFrameImpulse_TestPlayer");
        CharacterController controller = playerObject.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        Component receiver = playerObject.AddComponent(receiverType);
        MethodInfo apply = receiverType.GetMethod("ApplyImpact", PublicInstance);
        MethodInfo advance = receiverType.GetMethod("AdvanceImpact", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(advance, Is.Not.Null);

        apply.Invoke(receiver, new object[] { Vector3.right, 5.5f, 0.32f });
        advance.Invoke(receiver, new object[] { 0.333f });
        float longFrameDistance = playerObject.transform.position.x;

        playerObject.transform.position = Vector3.zero;
        receiverType.GetField("impactVelocity", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(receiver, Vector3.zero);
        receiverType.GetField("remainingDuration", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(receiver, 0f);
        yield return null;

        apply.Invoke(receiver, new object[] { Vector3.right, 5.5f, 0.32f });
        for (int i = 0; i < 32; i++)
            advance.Invoke(receiver, new object[] { 0.01f });
        float smallFrameDistance = playerObject.transform.position.x;

        Assert.That(longFrameDistance, Is.EqualTo(0.88f).Within(0.01f));
        Assert.That(smallFrameDistance, Is.EqualTo(longFrameDistance).Within(0.01f));
        Assert.That(controller.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator SweptMeshContactWithIgnoredPairTriggersControlledReceiverMotion()
    {
        Type detectorType = Type.GetType("Fase4PistonSweepDetector, Assembly-CSharp");
        Assert.That(detectorType, Is.Not.Null);
        MethodInfo sweep = detectorType.GetMethod("TryGetContact", BindingFlags.Public | BindingFlags.Static);
        Assert.That(sweep, Is.Not.Null);

        playerObject = new GameObject("Fase4_SweptContact_TestPlayer");
        CharacterController controller = playerObject.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        pistonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pistonObject.name = "Fase4_SweptContact_TestPistonMesh";
        pistonObject.transform.position = new Vector3(2f, 1f, 0f);
        Mesh mesh = pistonObject.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.Destroy(pistonObject.GetComponent<BoxCollider>());
        MeshCollider meshCollider = pistonObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        Physics.SyncTransforms();

        pistonObject.transform.position = new Vector3(-2f, 1f, 0f);
        Physics.SyncTransforms();
        Physics.IgnoreCollision(controller, meshCollider, true);

        Vector3 pointA = new Vector3(0f, 0.5f, 0f);
        Vector3 pointB = new Vector3(0f, 1.5f, 0f);
        bool detected = (bool)sweep.Invoke(null, new object[]
        {
            pointA, pointB, 0.5f, meshCollider, new Vector3(-4f, 0f, 0f),
            new Collider[16], new RaycastHit[16]
        });

        Assert.That(detected, Is.True, "A varredura deve detectar o MeshCollider durante a travessia mesmo com resposta sólida ignorada.");
        Assert.That(Physics.GetIgnoreCollision(controller, meshCollider), Is.True);

        Component receiver = playerObject.AddComponent(Type.GetType("Fase4ObstaclePushReceiver, Assembly-CSharp"));
        MethodInfo apply = receiver.GetType().GetMethod("ApplyImpact", PublicInstance);
        apply.Invoke(receiver, new object[] { Vector3.right, 4f, 0.35f });
        for (int frame = 0; frame < 30; frame++)
            yield return null;

        Assert.That(playerObject.transform.position.x, Is.GreaterThan(0.25f),
            "Após a detecção da travessia, o receiver deve mover o CharacterController.");
        Assert.That(controller.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator ControllerDetectsPistonCrossingDuringLongFrameAndAppliesOnlyOneImpact()
    {
        Type playerMovementType = Type.GetType("PlayerMovement, Assembly-CSharp");
        Type controllerType = Type.GetType("Fase4ArenaObstacleController, Assembly-CSharp");
        Type receiverType = Type.GetType("Fase4ObstaclePushReceiver, Assembly-CSharp");
        Assert.That(playerMovementType, Is.Not.Null);
        Assert.That(controllerType, Is.Not.Null);
        Assert.That(receiverType, Is.Not.Null);

        playerObject = new GameObject("Fase4_ControllerSweep_TestPlayer");
        playerObject.transform.position = new Vector3(0f, 1f, 0f);
        CharacterController characterController = playerObject.AddComponent<CharacterController>();
        characterController.height = 2f;
        characterController.radius = 0.5f;
        Behaviour movement = (Behaviour)playerObject.AddComponent(playerMovementType);
        playerMovementType.GetField("controller").SetValue(movement, characterController);
        movement.enabled = false;

        obstacleRoot = new GameObject("Fase4_ControllerSweep_TestArena_Obstaculos");
        GameObject bar = Child("BarraGiratoria", obstacleRoot.transform);
        GameObject shaft = Child("BarraGiratoria_HasteMetalica", bar.transform);
        shaft.AddComponent<BoxCollider>();

        for (int index = 1; index <= 3; index++)
        {
            GameObject pistonRoot = Child($"PistaoHorizontal_{index:00}", obstacleRoot.transform);
            if (index == 1)
                pistonRoot.transform.position = new Vector3(2f, 1f, 0f);
            else
                pistonRoot.transform.position = new Vector3(index == 2 ? 40f : -40f, 1f, 20f);

            CreateMovingMeshCollider("Pistao_Haste", pistonRoot.transform);
            CreateMovingMeshCollider("Pistao_CabecaImpacto", pistonRoot.transform);
        }

        Component obstacleController = obstacleRoot.AddComponent(controllerType);
        object rounds = CreateRoundManager();
        bool initialized = (bool)controllerType.GetMethod("Initialize").Invoke(obstacleController,
            new object[] { rounds, movement });
        Assert.That(initialized, Is.True, "O controlador deve inicializar com o fixture de obstáculo real.");

        ActivatePistons(rounds);
        Physics.SyncTransforms();
        InvokePrivate(obstacleController, "AdvancePistonSchedule", 2.5f);

        Assert.That(controllerType.GetField("pistonHitThisCycle", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(obstacleController), Is.True,
            "A travessia da cabeça pelo volume do CharacterController deve ser detectada pelo próprio controlador.");

        Component receiver = playerObject.GetComponent(receiverType);
        Assert.That(receiver, Is.Not.Null);
        FieldInfo velocityField = receiverType.GetField("impactVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 queuedVelocity = (Vector3)velocityField.GetValue(receiver);
        Assert.That(queuedVelocity.magnitude, Is.EqualTo(4f).Within(0.01f),
            "Um único ataque deve registrar somente um empurrão, mesmo com haste e cabeça sobrepostas.");

        for (int frame = 0; frame < 30; frame++)
            yield return null;

        Assert.That(playerObject.transform.position.x, Is.LessThan(-0.2f),
            "O receiver deve mover o CharacterController após a detecção física varrida.");
        Assert.That(characterController.enabled, Is.True);
        InvokePrivate(obstacleController, "Shutdown");
    }

    private GameObject Child(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void CreateMovingMeshCollider(string name, Transform parent)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
        part.GetComponent<BoxCollider>().enabled = false;
        MeshCollider collider = part.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    private object CreateRoundManager()
    {
        Type definitionType = Type.GetType("Fase4ExpressionDefinition, Assembly-CSharp");
        expressionAsset = ScriptableObject.CreateInstance(definitionType);
        Type operationType = Type.GetType("Fase4ArithmeticOperation, Assembly-CSharp");
        Array operations = Array.CreateInstance(operationType, 3);
        object add = Enum.Parse(operationType, "Add");
        for (int index = 0; index < operations.Length; index++) operations.SetValue(add, index);
        Type pairType = definitionType.GetNestedType("ParenthesisPair", BindingFlags.Public | BindingFlags.NonPublic);
        Array pairs = Array.CreateInstance(pairType, 1);
        pairs.SetValue(Activator.CreateInstance(pairType, 0, 1), 0);
        definitionType.GetMethod("Configure").Invoke(expressionAsset,
            new object[] { "playmode-sweep", new[] { 1, 2, 3, 4 }, operations, pairs });

        Array expressions = Array.CreateInstance(definitionType, 1);
        expressions.SetValue(expressionAsset, 0);
        Type deckType = Type.GetType("Fase4ExpressionDeck, Assembly-CSharp");
        object deck = Activator.CreateInstance(deckType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { expressions, new System.Random(1) }, null);
        Type managerType = Type.GetType("Fase4RoundManager, Assembly-CSharp");
        return Activator.CreateInstance(managerType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new[] { deck }, null);
    }

    private static void ActivatePistons(object rounds)
    {
        rounds.GetType().GetMethod("RequestAttempt").Invoke(rounds, null);
        rounds.GetType().GetMethod("NotifySpawnCompleted").Invoke(rounds, null);
        MethodInfo submit = rounds.GetType().GetMethod("SubmitAnswer", new[] { typeof(bool) });
        submit.Invoke(rounds, new object[] { false });
        submit.Invoke(rounds, new object[] { false });
    }

    private static void InvokePrivate(Component instance, string methodName, params object[] args)
    {
        instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, args);
    }

    [TearDown]
    public void TearDown()
    {
        if (playerObject != null)
            UnityEngine.Object.Destroy(playerObject);
        if (pistonObject != null)
            UnityEngine.Object.Destroy(pistonObject);
        if (obstacleRoot != null)
            UnityEngine.Object.Destroy(obstacleRoot);
        if (expressionAsset != null)
            UnityEngine.Object.Destroy(expressionAsset);
    }
}
