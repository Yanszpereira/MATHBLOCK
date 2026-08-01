using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BlockResizeGizmoTests
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public;

    [Test]
    public void RightAndLeft_MapToVisibleDirectionsOnFrontFace()
    {
        Assert.That(InvokeDirection("ResolveHorizontalDirection", Vector3.right, Vector3.right, true), Is.EqualTo("PositiveHorizontal"));
        Assert.That(InvokeDirection("ResolveHorizontalDirection", Vector3.right, Vector3.right, false), Is.EqualTo("NegativeHorizontal"));
    }

    [Test]
    public void RightAndLeft_InvertWhenPositiveAxisPointsLeftOnBackFace()
    {
        Assert.That(InvokeDirection("ResolveHorizontalDirection", Vector3.right, Vector3.left, true), Is.EqualTo("NegativeHorizontal"));
        Assert.That(InvokeDirection("ResolveHorizontalDirection", Vector3.right, Vector3.left, false), Is.EqualTo("PositiveHorizontal"));
    }

    [Test]
    public void TopAndBottom_MapToVisibleVerticalDirections()
    {
        Assert.That(InvokeDirection("ResolveVerticalDirection", Vector3.up, Vector3.up, true), Is.EqualTo("PositiveVertical"));
        Assert.That(InvokeDirection("ResolveVerticalDirection", Vector3.up, Vector3.up, false), Is.EqualTo("NegativeVertical"));
    }

    [Test]
    public void Layout_PlacesAllFiveHandlesAtFaceEdgesAndCenter()
    {
        object layout = InvokeStatic("BlockResizeGizmo", "CalculateLayout", Vector3.zero, Vector3.right, Vector3.up, 4f, 2f);
        AssertVectorProperty(layout, "Top", Vector3.up);
        AssertVectorProperty(layout, "Bottom", Vector3.down);
        AssertVectorProperty(layout, "Left", Vector3.left * 2f);
        AssertVectorProperty(layout, "Right", Vector3.right * 2f);
        AssertVectorProperty(layout, "Center", Vector3.zero);
    }

    [Test]
    public void LinearSteps_CalculatePositiveAndNegativeUnits()
    {
        Assert.That(InvokeInt("CalculateLinearSteps", 1.1f, 1f, 0.5f), Is.EqualTo(1));
        Assert.That(InvokeInt("CalculateLinearSteps", -2.1f, 1f, 0.5f), Is.EqualTo(-2));
    }

    [Test]
    public void RadialSteps_ArePositiveAndReturnTowardDragOrigin()
    {
        Assert.That(InvokeInt("CalculateRadialSteps", 2.1f, 1f, 0.5f), Is.EqualTo(2));
        Assert.That(InvokeInt("CalculateRadialSteps", 0.9f, 1f, 0.5f), Is.EqualTo(1));
        Assert.That(InvokeInt("CalculateRadialSteps", 0f, 1f, 0.5f), Is.EqualTo(0));
    }

    [Test]
    public void Steps_StayZeroBeforeThreshold()
    {
        Assert.That(InvokeInt("CalculateLinearSteps", 0.49f, 1f, 0.5f), Is.EqualTo(0));
        Assert.That(InvokeInt("CalculateRadialSteps", 0.49f, 1f, 0.5f), Is.EqualTo(0));
    }

    [Test]
    public void AbsoluteCalculation_DoesNotAccumulatePreviousFrameResult()
    {
        int firstFrame = InvokeInt("CalculateLinearSteps", 1.2f, 1f, 0.5f);
        int secondFrame = InvokeInt("CalculateLinearSteps", 1.2f, 1f, 0.5f);
        Assert.That(firstFrame, Is.EqualTo(1));
        Assert.That(secondFrame, Is.EqualTo(1));
    }

    [Test]
    public void FaceSelection_UsesOnlyConfiguredPlaneNormals()
    {
        Vector3 chosen = (Vector3)InvokeStatic(
            "BlockResizeGizmo",
            "ChooseFaceNormal",
            Vector3.forward,
            Vector3.zero,
            new Vector3(0f, 0f, -5f),
            Vector3.right,
            true
        );
        Assert.That(chosen, Is.EqualTo(Vector3.back));
    }

    [Test]
    public void FaceSelection_UsesReliableHitNormal()
    {
        Vector3 chosen = (Vector3)InvokeStatic(
            "BlockResizeGizmo",
            "ChooseFaceNormal",
            Vector3.forward,
            Vector3.zero,
            new Vector3(0f, 0f, 5f),
            Vector3.back,
            true
        );
        Assert.That(chosen, Is.EqualTo(Vector3.back));
    }

    private static string InvokeDirection(string methodName, Vector3 cameraAxis, Vector3 blockAxis, bool positiveHandle)
    {
        return InvokeStatic("BlockResizeGizmo", methodName, cameraAxis, blockAxis, positiveHandle).ToString();
    }

    private static int InvokeInt(string methodName, float distance, float unit, float deadZone)
    {
        return (int)InvokeStatic("BlockResizeController", methodName, distance, unit, deadZone);
    }

    private static object InvokeStatic(string typeName, string methodName, params object[] arguments)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Tipo {typeName} nao encontrado.");
        MethodInfo method = type.GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"Metodo {typeName}.{methodName} nao encontrado.");
        return method.Invoke(null, arguments);
    }

    private static void AssertVectorProperty(object target, string propertyName, Vector3 expected)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null);
        Vector3 actual = (Vector3)property.GetValue(target);
        Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
