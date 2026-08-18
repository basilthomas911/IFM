# IFM Financial Modeling Prep Market Data Architecture

Status: Implemented
Version: 0.13
Date: 2026-08-18
Scope: Financial Modeling Prep US Treasury curve and economic-calendar acquisition and MarketData-domain import

## 1. Purpose

This document defines the architecture for `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep` (FMP). The project
obtains US Treasury curve and economic-calendar data from the Financial Modeling Prep stable APIs, normalizes the
responses into provider-neutral records, and supports their import through the existing yield-curve and economic-calendar
command paths.

The authoritative import request contracts are parameter-only:

- `ImportYieldCurveRatesParameter` carries `ImportDate` and `ErrorCode`; and
- `ImportEconomicCalendarsParameter` carries `ImportedDate`, optional `CountryCodes`, and `ErrorCode`.

Provider rows never enter a command or main import event. The MarketData import-event handler acquires them through
`IReferenceDataApi`, maps them to canonical read models, and passes one 0..N array to durable storage.

## 2. Decisions established by this design

1. FMP is an outbound data adapter. It never connects to ScyllaDB and never writes application tables.
2. An API-backed import starts at the domain actor: accept a parameter-only command, commit a parameter-only import
   event, acquire and normalize data in its event-family handler, then bulk-write the corresponding read table.
3. Direct table import means one bounded bulk storage operation after the import event. It does not mean issuing an
   `Add` or `Change` actor command for every returned row.
4. The command/event actor flow remains authoritative. No database write occurs before the import command and its main
   domain event have been accepted, but imported rows are transactional and are not replay state.
5. Duplicate behavior is configurable independently for yield curves and economic calendars.
6. The initial default duplicate policy is **Overwrite** because FMP can revise calendar forecasts, previous values,
   actual values, and other data after first publication.
7. **Reject** mode throws a typed duplicate exception and does not silently skip an existing logical key.
8. The effective duplicate policy is recorded on the committed import event and applied to that storage attempt.
9. A single-date import command is the initial execution and failure boundary. Date ranges may be split into multiple
   single-date commands.
10. Legacy row-array command payloads and external-query storage facades are removed; all imports use the same
    parameter-only actor workflow.
11. Economic calendar is market data. Its actors, shared contracts, models, storage interfaces, and table move from the
    Reference domain/storage boundary into the MarketData domain and `MarketDataDbContext`.
12. There is exactly one runtime economic-calendar row table: `economic_calendar_v2`. The former canonical-plus-projection design is
    replaced rather than copied into MarketData.
13. Runtime economic-calendar CQL never uses `ALLOW FILTERING` and never falls back to an unbounded table scan.
14. `ITreasuryCurve` and `IEconomicCalendar` are provider-neutral contracts in
    `TomasAI.IFM.Framework.MarketData.Contracts`; FMP supplies their production
    implementations. Application orchestration consumes `ITreasuryCurve`,
    selects the option-pricing rate, and passes that scalar into DataBento;
    DataBento does not implement or call either FMP-backed contract.

## 3. Current-state findings

### 3.1 External reader contexts

Before the FMP cutover, `Application.Storage` contained:

- `YieldCurveRatesDbContext`, which reads an external URI through the generic Framework Storage object reader; and
- `EconomicCalendarsDbContext`, which did the same for an economic-calendar URI.

These types are external source gateways despite their `DbContext` names. They are not the contexts that own the
durable ScyllaDB tables.

Both wrapper contexts are retired. The FMP abstraction owns acquisition, the MarketData event-family handlers own
import orchestration, and `MarketDataDbContext` owns only durable market-data reads and writes.

### 3.2 Current durable table ownership

The durable records are currently written as follows:

| Dataset | Current storage context | Current table or projection | Target |
| --- | --- | --- | --- |
| US Treasury curve canonical row | `MarketDataDbContext` | `yield_curve_rates` | Remains in MarketData |
| US Treasury ordered-date query projection | `MarketDataDbContext` | `yield_curve_rate_by_date` | Supports exact, bounded-range, and server-ordered latest reads |
| US Treasury year lookup | `MarketDataDbContext` | `yield_curve_rate_year` | Returns distinct bounded years without reading rate rows |
| Economic calendar legacy source | Offline migration only | `economic_calendar` | Preserved temporarily for rollback; never read at runtime |
| Economic calendar canonical row | `MarketDataDbContext` | `economic_calendar_v2` | Sole runtime row table and bounded country/month query source |
| Economic calendar country catalog | `MarketDataDbContext` | `economic_calendar_country_code` | Bounded observed-country lookup; contains no event rows |

`YieldCurveRateStateRepository` and `EconomicCalendarStateRepository` post their main imported events to the event
workflow. Their event-family handlers acquire provider-neutral data and invoke
`MarketDataDbContext.InsertYieldCurveRatesAsync` or `InsertEconomicCalendarsAsync` with one canonical array.

### 3.3 Existing duplicate behavior

ScyllaDB `INSERT` is an upsert unless conditional syntax is used. The current storage CQL therefore overwrites matching
keys. However, the current economic-calendar command state rejects an imported key that already exists, while the
yield-curve import state does not apply the same rejection rule.

The implementation must remove this inconsistency by applying one explicit effective policy at command decision,
event, and storage-write boundaries.

### 3.4 Yield-curve schema and deployed-table ordering

The canonical schema now declares `PRIMARY KEY ((id), valueDate)` with descending `valueDate` clustering, matching its
runtime CQL. Existing databases may still have the older ascending or incompatible definition because
`CREATE TABLE IF NOT EXISTS` cannot change a table's primary-key or clustering layout. Runtime reads therefore use the
additive `yield_curve_rate_by_date` projection. It has a constant `lookupId` partition and descending `valueDate`, so
latest is a server-side `LIMIT 1` read and ranges remain clustering-key reads. The offline market projection migration
rebuilds and fingerprints this projection from `yield_curve_rates` before cutover.

