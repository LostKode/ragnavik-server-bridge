# Changelog

## 1.0.0

* Consolidate Ragnavik Progress and Ragnavik Catos Reporter into one server-only bridge.
* Separate Progress and Catos integration behind internal adapters and reserve a disabled AzuAntiCheat adapter.
* Share authentication, atomic disk-backed delivery, retries, deduplication, configuration, and diagnostics.
* Import legacy Catos queued events and seed blank bridge settings from legacy configuration.
* Quarantine corrupt queue entries without blocking later events.
* Add offline tests for delivery, retries, duplicates, corrupt entries, and disabled adapters.
