# IFM Financial Modeling Prep Market Data Architecture

Status: Implemented
Version: 0.7
Date: 2026-08-15
Scope: Financial Modeling Prep US Treasury curve and economic-calendar acquisition and MarketData-domain import

## 1. Purpose

This document defines the architecture for `TomasAI.IFM.Framework.MarketData.FinancialModelingPrep` (FMP). The project
obtains US Treasury curve and economic-calendar data from the Financial Modeling Prep stable APIs, normalizes the
responses into existing IFM models, and support their import through the existing yield-curve and economic-calendar
command paths.

The first implementation preserves the current command parameter contracts:

- `ImportYieldCurveRatesParameter` with `ImportDate`, `YieldCurveRates`, and `ErrorCode`; and
- `ImportEconomicCalendarsParameter` with `ImportedDate`, `EconomicCalendars`, and `ErrorCode`.

An API-backed import may issue multiple existing import commands for a single date or date range. No new date-range
command contract is required for the first implementation.

## 2. Decisions established by this design

1. FMP is an outbound data adapter. It never connects to ScyllaDB and never writes application tables.
2. An API-backed import is a composed operation: fetch from FMP, normalize and validate, construct the current import
   command parameters, commit the domain event, then directly bulk-write the corresponding read tables.
3. Direct table import means one bounded bulk storage operation after the import event. It does not mean issuing an
   `Add` or `Change` actor command for every returned row.
4. Event sourcing remains authoritative. No database write occurs before the import command and its domain event have
   been accepted.
5. Duplicate behavior is configurable independently for yield curves and economic calendars.
6. The initial default duplicate policy is **Overwrite** because FMP can revise calendar forecasts, previous values,
   actual values, and other data after first publication.
7. **Reject** mode throws a typed duplicate exception and does not silently skip an existing logical key.
8. The effective duplicate policy is recorded on the committed import event. Changing configuration later cannot alter
   replay or denormalization semantics for an existing event.
9. A single-date import command is the initial execution and failure boundary. Date ranges may be split into multiple
   single-date commands.
10. The existing command payload arrays remain supported. The API-backed coordinator populates those arrays from FMP.
11. Economic calendar is market data. Its actors, shared contracts, models, storage interfaces, and table move from the
    Reference domain/storage boundary into the MarketData domain and `MarketDataDbContext`.
12. There is exactly one economic-calendar table: `economic_calendar`. The current canonical-plus-projection design is
    replaced rather than copied into MarketData.
13. Runtime economic-calendar CQL never uses `ALLOW FILTERING` and never falls back to an unbounded table scan.
14. `ITreasuryCurve` and `IEconomicCalendar` are provider-neutral contracts in
    `TomasAI.IFM.Framework.MarketData.Contracts`; FMP supplies their production
    implementations. Application orchestration consumes `ITreasuryCurve`,
    selects the option-pricing rate, and passes that scalar into DataBento;
    DataBento does not implement or call either FMP-backed contract.

## 3. Current-state findings

### 3.1 External reader contexts

`Application.Storage` currently contains:

- `YieldCurveRatesDbContext`, which reads an external URI through the generic Framework Storage object reader; and
- `EconomicCalendarsDbContext`, which does the same for an economic-calendar URI.

These types are external source gateways despite their `DbContext` names. They are not the contexts that own the
durable ScyllaDB tables.

The FMP adapter will replace their provider-specific URI and JSON-reading behavior. Their public `ReadAsync` methods may
remain as short-lived compatibility facades during the first implementation. In the target architecture,
`EconomicCalendarsDbContext` is retired: the FMP abstraction owns acquisition, the MarketData application coordinator
owns import orchestration, and `MarketDataDbContext` owns durable economic-calendar reads and writes.

### 3.2 Current durable table ownership

The durable records are currently written as follows:

