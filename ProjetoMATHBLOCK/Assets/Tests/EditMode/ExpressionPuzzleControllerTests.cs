using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ExpressionPuzzleControllerTests
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly List<GameObject> createdObjects = new List<GameObject>();

    private static Type ControllerType => RequireType("ExpressionPuzzleController");
    private static Type ExpressionType =>
        ControllerType.GetNestedType("ExpressionDefinition", BindingFlags.Public);
    private static Type OperatorType =>
        ControllerType.GetNestedType("ArithmeticOperator", BindingFlags.Public);
    private static Type GroupingType =>
        ControllerType.GetNestedType("ExpressionGrouping", BindingFlags.Public);
    private static Type AxisType =>
        ControllerType.GetNestedType("LayoutAxis", BindingFlags.Public);

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void Layout_ThreePads_AreCenteredAndEquallySpacedOnSelectedAxis()
    {
        Vector3 first = CalculatePadPosition(0);
        Vector3 middle = CalculatePadPosition(1);
        Vector3 last = CalculatePadPosition(2);

        Assert.That(first, Is.EqualTo(new Vector3(0f, 0f, -10f)));
        Assert.That(middle, Is.EqualTo(Vector3.zero));
        Assert.That(last, Is.EqualTo(new Vector3(0f, 0f, 10f)));
        Assert.That(
            Vector3.Distance(first, middle),
            Is.EqualTo(Vector3.Distance(middle, last)));
    }

    [Test]
    public void LeftGroupedExpression_EvaluatesInDeclaredOrder()
    {
        object expression = CreateExpression("Add", "Subtract", "Left");

        bool valid = TryEvaluate(expression, new[] { 4, 3, 2 }, out int result);

        Assert.That(valid, Is.True);
        Assert.That(result, Is.EqualTo(5));
        Assert.That(BuildDisplayText(expression), Is.EqualTo("(Pad1 + Pad2) - Pad3"));
    }

    [Test]
    public void RightGroupedExpression_EvaluatesInnerOperationFirst()
    {
        object expression = CreateExpression("Subtract", "Add", "Right");

        bool valid = TryEvaluate(expression, new[] { 10, 3, 2 }, out int result);

        Assert.That(valid, Is.True);
        Assert.That(result, Is.EqualTo(5));
    }

    [TestCase(0, true)]
    [TestCase(5, true)]
    [TestCase(6, false)]
    [TestCase(9, false)]
    [TestCase(10, true)]
    [TestCase(11, false)]
    public void RestrictedOperands_AllowOnlyZeroThroughFiveOrTen(int value, bool expected)
    {
        MethodInfo method = ControllerType.GetMethod(
            "IsAllowedRestrictedOperand",
            StaticFlags);

        Assert.That(method, Is.Not.Null);
        Assert.That((bool)method.Invoke(null, new object[] { value }), Is.EqualTo(expected));
    }

    [Test]
    public void MultiplicationExpression_RejectsAnyPadValueAboveFiveExceptTen()
    {
        object expression = CreateExpression("Multiply", "Subtract", "Left");

        bool accepted = TryEvaluate(expression, new[] { 2, 5, 1 }, out int acceptedResult);
        bool rejected = TryEvaluate(expression, new[] { 2, 6, 1 }, out _);
        bool acceptedTen = TryEvaluate(expression, new[] { 10, 2, 5 }, out int tenResult);

        Assert.That(accepted, Is.True);
        Assert.That(acceptedResult, Is.EqualTo(9));
        Assert.That(rejected, Is.False);
        Assert.That(acceptedTen, Is.True);
        Assert.That(tenResult, Is.EqualTo(15));
    }

    [Test]
    public void DivisionExpression_RequiresExactNonZeroDivision()
    {
        object expression = CreateExpression("Divide", "Add", "Left");

        bool exact = TryEvaluate(expression, new[] { 10, 5, 2 }, out int exactResult);
        bool fractional = TryEvaluate(expression, new[] { 5, 2, 1 }, out _);
        bool zeroDivisor = TryEvaluate(expression, new[] { 5, 0, 1 }, out _);

        Assert.That(exact, Is.True);
        Assert.That(exactResult, Is.EqualTo(4));
        Assert.That(fractional, Is.False);
        Assert.That(zeroDivisor, Is.False);
    }

    [Test]
    public void AdditionOnlyExpression_AllowsValuesAboveFive()
    {
        object expression = CreateExpression("Add", "Subtract", "Left");

        bool valid = TryEvaluate(expression, new[] { 12, 8, 3 }, out int result);

        Assert.That(valid, Is.True);
        Assert.That(result, Is.EqualTo(17));
    }

    [Test]
    public void PresentationObject_UsesChildTmpTextForExpressionAndTarget()
    {
        GameObject root = Track(new GameObject("ExpressionPuzzleControllerTest"));
        Component controller = root.AddComponent(ControllerType);
        GameObject presenter = Track(new GameObject("Presenter"));
        presenter.transform.SetParent(root.transform);
        GameObject textObject = Track(new GameObject("Text"));
        textObject.transform.SetParent(presenter.transform);

        Type textMeshProType = RequireType("TMPro.TextMeshPro");
        Component textComponent = textObject.AddComponent(textMeshProType);

        SetField(controller, "expressionDisplayObject", presenter);
        SetField(controller, "currentExpression", CreateExpression("Add", "Subtract", "Left"));
        SetField(controller, "targetValue", 7);

        MethodInfo refreshPresentation =
            ControllerType.GetMethod("RefreshPresentation", InstanceFlags);
        Assert.That(refreshPresentation, Is.Not.Null);
        refreshPresentation.Invoke(controller, null);

        PropertyInfo textProperty = textMeshProType.GetProperty("text", InstanceFlags);
        Assert.That(textProperty, Is.Not.Null);
        Assert.That(
            textProperty.GetValue(textComponent),
            Is.EqualTo("(Pad1 + Pad2) - Pad3 = 7"));
    }

    [Test]
    public void InclusiveTargetGeneration_StaysInsideWideIntegerRange()
    {
        GameObject root = Track(new GameObject("ExpressionPuzzleControllerTest"));
        Component controller = root.AddComponent(ControllerType);
        SetField(controller, "useFixedSeed", true);
        SetField(controller, "fixedSeed", 98765);

        MethodInfo nextInclusive = ControllerType.GetMethod("NextInclusive", InstanceFlags);
        Assert.That(nextInclusive, Is.Not.Null);

        const int minimum = -2000000000;
        const int maximum = 1000000000;

        for (int i = 0; i < 1000; i++)
        {
            int value = (int)nextInclusive.Invoke(
                controller,
                new object[] { minimum, maximum });

            Assert.That(value, Is.InRange(minimum, maximum));
        }
    }

    private static Vector3 CalculatePadPosition(int index)
    {
        MethodInfo method = ControllerType.GetMethod(
            "CalculatePadLocalPosition",
            StaticFlags);
        Assert.That(method, Is.Not.Null);

        return (Vector3)method.Invoke(
            null,
            new object[]
            {
                Vector3.zero,
                new Vector3(1f, 1f, 24f),
                Enum.Parse(AxisType, "Z"),
                2f,
                index,
                3
            });
    }

    private static object CreateExpression(
        string firstOperator,
        string secondOperator,
        string grouping)
    {
        ConstructorInfo constructor = ExpressionType.GetConstructor(
            new[] { typeof(string), OperatorType, OperatorType, GroupingType });
        Assert.That(constructor, Is.Not.Null);

        return constructor.Invoke(
            new[]
            {
                (object)"Teste",
                Enum.Parse(OperatorType, firstOperator),
                Enum.Parse(OperatorType, secondOperator),
                Enum.Parse(GroupingType, grouping)
            });
    }

    private static bool TryEvaluate(object expression, int[] values, out int result)
    {
        MethodInfo method = ControllerType.GetMethod("TryEvaluate", StaticFlags);
        Assert.That(method, Is.Not.Null);

        object[] arguments = { expression, values, 0 };
        bool valid = (bool)method.Invoke(null, arguments);
        result = (int)arguments[2];
        return valid;
    }

    private static string BuildDisplayText(object expression)
    {
        MethodInfo method = ExpressionType.GetMethod("BuildDisplayText", InstanceFlags);
        Assert.That(method, Is.Not.Null);
        return (string)method.Invoke(expression, null);
    }

    private GameObject Track(GameObject target)
    {
        createdObjects.Add(target);
        return target;
    }

    private static Type RequireType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        throw new InvalidOperationException($"Type not found: {typeName}");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(target, value);
    }
}
