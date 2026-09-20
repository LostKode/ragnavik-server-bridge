using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace RagnavikServerBridge;

[BepInPlugin(ModGuid, ModName, ModVersion)]
[BepInDependency("com.catosvalheim.anticheat", "1.0.4")]
public sealed class BridgePlugin : BaseUnityPlugin
{
    internal ManualLogSource Log => Logger;
    public const string ModGuid = "lostkode.ragnavik.serverbridge";
    public const string ModName = "Ragnavik Server Bridge";
    public const string ModVersion = "1.0.5";
    internal static BridgePlugin? Instance;
    internal static BridgeLog? BridgeLogger;
    internal DiskOutbox? Outbox;
    private DeliveryPump? _pump;
    private Timer? _timer;
    private int _pumping;

    private void Awake()
    {
        Instance = this;
        BridgeLogger = new BridgeLog(Logger);
        var enabled = Config.Bind("General", "Enabled", false, "Enable the bridge after private endpoints are configured.");
        var server = Config.Bind("General", "ServerName", "Ragnavik", "Private server label included in reports.");
        var tokenFile = Config.Bind("Connection", "TokenFile", "", "Server-only authentication token file.");
        var progressTokenHeader = Config.Bind("Connection", "ProgressTokenHeader", "X-Ragnavik-Token", "Authentication header for progress delivery.");
        var catosTokenHeader = Config.Bind("Connection", "CatosTokenHeader", "X-Ragnavik-Anticheat", "Authentication header for Catos delivery.");
        var progressEndpoint = Config.Bind("Connection", "ProgressEndpoint", "", "Private progress receiver URL.");
        var catosEndpoint = Config.Bind("Connection", "CatosEndpoint", "", "Private Catos receiver URL.");
        var retrySeconds = Config.Bind("Connection", "RetrySeconds", 5, new ConfigDescription("Seconds between delivery attempts.", new AcceptableValueRange<int>(2, 300)));
        var maxQueued = Config.Bind("Connection", "MaxQueuedEvents", 1000, new ConfigDescription("Maximum durable events retained.", new AcceptableValueRange<int>(10, 10000)));
        var progressEnabled = Config.Bind("Adapters", "ProgressEnabled", true, "Enable boss and EpicMMO reporting.");
        var catosEnabled = Config.Bind("Adapters", "CatosEnabled", true, "Enable Catos mismatch and timeout reporting.");
        var azuEnabled = Config.Bind("Adapters", "AzuAntiCheatEnabled", false, "Reserved for a future validated AzuAntiCheat adapter.");

        LegacyConfiguration.Apply(Paths.ConfigPath, enabled, server, tokenFile, progressTokenHeader, progressEndpoint, catosEndpoint, Logger);
        if (!enabled.Value) { Logger.LogInfo($"{ModName} v{ModVersion} is disabled."); return; }
        if (string.IsNullOrWhiteSpace(tokenFile.Value) || string.IsNullOrWhiteSpace(progressTokenHeader.Value) || string.IsNullOrWhiteSpace(catosTokenHeader.Value)) { Logger.LogError("Bridge authentication is incomplete; adapters remain disabled."); return; }

        Outbox = new DiskOutbox(Path.Combine(Paths.ConfigPath, "RagnavikServerBridgeQueue"), maxQueued.Value, BridgeLogger);
        var imported = Outbox.ImportLegacyJson(Path.Combine(Paths.ConfigPath, "RagnavikCatosReporterQueue"), catosEndpoint.Value, "catos");
        if (imported > 0) Logger.LogInfo($"Imported {imported} legacy Catos Reporter queue entries.");
        _pump = new DeliveryPump(Outbox, new HttpBridgeTransport(), () => File.ReadAllText(tokenFile.Value), adapter => string.Equals(adapter, "catos", StringComparison.OrdinalIgnoreCase) ? catosTokenHeader.Value : progressTokenHeader.Value, BridgeLogger);
        _timer = new Timer(_ => Pump(), null, 0, checked(retrySeconds.Value * 1000));

        if (AdapterGate.CanStart(enabled.Value, progressEnabled.Value, progressEndpoint.Value)) ProgressAdapter.Install(this, Config, server.Value, progressEndpoint.Value);
        else Logger.LogInfo("Progress adapter is disabled or has no valid endpoint.");
        if (AdapterGate.CanStart(enabled.Value, catosEnabled.Value, catosEndpoint.Value)) CatosAdapter.Install(this, server.Value, catosEndpoint.Value);
        else Logger.LogInfo("Catos adapter is disabled or has no valid endpoint.");
        if (azuEnabled.Value) Logger.LogWarning("AzuAntiCheat adapter is reserved but not implemented; no patches were installed.");
        Logger.LogInfo($"{ModName} v{ModVersion} loaded.");
    }

    internal bool Enqueue(string adapter, string endpoint, string id, string body)
    {
        var queued = Outbox?.Enqueue(new OutboxRecord { Adapter = adapter, Endpoint = endpoint, Id = id, Body = body }) == true; if (queued) Pump(); return queued;
    }
    private void Update() => ProgressAdapter.Tick();
    private void Pump()
    {
        if (_pump == null || Interlocked.Exchange(ref _pumping, 1) != 0) return;
        Task.Run(() => { try { _pump.PumpOnce(); } finally { Interlocked.Exchange(ref _pumping, 0); } });
    }
    private void OnDestroy() { _timer?.Dispose(); Instance = null; }
}

internal sealed class HttpBridgeTransport : IBridgeTransport
{
    public bool Send(Uri endpoint, string header, string token, string body, out string diagnostic)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            var request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = 10000; request.ReadWriteTimeout = 10000;
            request.Headers[header] = token; request.ContentLength = bytes.Length;
            using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            using var response = (HttpWebResponse)request.GetResponse();
            var code = (int)response.StatusCode; diagnostic = $"HTTP {code}"; return code >= 200 && code < 300;
        }
        catch (WebException exception) when (exception.Response is HttpWebResponse response)
        {
            string detail;
            try
            {
                using var stream = response.GetResponseStream();
                using var reader = stream == null ? null : new StreamReader(stream, Encoding.UTF8);
                detail = reader?.ReadToEnd() ?? "";
            }
            catch { detail = ""; }
            diagnostic = DeliveryDiagnostic.Format((int)response.StatusCode, response.StatusDescription, detail);
            return false;
        }
        catch (Exception exception) { diagnostic = string.Concat(exception.GetType().Name, ": ", exception.Message); return false; }
    }
}

internal sealed class BridgeLog : IBridgeLog
{
    private readonly ManualLogSource _log;
    public BridgeLog(ManualLogSource log) => _log = log;
    public void Info(string message) => _log.LogInfo(message);
    public void Warning(string message) => _log.LogWarning(message);
    public void Error(string message) => _log.LogError(message);
}
