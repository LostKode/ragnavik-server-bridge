using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RagnavikServerBridge;

internal interface IBridgeTransport { bool Send(Uri endpoint, string header, string token, string body, out string diagnostic); }
internal interface IBridgeLog { void Info(string message); void Warning(string message); void Error(string message); }
internal sealed class OutboxRecord { public string Id { get; set; } = ""; public string Adapter { get; set; } = ""; public string Endpoint { get; set; } = ""; public string Body { get; set; } = ""; }

internal sealed class DiskOutbox
{
    private const string Version = "RSB1";
    private readonly string _directory;
    private readonly string _corruptDirectory;
    private readonly int _maximum;
    private readonly IBridgeLog _log;
    public DiskOutbox(string directory, int maximum, IBridgeLog log) { _directory = directory; _corruptDirectory = Path.Combine(directory, "corrupt"); _maximum = maximum; _log = log; Directory.CreateDirectory(_directory); }
    public bool Enqueue(OutboxRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Id) || string.IsNullOrWhiteSpace(record.Adapter) || string.IsNullOrWhiteSpace(record.Endpoint) || string.IsNullOrWhiteSpace(record.Body)) throw new ArgumentException("Outbox records require id, adapter, endpoint, and body.");
        var final = Path.Combine(_directory, SafeId(record.Id) + ".event");
        if (File.Exists(final)) return false;
        if (Directory.GetFiles(_directory, "*.event").Length >= _maximum) { _log.Error($"Server Bridge outbox is full ({_maximum}); event {record.Id} was not queued."); return false; }
        var temporary = final + ".tmp";
        File.WriteAllText(temporary, Serialize(record), new UTF8Encoding(false));
        try { File.Move(temporary, final); }
        catch (IOException) when (File.Exists(final)) { File.Delete(temporary); return false; }
        return true;
    }
    public IEnumerable<(string Path, OutboxRecord Record)> ReadPending()
    {
        foreach (var path in Directory.GetFiles(_directory, "*.event").OrderBy(value => value, StringComparer.Ordinal))
        {
            OutboxRecord record;
            try { record = Deserialize(File.ReadAllText(path)); }
            catch (Exception exception)
            {
                Directory.CreateDirectory(_corruptDirectory);
                var destination = Path.Combine(_corruptDirectory, Path.GetFileName(path));
                if (File.Exists(destination)) destination += "." + DateTime.UtcNow.Ticks;
                File.Move(path, destination);
                _log.Error($"Quarantined corrupt outbox entry {Path.GetFileName(path)}: {exception.Message}");
                continue;
            }
            yield return (path, record);
        }
    }
    public void Complete(string path) => File.Delete(path);
    public int ImportLegacyJson(string legacyDirectory, string endpoint, string adapter)
    {
        if (!Directory.Exists(legacyDirectory) || string.IsNullOrWhiteSpace(endpoint)) return 0;
        var imported = 0;
        foreach (var path in Directory.GetFiles(legacyDirectory, "*.json").OrderBy(value => value, StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            try
            {
                var destination = Path.Combine(_directory, SafeId(id) + ".event");
                if (Enqueue(new OutboxRecord { Id = id, Adapter = adapter, Endpoint = endpoint, Body = File.ReadAllText(path) }) || File.Exists(destination)) { File.Delete(path); imported++; }
            }
            catch (Exception exception) { _log.Warning($"Legacy queue entry {Path.GetFileName(path)} was retained: {exception.Message}"); }
        }
        return imported;
    }
    private static string Serialize(OutboxRecord value) => string.Join("|", new[] { Version, Encode(value.Id), Encode(value.Adapter), Encode(value.Endpoint), Encode(value.Body) });
    private static OutboxRecord Deserialize(string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 5 || parts[0] != Version) throw new InvalidDataException("unsupported record format");
        return new OutboxRecord { Id = Decode(parts[1]), Adapter = Decode(parts[2]), Endpoint = Decode(parts[3]), Body = Decode(parts[4]) };
    }
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
    private static string SafeId(string value) { var safe = new string(value.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray()); if (string.IsNullOrEmpty(safe)) throw new ArgumentException("Unsafe event id."); return safe; }
}

internal sealed class DeliveryPump
{
    private readonly DiskOutbox _outbox; private readonly IBridgeTransport _transport; private readonly Func<string> _readToken; private readonly string _header; private readonly IBridgeLog _log;
    public DeliveryPump(DiskOutbox outbox, IBridgeTransport transport, Func<string> readToken, string header, IBridgeLog log) { _outbox = outbox; _transport = transport; _readToken = readToken; _header = header; _log = log; }
    public int PumpOnce()
    {
        string token;
        try { token = _readToken().Trim(); }
        catch (Exception exception) { _log.Warning($"Server Bridge token unavailable: {exception.Message}"); return 0; }
        if (string.IsNullOrEmpty(token)) { _log.Warning("Server Bridge token file is empty."); return 0; }
        var delivered = 0;
        foreach (var item in _outbox.ReadPending())
        {
            if (!Uri.TryCreate(item.Record.Endpoint, UriKind.Absolute, out var endpoint)) { _log.Error($"Event {item.Record.Id} has an invalid endpoint and remains queued."); break; }
            if (!_transport.Send(endpoint, _header, token, item.Record.Body, out var diagnostic)) { _log.Warning($"{item.Record.Adapter} delivery deferred: {diagnostic}"); break; }
            _outbox.Complete(item.Path); delivered++;
        }
        return delivered;
    }
}

internal static class AdapterGate
{
    public static bool CanStart(bool bridgeEnabled, bool adapterEnabled, string endpoint) => bridgeEnabled && adapterEnabled && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
