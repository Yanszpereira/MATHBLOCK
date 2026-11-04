using UnityEngine;

/// <summary>Armazena, apenas durante a partida atual, os resultados das quatro fases.</summary>
public static class ResultadosDasFases
{
    public const int QuantidadeDeFases = 4;

    private static readonly float[] percentuais = new float[QuantidadeDeFases + 1];
    private static readonly string[] notas = new string[QuantidadeDeFases + 1];
    private static readonly bool[] possuiResultado = new bool[QuantidadeDeFases + 1];

    public static bool NotaFinalDisponivel =>
        possuiResultado[1] && possuiResultado[2] && possuiResultado[3] && possuiResultado[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarSessao()
    {
        LimparResultados();
    }

    public static bool RegistrarResultado(int fase, float percentualTempoRestante)
    {
        if (!FaseValida(fase))
            return false;

        float percentualNormalizado = NormalizarPercentual(percentualTempoRestante);
        percentuais[fase] = percentualNormalizado;
        notas[fase] = CountdownTimer.CalculateNota(percentualNormalizado);
        possuiResultado[fase] = true;
        return true;
    }

    public static bool TryObterResultado(int fase, out ResultadoDaFase resultado)
    {
        if (!FaseValida(fase) || !possuiResultado[fase])
        {
            resultado = default;
            return false;
        }

        resultado = new ResultadoDaFase(fase, percentuais[fase], notas[fase]);
        return true;
    }

    public static void LimparResultados()
    {
        for (int fase = 1; fase <= QuantidadeDeFases; fase++)
        {
            percentuais[fase] = 0f;
            notas[fase] = null;
            possuiResultado[fase] = false;
        }
    }

    public static void IniciarNovaPartida()
    {
        LimparResultados();
    }

    public static bool TryObterResultadoFinal(out ResultadoFinal resultadoFinal)
    {
        if (!NotaFinalDisponivel)
        {
            resultadoFinal = default;
            return false;
        }

        float media = 0f;
        for (int fase = 1; fase <= QuantidadeDeFases; fase++)
            media += percentuais[fase];

        media /= QuantidadeDeFases;
        resultadoFinal = new ResultadoFinal(media, CountdownTimer.CalculateNota(media));
        return true;
    }

    private static bool FaseValida(int fase) =>
        fase >= 1 && fase <= QuantidadeDeFases;

    private static float NormalizarPercentual(float percentual)
    {
        if (float.IsNaN(percentual) || float.IsInfinity(percentual))
            return 0f;

        return Mathf.Clamp(percentual, 0f, 100f);
    }
}

public readonly struct ResultadoDaFase
{
    public int Fase { get; }
    public float PercentualTempoRestante { get; }
    public string Nota { get; }

    public ResultadoDaFase(int fase, float percentualTempoRestante, string nota)
    {
        Fase = fase;
        PercentualTempoRestante = percentualTempoRestante;
        Nota = nota;
    }
}

public readonly struct ResultadoFinal
{
    public float PercentualMedio { get; }
    public string Nota { get; }

    public ResultadoFinal(float percentualMedio, string nota)
    {
        PercentualMedio = percentualMedio;
        Nota = nota;
    }
}
