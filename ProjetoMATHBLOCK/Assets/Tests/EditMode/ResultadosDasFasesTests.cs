using System;
using System.Reflection;
using NUnit.Framework;

public class ResultadosDasFasesTests
{
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public;
    private Type storeType;
    private MethodInfo registrar;
    private MethodInfo limpar;
    private MethodInfo iniciarNovaPartida;
    private MethodInfo obterResultado;
    private MethodInfo obterFinal;
    private PropertyInfo notaFinalDisponivel;

    [SetUp]
    public void SetUp()
    {
        storeType = Type.GetType("ResultadosDasFases, Assembly-CSharp");
        Assert.That(storeType, Is.Not.Null);
        registrar = storeType.GetMethod("RegistrarResultado", StaticFlags);
        limpar = storeType.GetMethod("LimparResultados", StaticFlags);
        iniciarNovaPartida = storeType.GetMethod("IniciarNovaPartida", StaticFlags);
        obterResultado = storeType.GetMethod("TryObterResultado", StaticFlags);
        obterFinal = storeType.GetMethod("TryObterResultadoFinal", StaticFlags);
        notaFinalDisponivel = storeType.GetProperty("NotaFinalDisponivel", StaticFlags);
        Assert.That(registrar, Is.Not.Null);
        Assert.That(limpar, Is.Not.Null);
        Assert.That(iniciarNovaPartida, Is.Not.Null);
        Assert.That(obterResultado, Is.Not.Null);
        Assert.That(obterFinal, Is.Not.Null);
        Assert.That(notaFinalDisponivel, Is.Not.Null);
        Limpar();
    }

    [Test]
    public void RegistrarResultado_StoresAndReplacesPhaseResult()
    {
        Assert.That(Registrar(1, 80f), Is.True);
        Assert.That(TryObter(1, out float percentual, out string nota), Is.True);
        Assert.That(percentual, Is.EqualTo(80f));
        Assert.That(nota, Is.EqualTo("A"));

        Assert.That(Registrar(1, 59f), Is.True);
        Assert.That(TryObter(1, out percentual, out nota), Is.True);
        Assert.That(percentual, Is.EqualTo(59f));
        Assert.That(nota, Is.EqualTo("C"));
    }

    [Test]
    public void LimparResultadosAndIniciarNovaPartida_RemoveAllPhaseResults()
    {
        Registrar(1, 100f);
        Registrar(2, 80f);
        Limpar();
        Assert.That(TryObter(1, out _, out _), Is.False);
        Assert.That(TryObter(2, out _, out _), Is.False);

        Registrar(3, 60f);
        iniciarNovaPartida.Invoke(null, null);
        Assert.That(TryObter(3, out _, out _), Is.False);
    }

    [Test]
    public void ResultadoFinal_IsUnavailableUntilAllFourPhasesHaveResults()
    {
        Registrar(1, 100f);
        Registrar(2, 80f);
        Registrar(3, 60f);

        Assert.That((bool)notaFinalDisponivel.GetValue(null), Is.False);
        Assert.That(TryObterFinal(out _, out _), Is.False);
    }

    [Test]
    public void ResultadoFinal_AveragesFourPhasesEquallyAndUsesGradeScale()
    {
        Registrar(1, 100f);
        Registrar(2, 80f);
        Registrar(3, 60f);
        Registrar(4, 40f);

        Assert.That((bool)notaFinalDisponivel.GetValue(null), Is.True);
        Assert.That(TryObterFinal(out float media, out string nota), Is.True);
        Assert.That(media, Is.EqualTo(70f));
        Assert.That(nota, Is.EqualTo("B"));
    }

    [Test]
    public void RegistrarResultado_InvalidPhaseIsRejectedAndPercentageIsNormalized()
    {
        Assert.That(Registrar(0, 50f), Is.False);
        Assert.That(Registrar(5, 50f), Is.False);

        Assert.That(Registrar(1, float.NaN), Is.True);
        Assert.That(TryObter(1, out float percentual, out string nota), Is.True);
        Assert.That(percentual, Is.Zero);
        Assert.That(nota, Is.EqualTo("D"));

        Assert.That(Registrar(2, 150f), Is.True);
        Assert.That(TryObter(2, out percentual, out nota), Is.True);
        Assert.That(percentual, Is.EqualTo(100f));
        Assert.That(nota, Is.EqualTo("A"));
    }

    private bool Registrar(int fase, float percentual) =>
        (bool)registrar.Invoke(null, new object[] { fase, percentual });

    private void Limpar() => limpar.Invoke(null, null);

    private bool TryObter(int fase, out float percentual, out string nota)
    {
        object[] args = { fase, null };
        bool found = (bool)obterResultado.Invoke(null, args);
        if (!found)
        {
            percentual = 0f;
            nota = null;
            return false;
        }

        object resultado = args[1];
        Type type = resultado.GetType();
        percentual = (float)type.GetProperty("PercentualTempoRestante").GetValue(resultado);
        nota = (string)type.GetProperty("Nota").GetValue(resultado);
        return true;
    }

    private bool TryObterFinal(out float media, out string nota)
    {
        object[] args = { null };
        bool found = (bool)obterFinal.Invoke(null, args);
        if (!found)
        {
            media = 0f;
            nota = null;
            return false;
        }

        object resultado = args[0];
        Type type = resultado.GetType();
        media = (float)type.GetProperty("PercentualMedio").GetValue(resultado);
        nota = (string)type.GetProperty("Nota").GetValue(resultado);
        return true;
    }
}
