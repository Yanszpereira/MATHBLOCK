using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class Fase4ObstacleScheduleTests
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private Type scheduleType;

    [SetUp]
    public void SetUp()
    {
        scheduleType = ResolveRuntimeType("Fase4PistonCycleSchedule");
        Assert.That(scheduleType, Is.Not.Null, "A agenda temporal dos pistões deve existir.");
    }

    [TestCase(1, 15f, 24f)]
    [TestCase(2, 12f, 30f)]
    [TestCase(3, 9f, 40f)]
    public void BarRotationUsesTheConfiguredLapTime(int round, float expectedSeconds, float expectedDegreesPerSecond)
    {
        Type timingType = ResolveRuntimeType("Fase4ObstacleTiming");
        Assert.That(timingType, Is.Not.Null);
        MethodInfo secondsMethod = timingType.GetMethod("GetSecondsPerRevolution", PublicStatic);
        MethodInfo speedMethod = timingType.GetMethod("GetDegreesPerSecond", PublicStatic);

        Assert.That((float)secondsMethod.Invoke(null, new object[] { round }), Is.EqualTo(expectedSeconds));
        Assert.That((float)speedMethod.Invoke(null, new object[] { round }), Is.EqualTo(expectedDegreesPerSecond));
    }

    [Test]
    public void PistonCycleUsesTwoSecondWarningOneAndHalfSecondExtensionHalfSecondHoldTwoSecondRetractionAndTenSecondCadence()
    {
        object schedule = CreateSchedule();
        Invoke(schedule, "Activate");

        Advance(schedule, 1.99f);
        AssertPhase(schedule, "Warning");
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0f));

        Advance(schedule, 0.01f);
        AssertPhase(schedule, "Extending");
        Advance(schedule, 0.375f);
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0.15625f).Within(0.0001f),
            "A extensão deve usar interpolação suave, não progresso linear.");
        Advance(schedule, 0.375f);
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0.5f).Within(0.0001f));
        Advance(schedule, 0.75f);
        AssertPhase(schedule, "Holding");
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(1f).Within(0.0001f));

        Advance(schedule, 0.5f);
        AssertPhase(schedule, "Retracting");
        Advance(schedule, 0.5f);
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0.84375f).Within(0.0001f),
            "A retração também deve usar interpolação suave.");
        Advance(schedule, 1.5f);
        AssertPhase(schedule, "Resting");
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0f).Within(0.0001f));

        Advance(schedule, 3.999f);
        AssertPhase(schedule, "Resting");
        Advance(schedule, 0.001f);
        AssertPhase(schedule, "Warning");
        Assert.That(GetInt(schedule, "CycleIndex"), Is.EqualTo(2));
    }

    [Test]
    public void StopDuringExtensionRetractsFromCurrentPositionAndPreventsFurtherCycles()
    {
        object schedule = CreateSchedule();
        Invoke(schedule, "Activate");
        Advance(schedule, 2.75f);
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0.5f).Within(0.001f));

        Invoke(schedule, "Stop");
        AssertPhase(schedule, "Retracting");
        Advance(schedule, 1f);
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0.25f).Within(0.001f));
        Advance(schedule, 1f);
        AssertPhase(schedule, "Stopped");
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0f).Within(0.0001f));

        Advance(schedule, 20f);
        AssertPhase(schedule, "Stopped");
        Assert.That(GetInt(schedule, "CycleIndex"), Is.EqualTo(1));
    }

    [Test]
    public void DuplicateActivationDoesNotRestartTheCurrentWarningOrCreateAnotherCycle()
    {
        object schedule = CreateSchedule();
        Invoke(schedule, "Activate");
        Advance(schedule, 1f);
        Invoke(schedule, "Activate");
        Advance(schedule, 1f);

        AssertPhase(schedule, "Extending");
        Assert.That(GetInt(schedule, "CycleIndex"), Is.EqualTo(1));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1f)]
    public void InvalidElapsedTimeDoesNotAdvanceTheCycle(float deltaTime)
    {
        object schedule = CreateSchedule();
        Invoke(schedule, "Activate");
        Advance(schedule, deltaTime);

        AssertPhase(schedule, "Warning");
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0f));
    }

    [Test]
    public void StopDuringWarningCancelsTheAttackWithoutExtending()
    {
        object schedule = CreateSchedule();
        Invoke(schedule, "Activate");
        Advance(schedule, 1f);
        Invoke(schedule, "Stop");

        AssertPhase(schedule, "Stopped");
        Assert.That(GetFloat(schedule, "ExtensionAmount"), Is.EqualTo(0f));
    }

    [Test]
    public void ReachSolverStopsAtTheFirstTriangleEdgeIntersectionWithTheArenaBoundary()
    {
        Type solverType = ResolveRuntimeType("Fase4PistonReachSolver");
        Assert.That(solverType, Is.Not.Null);
        MethodInfo solve = solverType.GetMethod("TryGetMaximumTravel", BindingFlags.Public | BindingFlags.Static);
        Assert.That(solve, Is.Not.Null);
        object[] arguments =
        {
            new[] { new Vector3(10f, 0f, -3f), new Vector3(10f, 0f, 3f), new Vector3(12f, 0f, 0f) },
            new[] { 0, 1, 2 },
            Vector3.zero,
            Vector3.left,
            5f,
            0f
        };

        bool success = (bool)solve.Invoke(null, arguments);

        Assert.That(success, Is.True);
        Assert.That((float)arguments[5], Is.EqualTo(5f).Within(0.0001f));
    }

    [Test]
    public void ReachSolverRejectsGeometryAlreadyInsideTheProtectedRadius()
    {
        Type solverType = ResolveRuntimeType("Fase4PistonReachSolver");
        MethodInfo solve = solverType.GetMethod("TryGetMaximumTravel", BindingFlags.Public | BindingFlags.Static);
        object[] arguments =
        {
            new[] { new Vector3(4f, 0f, -1f), new Vector3(4f, 0f, 1f), new Vector3(3f, 0f, 0f) },
            new[] { 0, 1, 2 },
            Vector3.zero,
            Vector3.left,
            5f,
            0f
        };

        bool success = (bool)solve.Invoke(null, arguments);

        Assert.That(success, Is.False);
    }

    private object CreateSchedule() => Activator.CreateInstance(scheduleType);

    private static Type ResolveRuntimeType(string typeName) => Type.GetType(typeName + ", Assembly-CSharp");

    private static void Invoke(object target, string method, params object[] arguments)
    {
        MethodInfo info = target.GetType().GetMethod(method, PublicInstance);
        Assert.That(info, Is.Not.Null, "Método esperado ausente: " + method);
        info.Invoke(target, arguments);
    }

    private static void Advance(object target, float seconds) => Invoke(target, "Advance", seconds);

    private static void AssertPhase(object target, string expected) =>
        Assert.That(GetProperty(target, "CurrentPhase").ToString(), Is.EqualTo(expected));

    private static float GetFloat(object target, string property) => (float)GetProperty(target, property);

    private static int GetInt(object target, string property) => (int)GetProperty(target, property);

    private static object GetProperty(object target, string property)
    {
        PropertyInfo info = target.GetType().GetProperty(property, PublicInstance);
        Assert.That(info, Is.Not.Null, "Propriedade esperada ausente: " + property);
        return info.GetValue(target);
    }
}
