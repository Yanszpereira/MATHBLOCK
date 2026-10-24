using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;

/// <summary>Consulta o Console nativo sem limpar nem esconder suas mensagens.</summary>
public static class RevisaoConsole
{
    [MenuItem("MATHBLOCK/Revisao visual/Relatar avisos do Console")]
    private static void Relatar()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        Assembly assembly = typeof(Editor).Assembly;
        Type entradas = assembly.GetType("UnityEditor.LogEntries"), entrada = assembly.GetType("UnityEditor.LogEntry");
        Type modo = assembly.GetType("UnityEditor.LogMode");
        var linhas = new HashSet<string>();
        entradas.GetMethod("StartGettingEntries", flags).Invoke(null, null);
        try
        {
            int quantidade = Math.Min(2000, (int)entradas.GetMethod("GetCount", flags).Invoke(null, null));
            object item = Activator.CreateInstance(entrada);
            for (int i = 0; i < quantidade; i++)
            {
                entradas.GetMethod("GetEntryInternal", flags).Invoke(null, new[] { (object)i, item });
                int valor = (int)entrada.GetField("mode", flags).GetValue(item);
                string severidade = modo != null ? Enum.ToObject(modo, valor).ToString() : valor.ToString();
                if (modo != null && !severidade.Contains("Warning") && !severidade.Contains("Error") && !severidade.Contains("Exception")) continue;
                string mensagem = (string)entrada.GetField("message", flags).GetValue(item);
                linhas.Add(severidade + " | " + mensagem.Split('\n')[0].Trim());
            }
        }
        finally { entradas.GetMethod("EndGettingEntries", flags).Invoke(null, null); }
        File.WriteAllLines("C:/Users/arbustim/Documents/Codex/2026-08-14/d/console-nativo.txt", linhas);
    }
}
