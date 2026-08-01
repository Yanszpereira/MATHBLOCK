using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ResizableBlockTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    private static Type ResizableBlockType => RequireType("ResizableBlock");
    private static Type MathBlockValueType => RequireType("MathBlockValue");
    private static Type ResizePlaneType => RequireType("ResizePlane");
    private static Type ResizeDirectionType => RequireType("ResizeDirection");

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void ValueOne_RemainsOneByOne()
    {
        TestBlock block = CreateBlock(1);

        Assert.That(TryApply(block, "PositiveHorizontal", 1, out string failure), Is.False);
        Assert.That(failure, Is.EqualTo("AreaLimitExceeded"));
        AssertDimensions(block, 1, 1, 1);
    }

    [Test]
    public void ValueThree_AllowsTwoByOneAndThreeByOne()
    {
        TestBlock block = CreateBlock(3);

        Assert.That(TryApply(block, "PositiveHorizontal", 1, out _), Is.True);
        AssertDimensions(block, 2, 1, 1);
        Assert.That(TryApply(block, "PositiveHorizontal", 1, out _), Is.True);
        AssertDimensions(block, 3, 1, 1);
    }

    [Test]
    public void ValueThree_RejectsTwoByTwo()
    {
        TestBlock block = CreateBlock(3);

        Assert.That(TryApply(block, "Center", 1, out string failure), Is.False);
        Assert.That(failure, Is.EqualTo("AreaLimitExceeded"));
        AssertDimensions(block, 1, 1, 1);
    }

    [Test]
    public void ValueFour_AllowsTwoByTwoAndRejectsThreeByTwo()
    {
        TestBlock block = CreateBlock(4);

        Assert.That(TryApply(block, "Center", 1, out _), Is.True);
        AssertDimensions(block, 2, 2, 1);
        Assert.That(TryApply(block, "PositiveHorizontal", 1, out string failure), Is.False);
        Assert.That(failure, Is.EqualTo("AreaLimitExceeded"));
    }

    [Test]
    public void ValueSix_AllowsThreeByTwo()
    {
        TestBlock block = CreateBlock(6);

        Assert.That(TryApply(block, "Center", 1, out _), Is.True);
        Assert.That(TryApply(block, "PositiveHorizontal", 1, out _), Is.True);
        AssertDimensions(block, 3, 2, 1);
    }

    [Test]
    public void ValueSix_RejectsThreeByThree()
    {
        TestBlock block = CreateBlock(6);

        Assert.That(TryApply(block, "PositiveHorizontal", 2, out _), Is.True);
        Assert.That(TryApply(block, "PositiveVertical", 2, out string failure), Is.False);
        Assert.That(failure, Is.EqualTo("AreaLimitExceeded"));
        AssertDimensions(block, 3, 1, 1);
    }

    [Test]
    public void ValueNine_AllowsThreeByThree()
    {
        TestBlock block = CreateBlock(9);

        Assert.That(TryApply(block, "Center", 2, out _), Is.True);
        AssertDimensions(block, 3, 3, 1);
    }

    [Test]
    public void DimensionZero_IsRejected()
    {
        TestBlock block = CreateBlock(9);

        Assert.That(IsDimensionValid(block, 0, 1, 1, out string failure), Is.False);
        Assert.That(failure, Is.EqualTo("DimensionBelowMinimum"));
    }

    [Test]
    public void ValueZero_IsRejected()
    {
        TestBlock block = CreateBlock(0);

        Assert.That(IsDimensionValid(block, 1, 1, 1, out string failure), Is.False);
        Assert.That(failure, Is.EqualTo("InvalidValue"));
    }

    [TestCase("XY", 2, 2, 1)]
    [TestCase("XZ", 2, 1, 2)]
    [TestCase("YZ", 1, 2, 2)]
    public void CenterResize_ChangesOnlyConfiguredPlane(string plane, int width, int height, int depth)
    {
        TestBlock block = CreateBlock(9, plane);

        Assert.That(TryApply(block, "Center", 1, out _), Is.True);
        AssertDimensions(block, width, height, depth);
    }

    [Test]
    public void PositiveHorizontalGrowth_MovesCenterHalfUnitPositiveX()
    {
        TestBlock block = CreateBlock(3);

        Assert.That(TryApply(block, "PositiveHorizontal", 1, out _), Is.True);
        Assert.That(block.Root.transform.position, Is.EqualTo(Vector3.right * 0.5f).Using(Vector3Comparer.Instance));
    }

    [Test]
    public void NegativeHorizontalGrowth_MovesCenterHalfUnitNegativeX()
    {
        TestBlock block = CreateBlock(3);

        Assert.That(TryApply(block, "NegativeHorizontal", 1, out _), Is.True);
        Assert.That(block.Root.transform.position, Is.EqualTo(Vector3.left * 0.5f).Using(Vector3Comparer.Instance));
    }

    [Test]
    public void PositiveVerticalGrowth_MovesCenterHalfUnitPositiveY()
    {
        TestBlock block = CreateBlock(3);

        Assert.That(TryApply(block, "PositiveVertical", 1, out _), Is.True);
        Assert.That(block.Root.transform.position, Is.EqualTo(Vector3.up * 0.5f).Using(Vector3Comparer.Instance));
    }

    [Test]
    public void CenterGrowth_DoesNotMoveCenter()
    {
        TestBlock block = CreateBlock(4, position: new Vector3(2f, 3f, 4f));
        Vector3 initialPosition = block.Root.transform.position;

        Assert.That(TryApply(block, "Center", 1, out _), Is.True);
        Assert.That(block.Root.transform.position, Is.EqualTo(initialPosition).Using(Vector3Comparer.Instance));
    }

    [Test]
    public void RotatedBlock_UsesLocalAxisForCenterOffset()
    {
        Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
        TestBlock block = CreateBlock(3, rotation: rotation);
        Vector3 expectedPosition = block.Root.transform.right * 0.5f;

        Assert.That(TryApply(block, "PositiveHorizontal", 1, out _), Is.True);
        Assert.That(block.Root.transform.position, Is.EqualTo(expectedPosition).Using(Vector3Comparer.Instance));
    }

    [Test]
    public void CaptureAndRestore_RestoresDimensionsPositionAndVisuals()
    {
        Vector3 initialPosition = new Vector3(3f, 2f, -1f);
        TestBlock block = CreateBlock(9, position: initialPosition);
        object state = Invoke(block.ResizeComponent, "CaptureState");

        Assert.That(TryApply(block, "Center", 1, out _), Is.True);
        AssertDimensions(block, 2, 2, 1);

        Invoke(block.ResizeComponent, "RestoreState", state);

        AssertDimensions(block, 1, 1, 1);
        Assert.That(block.Root.transform.position, Is.EqualTo(initialPosition).Using(Vector3Comparer.Instance));
        Assert.That(block.Collider.size, Is.EqualTo(Vector3.one).Using(Vector3Comparer.Instance));
        Assert.That(block.Visual.localScale, Is.EqualTo(Vector3.one).Using(Vector3Comparer.Instance));
    }

    [Test]
    public void Decrease_NeverProducesDimensionBelowOne()
    {
        TestBlock block = CreateBlock(3);

        Assert.That(TryApply(block, "PositiveHorizontal", 1, out _), Is.True);
        Assert.That(TryApply(block, "PositiveHorizontal", -1, out _), Is.True);
        Assert.That(TryApply(block, "PositiveHorizontal", -1, out string failure), Is.False);
        Assert.That(failure, Is.EqualTo("DimensionBelowMinimum"));
        AssertDimensions(block, 1, 1, 1);
    }

    [Test]
    public void FixedDimension_CannotBeChanged()
    {
        TestBlock block = CreateBlock(9, "XY");

        Assert.That(IsDimensionValid(block, 1, 1, 2, out string failure), Is.False);
        Assert.That(failure, Is.EqualTo("FixedDimensionChanged"));
    }

    [Test]
    public void UnusedArea_IsAllowed()
    {
        TestBlock block = CreateBlock(5);

        Assert.That(TryApply(block, "Center", 1, out _), Is.True);
        AssertDimensions(block, 2, 2, 1);
    }

    private TestBlock CreateBlock(
        int value,
        string plane = "XY",
        Vector3? position = null,
        Quaternion? rotation = null)
    {
        GameObject root = new GameObject("ResizableBlockTest");
        createdObjects.Add(root);
        root.transform.SetPositionAndRotation(position ?? Vector3.zero, rotation ?? Quaternion.identity);

        Rigidbody rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        BoxCollider collider = root.AddComponent<BoxCollider>();
        Component mathBlockValue = root.AddComponent(MathBlockValueType);

        GameObject visualObject = new GameObject("BlockVisual");
        visualObject.transform.SetParent(root.transform, false);
        Transform visual = visualObject.transform;

        Component resizeComponent = root.AddComponent(ResizableBlockType);
        SetField(resizeComponent, "mathBlockValue", mathBlockValue);
        SetField(resizeComponent, "visualRoot", visual);
        SetField(resizeComponent, "blockCollider", collider);
        SetField(resizeComponent, "blockRigidbody", rigidbody);
        SetField(resizeComponent, "resizePlane", Enum.Parse(ResizePlaneType, plane));
        SetField(resizeComponent, "unitSize", Vector3.one);
        SetField(resizeComponent, "obstacleMask", (LayerMask)0);
        SetField(resizeComponent, "collisionSkin", 0.01f);
        Invoke(mathBlockValue, "SetValue", value);
        InvokeNonPublic(resizeComponent, "ApplyCurrentDimensions");

        return new TestBlock(root, visual, collider, resizeComponent);
    }

    private static bool TryApply(TestBlock block, string direction, int deltaUnits, out string failure)
    {
        object directionValue = Enum.Parse(ResizeDirectionType, direction);
        object[] arguments = { directionValue, deltaUnits, null };
        bool result = (bool)GetMethod(block.ResizeComponent, "TryApplyResize").Invoke(block.ResizeComponent, arguments);
        failure = arguments[2].ToString();
        return result;
    }

    private static bool IsDimensionValid(
        TestBlock block,
        int width,
        int height,
        int depth,
        out string failure)
    {
        object[] arguments = { width, height, depth, null };
        bool result = (bool)GetMethod(block.ResizeComponent, "IsDimensionProposalValid")
            .Invoke(block.ResizeComponent, arguments);
        failure = arguments[3].ToString();
        return result;
    }

    private static void AssertDimensions(TestBlock block, int width, int height, int depth)
    {
        Assert.That(GetProperty<int>(block.ResizeComponent, "Width"), Is.EqualTo(width));
        Assert.That(GetProperty<int>(block.ResizeComponent, "Height"), Is.EqualTo(height));
        Assert.That(GetProperty<int>(block.ResizeComponent, "Depth"), Is.EqualTo(depth));
    }

    private static T GetProperty<T>(Component component, string propertyName)
    {
        return (T)component.GetType().GetProperty(propertyName, InstanceFlags).GetValue(component);
    }

    private static void SetField(Component component, string fieldName, object value)
    {
        FieldInfo field = component.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Campo {fieldName} nao encontrado em {component.GetType().Name}.");
        field.SetValue(component, value);
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        return GetMethod(target, methodName).Invoke(target, arguments);
    }

    private static object InvokeNonPublic(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Metodo {methodName} nao encontrado em {target.GetType().Name}.");
        return method.Invoke(target, arguments);
    }

    private static MethodInfo GetMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Metodo {methodName} nao encontrado em {target.GetType().Name}.");
        return method;
    }

    private static Type RequireType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Tipo {typeName} nao encontrado em Assembly-CSharp.");
        return type;
    }

    private readonly struct TestBlock
    {
        public GameObject Root { get; }
        public Transform Visual { get; }
        public BoxCollider Collider { get; }
        public Component ResizeComponent { get; }

        public TestBlock(GameObject root, Transform visual, BoxCollider collider, Component resizeComponent)
        {
            Root = root;
            Visual = visual;
            Collider = collider;
            ResizeComponent = resizeComponent;
        }
    }

    private sealed class Vector3Comparer : IEqualityComparer<Vector3>
    {
        public static readonly Vector3Comparer Instance = new Vector3Comparer();

        public bool Equals(Vector3 left, Vector3 right)
        {
            return Vector3.Distance(left, right) <= 0.0001f;
        }

        public int GetHashCode(Vector3 value)
        {
            return value.GetHashCode();
        }
    }
}