### 3.5 Credential exposure

Third-party API keys currently appear in checked-in test configuration/source. Those credentials must be considered
exposed, rotated, removed from Git-tracked content, and replaced with environment, user-secret, or host secret-provider
configuration before live FMP integration tests or runtime registration are enabled.

### 3.6 Economic-calendar query bounds

The current CQL does not contain a literal `ALLOW FILTERING` clause, but several paths execute an unbounded
`SELECT ... FROM economic_calendar` and filter or de-duplicate rows in application memory. Those full scans include the
fallback range path, the no-argument all-calendars query, country-code discovery, and projection reconciliation.

Runtime scans have now been removed. The paged request derives every month partition from its explicit UTC bounds and
country list; it rejects ranges over 120 months, more than 32 countries, or fan-out over 512 partitions. Each
country/month partition is limited to 2,500 rows and pages are limited to 500 rows. Full scans remain only in the
explicit offline cutover migration, where import writers are paused and source/target counts plus fingerprints are
reconciled.

## 4. Goals and non-goals

### 4.1 Goals

The architecture must:

- use the supported FMP stable endpoints;
- keep the API key outside URLs, logs, traces, exceptions, and source control;
- support cancellation and bounded date windows;
- map FMP responses explicitly rather than deserialize directly into domain models;
- use parameter-only import commands and main events;
- support one or more commands for single-date or date-range acquisition;
- write the corresponding tables directly through their existing bulk storage contexts;
- make overwrite versus duplicate rejection explicit and testable;
- preserve event-sourced command decisions without replaying transactional provider acquisition;
- handle FMP revisions to existing economic events; and
- move all economic-calendar ownership into the MarketData domain and storage context;
- use one economic-calendar table with key-complete CQL only;
- remove all `ALLOW FILTERING` and unbounded runtime table-scan behavior; and
- expose bounded operational metrics and actionable failures.

### 4.2 Non-goals

The first implementation does not:

- add equities, company fundamentals, earnings, news, or other FMP datasets;
- allow the FMP framework project to reference `Application.Storage`;
- allow an FMP response to bypass validation or actor command handling;
- add a new public date-range import command;
- make a multi-partition Scylla batch globally transactional;
- run a polling loop inside the FMP client;
- place API keys in connection strings; or
- modernize or approve the legacy scheduled-task framework or its import workflows.

## 5. System context

```text
UI through typed REST- or NATS-backed client
          |
          | submit a parameter-only import request and receive a command ID
          v
MarketData YieldCurveRate / EconomicCalendar command actor
          |
          | committed parameter-only import event with effective duplicate policy
          v
MarketData import event-family handler
          |
          +--> Application.MarketData.IReferenceDataApi
          |       |
          |       +--> FinancialModelingPrep adapter (treasury-rates / economic-calendar)
          |       |
          |       +--> provider-neutral records -> canonical domain array (0..N)
          |
          +--> MarketDataDbContext bulk storage
          |
          +--> correlated ImportedComplete or ImportedFail event
```

The event actor awaits acquisition and storage, so a terminal event cannot precede the durable write. Its asynchronous
handler does not block a thread. Each attempt is terminal: failure is recorded and a retry is a new command with a new
command ID.

## 6. Project boundary and dependencies

`TomasAI.IFM.Framework.MarketData.FinancialModelingPrep` targets .NET 10 and references:

- `TomasAI.IFM.Framework.MarketData` for the common market-data framework boundary;
- `TomasAI.IFM.Domain.MarketData.Shared` for both `YieldCurveRateReadModel` and, after its migration,
  `EconomicCalendarReadModel`.

The FMP project does not retain a target dependency on `TomasAI.IFM.Domain.Reference.Shared`. During implementation,
the economic-calendar shared contracts move first or are bridged temporarily by the application composition layer.

It does not reference `Application.Storage`, domain actor implementations, API hosts, or UI projects.

The provider project owns:

- FMP endpoint paths and query construction;
- typed HTTP execution;
- provider request and response DTOs;
- provider response validation;
- provider-to-canonical mapping;
- FMP-specific exceptions and error classification; and
- FMP request telemetry.

Application, domain, and storage layers own:

- the vendor-neutral `IReferenceDataApi` facade;
- command creation and event sourcing;
- import-event acquisition, mapping, and terminal-event publication;
- duplicate policy;
- MarketData table writes;
- import completion/failure events; and
- database reconciliation.

## 7. FMP API contract

### 7.1 Base address and authorization

The base address is `https://financialmodelingprep.com/stable/`. Initial endpoints are:

| Capability | Relative endpoint |
| --- | --- |
| US Treasury curve | `treasury-rates` |
| Economic data releases | `economic-calendar` |

Every request is date bounded using supported `from` and `to` query values. The API key is sent using the `apikey`
request header. It is never placed in the query string even though FMP supports query authentication.

### 7.2 Provider-neutral client contracts

The binding framework boundary is equivalent to:

```csharp
namespace TomasAI.IFM.Framework.MarketData.Contracts;

public interface ITreasuryCurve
{
    Task<TreasuryCurveSnapshot?> GetLatestAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TreasuryCurveSnapshot>> GetRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

public interface IEconomicCalendar
{
    Task<IReadOnlyList<EconomicCalendarEntry>> GetAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlySet<string>? countryCodes = null,
        CancellationToken cancellationToken = default);
}
```

The provider-neutral snapshot/entry records live beside these contracts in
`Framework.MarketData/Contracts/ReferenceData`. A `TreasuryCurveSnapshot`
contains its value date, country/currency, acquisition time/source, and a
unique ordered list of `TreasuryRatePoint` values. Each point identifies a
`TreasuryTenor` and stores an explicitly named `RatePercent`; its `DecimalRate`
property performs the percentage-point conversion required by pricing.

