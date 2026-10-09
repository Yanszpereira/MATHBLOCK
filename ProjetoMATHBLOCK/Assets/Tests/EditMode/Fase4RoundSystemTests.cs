using System;
using System.Collections;
using System.Collections.Generic;

using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class Fase4RoundSystemTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string BankPath = "Assets/Data/Fase4/Fase4ExpressionBank.asset";

    private UnityEngine.Object bank;
    private IList expressions;
    private Type definitionType;
    private Type evaluatorType;
    private Type deckType;
    private Type managerType;

    [SetUp]
    public void SetUp()
    {
        bank = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(BankPath);
        Assert.That(bank, Is.Not.Null, "Banco ScriptableObject ausente.");
        definitionType = RequireType("Fase4ExpressionDefinition");
        evaluatorType = RequireType("Fase4ExpressionEvaluator");
        deckType = RequireType("Fase4ExpressionDeck");
        managerType = RequireType("Fase4RoundManager");
        expressions = (IList)GetProperty(bank, "Expressions");
    }

    [Test]
public void Bank_EvaluatesAllTwentyExpressionsWithinDeclaredRules()
{
    int[] expected = {19,8,24,5,23,29,6,12,29,20,6,9,33,24,38,25,5,30,29,8};
    Assert.That(expressions.Count, Is.EqualTo(20));
    MethodInfo compatible = evaluatorType.GetMethod("IsCompatibleWithMathBlockOperations", StaticFlags);
    Assert.That(compatible, Is.Not.Null);
    for (int i = 0; i < expressions.Count; i++)
    {
        object expression = expressions[i];
        string id = (string)GetProperty(expression, "ExpressionId");
        int[] operands = (int[])GetProperty(expression, "Operands");
        Array operations = (Array)GetProperty(expression, "Operations");
        Array pairs = (Array)GetProperty(expression, "ParenthesisPairs");
        Assert.That(operands.Length, Is.EqualTo(4), id);
        Assert.That(operations.Length, Is.EqualTo(3), id);
        Assert.That(pairs.Length, Is.InRange(1, 2), id);
        foreach (int value in operands) Assert.That(value, Is.InRange(1, 20), id + " operand");
        foreach (object operation in operations)
            Assert.That((bool)compatible.Invoke(null, new[] { operation }), Is.True, operation.ToString());
        bool valid = Evaluate(expression, out int result);
        Assert.That(valid, Is.True, "Resultado intermediário inválido: " + id);
        Assert.That(result, Is.EqualTo(expected[i]), "Resultado divergente em " + id);
        Assert.That(result, Is.InRange(1, 50), id);
    }
}

    [Test]
    public void Evaluator_UsesParenthesesAndConventionalMultiplicationPrecedence()
    {
        object operationAdd = Enum.Parse(RequireType("Fase4ArithmeticOperation"), "Add");
        object operationMultiply = Enum.Parse(RequireType("Fase4ArithmeticOperation"), "Multiply");
        Array operations = Array.CreateInstance(operationAdd.GetType(), 3);
        operations.SetValue(operationAdd, 0);
        operations.SetValue(operationMultiply, 1);
        operations.SetValue(operationAdd, 2);

        Type pairType = definitionType.GetNestedType("ParenthesisPair", BindingFlags.Public);
        Array pairs = Array.CreateInstance(pairType, 1);
        pairs.SetValue(Activator.CreateInstance(pairType, new object[] { 1, 2 }), 0);
        MethodInfo method = evaluatorType.GetMethod("TryEvaluate", StaticFlags, null,
            new[] { typeof(int[]), operations.GetType(), pairs.GetType(), typeof(int).MakeByRefType() }, null);
        Assert.That(method, Is.Not.Null);

        object[] args = { new[] { 2, 3, 4, 1 }, operations, pairs, 0 };
        bool valid = (bool)method.Invoke(null, args);
        Assert.That(valid, Is.True);
        Assert.That((int)args[3], Is.EqualTo(15), "2 + (3 × 4) + 1 deve respeitar a precedência.");
    }

    [Test]
    public void Deck_DoesNotRepeatUntilExhaustedAndAvoidsImmediateRefillRepeat()
    {
        object deck = CreateDeck(713);
        var seen = new HashSet<string>();
        string last = null;
        for (int i = 0; i < 20; i++)
        {
            object item = Invoke(deck, "Draw");
            string id = (string)GetProperty(item, "ExpressionId");
            Assert.That(seen.Add(id), Is.True, "Expressão repetida antes de esgotar o banco.");
            last = id;
        }

        string next = (string)GetProperty(Invoke(deck, "Draw"), "ExpressionId");
        Assert.That(next, Is.Not.EqualTo(last));
    }

    [Test]
