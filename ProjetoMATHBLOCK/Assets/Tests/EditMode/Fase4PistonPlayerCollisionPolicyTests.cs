using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Fase4PistonPlayerCollisionPolicyTests
{
    private GameObject playerObject;
    private GameObject pistonObject;
    private GameObject replacementPistonObject;
    private GameObject worldObject;
    private object policy;
    private Type policyType;

    [SetUp]
    public void SetUp()
    {
        policyType = Type.GetType("Fase4PistonPlayerCollisionPolicy, Assembly-CSharp");
        Assert.That(policyType, Is.Not.Null, "A política localizada de colisão dos pistões deve existir.");
        playerObject = new GameObject("Fase4_CollisionPolicy_TestPlayer");
        playerObject.AddComponent<CharacterController>();
        playerObject.AddComponent<CapsuleCollider>();
        pistonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pistonObject.name = "Fase4_CollisionPolicy_TestPiston";
        pistonObject.transform.position = new Vector3(0.65f, 0f, 0f);
        Mesh mesh = pistonObject.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(pistonObject.GetComponent<BoxCollider>());
        MeshCollider pistonCollider = pistonObject.AddComponent<MeshCollider>();
        pistonCollider.sharedMesh = mesh;
        worldObject = new GameObject("Fase4_CollisionPolicy_TestWorld");
        worldObject.transform.position = new Vector3(4f, 0f, 0f);
        worldObject.AddComponent<BoxCollider>();
        policy = Activator.CreateInstance(policyType, true);
    }

    [Test]
    public void PairwiseIgnorePreventsPlayerResponseButOverlapQueryStillFindsPiston()
    {
        var playerColliders = playerObject.GetComponents<Collider>();
        var pistonColliders = pistonObject.GetComponents<Collider>();
        Invoke("Apply", playerColliders, pistonColliders);

        Assert.That(Physics.GetIgnoreCollision(playerObject.GetComponent<CharacterController>(), pistonColliders[0]), Is.True);
        Assert.That(Physics.GetIgnoreCollision(playerObject.GetComponent<CapsuleCollider>(), pistonColliders[0]), Is.True);
        Assert.That(Physics.GetIgnoreCollision(playerObject.GetComponent<CharacterController>(), worldObject.GetComponent<Collider>()), Is.False,
            "A política não deve alterar a colisão do jogador com a geometria normal do cenário.");

        Physics.SyncTransforms();
        Collider[] overlaps = Physics.OverlapCapsule(
            new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.5f,
            1 << pistonObject.layer, QueryTriggerInteraction.Ignore);

        Assert.That(Array.Exists(overlaps, collider => collider == pistonColliders[0]), Is.True,
            "A consulta de contato da cápsula deve continuar detectando o pistão mesmo com a resposta física ignorada.");
        Assert.That(Physics.GetIgnoreCollision(playerColliders[0], pistonColliders[0]), Is.True);
    }

    [Test]
    public void RestorePreservesCollisionPairsThatWereAlreadyIgnored()
    {
        Collider characterController = playerObject.GetComponent<CharacterController>();
        Collider playerCapsule = playerObject.GetComponent<CapsuleCollider>();
        Collider pistonCollider = pistonObject.GetComponent<Collider>();
        Physics.IgnoreCollision(characterController, pistonCollider, true);

        Invoke("Apply", playerObject.GetComponents<Collider>(), pistonObject.GetComponents<Collider>());
        Invoke("Restore");

        Assert.That(Physics.GetIgnoreCollision(characterController, pistonCollider), Is.True);
        Assert.That(Physics.GetIgnoreCollision(playerCapsule, pistonCollider), Is.False);
    }

    [Test]
    public void ApplyAfterReinitializationTracksNewPistonPairsAndPreservesOriginalSnapshots()
    {
        Collider characterController = playerObject.GetComponent<CharacterController>();
        Collider firstPiston = pistonObject.GetComponent<Collider>();
        Physics.IgnoreCollision(characterController, firstPiston, true);

        Invoke("Apply", playerObject.GetComponents<Collider>(), pistonObject.GetComponents<Collider>());

        replacementPistonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        replacementPistonObject.name = "Fase4_CollisionPolicy_ReplacementPiston";
        Collider replacementPiston = replacementPistonObject.GetComponent<Collider>();
        Invoke("Apply", playerObject.GetComponents<Collider>(), new[] { replacementPiston });

        Assert.That(Physics.GetIgnoreCollision(characterController, firstPiston), Is.True);
        Assert.That(Physics.GetIgnoreCollision(characterController, replacementPiston), Is.True,
            "A Apply subsequente deve incluir os novos colliders sem abandonar pares pendentes.");

        Invoke("Restore");
        Assert.That(Physics.GetIgnoreCollision(characterController, firstPiston), Is.True,
            "O estado original do par antigo deve continuar preservado.");
        Assert.That(Physics.GetIgnoreCollision(characterController, replacementPiston), Is.False,
            "O par recém-adicionado deve ser restaurado ao estado anterior à nova aplicação.");
    }

    [Test]
    public void SweptQueryFindsMeshColliderCrossingTheCapsuleDuringOneLongFrame()
    {
        pistonObject.transform.position = new Vector3(-2f, 0f, 0f);
        Physics.SyncTransforms();

        Invoke("Apply", playerObject.GetComponents<Collider>(), pistonObject.GetComponents<Collider>());
        bool contact = TrySweptContact(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.5f,
            pistonObject.GetComponent<Collider>(), new Vector3(-4f, 0f, 0f));

        Assert.That(contact, Is.True,
            "A forma real do MeshCollider deve ser detectada no percurso mesmo quando a pose final já passou da cápsula.");
    }

    [Test]
    public void SweptQueryDoesNotReportGeometryOutsideTheActualPath()
    {
        pistonObject.transform.position = new Vector3(-2f, 0f, 3f);
        Physics.SyncTransforms();

        bool contact = TrySweptContact(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.5f,
            pistonObject.GetComponent<Collider>(), new Vector3(-4f, 0f, 0f));

        Assert.That(contact, Is.False);
    }

    [Test]
    public void ZeroTranslationStillDetectsAnExistingStaticOverlap()
    {
        pistonObject.transform.position = new Vector3(0.65f, 0f, 0f);
        Physics.SyncTransforms();
        bool contact = TrySweptContact(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.5f,
            pistonObject.GetComponent<Collider>(), Vector3.zero);

        Assert.That(contact, Is.True, "Deslocamento nulo não deve suprimir contato estático existente.");
    }

    [TestCase(float.NaN, 0f, 1f)]
    [TestCase(float.PositiveInfinity, 0f, 1f)]
    [TestCase(float.NegativeInfinity, 0f, 1f)]
    public void SweptQueryRejectsInvalidTranslationWithoutContact(float x, float y, float z)
    {
        pistonObject.transform.position = new Vector3(0f, 0f, 3f);
        Physics.SyncTransforms();
        bool contact = TrySweptContact(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f), 0.5f,
            pistonObject.GetComponent<Collider>(), new Vector3(x, y, z));

        Assert.That(contact, Is.False);
    }

    [Test]
    public void BarSkinMarginDoesNotChangePistonDetectionRadius()
    {
        Type detectorType = Type.GetType("Fase4PistonSweepDetector, Assembly-CSharp");
        MethodInfo barRadius = detectorType.GetMethod("GetBarDetectionRadius", BindingFlags.Public | BindingFlags.Static);
        MethodInfo pistonRadius = detectorType.GetMethod("GetPistonDetectionRadius", BindingFlags.Public | BindingFlags.Static);

        float barWithOriginalSkin = (float)barRadius.Invoke(null, new object[] { 0.5f, 0.08f, 1f });
        float barWithLargerSkin = (float)barRadius.Invoke(null, new object[] { 0.5f, 0.2f, 1f });
        float pistonRadiusA = (float)pistonRadius.Invoke(null, new object[] { 0.5f, 2f, 0f });
        float pistonRadiusB = (float)pistonRadius.Invoke(null, new object[] { 0.5f, 0.5f, 0f });

        Assert.That(barWithOriginalSkin, Is.EqualTo(0.58f).Within(0.0001f));
        Assert.That(barWithLargerSkin, Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(pistonRadiusA, Is.EqualTo(1f).Within(0.0001f), "O raio mundial deve aplicar a escala horizontal uma única vez.");
        Assert.That(pistonRadiusB, Is.EqualTo(0.25f).Within(0.0001f));
    }

    private bool TrySweptContact(Vector3 pointA, Vector3 pointB, float radius, Collider target, Vector3 relativeTranslation)
    {
        Type detectorType = Type.GetType("Fase4PistonSweepDetector, Assembly-CSharp");
        MethodInfo method = detectorType.GetMethod("TryGetContact", BindingFlags.Public | BindingFlags.Static);
        return (bool)method.Invoke(null, new object[]
        {
            pointA, pointB, radius, target, relativeTranslation, new Collider[32], new RaycastHit[32]
        });
    }

    private void Invoke(string methodName, params object[] arguments)
    {
        MethodInfo method = policyType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "Método esperado ausente: " + methodName);
        method.Invoke(policy, arguments);
    }

    [TearDown]
    public void TearDown()
    {
        InvokeIfPresent("Restore");
        if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
        if (pistonObject != null) UnityEngine.Object.DestroyImmediate(pistonObject);
        if (replacementPistonObject != null) UnityEngine.Object.DestroyImmediate(replacementPistonObject);
        if (worldObject != null) UnityEngine.Object.DestroyImmediate(worldObject);
    }

    private void InvokeIfPresent(string methodName)
    {
        if (policyType == null || policy == null)
            return;
        MethodInfo method = policyType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        method?.Invoke(policy, null);
    }
}