An `EconomicCalendarEntry` contains UTC event time, country, event name,
nullable actual/forecast/previous values, impact, unit, change,
change-percentage, acquisition time, and source. Provider values remain strings
because releases can contain suffixes, units, or status text. Missing data is
`null`, never a synthetic zero. The logical storage identity remains
`(EventTimeUtc, CountryCode, EventName)`.

These records contain no FMP DTO names or HTTP concerns. The concrete implementations are
`FinancialModelingPrepTreasuryCurve` and
`FinancialModelingPrepEconomicCalendar` in this project. Storage, application,
and DataBento consumers receive these abstractions through dependency
injection rather than constructing `HttpClient` or a concrete FMP client.

The FMP startup extension will register only the framework abstractions:

```csharp
services.AddFinancialModelingPrepMarketData(configuration);

// registrations owned by the extension
services.AddSingleton<ITreasuryCurve, FinancialModelingPrepTreasuryCurve>();
services.AddSingleton<IEconomicCalendar, FinancialModelingPrepEconomicCalendar>();
```

It does not register or reference the application-level `IMarketDataApi`.
Application startup composes these services into storage/import coordinators
and into the application market-data API as required.

An application-layer cache-aside decorator may cache a complete Treasury curve
by its published curve date. A hybrid high-frequency L1/L2 cache is not required
for this low-rate input. Provider adapters do not reference Blackboard.
Application orchestration resolves the curve/tenor and passes the selected
scalar rate into an option-chain session, so DataBento performs no FMP HTTP or
Redis operation on an option-tick hot path.

### 7.3 Date windows

The client validates `from <= to`, rejects unbounded requests, and chunks a larger requested range using a configured
maximum provider window. Adjacent chunks must not produce duplicate logical rows in the final normalized batch.

Treasury data is US-only. Economic-calendar country filtering is configurable; the default country set is decided by
host policy rather than hardcoded in the adapter.

### 7.4 Response bounds

Configuration limits:

- maximum request range;
- maximum response bytes;
- maximum normalized rows;
- maximum concurrent requests;
- request timeout; and
- total operation timeout.

Exceeding a bound fails the acquisition before a command is submitted. The system never truncates a successful-looking
import silently.

## 8. Data mapping

### 8.1 US Treasury curve

The provider DTO maps FMP maturity fields explicitly:

| FMP field | IFM field |
| --- | --- |
| `date` | `ValueDate` |
| `month1` | `OneMonth` |
| `month2` | `TwoMonth` |
| `month3` | `ThreeMonth` |
| `month6` | `SixMonth` |
| `year1` | `OneYear` |
| `year2` | `TwoYear` |
| `year3` | `ThreeYear` |
| `year5` | `FiveYear` |
| `year7` | `SevenYear` |
| `year10` | `TenYear` |
| `year20` | `TwentyYear` |
| `year30` | `ThirtyYear` |

The logical duplicate key is `ValueDate`. Missing maturities are not converted to zero because zero is a valid rate.
The provider DTO therefore uses nullable numeric values and mapping fails when a maturity required by the current IFM
model is absent or non-finite.

### 8.2 Economic calendar

The initial mapping is:

| FMP meaning | IFM field | Rule |
| --- | --- | --- |
| release timestamp | `EventDate` | Parse with explicit offset and normalize to UTC |
| country | `CountryCode` | Trim and normalize to the accepted ISO-like code |
| event name | `EventName` | Trim; required and non-empty |
| actual | `Actual` | Preserve provider representation; empty remains empty |
| estimate/forecast | `Forecast` | Preserve provider representation; empty remains empty |
| previous/prior | `Prior` | Preserve provider representation; empty remains empty |
| import timestamp | `CreatedOn` | UTC acquisition/import time under the current schema |
| provider identity | `CreatedBy` | Stable non-secret FMP importer identity |

The logical key is `(EventDateUtc, CountryCode, EventName)`. The framework
entry also preserves `impact`, `unit`, `change`, and `changePercentage`. The
current canonical model and Scylla tables cannot persist those fields. The
proposed implementation adds nullable `Impact`, `Unit`, `Change`, and
`ChangePercentage` fields using backward-compatible serialization keys and
storage columns; until that migration is approved, the application mapper must
report omitted fields rather than pretending they were stored.

The current mapper converts the provider timestamp to workstation local time and substitutes `"0"` for absent values.
The new mapper will not do either. Storage identity must not change with workstation time zone, and missing data must
remain distinguishable from a reported zero.

## 9. MarketData ownership and single-table storage

### 9.1 Domain migration

Economic calendar moves out of the Reference bounded context because releases, forecasts, actuals, and revisions are
time-sensitive market inputs used by trading and market analytics.

The target move includes:

- `EconomicCalendar` command, event, and query actors from `TomasAI.IFM.Domain.Reference` to
  `TomasAI.IFM.Domain.MarketData`;
- command parameters, commands, events, queries, entity IDs, read models, validation, service APIs, and API DTOs from
  `TomasAI.IFM.Domain.Reference.Shared` to `TomasAI.IFM.Domain.MarketData.Shared`;
- economic-calendar methods from `IReferenceCommandApi` and `IReferenceQueryApi` to the corresponding MarketData
  command/query APIs, including their REST and NATS client implementations;
- state repositories and denormalization dependencies from `IReferenceDbContext` to `IMarketDataDbContext`;
- actor registrations and dependency injection from the Reference actor assembly to the MarketData actor assembly;
- REST and NATS client imports/namespaces while preserving external subjects and routes where compatibility requires;
  and
