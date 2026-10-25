# Frozen layering compatibility fixtures

These files were captured before the corresponding contract batches moved. Keep them immutable; rebuilding them from relocated types would remove the historical comparison.

- `framework-contracts.json`: 98 plain/LZ4 payloads for 49 provider-facing data/event types, captured before the 53-type signature closure moved from Domain contract assemblies into neutral Shared. The accompanying `.types.json` records that closure.
- `wire-boundaries.json`: 48 plain/LZ4 payloads for 24 pricing and market-data wire types, captured before their Framework-to-Shared move. The accompanying `.types.json` records those types.
- Each payload has its original CLR identity and SHA-256. The acceptance test resolves current types, reads the frozen bytes and checks identical re-encoding. Stored event IDs are reset only for that byte comparison because replay restores authoritative event versions.

`TomasAI.IFM.LivePipeline.IntegrationTests/EventTypeCompatibilityAcceptanceTests.cs` consumes these fixtures. The separate `test-support/EventIdentityRelocation` executables verify a real namespace/assembly move and old-reader rollback in independent processes.

These are synthetic structural fixtures, not market observations or an exhaustive corpus of every historical event. The separate read-only database qualification compares exact stored rows and hashes; it does not rewrite event payloads or metadata.
