using System;
using System.Reflection;
using NUnit.Framework;

public class CountdownTimerGradeTests
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public;
    private Type timerType;
    private MethodInfo percentageMethod;
    private MethodInfo gradeMethod;

    [SetUp]
    public void SetUp()
    {
        timerType = Type.GetType("CountdownTimer, Assembly-CSharp");
        Assert.That(timerType, Is.Not.Null, "CountdownTimer não foi compilado em Assembly-CSharp.");

        percentageMethod = timerType.GetMethod("CalculatePercentualTempoRestante", StaticFlags);
        gradeMethod = timerType.GetMethod("CalculateNota", StaticFlags);
        Assert.That(percentageMethod, Is.Not.Null);
        Assert.That(gradeMethod, Is.Not.Null);
    }

    [TestCase(300f, 300f, 100f)]
    [TestCase(300f, 240f, 80f)]
    [TestCase(300f, 239.999f, 79.99967f)]
    [TestCase(300f, 180f, 60f)]
    [TestCase(300f, 179.999f, 59.99967f)]
    [TestCase(300f, 120f, 40f)]
    [TestCase(300f, 119.999f, 39.99967f)]
    [TestCase(300f, 0f, 0f)]
    public void CalculatePercentualTempoRestante_UsesConfiguredInitialTime(
        float tempoInicial,
        float tempoRestante,
        float expectedPercentual)
    {
        Assert.That(Percentual(tempoInicial, tempoRestante),
            Is.EqualTo(expectedPercentual).Within(0.001f));
    }

    [TestCase(80f, "A")]
    [TestCase(79.999f, "B")]
    [TestCase(60f, "B")]
    [TestCase(59.999f, "C")]
    [TestCase(40f, "C")]
    [TestCase(39.999f, "D")]
    [TestCase(0f, "D")]
    [TestCase(-1f, "D")]
    [TestCase(100f, "A")]
    public void CalculateNota_RespectsGradeBoundaries(float percentual, string expectedNota)
    {
        Assert.That(Nota(percentual), Is.EqualTo(expectedNota));
    }

    [TestCase(0f, 30f)]
    [TestCase(-1f, 30f)]
    [TestCase(float.NaN, 30f)]
    [TestCase(float.PositiveInfinity, 30f)]
    [TestCase(300f, float.NaN)]
    [TestCase(300f, float.PositiveInfinity)]
    public void CalculatePercentualTempoRestante_InvalidValuesReturnZero(float tempoInicial, float tempoRestante)
    {
        Assert.That(Percentual(tempoInicial, tempoRestante), Is.Zero);
    }

    [Test]
    public void CalculatePercentualTempoRestante_ClampsRemainingTime()
    {
        Assert.That(Percentual(300f, -1f), Is.Zero);
        Assert.That(Percentual(300f, 600f), Is.EqualTo(100f));
    }

    private float Percentual(float tempoInicial, float tempoRestante) =>
        (float)percentageMethod.Invoke(null, new object[] { tempoInicial, tempoRestante });

    private string Nota(float percentual) =>
        (string)gradeMethod.Invoke(null, new object[] { percentual });
}