- all economic-calendar schema, CQL, mapping, and read/write methods from `ReferenceDbContext` into
  `MarketDataDbContext`.

The migration preserves serialized MessagePack keys, command/event names, verbs, error identifiers, entity-ID format,
and existing import command parameters. If persisted event type resolution includes the old CLR namespace or assembly,
an explicit type alias/upcaster maps historical Reference-domain economic-calendar events to the MarketData types.
Historical streams are not abandoned or rewritten casually.

The MarketData API becomes the authoritative public surface. If a compatibility window is required, old Reference API
routes forward to the MarketData contract and are marked obsolete; they do not retain a second Reference actor or
storage implementation.

After cutover, the Reference projects have no economic-calendar actor, shared contract, API, storage, schema, CQL, or
registration dependency.

### 9.2 Single table schema

The target MarketData keyspace contains exactly one runtime economic-calendar row table. A versioned name is required
because ScyllaDB cannot alter the primary key of the legacy table in place:

```cql
CREATE TABLE economic_calendar_v2 (
    countryCode text,
    monthBucket int,
    eventDate timestamp,
    eventName text,
    actual text,
    forecast text,
    prior text,
    impact text,
    unit text,
    createdOn timestamp,
    createdBy text,
    commandId uuid,
    PRIMARY KEY ((countryCode, monthBucket), eventDate, eventName)
) WITH CLUSTERING ORDER BY (eventDate DESC, eventName ASC);
```

`monthBucket` is the UTC `yyyyMM` integer derived from `EventDate`. It is stored because it is part of the physical
partition key, but it is not a separate domain identity field. The logical identity remains
`(EventDateUtc, CountryCode, EventName)`.

This layout promotes the useful shape of the former country/month projection to the only canonical row table.
`commandId` makes same-command Reject replay observable at the row that owns the logical key. The old event-date
table and all `by_country_month`, `by_month`, and month-catalog projections are removed after the verification window.

### 9.3 Key-complete query patterns

Every runtime CQL statement supplies `countryCode` and `monthBucket`. Supported queries are:

| Operation | Partition strategy | CQL behavior |
| --- | --- | --- |
| Get exact event | Derive month bucket from the ID timestamp and use its country | One partition plus full clustering key |
| Get one country/day | One country/month partition | Bounded `eventDate` clustering range |
| Get one country/range | One partition read for each intersecting UTC month | Bounded fan-out and merge-sort |
| Get configured countries/range | Country list multiplied by intersecting months | Bounded parallel fan-out with concurrency limit |
| Insert/overwrite | Group rows by country/month | Partition-local writes/upserts |
| Reject duplicate | Country/month plus full event key | Conditional `IF NOT EXISTS` at the target row |
| Delete | Derive country/month from the old ID | Full primary key delete |
| Change identity | Delete old full key, insert new full key | Two explicit key-complete mutations |

No supported query appends `ALLOW FILTERING`. No normal request executes `SELECT ... FROM economic_calendar` without a
partition predicate. Client-side filtering may only refine rows already read from explicitly addressed bounded
partitions; it cannot be used to turn a full-table scan into a query implementation.

### 9.4 Replacement for unbounded queries

The existing no-argument `GetEconomicCalendarAllAsync` contract is incompatible with the target Scylla model. It is
deprecated and replaced by a bounded query requiring:

- `StartDateUtc` and `EndDateUtc`;
- one or more authorized country codes; and
- a configured maximum range, row count, partition count, and fan-out concurrency.

A temporary compatibility implementation may apply a configured date horizon and configured country set, but it must
not scan the table. If the bounded defaults are absent, the compatibility query fails validation.

`GetEconomicCalendarCountryCodesAsync` also stops scanning and de-duplicating the event table. Country codes come from
the enabled FMP/import configuration and are projected into the MarketData actor read model from accepted imports. The
query returns that bounded actor read model; it does not discover countries through CQL.

An administrative offline export or repair tool may token-scan the table under explicit resource controls. That is not
a runtime query API and does not justify `ALLOW FILTERING`.

#### 9.4.1 Implemented paged contract

`GetEconomicCalendarPage` is available through the direct actor API, REST, and NATS clients. It requires UTC start/end
bounds, at least one country, a page size of at most 500, and an opaque continuation token. Validation caps ranges at
120 months, countries at 32, and total country/month fan-out at 512 partitions. Each partition is capped at 2,500 rows.
Tokens are request-bound and rejected when malformed or replayed with different bounds/countries.

The no-argument all-calendar and external-calendar contracts are obsolete. The external contract no longer has a
storage facade and returns an explicit instruction to use the authenticated FMP import endpoint. The country and
yield-curve-year catalogs are monotonic observed-value indexes; they contain lookup values, not calendar event rows.

### 9.5 Storage migration and cutover

Migration is a controlled operation:

1. pause calendar imports and create `economic_calendar_v2`, `economic_calendar_country_code`, and
   `economic_calendar_cutover_v2` without deploying the new runtime binary;
2. run the Market projection migration tool, which reads only legacy `economic_calendar` as the source;
3. clear the offline target, normalize timestamps to UTC, calculate `monthBucket`, and rebuild `economic_calendar_v2` in bounded batches;
4. compare source/target logical-key counts and deterministic row digests;
5. rebuild and replay affected actor state using the MarketData type aliases;
6. deploy the runtime binary, whose command/query actors and FMP imports use only `economic_calendar_v2`;
7. run key-complete query, import, overwrite, Reject, and recovery tests;
8. observe a defined verification window; and
9. after the rollback window, drop `economic_calendar`, `economic_calendar_by_country_month_v2`,
   `economic_calendar_by_month_v1`, and `economic_calendar_month_v1`.

The migration never attempts to merge both old tables as independent sources of truth. The old canonical table is the
source; projection discrepancies are reported and resolved before cutover.