public void Distractors_AreUniqueExcludeRequiredValuesAndStayInRange()
{
    Type generator = RequireType("Fase4DistractorGenerator");
    MethodInfo method = generator.GetMethod("Generate", StaticFlags);
    Assert.That(method, Is.Not.Null);
    int[] source = {3,4,7,2,14,5,19};
    int[] required = {3,4,7,2,14,5,19};
    for (int seed = 0; seed < 200; seed++)
    {
        int[] generated = (int[])method.Invoke(null, new object[] { source, required, new System.Random(seed), (int?)3 });
        Assert.That(generated.Length, Is.EqualTo(3));
        Assert.That(generated.Distinct().Count(), Is.EqualTo(3));
        foreach (int value in generated)
        {
            Assert.That(value, Is.InRange(1, 50));
            Assert.That(Array.IndexOf(required, value), Is.EqualTo(-1));
            int nearestSource = 100;
            foreach (int candidate in source)
                nearestSource = Math.Min(nearestSource, Math.Abs(candidate - value));
            Assert.That(nearestSource <= 3, Is.True, "Variação inicial ±3 deveria bastar neste caso.");
        }
    }
}

    [Test]
    public void Round_StartsPausedDuringPreparationAndClockStartsAfterSpawn()
    {
        object manager = CreateManager(22);
        Assert.That(State(manager), Is.EqualTo("AwaitingInteraction"));
        Assert.That(CallBool(manager, "RequestAttempt"), Is.True);
        Assert.That(State(manager), Is.EqualTo("Preparing"));
        Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(60f));
        Invoke(manager, "Tick", 10f);
        Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(60f));
        Assert.That(CallBool(manager, "NotifySpawnCompleted"), Is.True);
        Invoke(manager, "Tick", 7f);
        Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(53f));
    }

    [Test]
    public void WrongAnswersApplyFirstPenaltyActivatePistonsOnSecondAndFailOnThird()
    {
        object manager = CreateManager(25);
        StartRound(manager);
        Assert.That(CallBool(manager, "SubmitAnswer", false), Is.True);
        Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(55f));
        Assert.That(CallBool(manager, "SubmitAnswer", false), Is.True);
        Assert.That((bool)GetProperty(manager, "PistonsLogicallyActive"), Is.True);
        Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(55f), "Erros posteriores não aplicam -5.");
        Assert.That(CallBool(manager, "SubmitAnswer", false), Is.True);
        Assert.That(State(manager), Is.EqualTo("FailureAndRespawn"));
        Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(0));
        Assert.That((bool)GetProperty(manager, "PistonsLogicallyActive"), Is.False);
    }

    [Test]
    public void CorrectAnswerAddsThirtyFiveAndPausesDuringFiveSecondTransition()
    {
        object manager = CreateManager(29);
        StartRound(manager);
        Invoke(manager, "Tick", 10f);
        Assert.That(CallBool(manager, "SubmitAnswer", true), Is.True);
        Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(85f));
        Assert.That(State(manager), Is.EqualTo("InterRoundTransition"));
        Invoke(manager, "Tick", 4.9f);
        Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(85f));
        Assert.That(State(manager), Is.EqualTo("InterRoundTransition"));
        Invoke(manager, "Tick", 0.1f);
        Assert.That(State(manager), Is.EqualTo("Preparing"));
        Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(1));
        Assert.That((int)GetProperty(manager, "ErrorsThisRound"), Is.EqualTo(0));
        Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(85f));
    }

    [Test]
