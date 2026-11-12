# DbContext source layout implementation plan

Date: 2026-11-12

Status: Implemented. This is the source-layout follow-up to the [13-context alignment plan](DbContext-Conventions-Alignment-Implementation-Plan-v1.0.md).

## Scope and acceptance

Apply the [mandatory source layout](../../TomasAI.IFM.Application.Storage/Docs/Storage-Implementation-Details.md#source-organization-conventions) to all 13 concrete Application.Storage contexts. Each context owns exactly seven root source files and one `Schema/` folder with a schema executor and a native DDL catalog. Move supporting documentation to Storage's Docs directory and move independent services or shared infrastructure to their owning locations. The shared abstract schema executor stays outside the concrete context directories.

Preserve public contracts, dependency injection lifetimes, physical database objects, provider dialects, event and serialization identities, parameter ordering, database integrity checks and transaction boundaries. Preserve existing public type names and namespaces where moving a declaration is sufficient. Changes to a public type or namespace require updating and verifying all consumers.

## Work and verification by context

| Context | Implementation | Required regression coverage |
| --- | --- | --- |
| ConfigurationDb | The nine source paths already match. Verify placement of mappings, transaction sessions and writer leases in the extensions file. | PostgreSQL baseline rebuild, parameter version reads, writer locks and concurrent write rejection. |
| EventSourceDb | Consolidate CommandAudit and EventLog context-owned bindings, helpers, appenders and result types into the prescribed files. Put context-specific transaction and journal implementations alongside the extensions and co-locate their capability contracts by role. Move shared financial telemetry outside the context. Remove the three feature folders and relocate the binary cutover guide. Normalize catalog filenames to the existing `EventSourceActor` context stem. | PostgreSQL sequential and binary COPY writes, command deduplication and conflicts, batch ordering, latest creation and typed snapshot replay, checkpoint and gap validation, atomic append/projection/receipt rollback, retry and commit-uncertainty handling, and Risk replay idempotency. |
| MarketDataDb | Place `MarketDataImportStreamFence` beside the transaction helpers. Keep `TradingDaysKey` with its consumer if it is a persistence key, or move it to the calendar/cache owner if it represents application policy. Remove the old ScyllaDb subfolder from the context layout. | PostgreSQL import rollback, stale revision and concurrent writes, trading calendar reads and stable snapshot ordering. |
| MarketDataServiceDb | Verify the seven root source files and schema pair. Relocate the subscriptions README and remove the Recovery and Subscriptions subfolders. | PostgreSQL subscription intent round trips and restart, recovery receipt/checkpoint idempotency, and atomic VX-pair replacement. |
| PortfolioDb | Merge `ExecutionCapacitySql` into `PortfolioDbSql.cs` and remove the remaining Recovery subfolder from the context layout. | PostgreSQL capacity allocation and contention, duplicate requests, rollback across events and projections, and financial restart recovery. |
| ReferenceDb | The nine source paths already match. Verify catalog statements, typed bindings, projections and context helpers are in the prescribed files. | PostgreSQL lookup and catalog round trips, projection rebuild, and concurrent version updates. |
| ScheduledTaskDb | Co-locate `IScheduledTaskTransactionalProjectionWriter` with the write contract and preserve its enlisted capability. | PostgreSQL latest task/catalog/run snapshots, task claim and restart, and rollback of run and session-date projections. |
| SecuritiesDb | Consolidate the reference persistence implementation, projection rows, cursors, keys and transaction helpers by role. Relocate the symbol migration guide outside Schema. | PostgreSQL latest full-state snapshots, historical reconciliation, exact identity, stable paging, and atomic import and option reference writes. |
| SequenceIdDb | Create the missing extensions file and move eligible helper logic there. Preserve the shared allocator contract. | PostgreSQL concurrent sequence allocation, block size, current-value reads and restart without duplicate IDs. |
| SystemAdminDb | The nine source paths already match. Verify SQL, bindings and support type placement. | PostgreSQL latest state snapshots, session-date reads and concurrent updates. |
| TradeDb | Co-locate `IEnlistedTradeDbWriter` with the write contract. Place context-owned feed-recovery persistence alongside the extensions and shared telemetry with its diagnostics owner. | PostgreSQL trade and broker-account round trips, audit and state rollback, option feed recovery, and broker reconciliation without duplicate fills. |
| TradePlanDb | Co-locate the offset-read session interface with the read contract and the PostgreSQL session implementation with the extensions. | PostgreSQL offset/page ordering and isolation, session disposal, snapshot restore and baseline reconstruction. |
| TradeTickQuoteDb | Co-locate `IStreamingRawFuturesTradeReader` with the read contract. Rename the two CQL catalog files to the common `DbSql` and `SchemaSql` names. Remove the old Parameters subfolder. | ScyllaDB schema order, prepared bind-value ownership and disposal, raw trade/quote precision, UDT round trips, identity/time-index consistency and streaming order. |

## Audit implementation

1. Add a source-layout rule to the central audit. For each of the 13 registered concrete contexts, compare actual paths with the exact allowed paths derived from the concrete class stem. Report missing files, extra files, extra directories and nested Schema content by path. Include non-C# files in the directory check. Keep generated build output outside the source directories.
2. Replace the Configuration-only placement rule and the EventSource two-type check with common ownership rules. Check the context class, interfaces, receiver helpers, support types, mappings, parameter models, runtime catalog and schema catalog by role. Permit secondary capability interfaces inside the prescribed contract files.
3. Require one runtime catalog and one schema catalog. Retain dialect checks independently of filenames. PostgreSQL contexts use PostgreSQL SQL, and the raw tick context uses ScyllaDB CQL. Detect split statement fragments and SQL/CQL embedded in implementation files.
4. Add audit fixtures for a valid layout, missing and extra paths, nested Schema, a misplaced helper or transaction adapter, an extra contract file, a legacy catalog name, a statement outside its catalog and the wrong dialect. Cover EventSource's concrete class stem and verify the rule executes for every registered context.

## Execution and test gates

1. Record the starting revision and working-tree changes. Trace consumers and DI registrations before moving implementations. Run existing focused tests and capture baselines for the EventSource replay/append, Securities reconciliation and raw tick paths.
2. Add the audit first. Align the contexts with file-only changes and existing compliant layouts, then work through MarketData, Portfolio, Securities and Trade. Complete EventSource as a separate slice so its append, audit and transaction paths can be qualified together. Build affected consumers after each slice.
3. Run focused unit tests for typed binding, mapping, disposal, identity and retry semantics. Run existing or extended BDD scenarios through normal entry points for startup/rebuild, audit retries, latest snapshot restore, import reconciliation and broker reconciliation. Use existing test projects and add coverage where a move exposes a gap.
4. Run the full storage convention and integration suites against disposable PostgreSQL and ScyllaDB, plus affected domain unit, BDD and integration suites. Verify both fresh schema creation and additive startup against populated fixtures. Include rollback and replay idempotency tests with real commits, and keep Development data out of the test fixtures.
5. Re-run the focused timings with the same fixtures and inputs. Include stream resolution on both sides of snapshot replay, and compare elapsed time and managed allocations for the affected hot paths. Record test results, any skips, and unresolved findings before handing off the implementation.

## Execution results

- All 13 concrete context directories now have exactly the prescribed nine files. EventSource catalog paths use the concrete context stem. ScyllaDB uses the common catalog paths and retains its native statements. Existing public type names and namespaces are preserved. The unused internal `TradingDaysKey` was removed.
- The expanded audit completed 195 context/rule checks with zero findings. The final focused convention and ScyllaDB run passed 330 tests, including schema reapplication over 6,001 existing raw trades.
- The full Storage integration suite passed 1,116 tests against disposable infrastructure. This included the 1.2-million-event replay, PostgreSQL schema reapplication, and Securities projection reconciliation.
- Nine unit suites passed 4,133 tests. Eight BDD suites passed 603 tests. Focused BrokerAccount, Securities, Reference, Portfolio and Trade integration runs passed 88 tests, including Risk projection/receipt rollback and three complete emulator round trips with repeat Reconcile. Two opt-in live-market Trade tests were skipped.

### Before and after measurements

The Release baseline was frozen before the layout changes, including the preceding EventSource replay fixes already in the working tree. Both builds ran on the same disposable PostgreSQL instance and on the same disposable ScyllaDB instance. The database or keyspace was reset between runs. The order was after, before, before, after. PostgreSQL collected 24 measured samples per build and ScyllaDB collected 40 after warm-up. Allocations are process-wide managed bytes per operation.

| Workload | Median elapsed time, ms | Median managed bytes |
| --- | ---: | ---: |
| PostgreSQL resolve stream and read 24 snapshot rows | 5.97 → 5.61 | 86,552 → 86,360 |
| PostgreSQL resolve and read with cold connections | 52.70 → 53.33 | 416,792 → 416,624 |
| PostgreSQL append 64 audited events | 17.28 → 17.70 | 386,988 → 383,932 |
| PostgreSQL publish 128 option definitions | 137.50 → 148.35 | 569,464 → 569,568 |
| PostgreSQL reconcile 64 Securities contracts | 5.61 → 5.65 | 105,496 → 104,528 |
| PostgreSQL control round trip | 1.02 → 0.96 | 768 → 768 |
| ScyllaDB write a 512-quote segment | 2.87 → 3.12 | 22,448 → 22,448 |
| ScyllaDB read 512 trades | 8.00 → 7.61 | 407,360 → 407,360 |
| ScyllaDB control round trip | 1.59 → 1.71 | 10,304 → 10,304 |

Allocations were within 1% on all measured workloads. Option-definition publication was about 8% slower in this sample. ScyllaDB quote writes were about 9% slower, alongside an 8% increase in its control round trip. The first BenchmarkDotNet runs overlapped integration tests and showed more timing variance, particularly on cold PostgreSQL connections. The table uses the same-instance runs after test completion.

The TRX files, audit report and raw benchmark samples are in `E:/IFM/DbContextSourceLayout/20261112`.
