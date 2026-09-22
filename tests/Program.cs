using RagnavikServerBridge;

var tests = new (string Name, Action Run)[] {
    ("delivery removes accepted events", Delivery),
    ("retry retains failed events", Retry),
    ("duplicate ids are ignored", Duplicate),
    ("corrupt entries are quarantined", Corrupt),
    ("disabled adapters do not start", DisabledAdapters),
    ("maintenance countdown thresholds and copy", MaintenanceCountdowns),
    ("HTTP diagnostics include safe receiver detail", HttpDiagnostics),
};
foreach (var test in tests) { test.Run(); Console.WriteLine($"PASS {test.Name}"); }

static void Delivery()
{
    using var fixture = new Fixture();
    fixture.Outbox.Enqueue(Event("one"));
    Equal(1, fixture.Pump(new FakeTransport(true)).PumpOnce());
    Equal(0, Directory.GetFiles(fixture.Path, "*.event").Length);
}
static void Retry()
{
    using var fixture = new Fixture();
    fixture.Outbox.Enqueue(Event("retry"));
    Equal(0, fixture.Pump(new FakeTransport(false)).PumpOnce());
    Equal(1, Directory.GetFiles(fixture.Path, "*.event").Length);
    Equal(1, fixture.Pump(new FakeTransport(true)).PumpOnce());
}
static void Duplicate()
{
    using var fixture = new Fixture();
    True(fixture.Outbox.Enqueue(Event("same")));
    True(!fixture.Outbox.Enqueue(Event("same")));
    Equal(1, Directory.GetFiles(fixture.Path, "*.event").Length);
}
static void Corrupt()
{
    using var fixture = new Fixture();
    File.WriteAllText(System.IO.Path.Combine(fixture.Path, "broken.event"), "not an event");
    Equal(0, fixture.Pump(new FakeTransport(true)).PumpOnce());
    Equal(1, Directory.GetFiles(System.IO.Path.Combine(fixture.Path, "corrupt")).Length);
}
static void DisabledAdapters()
{
    True(!AdapterGate.CanStart(false, true, "https://localhost/progress"));
    True(!AdapterGate.CanStart(true, false, "https://localhost/progress"));
    True(!AdapterGate.CanStart(true, true, ""));
    True(AdapterGate.CanStart(true, true, "https://localhost/progress"));
}
static void HttpDiagnostics()
{
    var diagnostic = DeliveryDiagnostic.Format(400, "Bad Request", "{\"error\":\"invalid_progress\",\n\"detail\":\"players is invalid\"}");
    True(diagnostic.Contains("HTTP 400 Bad Request"));
    True(diagnostic.Contains("players is invalid"));
    True(!diagnostic.Contains("\n"));
}
static OutboxRecord Event(string id) => new() { Id = id, Adapter = "test", Endpoint = "https://localhost/events", Body = "{}" };
static void True(bool value) { if (!value) throw new Exception("Expected true."); }
static void MaintenanceCountdowns()
{
    Equal(600, MaintenanceCountdown.Due(601, 600)!.Value);
    Equal(300, MaintenanceCountdown.Due(302, 299)!.Value);
    Equal(10, MaintenanceCountdown.Due(11, 10)!.Value);
    Equal(1, MaintenanceCountdown.Due(2, 1)!.Value);
    True(!MaintenanceCountdown.Due(299, 298).HasValue);
    True(MaintenanceCountdown.Message(60, "Update").Contains("1 minute"));
    True(MaintenanceCountdown.Message(1, "Update").Contains("1 second"));
}
static void Equal<T>(T expected, T actual) where T : IEquatable<T> { if (!expected.Equals(actual)) throw new Exception($"Expected {expected}, got {actual}."); }

sealed class Fixture : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rsb-tests-" + Guid.NewGuid().ToString("N"));
    public DiskOutbox Outbox { get; }
    private readonly TestLog _log = new();
    public Fixture() => Outbox = new DiskOutbox(Path, 10, _log);
    public DeliveryPump Pump(IBridgeTransport transport) => new(Outbox, transport, () => "token", _ => "X-Test", _log);
    public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
}
sealed class FakeTransport : IBridgeTransport
{
    private readonly bool _result;
    public FakeTransport(bool result) => _result = result;
    public bool Send(Uri endpoint, string header, string token, string body, out string diagnostic) { diagnostic = _result ? "ok" : "failed"; return _result; }
}
sealed class TestLog : IBridgeLog { public void Info(string message) { } public void Warning(string message) { } public void Error(string message) { } }
