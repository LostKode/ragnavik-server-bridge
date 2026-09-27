using System;
namespace RagnavikServerBridge;

internal sealed class WorldSaveEvidence
{
    private readonly object _gate = new();
    private bool _started;
    private bool _completed;
    private bool _failed;
    internal void Observe(string message)
    {
        lock (_gate)
        {
            if (message.Contains("Save World Thread Started!")) _started = true;
            if (message.Contains("World save (5/5) FAILED") || message.Contains("Error saving world!")) _failed = true;
            if (_started && message.Contains("World save (5/5) done.")) _completed = true;
        }
    }
    internal void RequireSuccess()
    {
        lock (_gate)
        {
            if (_failed) throw new InvalidOperationException("World save reported a write failure; deployment blocked.");
            if (!_started || !_completed) throw new InvalidOperationException("World save did not confirm successful writes; deployment blocked.");
        }
    }
}
