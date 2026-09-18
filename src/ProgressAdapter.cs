using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace RagnavikServerBridge;

internal sealed class ProgressAdapter
{
    private static ProgressAdapter? _instance;
    private readonly BridgePlugin _bridge;
    private readonly string _server;
    private readonly string _endpoint;
    private readonly FieldInfo? _globalKeys = AccessTools.Field(typeof(ZoneSystem), "m_globalKeys");
    private readonly ConfigEntry<int> _checkSeconds;
    private readonly ConfigEntry<int> _heartbeatSeconds;
    private readonly ConfigEntry<int> _milestoneStep;
    private readonly ConfigEntry<float> _participantRadius;
    private readonly ConfigEntry<bool> _reportBosses;
    private readonly ConfigEntry<bool> _reportLevels;
    private readonly List<BossKill> _pending = new();
    private readonly HashSet<string> _seenDeaths = new(StringComparer.Ordinal);
    private float _nextCheck;
    private float _nextHeartbeat;
    private string _lastSignature = "";

    private ProgressAdapter(BridgePlugin bridge, ConfigFile config, string server, string endpoint)
    {
        _bridge = bridge; _server = server; _endpoint = endpoint;
        _milestoneStep = config.Bind("Progress", "EpicMMOLevelStep", 10, new ConfigDescription("EpicMMO reporting interval.", new AcceptableValueRange<int>(1, 100)));
        _reportBosses = config.Bind("Progress", "ReportBossDefeats", true, "Report boss defeats and participants.");
        _reportLevels = config.Bind("Progress", "ReportEpicMMOLevels", true, "Report connected character level milestones.");
        _participantRadius = config.Bind("Progress", "BossParticipantRadius", 100f, new ConfigDescription("Boss participant radius in meters.", new AcceptableValueRange<float>(1f, 500f)));
        _checkSeconds = config.Bind("Progress", "CheckSeconds", 30, new ConfigDescription("Seconds between checks.", new AcceptableValueRange<int>(10, 600)));
        _heartbeatSeconds = config.Bind("Progress", "HeartbeatSeconds", 3600, new ConfigDescription("Seconds between unchanged snapshots.", new AcceptableValueRange<int>(300, 86400)));
    }

    public static void Install(BridgePlugin bridge, ConfigFile config, string server, string endpoint)
    {
        _instance = new ProgressAdapter(bridge, config, server, endpoint);
        Harmony.CreateAndPatchAll(typeof(ProgressAdapter), BridgePlugin.ModGuid + ".progress");
        bridge.Log.LogInfo("Progress adapter installed.");
    }

    public static void Tick() => _instance?.Update();

    public void Update()
    {
        if (Time.realtimeSinceStartup < _nextCheck || ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
        _nextCheck = Time.realtimeSinceStartup + _checkSeconds.Value;
        var keys = _globalKeys?.GetValue(ZoneSystem.instance) as HashSet<string>;
        if (keys == null) { _bridge.Log.LogWarning("ZoneSystem.m_globalKeys is unavailable; progress collection is paused."); return; }
        var bosses = _reportBosses.Value ? keys.Where(key => key.StartsWith("defeated_", StringComparison.OrdinalIgnoreCase)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray() : Array.Empty<string>();
        var players = _reportLevels.Value ? ReadPlayers() : Array.Empty<PlayerProgress>();
        var signature = string.Join("|", bosses) + "#" + string.Join("|", players.Select(player => $"{player.id}:{player.level}")) + "#" + string.Join("|", _pending.Select(kill => kill.id));
        if (signature == _lastSignature && Time.realtimeSinceStartup < _nextHeartbeat) return;
        var id = "progress-" + StableHash(signature);
        if (_bridge.Enqueue("progress", _endpoint, id, JsonUtility.ToJson(new ProgressReport { server = _server, instance = Environment.GetEnvironmentVariable("HOSTNAME") ?? "", bosses = bosses, players = players, bossKills = _pending.ToArray(), milestoneStep = _milestoneStep.Value })))
        { _lastSignature = signature; _nextHeartbeat = Time.realtimeSinceStartup + _heartbeatSeconds.Value; _pending.Clear(); }
    }

    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    [HarmonyPostfix]
    private static void AfterCharacterDamage(Character __instance, HitData hit)
    {
        var self = _instance;
        if (self == null || !self._reportBosses.Value || __instance == null || hit == null || !__instance.IsBoss() || __instance.GetHealth() > 0f) return;
        var eventId = __instance.GetComponent<ZNetView>()?.GetZDO()?.m_uid.ToString() ?? $"{__instance.name}:{Time.frameCount}";
        if (!self._seenDeaths.Add(eventId)) return;
        var nearby = Player.GetAllPlayers().Where(player => player != null && !player.IsDead() && Vector3.Distance(player.transform.position, __instance.transform.position) <= self._participantRadius.Value).Select(player => player.GetPlayerName()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var killer = hit.GetAttacker() is Player player ? player.GetPlayerName() : "Unknown Viking";
        self._pending.Add(new BossKill { id = eventId, key = __instance.m_defeatSetGlobalKey ?? "", boss = __instance.GetHoverName(), killer = killer, participants = nearby });
        self._nextCheck = 0f;
    }

    private PlayerProgress[] ReadPlayers()
    {
        if (ZDOMan.instance == null) return Array.Empty<PlayerProgress>();
        var result = new List<PlayerProgress>();
        foreach (var player in ZNet.instance.GetPlayerList())
        {
            var zdo = ZDOMan.instance.GetZDO(player.m_characterID);
            if (zdo != null) result.Add(new PlayerProgress { id = player.m_characterID.UserID.ToString(), name = player.m_name ?? "Unknown Viking", level = Math.Max(1, zdo.GetInt("EpicMMOSystem_level", 1)) });
        }
        return result.OrderBy(player => player.id, StringComparer.Ordinal).ToArray();
    }
    private static string StableHash(string value) { unchecked { uint hash = 2166136261; foreach (var c in value) { hash ^= c; hash *= 16777619; } return hash.ToString("x8"); } }
    [Serializable] private sealed class ProgressReport { public string server = ""; public string instance = ""; public string[] bosses = Array.Empty<string>(); public PlayerProgress[] players = Array.Empty<PlayerProgress>(); public BossKill[] bossKills = Array.Empty<BossKill>(); public int milestoneStep; }
    [Serializable] private sealed class PlayerProgress { public string id = ""; public string name = ""; public int level; }
    [Serializable] private sealed class BossKill { public string id = ""; public string key = ""; public string boss = ""; public string killer = ""; public string[] participants = Array.Empty<string>(); }
}
