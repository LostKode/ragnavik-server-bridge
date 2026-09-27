using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

namespace RagnavikServerBridge;

internal sealed class WorldSaveAdapter
{
    private static WorldSaveAdapter? _instance;
    private readonly BridgePlugin _bridge;
    private readonly string _directory;
    private float _nextPoll;
    private string? _lastId;
    private static readonly MethodInfo? SaveMethod = typeof(ZNet).GetMethod("Save", new[] { typeof(bool), typeof(bool), typeof(bool) });

    private WorldSaveAdapter(BridgePlugin bridge)
    {
        _bridge = bridge;
        _directory = Path.Combine(Paths.ConfigPath, "RagnavikMaintenance");
        Directory.CreateDirectory(_directory);
    }

    internal static void Install(BridgePlugin bridge) => _instance = new WorldSaveAdapter(bridge);
    internal static void Tick() => _instance?.Update();
    internal static void Stop() => _instance = null;

    private void Update()
    {
        if (Time.realtimeSinceStartup < _nextPoll) return;
        _nextPoll = Time.realtimeSinceStartup + 1;
        var path = Path.Combine(_directory, "save-request.json");
        if (!File.Exists(path)) return;
        SaveRequest? request;
        try { request = JsonUtility.FromJson<SaveRequest>(File.ReadAllText(path)); }
        catch (Exception exception) { _bridge.Log.LogWarning("Unreadable world-save request: " + exception.Message); return; }
        if (request == null || request.id == _lastId || !Regex.IsMatch(request.id ?? "", "^[a-f0-9]{32}$")) return;
        _lastId = request.id;
        var resultPath = Path.Combine(_directory, "save-result.json");
        // A completed request is never replayed after a bridge/server restart.
        if (File.Exists(resultPath))
        {
            try { if (JsonUtility.FromJson<SaveResult>(File.ReadAllText(resultPath))?.id == request.id) return; }
            catch (Exception) { /* A corrupt receipt cannot authorize any deployment. */ }
        }
        var result = new SaveResult { id = request.id!, status = "failed" };
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (request.createdAt > now + 5 || now - request.createdAt > 30)
                throw new InvalidOperationException("World-save request expired or has an invalid timestamp.");
            var net = ZNet.instance;
            if (net == null || !net.IsServer() || !net.IsDedicated())
                throw new InvalidOperationException("World-save requests require a running dedicated server.");
            if (SaveMethod == null) throw new MissingMethodException("Unverified ZNet.Save signature.");
            if (net.IsSaving()) throw new InvalidOperationException("Another world save is in progress; retry with a new request after it completes.");
            var evidence = new WorldSaveEvidence();
            void Observe(string message, string stack, LogType type) => evidence.Observe(message);
            Application.logMessageReceivedThreaded += Observe;
            try
            {
                // Synchronous world-only save; never enter SaveWorldAndPlayerProfiles.
                SaveMethod.Invoke(net, new object[] { true, false, false });
            }
            finally { Application.logMessageReceivedThreaded -= Observe; }
            evidence.RequireSuccess();
            result.status = "completed";
            result.completedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        catch (Exception exception)
        {
            result.error = (exception.InnerException ?? exception).Message;
        }
        var temporary = resultPath + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(JsonUtility.ToJson(result));
            writer.Flush();
            stream.Flush(true);
        }
        if (File.Exists(resultPath)) File.Replace(temporary, resultPath, null);
        else File.Move(temporary, resultPath);
        _bridge.Log.LogInfo($"World-save request {result.id}: {result.status}{(result.error == "" ? "" : "; " + result.error)}");
    }

    [Serializable] private sealed class SaveRequest { public string id = ""; public long createdAt = 0; }
    [Serializable] private sealed class SaveResult
    {
        public string id = "";
        public string status = "";
        public string error = "";
        public long completedAt;
    }
}
