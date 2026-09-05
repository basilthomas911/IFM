# Trade strategy product catalog and family creation

As-built source implementation, 2026-09-05. This document supersedes the earlier read-only/unique-SystemKey restrictions. It does not enable new trading strategies, place orders, or complete unrelated Stage 4 gates.

## Identity and contracts

`IMarketDataApi.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType family, CancellationToken)` returns `ServiceResult<TradeStrategySymbolReadModel[]>`. MessagePack keys are 0 Id, 1 Symbol, 2 Currency, 3 Exchange, 4 Description. Every returned item requires a positive ID, nonempty trimmed Symbol/Exchange, a three-uppercase-letter Currency and generated Description such as `ES futures` or `ES futures options`. A bad item fails the entire lookup; no partial success, fabricated currency/exchange, or stale-cache fallback is returned.

The stable IFM product identity is the sequence-allocated integer associated with `(Family, Exchange, Symbol, Currency)`. Example: FuturesOption / XCME / ES / USD. Exchange is provider metadata, not a hard-coded spelling guarantee. Identity survives application restart and futures contract rollover; it is not a Databento expiring instrument ID. Different families, exchanges, symbols or currencies produce different IDs. Sequence gaps from competing writers are acceptable; IDs are never reused.

`TradeStrategyFamilyReadModel` retains keys 0–11 and appends key 12 TradeStrategySymbolId and key 13 Exchange. `SystemKey` is exactly the enum-name composition `Family-Strategy` (e.g. `FuturesOption-VerticalSpread`). It is classification, NOT unique identity. Several definitions can share it. The exact family reference is `(TradeStrategyFamilyId, DefinitionVersion)`. Newly created definitions are immutable, Active, version 1. Natural duplicates `(Family, Strategy, TradeStrategySymbolId, TimeFrame)` are rejected. There is no edit/retire/delete/version-creation command in this increment.

## Provider boundary and configuration

Discovery uses Databento contract definitions, without starting a realtime tick subscription. Futures definitions must be current; options are matched to their actual underlying by publisher plus underlying instrument ID (raw symbol only if that ID is absent). Option roots are explicitly configured; they are not assumed to equal the futures root. Missing expiry, price currency, exchange, underlying match or an entire configured product fails discovery. Settlement currency is not used as a replacement for missing price currency.

Initial server configuration at `AppSettings:Databento:TradeStrategyProducts`:

```json
[{ "Symbol": "ES", "Dataset": "GLBX.MDP3", "OptionRoots": ["ES"] }]
```

This is a bounded configured product universe, not an exchange-wide instrument search or a guarantee of every option expiry. Add approved products/option roots here to expand discovery; up to 100 products and 32 option roots per product. Current support is Futures and FuturesOption. Other enum families return an unsupported error without provider calls. Synthetic feeds cannot manufacture reference products. Databento credentials, definition access and normal ReferenceDb/sequence availability are required. No bulk historical tick/option-price loader is started; definition-query billing follows the account's provider entitlements.

The server registers `AddTradeStrategySymbolCatalog()` plus the persistent `ITradeStrategySymbolStore`. Feed-only hosts may omit catalog registration; their lookup fails unavailable without affecting existing realtime API startup. Successful catalogs are cached per family for five minutes with defensive array copies and serialized refresh. Cancellation is propagated; an in-progress synchronous native metadata query is bounded by the configured provider timeout and cannot be forcibly interrupted by managed cancellation.

## Commands, queries and UI

References → Trade Strategy Families has a dedicated **Create Family…** action. Existing definitions remain read-only. Select Futures/FuturesOption, a supported strategy, Daily/Weekly/Monthly, and a returned product. Symbol selection populates read-only Currency and Exchange; SystemKey is generated. Description is user-entered (1–512 characters). A family change clears selections; generation fencing prevents delayed results from replacing a newer selection. Loading, invalid metadata and failures block creation.

UI query path: `IReferenceQueryApi.GetTradeStrategySymbolsAsync` → `GetTradeStrategySymbolsQuery` → ReferenceQueryActor → IMarketDataApi. Creation path: `IReferenceCommandApi.CreateTradeStrategyFamilyAsync` → `CreateTradeStrategyFamilyCommand` → TradeStrategyFamilyCommandActor → TradeStrategyFamilyCreationService → catalog store. This uses existing NATS command/query transport, not new HTTP endpoints. No UI accesses Databento or ReferenceDb directly.

The request contains OperationId, Family, Strategy, TimeFrame, TradeStrategySymbolId and Description. It does not accept a client family ID, Symbol, Currency or Exchange. The service verifies the selected product against the current catalog and derives stored metadata. Audit time is server UTC; creator follows the existing command transport's server-account identity convention (not a new authenticated end-user identity mechanism). The command returns its operation acknowledgement; the UI reloads the authoritative list after success.

Retries retain the same OperationId for the same request. One database compare-and-set transaction records both definition and receipt. Replaying a committed request returns the same ID/audit record; reuse with different content or a new-operation natural duplicate fails. Unknown outcomes may be retried safely. Creation revalidates product availability before consulting the receipt, so retry during a provider outage can remain unavailable until discovery recovers, but cannot create a duplicate.

