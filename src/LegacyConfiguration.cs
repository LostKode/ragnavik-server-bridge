using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace RagnavikServerBridge;

internal static class LegacyConfiguration
{
    public static void Apply(string configPath, ConfigEntry<bool> enabled, ConfigEntry<string> server,
        ConfigEntry<string> tokenFile, ConfigEntry<string> tokenHeader, ConfigEntry<string> progressEndpoint,
        ConfigEntry<string> catosEndpoint, ManualLogSource log)
    {
        var progress = Read(Path.Combine(configPath, "lostkode.ragnavik.progress.cfg"));
        var catos = Read(Path.Combine(configPath, "lostkode.ragnavik.catosreporter.cfg"));
        var migrated = false;
        migrated |= Seed(progressEndpoint, progress, "Connection.Endpoint");
        migrated |= Seed(catosEndpoint, catos, "Connection.Endpoint");
        migrated |= Seed(tokenFile, progress, "Connection.TokenFile") || Seed(tokenFile, catos, "Connection.TokenFile");
        migrated |= Seed(tokenHeader, progress, "Connection.TokenHeader", "X-Ragnavik-Token") || Seed(tokenHeader, catos, "Connection.TokenHeader", "X-Ragnavik-Token");
        migrated |= Seed(server, progress, "Messages.ServerName", "Ragnavik") || Seed(server, catos, "General.ServerName", "Ragnavik");
        if (!enabled.Value && (ReadBool(progress, "General.Enabled") || ReadBool(catos, "General.Enabled"))) { enabled.Value = true; migrated = true; }
        if (migrated) log.LogWarning("Seeded bridge settings from legacy Progress or Catos Reporter config. Review the new bridge config before removing legacy files.");
    }
    private static Dictionary<string, string> Read(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return values;
        var section = "";
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
            var equals = line.IndexOf('=');
            if (equals > 0) values[section + "." + line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
        }
        return values;
    }
    private static bool Seed(ConfigEntry<string> target, Dictionary<string, string> source, string key, string defaultValue = "")
    {
        if (!string.Equals(target.Value, defaultValue, StringComparison.Ordinal) || !source.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return false;
        target.Value = value; return true;
    }
    private static bool ReadBool(Dictionary<string, string> source, string key) => source.TryGetValue(key, out var value) && bool.TryParse(value, out var result) && result;
}
