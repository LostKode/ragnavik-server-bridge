# Ragnavik Progress

Ragnavik Progress is a server-only Valheim plugin that reports boss defeats and EpicMMO level milestones to a private HTTP receiver. It does not contain the Ragnavik status bot or a Discord integration.

The extracted source is based on the authoritative `ragnavik-status-bot` release worktree at commit `5e0595ffe6b0bd6f6bd4a44f8216c2c643fdb588`. Generated binaries and release archives are intentionally excluded.

## Features

* Reports newly defeated world bosses.
* Records the killing player and nearby participants for future boss defeats.
* Reports connected characters reaching configurable EpicMMO level intervals.
* Defaults to disabled and requires an explicitly configured private endpoint.
* Sends no ordinary creature kills.

## Build

Install the .NET SDK and provide assemblies from a compatible dedicated Valheim server and BepInEx installation:

```sh
export VALHEIM_MANAGED_DIR=/path/to/valheim_server_Data/Managed
export BEPINEX_CORE_DIR=/path/to/BepInEx/core
scripts/build.sh
```

The inputs must contain `assembly_valheim.dll`, the referenced Unity assemblies, `BepInEx.dll`, and `0Harmony.dll`. The compiled plugin is written to `src/bin/Release/netstandard2.1/RagnavikProgress.dll`.

## Package and validate

After a successful build:

```sh
scripts/package.sh
```

The script creates `artifacts/Ragnavik_Progress-<version>.zip` and validates its layout, JSON manifest, and absence of nested build outputs. Artifacts are ignored by Git.

## Install

Install only on the dedicated server. Copy the plugin under `BepInEx/plugins/RagnavikProgress/` and configure `BepInEx/config/lostkode.ragnavik.progress.cfg` after the first start. Add `lostkode.ragnavik.progress` to `CatosAntiCheat_ServerOnly.txt`. Clients do not install this plugin.

For the complete release, anti-cheat, backup, deployment, and post-deployment verification checklist, see [DEPLOYMENT.md](DEPLOYMENT.md).

## Receiver contract

The configured endpoint receives JSON snapshots containing `server`, `instance`, `bosses`, `bossKills`, `players`, and `milestoneStep`. It must return HTTP `204 No Content` after accepting the report. Snapshots can repeat, so the receiver owns durable deduplication. Character IDs are private deduplication keys and should not appear in public messages.

Authentication tokens belong in a server-only token file. Never commit them, place them in configuration templates, print them in logs, or include them in a release package.