public void FailureRetryResetsCurrentRoundToSixtyAndPreservesCompletedRounds()
{
    object manager = CreateManager(31);
    StartRound(manager);
    CallBool(manager, "SubmitAnswer", true);
    Invoke(manager, "Tick", 5f);
    StartPreparedRound(manager);
    Assert.That((int)GetProperty(manager, "CurrentRound"), Is.EqualTo(2));

    Invoke(manager, "Tick", 96f);
    Assert.That(State(manager), Is.EqualTo("FailureAndRespawn"));
    Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(1));
    CallBool(manager, "NotifyRespawnCompleted");
    Assert.That(CallBool(manager, "RequestAttempt"), Is.True);
    Assert.That(State(manager), Is.EqualTo("Preparing"));
    Assert.That((int)GetProperty(manager, "CurrentRound"), Is.EqualTo(2));
    Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(1));
    Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(60f));
}

    [Test]
    public void ThirdWrongAnswerAtZeroTimeFailsImmediatelyAndDuplicateAnswersAreIgnored()
    {
        object manager = CreateManager(35);
        StartRound(manager);
        Invoke(manager, "Tick", 56f);
        CallBool(manager, "SubmitAnswer", false);
        Assert.That(State(manager), Is.EqualTo("FailureAndRespawn"), "O -5 que esgota o tempo encerra a tentativa imediatamente.");
        Assert.That(CallBool(manager, "SubmitAnswer", true), Is.False);
    }

    [Test]
    public void ThreeCorrectRoundsCompleteChallengeWithoutStartingFourthRound()
    {
        object manager = CreateManager(37);
        for (int round = 0; round < 3; round++)
        {
            if (round == 0) CallBool(manager, "RequestAttempt");
            else Invoke(manager, "Tick", 5f);
            CallBool(manager, "NotifySpawnCompleted");
            Assert.That(CallBool(manager, "SubmitAnswer", true), Is.True);
        }
        Assert.That(State(manager), Is.EqualTo("ChallengeCompleted"));
        Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(3));
        Assert.That(CallBool(manager, "RequestAttempt"), Is.False);
    }

    private object CreateManager(int seed)
    {
        object deck = CreateDeck(seed);
        return Activator.CreateInstance(managerType, new[] { deck });
    }

private object CreateDeck(int seed)
{
    ConstructorInfo constructor = deckType.GetConstructors()[0];
    return constructor.Invoke(new object[] { GetProperty(bank, "Expressions"), new System.Random(seed) });
}

    private void StartRound(object manager)
    {
        Assert.That(CallBool(manager, "RequestAttempt"), Is.True);
        StartPreparedRound(manager);
    }

    private static void StartPreparedRound(object manager)
    {
        Assert.That(CallBool(manager, "NotifySpawnCompleted"), Is.True);
    }

    private static bool Evaluate(object expression, out int result)
    {
        MethodInfo method = RequireType("Fase4ExpressionEvaluator")
            .GetMethod("TryEvaluate", StaticFlags, null,
                new[] { RequireType("Fase4ExpressionDefinition"), typeof(int).MakeByRefType() }, null);
        object[] args = { expression, 0 };
        bool valid = (bool)method.Invoke(null, args);
        result = (int)args[1];
        return valid;
    }

    private static bool CallBool(object target, string name, params object[] args)
        => (bool)Invoke(target, name, args);

    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = Array.Find(target.GetType().GetMethods(InstanceFlags), candidate =>
        {
            if (candidate.Name != name) return false;
            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length != args.Length) return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null)
                {
                    if (parameters[i].ParameterType.IsValueType) return false;
                }
                else if (!parameters[i].ParameterType.IsInstanceOfType(args[i]))
                {
                    return false;
                }
            }
            return true;
        });
        Assert.That(method, Is.Not.Null, "Método ausente: " + name);
        return method.Invoke(target, args);
    }

    private static object GetProperty(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name, InstanceFlags);
        Assert.That(property, Is.Not.Null, "Propriedade ausente: " + name);
        return property.GetValue(target);
    }

    private static string State(object manager) => GetProperty(manager, "State").ToString();

    private static Type RequireType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException("Type not found: " + name);
    }


