# Persisted identity and layering qualification tool

This is a composition tool with an API Core reference. It must not be referenced by production libraries.

Modes:

- `OUTPUT.json`: inventories current event types and their effective frozen write identities.
- `--qualify-public-ports OUTPUT.json`: inspects exported Domain signatures and reachable Application/Shared contract surfaces for Framework or provider SDK leaks; fails when any are found. Use with the compiled-reference gate, which also checks Domain method bodies and constructor bindings.
- `--qualify-stored DATABASE OUTPUT.json [BEFORE.json]`: reads a representative latest row for each stored event type, or the exact event versions in BEFORE.json; records identities and source/reconstructed SHA-256 hashes. Uses a read-only PostgreSQL transaction against the local `ifm_db` container. No payloads are written to the report. The optional comparison requires identical outcomes and hashes.
- `--freeze-framework-types`, `--freeze-wire-boundaries`, `--freeze-all-events`: development capture tools used before moves. Do not regenerate committed historical fixtures after moving their contracts. Synthetic instance creation is not exhaustive qualification of all production event schemas.

The 733-entry write baseline and legacy read-target map are embedded in neutral Shared. The writer preserves old names; the reader accepts inventoried historical and current CLR spellings. Stable read IDs remain explicit registrations. Stable write identities are not enabled by this migration.

The all-event synthetic capture exposed a pre-existing, unused `MarketDataFeedResetApiEvent` whose unkeyed properties inherit a keyed transport contract. That capture is not a passing all-event serialization gate; its schema was left unchanged. No row of that type was present in the inspected Development event metadata. This does not affect the 733-identity registration gate or the qualified frozen/stored payloads.
