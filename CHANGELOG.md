# Changelog

| Version | Changes |
| --- | --- |
| 1.0.9 | Restore maintenance countdown messages after Valheim added the optional message logging parameter. |
| 1.0.8 | Read connected EpicMMO player levels from live server player objects so level milestones reach Longhouse. |
| 1.0.7 | Broadcast planned maintenance warnings in game at 10 minutes, 5 minutes, 1 minute, 30 seconds, and each of the final 10 seconds. |
| 1.0.6 | Report per-player private boss keys, player deaths with safe cause attribution, and world day, time, and active raid data for Discord progress commands. |
| 1.0.5 | Report safe receiver validation details for deferred deliveries so HTTP failures can be diagnosed without exposing credentials or request payloads. |
| 1.0.4 | Use separate authentication headers for progress and Catos delivery so both receivers accept the shared token while notifications remain enabled. |
| 1.0.3 | Correct the CatosAntiCheat 1.0.4 mismatch and timeout hook signatures so notifications remain enabled without invalid Harmony IL during dedicated-server startup. |
| 1.0.2 | Link the Hexium package to the Ragnavik website and restore CatosAntiCheat 1.0.4 mismatch and timeout notifications. |
| 1.0.1 | Ship the Server Bridge package with the approved Fjord Gate icon and classify it as server only on Hexium. |
| 1.0.0 | Consolidate Ragnavik Progress and Catos Reporter into one server-only bridge. Separate integrations behind internal adapters and reserve a disabled AzuAntiCheat adapter. Share authentication, atomic disk-backed delivery, retries, deduplication, configuration, and diagnostics. Import legacy Catos queued events, seed blank settings from legacy configuration, quarantine corrupt queue entries, and add offline coverage. |
