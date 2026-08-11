using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ResizableBlockUndoTests
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly List<GameObject> createdObjects = new List<GameObject>();

    private static Type ResizableBlockType => RequireType("ResizableBlock");
    private static Type MathBlockValueType => RequireType("MathBlockValue");
    private static Type DesfazerManagerType => RequireType("DesfazerManager");
    private static Type GravityInteractType => RequireType("GravityInteract");
    private static Type PencilOperatorType => RequireType("GravityInteract+PencilOperator");
    private static Type ResizeFaceType => RequireType("ResizeFace");
    private static Type ResizeDirectionType => RequireType("ResizeDirection");

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();

        GameObject undoManager = GameObject.Find("DesfazerManager");
        if (undoManager != null)
            UnityEngine.Object.DestroyImmediate(undoManager);
    }

    [Test]
    public void UndoAddition_AutoFitsTargetAndRestoresConsumedBlock()
    {
        TestBlock target = CreateBlock("UndoTarget", 4, new Vector3(0f, 2f, 0f));
        Assert.That(TryApplyResize(target, "PositiveZ", "PositiveX", 1, out _), Is.True);
        Assert.That(TryApplyResize(target, "PositiveZ", "PositiveY", 1, out _), Is.True);

        TestBlock consumed = CreateBlock("UndoOperand", 5, new Vector3(3f, 2f, 0f));
        object undoManager = GetUndoManager();
        object addition = Enum.Parse(PencilOperatorType, "Addition");

        bool recorded = (bool)Invoke(
            undoManager,
            "TryRecordOperation",
            target.ValueComponent,
            consumed.ValueComponent,
            addition,
            4
        );
        Assert.That(recorded, Is.True);

        Invoke(target.ValueComponent, "SetValue", 9);
        Assert.That(TryApplyResize(target, "PositiveZ", "PositiveX", 1, out _), Is.True);
        Assert.That(TryApplyResize(target, "PositiveZ", "PositiveY", 1, out _), Is.True);
        AssertDimensions(target, 3, 3, 1);

        UnityEngine.Object.DestroyImmediate(consumed.Root);

        bool undone = (bool)Invoke(target.ValueComponent, "TryUndoLastOperation", 1f);

        Assert.That(undone, Is.True);
        Assert.That(GetProperty<int>(target.ValueComponent, "CurrentValue"), Is.EqualTo(4));
        AssertDimensions(target, 2, 2, 1);

        GameObject restored = GameObject.Find("UndoOperand_Restored");
        Assert.That(restored, Is.Not.Null);
        Track(restored);

        Component restoredValue = restored.GetComponent(MathBlockValueType);
        Assert.That(restoredValue, Is.Not.Null);
        Assert.That(GetProperty<int>(restoredValue, "CurrentValue"), Is.EqualTo(5));
        Assert.That(GetOperationCount(target.ValueComponent), Is.Zero);
    }

    [Test]
    public void UndoToZero_IsRejectedAndKeepsHistoryAndOperandSnapshot()
    {
        TestBlock target = CreateBlock("ZeroUndoTarget", 0, new Vector3(0f, 2f, 0f));
        TestBlock consumed = CreateBlock("ZeroUndoOperand", 9, new Vector3(3f, 2f, 0f));
        object undoManager = GetUndoManager();
        object addition = Enum.Parse(PencilOperatorType, "Addition");

        bool recorded = (bool)Invoke(
            undoManager,
            "TryRecordOperation",
            target.ValueComponent,
            consumed.ValueComponent,
            addition,
            0
        );
        Assert.That(recorded, Is.True);

        Invoke(target.ValueComponent, "SetValue", 9);
        Assert.That(TryApplyResize(target, "PositiveZ", "PositiveX", 2, out _), Is.True);
        Assert.That(TryApplyResize(target, "PositiveZ", "PositiveY", 2, out _), Is.True);
        AssertDimensions(target, 3, 3, 1);

        UnityEngine.Object.DestroyImmediate(consumed.Root);

        bool undone = (bool)Invoke(target.ValueComponent, "TryUndoLastOperation", 1f);

        Assert.That(undone, Is.False);
        Assert.That(GetProperty<int>(target.ValueComponent, "CurrentValue"), Is.EqualTo(9));
        AssertDimensions(target, 3, 3, 1);
        Assert.That(GetOperationCount(target.ValueComponent), Is.EqualTo(1));
        Assert.That(GameObject.Find("ZeroUndoOperand_Restored"), Is.Null);
    }

    [Test]
    public void UndoRaycast_SkipsNearestRestoredBlockWithoutHistory()
    {
        TestBlock target = CreateBlock("UndoRaycastTarget", 4, new Vector3(0f, 2f, 5f));
        target.Root.tag = "MathBlock";

        TestBlock consumed = CreateBlock("UndoRaycastOperand", 5, new Vector3(3f, 2f, 5f));
        object undoManager = GetUndoManager();
        object addition = Enum.Parse(PencilOperatorType, "Addition");
        Assert.That(
            (bool)Invoke(
                undoManager,
                "TryRecordOperation",
                target.ValueComponent,
                consumed.ValueComponent,
                addition,
                4
            ),
            Is.True
        );
        Invoke(target.ValueComponent, "SetValue", 9);
        UnityEngine.Object.DestroyImmediate(consumed.Root);

        TestBlock restoredBlockInFront = CreateBlock(
            "RestoredOperandWithoutHistory",
            5,
            new Vector3(0f, 2f, 2f)
        );
        restoredBlockInFront.Root.tag = "MathBlock";

        GameObject player = new GameObject("UndoRaycastPlayer");
        Track(player);
        player.transform.SetPositionAndRotation(new Vector3(0f, 2f, 0f), Quaternion.identity);
        Component gravityInteract = player.AddComponent(GravityInteractType);
        SetField(gravityInteract, "interactionCamera", player.transform);
        SetField(gravityInteract, "grabDistance", 10f);

        Physics.SyncTransforms();
        Assert.That(
            GetProperty<bool>(target.ValueComponent, "HasOperationsToUndo"),
            Is.True,
            "O alvo de fundo deve manter sua operacao antes do raycast."
        );

        RaycastHit[] raycastHits = Physics.RaycastAll(
            player.transform.position,
            player.transform.forward,
            10f
        );
        bool foundTargetCollider = false;
        List<string> hitSummary = new List<string>();
        for (int hitIndex = 0; hitIndex < raycastHits.Length; hitIndex++)
        {
            RaycastHit raycastHit = raycastHits[hitIndex];
            foundTargetCollider |= raycastHit.collider == target.Collider;
            hitSummary.Add($"{raycastHit.collider.name}@{raycastHit.distance:0.###}");
        }

        Assert.That(
            foundTargetCollider,
            Is.True,
            $"O raycast deve alcancar o alvo ao fundo. Hits: {string.Join(", ", hitSummary)}"
        );

        Invoke(gravityInteract, "TryHandleUndoBlockOperation");

        Assert.That(GetProperty<int>(target.ValueComponent, "CurrentValue"), Is.EqualTo(4));
        Assert.That(GetOperationCount(target.ValueComponent), Is.Zero);
        Assert.That(
            GetProperty<int>(restoredBlockInFront.ValueComponent, "CurrentValue"),
            Is.EqualTo(5)
        );

        GameObject restoredOperand = GameObject.Find("UndoRaycastOperand_Restored");
        Assert.That(restoredOperand, Is.Not.Null);
        Track(restoredOperand);
    }

    [Test]
    public void FitToValueThree_PreservesCubeShape()
    {
        TestBlock block = CreateBlock("CubeFitTarget", 8, new Vector3(0f, 2f, 0f));
        Assert.That(TryApplyResize(block, "PositiveZ", "PositiveX", 1, out _), Is.True);
        Assert.That(TryApplyResize(block, "PositiveZ", "PositiveY", 1, out _), Is.True);
        Assert.That(TryApplyResize(block, "PositiveX", "PositiveZ", 1, out _), Is.True);
        AssertDimensions(block, 2, 2, 2);

        bool fitted = TryFitToValue(block, 3, out string failure);

        Assert.That(fitted, Is.True, failure);
        Assert.That(failure, Is.EqualTo("None"));
        AssertDimensions(block, 1, 1, 1);
    }

    [Test]
    public void FitToValueFour_PreservesDynamicBottomAndRigidbodyFlags()
    {
        TestBlock block = CreateBlock("GroundedFitTarget", 9, new Vector3(0f, 5f, 0f));
        Assert.That(TryApplyResize(block, "PositiveZ", "PositiveX", 2, out _), Is.True);
        Assert.That(TryApplyResize(block, "PositiveZ", "PositiveY", 2, out _), Is.True);
        AssertDimensions(block, 3, 3, 1);

        block.Rigidbody.isKinematic = false;
        block.Rigidbody.useGravity = true;
        Physics.SyncTransforms();
        float bottomBeforeFit = block.Collider.bounds.min.y;

        bool fitted = TryFitToValue(block, 4, out string failure);

        Assert.That(fitted, Is.True, failure);
        AssertDimensions(block, 2, 2, 1);
        Assert.That(block.Collider.bounds.min.y, Is.EqualTo(bottomBeforeFit).Within(0.0001f));
        Assert.That(block.Rigidbody.isKinematic, Is.False);
        Assert.That(block.Rigidbody.useGravity, Is.True);
    }

    private TestBlock CreateBlock(string objectName, int value, Vector3 position)
    {
        GameObject root = new GameObject(objectName);
        Track(root);
        root.transform.position = position;

        Rigidbody rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        BoxCollider collider = root.AddComponent<BoxCollider>();
        Component mathBlockValue = root.AddComponent(MathBlockValueType);

        GameObject visualObject = new GameObject("BlockVisual");
        visualObject.transform.SetParent(root.transform, false);

        Component resizeComponent = root.AddComponent(ResizableBlockType);
        SetField(resizeComponent, "mathBlockValue", mathBlockValue);
        SetField(resizeComponent, "visualRoot", visualObject.transform);
        SetField(resizeComponent, "blockCollider", collider);
        SetField(resizeComponent, "blockRigidbody", rigidbody);
        SetField(resizeComponent, "unitSize", Vector3.one);
        SetField(resizeComponent, "obstacleMask", (LayerMask)0);
        SetField(resizeComponent, "collisionSkin", 0.01f);

        Invoke(mathBlockValue, "SetValue", value);
        Invoke(resizeComponent, "ApplyCurrentDimensions");

        return new TestBlock(root, collider, rigidbody, mathBlockValue, resizeComponent);
    }

    private object GetUndoManager()
    {
        object manager = DesfazerManagerType
            .GetProperty("Instance", StaticFlags)
            .GetValue(null);

        Component managerComponent = manager as Component;
        if (managerComponent != null)
            Track(managerComponent.gameObject);

        return manager;
    }

    private void Track(GameObject target)
    {
        if (target != null && !createdObjects.Contains(target))
            createdObjects.Add(target);
    }

    private static bool TryApplyResize(
        TestBlock block,
        string face,
        string direction,
        int deltaUnits,
        out string failure)
    {
        object[] arguments =
        {
            Enum.Parse(ResizeFaceType, face),
            Enum.Parse(ResizeDirectionType, direction),
            deltaUnits,
            null
        };

        bool result = (bool)GetMethod(block.ResizeComponent, "TryApplyResize")
            .Invoke(block.ResizeComponent, arguments);
        failure = arguments[3].ToString();
        return result;
    }

    private static bool TryFitToValue(TestBlock block, int maximumVolume, out string failure)
    {
        object[] arguments = { maximumVolume, null };
        bool result = (bool)GetMethod(block.ResizeComponent, "TryFitToValue")
            .Invoke(block.ResizeComponent, arguments);
        failure = arguments[1].ToString();
        return result;
    }

    private static int GetOperationCount(Component valueComponent)
    {
        object stack = valueComponent.GetType()
            .GetProperty("OperationStack", InstanceFlags)
            .GetValue(valueComponent);

        return (int)stack.GetType()
            .GetProperty("Count", InstanceFlags)
            .GetValue(stack);
    }

    private static void AssertDimensions(TestBlock block, int width, int height, int depth)
    {
        Assert.That(GetProperty<int>(block.ResizeComponent, "Width"), Is.EqualTo(width));
        Assert.That(GetProperty<int>(block.ResizeComponent, "Height"), Is.EqualTo(height));
        Assert.That(GetProperty<int>(block.ResizeComponent, "Depth"), Is.EqualTo(depth));
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        return (T)target.GetType()
            .GetProperty(propertyName, InstanceFlags)
            .GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Campo {fieldName} nao encontrado em {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        return GetMethod(target, methodName).Invoke(target, arguments);
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
        public BoxCollider Collider { get; }
        public Rigidbody Rigidbody { get; }
        public Component ValueComponent { get; }
        public Component ResizeComponent { get; }

        public TestBlock(
            GameObject root,
            BoxCollider collider,
            Rigidbody rigidbody,
            Component valueComponent,
            Component resizeComponent)
        {
            Root = root;
            Collider = collider;
            Rigidbody = rigidbody;
            ValueComponent = valueComponent;
            ResizeComponent = resizeComponent;
        }
    }
}