## 10. API-backed import operation

### 10.1 Parameter-only request contracts

REST and NATS clients expose the same authenticated command API. Both invoke the existing MarketData domain command
actors; neither transport owns a separate import coordinator or provider call.

```text
ImportYieldCurveRatesParameter(ImportDate, ErrorCode)
  -> ImportYieldCurveRatesCommand
  -> YieldCurveRatesImportedEvent(ImportDate, DuplicatePolicy)

ImportEconomicCalendarsParameter(ImportedDate, CountryCodes, ErrorCode)
  -> ImportEconomicCalendarsCommand
  -> EconomicCalendarsImportedEvent(ImportedDate, CountryCodes, DuplicatePolicy)
```

The command and main event schemas intentionally contain no imported records. Historic array-carrying request schemas
and the `GetExternal*` query surfaces are not supported by the authoritative flow.

### 10.2 Single-date workflow

1. The caller requests acquisition for one date.
2. The command actor validates the request and resolves the configured duplicate policy.
3. The command state emits a parameter-only main imported event.
4. The state repository posts that event to its MarketData event actor without treating rows as replay state.
5. The event-family handler calls `IReferenceDataApi.TreasuryCurve` or `.EconomicCalendar`.
6. The FMP client validates HTTP status, content type, response bounds, JSON, and required fields.
7. The handler maps provider-neutral records into canonical domain read models.
8. The handler calls the array-based storage API once, including when the array is empty.
9. After storage succeeds, the handler sends a correlated complete event containing the canonical 0..N records.
10. On acquisition, mapping, validation, or storage failure, the handler sends a correlated fail event and no complete
    event.

An empty valid FMP response is a successful zero-record import. It is distinguishable from every failure.

### 10.3 Date-range workflow

The application coordinator splits an API-requested range into deterministic single-date command submissions. It does
not acquire provider data. Commands use stable ordering, and cancellation stops submission of new commands without
cancelling a command already durably accepted.

This approach gives every date its own correlation and retry boundary while keeping actor messages bounded.

### 10.4 Direct storage writes

The import event handler writes a batch:

- yield curves call one bounded `InsertYieldCurveRatesAsync` operation that maintains `yield_curve_rates`,
  `yield_curve_rate_by_date`, and the de-duplicated year lookup; and
- economic calendars call one bounded `MarketDataDbContext.InsertEconomicCalendarsAsync` operation that maintains the
  canonical country/month-partitioned rows and country lookup catalog.

The handler does not emit one `Add` or `Change` command per row. Native storage batching may be used only within the
driver's safe size and partition constraints. Storage contexts contain no FMP-derived interface and perform no HTTP
request.

## 11. Duplicate policy

### 11.1 Configuration

Duplicate policy is configured per dataset:

```text
FinancialModelingPrepImport:
  YieldCurveRates:
    DuplicatePolicy: Overwrite | Reject
  EconomicCalendar:
    DuplicatePolicy: Overwrite | Reject
```

The initial default for both is `Overwrite`. Configuration is validated at startup. Unknown values fail startup rather
than falling back silently.

The current import command parameters do not gain a policy property. The command actor resolves the effective policy,
records it on the import event, and the import event-family handler passes that recorded value to storage. A later command contract may
allow an authorized per-request override.

### 11.2 Duplicate categories

The importer distinguishes:

| Condition | Overwrite | Reject |
| --- | --- | --- |
| Same key and byte-equivalent normalized data repeated in one FMP response | Collapse deterministically and record a metric | Throw duplicate exception before command submission |
| Same key with conflicting normalized values in one response | Throw ambiguous-provider-data exception | Throw ambiguous-provider-data exception |
| Logical key already exists in domain state/storage | Replace the stored values | Throw duplicate exception |
| Same accepted command is replayed or retried | Idempotent completion | Idempotent completion for that command, not a new duplicate failure |

Conflicting rows within one provider response fail in both modes because choosing a winner by response order is not a
safe overwrite rule.

### 11.3 Overwrite behavior

Overwrite uses normal Scylla upsert behavior for matching primary keys. It is appropriate for FMP calendar records,
whose forecast, prior, actual, unit, and impact can change as an event approaches or is published.

For economic calendars, overwrite updates the one `MarketDataDbContext.economic_calendar` row. If normalization changes
a logical key, the operation is a delete-old plus insert-new change, not an overwrite of the old identity.

Command state contains no imported rows. The event-family handler passes the canonical batch and effective policy to
storage, where matching logical keys are overwritten.

### 11.4 Reject behavior

Reject mode uses a conditional ownership write to protect against concurrent writers. A check-then-unconditional-insert
implementation is not sufficient. A duplicate produces `MarketDataImportDuplicateException` containing bounded,
non-secret operation context.

For economic calendars, the single `economic_calendar` row is the conditional ownership point. No second projection
write or projection-repair workflow exists after migration.

### 11.5 Retry and partial-failure constraint

ScyllaDB does not make arbitrary multi-partition date-range imports globally atomic. A process failure can occur after
some rows have been applied. Therefore:

- a command ID is the storage ownership/idempotency identity for its single attempt;
- overwrite is naturally idempotent for the same normalized values;
- Reject recognizes rows already owned by the same command rather than classifying them as foreign duplicates; and
- initial range orchestration uses single-date commands to bound any partial result.

The event workflow does not automatically replay a failed external acquisition. An authorized UI retry submits a new
command because current provider data is desired; the old attempt remains terminally failed. Legacy scheduled-task
retry behavior is deferred until that framework is separately reviewed.

## 12. Exceptions and result semantics

The implementation uses distinct failures for:

- invalid date range or country filter;
- authentication/authorization failure;
- rate limiting;
- transient provider unavailability;
- malformed or oversized provider response;
- missing required FMP fields;
- ambiguous provider duplicates;
- existing storage duplicate under Reject policy;
- storage write failure; and
- cancellation.

