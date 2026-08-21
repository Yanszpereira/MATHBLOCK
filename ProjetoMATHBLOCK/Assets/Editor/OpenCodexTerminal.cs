#if UNITY_EDITOR_WIN
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class OpenCodexTerminal
{
    [MenuItem("MATHBLOCK/Codex/Abrir Codex no CMD", priority = 5)]
    private static void OpenCodex()
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string command = $"cd /d \"{projectPath}\" && codex";

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/K \"{command}\"",
            UseShellExecute = true,
            WorkingDirectory = projectPath,
            WindowStyle = ProcessWindowStyle.Normal
        });
    }

    [MenuItem("MATHBLOCK/Codex/Abrir somente o CMD", priority = 6)]
    private static void OpenTerminalOnly()
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/K cd /d \"{projectPath}\"",
            UseShellExecute = true,
            WorkingDirectory = projectPath,
            WindowStyle = ProcessWindowStyle.Normal
        });
    }
}
#endif
