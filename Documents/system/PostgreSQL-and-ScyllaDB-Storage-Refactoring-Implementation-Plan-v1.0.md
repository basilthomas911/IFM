# PostgreSQL and ScyllaDB storage refactoring implementation plan

Date: 2026-10-11

Status: Development provider migration, verified source baselines, isolated restore and API/UI/Scheduler startup are validated. Normal solution compilation and the final focused regressions pass. Extended performance qualification is deferred to a weekend by the user; it is not a migration correctness gate. The local backup-host enrollment was repaired and host readiness verified; see final repair entry.

Move application persistence to one PostgreSQL database with domain schemas. Keep ScyllaDB only for Databento trade and quote ingestion in a new TradeTickQuoteDb. Required business projections and their source events must commit together. Trade plans become PostgreSQL snapshots with the same atomic boundary; realtime observations continue to write directly to their tables.

This plan supersedes the ScyllaDB TradePlanDb destination and independent source-event/projection writes in earlier monitoring plans. Existing calculation formulas, identifiers, lifecycle rules and read-only monitoring behavior remain requirements.

## Persistence rules

| Processing | Boundary | Failure behavior |
| --- | --- | --- |
| Business commands/functions | Source events, required PostgreSQL projections and applicable completion records on one connection and transaction | Roll back together; preserve explicit business failure policies |
| Trade-plan snapshots | Source snapshot event, plan history and required activity rows in one PostgreSQL transaction | Log and drop failed updates; calculate a fresh snapshot on the next leg update |
| Realtime PostgreSQL observations | Direct writes; separate transaction for related writes | Explicit bounded observation failure policy; no business event append required |
| Databento trades/quotes | Direct ScyllaDB writes | Preserve explicit ingestion failure reporting |
| External operations/notifications | After commit; transactional outbox when reliable delivery is required | Delivery cannot undo committed business state; no durable trade-plan replay backlog |

Provider calls, option calculations, broker operations and actor/NATS calls run outside database transactions. No trade-plan projection replay, repair loop, rolling plan cache or reconciliation requirement is introduced.

## Destinations

| Context/store | Destination |
| --- | --- |
| EventSourceDb, including command/event/projection infrastructure | PostgreSQL |
| TradeDb, PortfolioDb and TradePlanDb | PostgreSQL |
| ReferenceDb, SecuritiesDb and ConfigurationDb | PostgreSQL |
| SequenceIdDb, SystemAdminDb and ScheduledTaskDb | PostgreSQL |
| OptionPricerDb and MarketDataServiceDb | PostgreSQL |
| MarketDataDb observations, bars, analytics, EOD data, parameters, evidence and composition preparation | PostgreSQL |
| Raw Databento trade/quote tables currently accessed through MarketDataDb | ScyllaDB TradeTickQuoteDb |
| Backup journals, auxiliary stores and scheduler persistence | Include in inventory; PostgreSQL for durable application database state |

Keep domain DbContext names and read/write contracts. Separate domain ownership with schemas inside one physical PostgreSQL database. TradeTickQuoteDb is a ScyllaDB context/keyspace and does not require another cluster.

## Stage 1 Inventory and baseline

- Inventory every context, table, mapper, CQL statement, standalone store, schema initializer and consumer. Record ownership, keys, indexes, serialization, concurrency and retention.
- Classify individual projector descriptors as required business projection, realtime observation, disposable snapshot, external operation or optional derived projection. Regenerate the previous inventory of 57 command projector classes, four function projectors and three realtime projectors.
- Identify all raw Databento trade/quote writers and readers, including futures/options, imports, aggregation and UI queries. Derived bars/signals remain PostgreSQL.
- Trace append, projection, completion, notification and checkpoint ordering.
- Record versions, Docker resources, mounts, row counts, actual peak update rates, representative payload sizes and existing latency.

Deliverables: table destination manifest, transaction-boundary manifest and baseline measurements.

## Stage 2 Shared transaction infrastructure

- Extend EventSourceDb/PostgresEventTransaction and enlisted repository conventions across context writers.
- Provide explicit standalone and transaction-enlisted writer entry points. Enlisted writers never open, commit or dispose a separate transaction.
- Preserve stream-version checks, event identities, command idempotency and sequence semantics. Translate conditional CQL writes into SQL constraints/conditional updates.
- Reuse AtomicBusinessAndEvent completion for functions where applicable; move independent projection calls into enlisted persistence methods.
- Accept actor state and report successful completion only after commit. Discard/reload speculative state after rollback before another request.
- Enlist applicable completion/checkpoint and reliable notification-outbox records. Disposable monitoring notifications remain best effort after commit.

Acceptance: injected failure between append and projection commits neither; successful writes expose both. No transaction waits for an external actor or provider.

## Stage 3 TradeTickQuoteDb

- Add Application.Storage/TradeTickQuoteDb context, read/write contracts, CQL, parameters and schema following repository conventions.
- Add factory, schema, provider, configuration and health registrations.
- Move the inventoried raw Databento trade/quote tables and consumers, including TickAggregationRealtimeProjector and option ingestion.
- Preserve identities, source sequences, price types, timestamps and value dates. Verify six-hour ES/VX queries across value dates and bounded partition fan-out.
- Remove moved tables from MarketDataDb initialization after migration and reader verification.

Acceptance: only raw trades/quotes use this ScyllaDB context; derived market data uses PostgreSQL.

## Stage 4 PostgreSQL contexts and schemas

Progress verified on 2026-10-11: native observation paths now include EOD/TDI, ATR/ADX/MACD, EMA/Bollinger, VWAP, bars, closing-price ranges, VX term structure, calculated option prices and RSI. Reference family/catalog initialization and CAS writes also use native SQL. The isolated suite verifies these operations alongside raw Scylla ownership and injected EOD transaction rollback. Market Outlook, immutable option trade evidence and composition preparation native paths also passed the isolated suite, including identity-collision rejection and reuse of accepted preparation. This is progress within Stage 4, not acceptance of the whole provider migration; remaining MarketData/Securities/Reference operations and cutover configuration must still be completed.


- Implement SQL and versioned schema migrations for every PostgreSQL destination, including existing PostgreSQL stores that must share the physical database.
- Map UUIDs, dates, UTC timestamps, decimals, doubles, nulls, enums and payloads explicitly. Verify precision and decimal/double representation during conversion.
- Translate partition/clustering keys into primary keys and query indexes. Remove redundant lookup copies where contracts permit indexed queries.
- Keep schema creation out of ordinary reads/writes. Preserve published SecuritiesDb versions and pricing bundles; do not restore retired ReferenceDb pricing stores.
- Convert parameter publication, scheduling, backup catalog/log stores, sequence allocation and financial/workflow state.
- Preserve full ascending position history, selected-position value-date plan history, latest snapshot and virtual/paged plan lists without a fixed 200-row cap.

Acceptance: all context contract tests pass; no application database state outside TradeTickQuoteDb depends on ScyllaDB.

## Stage 5 Business persistence boundaries

- Apply required command projections before event commit using the shared transaction instead of post-commit queue submission.
- Convert RegimeDiscoveryFunctionProjector, MarketConditionAssessmentProjector, TradeSelectionProjector and OrderCompositionProjector writes to atomic function persistence.
- Enlist cross-domain trade/position/portfolio updates when they constitute one operation.
- Separate notifications, committed subscription handoffs, broker calls and cache updates from required SQL projection writes.
- Retain asynchronous projection only when explicitly optional. It owns its later projection/checkpoint/outbox transaction and cannot join the source transaction.
- Remove obsolete required-projection queue/replay/repair wiring after replacement verification. Preserve authoritative business event loading and reconstruction.

Acceptance: business success never accompanies a missing required projection; duplicate/concurrent requests preserve invariants.

## Stage 6 Trade plans and realtime observations

