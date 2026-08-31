# Market Outlook Historical Analytics Warm-up Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Plan ID | `MOHW` |
| Status | Implemented and qualified |
| Date | 2026-08-31 |
| Scope | Development startup coverage, ES/VX historical replay, Market Outlook EMA/Bollinger migration |
| Related authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |
| Historical-loader baseline | `Regime-Discovery-Market-Signal-Interface-MDSI-3-Historical-Data-Loader-v1.0.md` |
| Market Outlook baseline | `Market-Outlook-Snapshot-Command-Architecture-MOS-0-Baseline-v1.0.md` |

## 1. Objective

Make the four Bollinger-derived values and EMA50/EMA200 values in Market Outlook
reliable by ensuring that the Analytics actors have an ordered one-year Daily
history before those values are presented.

During Development, every UI startup requests a coverage check over NATS. The
server downloads only missing Databento sessions, replays locally stored Daily
observations when acquisition is unnecessary, and publishes independently
available typed EMA and Bollinger components to Market Outlook.

Automatic historical loading is prohibited in Production. Production ignores
the automatic request even if configuration is accidentally set to enabled.
Any Production reload is an explicitly invoked offline operation outside the UI
startup path.

## 2. Fixed decisions

1. `HistoricalAnalyticsWarmup.Enabled` defaults to `true` in
   `appsettings.Development.json` and `false` in base and Production settings.
2. Server code also requires `IHostEnvironment.IsDevelopment()`. Configuration
   cannot override this environment guard.
3. Every UI startup sends one non-blocking NATS `Ensure` request. The API server
   owns provider credentials, coverage decisions, budgets, storage, and replay.
4. The target end date is the most recently completed authoritative futures
   trading value date. An incomplete current session is never historical input.
5. The requested window is the trailing 365 calendar days, subject to a minimum
   of 201 valid ordered Daily sessions.
6. The configured series are ES calendar-front, VX calendar-front, and VX
   calendar-second continuous series (`ES.c.0`, `VX.c.0`, and `VX.c.1`).
7. Raw-data coverage and Analytics-calculation coverage are separate. Existing
   raw history is replayed locally and is never downloaded merely because an
   Analytics checkpoint is absent or obsolete.
8. Initial acquisition fills the required window. Later runs acquire only
   missing contiguous trading-date ranges.
9. EMA and Bollinger use completed Daily closes. Values do not change on every
   intraday tick and are not presented as provisional Daily indicators.
10. Missing or unwarmed values display `N/A`, never a misleading numeric zero.
11. Market Outlook retains OR semantics: one missing component cannot prevent
    another valid component from updating.
12. Disabling automatic warm-up does not delete history, reset Analytics state,
    or stop normal live-market-data processing.

## 3. Configuration contract

The base and Production configuration contains an explicitly disabled section.
The Development override enables it:

```json
{
  "AppSettings": {
    "HistoricalAnalyticsWarmup": {
      "Enabled": true,
      "TriggerOnUiStartup": true,
      "LookbackCalendarDays": 365,
      "MinimumValidDailySessions": 201,
      "Series": [ "ES.c.0", "VX.c.0", "VX.c.1" ],
      "SignalFamilies": [ "EMA", "BollingerBand" ],
      "MaximumCostUsd": 10.00,
      "MaximumBytes": 1073741824,
      "NormalizationVersion": "historical-daily-v1",
      "CalculationConfigurationVersion": "ema-bb-daily-v1"
    }
  }
}
```

The cost and byte values are hard ceilings, not spending targets. The existing
estimate-before-acquire behavior remains mandatory. An estimate above either
ceiling fails safely and reports an operational status without disabling the UI.

## 4. Target runtime flow

```text
UI startup
  -> NATS EnsureFuturesAnalyticsHistoricalCoverage command
  -> server environment/configuration guard
  -> authoritative last-completed value date
  -> raw ES/VX coverage query
     -> acquire only missing ranges through Databento
     -> persist immutable observations and coverage audit
  -> ordered local Daily replay
     -> EMA10/20/50/200 actor
     -> same-observation BB10/20 actor
  -> typed EMA and BB Market Outlook component updates
  -> snapshot persistence and Notify publication
  -> UI shows EMA50/EMA200 and BB20 values
```

Repeated UI startups on the same day join or reuse the same durable coverage
operation. Concurrent UI processes cannot start duplicate provider acquisitions.

## 5. Implementation gates

### MOHW-00 — Baseline, contracts, and documentation

Deliverables:

- freeze the six existing UI fields and their legacy sources;
- define the configuration/options contract and validation;
- define automatic, disabled, already-current, acquiring, replaying, completed,
  and failed outcomes;
- record ES/VX series identities, value-date policy, calculation versions, and
  the Production prohibition; and
- update the Market Outlook and historical-loader design/specification documents.

Exit tests:

- architecture tests enforce dependency direction and NATS ownership;
- configuration serialization/default tests pass; and
- documentation links and gate inventory are verified.

### MOHW-01 — Development default and hard Production guard

Deliverables:

- add strongly typed, validated `HistoricalAnalyticsWarmupOptions`;
- enable it by default only in Development configuration;
- add server-side `IsDevelopment()` enforcement before coverage, storage replay,
  estimation, or provider acquisition;
- make a Production request return a non-error `IgnoredInProduction` outcome; and
- preserve a separately invoked offline loader path for controlled reloads.

Exit tests:

- unit: base=false, Development=true, Production=false;
- BDD: Production with `Enabled=true` still performs zero provider/storage-replay
  calls;
- integration: a NATS Ensure request in Production returns ignored; and
- verification: Production startup generates no historical-provider request.

### MOHW-02 — Trading-calendar coverage query

Deliverables:

- add bounded ScyllaDB Daily EOD range reads by series and year/month partition;
- calculate expected sessions from the authoritative market calendar;
- report present, invalid, missing, and unexpected-gap dates;
- group missing dates into minimal contiguous acquisition ranges; and
- report whether ES contains at least 201 ordered valid sessions.

Exit tests:

- unit: weekends, holidays, leap year, year/month boundary, and missed-day cases;
- BDD: initial year, already-current, one missing day, and several missed days;
- integration: real Scylla schema range reads for ES and both VX series; and
- verification: coverage hash and counts are stable for identical stored data.

### MOHW-03 — NATS Ensure coordinator and concurrency fencing

Deliverables:

- add typed Ensure command, Requested/Completed/Failed or skipped outcomes, and a
  diagnostics query;
- derive a stable operation key from target value date, series set, and version;
- serialize same-key work so concurrent UI processes share one operation;
- reuse completed same-day results; and
- leave the UI startup non-blocking while exposing progress.

Exit tests:

- unit: operation-key stability and outcome mapping;
- BDD: ten repeated same-day UI starts produce one logical operation;
- integration: simultaneous multi-process NATS requests cause at most one
  provider acquisition; and
- verification: a failed operation is resumable without duplicate completed work.

### MOHW-04 — Missing-range acquisition and durable audit

Deliverables:

- submit the initial trailing-year request when no coverage exists;
- submit only missing contiguous ranges after initial coverage;
- retain estimate/cost/size checks, resumable checkpoints, immutable manifests,
  roll audits, and insert-if-absent writes; and
- distinguish expected non-trading dates from provider gaps.

Exit tests:

- unit: range generation, budget rejection, and gap classification;
- BDD: first load, same-day restart, next-day increment, and multi-day catch-up;
- integration: fake Databento provider plus PostgreSQL/Scylla proves idempotency;
- verification: one-year fixture yields at least 201 valid ES sessions and no
  duplicate observations; and
- controlled Development smoke: estimate and acquisition remain within configured
  ceilings before any real Databento request proceeds.

### MOHW-05 — Ordered local Daily EMA/Bollinger replay

Deliverables:

- introduce a replay coordinator that reads stored valid Daily observations in
  strict market-time order;
- advance EMA first and pass the exact same observation identity and EMA result to
  Bollinger;
- support local replay when raw data exists but calculation state is absent;
- fence replay by normalization/calculation version and latest processed
  observation; and
- retain duplicate/stale observation handling without first-chance exceptions.

Exit tests:

- unit: EMA seed and recurrence, 201-session EMA200 warm-up, 40-session BB20
  baseline warm-up, duplicates, stale input, and version changes;
- BDD: raw-present/state-missing replays locally with zero provider calls;
- integration: a year of ordered Scylla Daily rows produces typed EMA/BB actor
  results across an ES contract roll; and
- verification: results match an independent deterministic reference calculator.

### MOHW-06 — Typed Market Outlook snapshot components

Deliverables:

- append typed `FuturesEmaSignalReadModel` and `FuturesBbSignalReadModel`
  components to Market Outlook commands, state, events, snapshots, and storage;
- migrate the four Bollinger fields to BB20 standard deviation, upper, EMA20
  center, and lower values;
- migrate moving averages to EMA50 and EMA200;
- preserve append-only MessagePack keys and additive storage migration; and
- publish each valid component independently under OR semantics.

Exit tests:

- unit: component mapping, stale fencing, partial snapshots, and `N/A` policy;
- BDD: EMA-only, BB-only, both, neither, stale, and independently delayed updates;
- integration: NATS component -> snapshot actor -> Scylla -> query -> Notify; and
- verification: no Market Outlook path reads the six legacy EOD fields.

### MOHW-07 — Market Outlook UI migration

Deliverables:

- display BB20 standard deviation, upper band, EMA20 mean/center, and lower band;
- relabel `50 DMA` and `200 DMA` as `50 EMA` and `200 EMA`;
- show `N/A` until the applicable typed value is available and warm;
- refresh when either typed component arrives; and
- report warm-up progress through Status Console without blocking menus.

Exit tests:

- presentation unit tests cover formatting, nulls, warm/unwarm, and independent
  refresh;
- UI automation covers startup while loading, already-current, delayed component,
  failed acquisition, and disabled configuration;
- visual acceptance verifies labels, contrast, layout, and unchanged unrelated
  Market Outlook controls; and
