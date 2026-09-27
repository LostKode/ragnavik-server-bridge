using RagnavikServerBridge;

void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
var first = new ZNetPeer(); var second = new ZNetPeer();
var result = MaintenanceMessenger.Send(new[] { first, second }, "Maintenance in 60 seconds");
Check(result == (2, 2, 0), "connected peers receive messages without any Player objects");
Check(first.m_rpc.Messages.Single() == ("ServerCharacters IngameMessage", "Maintenance in 60 seconds"), "verified chat and center RPC and text");
Check(second.m_rpc.Messages.Count == 1, "each recipient queued once");
Check(MaintenanceMessenger.Send(Array.Empty<ZNetPeer>(), "test") == (0, 0, 0), "empty roster never reports successful delivery");
var notReady = new ZNetPeer { Ready = false };
Check(MaintenanceMessenger.Send(new[] { notReady }, "test") == (0, 0, 0), "unready peer excluded");
var disconnected = new ZNetPeer(); disconnected.m_rpc.Connected = false;
Check(MaintenanceMessenger.Send(new[] { disconnected }, "test") == (1, 0, 1), "disconnected peer is a failure");
var broken = new ZNetPeer(); broken.m_rpc.Throw = true;
var good = new ZNetPeer();
Check(MaintenanceMessenger.Send(new[] { broken, good }, "test") == (2, 1, 1), "one failed peer does not prevent other recipients");
Check(good.m_rpc.Messages.Count == 1, "remaining peer still receives notice");

public class ZNetPeer { public bool Ready = true; public ZRpc m_rpc = new(); public bool IsReady() => Ready; }
public class ZRpc {
    public bool Connected = true; public bool Throw;
    public List<(string, string)> Messages = new();
    public bool IsConnected() => Connected;
    public void Invoke(string name, params object[] args) { if (Throw) throw new Exception("send failed"); Messages.Add((name, (string)args.Single())); }
}