- Convert all TradePlanDb tables: Iron Condor, Vertical Spread, Futures, activity and exit workflows. Preserve snapshot payloads and scalar risk fields.
- Replace IronCondorTradePlanFunctionStateRepository source-commit-then-project submission with atomic event/plan/activity persistence; apply equivalent boundaries to other strategy plan writers.
- Calculate before starting the transaction. Where the calculation requires prior state, load only the latest persisted source snapshot, never historical plans for reconstruction.
- On failed writes, roll back, log command/event/trade/position/value date/revision, exception and elapsed time, then drop. No retry, repair backlog or successful persistence notification.
- Publish after commit. Historical UI reads use persisted tables; reload uses the latest persisted source snapshot.
- Convert FuturesEodDataRealtimeProjector and FuturesTdiSignalRealtimeProjector writes to PostgreSQL without adding business event-log writes. Independently document/test the optional EOD spool/batch writer.
- Preserve opening row, one daily MTM row, immutable EOD and filled closing position. Feed stops before EOD; operational value date advances after successful EOD execution.

Acceptance: successful plans have event and required rows; failed plans have neither and do not block later updates.

## Stage 7 Migration and host integration

- Back up current databases; preserve original containers/data and migration manifests until acceptance.
- Build resumable offline migration with counts, keys and payload/value verification. Migration tooling is separate from realtime processing and introduces no runtime dual-write period.
- Stop IFM writers for final migration/cutover; configure one PostgreSQL database and TradeTickQuoteDb; verify schema and application reads/writes.
- Update Api.Server.Core, workers, scheduler/backup hosts, VS Code scripts, Docker definitions, maintenance tools and fixtures.
- Update local/AWS backup selection, catalog, restore/schema validation and recovery health checks. Test PostgreSQL schemas and ScyllaDB TradeTickQuoteDb restores using isolated data.
- Retire old CQL/provider wiring only after all consumers pass. Keep rollback artifacts; do not delete development data to simplify conversion.

Acceptance: start/stop, market open/close, backup/restore and health checks work against the new layout.

## Stage 8 Integration and failure tests

- Test leg update through option calculation, dated MTM update, plan generation, atomic commit, notification and screen display.
- Inject failures/cancellation before commit, SQL projection failure after append, version conflicts and connection loss. Verify atomic visibility and actor state recovery. Distinguish uncertain commit outcomes from proven rollbacks in logs.
- Verify financial invariants and required outbox records; no plan retry/replay backlog.
- Test selected position/value-date history, current DailyPnl, ascending position history and virtual plan-list reads.
- Use FlaUI for monitoring controls and persisted-plan display when desktop automation is available. Destructive fault/restore tests use isolated databases.

Acceptance: database and UI evidence prove a persisted plan; notifications alone do not.

## Stage 9 BenchmarkDotNet and sustained mixed load ? deferred weekend qualification

User decision on 2026-10-12: defer the extended campaign to a weekend. Keep `fsync=on` and `synchronous_commit=on` as the application defaults. Migration correctness is accepted through schema/data comparisons, actual provider reads, startup, transaction/rollback tests and isolated restore verification. The extended campaign evaluates capacity and sustained latency separately; no automatic weekend task has been installed.

Use Application.Storage.Benchmarks for BenchmarkDotNet transaction measurements and a separate sustained runner for overlapping workloads.

| Workload | Measurements |
| --- | --- |
| Plan only | Atomic event + plan + activity writes with representative payloads |
| Business only | Representative isolated trade/position/portfolio transactions |
| Mixed | Continuous plans alongside the same business transactions |
| Mixed with reads | Latest-plan and paged value-date history queries during writes |
| Failure/recovery | Rollbacks, drops, subsequent successful writes and business latency |

- Fix Docker CPU/memory/storage, record exact image/driver versions, indexes and configuration, and keep fsync/synchronous commit enabled. Use isolated databases.
- Additional requested durability experiment: compare fsync/synchronous_commit on/on, on/off, off/on, and off/off in disposable instances. These are measurement profiles, not application configuration changes. Also compare a durable source commit followed by an async projection commit in the same fsync-enabled instance, and a durable source instance with an fsync-disabled projection instance. Record the extra resources and loss of event/projection atomicity in the separate-instance case.
- For the split benchmark, keep ledger business transactions durable. Rebuildability must be demonstrated per projection before adopting asynchronous projection persistence: commit its pending work with the source event, use idempotent projection writes, and recover checkpoints. Direct realtime tables without source events are not automatically rebuildable from the event log. Iron Condor plan failures retain the approved drop/no-replay behavior.
- Rollback probes verify transaction boundaries, not power-loss safety. Never label fsync-disabled writes crash-safe; acknowledged asynchronous commits may be lost even with fsync enabled. Keep the atomic durable path as the refactor default pending an explicit architecture decision based on measured results.
- Reproducible comparison: scripts/Benchmarks/Compare-PostgresDurability.ps1; aggregation: scripts/Benchmarks/Summarize-PostgresDurability.py. A 120-second load comparison is preliminary and does not satisfy the 30-minute/repeated-case/60-minute-soak acceptance gates below.
- Capture a preconversion baseline, including the current split event/Scylla plan path where practical. Identify its different consistency guarantee.
- Vary concurrency at 1, 4, 16 and 32; offered plan rate at measured normal, peak and twice peak. Use distinct streams to distinguish version conflicts from database capacity.
- Warm up, measure each sustained case for at least 30 minutes, repeat representative baseline/mixed cases three times, and run a 60-minute soak of the final configuration.
- Report throughput, p50/p95/p99, mixed versus business-only latency, offered/completed rates, errors/drops, queue depth, allocations/GC, CPU, disk latency, WAL, checkpoints, connection-pool and lock waits.
- Report saturation and queue growth explicitly. Initial provisional engineering criterion: measured peak has no sustained queue growth, no unexpected business errors/atomicity violations and mixed business p99 within 20% of business-only baseline. Report absolute latency; this is not a production trading SLA.
- Tune indexes, pool limits, transaction size and concurrency based on measured bottlenecks; rerun affected cases.

Deliverables: BenchmarkDotNet reports, sustained-run raw data, environment manifest and capacity/limitations summary.

## Completion checklist

- [x] Complete table and transaction-boundary manifests.
- [x] All application domain schemas reside in one PostgreSQL database except raw Databento trades/quotes.
- [x] TradeTickQuoteDb is the only application ScyllaDB destination.
- [x] Required source events and business projections commit atomically.
- [x] Atomic trade-plan writes log/drop failures without replay backlog.
- [x] Direct realtime writes have explicit transaction ownership.
- [x] Migration, rollback artifacts and isolated database backup/restore verification complete. Separate backup-host operation is blocked by missing local vault enrollment; see runtime handoff below.
- [x] Normal solution build and recorded contract/integration/lifecycle/UI checks pass; strict analyzers are deferred.
- [ ] Extended BenchmarkDotNet and sustained mixed-load reports: deferred weekend capacity qualification, excluded from migration correctness completion.
- [x] Storage inventory, transaction boundaries and this implementation plan record the promoted Development behavior and outstanding operational items.

## Blockers

Concrete blockers include inaccessible databases/Docker, unreadable source data without a recoverable representation, unavailable credentials for required live backup verification, or unavailable desktop automation. Report the affected step and continue independent work. Transaction refactoring, migration tooling and performance tuning are implementation work rather than blockers.

## Current implementation references

- TomasAI.IFM.Application.Storage/DbContextFactory.cs
- TomasAI.IFM.Application.Storage/EventSourceDb/PostgresEventTransaction.cs
- TomasAI.IFM.Shared/EventModelActor/BaseEventSourceFunctionActor.cs
- TomasAI.IFM.Shared/EventModelActor/FunctionExecutionPolicy.cs
- TomasAI.IFM.Application.EventProjector/Realtime/BaseRealtimeProjector.cs
- TomasAI.IFM.Domain.Trade/Futures/Option/Position/IronCondor/Plan/Function/State/IronCondorTradePlanFunctionStateRepository.cs
- TomasAI.IFM.Application.Storage/TradePlanDb/Schema/TradePlanSchemaCql.cs

## Event-log-authoritative projections amendment — 2026-10-11

