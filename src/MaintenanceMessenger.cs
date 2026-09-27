using System;
using System.Collections.Generic;

namespace RagnavikServerBridge;

internal static class MaintenanceMessenger
{
    internal const string RpcName = "ServerCharacters IngameMessage";

    // ServerCharacters 1.4.17 registers this peer RPC on clients and displays
    // the message in both Chat and MessageHud. No server-side Player is needed.
    internal static (int Recipients, int Queued, int Failed) Send(IEnumerable<ZNetPeer> peers, string message)
    {
        var recipients = 0;
        var queued = 0;
        var failed = 0;
        foreach (var peer in peers)
        {
            if (peer == null || !peer.IsReady()) continue;
            recipients++;
            try
            {
                if (peer.m_rpc == null || !peer.m_rpc.IsConnected()) { failed++; continue; }
                peer.m_rpc.Invoke(RpcName, message);
                queued++;
            }
            catch (Exception) { failed++; }
        }
        return (recipients, queued, failed);
    }
}