The current external contexts swallow most exceptions and return an empty collection. That behavior is not retained for
FMP imports. An empty list means FMP successfully returned no qualifying rows; it never means authentication, parsing,
timeout, or storage failure.

Range submission results report accepted or rejected command submissions. Actual row counts and final success/failure
belong to the correlated complete/fail events and operational logs, not the initial command response.

## 13. HTTP resilience

The future typed client uses one managed `HttpClient` and propagates the caller's cancellation token through send,
deserialization, mapping, and orchestration.

It applies:

- a bounded per-attempt timeout and total operation deadline;
- retry only for safe GET requests and transient network, `429`, and selected `5xx` failures;
- `Retry-After` when supplied, otherwise capped exponential backoff with jitter;
- no retry for authentication, authorization, validation, or malformed-payload failures;
- concurrency limiting aligned with the subscribed FMP plan; and
- a circuit breaker that fails quickly during sustained provider failure.

HTTP retries occur only inside acquisition for safe transient failures. Once the handler produces a fail event, another
end-to-end import is a new domain command.

## 14. Configuration and secrets

Non-secret configuration includes:

- stable base URI;
- endpoint paths;
- default and maximum date windows;
- allowed economic-calendar country codes;
- response and row limits;
- timeout and retry budgets;
- provider concurrency/rate limits;
- per-dataset duplicate policy; and
- telemetry thresholds.

The API key is supplied by environment/user secrets in development and the host secret provider in deployed
environments. Options validation rejects a missing key when FMP is enabled. Secret values are redacted from HTTP
logging and never stored in `ConnectionStrings`.

Repository history containing exposed keys is not silently rewritten as part of this project. The keys are rotated,
tracked files are cleaned, and the repository is scanned before live tests are authorized. History remediation is a
separate explicit security decision if required.

## 15. Observability

Metrics include bounded labels for dataset and outcome:

- FMP request count, duration, retries, status class, and response bytes;
- normalized row count and rejected-row count;
- acquisition-to-command latency;
- import command count and duration;
- inserted, overwritten, rejected, and exact-duplicate counts;
- no-data date count;
- storage write and partition-fan-out failure count;
- provider rate-limit/circuit state; and
- last successful acquisition/import age by dataset.

Metrics never label individual dates, event names, URLs, command IDs, or API keys. Those details belong in structured,
bounded logs and traces.

## 16. Test strategy

### 16.1 Provider unit and contract tests

- treasury and calendar fixture deserialization;
- complete field mapping;
- missing/null/invalid field behavior;
- UTC and offset normalization;
- country filtering;
- response bounds;
- cancellation;
- HTTP error classification; and
- secret-redaction checks.

Fixtures are sanitized and contain no live key.

### 16.2 Import command compatibility tests

- current parameter MessagePack/JSON contracts remain compatible;
- FMP results populate the current arrays correctly;
- a single date creates one current import command;
- a range creates deterministic single-date commands;
- empty dates do not create successful empty imports; and
- cancellation stops later command submission.

### 16.3 Duplicate-policy tests

- overwrite inserts new rows;
- overwrite revises the existing row in the single calendar table;
- Reject throws for an existing key;
- Reject conditional writes close the preflight race;
- exact in-response duplicates follow the policy;
- conflicting in-response duplicates always fail;
- same-command replay is idempotent; and
- partial failure resumes without misclassifying the same command's rows.

### 16.4 Storage integration tests

- direct yield-curve batch import into `yield_curve_rates`;
- direct economic-calendar batch import into the single MarketData table;
- exact, day, country/range, and configured-country bounded fan-out queries without `ALLOW FILTERING`;
- validation that runtime CQL contains no unbounded economic-calendar table scans;
- schema/runtime CQL agreement for the yield-curve key;
- concurrent imports under both policies;
- single-date and multi-command range behavior; and
- command/event failure and completion outcomes.

### 16.5 Live tests

Live FMP tests are opt-in, secret-gated, date-bounded, rate-limited, and excluded from the default suite. They verify
endpoint access and contract drift without writing production tables.

### 16.6 UI process acceptance

The G2-016 through G2-019 Development process slice proves the manual and provider-backed treasury paths through the
real WinForms editor. The editor exposes an explicit import-date picker; it sends that selected date through the typed
NATS client as a parameter-only domain command and never calls FMP or storage directly. The process test observes the
source and terminal events by exact command ID, compares the terminal event's canonical 0..N provider rows with typed
durable queries and refreshed visible state, and restores the captured baseline through public domain commands.

Accepted run `20260818-170357-a09e681e82d84bab8fb514a427f358ef` used the production FMP adapter and returned one
treasury row for `2026-07-17`. It also proved manual add/change/remove without invoking FMP and completed cleanup. This
is UI process acceptance of the existing architecture, including its valid zero-row semantics; it does not move provider
acquisition into the UI or storage layers.

## 17. Implementation stages after design approval

### Stage 1: security and contracts

1. Rotate and remove checked-in third-party keys.
2. Add validated FMP options and secret injection.
3. Define the client abstraction, provider DTOs, mappings, and typed exceptions.
4. Add sanitized provider contract fixtures.

### Stage 2: domain and storage ownership migration

1. Move economic-calendar shared contracts into `Domain.MarketData.Shared` with serialization compatibility.
2. Move actors and state repositories into `Domain.MarketData`.
3. Add the one country/month-partitioned table to `MarketDataDbContext`.
4. Migrate and verify the legacy MarketData calendar source, then remove the old row/projection tables after rollback.
5. Replace unbounded APIs with key-complete bounded fan-out queries.

### Stage 3: provider client

