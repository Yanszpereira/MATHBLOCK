using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class OperatorSelectionTests
{
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly List<GameObject> createdObjects = new List<GameObject>();

    private static Type GravityInteractType => RequireType("GravityInteract");
    private static Type OperatorsScriptType => RequireType("OperatorsScript");

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

    [TestCase("SelectAdditionFromUI", "Addition", "additionIcon")]
    [TestCase("SelectSubtractionFromUI", "Subtraction", "subtractionIcon")]
    [TestCase("SelectMultiplicationFromUI", "Multiplication", "multiplicationIcon")]
    [TestCase("SelectDivisionFromUI", "Division", "divisionIcon")]
    public void UiSelection_EquipsOperatorAndUpdatesHud(
        string selectionMethod,
        string expectedOperator,
        string selectedIconField)
    {
        GameObject player = Track(new GameObject("OperatorSelectionPlayer"));
        Component gravityInteract = player.AddComponent(GravityInteractType);
        Component operators = player.GetComponent(OperatorsScriptType);

        Assert.That(operators, Is.Not.Null);
        SetField(operators, "playOperatorSelectionSounds", false);

        string[] iconFields =
        {
            "additionIcon",
            "subtractionIcon",
            "multiplicationIcon",
            "divisionIcon"
        };

        foreach (string iconField in iconFields)
        {
            GameObject iconObject = Track(new GameObject(iconField, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)));
            SetField(operators, iconField, iconObject.GetComponent<Image>());
        }

        Invoke(operators, selectionMethod);

        object equipped = gravityInteract.GetType()
            .GetProperty("EquippedOperator", InstanceFlags)
            ?.GetValue(gravityInteract);

        Assert.That(equipped?.ToString(), Is.EqualTo(expectedOperator));

        foreach (string iconField in iconFields)
        {
            Image icon = (Image)GetField(operators, iconField);
            float expectedAlpha = iconField == selectedIconField ? 1f : 0.7f;
            Assert.That(icon.color.a, Is.EqualTo(expectedAlpha).Within(0.001f));
        }
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

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
        Assert.That(method, Is.Not.Null, $"Method not found: {methodName}");
        method.Invoke(target, null);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        field.SetValue(target, value);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
        Assert.That(field, Is.Not.Null, $"Field not found: {fieldName}");
        return field.GetValue(target);
    }
}
