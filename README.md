# Ragnavik Server Bridge

Ragnavik Server Bridge is one server-only Valheim plugin for authenticated Ragnavik events. Its internal adapters keep game integration separate from shared authentication, durable delivery, retries, deduplication, configuration, and diagnostics.

## Adapters

* Progress reports global boss milestones, per-player private boss defeat keys, boss participants, and EpicMMO level milestones.
* Catos observes CatosAntiCheat 1.0.4 mismatch and timeout notification methods. It does not replace or weaken enforcement.
* AzuAntiCheat is a disabled configuration placeholder. No Azu hooks are shipped until its contract is validated.

The bridge defaults to disabled. Progress and Catos use separate receiver URLs so the existing `/progress` and `/anticheat` payload contracts can remain intact, while both share one token file, header, retry loop, and disk outbox.

## Build and test

Provide compatible Valheim and BepInEx reference directories, then run:

```sh
export VALHEIM_MANAGED_DIR=/path/to/valheim_server_Data/Managed
export BEPINEX_CORE_DIR=/path/to/BepInEx/core
scripts/build.sh
scripts/package.sh
```

The build runs the offline outbox tests. The package is `artifacts/LostKode-Ragnavik_Server_Bridge-<version>.zip` and contains `RagnavikServerBridge.dll` plus `lostkode.ragnavik.serverbridge.cfg`.

The bridge deliberately uses one token header for both routes. If the legacy receivers expect different headers, update their private configuration to accept the bridge header before enabling both adapters.

## Migration

Install the bridge only on the dedicated server. Replace the old Progress and Catos Reporter DLLs with this bridge DLL, then replace both legacy GUID entries in `CatosAntiCheat_ServerOnly.txt` with `lostkode.ragnavik.serverbridge`. Do not load old and new DLLs together.

On first start, blank bridge settings are seeded from `lostkode.ragnavik.progress.cfg` and `lostkode.ragnavik.catosreporter.cfg`. The existing Catos queue under `RagnavikCatosReporterQueue` is atomically imported into `RagnavikServerBridgeQueue` after a Catos endpoint is available. Legacy Progress had no disk queue, so only its configuration can be migrated. Review the generated bridge config before removing legacy config files.

Authentication tokens remain in a server-only file and must never be logged, committed, or packaged. Corrupt outbox entries are moved to `RagnavikServerBridgeQueue/corrupt` for diagnosis instead of blocking valid events.

See [DEPLOYMENT.md](DEPLOYMENT.md) for the staging and rollback boundary.