[Test]
public void ExpressionDistractorsRandomizeCountAndExcludeEveryRequiredValue()
{
    Type generator = RequireType("Fase4DistractorGenerator");
    MethodInfo method = generator.GetMethod("GenerateForExpression", StaticFlags);
    Assert.That(method, Is.Not.Null);
    object expression = expressions[0];
    MethodInfo traceMethod = evaluatorType.GetMethod("TryEvaluateWithIntermediateValues", StaticFlags);
    object[] traceArgs = { expression, 0, null };
    Assert.That((bool)traceMethod.Invoke(null, traceArgs), Is.True);
    var required = new List<int>((int[])GetProperty(expression, "Operands"));
    required.AddRange((int[])traceArgs[2]);

    for (int seed = 0; seed < 100; seed++)
    {
        int[] generated = (int[])method.Invoke(null, new object[] { expression, new System.Random(seed), null });
        Assert.That(generated.Length, Is.InRange(2, 3));
        Assert.That(generated.Distinct().Count(), Is.EqualTo(generated.Length));
        foreach (int value in generated)
        {
            Assert.That(value, Is.InRange(1, 50));
            Assert.That(required.Contains(value), Is.False);
            int nearest = 100;
            foreach (int source in required) nearest = Math.Min(nearest, Math.Abs(source - value));
            Assert.That(nearest, Is.LessThanOrEqualTo(3));
        }
    }
}


[Test]
public void Evaluator_ExposesThreeIntermediateResultsForDistractorSelection()
{
    MethodInfo method = evaluatorType.GetMethod("TryEvaluateWithIntermediateValues", StaticFlags);
    Assert.That(method, Is.Not.Null);
    for (int i = 0; i < expressions.Count; i++)
    {
        object[] args = { expressions[i], 0, null };
        bool valid = (bool)method.Invoke(null, args);
        Assert.That(valid, Is.True);
        int result = (int)args[1];
        int[] intermediates = (int[])args[2];
        Assert.That(intermediates.Length, Is.EqualTo(3));
        foreach (int value in intermediates) Assert.That(value, Is.InRange(1, 50));
        Assert.That(intermediates[intermediates.Length - 1], Is.EqualTo(result));
    }
}

[Test]
public void Tick_IgnoresNonFiniteAndNegativeDeltas()
{
    object manager = CreateManager(41);
    StartRound(manager);
    float before = (float)GetProperty(manager, "RemainingSeconds");

    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
        Invoke(manager, "Tick", invalid);

    Assert.That(State(manager), Is.EqualTo("Active"));
    Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(before));
}

[Test]
public void Tick_LargestFiniteDeltaClampsAndCompletesOnlyTheCurrentPhase()
{
    object activeManager = CreateManager(42);
    StartRound(activeManager);
    Invoke(activeManager, "Tick", float.MaxValue);
    Assert.That(State(activeManager), Is.EqualTo("FailureAndRespawn"));
    Assert.That((float)GetProperty(activeManager, "RemainingSeconds"), Is.Zero);

    object transitionManager = CreateManager(43);
    StartRound(transitionManager);
    CallBool(transitionManager, "SubmitAnswer", true);
    Invoke(transitionManager, "Tick", float.MaxValue);
    Assert.That(State(transitionManager), Is.EqualTo("Preparing"));
    Assert.That((int)GetProperty(transitionManager, "CompletedRounds"), Is.EqualTo(1));
}