1. Implement the typed HTTP client and resilience policies.
2. Retire both external-reader storage contexts and every `GetExternal*` query surface.
3. Add provider unit, contract, and opt-in live tests.

### Stage 4: parameter-only actor import

1. Make REST- and NATS-backed typed clients submit parameter-only commands to the same domain actors; UI consumes the
   client abstraction.
2. Split date ranges into deterministic single-date commands.
3. Record request parameters and effective duplicate policy on main import events.
4. Acquire through `IReferenceDataApi` in the event-family handlers and publish correlated terminal events.
5. Make imported command events operation markers rather than replayed row state.

### Stage 5: storage policies

1. Implement direct overwrite bulk writes.
2. Implement conditional Reject writes and same-command retry identity.
3. Verify partition-local economic-calendar writes and key-complete reads.
4. Add integration, concurrency, and failure-injection tests.

### Stage 6: UI terminal-operation integration

1. Register the provider and import coordinator in the host.
2. Add metrics, alerts, health, and bounded logging.
3. Roll out correlated complete/fail tracking to UI operations through the typed client and event listener, following
   the system-wide UI terminal-operation convention.
4. Use `YieldCurveRateEditorViewModel` as the reference implementation and apply the same correlation lifecycle to
   `EconomicCalendarEditorViewModel` imports.
5. Use the shared terminal-correlation primitive for the application shell's automatic startup imports. Start both
   listeners first, attempt each import once, observe each exact command ID for up to 30 seconds, report only
   failed/unobserved outcomes, perform no retry, and continue startup so a user can import later.
6. Run affected UI, client, domain, serialization, and storage integration suites.
7. Keep legacy scheduled tasks outside this rollout until their task lifecycle, status persistence, retry, recovery, and
   user-observation requirements have been reviewed and redesigned.

## 18. Acceptance criteria

The design is implemented only when:

1. The FMP project has no storage or host dependency.
2. API keys never appear in tracked files, URLs, logs, traces, or exceptions.
3. Both stable endpoints use bounded dates and cancellation.
4. Provider DTOs are separate from canonical read models.
5. Treasury fields map completely without treating missing values as zero.
6. Calendar timestamps are normalized to UTC and missing values remain distinguishable from zero.
7. Import commands and main events contain request parameters but no provider rows.
8. An API-backed range is represented by multiple single-date command submissions.
9. Event-family handlers acquire through `IReferenceDataApi`; storage has no FMP dependency.
10. Import commands remain event sourced before table writes.
11. Imports write the target tables directly in bounded batches rather than emitting per-row commands.
12. Overwrite and Reject are independently configurable per dataset.
13. The effective policy is persisted on the import event.
14. Overwrite is the initial default and updates the single target row.
15. Reject throws a typed exception and uses a conditional storage write.
16. Same-command storage ownership is idempotent under both policies.
17. Conflicting provider duplicates fail in both policies.
18. Range partial-failure semantics are visible and retryable by date.
19. Yield-curve schema and runtime CQL use the same primary key.
20. Economic-calendar contracts and actors reside in the MarketData domain and shared project.
21. `MarketDataDbContext` owns the only runtime economic-calendar row table, `economic_calendar_v2`.
22. The Reference domain and storage context contain no economic-calendar behavior after cutover.
23. Every runtime economic-calendar CQL query supplies a complete partition key or performs bounded fan-out over known
    country/month keys.
24. No economic-calendar CQL uses `ALLOW FILTERING` or an unbounded table scan.
25. The no-argument all-calendar query is removed or constrained by explicit configured bounds without scanning.
26. Country-code queries use configured/projected actor state rather than scanning event rows.
27. Provider, import, duplicate, concurrency, migration, and storage tests pass.
28. Live tests remain opt-in and cannot run without explicit secret configuration.
29. `ITreasuryCurve` and `IEconomicCalendar` are defined in
    `Framework.MarketData.Contracts` and implemented by the FMP project.
30. Application orchestration resolves and passes the selected rate; DataBento
    performs no FMP HTTP or Blackboard/Redis operation on its option-record hot
    path.
31. A valid zero-row provider response performs an empty bulk call and produces a successful complete event.
32. Acquisition, mapping, validation, and storage failures produce a fail event and never a complete event.
33. Yield-curve and economic-calendar maintenance imports use command IDs to observe terminal success/failure; a retry
    submits a new command.
34. Request, complete, and fail schemas have serialization round-trip tests.
35. Automatic desktop startup attempts both imports once before the live-feed trading-hours gate, observes each
    correlated terminal result for a bounded 30 seconds, reports failed/unobserved results without retry, cleans up its
    startup-only listeners, and allows normal startup plus later manual import to continue.
36. Legacy scheduled tasks are not represented as terminal-tracking compliant or rollout-ready until their separate
    review and redesign is complete.

## 19. Decisions requested during review

