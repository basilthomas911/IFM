# Event type identity compatibility — first implementation step

Date: 2026-10-25

## Scope

Prepare persisted-event readers for later assembly/namespace moves. This step does not move event classes, change writer identities, rewrite the event log, or enable stable-name writes. Payload schema migration remains a separate concern.

## Read behavior

`Shared/EventSourcing/EventTypeRegistry` provides an additive, thread-safe registry. Readers accept:

- Existing assembly-qualified CLR identities through the existing CLR resolution path.
- Explicit historical CLR aliases, including names whose old assembly is no longer available.
- Explicit stable IDs such as `trade.iron-condor.position-changed/v1`.

Stable IDs and historical aliases must be registered deliberately. Unknown versions are never guessed. Registration rejects conflicting aliases atomically and cannot redirect an available CLR identity to a different class. A registered alias changes type resolution only; it does not transform payload fields.

API actor-runtime bootstrap registers these initial stable read aliases:

| Stable ID | Current contract |
| --- | --- |
| `trade.iron-condor.position-changed/v1` | `IronCondorPositionChangedEvent` |
| `market-data.futures.iti-signal-generated/v1` | `FuturesItiSignalGeneratedEvent` |
| `portfolio.fund-mandate-created/v1` | `FundMandateCreatedEvent` |

The catalog is in `Application.Api.Server.Core/Hosting/PersistedEventTypeAliases.cs`, keeping business-contract references out of the generic Shared registry. This is an initial catalog, not registration of every event under a stable ID. Existing CLR names continue to resolve without catalog entries. Before a real class move, register every affected historical name explicitly and qualify its stored payloads.

## Reader coverage

- Event-log MessagePack codec, both plain and LZ4 envelopes.
- Event stream and event log read models.
- PostgreSQL typed snapshot and last-N range queries: compatible metadata IDs are selected by resolved event type, including aliases with changed short class names. Alias row IDs are not cached, allowing newly inserted metadata spellings to be discovered.
- Event-projector outbox deserialization.
- NATS durable replay queue: binary and legacy JSON envelopes; failed resolutions are not cached.
- Existing Portfolio namespace and contractless-payload compatibility remains supported.

Unavailable event-log types remain observable as `UnknownEvent` with their original identity and payload. Direct codec and durable delivery reads fail explicitly when a type is unavailable. Invalid non-event types and corrupt payloads of known events remain failures; they are not silently converted into successful replay.

## Compatibility safeguards

Writers retain assembly-qualified identities. Envelope layout, compression, payload bytes, stream/global event IDs and deterministic outbox message IDs remain unchanged. There is no database schema change or production data rewrite.

Stable aliases are exact and case-sensitive; legacy CLR lookup retains its previous case-insensitive behavior. No arbitrary assembly scan or short-name matching is used to choose a replacement class.

## Verification

The focused qualification suites are:

| Suite | Coverage |
| --- | --- |
| Shared EventSourcing unit tests | Old/current/moved/stable identities; plain/LZ4; metadata preservation; atomic collisions; concurrent registration; unknown versions; corrupted envelopes |
| NATS durable queue unit tests | Existing queue behavior plus binary/legacy JSON aliases and repair after failed resolution |
| Storage snapshot/range and dual-appender integration tests | Real isolated PostgreSQL; sequential and binary COPY appenders; mixed identities; latest snapshot and typed ranges; unchanged payload bytes; existing transaction/idempotency cases |
| LivePipeline event-type acceptance tests | Frozen pre-change payload corpus; real Iron Condor/ITI/Portfolio contracts; actual outbox serializer; current concrete event-contract CLR identities |
| Portfolio legacy replay unit tests | Existing old namespace and legacy payload handling |

`test-support/EventTypeCompatibilityFixtures/manifest.json` contains six frozen plain/LZ4 payloads generated using the pre-change codec from Git HEAD. Its provenance, source hash, payload hashes and expected decoded values are recorded alongside the fixtures. These tests compare old bytes, not only new-code round trips.

Qualification result: **100 focused tests passed**: 21 Shared EventSourcing, 26 NATS durable queue, 49 PostgreSQL snapshot/range and dual-appender integration, three frozen-payload/outbox acceptance, and one Portfolio legacy replay test. The acceptance project also compiled the API registration path successfully.

Focused test logs are under ignored `.artifacts/event-type-*-results.log`. No running API/UI restart or production event-log change is part of this verification. The isolated database tests alter only their own synthetic metadata rows to simulate future names. An initial version of the new database test retained a reused streaming mapper carrier; its capture callback was corrected to copy values while enumerating, then all 49 database cases passed on rerun.

## Gate before relocating types or changing writes

1. Inventory affected stored identities, including snapshots, outbox and durable replay records.
2. Add explicit historical aliases and payload fixtures for every affected contract; introduce an explicit payload upcaster if its schema changes.
3. Register the catalog in every process/tool that reads those records before deployment.
4. Deploy and qualify read compatibility before any writer emits a new identity.
5. Separately decide rollback compatibility: an old binary will not automatically understand new stable IDs. Keep legacy writes until that deployment/rollback policy is established.

This reader step removes one migration risk; it does not complete the Domain/Application/Framework layering migration.