[Test]
public void Tick_ExactZeroEndsActiveRoundAndExactTransitionBoundaryStartsNextRound()
{
    object activeManager = CreateManager(44);
    StartRound(activeManager);
    Invoke(activeManager, "Tick", 60f);
    Assert.That((float)GetProperty(activeManager, "RemainingSeconds"), Is.Zero);
    Assert.That(State(activeManager), Is.EqualTo("FailureAndRespawn"));

    object transitionManager = CreateManager(45);
    StartRound(transitionManager);
    CallBool(transitionManager, "SubmitAnswer", true);
    Invoke(transitionManager, "Tick", 5f);
    Assert.That(State(transitionManager), Is.EqualTo("Preparing"));
    Assert.That((int)GetProperty(transitionManager, "CompletedRounds"), Is.EqualTo(1));
}

[Test]
public void TimeChanged_ReentrantCorrectSubmissionCannotAwardBonusOrRoundTwice()
{
    object manager = CreateManager(46);
    StartRound(manager);
    bool reentered = false;
    SubscribeEvent(manager, "TimeChanged", _ =>
    {
        if (reentered) return;
        reentered = true;
        Assert.That(CallBool(manager, "SubmitAnswer", true), Is.False);
    });

    Assert.That(CallBool(manager, "SubmitAnswer", true), Is.True);
    Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(1));
    Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(95f));
    Assert.That(State(manager), Is.EqualTo("InterRoundTransition"));
}

[Test]
public void WrongAnswerRecorded_ReentrantSubmissionCannotCountAnExtraError()
{
    object manager = CreateManager(47);
    StartRound(manager);
    bool reentered = false;
    SubscribeEvent(manager, "WrongAnswerRecorded", _ =>
    {
        if (reentered) return;
        reentered = true;
        Assert.That(CallBool(manager, "SubmitAnswer", false), Is.False);
    });

    Assert.That(CallBool(manager, "SubmitAnswer", false), Is.True);
    Assert.That((int)GetProperty(manager, "ErrorsThisRound"), Is.EqualTo(1));
    Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(55f));
    Assert.That((bool)GetProperty(manager, "PistonsLogicallyActive"), Is.False);
    Assert.That(State(manager), Is.EqualTo("Active"));
}

[Test]
public void ExpressionSelected_ReentrantAttemptCannotDrawOrStartAnotherAttempt()
{
    object manager = CreateManager(48);
    int notifications = 0;
    SubscribeEvent(manager, "ExpressionSelected", _ =>
    {
        notifications++;
        Assert.That(State(manager), Is.EqualTo("Preparing"));
        Assert.That(CallBool(manager, "RequestAttempt"), Is.False);
        Assert.That(CallBool(manager, "NotifySpawnCompleted"), Is.False);
    });

    Assert.That(CallBool(manager, "RequestAttempt"), Is.True);
    Assert.That(notifications, Is.EqualTo(1));
    Assert.That(State(manager), Is.EqualTo("Preparing"));
}

[Test]
public void ReentrantSpawnOrRespawnCallbacksCannotAdvanceDuringEventDispatch()
{
    object manager = CreateManager(49);
    SubscribeEvent(manager, "StateChanged", _ =>
    {
        Assert.That(CallBool(manager, "NotifySpawnCompleted"), Is.False);
        Assert.That(CallBool(manager, "RequestAttempt"), Is.False);
    });

    Assert.That(CallBool(manager, "RequestAttempt"), Is.True);
    Assert.That(State(manager), Is.EqualTo("Preparing"));
    Assert.That(CallBool(manager, "NotifySpawnCompleted"), Is.True);
    Assert.That(State(manager), Is.EqualTo("Active"));
}

