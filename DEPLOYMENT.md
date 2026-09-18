# Release and deployment

Ragnavik Server Bridge is server-only. Its GUID is `lostkode.ragnavik.serverbridge`, which belongs in `CatosAntiCheat_ServerOnly.txt`. Clients must not install it.

## Release gate

1. Keep `BridgePlugin.ModVersion`, `package/manifest.json`, and `CHANGELOG.md` aligned.
2. Run `scripts/build.sh` and `scripts/package.sh` from a clean checkout.
3. Validate the exact CatosAntiCheat 1.0.4 hook signatures and stage both existing receiver routes.
4. Publish the corresponding Ragnavik website post before any Thunderstore publication.
5. Reconstruct the complete effective client and server manifests. Remove the two legacy DLLs and GUIDs only when adding this bridge DLL and GUID.
6. Before an authorized production restart, check players, save the world, and create and verify a separate rollback backup.
7. After restart, verify node placement, readiness, BepInEx plugin count, bridge diagnostics, Catos enforcement, outbox delivery, and source-to-runtime parity.

## Staging checks

Confirm a progress snapshot is accepted once, a controlled Catos mismatch and timeout are each accepted once, an unavailable receiver leaves events queued, and restored service drains them once. Place a malformed test entry in the staging outbox and confirm it is quarantined without blocking later entries. Enable no AzuAntiCheat setting in production because that adapter is intentionally not implemented.

## Rollback

Restore the two previous DLLs, configs, GUID policy entries, and runtime trees from the verified rollback artifact. Retain `RagnavikServerBridgeQueue` during rollback so events are not silently destroyed. Catos enforcement remains independent of bridge delivery.
