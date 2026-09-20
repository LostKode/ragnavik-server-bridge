using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace RagnavikServerBridge;

internal static class CatosAdapter
{
    private const string CatosGuid = "com.catosvalheim.anticheat";
    private static BridgePlugin? _bridge;
    private static string _endpoint = "";
    private static string _server = "Ragnavik";
    public static void Install(BridgePlugin bridge, string server, string endpoint)
    {
        _bridge = bridge; _server = string.IsNullOrWhiteSpace(server) ? "Ragnavik" : server.Trim(); _endpoint = endpoint;
        try
        {
            var poster = AccessTools.TypeByName("CatosAntiCheat.DiscordPoster");
            if (poster == null) { bridge.Log.LogError("Catos notification hooks are unavailable; Catos adapter is disabled and enforcement is unaffected."); return; }
            var mismatch = AccessTools.Method(poster, "PostMismatchKick", new[] { typeof(string), typeof(ulong), typeof(List<string>) });
            var timeout = AccessTools.Method(poster, "PostTimeoutKick", new[] { typeof(string), typeof(ulong), typeof(float) });
            if (mismatch == null || timeout == null) { bridge.Log.LogError("Catos notification hooks are unavailable; Catos adapter is disabled and enforcement is unaffected."); return; }
            var harmony = new Harmony(BridgePlugin.ModGuid + ".catos");
            harmony.Patch(mismatch, postfix: new HarmonyMethod(typeof(CatosAdapter), nameof(AfterMismatch)));
            harmony.Patch(timeout, postfix: new HarmonyMethod(typeof(CatosAdapter), nameof(AfterTimeout)));
            bridge.Log.LogInfo($"Catos adapter installed for {CatosGuid}.");
        }
        catch (Exception exception) { bridge.Log.LogError($"Catos adapter failed compatibility checks: {exception.GetType().Name}: {exception.Message}"); }
    }
    private static void AfterMismatch(string __0, ulong __1, List<string> __2)
    {
        try
        {
            var problems = new List<string>();
            if (__2 != null) foreach (var value in __2) if (value != null && problems.Count < 20) problems.Add(value);
            if (problems.Count == 0) problems.Add("unspecified Catos rejection");
            Enqueue("mismatch", __0, __1, problems, null);
        }
        catch (Exception exception) { _bridge?.Log.LogWarning($"Could not capture Catos mismatch: {exception.Message}"); }
    }
    private static void AfterTimeout(string __0, ulong __1, float __2)
    {
        try { Enqueue("timeout", __0, __1, null, __2); }
        catch (Exception exception) { _bridge?.Log.LogWarning($"Could not capture Catos timeout: {exception.Message}"); }
    }
    private static void Enqueue(string kind, string playerName, ulong steamId, IList<string>? problems, float? timeout)
    {
        var id = Guid.NewGuid().ToString("N");
        var json = new StringBuilder("{\"eventId\":\"").Append(id).Append("\",\"type\":\"").Append(kind)
            .Append("\",\"server\":\"").Append(Escape(_server)).Append("\",\"steamId\":\"").Append(steamId.ToString(CultureInfo.InvariantCulture))
            .Append("\",\"characterName\":\"").Append(Escape(playerName)).Append("\",\"catosVersion\":\"").Append(Escape(ReadCatosVersion())).Append('"');
        if (kind == "mismatch")
        {
            json.Append(",\"problems\":[");
            for (var i = 0; i < problems!.Count; i++) { if (i > 0) json.Append(','); json.Append('"').Append(Escape(Limit(problems[i], 300))).Append('"'); }
            json.Append(']');
        }
        else json.Append(",\"timeoutSeconds\":").Append(timeout.GetValueOrDefault().ToString("0.###", CultureInfo.InvariantCulture));
        _bridge?.Enqueue("catos", _endpoint, id, json.Append('}').ToString());
    }
    private static string ReadCatosVersion() { try { return AccessTools.Field(AccessTools.TypeByName("CatosAntiCheat.Plugin"), "ModVersion")?.GetRawConstantValue()?.ToString() ?? ""; } catch { return ""; } }
    private static string Limit(string value, int maximum) => string.IsNullOrEmpty(value) || value.Length <= maximum ? value ?? "" : value.Substring(0, maximum);
    private static string Escape(string value)
    {
        var output = new StringBuilder();
        foreach (var c in value ?? "") switch (c) { case '"': output.Append("\\\""); break; case '\\': output.Append("\\\\"); break; case '\n': output.Append("\\n"); break; case '\r': output.Append("\\r"); break; case '\t': output.Append("\\t"); break; default: if (c < 0x20) output.Append("\\u").Append(((int)c).ToString("X4")); else output.Append(c); break; }
        return output.ToString();
    }
}