[Test]
public void StateChanged_ReentrantRespawnCannotEscapeFailureBeforeFailureNotification()
{
    object manager = CreateManager(52);
    StartRound(manager);
    SubscribeEvent(manager, "StateChanged", state =>
    {
        if (state.ToString() != "FailureAndRespawn") return;
        Assert.That(CallBool(manager, "NotifyRespawnCompleted"), Is.False);
        Assert.That(CallBool(manager, "RequestAttempt"), Is.False);
    });
    int failureNotifications = 0;
    SubscribeEvent(manager, "RoundAttemptFailed", _ =>
    {
        failureNotifications++;
        Assert.That(State(manager), Is.EqualTo("FailureAndRespawn"));
    });

    Invoke(manager, "Tick", 60f);
    Assert.That(failureNotifications, Is.EqualTo(1));
    Assert.That(State(manager), Is.EqualTo("FailureAndRespawn"));
}

[Test]
public void ExternalRespawnAbortHasNoErrorPenaltyAndKeepsCompletedRounds()
{
    object manager = CreateManager(53);
    StartRound(manager);
    Assert.That(CallBool(manager, "SubmitAnswer", true), Is.True);
    Invoke(manager, "Tick", 5f);
    StartPreparedRound(manager);
    Invoke(manager, "Tick", 12f);

    MethodInfo abort = managerType.GetMethod("AbortAttemptWithoutPenalty", InstanceFlags, null, Type.EmptyTypes, null);
    Assert.That(abort, Is.Not.Null, "O respawn por Void precisa encerrar a tentativa sem simular erro.");
    Assert.That((bool)abort.Invoke(manager, null), Is.True);
    Assert.That(State(manager), Is.EqualTo("FailureAndRespawn"));
    Assert.That((int)GetProperty(manager, "ErrorsThisRound"), Is.Zero);
    Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(1));

    Assert.That(CallBool(manager, "NotifyRespawnCompleted"), Is.True);
    Assert.That(CallBool(manager, "RequestAttempt"), Is.True);
    Assert.That((int)GetProperty(manager, "CurrentRound"), Is.EqualTo(2));
    Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(1));
    Assert.That((float)GetProperty(manager, "RemainingSeconds"), Is.EqualTo(60f));
}

[Test]
public void ExternalRespawnAbortIsRejectedOutsideAnActiveRound()
{
    object manager = CreateManager(54);
    MethodInfo abort = managerType.GetMethod("AbortAttemptWithoutPenalty", InstanceFlags, null, Type.EmptyTypes, null);
    Assert.That(abort, Is.Not.Null);
    Assert.That((bool)abort.Invoke(manager, null), Is.False);
    Assert.That(State(manager), Is.EqualTo("AwaitingInteraction"));
}

[Test]
public void NumericSubmissionAcceptsOnlyMatchingFinalValue()
{
    object manager = CreateManager(50);
    StartRound(manager);
    object expression = GetProperty(manager, "CurrentExpression");
    Assert.That(Evaluate(expression, out int expected), Is.True);
    MethodInfo submitNumeric = managerType.GetMethod("SubmitAnswer", InstanceFlags, null,
        new[] { typeof(int) }, null);
    Assert.That(submitNumeric, Is.Not.Null, "A API numérica deve centralizar a comparação do resultado final.");

    Assert.That((bool)submitNumeric.Invoke(manager, new object[] { expected }), Is.True);
    Assert.That((int)GetProperty(manager, "CompletedRounds"), Is.EqualTo(1));
    Assert.That(State(manager), Is.EqualTo("InterRoundTransition"));

    object wrongManager = CreateManager(51);
    StartRound(wrongManager);
    Assert.That(Evaluate(GetProperty(wrongManager, "CurrentExpression"), out int wrongExpected), Is.True);
    int wrongValue = wrongExpected == 50 ? 49 : wrongExpected + 1;
    Assert.That((bool)submitNumeric.Invoke(wrongManager, new object[] { wrongValue }), Is.True);
    Assert.That((int)GetProperty(wrongManager, "ErrorsThisRound"), Is.EqualTo(1));
    Assert.That(State(wrongManager), Is.EqualTo("Active"));
}

