using System;
using System.Collections;
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
            var mismatch = AccessTools.Method(poster, "PostMismatchKick");
            var timeout = AccessTools.Method(poster, "PostTimeoutKick");
            if (poster == null || mismatch == null || timeout == null) { bridge.Log.LogError("Catos notification hooks are unavailable; Catos adapter is disabled and enforcement is unaffected."); return; }
            var harmony = new Harmony(BridgePlugin.ModGuid + ".catos");
            harmony.Patch(mismatch, postfix: new HarmonyMethod(typeof(CatosAdapter), nameof(AfterMismatch)));
            harmony.Patch(timeout, postfix: new HarmonyMethod(typeof(CatosAdapter), nameof(AfterTimeout)));
            bridge.Log.LogInfo($"Catos adapter installed for {CatosGuid}.");
        }
        catch (Exception exception) { bridge.Log.LogError($"Catos adapter failed compatibility checks: {exception.GetType().Name}: {exception.Message}"); }
    }
    private static void AfterMismatch(object __0, object __1)
    {
        try
        {
            var problems = new List<string>();
            if (__1 is IEnumerable values) foreach (var value in values) if (value != null && problems.Count < 20) problems.Add(value.ToString());
            if (problems.Count == 0) problems.Add("unspecified Catos rejection");
            Enqueue("mismatch", __0, problems, null);
        }
        catch (Exception exception) { _bridge?.Log.LogWarning($"Could not capture Catos mismatch: {exception.Message}"); }
    }
    private static void AfterTimeout(object __0, float __1)
    {
        try { Enqueue("timeout", __0, null, __1); }
        catch (Exception exception) { _bridge?.Log.LogWarning($"Could not capture Catos timeout: {exception.Message}"); }
    }
    private static void Enqueue(string kind, object peer, IList<string>? problems, float? timeout)
    {
        var id = Guid.NewGuid().ToString("N");
        var json = new StringBuilder("{\"eventId\":\"").Append(id).Append("\",\"type\":\"").Append(kind)
            .Append("\",\"server\":\"").Append(Escape(_server)).Append("\",\"steamId\":\"").Append(Escape(ResolveSteamId(peer)))
            .Append("\",\"characterName\":\"").Append(Escape(ReadPeerName(peer))).Append("\",\"catosVersion\":\"").Append(Escape(ReadCatosVersion())).Append('"');
        if (kind == "mismatch")
        {
            json.Append(",\"problems\":[");
            for (var i = 0; i < problems!.Count; i++) { if (i > 0) json.Append(','); json.Append('"').Append(Escape(Limit(problems[i], 300))).Append('"'); }
            json.Append(']');
        }
        else json.Append(",\"timeoutSeconds\":").Append(timeout.GetValueOrDefault().ToString("0.###", CultureInfo.InvariantCulture));
        _bridge?.Enqueue("catos", _endpoint, id, json.Append('}').ToString());
    }
    private static string ResolveSteamId(object peer) { try { return AccessTools.Method(AccessTools.TypeByName("CatosAntiCheat.AdminCheck"), "ResolveSteamId")?.Invoke(null, new[] { peer }) as string ?? ""; } catch { return ""; } }
    private static string ReadPeerName(object peer) { try { return AccessTools.Field(peer?.GetType(), "m_playerName")?.GetValue(peer) as string ?? ""; } catch { return ""; } }
    private static string ReadCatosVersion() { try { return AccessTools.Field(AccessTools.TypeByName("CatosAntiCheat.Plugin"), "ModVersion")?.GetRawConstantValue()?.ToString() ?? ""; } catch { return ""; } }
    private static string Limit(string value, int maximum) => string.IsNullOrEmpty(value) || value.Length <= maximum ? value ?? "" : value.Substring(0, maximum);
    private static string Escape(string value)
    {
        var output = new StringBuilder();
        foreach (var c in value ?? "") switch (c) { case '"': output.Append("\\\""); break; case '\\': output.Append("\\\\"); break; case '\n': output.Append("\\n"); break; case '\r': output.Append("\\r"); break; case '\t': output.Append("\\t"); break; default: if (c < 0x20) output.Append("\\u").Append(((int)c).ToString("X4")); else output.Append(c); break; }
        return output.ToString();
    }
}