All business read projections must be reconstructable from complete accepted source-event facts. Atomic correlation of a receipt event with financial table writes does not satisfy this requirement when the event omits the actual posting lines and state changes. Register materialization-only actions separately from broker/workflow/notification effects. Existing incomplete source history requires explicit baseline/import facts rather than fabricated upcasting.

Implementation and per-projector inventory: [Event-source read-projection status](Event-Source-Read-Projection-Refactor-Status-2026-10-11.md). Ledger/configuration/capacity/emulator/authority source facts and rebuild services are implemented, with explicit host registration and trade materialization actions. The user resolved the automatic approval blocks; final operational-record verification and the six-profile benchmark comparison are in progress. Existing receipt-only history still requires a verified baseline decision. This amendment does not mark the full storage migration or all-projector reconstruction complete.


## Approved Development financial baseline (2026-10-11)

The owner selected a verified baseline event for older receipt-only financial history. `FinancialProjectionBaseline` captures the complete `portfolio_financial` business row facts into a MessagePack `FinancialProjectionBaselineEvent`, with schema version, source cut, ordered table columns and SHA-256 row-image hashes. Writers must be stopped explicitly; table locks protect verification and capture. Every journal must have at least two balanced entries, and every materialized account total must reconcile to journal entries. A mismatch refuses capture rather than inventing facts.

Reconstruction requires an empty financial target, the original source log identities already copied, and the same schema. It inserts the recorded facts with immutable journal guards enabled, verifies totals, and records the applied baseline checkpoint in the same transaction. PostgreSQL physical transaction IDs are regenerated. No broker, scheduler or export delivery is dispatched. Subsequent reconstruction reads only complete source events after the baseline event; earlier receipt-only events are skipped. Baselines cover the financial schema; they do not substitute for the separate Portfolio/Fund projection reconstruction or create monitoring-plan replay.

The source-only reader loads the latest baseline without depending on projection receipts. `RebuildFromBaselineAsync` applies it once and rejects any supplied pre-baseline events. The baseline API is registered in host DI. Live Development capture/cutover has not been performed; isolated round-trip verification is in progress. The shared test database intentionally contains balance-only capacity fixtures and was correctly refused by reconciliation; the successful round-trip must use a clean source whose balances come from journals.

Financial source reconstruction tests before this baseline addition: **42 passed**, including configuration, ledger, capacity, emulator, authority, export and broker accounting intent. Evidence: `.artifacts/approved-financial-tests-final.log`. Six-profile benchmarks remain in progress; development builds overlapped part of the asynchronous mixed-load interval, so that interval is exploratory and must not support an uncontaminated performance comparison.


### Live Development baseline isolated reconstruction evidence

The stored baseline **d863c143-fb86-4254-825c-97c34daf4a78** was reconstructed into a newly created, isolated PostgreSQL database. All **171 business rows across 27 financial tables** matched the source row images exactly; journal and account totals reconciled, and repeated application made no duplicate changes. The original financial rows were unchanged. The exact temporary database was removed in the verifier's `finally` block. Evidence: `.artifacts/development-financial-baseline-isolated-verification-20261011.log`.

The verifier copies only the original event-log identities referenced by financial receipts and the baseline, sufficient for this financial materialization test. Full application cutover still requires the full authoritative event log, related Portfolio/Fund state, accepted-order projections and the remaining schemas. It is not a full application health/cutover qualification.

The live Development Portfolio schema contains five accepted orders/five composition decisions and no close claims. These are outside this 27-table financial-schema baseline and remain part of the continued composition-source reconstruction work. No Development database or existing source event was deleted.

## Six-profile durability measurements (2026-10-11)

Run `85b0e3161382` completed all six profiles, five BenchmarkDotNet cases per profile, and two 120-second sustained modes per profile. PostgreSQL 17.2; Docker limits per instance: four CPUs and 4 GiB. BDN: three warmups, eight measured iterations, one launch. Sustained: four business writers at one operation/second each; mixed mode adds four plan writers at five snapshots/second each. Containers and volumes were disposable and owned by the driver. Existing Development database durability settings were not changed.

Means in milliseconds:

| Profile | Event ms | Projection ms | Plan ms | Ledger ms | Concurrent ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| AtomicDurable | 9.298 | 9.723 | 13.024 | 33.306 | 34.906 |
| AtomicAsyncCommit | 4.656 | 4.876 | 7.867 | 28.267 | 27.268 |
| AtomicNoFsync | 4.690 | 5.018 | 8.337 | 27.566 | 28.543 |
| AtomicNoFsyncAsyncCommit | 4.533 | 5.452 | 7.990 | 31.387 | 33.649 |
| SplitSameInstanceAsyncProjection | 9.286 | 5.816 | 15.257 | 30.999 | 31.365 |
| SplitSeparateUnsafeProjection | 9.379 | 5.889 | 20.887 | 33.719 | 35.473 |

| Profile | Business only completed | Mixed business | Mixed plans | Business p99 ms | Plan p99 ms | Errors |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| AtomicDurable | 480 | 481 | 2400 | 53.41 | 26.61 | 0 |
| AtomicAsyncCommit | 480 | 480 | 2402 | 47.39 | 14.08 | 0 |
| AtomicNoFsync | 480 | 480 | 2400 | 43.77 | 14.50 | 0 |
| AtomicNoFsyncAsyncCommit | 480 | 480 | 2400 | 49.32 | 14.80 | 0 |
| SplitSameInstanceAsyncProjection | 480 | 484 | 2401 | 66.47 | 35.51 | 0 |
| SplitSeparateUnsafeProjection | 480 | 480 | 2403 | 54.48 | 29.69 | 0 |

All twelve load modes reported zero application errors. SQL rollback probes verified transactional rollback in each source database; they do not verify power-loss durability. fsync-off and synchronous-commit-off configurations are experimental comparisons, not financial admission settings.

Atomic asynchronous commit was approximately 39.6% faster for plan writes and 15.1% faster for ledger posting than atomic durable commit in this run. Turning fsync off did not establish an additional consistent benefit. The same-instance and separate-instance split profiles were slower for plan writes than atomic durable commit (15.257 and 20.887 ms versus 13.024 ms), and split commits lose source/projection atomicity.

