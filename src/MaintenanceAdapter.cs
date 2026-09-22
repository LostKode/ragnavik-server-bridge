using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using BepInEx.Configuration;
using UnityEngine;

namespace RagnavikServerBridge;

internal sealed class MaintenanceAdapter
{
    private static MaintenanceAdapter? _instance;
    private readonly BridgePlugin _bridge;
    private readonly Uri _endpoint;
    private readonly string _tokenFile;
    private readonly string _tokenHeader;
    private readonly ConfigEntry<int> _pollSeconds;
    private readonly object _gate = new();
    private MaintenanceState _state = new();
    private float _nextPoll;
    private double? _schedule;
    private int _previousRemaining;
    private int _polling;

    private MaintenanceAdapter(BridgePlugin bridge, ConfigFile config, string progressEndpoint,
                               string tokenFile, string tokenHeader)
    {
        _bridge = bridge;
        _endpoint = new Uri(new Uri(progressEndpoint), "/maintenance/game");
        _tokenFile = tokenFile;
        _tokenHeader = tokenHeader;
        _pollSeconds = config.Bind("Maintenance", "PollSeconds", 1,
            new ConfigDescription("Seconds between maintenance countdown checks.",
                new AcceptableValueRange<int>(1, 10)));
    }

    public static void Install(BridgePlugin bridge, ConfigFile config, string progressEndpoint,
                               string tokenFile, string tokenHeader)
    {
        _instance = new MaintenanceAdapter(bridge, config, progressEndpoint, tokenFile, tokenHeader);
        bridge.Log.LogInfo("Maintenance countdown adapter installed.");
    }

    public static void Tick() => _instance?.Update();

    private void Update()
    {
        if (Time.realtimeSinceStartup >= _nextPoll)
        {
            _nextPoll = Time.realtimeSinceStartup + _pollSeconds.Value;
            if (Interlocked.Exchange(ref _polling, 1) == 0)
                Task.Run(() => { try { Poll(); } finally { Interlocked.Exchange(ref _polling, 0); } });
        }

        MaintenanceState state;
        lock (_gate) state = _state;
        if (!state.active || state.shutdownAt <= 0)
        {
            _schedule = null;
            return;
        }

        var remaining = Math.Max(0, (int)Math.Ceiling(state.shutdownAt -
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d));
        if (_schedule != state.shutdownAt)
        {
            _schedule = state.shutdownAt;
            _previousRemaining = remaining + 1;
        }

        var due = MaintenanceCountdown.Due(_previousRemaining, remaining);
        _previousRemaining = remaining;
        if (!due.HasValue) return;
        var message = MaintenanceCountdown.Message(due.Value, state.reason);
        foreach (var player in Player.GetAllPlayers())
            player?.Message(MessageHud.MessageType.Center, message);
        _bridge.Log.LogInfo($"Broadcast maintenance countdown: {due.Value} second(s).");
    }

    private void Poll()
    {
        try
        {
            var request = (HttpWebRequest)WebRequest.Create(_endpoint);
            request.Method = "GET";
            request.Timeout = 3000;
            request.ReadWriteTimeout = 3000;
            request.Headers[_tokenHeader] = File.ReadAllText(_tokenFile).Trim();
            using var response = (HttpWebResponse)request.GetResponse();
            using var stream = response.GetResponseStream();
            using var reader = stream == null ? null : new StreamReader(stream);
            var state = JsonUtility.FromJson<MaintenanceState>(reader?.ReadToEnd() ?? "{}") ?? new MaintenanceState();
            lock (_gate) _state = state;
        }
        catch (Exception exception)
        {
            _bridge.Log.LogWarning($"Maintenance countdown check failed: {exception.Message}");
        }
    }

    [Serializable]
    private sealed class MaintenanceState
    {
        public bool active = false;
        public double shutdownAt = 0;
        public string reason = "";
    }
}
