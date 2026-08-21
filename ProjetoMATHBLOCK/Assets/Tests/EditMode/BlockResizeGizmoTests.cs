using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BlockResizeGizmoTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public;
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null) UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
    }

    [Test]
    public void Prefab_HasExactlyFourHandlesAndNoCenter()
    {
        Type assetDatabase = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
        MethodInfo loadAsset = assetDatabase.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) });
        GameObject prefab = (GameObject)loadAsset.Invoke(null, new object[] { "Assets/Prefab/BlockResizeGizmo.prefab", typeof(GameObject) });
        Assert.That(prefab, Is.Not.Null);

        Type handleType = RequireType("BlockResizeHandle");
        Component[] handles = prefab.GetComponentsInChildren(handleType, true);
        Assert.That(handles, Has.Length.EqualTo(4));
        foreach (Component handle in handles)
            Assert.That(GetProperty(handle, "Position").ToString(), Is.Not.EqualTo("Center"));

        Assert.That(Enum.GetNames(RequireType("ResizeHandlePosition")), Does.Not.Contain("Center"));
        Assert.That(RequireType("BlockResizeGizmo").GetField("centerHandle", InstanceFlags), Is.Null);
    }

    [Test]
    public void RadialResizeCalculation_NoLongerExists()
    {
        Assert.That(RequireType("BlockResizeController").GetMethod("CalculateRadialSteps", StaticFlags), Is.Null);
        Assert.That(Enum.GetNames(RequireType("ResizeDirection")), Does.Not.Contain("Center"));
    }

    [TestCase(1f, 0f, 0f, "PositiveX")]
    [TestCase(-1f, 0f, 0f, "NegativeX")]
    [TestCase(0f, 1f, 0f, "PositiveY")]
    [TestCase(0f, -1f, 0f, "NegativeY")]
    [TestCase(0f, 0f, 1f, "PositiveZ")]
    [TestCase(0f, 0f, -1f, "NegativeZ")]
    public void LocalNormal_IdentifiesAllSixFaces(float x, float y, float z, string expected)
    {
        object[] arguments = { new Vector3(x, y, z), null, 0.75f };
        bool reliable = (bool)InvokeStatic("ResizableBlock", "TryGetFaceFromLocalNormal", arguments);
        Assert.That(reliable, Is.True);
        Assert.That(arguments[1].ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void RotatedBlock_HitNormalStillReturnsLocalFace()
    {
        Component block = CreateBlock(9, Quaternion.Euler(20f, 70f, 15f));
        Vector3 worldPositiveX = block.transform.TransformDirection(Vector3.right);
        object face = InvokeStatic(
            "BlockResizeGizmo",
            "SelectFace",
            block,
            worldPositiveX,
            true,
            block.transform.position + worldPositiveX * 5f
        );
        Assert.That(face.ToString(), Is.EqualTo("PositiveX"));
    }

    [TestCase(5f, 0f, 0f, "PositiveX")]
    [TestCase(-5f, 0f, 0f, "NegativeX")]
    [TestCase(0f, 5f, 0f, "PositiveY")]
    [TestCase(0f, -5f, 0f, "NegativeY")]
    [TestCase(0f, 0f, 5f, "PositiveZ")]
    [TestCase(0f, 0f, -5f, "NegativeZ")]
    public void CameraFallback_SelectsVisibleFace(float x, float y, float z, string expected)
    {
        Component block = CreateBlock(9, Quaternion.identity);
        object face = InvokeStatic(
            "BlockResizeGizmo",
            "SelectFace",
            block,
            Vector3.zero,
            false,
            new Vector3(x, y, z)
        );
        Assert.That(face.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void CameraFallback_TieUsesCloserFaceCenter()
    {
        bool closerWins = (bool)InvokeStatic("BlockResizeGizmo", "IsBetterFaceCandidate", 0.8f, 2f, 0.8f, 3f);
        bool fartherLoses = (bool)InvokeStatic("BlockResizeGizmo", "IsBetterFaceCandidate", 0.8f, 4f, 0.8f, 3f);
        Assert.That(closerWins, Is.True);
        Assert.That(fartherLoses, Is.False);
    }

    [TestCase("PositiveX")]
    [TestCase("NegativeX")]
    [TestCase("PositiveY")]
    [TestCase("NegativeY")]
    [TestCase("PositiveZ")]
    [TestCase("NegativeZ")]
    public void FaceBasis_ArrowsFollowCameraAndRemainOrthonormal(string faceName)
    {
        Component block = CreateBlock(9, Quaternion.identity);
        Camera camera = CreateCameraForFace(block, faceName);
        object basis = InvokeStatic(
            "BlockResizeGizmo",
            "CalculateFaceBasis",
            block,
            Enum.Parse(RequireType("ResizeFace"), faceName),
            camera
        );

        Vector3 normal = GetVectorProperty(basis, "Normal");
        Vector3 horizontal = GetVectorProperty(basis, "Horizontal");
        Vector3 vertical = GetVectorProperty(basis, "Vertical");
        Vector3 projectedRight = Vector3.ProjectOnPlane(camera.transform.right, normal).normalized;
        Vector3 projectedUp = Vector3.ProjectOnPlane(camera.transform.up, normal).normalized;

        Assert.That(Vector3.Dot(horizontal, projectedRight), Is.GreaterThan(0f));
        Assert.That(Vector3.Dot(vertical, projectedUp), Is.GreaterThan(0f));
        Assert.That(Mathf.Abs(Vector3.Dot(normal, horizontal)), Is.LessThan(0.0001f));
        Assert.That(Mathf.Abs(Vector3.Dot(normal, vertical)), Is.LessThan(0.0001f));
        Assert.That(Mathf.Abs(Vector3.Dot(horizontal, vertical)), Is.LessThan(0.0001f));
        Assert.That(normal.magnitude, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(horizontal.magnitude, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(vertical.magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void FaceBasis_RemainsCorrectForRotatedBlock()
    {
        Component block = CreateBlock(9, Quaternion.Euler(25f, 40f, 15f));
        Camera camera = CreateCameraForFace(block, "NegativeX");
        object basis = InvokeStatic(
            "BlockResizeGizmo",
            "CalculateFaceBasis",
            block,
            Enum.Parse(RequireType("ResizeFace"), "NegativeX"),
            camera
        );
        Vector3 normal = GetVectorProperty(basis, "Normal");
        Vector3 horizontal = GetVectorProperty(basis, "Horizontal");
        Vector3 vertical = GetVectorProperty(basis, "Vertical");
        Assert.That(Vector3.Dot(horizontal, camera.transform.right), Is.GreaterThan(0f));
        Assert.That(Vector3.Dot(vertical, camera.transform.up), Is.GreaterThan(0f));
        Assert.That(Mathf.Abs(Vector3.Dot(normal, horizontal)), Is.LessThan(0.0001f));
        Assert.That(Mathf.Abs(Vector3.Dot(normal, vertical)), Is.LessThan(0.0001f));
    }

    [Test]
    public void Layout_PlacesExactlyFourHandlesAtFaceEdges()
    {
        object layout = InvokeStatic("BlockResizeGizmo", "CalculateLayout", Vector3.zero, Vector3.right, Vector3.up, 4f, 2f);
        AssertVectorProperty(layout, "Top", Vector3.up);
        AssertVectorProperty(layout, "Bottom", Vector3.down);
        AssertVectorProperty(layout, "Left", Vector3.left * 2f);
        AssertVectorProperty(layout, "Right", Vector3.right * 2f);
        Assert.That(layout.GetType().GetProperty("Center"), Is.Null);
    }

    [Test]
    public void LinearSteps_CalculatePositiveNegativeAndThreshold()
    {
        Assert.That(InvokeInt("CalculateLinearSteps", 1.1f, 1f, 0.5f), Is.EqualTo(1));
        Assert.That(InvokeInt("CalculateLinearSteps", -2.1f, 1f, 0.5f), Is.EqualTo(-2));
        Assert.That(InvokeInt("CalculateLinearSteps", 0.49f, 1f, 0.5f), Is.EqualTo(0));
    }

    [Test]
    public void AbsoluteStepCalculation_DoesNotAccumulate()
    {
        int firstFrame = InvokeInt("CalculateLinearSteps", 1.2f, 1f, 0.5f);
        int secondFrame = InvokeInt("CalculateLinearSteps", 1.2f, 1f, 0.5f);
        Assert.That(firstFrame, Is.EqualTo(1));
        Assert.That(secondFrame, Is.EqualTo(1));
    }

    [TestCase(99f, 100f, 6, 0)]
    [TestCase(100f, 100f, 6, 1)]
    [TestCase(249f, 100f, 6, 2)]
    [TestCase(-249f, 100f, 6, -2)]
    [TestCase(2000f, 100f, 6, 6)]
    public void TouchSteps_UsesPixelThresholdAndGestureLimit(
        float pixels, float pixelsPerUnit, int maximum, int expected)
    {
        int result = (int)InvokeStatic(
            "BlockResizeController", "CalculateTouchSteps", pixels, pixelsPerUnit, maximum);
        Assert.That(result, Is.EqualTo(expected));
    }

    private Component CreateBlock(int value, Quaternion rotation)
    {
        GameObject root = Track(new GameObject("GizmoTestBlock"));
        root.transform.rotation = rotation;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        BoxCollider collider = root.AddComponent<BoxCollider>();
        GameObject visual = Track(new GameObject("BlockVisual"));
        visual.transform.SetParent(root.transform, false);
        Component mathValue = root.AddComponent(RequireType("MathBlockValue"));
        Invoke(mathValue, "SetValue", value);
        Component block = root.AddComponent(RequireType("ResizableBlock"));
        SetField(block, "mathBlockValue", mathValue);
        SetField(block, "visualRoot", visual.transform);
        SetField(block, "blockCollider", collider);
        SetField(block, "blockRigidbody", body);
        SetField(block, "obstacleMask", (LayerMask)0);
        InvokeNonPublic(block, "ApplyCurrentDimensions");
        return block;
    }

    private Camera CreateCameraForFace(Component block, string faceName)
    {
        object face = Enum.Parse(RequireType("ResizeFace"), faceName);
        Vector3 normal = (Vector3)Invoke(block, "GetFaceNormalWorld", face);
        Vector3 center = (Vector3)Invoke(block, "GetFaceCenterWorld", face);
        Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? block.transform.forward : Vector3.up;
        GameObject cameraObject = Track(new GameObject("BasisCamera"));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(center + normal * 5f, Quaternion.LookRotation(-normal, up));
        return camera;
    }

    private T Track<T>(T target) where T : UnityEngine.Object
    {
        createdObjects.Add(target);
        return target;
    }

    private static int InvokeInt(string methodName, float distance, float unit, float deadZone)
    {
        return (int)InvokeStatic("BlockResizeController", methodName, distance, unit, deadZone);
    }

    private static object InvokeStatic(string typeName, string methodName, params object[] arguments)
    {
        Type type = RequireType(typeName);
        MethodInfo method = type.GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"Metodo {typeName}.{methodName} nao encontrado.");
        return method.Invoke(null, arguments);
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Metodo {methodName} nao encontrado.");
        return method.Invoke(target, arguments);
    }

    private static object InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Metodo {methodName} nao encontrado.");
        return method.Invoke(target, arguments);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceFlags);
        Assert.That(property, Is.Not.Null, $"Propriedade {propertyName} nao encontrada.");
        return property.GetValue(target);
    }

    private static Vector3 GetVectorProperty(object target, string propertyName)
    {
        return (Vector3)GetProperty(target, propertyName);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Campo {fieldName} nao encontrado.");
        field.SetValue(target, value);
    }

    private static Type RequireType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Tipo {name} nao encontrado.");
        return type;
    }

    private static void AssertVectorProperty(object target, string propertyName, Vector3 expected)
    {
        Vector3 actual = GetVectorProperty(target, propertyName);
        Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
