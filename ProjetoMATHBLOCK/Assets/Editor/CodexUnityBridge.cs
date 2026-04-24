#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class CodexUnityBridge
{
    [Serializable]
    private class BridgeRequest
    {
        public string command;
        public string path;
        public string query;
        public string folder;
        public int limit;
    }

    [Serializable]
    private class BridgeResponse
    {
        public bool ok;
        public string command;
        public string message;
        public string payloadJson;
    }

    [Serializable]
    private class SelectionPayload
    {
        public string type;
        public string name;
        public string assetPath;
        public string hierarchyPath;
        public string scenePath;
    }

    [Serializable]
    private class StatusPayload
    {
        public string projectPath;
        public string productName;
        public string unityVersion;
        public bool isPlaying;
        public bool isPaused;
        public bool isCompiling;
        public string activeScene;
        public SelectionPayload selection;
    }

    [Serializable]
    private class AssetEntry
    {
        public string path;
        public string guid;
        public string type;
        public string name;
    }

    [Serializable]
    private class AssetListPayload
    {
        public AssetEntry[] items;
    }

    [Serializable]
    private class SimplePayload
    {
        public string path;
        public string message;
    }

    private static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();
    private static readonly object LifecycleLock = new object();
    private static TcpListener Listener;
    private static Thread ListenerThread;
    private static bool IsRunning;
    private static int Port => ReadPortFromEnvironment();

    static CodexUnityBridge()
    {
        EditorApplication.update += DrainMainThreadQueue;
        EditorApplication.quitting += Shutdown;
        AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        StartIfNeeded();
    }

    private static int ReadPortFromEnvironment()
    {
        string rawPort = Environment.GetEnvironmentVariable("UNITY_BRIDGE_PORT");
        if (int.TryParse(rawPort, out int port) && port > 0 && port < 65536)
        {
            return port;
        }

        return 24680;
    }

    private static void StartIfNeeded()
    {
        lock (LifecycleLock)
        {
            if (IsRunning)
            {
                return;
            }

            try
            {
                Listener = new TcpListener(IPAddress.Loopback, Port);
                Listener.Start();
                IsRunning = true;
                ListenerThread = new Thread(ListenLoop);
                ListenerThread.IsBackground = true;
                ListenerThread.Start();
                Debug.Log($"Codex Unity bridge ativo em 127.0.0.1:{Port}");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                Debug.LogError($"Falha ao iniciar bridge do Codex Unity: {ex.Message}");
            }
        }
    }

    private static void Shutdown()
    {
        lock (LifecycleLock)
        {
            IsRunning = false;
            try
            {
                Listener?.Stop();
            }
            catch
            {
                // Ignorado: o editor esta encerrando.
            }
        }
    }

    private static void ListenLoop()
    {
        while (IsRunning)
        {
            try
            {
                using (TcpClient client = Listener.AcceptTcpClient())
                {
                    HandleClient(client);
                }
            }
            catch (SocketException)
            {
                if (!IsRunning)
                {
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro no bridge Unity: {ex.Message}");
            }
        }
    }

    private static void HandleClient(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            string raw = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            BridgeRequest request = JsonUtility.FromJson<BridgeRequest>(raw);
            BridgeResponse response = ExecuteOnMainThread(() => HandleRequest(request));
            writer.WriteLine(JsonUtility.ToJson(response));
        }
    }

    private static BridgeResponse ExecuteOnMainThread(Func<BridgeResponse> action, int timeoutMs = 10000)
    {
        ManualResetEventSlim gate = new ManualResetEventSlim(false);
        BridgeResponse response = null;

        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                response = action();
            }
            catch (Exception ex)
            {
                response = ErrorResponse("exception", ex.Message);
            }
            finally
            {
                gate.Set();
            }
        });

        if (!gate.Wait(timeoutMs))
        {
            return ErrorResponse("timeout", "O Unity bridge expirou aguardando a main thread.");
        }

        return response ?? ErrorResponse("empty", "Resposta vazia do Unity bridge.");
    }

    private static void DrainMainThreadQueue()
    {
        while (MainThreadQueue.TryDequeue(out Action action))
        {
            action();
        }
    }

    private static BridgeResponse HandleRequest(BridgeRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.command))
        {
            return ErrorResponse("invalid_request", "Requisicao invalida.");
        }

        string command = request.command.Trim().ToLowerInvariant();
        switch (command)
        {
            case "status":
                return OkResponse("status", "Status lido com sucesso.", BuildStatusPayload());
            case "list_assets":
                return OkResponse("list_assets", "Assets listados com sucesso.", BuildAssetListPayload(request.query, request.folder, request.limit, false));
            case "list_scenes":
                return OkResponse("list_scenes", "Cenas listadas com sucesso.", BuildAssetListPayload(request.query, request.folder, request.limit, true));
            case "open_asset":
                return OpenAsset(request.path);
            case "select_asset":
                return SelectAsset(request.path);
            case "open_scene":
                return OpenScene(request.path);
            case "refresh_assets":
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                return OkResponse("refresh_assets", "Assets atualizados com sucesso.", new SimplePayload { message = "Assets atualizados com sucesso." });
            default:
                return ErrorResponse(command, $"Comando desconhecido: {request.command}");
        }
    }

    private static BridgeResponse OpenAsset(string path)
    {
        string normalizedPath = NormalizeAssetPath(path);
        if (normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            return OpenScene(normalizedPath);
        }

        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalizedPath);
        if (asset == null)
        {
            return ErrorResponse("open_asset", $"Asset nao encontrado: {normalizedPath}");
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        return OkResponse(
            "open_asset",
            $"Asset aberto: {normalizedPath}",
            new SimplePayload { path = normalizedPath, message = "Asset aberto no editor." }
        );
    }

    private static BridgeResponse SelectAsset(string path)
    {
        string normalizedPath = NormalizeAssetPath(path);
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalizedPath);
        if (asset == null)
        {
            return ErrorResponse("select_asset", $"Asset nao encontrado: {normalizedPath}");
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        return OkResponse(
            "select_asset",
            $"Asset selecionado: {normalizedPath}",
            new SimplePayload { path = normalizedPath, message = "Asset selecionado no Project." }
        );
    }

    private static BridgeResponse OpenScene(string path)
    {
        string normalizedPath = NormalizeAssetPath(path);
        if (!normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResponse("open_scene", $"Caminho nao aponta para uma cena Unity: {normalizedPath}");
        }

        UnityEngine.Object sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalizedPath);
        if (sceneAsset == null)
        {
            return ErrorResponse("open_scene", $"Cena nao encontrada: {normalizedPath}");
        }

        Scene scene = EditorSceneManager.OpenScene(normalizedPath, OpenSceneMode.Single);
        return OkResponse(
            "open_scene",
            $"Cena aberta: {scene.path}",
            new SimplePayload { path = scene.path, message = "Cena aberta no editor." }
        );
    }

    private static StatusPayload BuildStatusPayload()
    {
        UnityEngine.Object activeObject = Selection.activeObject;
        GameObject activeGameObject = Selection.activeGameObject;
        SelectionPayload selection = new SelectionPayload();

        if (activeGameObject != null)
        {
            selection.type = "GameObject";
            selection.name = activeGameObject.name;
            selection.assetPath = AssetDatabase.GetAssetPath(activeGameObject);
            selection.hierarchyPath = BuildHierarchyPath(activeGameObject.transform);
            selection.scenePath = activeGameObject.scene.path;
        }
        else if (activeObject != null)
        {
            selection.type = activeObject.GetType().Name;
            selection.name = activeObject.name;
            selection.assetPath = AssetDatabase.GetAssetPath(activeObject);
            selection.hierarchyPath = "";
            selection.scenePath = "";
        }
        else
        {
            selection.type = "";
            selection.name = "";
            selection.assetPath = "";
            selection.hierarchyPath = "";
            selection.scenePath = "";
        }

        StatusPayload payload = new StatusPayload
        {
            projectPath = Application.dataPath,
            productName = Application.productName,
            unityVersion = Application.unityVersion,
            isPlaying = EditorApplication.isPlaying,
            isPaused = EditorApplication.isPaused,
            isCompiling = EditorApplication.isCompiling,
            activeScene = SceneManager.GetActiveScene().path,
            selection = selection,
        };
        return payload;
    }

    private static AssetListPayload BuildAssetListPayload(string query, string folder, int limit, bool scenesOnly)
    {
        string normalizedFolder = string.IsNullOrWhiteSpace(folder) ? string.Empty : NormalizeAssetPath(folder);
        string searchQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        int safeLimit = Mathf.Clamp(limit <= 0 ? 25 : limit, 1, 100);

        string filter = scenesOnly ? "t:Scene" : searchQuery;
        string[] guids = string.IsNullOrWhiteSpace(normalizedFolder)
            ? AssetDatabase.FindAssets(filter)
            : AssetDatabase.FindAssets(filter, new[] { normalizedFolder });

        AssetEntry[] items = new AssetEntry[Mathf.Min(guids.Length, safeLimit)];
        int count = 0;
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            if (scenesOnly && !assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!scenesOnly && !string.IsNullOrWhiteSpace(searchQuery))
            {
                string assetName = Path.GetFileNameWithoutExtension(assetPath);
                if (assetName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) < 0 &&
                    assetPath.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            if (count >= safeLimit)
            {
                break;
            }

            items[count] = new AssetEntry
            {
                path = assetPath,
                guid = guid,
                type = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.Name ?? "",
                name = Path.GetFileNameWithoutExtension(assetPath),
            };
            count++;
        }

        if (count != items.Length)
        {
            Array.Resize(ref items, count);
        }

        return new AssetListPayload { items = items };
    }

    private static BridgeResponse OkResponse(string command, string message, object payload)
    {
        string payloadJson = payload == null ? "" : JsonUtility.ToJson(payload);
        return new BridgeResponse
        {
            ok = true,
            command = command,
            message = message,
            payloadJson = payloadJson,
        };
    }

    private static BridgeResponse ErrorResponse(string command, string message)
    {
        return new BridgeResponse
        {
            ok = false,
            command = command,
            message = message,
            payloadJson = "",
        };
    }

    private static string NormalizeAssetPath(string path)
    {
        string normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Caminho vazio.");
        }

        if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Caminho fora de Assets/Packages: {normalized}");
        }

        return normalized;
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(transform.name);
        Transform current = transform.parent;
        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }
}
#endif