| Decision | Proposed direction |
| --- | --- |
| Initial command contract | Parameter-only `ImportYieldCurveRatesParameter` and `ImportEconomicCalendarsParameter` |
| API-backed range | Issue deterministic single-date commands; each event handler fetches its requested date |
| Direct write meaning | One 0..N bulk storage call from the main import-event handler; no per-row actor commands |
| Yield target | `MarketDataDbContext.yield_curve_rates` |
| Calendar domain | Move actors and all shared contracts from Reference to MarketData |
| Calendar storage owner | Move reads, writes, schema, and CQL to `MarketDataDbContext` |
| Calendar table | Exactly one `economic_calendar` table; remove `economic_calendar_by_country_month_v2` |
| Calendar primary key | `((countryCode, monthBucket), eventDate, eventName)` with UTC `yyyyMM` bucket |
| Calendar queries | Key-complete partition reads or bounded country/month fan-out only |
| `ALLOW FILTERING` | Prohibited in every runtime economic-calendar CQL statement |
| Unbounded all query | Deprecate/replace with required bounded dates and country set |
| Country-code query | Read configuration/actor projection, never scan the calendar table |
| Initial duplicate default | `Overwrite` for both datasets |
| Duplicate configuration | Independent `Overwrite` or `Reject` setting per dataset |
| Policy audit | Record effective policy on every import event |
| Reject concurrency | Conditional ownership through `IF NOT EXISTS`; never check then unconditionally insert |
| Range atomicity | No global transaction claim; single-date commands bound partial failure |
| Economic calendar revisions | Overwrite the matching row in the single MarketData table |
| Calendar supplemental values | Add optional `Impact`, `Unit`, `Change`, and `ChangePercentage` canonical/storage fields before claiming full FMP fidelity |
| Time handling | Normalize provider timestamps to UTC; do not convert identity to workstation local time |
| Missing values | Preserve missing/empty distinction; do not synthesize `"0"` |
| Yield key | Reconcile schema/runtime CQL on `valueDate` before enabling imports |
| Credential gate | Rotate exposed keys and remove plaintext secrets before live use |
| Framework contracts | Define provider-neutral `ITreasuryCurve` and `IEconomicCalendar` in `Framework.MarketData.Contracts` |
| Provider ownership | Implement both contracts in Financial Modeling Prep; application orchestration consumes Treasury data and passes the selected rate to DataBento |
| Terminal-operation rollout | Apply exact-ID terminal tracking to maintenance editors and one-attempt automatic desktop startup; use a 30-second startup observation bound, failure-only presentation, no retry, and degraded continuation; defer all legacy scheduled-task claims until a separate scheduler review |

## 20. References

- [UI Terminal-Operation Tracking and Rollout](../../Documents/system/UI-Terminal-Operation-Tracking-and-Rollout.md)
- [FMP stable Treasury Rates API](https://site.financialmodelingprep.com/developer/docs/stable/treasury-rates)
- [FMP stable Economic Data Releases Calendar API](https://site.financialmodelingprep.com/developer/docs/stable/economics-calendar)
- [FMP API quickstart and authentication](https://site.financialmodelingprep.com/developer/docs/quickstart)
- [FMP published dataset cycle times](https://site.financialmodelingprep.com/developer/docs/cycle-times)
- [FMP changelog](https://site.financialmodelingprep.com/developer/docs/changelog)

## 21. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.13 | 2026-08-18 | Accepted G2-016 through G2-019 through the real Development UI: explicit operator-selected treasury import date, exact-ID source/terminal correlation, production FMP canonical result matched to durable and visible state, manual yield-curve maintenance, and public-command baseline restoration. |
| 0.12 | 2026-08-16 | Added the shared terminal-correlation primitive and migrated automatic desktop yield/calendar startup imports to listener-first exact-ID tracking, 30-second bounded observation, failure-only reporting, no retry, cleanup, and continued startup outside the live-feed trading-hours gate. |
| 0.11 | 2026-08-16 | Implemented economic-calendar editor terminal tracking with exact command-ID complete/fail correlation, early-event buffering, durable projection refresh after complete, typed failure, independent listener lifecycle, and focused UI tests. |
| 0.10 | 2026-08-16 | Scoped terminal-operation rollout to UI, named the yield-curve editor as the reference pattern and economic-calendar editor as the next migration, corrected import-handler storage ownership wording, and explicitly deferred legacy scheduled-task review. |
| 0.9 | 2026-08-16 | Established the authoritative parameter-only actor import flow, event-handler acquisition through `IReferenceDataApi`, 0..N bulk storage calls, correlated terminal events, transactional non-replay semantics, and removal of both legacy external-query storage facades. |
| 0.8 | 2026-08-16 | Completed the versioned single-table calendar cutover, request-bound paged actor/REST/NATS contract, direct canonical LWT Reject behavior, restartable reconciliation tool, compatibility projection removal, and external-calendar facade retirement. |
| 0.7 | 2026-08-15 | Implemented the FMP adapters, compatibility facades, deterministic application import coordinator, independently configured event-persisted duplicate policies, LWT Reject ownership, supplemental calendar persistence, host/API/schedule/health/metrics wiring, secret cleanup, tests, and rollout migration instructions. |
| 0.6 | 2026-08-10 | Defined the provider-neutral framework contracts and storage-capable records in code; made Treasury percentage units explicit, preserved UTC/provenance and nullable calendar values including impact/unit/change fields, and specified FMP-to-framework DI registration without any dependency on the application API. |
| 0.5 | 2026-08-10 | Removed the high-frequency Blackboard L1/L2 requirement: Treasury curves may use ordinary application cache-aside, while DataBento owns its in-process quote/trade hot values and continues to receive only the selected scalar rate. |
| 0.4 | 2026-08-10 | Clarified that application orchestration owns Treasury L1/L2 caching and tenor selection and passes the selected scalar rate into DataBento, which performs no FMP or Blackboard calls. |
| 0.3 | 2026-08-10 | Made `ITreasuryCurve` and `IEconomicCalendar` binding provider-neutral framework contracts, assigned both implementations to the FMP adapter, and documented application Blackboard caching plus DataBento's consumer-only hot-path boundary. |
| 0.2 | 2026-08-10 | Moved target economic-calendar ownership from Reference to MarketData, replaced the canonical-plus-projection layout with one country/month-partitioned MarketData table, prohibited `ALLOW FILTERING` and runtime full-table scans, redesigned bounded queries, and added the domain/storage data migration path. |
| 0.1 | 2026-08-10 | Created the FMP architecture and defined API-backed imports using current command parameters, direct event-denormalized table writes, configurable Overwrite/Reject duplicate behavior, single-date range partitioning, mapping, resilience, secrets, observability, testing, and review decisions. |