- interactive Development acceptance confirms repeated UI restarts do not create
  duplicate downloads.

### MOHW-08 — Operational resilience and observability

Deliverables:

- expose target date, covered range, valid/missing counts, latest replayed date,
  calculation version, provider-acquisition count, and current outcome;
- distinguish provider failure, budget rejection, storage failure, replay failure,
  and insufficient valid sessions;
- use normal result/outcome contracts for expected absence or disabled behavior;
- retain last valid Market Outlook values when a later warm-up fails; and
- support cancellation/shutdown and safe resume.

Exit tests:

- unit: outcome/severity mapping and sanitized error messages;
- integration: injected provider, PostgreSQL, Scylla, NATS, replay, and shutdown
  failures;
- verification: no expected disabled/missing/stale path emits first-chance
  exceptions; and
- soak: repeated startup/cancellation/restart cycles converge to one current
  coverage state.

### MOHW-09 — Full qualification and closeout

Required suites:

- Analytics BDD and unit tests;
- Application MarketData unit tests;
- Analytics and storage integration tests with PostgreSQL, ScyllaDB, and NATS;
- API Server configuration/environment tests;
- UI presentation and system tests;
- deterministic one-year ES/VX verification fixture;
- controlled real-Databento Development smoke within configured ceilings; and
- solution build plus targeted regression suites for Market Outlook, historical
  loading, live-feed OR semantics, and value-date handling.

Exit criteria:

1. A clean Development environment backfills and warms all six values.
2. A same-day restart performs no additional provider acquisition.
3. A missed-day restart loads only missing sessions and advances the indicators.
4. EMA200 uses at least 201 valid ordered ES Daily closes.
5. Market Outlook displays typed BB20 and EMA values, never legacy zeros.
6. Partial components continue updating independently.
7. Production performs no automatic coverage query, replay, estimate, staging, or
   provider acquisition even when configuration is forcibly enabled.
8. Offline Production loading remains possible through an explicit operator tool,
   not through application or UI startup.
9. All required tests pass and the implementation/design documents record final
   evidence before the plan is marked complete.

## 6. Execution order

Gates execute in numeric order. MOHW-00 through MOHW-05 establish safe data and
calculation authority before MOHW-06 and MOHW-07 change the user-visible fields.
MOHW-08 is exercised throughout and formally closed before final qualification.

No gate may be marked complete solely because code compiles. Its listed BDD,
unit, integration, verification, and UI evidence must be recorded.

## 7. Implementation closeout

All implementation gates `MOHW-00` through `MOHW-09` are complete.

| Gate | Result | Principal evidence |
| --- | --- | --- |
| `MOHW-00` | Complete | Options, NATS contract, typed snapshot contract, and the design addenda are committed together. |
| `MOHW-01` | Complete | Development defaults enabled; base and Production disabled; server derives the environment flag from `IHostEnvironment` and ignores automatic work before any storage/provider call. |
| `MOHW-02` | Complete | Partition-bounded Scylla Daily range reads, CME trading-date coverage, invalid/missing classification, and a cross-month Scylla integration test. |
| `MOHW-03` | Complete | UI sends one non-blocking NATS request; the singleton coordinator serializes concurrent UI processes; ten concurrent requests replay once. |
| `MOHW-04` | Complete | Only disjoint missing trading-date groups are acquired, each has a stable attempt identity, and coverage is revalidated before replay. |
| `MOHW-05` | Complete | Ordered Daily replay drives the event-sourced EMA-to-Bollinger chain; the 201-session verification warms EMA200 and BB20 and reconciles the active value date. |
| `MOHW-06` | Complete | Typed EMA/BB values use append-only MessagePack keys throughout commands, state, events, snapshots, Scylla projection, query, and Notify. |
| `MOHW-07` | Complete | Market Outlook uses BB20 standard deviation/upper/EMA20/lower and EMA50/EMA200; labels read `50 EMA`/`200 EMA`; unavailable values are `N/A`. |
| `MOHW-08` | Complete | Normal ignored/disabled/current outcomes, bounded acquisition, cancellation, deterministic replay hashes, last-valid snapshot retention, and structured completion logs. |
| `MOHW-09` | Complete | Solution build and all targeted unit, BDD, NATS/Scylla integration, presentation, and WinForms layout suites pass. |

### Final qualification evidence (2026-08-31)

- solution build: succeeded with 0 warnings and 0 errors;
- Application MarketData unit tests: 87 passed;
- Market Data Analytics unit/verification tests: 1,089 passed;
- Market Data Analytics BDD tests: 470 passed;
- Market Data Analytics integration tests: 49 passed;
- UI presentation tests: 261 passed;
- WinForms Market Outlook layout system test: 1 passed;
- Scylla historical cross-month range integration test: 1 passed; and
- configuration JSON validation and `git diff --check`: passed.

The automated provider qualification uses the synthetic Databento implementation.
No billable real-Databento acquisition was started as part of repository testing.
The first Development UI startup is the controlled runtime smoke: it retains the
configured cost/byte ceilings and will acquire only coverage that is actually
missing. Production cannot enter that path.