[Test]
public void BlackboardFormatsRoundTimerAsNonNegativeMinutesAndSeconds()
{
    Type viewType = RequireType("Fase4BlackboardView");
    MethodInfo format = viewType.GetMethod("FormatTime", StaticFlags, null, new[] { typeof(float) }, null);
    Assert.That(format, Is.Not.Null);
    Assert.That((string)format.Invoke(null, new object[] { 60f }), Is.EqualTo("01:00"));
    Assert.That((string)format.Invoke(null, new object[] { 35.1f }), Is.EqualTo("00:36"));
    Assert.That((string)format.Invoke(null, new object[] { 0f }), Is.EqualTo("00:00"));
    Assert.That((string)format.Invoke(null, new object[] { -2f }), Is.EqualTo("00:00"));
}

[Test]
public void ReceptacleInteraction_UsesExistingKeyboardEActionWithoutChangingItsBinding()
{
    UnityEngine.Object actions = AssetDatabase.LoadMainAssetAtPath("Assets/Other/InputSystem_Actions.inputactions");
    Assert.That(actions, Is.Not.Null);

    Type coordinatorType = RequireType("Fase4ArenaRoundCoordinator");
    MethodInfo resolve = coordinatorType.GetMethod("FindReceptacleInteractAction", StaticFlags);
    Assert.That(resolve, Is.Not.Null);

    object action = resolve.Invoke(null, new object[] { actions });
    Assert.That(action, Is.Not.Null);
    Assert.That((string)GetProperty(action, "name"), Is.EqualTo("Operators"));
    IEnumerable bindings = (IEnumerable)GetProperty(action, "bindings");
    bool hasKeyboardE = false;
    foreach (object binding in bindings)
        hasKeyboardE |= string.Equals((string)GetProperty(binding, "path"), "<Keyboard>/e", StringComparison.OrdinalIgnoreCase);

    Assert.That(hasKeyboardE, Is.True,
        "A mesma ação que o coordenador resolve precisa conter o binding de E; verificar o JSON isoladamente não prova essa relação.");
}

[Test]
public void BlackboardGlyphRotation_FacesTheBoardFrontAndKeepsTextUpright()
{
    Type viewType = RequireType("Fase4BlackboardView");
    MethodInfo calculate = viewType.GetMethod("CalculateGlyphFrontRotation", StaticFlags);
    Assert.That(calculate, Is.Not.Null);

    Vector3 boardFront = Vector3.left;
    Quaternion rotation = (Quaternion)calculate.Invoke(null, new object[] { boardFront });
    Vector3 visibleGlyphFront = -(rotation * Vector3.forward);

    Assert.That(Vector3.Dot(visibleGlyphFront, boardFront), Is.GreaterThan(0.999f));
    Assert.That(Vector3.Dot(rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f),
        "A correção de 180 graus não deve virar o texto verticalmente.");
    Assert.That(Vector3.Dot(rotation * Vector3.right, Vector3.back), Is.GreaterThan(0.999f),
        "A leitura horizontal deve corresponder à face frontal do quadro.");
}

private static void SubscribeEvent(object target, string eventName, Action<object> callback)
{
    EventInfo eventInfo = target.GetType().GetEvent(eventName, InstanceFlags);
    Assert.That(eventInfo, Is.Not.Null, "Evento ausente: " + eventName);
    ParameterInfo[] eventParameters = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();
    Assert.That(eventParameters.Length, Is.LessThanOrEqualTo(1), "Helper supports zero- or one-argument events.");
    ParameterExpression[] parameters = Array.ConvertAll(eventParameters,
        parameter => Expression.Parameter(parameter.ParameterType, parameter.Name));
    Expression callbackArgument = parameters.Length == 0
        ? Expression.Constant(null, typeof(object))
        : Expression.Convert(parameters[0], typeof(object));
    MethodCallExpression invoke = Expression.Call(Expression.Constant(callback),
        typeof(Action<object>).GetMethod("Invoke"), callbackArgument);
    Delegate handler = Expression.Lambda(eventInfo.EventHandlerType, invoke, parameters).Compile();
    eventInfo.AddEventHandler(target, handler);
}
}
