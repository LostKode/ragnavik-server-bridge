# Release and deployment

Ragnavik Progress is server-only. Never add it to a client pack. Its plugin GUID, `lostkode.ragnavik.progress`, belongs in `CatosAntiCheat_ServerOnly.txt`.

## Release gate

1. Update the plugin version in `src/RagnavikProgress.cs`, `package/manifest.json`, and `CHANGELOG.md`.
2. Build from a clean checkout using `scripts/build.sh` and package with `scripts/package.sh`.
3. Validate the archive with `scripts/validate-package.sh` and test it on staging before production.
4. Create and publish the corresponding Ragnavik website blog post. A release must not be published without that post.
5. Reconstruct the complete effective client and server manifests. Version-check shared mods, put every client-only mod in `CatosAntiCheat_ExtraWhitelist.txt`, and put every server-only mod in `CatosAntiCheat_ServerOnly.txt`.
6. Before any production restart, save the world and create and verify a separate rollback backup. Check for connected players and coordinate downtime.
7. Deploy only the validated DLL and configuration. Never copy credentials into source control or release archives.
8. After restart, verify the intended node, service readiness, BepInEx logs, loaded plugin count, compatibility errors, and source-to-runtime plugin parity. A running task count alone is not proof of a successful deployment.

## Configuration

The package defaults to disabled. Configure the private endpoint and token file on the server, then enable reporting. The token file and its contents must remain outside this repository and package.

The receiver must authenticate requests, validate their size and fields, deduplicate repeated snapshots, and return HTTP `204 No Content` only after safely accepting a report.