| Dataset | Current storage context | Current table or projection | Target |
| --- | --- | --- | --- |
| US Treasury curve canonical row | `MarketDataDbContext` | `yield_curve_rates` | Remains in MarketData |
| US Treasury ordered-date query projection | `MarketDataDbContext` | `yield_curve_rate_by_date_v1` | Supports exact, bounded-range, and server-ordered latest reads |
| US Treasury year lookup | `MarketDataDbContext` | `yield_curve_rate_year_v1` | Returns distinct bounded years without reading rate rows |
| Economic calendar canonical row | `MarketDataDbContext` | `economic_calendar` | Remains the current migration/backfill source |
| Economic calendar country/month query projection | `MarketDataDbContext` | `economic_calendar_by_country_month_v2` | Supports bounded country/date ranges |
| Economic calendar bounded-all projection | `MarketDataDbContext` | `economic_calendar_by_month_v1` | Supports bounded recent-month reads across countries |
| Economic calendar lookup projections | `MarketDataDbContext` | `economic_calendar_country_code_v1`, `economic_calendar_month_v1` | Avoid event-table scans and supply known partitions |

`YieldCurveRateStateRepository` denormalizes `YieldCurveRatesImportedEvent` directly through
`InsertYieldCurveRatesAsync`. The current Reference-domain `EconomicCalendarStateRepository` denormalizes
`EconomicCalendarsImportedEvent` through `ReferenceDbContext.InsertEconomicCalendarsAsync`. The target moves that actor
repository and bulk write into the MarketData domain and `MarketDataDbContext`.

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
additive `yield_curve_rate_by_date_v1` projection. It has a constant `lookupId` partition and descending `valueDate`, so
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

Runtime scans have now been removed. Country codes and known month partitions come from small lookup projections;
all-calendar reads fan out only over the latest 120 known UTC months, and country/range reads reject ranges over 120
UTC months. At most four month partitions are queried concurrently. Each month read is limited to 2,500 rows and the
merged response is limited to 10,000 rows. Full scans remain only in the explicit offline projection migration, where
import writers are paused and source/target counts plus fingerprints are reconciled.

## 4. Goals and non-goals

### 4.1 Goals

The architecture must:

- use the supported FMP stable endpoints;
- keep the API key outside URLs, logs, traces, exceptions, and source control;
- support cancellation and bounded date windows;
- map FMP responses explicitly rather than deserialize directly into domain models;
- retain the current import command parameter contracts;
- support one or more commands for single-date or date-range acquisition;
- write the corresponding tables directly through their existing bulk storage contexts;
- make overwrite versus duplicate rejection explicit and testable;
- preserve event-sourced command decisions and deterministic replay;
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
- change the economic-calendar or yield-curve UI workflow beyond what is needed to invoke existing imports.

## 5. System context

```text
UI / API / ScheduledTask
          |
          | request an API-backed import for a date or bounded date range
          v
Application import coordinator
          |
          | async HTTPS with cancellation
          v
FinancialModelingPrep adapter
  - treasury-rates
  - economic-calendar
          |
          | provider DTO -> canonical read model
          v
YieldCurveRatesDbContext / EconomicCalendarsDbContext transitional compatibility facade
          |
          | construct current Import*Parameter and Import*Command
          v
MarketData YieldCurveRate / EconomicCalendar command actor
          |
          | committed import domain event with effective duplicate policy
          v
MarketData state repository denormalization
          |
          +--> MarketDataDbContext.yield_curve_rates
          |
          +--> MarketDataDbContext.economic_calendar
```

The FMP network call is never performed on an actor mailbox thread. The coordinator completes acquisition and bounded
mapping before submitting a command. The command actor receives an in-memory bounded payload compatible with the
current command contract.

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

Application and storage layers own:

- import orchestration;
- command creation and event sourcing;
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

The target MarketData keyspace contains exactly one economic-calendar table:

```cql
CREATE TABLE economic_calendar (
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
    PRIMARY KEY ((countryCode, monthBucket), eventDate, eventName)
) WITH CLUSTERING ORDER BY (eventDate DESC, eventName ASC);
```

`monthBucket` is the UTC `yyyyMM` integer derived from `EventDate`. It is stored because it is part of the physical
partition key, but it is not a separate domain identity field. The logical identity remains
`(EventDateUtc, CountryCode, EventName)`.

This layout promotes the useful shape of the current country/month projection to the only canonical table. The old
event-date-partitioned `ReferenceDb.economic_calendar` table and `economic_calendar_by_country_month_v2` are both
removed after verified migration.

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

#### 9.4.1 Implemented bounded compatibility phase

