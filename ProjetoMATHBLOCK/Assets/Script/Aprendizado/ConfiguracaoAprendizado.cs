using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ConfiguracaoAprendizado", menuName = "MATHBLOCK/Aprendizado/Configuracao")]
public sealed class ConfiguracaoAprendizado : ScriptableObject
{
    public enum TipoGatilho { Porta, Elevador, Balanca }

    [Serializable]
    public sealed class PesoConteudo
    {
        public ConteudoMatematico conteudo;
        [Min(0.01f)] public float peso = 1f;
    }

    [Serializable]
    public sealed class Subfase
    {
        public string id;
        public string cena;
        public string nome;
        public TipoGatilho gatilho;
        [Tooltip("Ordem do componente do mesmo tipo na cena, comecando em zero.")]
        [Min(0)] public int indiceNaCena;
        public List<PesoConteudo> conteudos = new List<PesoConteudo>();
    }

    [SerializeField] private List<Subfase> subfases = new List<Subfase>();
    public IReadOnlyList<Subfase> Subfases => subfases;

    private void OnEnable()
    {
        if (subfases == null || subfases.Count == 0)
            AplicarPadrao();
    }

    public IEnumerable<Subfase> ObterDaCena(string cena)
    {
        foreach (Subfase subfase in subfases)
            if (subfase != null && string.Equals(subfase.cena, cena, StringComparison.Ordinal))
                yield return subfase;
    }

    public static ConfiguracaoAprendizado CriarPadraoEmMemoria()
    {
        ConfiguracaoAprendizado c = CreateInstance<ConfiguracaoAprendizado>();
        c.AplicarPadrao();
        return c;
    }

    [ContextMenu("Restaurar mapeamento padrao das Fases 1 a 3")]
    public void AplicarPadrao()
    {
        subfases = new List<Subfase>
        {
            Criar("fase1_porta1", "Fase 1", "Adicao e Subtracao", TipoGatilho.Porta, 0,
                (ConteudoMatematico.Operadores, .55f)),
            Criar("fase1_porta2", "Fase 1", "Operacoes com Multiplicacao", TipoGatilho.Porta, 1,
                (ConteudoMatematico.Operadores, .7f)),
            Criar("fase1_porta3", "Fase 1", "Operacoes com Divisao", TipoGatilho.Porta, 2,
                (ConteudoMatematico.Operadores, .85f)),
            Criar("fase2_elevador", "Fase 2", "Numero, Escala e Espaco", TipoGatilho.Elevador, 0,
                (ConteudoMatematico.EscalaEEspaco, 1f)),
            Criar("fase3_balanca1", "Fase 3", "Igualdade e Equilibrio I", TipoGatilho.Balanca, 0,
                (ConteudoMatematico.Equacoes, .85f)),
            Criar("fase3_balanca2", "Fase 3", "Igualdade e Equilibrio II", TipoGatilho.Balanca, 1,
                (ConteudoMatematico.Equacoes, 1f))
        };
    }

    private static Subfase Criar(string id, string cena, string nome, TipoGatilho tipo, int indice,
        params (ConteudoMatematico conteudo, float peso)[] pesos)
    {
        Subfase s = new Subfase { id = id, cena = cena, nome = nome, gatilho = tipo, indiceNaCena = indice };
        foreach (var p in pesos)
            s.conteudos.Add(new PesoConteudo { conteudo = p.conteudo, peso = p.peso });
        return s;
    }
}