## Storage and rollout

Normal Reference schema startup adds these tables without replacing legacy tables:

- `trade_strategy_symbol_v1`: partition family; clustering exchange, symbol, currency; persistent integer id. Conditional insert establishes the winner.
- `trade_strategy_family_catalog_v4`: one `V1` catalog partition containing revision and `payload_json`; a conditional revision update atomically owns entries, natural keys and operation receipts. Bounded to 1,000 newly created definitions and 16 contention attempts per command. This is a small administration catalog, not an instrument-level chain store.

Symbol IDs use the appended `Reference_TradeStrategySymbolId` sequence; family IDs use existing `Reference_TradeStrategyFamilyId`. Normal sequence initialization provisions enum-named sequences. Production sequence provisioning is unchanged; real CQL concurrency tests substitute only the sequence allocator.

Family queries combine preserved v3 seed definitions with v4 created definitions. The old v2→v3 migration preserves existing IDs, versions and audit data. The three canonical ES seeds remain unlinked legacy definitions with TradeStrategySymbolId=0 and empty Exchange; no guessed Exchange is written. They remain queryable/selectable by exact family identity. **All new product-linked definitions and all symbol lookup results require populated Symbol/Currency/Exchange.** A newly created linked definition may share classification with a legacy seed; it has its own exact ID. Existing policy references are not rewritten.

Fund mandates append MessagePack key 21 `PermittedTradeStrategyFamilies` (array of exact ID/version references). Assignments append key 22 `TradeStrategyFamily`; combined reference query rows append key 17. Updated editors write SchemaVersion 2. Existing string fields remain classification mirrors/legacy compatibility, not authority for typed mandates. Fund events, snapshots and JSON read projections carry the appended fields; their existing payload-based storage needs no table replacement. Exact-reference mandates cannot downgrade to legacy name-only permissions. The server validates referenced active catalog rows and classifications; assignment acceptance verifies exact Fund membership.

Legacy records are not bulk-rewritten. In editors, a name-only reference resolves only when exactly one active definition matches; otherwise it remains unavailable until explicitly replaced. Pre-v2 commands retain the preexisting legacy compatibility path; integrations should migrate to SchemaVersion 2. Typed Funds cannot accept a name-only assignment. Neither product discovery nor catalog creation automatically enables a family for any Fund.

Deploy matching API/UI binaries together; start the API with normal additive schema/bootstrap initialization before reconnecting the UI. Do not mix older seven-key family payloads with the typed layout or downgrade after creating exact references without reviewing those records. The source change does not restart the running application or migrate its live tables. Back up ReferenceDb plus sequences using normal operational procedures; restoring one without the other risks identity conflicts.

## Verification

Verified on Windows, 2026-09-05: full solution build succeeded with zero warnings/errors; MarketData unit suite 333 passed (one Linux-only containment test skipped); Reference unit/transport suite 42 passed; Portfolio unit suite 128 passed; storage migration/CQL integration 11 passed; targeted References/Portfolio UI suite 43 passed. Total: 557 passed, one platform-specific skip. Temporary CQL test keyspaces were removed; application keyspaces were untouched. No commit, push or application restart was performed.

Automated coverage includes DTO layout/round trips, provider-definition fixtures, unsupported families, missing metadata, cancellation, caching, server metadata validation, invalid strategy pairs, serialized command/query dispatch, editor selection/races/retries, exact Fund membership and version mismatch, snapshot/serialization retention, defensive copies and downgrade rejection. Real Scylla integration uses a unique disposable keyspace, verifies conditional-insert/CAS races, stable IDs across fresh store instances, idempotent creation, conflicts and multiple definitions sharing SystemKey, then removes only that test keyspace. Application tables are never recreated by that test.

Live Databento entitlement/metadata qualification and a running NATS API/UI end-to-end smoke test are separate operational checks; fixture tests do not prove those external services are currently available. Reproduction:

```powershell
dotnet build TomasAI.IFM.sln --no-restore --verbosity quiet -m:1 -nr:false
dotnet test TomasAI.IFM.Application.MarketData.UnitTests --no-restore --verbosity quiet
dotnet test TomasAI.IFM.Domain.Reference.UnitTests --no-restore --verbosity quiet
dotnet test TomasAI.IFM.Domain.Portfolio.UnitTests --no-restore --verbosity quiet
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests --no-restore --filter "FullyQualifiedName~TradeStrategyCatalogScyllaIntegrationTests|FullyQualifiedName~TradeStrategyFamilyMigrationTests"
dotnet test TomasAI.IFM.UI.Net.SystemTests --no-restore --filter "FullyQualifiedName~TradeFamilyCatalogUiTests|FullyQualifiedName~TradeStrategyFamilyCreationUiTests|FullyQualifiedName~TradeStrategyFamilyReferenceUiSystemTests|FullyQualifiedName~TradeStrategyTimeFrameUiTests"
```