Until the public actor/API contract carries an explicit continuation token, the no-argument compatibility query reads
the latest 120 entries in `economic_calendar_month_v1`, queries `economic_calendar_by_month_v1` in groups of four, and
stops at 10,000 merged rows. This is intentionally a bounded recent-data view even though the legacy method retains
`All` in its name. Country/range queries have the same global result cap and reject more than 120 intersecting months.
The future paged contract replaces this compatibility method; it does not relax these storage bounds.

The country, calendar-month, and yield-curve-year catalogs are monotonic observed-value indexes. Deleting the last row
for a catalog value does not remove its entry; a later bounded read may therefore visit an empty partition or offer a
year with no remaining rows. This trades a harmless empty lookup for race-free imports and avoids distributed
reference counting. The offline rebuild removes stale catalog entries when an exact catalog refresh is required.

### 9.5 Storage migration and cutover

Migration is a controlled operation:

1. deploy the new MarketData schema and code with imports paused;
2. read the old Reference canonical table as the migration source;
3. normalize timestamps to UTC, calculate `monthBucket`, validate logical keys, and write the new MarketData table;
4. compare source/target logical-key counts and deterministic row digests;
5. rebuild and replay affected actor state using the MarketData type aliases;
6. switch command/query actors and FMP imports to `MarketDataDbContext`;
7. run key-complete query, import, overwrite, Reject, and recovery tests;
8. observe a defined verification window; and
9. remove the old Reference tables, projection reconciliation code, interfaces, CQL, registrations, and contracts.

The migration never attempts to merge both old tables as independent sources of truth. The old canonical table is the
source; projection discrepancies are reported and resolved before cutover.

## 10. API-backed import operation

### 10.1 Current command compatibility

The first implementation preserves the serialized fields and API parameters of both existing imports. The coordinator
uses FMP results to populate the existing arrays:

```text
FMP treasury rows
  -> YieldCurveRateReadModel[]
  -> ImportYieldCurveRatesParameter
  -> ImportYieldCurveRatesCommand

FMP economic rows
  -> EconomicCalendarReadModel[]
  -> ImportEconomicCalendarsParameter
  -> ImportEconomicCalendarsCommand
```

Existing callers that already supply valid arrays remain supported. They pass through the same validation, duplicate
policy, event, and direct table-write path. A later command version may carry only a date range and source identity, but
that is not required for initial FMP support.

### 10.2 Single-date workflow

1. The caller requests acquisition for one date.
2. The coordinator calls the FMP client asynchronously with cancellation.
3. The client validates HTTP status, content type, response bounds, JSON, and required fields.
4. Provider rows are normalized into canonical read models.
5. The coordinator validates logical keys, values, and the complete bounded batch.
6. The coordinator builds the existing import command parameter and command.
7. The command actor resolves the configured duplicate policy for the dataset.
8. Domain state validates or merges the import according to that policy.
9. The committed import event records the effective duplicate policy and canonical payload.
10. The MarketData state repository writes the batch directly to the single canonical table.
11. Completion or failure follows the existing event-sourced denormalization contract.

An empty valid FMP response is represented as a no-data outcome and does not create a successful empty import event.

### 10.3 Date-range workflow

The initial coordinator splits a requested range into deterministic single-date imports. This is deliberately allowed
even when the provider call covers a larger window:

- treasury rows are grouped by `ValueDate`;
- economic rows are grouped by UTC event date;
- each group produces the current command parameter and one command;
- commands use stable ordering; and
- cancellation stops submission of new commands without cancelling a command already durably accepted.

This approach bounds actor payload size, avoids an ever-growing economic-calendar snapshot for one synthetic import
entity, gives a precise retry boundary, and limits partial range failure. A later optimized implementation may use a
bounded range command only after its state, serialization, replay, and partial-failure semantics are designed.

### 10.4 Direct storage writes

The import event is denormalized as a batch:

- yield curves call one bounded `InsertYieldCurveRatesAsync` operation that maintains `yield_curve_rates`,
  `yield_curve_rate_by_date_v1`, and the de-duplicated year lookup; and
- economic calendars call one bounded `MarketDataDbContext.InsertEconomicCalendarsAsync` operation that maintains the
  canonical row, country/month and month query projections, and country/month lookup catalogs.