PostgreSQL durability boundaries remain decisive: `fsync=off` can corrupt a cluster after an OS/hardware failure. `synchronous_commit=off` with fsync enabled preserves recoverable consistency but can lose recent acknowledged commits. One source/projection transaction cannot give its parts different commit durability. See [PostgreSQL 17 WAL configuration](https://www.postgresql.org/docs/17/runtime-config-wal.html).

Limits: these are exploratory single-machine measurements. Development builds, tests or baseline verification overlapped some load intervals; isolate and repeat before making sizing claims. The 120-second runs do not satisfy the plan's longer soak/repeat gates or establish actual peak trading capacity. No crash/power-loss test was performed. The unsafe projection instance was a disposable benchmark target, not the live read database.

Raw reports, server settings, source statistics, rollback probes and load JSON: `.artifacts/postgres-durability/85b0e3161382`. Consolidated CSV: `PostgreSQL-durability-comparison-2026-10-11.csv`.


## Latest execution state

The approved financial/composition baseline is complete and verified (331 rows, 34 tables, event 519591). This supersedes the earlier pending-baseline paragraphs. Native Securities pricing/reference and option-definition generation paths, and atomic assessment/selector/composer completion paths, have been added. The latest transaction batch passed 20 integration tests; all 1,493 Trade unit tests passed under a diagnostic analyzer override.

**Build gate blocker:** the current `Directory.Build.props` enables all analyzers as errors; the normal storage dependency build reports 670 analyzer errors across existing projects. Its settings have not been reverted. Diagnostic compilation/tests do not satisfy this normal build gate. The full migration, remaining native context/query conversions, complete projector audit, cutover/backup/health checks and longer benchmark gates remain incomplete. See [current refactor status](Event-Source-Read-Projection-Refactor-Status-2026-10-11.md) for exact evidence and limitations.

### Further native observation and securities execution (2026-10-11)

The isolated market observation test now passes Treasury curve ownership/duplicate rejection, canonical/date/year transaction rollback, option-chain same-tick Greek enrichment guards, rate-of-return latest-date reads, VX session statistics and concurrent volume accumulation, current EOD date-range queries, trade-signal canonical/index rollback, and ITI signal/timeframe-state rollback. ITI PostgreSQL reads use indexed canonical date-range, day/mode and latest trend/mode queries; they do not consult the Scylla projection-completion protocol. Delayed observations cannot replace a newer timeframe state or trade-signal lookup.

Evidence: `.artifacts/native-iti-integration.log` (one aggregate observation test passed, 77 seconds); `.artifacts/native-iti-strict-2.log` (normal Storage build, zero warnings/errors). The filter in this latest test run did not match the raw-routing class, so this result is not a new raw-routing verification. Earlier raw-routing results still apply. Treasury and VX family results also remain recorded separately.

Core Securities contract SQL and typed bindings have been added. Canonical contracts and symbol indexes are grouped transactionally on PostgreSQL, and runtime reads bypass Scylla completion checks. Their integration verification is still in progress. Immutable reference staging/publication and required business source-event enlistment remain separate audit items; this change does not claim full source/projection atomicity for Securities commands.

No Development connection switch or data migration was performed in this batch. All remaining cutover, health, rollback, source reconstruction and benchmark acceptance gates remain open.


### Accepted-source transaction follow-through (2026-10-11)

- Treasury index reconstruction now locks canonical/date/year tables and verifies row identities in one PostgreSQL transaction. An injected year-index failure leaves both existing indexes intact. Trade-signal lookup repair has a native PostgreSQL maintenance transaction; its focused repair verification remains pending.
- Download outcome source events and their rows commit together. Economic-calendar and Treasury imports persist complete accepted provider facts and canonical/index rows together. Acquisition retries read the single accepted completion snapshot instead of calling the provider again. Reconstruction callbacks perform storage materialization only.
- Securities command repositories now enlist source events, canonical contracts, symbol indexes and immutable reference publication. Broker enrichment for older option definitions runs before source event creation, outside the database transaction. Native post-commit projectors retain completion notifications and skip duplicate database writes; audited rebuild callbacks use accepted facts without broker queries.
- Spread-distribution and pricing-job command repositories now enlist source events and their required rows. Distribution rows record the source event ID so latest reads use accepted source order even when deterministic materialization IDs are negative. Calculations and provider work remain outside persistence/reconstruction.
- ADX, ATR, MACD, RSI, TDI, EMA, Bollinger Band, VWAP and VX term-structure command repositories now enlist accepted signal values with the source transaction. Runtime cache publications and terminal notifications remain after commit. The nine-family reconstruction test loads persisted event bytes, reconstructs rows without calculators/providers, and matches full PostgreSQL row images. ITI and trade-signal canonical/index enlistment is the next verification batch.

Evidence: `.artifacts/accepted-source-rebuild-integration-3.log` (two isolated tests passed, 154 seconds); `.artifacts/securities-pricing-source-integration-3.log` (five tests passed); `.artifacts/accepted-signal-source-integration-3.log` (nine-family atomicity/reconstruction test passed). Normal strict Storage build: `.artifacts/accepted-signal-store-strict.log` (zero warnings/errors). Securities, OptionPricer and Analytics domain builds passed under diagnostic analyzer overrides; those do not replace the remaining strict host gate.

Trade plans remain disposable, single-attempt snapshots with source/history writes in their owning transaction. No trade-plan replay, repair, retained plan history or retry queue was introduced by these changes.

The full refactor is still in progress. Remaining gates include the complete projector/native-maintenance audit, configuration baseline, normal strict API compilation, offline migration and verified Development cutover, backup/restore/health/rollback qualification, UI integration, and uncontaminated sustained benchmark repeats/soak. No Development database connections or durability settings were switched in this batch.


### Configuration and accepted observation verification (2026-10-12 UTC)

- The accepted-observation matrix now covers 17 source event types and 19 PostgreSQL tables: ADX, ATR, MACD, RSI, TDI, EMA, Bollinger Band, VWAP, VX term structure, ITI/timeframe state, trade-signal indexes, Market Outlook, closing prices, session bars/raw EOD, legacy bars and VX EOD. Injected failure rolls back sources and projections together. Reconstruction from actual persisted MessagePack event bytes matches complete row images; repeated reconstruction does not increment VX volume. Evidence: `.artifacts/accepted-legacy-eod-integration-2.log` (one matrix test passed).
- Daily bar reconstruction uses its already accepted interval bounds; the accumulator fixes these to the complete session. It does not query a current calendar. Legacy EOD events now carry accepted raw observation facts. VX EOD events carry exact accepted OHLC/volume replacement facts resolved under the transaction's contract/date lock before source append. Earlier events without these facts require explicit baseline coverage; reconstruction refuses to invent them.
- Generic parameter sets, assignments and startup records now enlist configuration rows, immutable versions, audit and operation receipts with their source events. Option-chain policy publication/retirement also enlists the MarketData policy rows. The focused test verifies rollback and identical reconstruction across nine tables, including original receipt timestamps.
- Configuration baseline capture and pure materialization are implemented. The isolated verifier reconstructs every captured table, including published and retired catalog versions, preserves immutable guards and restores the lookup sequence without reusing an allocated ID. It rejects capture without explicitly quiesced writers and rejects corrupted row hashes. Evidence: `.artifacts/configuration-baseline-integration-4.log` (two focused tests passed). **Development configuration baseline capture and full cutover remain outstanding.**
- The baseline migration CLI passes a normal strict build with zero warnings/errors and verifies saved row hashes. Its `--schema-inventory` mode performs no database operations and exports fully resolved schema definitions, including generated configuration/catalog tables and the separate financial schema. The inventory contains 272 table definitions; only the five TradeTickQuoteDb tables target ScyllaDB. Runtime counts, keys and values remain a separate migration verification gate.
- Restore-provider validation now includes TradeTickQuoteDb alongside the PostgreSQL contexts. This is table readability validation, not proof of restored data equality or completed cutover.

Normal strict Storage build for the EOD materialization batch passed with zero warnings/errors (`.artifacts/accepted-legacy-eod-storage-strict-3.log`). Diagnostic Feed/Analytics builds and integration tests do not replace the outstanding strict API host gate. Live-feed on/off transaction verification, the remaining native/projector audit, offline migration, rollback/backup/health/UI qualification and uncontaminated benchmark repeats/soak are still in progress. No Development connection switch, durability change or database reset was performed in this batch.


### Accepted Portfolio projection verification (2026-10-12 UTC)

- Portfolio, Fund and financial-policy command handlers capture complete accepted projection facts from their already-updated aggregate before append. Twenty-two additive MessagePack contracts preserve existing key numbers. No aggregate-history read is added to the normal command path.
- The existing financial admission fence materializes these facts on the same PostgreSQL transaction as the source event. Post-commit callbacks do not repeat captured writes. A missing transactional fence is rejected before append.
- Explicit read-model rebuild descriptors materialize accepted facts only, without admission, subscriptions, providers or notifications. The named-stream rebuilder uses the same native store. Older complete events are reconstructed only through their own revision, rather than loading later aggregate state; receipt-only older history still requires baseline evidence.
- The Portfolio policy-assignment descriptor and host transaction registration are included. Activation plus assignment currently remains a two-stream operation with independently atomic source/projection writes; its compound command boundary still needs review.
- Three isolated PostgreSQL tests passed: source/projection rollback and exact decoded-source rebuild, financial-fence materialization, and all 22 wire contracts. Twelve Portfolio projection regression tests passed after updating their fixtures to expose actual source histories. Normal strict Storage build passed with zero warnings and zero errors.
- Evidence: `.artifacts/accepted-portfolio-integration-4.log`, `.artifacts/portfolio-projection-regressions-2.log`, `.artifacts/accepted-portfolio-storage-strict-3.log`.
- Live-feed on/off source/projection failure and pure rebuild verification also passed two tests (`.artifacts/accepted-live-feed-integration.log`).

The full refactor remains in progress: strict API compilation, remaining native/projector and compound-command boundaries, migration/cutover, backup/restore/health/UI evidence, and uncontaminated sustained benchmark acceptance are still open. No Development connection or data cutover was performed by this batch.


### Offline migration verification (2026-10-12 UTC)

- A 435,251,494-byte pre-migration archive of Development EventSourceDb was restored into an isolated database. Its 498,406 events match source counts, event-version sums, payload-byte totals and the ordered payload fingerprint (MD5 of ordered per-event MD5 digests). Archive SHA-256: `FC07B79772F71D0CA1E25462D468DA4004388CA00C4B7260B29E90E0B9BFC379`.
- The three option-chain policy versions/current rows were copied without changing source revisions or content. The approved Development configuration baseline is now source event **519592**, source cut **519591**, baseline `e348ec0a-15ed-4929-be04-4268f6c2344f`: **1,036 rows across 37 tables**. Actual persisted source bytes reconstructed twice in an isolated database; every complete row image and the lookup sequence matched. This supersedes the earlier pending configuration-baseline entries.
- `scripts/Storage/ConfigurationBaselineMigration --verify-isolated-rebuild` verifies the actual saved baseline, rather than only synthetic test facts. Its normal strict build passes with zero warnings/errors.
- `scripts/Storage/OfflineMigration` prepares all 13 native PostgreSQL context schemas plus financial schema in an isolated staging database. It performs bounded-page Scylla reads, native PostgreSQL binary import, count/key/value fingerprints and a stable-source reread in one table transaction. Existing matching target rows are verified on resume; mismatches roll back. Missing source columns/tables and unsupported conversions require explicit handling. It cannot target the Development application database. Its normal strict build passes with zero warnings/errors.
- Sequence schema recovery now verifies allocation functions, sequence readability and allocation increments without calling `nextval`. The isolated regression test also removes a function, confirms rejection, restores it, and proves no sequence state changed (`.artifacts/sequence-recovery-integration.log`, one passed).
- The staged data inventory/copy is in progress. A finite year-one source timestamp exposed the provider's implicit infinity conversion; this is being resolved explicitly before migration acceptance. A new invocation table has no same-named Scylla source and requires ownership classification. Neither is a user-input blocker.

No Development connection cutover or durability change has occurred. Remaining migration, full-host strict compilation, complete projection audit, backup/restore/health/UI evidence and uncontaminated sustained benchmark acceptance are still required. Originals and rollback artifacts remain preserved.


### Consolidated migration verification (2026-10-12 UTC)

- Verified native PostgreSQL staging data was transferred into an isolated clone of the current Development event database. Complete JSON row images match across **140 transferred tables and 4,291,304 rows**. Two Regime tables were excluded from transfer because their canonical records and 115 invocation receipts were already materialized from actual accepted source events in the consolidated clone.
- The finite year-one timestamp conversion is resolved in the offline executable by disabling Npgsql infinity conversions. All four execution records were copied with exact finite source dates. Seven time-valued table families now preserve Scylla nanoseconds where exactly representable as PostgreSQL microseconds; unrepresentable values are rejected rather than rounded.
- Sequence migration preserves the 26 existing allocation high-water marks and increments. Two newly reserved sequence slots remain unallocated. Sequence `setval` is not transactional; the offline tool validates all states before writes and verifies them before admission.
- A failed binary import leaves zero target rows. Resume refuses mismatched nonempty targets without changing them. Both fault probes use an isolated fixture database; application sources are unchanged.
- New `regime_discovery_invocation` has no Scylla predecessor and is rebuilt from 115 complete persisted source events, without providers or notifications. New Securities pricing-reference bundle storage has no old same-named Scylla table and remains empty; retired unused Reference bundles are not resurrected.
- Raw Scylla migration has created the reviewed TradeTickQuote keyspace and a tagged source snapshot. The first table, `tick_trade_data`, has 649,775 exact matching complete rows. Verification of the remaining four tables is still running. Original source tables and snapshots remain intact.
- Latest strict API build has zero compiler errors and 1,506 analyzer errors. Explicit actor-context null diagnostics now retain their original argument names through a named helper, and generated C# 14 extension-block nested types have scoped compatibility annotations. Analyzer enforcement remains enabled.

This is not final cutover acceptance. Full-host strict compilation, remaining projection/host audits, consolidated backup/restore/health/UI qualification, and uncontaminated sustained benchmark repeats/soak remain open. Development application connections and durability settings have not been changed.


### DbContext and transaction regression verification (2026-10-12 UTC)

- Required argument and route-page validation now lives in the DbContext binding extensions, while public context methods implement the declared persistence capability contracts. Contract tests include the inherited market-data ports and enlisted Trade writer instead of checking only two interfaces.
- Fluent persistence calls are formatted on separate lines. XML documentation checks accept documented methods with intervening attributes.
- Standalone MarketDataService PostgreSQL reads no longer configure asynchronous disposal on a null ambient command. An enlisted command is disposed only after the null branch has selected the standalone path.
- Normal strict Storage build passes with zero warnings and errors (`.artifacts/context-conventions-strict-5.log`). The diagnostic test assembly passed 101 focused tests: 79 context convention tests and 22 durable-intent PostgreSQL/safety tests (`.artifacts/context-conventions-final-2.log`). This diagnostic test build does not satisfy the full-host analyzer gate.
- Legacy integration fixtures now require dedicated localhost test database/keyspace names rather than Development destinations. After stopping an earlier broad legacy test attempt, all nine original modern Trade tables were checked against verified staging rows, including 679 position-history rows. Pre-repair counts also matched. Original rows were reinserted and full row images verified; no unrelated rows were deleted. Final sequence high-water verification remains required before cutover.
- Latest strict API build still has zero compiler errors and 1,506 analyzer errors (`.artifacts/api-strict-build-46.log`). Mechanical fixes are in progress with analyzer enforcement retained.
- Raw quotes require logical CQL transfer: native SSTable refresh retains the source keyspace in the quote UDT definition, which does not match the new keyspace. The bounded transfer verifies complete row fingerprints and preserves the source and tagged snapshot. It is still running.

Development connection cutover and final benchmark acceptance have not occurred.


### Host-layout and recovery validation follow-through (2026-10-12 UTC)

- The native PostgreSQL/context suite passed 197 tests without failures or skips (`.artifacts/native-postgres-final-2.log`). The Portfolio suite passed 473 tests after collection-contract changes. Four conventional-projector tests include concurrent cache publication and immutable event-type results.
- `ApplicationStorageConnections` supplies thirteen domain PostgreSQL connections with one physical/security identity and one explicit raw Scylla connection. API registration, recovery infrastructure probes and maintenance composition roots use these shared settings. Raw contexts/schema no longer fall back to the derived MarketDataDb connection. Two layout tests verify routing and missing-raw rejection.
- The scheduler's separate PostgreSQL source has been archived and copied into consolidated staging: all 19 Quartz/scheduler tables and 12 stored rows match complete source/target row images. Archive SHA-256 `81322739F97522DCF3FA0EDB8FA632E228D7034667466A594E0C7A2EA7EC02F8`; proof `.artifacts/storage-cutover/consolidated-scheduler-transfer-proof-20261012.json`. Its runtime connection has not been switched.
- Restored-database validation now includes non-allocating sequence checks and financial schema version/table reads. An isolated failure test removes a financial table by reversible rename, confirms rejection, restores it, and confirms health without event writes. Three focused financial/layout tests passed; the normal Storage build has zero warnings/errors.
- Raw Scylla owns the five Databento tables and their UDT plus an infrastructure-only TTL probe table. The latter contains no business state and is excluded from source data migration/reconstruction. Hard recovery qualification now initializes/checks that probe in the raw destination rather than the derived MarketData database.
- The latest full diagnostic API build passes with zero warnings/errors under its explicit analyzer override; it is compilation evidence only. Strict API analyzer acceptance remains unfinished. The final migration, host cutover, backup/restore/runtime/UI checks and uncontaminated benchmarks remain open.

### Real host validation and disposal fixes (2026-10-12)

The API's actual composition root and providers validated the consolidated staging database successfully with `--validate-restored-databases-only` (exit 0). It read all 14 registered schema contexts, including raw TradeTickQuoteDb and SequenceIdDb, plus all 29 financial tables. This mode started no actors, feeds, hosted services, migrations or HTTP listeners. Evidence: `.artifacts/storage-cutover/consolidated-api-provider-health-20261012-6.log`.

This check exposed and resolved three integration defects: register shared connection settings as `IDbConnectionSettings`; keep the normal ConfigurationSchemaDb constructor free of value-type injection parameters and expose structure-only initialization through `WithoutLookupDefaults`; dispose the Simple Injector container asynchronously before cross-wired host dependencies. Worker recovery disposal now shares one cleanup task across DI aliases and concurrent callers. Its DI/routing regression group passed 10 tests; System Admin unit tests passed all 155 tests. Portfolio unit tests passed all 473 tests after the latest generic validation changes.

These results do not admit Development connections or complete the strict full-host analyzer gate. Raw quote migration, final archives/restore, host cutover and sustained benchmark acceptance remain in progress.

### Final migration and host handoff checks (2026-10-12)

- The resolved migration manifest accounts for all 142 destinations with zero unresolved conversions. The eight previously failing date/time tables were rechecked against the consolidated target; complete source rows matched. The two source-absent destinations are explicitly classified: Regime invocation is reconstructed from accepted source events, and Securities pricing bundles have no legacy source to copy. Evidence: `.artifacts/storage-cutover/resolved-migration-manifest-20261012.json` and `final-source-*.json`.
- Sequence state was refreshed after fixture activity: all 28 enum-defined source sequences now match the consolidated target with allocation size 100. The earlier 26-sequence report is superseded. Evidence: `consolidated-sequence-state-final-proof.json`.
- Full consolidated archive: 784,674,534 bytes, SHA-256 `E87A96DD124F08F9A500885544E1B88A683674F00AB7907FF1A627C23508F45D`. Restore into the fresh `ifm_storage_migration_staging_restored_20261012` database completed. Complete-row comparisons and application-provider validation are still in progress.
- Scheduler's shared connection path now resolves the same environment credential policy as API storage and passes the resolved connection to both Npgsql and Quartz. The two portable host-registration tests pass. Its Development installer no longer provisions an independent Scheduler database/role; it reads the API database setting. Script parsing passed; deployment settings have not yet been changed.
- Backup host templates now scope Scylla backup to `trade_tick_quote_test_db`. The historical `read-model-scylla` protection-set identifier remains stable for recorded manifests; it no longer selects business/read-model keyspaces for new backups. Existing backup records are not rewritten. The consolidated PostgreSQL/native-backup deployment handoff and qualification remain outstanding.
- Trade-signal lookup maintenance now locks, clears and reconstructs the actual `futures_trade_signal_lookup_by_scope` table in one transaction. Native observation integration passed, including an injected reconstruction failure preserving prior lookups and canonical observations.


### Final isolated migration verification (2026-10-12)

- Raw Databento migration completed: tick trades 649,775; tick quotes 1,484,499; futures ticks 4; futures ticks by time 4; option ticks 1. Every table's full canonical row fingerprint matches its unchanged source. Evidence: `.artifacts/storage-cutover/raw-scylla-final-proof-20261012.json`. The 126 rejected cross-keyspace UDT upload files were moved to an isolated quarantine after the logical quote copy passed; original source data and snapshots remain intact.
- The complete consolidated PostgreSQL archive restored successfully in one transaction to the isolated restore database. All 286 tables and 5,850,663 complete rows match source ? restore ? source, including scheduler tables and event history. Evidence: `.artifacts/storage-cutover/consolidated-isolated-restore-row-proof-20261012.json`.
- Metadata verification passed: 2,883 columns, 618 constraints, 476 indexes and all 31 sequence definitions/states. Three PostgreSQL deparser differences in array casts were normalized narrowly; index keys and predicates remain equivalent. Evidence: `.artifacts/storage-cutover/consolidated-isolated-restore-metadata-proof-20261012.json`.
- The API's actual providers validated the restored database and raw destination without starting feeds, actors, hosted services or migrations. Evidence: `.artifacts/storage-cutover/restored-consolidated-api-provider-health-20261012.log` (exit 0).
- Financial source/movement hashes now format numeric facts with invariant culture. Nine accounting tests passed, including identical accepted-fact hashes under en-US and fr-FR.

Development promotion, runtime/UI checks, strict full-host analyzer acceptance and uncontaminated sustained benchmarks remain open. Earlier chronological notes describing raw migration/restore as running are superseded by this verification.


### Domain regression verification (2026-10-12)

The current provider-refactor regression suites pass: Portfolio 474, Reference 208, System Admin 155, Analytics 1,190, and Feed 579 (2,606 total). These runs used the explicit diagnostic analyzer override; they verify behavior and do not replace normal strict compilation. Evidence: `.artifacts/TomasAI.IFM.Domain.Portfolio.UnitTests-provider-refactor-final.log`, `.artifacts/reference-provider-refactor-recheck.log`, `.artifacts/TomasAI.IFM.Domain.SystemAdmin.UnitTests-provider-refactor-final.log`, `.artifacts/analytics-provider-refactor-recheck-2.log`, and `.artifacts/feed-provider-refactor-final.log`.

Parameter rejection codes remain exact protocol values. Token-aware query/repository mocks now exercise the actual storage overloads; compiled logger assertions use the public ILogger contract. Analytics event error handlers now contain a failed secondary error publication and log it, including the original validation failure, instead of allowing a null-context publication exception to escape. No database connections were promoted during these tests.


### Current acceptance follow-through (2026-10-12)

- The latest dedicated PostgreSQL/provider/DbContext convention run passes all 199 tests (`.artifacts/native-postgres-provider-refactor-recheck.log`). This includes the compound Portfolio fence failure test: a conflict on the second append rolls back both source streams and their accepted projection rows.
- Policy activation plus Portfolio assignment now calls `AppendPolicyAndPortfolioAsync`, which enlists both streams through `PortfolioAuthorityFence.AppendBatchAsync`. Earlier chronological notes describing independent commits are superseded. Compatibility completion for already accepted older activations remains separate from the normal atomic path.
- After cancellation forwarding, Reference passes all 208 tests again (`.artifacts/reference-cancellation-regressions-3.log`). After extracting the compatibility helper and narrowing private return types, Portfolio passes all 474 (`.artifacts/portfolio-compound-refactor-regressions.log`).
- Diagnostic API compilation passes with zero warnings/errors (`.artifacts/api-diagnostic-80.log`). Normal strict host acceptance is still in progress; diagnostic settings do not discharge that gate. Development promotion and the quiet sustained benchmark/soak gates remain pending.


### Remaining host build checks and calculation refactoring (2026-10-12)

- Normal strict API builds improved from 234 analyzer errors (`.artifacts/api-strict-82.log`) to 194, 142, and then 102 (`.artifacts/api-strict-88.log`). All these builds had zero compiler errors. The strict full-host gate remains open. Diagnostic API build 88 passed with zero warnings/errors; diagnostic settings are not strict acceptance.
- Timer registrations now use `CancelAsync` and await both cancellation callbacks and the callback loop before disposing their cancellation source. Expected cancellation and coalesced semaphore-wakeup behavior are documented explicitly. The EOD outage spool retains its physical disk flush; asynchronous stream flushing alone does not replace that durability requirement.
- Frozen parameter startup snapshots build their sorted read-only views once. Market Outlook projector event types are also cached once. Mutable financial aggregate views retain their existing snapshot semantics.
- Wilder RSI seed/smoothing, ADX directional values, IV rank/percentile, provisional daily-bar construction, realized PnL, financial authority validation, and Development book posting-rule construction have been separated into focused methods without changing their calculation definitions. Sequential accepted-event reducers and asynchronous lifecycle operations keep explicit loops with scoped analyzer explanations.
- Latest affected regression evidence: Analytics 1,190 passed (`.artifacts/analytics-formula-helper-regressions.log`); Reference 208 passed (`.artifacts/reference-frozen-snapshot-regressions.log`); Feed 579 passed (`.artifacts/feed-async-stop-regressions.log`); Portfolio 474 passed after authority/book helper extraction (`.artifacts/portfolio-authority-helper-regressions.log`). Subsequent helper changes are undergoing the next build/regression pass.
- Automatic approval review rejected proposed MDI fallback simplification and a portfolio paging-loop condition edit. Neither rejected change was applied; existing trading fallback calculations and explicit paging-overflow behavior are preserved.

No Development application database connection has been promoted in this step. The original databases, verified consolidated/restored copies, raw tick/quote copy, and backup evidence are preserved. Remaining completion gates are strict host compilation, the final projector/host audit, Development connection handoff and runtime/UI qualification, and quiet sustained durability/mixed-load benchmarks with the planned repeats and soak.


### Atomic import and downstream host checks (2026-10-12)

- Economic-calendar and Treasury imports now require the same PostgreSQL database/security identity as EventSourceDb. The former non-PostgreSQL direct-write fallback was removed. Accepted source facts and projection rows remain enlisted on one transaction; publication remains after commit. Uncertain commit failures report an unknown persisted count.
- All 280 MarketData unit tests pass, including accepted import outcomes, failure counts, notification delivery failures and token-aware query fixtures (`.artifacts/market-data-atomic-import-regressions-3.log`). Native source/projection reconstruction remains covered by the isolated PostgreSQL import integration test.
- The earlier Feed, Analytics and Portfolio strict diagnostics are cleared. The strict API dependency traversal then reached Domain.Trade and reported 2,760 analyzer errors (`.artifacts/api-strict-110.log`). Conservative null guards, parameter-name alignment, ordinal string comparisons and configured awaits reduced that layer to 1,060 (`.artifacts/api-strict-115.log`). Diagnostic host compilation remains successful (`.artifacts/api-diagnostic-114.log`); this is not strict acceptance.
- Trade actor contexts and contracts are being separated into files named for their types. Existing namespaces, published identifiers, wire values, transactional ordering and trading decisions remain unchanged. Further strict checks and behavioral suites are pending.

Development connection promotion, runtime and UI qualification, and uncontaminated sustained benchmarks are still pending. The original and verified migration databases and archives remain preserved.


### Final Development clone verification (2026-10-12)

- The final `ifm_application_development` database was created from the verified consolidated staging database. Complete ordered row comparison verified all 286 tables and 5,850,663 rows; metadata comparison verified 2,883 columns, 618 constraints, 476 indexes and all 31 sequence definitions/states. The source remained unchanged. Evidence: `.artifacts/storage-cutover/development-clone-row-proof-20261012.json` and `development-clone-metadata-proof-20261012.json`.
- The isolated PostgreSQL/DbContext suite passed all 199 tests (`.artifacts/native-postgres-cutover-regressions.log`). Sequential domain suites passed 4,406 tests across MarketData, Reference, SystemAdmin, OptionPricer, Portfolio, Analytics, Feed and Trade. Subsequent logging changes require another affected Trade pass.
- Strict dependency checks have reached Domain.Trade. The latest completed check reports 640 analyzer errors, zero warnings, with no C# compiler errors (`.artifacts/api-strict-122.log`). Published namespaces are preserved; redundant interface declarations and context validation diagnostics have been corrected. This is progress, not full strict-build acceptance.

Application connections have not been promoted. Remaining gates include strict host acceptance, provider/runtime and UI checks on the final database, scheduler and backup connection handoff, the final transaction/projector audit, and uncontaminated sustained benchmarks and soak. Original databases and all verified archives remain preserved.


### Remaining strategy plan atomicity corrected (2026-10-12)

The final projector audit found that Futures and Vertical Spread Function repositories still used source-commit-then-project. They now enlist source append, command reservation and required material plan/activity rows through `TradePlanAtomicPersistence` on one PostgreSQL transaction. A persistence failure is logged and propagated for snapshot drop; publication runs after commit and cannot undo it. The existing nonmaterial-plan policy for those two strategies is preserved.

- Four isolated native PostgreSQL repository tests passed: successful source/row commit and failure injected after plan-row writes for each strategy. Both failure cases prove that source and plan rows are rolled back (`.artifacts/native-strategy-plan-atomic-repositories-2.log`).
- Three snapshot-notification tests passed: Iron Condor, Futures and Vertical Spread do not perform a second PostgreSQL write or request durable replay (`.artifacts/plan-notification-no-second-write-tests-2.log`).
- Iron Condor limit reconstruction remains an explicit source-facts write. Its live PostgreSQL notification skips the already committed write; its rebuild action remains available.
- The central daily risk calculator now separates observation mapping, spread/exposure metrics, daily forward-loss calculations and monitoring status into documented methods in the same class. The Trade regression suite passed all 1,493 tests after that extraction (`.artifacts/trade-snapshot-helper-regressions.log`). A full affected suite after the two additional repository changes remains pending.

The final Development clone passed actual application-provider health reads (`.artifacts/storage-cutover/final-development-api-provider-health-20261012.log`). Application connections remain unpromoted; strict host acceptance and final runtime/backup/UI/benchmark gates remain open.


### Normal compilation and complete projector role audit (2026-10-12)

Strict analyzer cleanup is deferred at the user's request. Normal compilation remains required; this supersedes earlier entries treating analyzer method-length diagnostics as a completion blocker.

- Normal API build passed with zero warnings and errors: `.artifacts/api-normal-155.log`. Explicit missing framework imports were corrected without restoring global strict settings.
- Actual API composition-root startup verification passed against `ifm_application_development` and `trade_tick_quote_test_db`: `.artifacts/storage-cutover/final-development-startup-verification-20261012.log`. This mode starts no schemas, actors, feeds or HTTP listeners; runtime acceptance remains separate.
- All **220** filtered native PostgreSQL, DbContext convention and backup projection tests passed using the normal build: `.artifacts/native-postgres-normal-final-3.log`. One non-blocking existing xUnit assertion-style warning remains.
- All **56** concrete projector roles have been traced in `PostgreSQL-ScyllaDB-Transaction-Boundaries.json`. Required business rows commit with source facts. Optional monthly financial history and subscription intents are explicitly separate, rebuildable asynchronous boundaries. Broker execution is an external post-commit action. Raw tick writes are the approved ScyllaDB exception; option-chain quote enrichment is a separate derived PostgreSQL write.
- The final Development database includes verified exit-workflow baseline event **519593** (three accepted stage rows, reconstructed twice), following the prior financial and configuration baselines. A new empty backup execution outbox is present. A fresh archive and isolated restore are being verified before promotion; earlier 286-table counts predate these additions.

Remaining acceptance: final archive/restore comparison, Development connection promotion, normal UI/Scheduler builds, runtime/start-stop and backup health checks, and quiet sustained BenchmarkDotNet workload repeats/soak. Automatic restart delivery of accepted backup requests remains uninstalled pending the previously requested authorization.


### Verified Development promotion (2026-10-12)

- The final archive restored to `ifm_storage_final_restore_20261012`. All **287 tables / 5,850,669 full rows** matched source -> restore -> source. Metadata matched **2,886 columns, 621 constraints, 479 indexes, 31 sequence definitions and states**. Proofs: `.artifacts/storage-cutover/final-development-restore-row-proof-20261012.json` and `final-development-restore-metadata-proof-20261012.json`.
- Archive: `.artifacts/storage-cutover/ifm-final-development-prepromotion-20261012.dump`, **784,678,518 bytes**, SHA-256 `CB30DACF38AF61909B266D0CA8D1556B1571D41F95D205A9939EDD2989C72EF6`.
- Actual application providers validated the isolated restore, all 14 contexts and 29 financial tables, without starting writers. Fresh schemas/catalogs initialized successfully in a dedicated empty PostgreSQL fixture, including the backup outbox foreign key ordering.
- `scripts/Storage/Promote-DevelopmentStorage.ps1` verified complete restore proofs and archive hash, required stopped Development processes, preserved rollback settings, and changed only Development configuration. All 13 domain connection keys now share `ifm_application_development`; raw TradeTickQuoteDb targets `trade_tick_quote_test_db`. Production settings and original databases are unchanged.
- Normal API/UI/ServerManager/Scheduler/backup-host builds passed. Normal tests: 220 native storage, 1,496 Trade, 39 portable Scheduler, one actual Quartz PostgreSQL persistence test and 112 AWS backup unit tests passed. Targeted UI tests passed three with two explicit live opt-in tests skipped.

Runtime startup/shutdown, scheduler deployment handoff, backup-host connection handoff and sustained durability/soak acceptance remain in progress. The new plan-only/mixed-with-reads benchmark modes passed a functional smoke test; its two-second results are not performance acceptance.

### Development runtime handoff ? 2026-10-12

Normal API and UI builds pass with zero warnings/errors. Actual Server Manager startup exposed and fixed: missing named cache-schema catalog entries; CQL-only option-chain reads; the instrument-definition/product adapters still using CQL; transient UI listener ownership diagnostics; and bounded actor-state loads losing the source-stream concurrency version when their selected event type was absent or an older snapshot was used. Stream metadata is now read before bounded state replay; the existing expected-version guard remains active at append. No history was cleared to make a restart succeed.

Evidence: native cache initialization/publication regressions 2/2; isolated PostgreSQL instrument definition/product/cursor tests 2/2; UI composition 1/1; RSI state-version tests 4/4. API/UI/Scheduler start through the standard Development script, API launch readiness returns 200, and the real UI main window is responsive. Final runtime log: `.artifacts/development-consolidated-start-20261012-8.log`. Normal builds: `.artifacts/api-postpromotion-normal-6.log`, `.artifacts/ui-postpromotion-normal-1.log`.

The rebuilt native backup container started and returned healthy, using the consolidated connection and the raw-only Scylla protection set. Its existing journal also resumed older LocalWorkstation requests whose default online-vault enrollment file is absent. The container was stopped to avoid repeated attempts; those requests/journal were preserved. This is not proof of a newly completed on-demand backup through that host. The complete current PostgreSQL archive/isolated restore/provider validation above remains verified.

Durability telemetry now captures container CPU/memory/I/O and PostgreSQL WAL/checkpointer/I/O/lock/backend-wait samples during sustained runs. Application durability remains unchanged. Strict analyzers remain deferred by user request.


### Durable default and weekend separation ? 2026-10-12

The live Development PostgreSQL server reports `fsync=on` and `synchronous_commit=on`: `.artifacts/storage-cutover/development-durability-settings-20261012.json`. No application persistence code requests `synchronous_commit=off`. Relaxed settings remain confined to disposable benchmark instances. The twelve-hour matrix has not started and no automatic schedule was created.

Final native regression run: 223 passing tests plus one formatting convention failure; that failure was corrected and rerun successfully alongside a new actual PostgreSQL committed-stream-version regression (2/2). The new regression proves a bounded state query must preserve source stream versions even when the selected snapshot event type is absent. Final UI normal compilation passes with zero warnings/errors. Telemetry smoke validation captures container CPU/memory/I/O and PostgreSQL WAL/checkpoint/I/O/wait data; it is not a performance result.


### Final normal compilation and migration correctness acceptance ? 2026-10-12

- The full `TomasAI.IFM.sln` normal build succeeds with **zero warnings and zero errors**: `.artifacts/solution-storage-refactor-normal-final-3.log`. Missing imports in two cache test files and explicitly typed null parameters in three queued-command tests were corrected. `Directory.Build.props` remains as the user configured it; strict analyzers were not restored.
- Final focused regressions pass: **4 cache tests**, **19 framework provider/connection-ownership tests**, and **2 native PostgreSQL corrections**. Logs: `.artifacts/shared-cache-normal-regressions.log`, `.artifacts/framework-dbprovider-normal-regressions.log`, `.artifacts/native-postgres-final-corrections-2.log`. The previous broad run passed 223 cases; its single formatting convention failure is fixed and included in that successful focused rerun. Earlier recorded native rollback/atomicity, Trade, Scheduler, backup and UI results remain applicable.
- Migration correctness is validated by full row and schema metadata comparisons, application-provider reads of the isolated restore, verified Development cutover, fresh schema initialization, actual API/UI/Scheduler startup/shutdown, and transaction rollback tests. Performance capacity is not yet qualified.
- Operational item: the local backup host is stopped because its preserved older operations expect the missing `E:\IFM\DatabaseBackup\online-vault\vault\enrollment\media.json`. Locate the original enrolled vault before resuming those requests. The database migration does not depend on recreating or discarding that vault. Automatic API-restart delivery of accepted backup requests remains a separately pending feature decision; no new dispatcher was installed.

#### Deferred weekend command

Run from the repository root during an agreed quiet weekend window, with Docker available and sufficient free storage. This is a manual command, not an installed schedule:

```powershell
.\scripts\Benchmarks\Compare-PostgresDurability.ps1 -LoadSeconds 1800 -OutputRoot .artifacts/weekend-postgres-durability
python .\scripts\Benchmarks\Summarize-PostgresDurability.py <the-run-directory-printed-by-the-script>
```

The command covers six profiles and four half-hour workloads (twelve hours plus BenchmarkDotNet overhead). Representative repeats, the final durable soak and concurrency/rate exploration remain follow-up capacity work. The script uses only generated, labelled disposable benchmark containers/volumes; application connections retain durable on/on settings. Host workloads and resource contention must be recorded when interpreting results. No long benchmark campaign is currently running.


### Local backup-host vault repair ? 2026-10-12

The missing enrollment blocker is resolved. The configured local vault was empty; no enrollment was found in the backup or qualification directories. An explicit Development-only `--enroll-development-vault` maintenance command uses the existing signed enrollment contract and existing manifest keys. It refuses a nonempty vault and non-Development environments. The vault now has signed `media.json` plus the public trust bundle. The current journal was preserved with a separate SQLite backup before the change.

The rebuilt host is running healthy, `/health/ready` returns 200, and a pending native backup progressed beyond its previous enrollment failure. The remaining older requests remain in the journal. Successful negative tests prove existing enrollment and Production enrollment are rejected without changing the identity. Evidence: `.artifacts/backup-vault-repair/enrollment.log`, `host-runtime.log`, `repeated-enrollment-rejected.log`, `production-enrollment-rejected.log`. This verifies the enrollment/startup repair, not completion of every queued backup or recovery of missing historical artifacts.


### Live workflow update correction ? 2026-10-12

The live Strategy screen exposed two gaps beyond the previous generic provider checks. `IntrinsicTimeStrategyWorkflowCommandActor` reserved an audit command before its repository reserved that command in the atomic commit, causing `CommandAuditDuplicateException` and rolling back new snapshots. The PostgreSQL workflow actor now delegates audit ownership to that atomic source/projection transaction; legacy provider behavior remains unchanged. Fresh daily and weekly rows were verified after deployment.

Workflow history/detail reads still used CQL SQL and object-array bindings. Six reads now select native schema-qualified SQL and typed positional Npgsql parameters on PostgreSQL: workflow detail, active state, start attempts, timeline, entity history and status/date history. Historical ordering is explicit. The seven focused workflow snapshot tests pass, including audit ownership, successful source/row commit, rollback, post-commit notification failure and actual PostgreSQL history reads. Evidence: `.artifacts/strategy-workflow-history-read-regressions-5.log`. These tests extend the migration correctness coverage; generic startup/provider validation alone did not reveal the UI query path gaps.

Live restart verification: all three Daily/Weekly/Monthly `GetHistoryPage` requests are followed by workflow detail `GetByIds` requests and successful replies, including saved monthly records. Runtime evidence: `.artifacts/strategy-workflow-live-history-verification.json`. Fresh daily/weekly snapshots were verified in PostgreSQL after correcting the audit boundary. This verifies the live query/hydration path; no visual FlaUI assertion was performed in this correction.