The importer does not emit one `Add` or `Change` command per row. Native storage batching may be used only within the
driver's safe size and partition constraints.

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
records it on the import event, and the storage projector applies that recorded value. A later command contract may
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

The economic-calendar command state must merge imported rows by logical key instead of throwing unconditionally. The
yield-curve command state continues to maintain existence by `ValueDate`. Both imported events contain the final
canonical batch and effective policy.

### 11.4 Reject behavior

Reject mode has two protections:

1. a bounded preflight checks duplicate keys in the normalized batch and current state/storage; and
2. conditional `INSERT ... IF NOT EXISTS` protects against a concurrent writer after preflight.

A check-then-unconditional-insert implementation is not sufficient. A duplicate produces a typed
`ExternalDataDuplicateException` (final name to follow repository conventions) containing the dataset, operation ID,
and a bounded list/count of logical keys. It contains no API key, URL query, or complete provider payload.

For economic calendars, the single `economic_calendar` row is the conditional ownership point. No second projection
write or projection-repair workflow exists after migration.

### 11.5 Retry and partial-failure constraint

ScyllaDB does not make arbitrary multi-partition date-range imports globally atomic. A process failure can occur after
some rows have been applied. Therefore:

- a committed command/event identity is the idempotency identity;
- completion state distinguishes replay of the same command from a new import;
- overwrite retries are naturally idempotent for the same normalized values;
- reject retries must recognize rows already written by the same command rather than classify them as foreign
  duplicates; and
- initial range orchestration uses single-date commands to bound any partial result.

Reject mode cannot be enabled in production until same-command retry behavior is proven with an import receipt or
equivalent durable operation identity. It must not depend only on an in-memory preflight.

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

Range import results report bounded counts for requested dates, acquired rows, accepted commands, inserted rows,
overwritten rows, rejected rows, no-data dates, failed dates, and remaining unsubmitted dates. Large row-level details
belong in logs or an operation record rather than actor replies.

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

Retries occur only during acquisition. A storage retry is controlled by the event denormalization/idempotency path, not
the HTTP policy.

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
4. Migrate and verify Reference data, then remove both old Reference tables and projection code.
5. Replace unbounded APIs with key-complete bounded fan-out queries.

### Stage 3: provider client

1. Implement the typed HTTP client and resilience policies.
2. Replace generic external URI reads behind the two compatibility contexts.
3. Add provider unit, contract, and opt-in live tests.

### Stage 4: current-command import

1. Add the API-backed coordinator that constructs current import parameters.
2. Split date ranges into deterministic single-date commands.
3. Record effective duplicate policy on import events.
4. Make economic-calendar state merge or reject according to that policy.
5. Correct yield-curve schema/runtime CQL mismatch.

### Stage 5: storage policies

1. Implement direct overwrite bulk writes.
2. Implement conditional Reject writes and same-command retry identity.
3. Verify partition-local economic-calendar writes and key-complete reads.
4. Add integration, concurrency, and failure-injection tests.

### Stage 6: operational integration

1. Register the provider and import coordinator in the host.
2. Add metrics, alerts, health, and bounded logging.
3. Integrate authorized UI/API/ScheduledTask invocation using the current commands.
4. Run affected domain and storage integration suites.

## 18. Acceptance criteria

The design is implemented only when:

1. The FMP project has no storage or host dependency.
2. API keys never appear in tracked files, URLs, logs, traces, or exceptions.
3. Both stable endpoints use bounded dates and cancellation.
4. Provider DTOs are separate from canonical read models.
5. Treasury fields map completely without treating missing values as zero.
6. Calendar timestamps are normalized to UTC and missing values remain distinguishable from zero.
7. The current import command parameter contracts remain accepted.
8. An API-backed range can be represented by multiple current single-date commands.
9. No actor thread performs an FMP HTTP request.
10. Import commands remain event sourced before table writes.
11. Imports write the target tables directly in bounded batches rather than emitting per-row commands.
12. Overwrite and Reject are independently configurable per dataset.
13. The effective policy is persisted on the import event.
14. Overwrite is the initial default and updates the single target row.
15. Reject throws a typed exception and uses a conditional storage write.
16. Same-command retry is idempotent under both policies.
17. Conflicting provider duplicates fail in both policies.
18. Range partial-failure semantics are visible and retryable by date.
19. Yield-curve schema and runtime CQL use the same primary key.
20. Economic-calendar contracts and actors reside in the MarketData domain and shared project.
21. `MarketDataDbContext` owns the only `economic_calendar` table.
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

## 19. Decisions requested during review

| Decision | Proposed direction |
| --- | --- |
| Initial command contract | Preserve current `ImportYieldCurveRatesParameter` and `ImportEconomicCalendarsParameter` |
| API-backed range | Fetch a bounded range, then issue deterministic single-date current commands |
| Direct write meaning | Bulk denormalization to the target tables after the committed import event; no per-row actor commands |
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
| Reject concurrency | Preflight plus conditional `IF NOT EXISTS`; never check then unconditionally insert |
| Range atomicity | No global transaction claim; single-date commands bound partial failure |
| Economic calendar revisions | Overwrite the matching row in the single MarketData table |
| Calendar supplemental values | Add optional `Impact`, `Unit`, `Change`, and `ChangePercentage` canonical/storage fields before claiming full FMP fidelity |
| Time handling | Normalize provider timestamps to UTC; do not convert identity to workstation local time |
| Missing values | Preserve missing/empty distinction; do not synthesize `"0"` |
| Yield key | Reconcile schema/runtime CQL on `valueDate` before enabling imports |
| Credential gate | Rotate exposed keys and remove plaintext secrets before live use |
| Framework contracts | Define provider-neutral `ITreasuryCurve` and `IEconomicCalendar` in `Framework.MarketData.Contracts` |
| Provider ownership | Implement both contracts in Financial Modeling Prep; application orchestration consumes Treasury data and passes the selected rate to DataBento |

## 20. References

- [FMP stable Treasury Rates API](https://site.financialmodelingprep.com/developer/docs/stable/treasury-rates)
- [FMP stable Economic Data Releases Calendar API](https://site.financialmodelingprep.com/developer/docs/stable/economics-calendar)
- [FMP API quickstart and authentication](https://site.financialmodelingprep.com/developer/docs/quickstart)
- [FMP published dataset cycle times](https://site.financialmodelingprep.com/developer/docs/cycle-times)
- [FMP changelog](https://site.financialmodelingprep.com/developer/docs/changelog)

## 21. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.7 | 2026-08-15 | Implemented the FMP adapters, compatibility facades, deterministic application import coordinator, independently configured event-persisted duplicate policies, LWT Reject ownership, supplemental calendar persistence, host/API/schedule/health/metrics wiring, secret cleanup, tests, and rollout migration instructions. |
| 0.6 | 2026-08-10 | Defined the provider-neutral framework contracts and storage-capable records in code; made Treasury percentage units explicit, preserved UTC/provenance and nullable calendar values including impact/unit/change fields, and specified FMP-to-framework DI registration without any dependency on the application API. |
| 0.5 | 2026-08-10 | Removed the high-frequency Blackboard L1/L2 requirement: Treasury curves may use ordinary application cache-aside, while DataBento owns its in-process quote/trade hot values and continues to receive only the selected scalar rate. |
| 0.4 | 2026-08-10 | Clarified that application orchestration owns Treasury L1/L2 caching and tenor selection and passes the selected scalar rate into DataBento, which performs no FMP or Blackboard calls. |
| 0.3 | 2026-08-10 | Made `ITreasuryCurve` and `IEconomicCalendar` binding provider-neutral framework contracts, assigned both implementations to the FMP adapter, and documented application Blackboard caching plus DataBento's consumer-only hot-path boundary. |
| 0.2 | 2026-08-10 | Moved target economic-calendar ownership from Reference to MarketData, replaced the canonical-plus-projection layout with one country/month-partitioned MarketData table, prohibited `ALLOW FILTERING` and runtime full-table scans, redesigned bounded queries, and added the domain/storage data migration path. |
| 0.1 | 2026-08-10 | Created the FMP architecture and defined API-backed imports using current command parameters, direct event-denormalized table writes, configurable Overwrite/Reject duplicate behavior, single-date range partitioning, mapping, resilience, secrets, observability, testing, and review decisions. |
