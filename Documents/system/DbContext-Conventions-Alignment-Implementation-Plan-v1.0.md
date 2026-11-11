# DB context conventions alignment implementation plan

Date: 2026-11-10

Status: IN PROGRESS. Trading-time freshness and tooltip prerequisites are deployed and verified healthy on November 10 at 6:47 p.m. America/Toronto. Source preservation and baseline qualification precede the 13-context changes. See sections 11–12.

## 1 Purpose and scope

Align all 13 concrete Application.Storage DB contexts with the mandatory [Storage implementation conventions](../../TomasAI.IFM.Application.Storage/Docs/Storage-Implementation-Details.md), the [C# extension conventions](CSharp-Extension-Conventions.md), the [actor implementation conventions](Actor-Implementation-Conventions.md), and the existing [PostgreSQL and ScyllaDB persistence boundaries](PostgreSQL-and-ScyllaDB-Storage-Refactoring-Implementation-Plan-v1.0.md).

The scope comprises ConfigurationDb, EventSourceDb, MarketDataDb, MarketDataServiceDb, PortfolioDb, ReferenceDb, ScheduledTaskDb, SecuritiesDb, SequenceIdDb, SystemAdminDb, TradeDb, TradePlanDb and TradeTickQuoteDb. The shared abstract SchemaDbContext is supporting infrastructure, rather than a fourteenth concrete context; its executor, helper placement and documentation are included where needed by these changes.

This work changes ownership and implementation structure while preserving observable behavior. It does not introduce core backtesting, new trading rules, automatic position opening, manual trade lifecycle testing, a database provider migration or performance tuning unrelated to the alignment.

The audit found no NpgsqlParameter or NpgsqlDbType usage in Application.Storage. Retain provider-neutral C# parameter values. Direct provider execution is not by itself a business-ownership violation: connection resources, transactions, serialization, locks, constraints and conditional writes remain persistence concerns.

## 2 Mandatory operational hold

The expected checkpoints are Market Close at 5:00 p.m. and Market Open after 6:00 p.m., America/Toronto. Passing a clock time does not release the hold.

Before implementation begins, record evidence for the same Development close/open cycle:

1. Market Close has a successful terminal scheduled-run outcome, with required child steps completed and no failed or blocked dependencies. Verify the operational date advances only after the required close work completes.
2. The subsequent Market Open has a successful terminal scheduled-run outcome. Verify feed startup obtains its ValueDate from the server-side provider and agrees with the API session and current contracts.
3. The API, scheduler, Rust Market Data Host and Rust eGui are running in the intended Development configuration. Required startup activities and active indicators are healthy. Options must have current-generation resource/workload evidence; quiet feeds need not produce new option ticks to qualify transport readiness.
4. Observe beyond the applicable startup/recovery budget and inspect fresh errors for this cycle. Historical incidents do not constitute a new failure; an unresolved current failure does.
5. Attach the close/open run IDs, timestamps, ValueDates, health observations and fresh log findings to the execution record before changing application code.

If either task fails, leave this refactor on hold and report the failed step. Do not use a manual rerun, bypassed dependency, forced green status or elapsed time as proof of successful scheduled execution. A recovery action needs its own recorded result. If execution resumes on a later day, use the relevant latest completed close/open pair.

Only this planning document is created before the gate. No baseline benchmark, test run, database mutation, service restart or application implementation is authorized by the planning phase.

## 3 Required end state

- Each concrete context is one non-partial class inheriting ObjectDataRepository of itself and directly exposing its combined context interface. Put additional persistence capabilities on the combined/read/write contracts instead of redundantly declaring them on the class. Preserve disposal ownership and interface compatibility while aligning EventSourceDb and SequenceIdDb.
- Every context supplies Database, DbReader and DbWriter consistently. Public persistence methods implement read/write contracts. Helpers belong to one context extension class using documented C# 14 extension blocks; result-set mappers are static MapTo methods with explicit ordinals.
- Each PostgreSQL context has one runtime SQL catalog and one schema SQL catalog/executor. ScyllaDB CQL remains only for raw trades and quotes in TradeTickQuoteDb. Remove verified obsolete non-tick CQL paths and compatibility catalogs without removing historical business records.
- Consolidate SetParameters binding models into the relevant DbParameters catalog as internal readonly record structs implementing IBindValue, with declared order, type, null and precision semantics. Keep prepared Scylla binding lifetime/ownership adapters where technically required.
- Public query DTOs belong to the appropriate shared contracts project. Internal provider/mapper support types may remain internal to Storage. Replace exposed query tuples with shared read models where required.
- Domain callers own request validation, business decisions, event selection, command coordination, import/rebuild sequencing and application cache policy. Moving those responsibilities to a different Storage helper does not satisfy the boundary.
- Storage retains database integrity checks, immutable identity validation, row locks, uniqueness, compare-and-set conditions, optimistic concurrency, receipts, checkpoint writes, technical batching and transaction resources. Preserve error codes and externally observed outcomes.
- Domain-owned event selection and required projections commit through the existing enlisted transaction mechanism. No actor, provider, broker or network reference-validation call runs while holding a database transaction.
- Queries consume projected read models. Moving a query out of Storage must not introduce direct reads of command aggregate state.
- Preserve existing stream identities, event names, serialized message layouts, money/strike precision, timestamps, cancellation and timeout ownership. Business dates and new message origins use the injected application TimeProvider; elapsed database and transport deadlines remain physical.

## 4 Execution phases

| Phase | Work | Exit condition |
| --- | --- | --- |
| 0 | Satisfy and record the operational hold gate. | Successful close/open pair and healthy Development evidence recorded. |
| 1 | Freeze the source revision and current uncommitted changes; inventory contracts, consumers, SQL, bindings, schemas, helper classes and public DTOs. Establish isolated fixtures and capture baseline correctness and timings before editing each affected path. | Traceable inventory, baseline results and transaction-boundary map for all 13 contexts. |
| 2 | Strengthen convention coverage; align class/interface shape, capability properties, SQL and parameter catalogs, helper placement, mappings, documentation and fluent formatting. Start with SequenceIdDb, SystemAdminDb and ScheduledTaskDb, then the remaining contexts. | Mechanical checks pass without changed business behavior. |
| 3 | Move ConfigurationDb and ReferenceDb publication/baseline policy, MarketDataDb orchestration, MarketDataServiceDb recovery/schema coordination and SecuritiesDb application policy to their owning layers. | Owning domain/application flows exercise typed persistence only; atomicity and identity regressions pass. |
| 4 | Align PortfolioDb, TradeDb and TradePlanDb financial/projection/cache responsibilities; align EventSourceDb public contracts and audit/replay coordination without changing persistence guarantees. | Financial, event-source, snapshot, concurrency and restart tests pass. |
| 5 | Complete all remaining context and cross-context regressions; collect paired timings and investigate material regressions. | All required unit, BDD, integration and convention checks pass; timing evidence covers every context. |
| 6 | Deploy a complete consistent Development package, restart through the established launcher, and verify health, reads, snapshots, scheduler and persisted state. | Final report and live acceptance evidence complete; manual lifecycle tests remain paused. |

Complete one bounded slice at a time and run its affected checks before proceeding. Preserve unrelated workspace changes. Do not run competing .NET builds against shared outputs. Keep build, test and benchmark artifacts on E: through the existing artifact configuration.

## 5 Context implementation and verification matrix

The unit, BDD and integration entries below are required work, not results already obtained.

| Context | Required implementation | Unit and BDD coverage | Actual database integration coverage |
| --- | --- | --- | --- |
| SecuritiesDb | Consolidate the 60 binding files into SecuritiesDbParameters; move inline reference SQL into SecuritiesDbSql and helpers into the approved context extension surface. Retire non-tick CQL fallback/schema catalogs after verifying callers. Separate application validation, rollover/rebuild decisions and cache policy from persistence. Preserve immutable IFM/provider bindings and expiry-scoped provider reuse rules. | Test binding order/types and all shared ID parsing. Given colliding short names on different underlyings, retain the saved identity and resolve the other deterministically. Given an expired reused provider ID, require authoritative later-definition evidence; reject unproven reuse and same-lifecycle changes. | PostgreSQL stage/publish/retry, original binding unchanged, conflicting identity rollback, exact version/digest checks, atomic calendar publication, failed refresh preserving the previous generation, and stable IDs across restart. No real Databento acquisition required by fixtures. |
| MarketDataDb | Remove non-tick CQL remnants and inline statements. Move request/composition validation, historical diagnostics reconstruction, import/reconciliation sequencing and cross-context routing policy to the owning MarketData/Feed/Analytics services. Keep typed observations, receipts, checkpoints and accepted projection writes in Storage. | Test caller validation and accepted DTO mapping. Given an import with gaps or an interrupted batch, the owning workflow chooses resume/repair and produces the same diagnostics. Preserve independent timeframe readiness and indicator algorithms. | PostgreSQL historical import reconciliation, checkpoint resume, duplicate batches, terminal diagnostics projections, decimal/UTC round trips and rollback. Verify raw trades/quotes use TradeTickQuoteDb while bars, indicators, EOD and derived evidence stay PostgreSQL. |
| ConfigurationDb | Move graph/capability/reference validation to domain command preparation. Move configuration baseline event creation and rebuild sequencing outside Storage. Persist accepted definitions, hashes, lifecycle facts and baseline rows through typed operations and the caller's transaction. | Given invalid capability/product/policy references, reject before persistence. Given an accepted publication, preserve validation results and lifecycle/error semantics. Test injected clock timestamps and baseline orchestration. | PostgreSQL immutable draft/version writes, publication sealing, concurrent transitions, hash conflicts and event/projection rollback. Rebuild an isolated target from accepted baseline/events and compare rows, lifecycle and sequence positions. |
| ReferenceDb | Move instrument-definition publication event creation, duplicate/publication decisions and rebuild sequencing to the owning MarketData domain/application flow; keep ReferenceDb as the catalog persistence adapter. Consolidate SQL and helpers; replace public tuple results with shared DTOs and move caller validation. | Given a repeated snapshot identity, return the existing accepted outcome; reject changed evidence under the same identity. Test exact definition mapping, product identities and caller clock origins. | PostgreSQL publication event and pointer commit together, rollback leaves the old pointer active, concurrent publishers cannot regress the catalog, and retry/restart produces no duplicate source event. Preserve staged selection completeness and read paging. |
| PortfolioDb | Move Fund-version disabling decisions and projection derivation to the owning Portfolio domain/projector. Persist the resulting accepted changes atomically under existing version/identity guards. Consolidate inline SQL and align bindings/mappers. | Given a newer/older Fund version, the domain produces the same permitted enabled/disabled outcomes. Test monetary precision, stale commands, insufficient capacity and idempotent settlement without changing financial rules. | PostgreSQL event/financial/projection atomicity, concurrent version publication, stale writes, receipts, rollback and restart. Verify Fund balances, reservations and settlement remain exact with no duplicate financial operations. |
| TradeDb | Remove redundant direct writer-interface declarations while preserving the inherited capability. Consolidate inline SQL, standalone recovery helpers and fluent formatting. Separate lifecycle/projection decisions from accepted writes; preserve immutable order terms and reconciliation semantics. | Given rejection, partial fill, cancel, limit change or late fill, preserve the established Trade State/Order Status outcomes and duplicate prevention. Verify source facts supplied to persistence and unchanged error codes. | PostgreSQL order/execution/fill/position projections, EOD history finalization, optimistic conflicts, atomic event append, rollback, retry and restart/Reconcile. Use isolated emulator scenarios for command workflows; do not resume manual UI lifecycle testing. |
| MarketDataServiceDb | Move caller validation and recovery/schema coordination outside the context. Move recovery-log SQL and binding helpers into the context catalogs. Initialize schema through the schema executor, not ordinary reads/writes. Preserve atomic VX pair updates, durable intents and incident uniqueness. | Given duplicate or conflicting incident facts, preserve the accepted/rejected outcome. Given a quiet feed, heartbeats and resource proofs can establish readiness without a new option tick. Test explicit dependency/error reporting. | PostgreSQL complete VX-pair replacement and rollback, concurrent revision conflicts, durable subscription intents, recovery fact/checkpoint idempotency and restart. Query paths must perform no DDL. |
| EventSourceDb | Move public checkpoint query DTOs to shared event-source contracts and align the public/disposal surface. Consolidate SQL and formatting. Separate command-audit/deduplication/replay coordination from technical append, checkpoint and transaction primitives; retain internal persistence batching. | Test identity/alias cache behavior, serialization, ordered binding, conflict outcomes and domain-owned execution decisions. Given a newer snapshot, reconstruct the same aggregate and monitoring limits; a failed append must not advance accepted state. | PostgreSQL latest creation/full-state snapshot selection, single-query tail loading with stream resolution, aliases, absent snapshot fallback, dense histories, stream versions, checkpoint concurrency, command receipts, rollback, cancellation and restart. Preserve full state rather than selecting an incomplete checkpoint solely for speed. |
| TradePlanDb | Move the chart cache and its TTL/admission policy to the owning query/application service. Consolidate inline SQL, bindings and formatting; retain typed paged reads and accepted snapshot writes. Preserve the existing disposable monitoring-snapshot failure policy. | Given a selected position/revision, return the same chart/history and cache scope. Verify TTL, revision invalidation, concurrent reads and domain-owned failure behavior without changing sampling or P&L calculations. | PostgreSQL complete and paged plan/history reads, caller-owned snapshot transaction, cancellation, concurrent revisions and rollback. Compare chart points and page boundaries; verify a failed disposable update does not introduce a replay backlog. |
| ScheduledTaskDb | Add DbReader/DbWriter properties. Move argument validation and Market Close run interpretation to domain handlers/projectors, retaining an authoritative projected completed-date query. Preserve task/catalog/run snapshots and dispatch dependencies. | Given a failed or partially completed close, do not advance the completed close date. Given a successful close, expose the exact completed date. Test duplicate dispatch, dependencies, pending/running/terminal states and server ValueDate use. | PostgreSQL latest full-state task/catalog/run snapshots plus tail, correct restart state, completed close/open date queries, dispatch/outbox idempotency, failed-step reporting and no duplicate run. These tests must cover the operational gate's real query path. |
| SystemAdminDb | Move argument validation to command/query producers. Retain typed backup/recovery facts, receipt conflicts, atomic projection/checkpoint writes and ordinal mappings; align documentation and contract coverage. | Given an invalid request, reject before the context call. Given repeated backup/recovery facts, preserve receipt and statistics semantics. Test nullable statistics and status transitions. | PostgreSQL backup/recovery lifecycle projections, receipt/digest conflicts, checkpoint rollback, latest state after restart and read-only administrative queries. Do not initiate real backup deletion or database restore. |
| TradeTickQuoteDb | Move BootstrapTradeRow to the owning shared Feed/MarketData contracts as a read model and update all consumers. Move request validation to callers and use MapTo mappers. Retain raw-only CQL catalogs, prepared binding lifetime ownership, exact provider timestamps and partition ordering. | Test price/size/sequence/date precision, nulls, window bounds, mapper ordinals and single-use binding ownership. Given a gap or late trade, preserve the existing ingestion/repair behavior and source identity. | Isolated ScyllaDB raw trade/quote writes and reads, partition/date boundaries, bounded windows, ordering, idempotent writes and cancellation. Verify typed bindings against prepared metadata and no derived/application tables in ScyllaDB. |
| SequenceIdDb | Align the concrete context with a combined read/write capability contract and Database/DbReader/DbWriter properties. Preserve the existing shared sequence-service API through a suitable interface/adapter boundary; avoid a Shared-to-Application dependency. Consolidate parameter bindings, helpers and XML documentation. | Test named sequence binding and allocation semantics. Given concurrent/retried requests, preserve uniqueness, monotonic allocation and configured block size; gaps remain permitted. | PostgreSQL real concurrent allocations, allocation-size/current-value reads, process restart and shared allocator compatibility. Never reset or consume Development sequences for qualification. |

## 6 Test organization and convention coverage

During implementation, add focused Application.Storage.UnitTests and Application.Storage.BDDTests projects if no suitable equivalent exists. Use the repository's xUnit/Given-When-Then patterns rather than introducing a new BDD framework. Keep database fixtures in the shared integration-testing infrastructure and real provider checks in Application.Storage.IntegrationTests. Do not make test projects depend on one another for reusable fixtures.

Add or update the existing owning-domain unit, BDD and integration projects when responsibility moves into Reference, MarketData, Feed, Analytics, Securities, Portfolio, Trade, SystemAdmin or Application workflows. Each context's matrix scenarios require all three levels: fast unit checks, observable BDD behavior, and actual provider execution. BDD must exercise public commands/queries or the real persistence adapter boundary rather than restating private helper logic.

Extend convention checks to cover every context and its supporting folders, including SequenceIdDb and shared schema infrastructure where applicable:

- Single context class, correct generic base and combined interface, capability properties and contract-only public persistence methods.
- C# 14 helper blocks, one helper class, static MapTo mappers, parameter/return XML documentation and one physical line per fluent invocation.
- SQL/DDL/parameter catalog ownership, no non-tick CQL fallback and no schema-generation suffix on physical table names.
- Public query DTO ownership and no public Storage query tuples; allow internal provider-support types explicitly.
- Caller-side argument validation, including indirect ValidateRequiredArgument/ValidateSnapshot calls that simple searches for throw statements miss.
- No Storage event construction, actor/provider calls inside transactions, domain lifecycle decisions or application cache policy hidden in standalone helpers.
- No PostgreSQL-specific parameter objects crossing context contracts. Verify actual bound order, typed nulls, enum values, exact decimals, dates, UTC timestamps and binary payloads.

Use source/semantic architecture checks and targeted call-boundary tests together. Avoid brittle fixed file counts that pass while a new standalone helper bypasses the intended ownership rule. Retain independent functional and integration assertions; passing a formatting audit does not establish runtime correctness.

All provider tests use disposable PostgreSQL databases and isolated Scylla keyspaces. Guard endpoint/database/keyspace identity before any fixture creates, clears or drops data. No paid Databento requests, Development sequence consumption or Development fixture resets are permitted.

## 7 Before and after timing protocol

Capture the before arm before changing each affected path. Use immutable source copies/worktrees that preserve the actual uncommitted baseline; do not reset or overwrite the user's workspace. Record source and binary hashes, SDK/runtime/provider versions, machine resources, build settings, schema/index definitions, fixture sizes and cache state. Baseline and candidate must use identical economic inputs and equivalent full-state correctness assertions.

Use the existing Application.Storage.Benchmarks, Application.Storage.Postgres.Benchmarks, Application.Storage.Scylla.Benchmarks and owning-domain benchmark projects as appropriate. Add context-specific BenchmarkDotNet cases during implementation; do not use unrelated database-version benchmarks as evidence for this refactor. No benchmark runs before the operational gate.

For each context, measure both the direct persistence operation and the public domain/application path affected by an ownership move. Include contract lookup, validation in its new owner, connection/transaction setup, serialization, commit and required projection work where the real caller performs them. Snapshot comparisons include stream resolution on both paths. Do not obtain an apparent gain by timing a smaller result, incomplete snapshot, pre-opened connection on only one arm, or omitted validation.

| Context | Required paired workload |
| --- | --- |
| SecuritiesDb | Resolve legacy/qualified/reused identities; stage/publish exact versions; read and atomically publish an option calendar. |
| MarketDataDb | Read/write representative indicator and bar observations; import a fixed history batch; reconcile/resume the same interrupted attempt. |
| ConfigurationDb | Validate and persist a representative catalog draft/publication; capture/materialize an isolated baseline of fixed size. |
| ReferenceDb | Insert definition batches of 1, 32 and 128 rows; page selections; publish/retry the same catalog snapshot. |
| PortfolioDb | Read Fund/current revision; apply a representative version projection and financial commit, including contention on the same Fund. |
| TradeDb | Read order/position history; commit lifecycle/fill projections; finalize EOD history; execute duplicate reconciliation. |
| MarketDataServiceDb | Read/write durable intents and recovery facts; replace an atomic VX pair with uncontended and conflicting revisions. |
| EventSourceDb | Append batches; resolve streams and load sparse/dense history from latest complete snapshots; cached/cold identity lookup and alias refresh. |
| TradePlanDb | Read the first and later history pages, chart points and fixed-revision cache hits/misses; write a plan snapshot. |
| ScheduledTaskDb | Read latest task/catalog/run state and completed close date; dispatch/update a run with duplicate requests. |
| SystemAdminDb | Read administrative history; commit a backup/recovery projection, receipt and checkpoint. |
| TradeTickQuoteDb | Bind/write fixed trade and quote batches; stream identical raw windows across date/partition boundaries. |
| SequenceIdDb | Single and block allocation plus current/allocation-size reads at single-client and concurrent load. |

Run identical BenchmarkDotNet configurations for before/after CPU/allocation cases with warmup and multiple measured iterations. For database scenarios, collect at least five alternating before/after pairs after warmup, with separate cold and warm-cache cases and concurrency levels 1, 8 and the supported representative workload. Run compilation and benchmarks sequentially, without competing benchmark hosts or Development load on the same database fixtures. Retain full durability settings and the same indexes and dataset sizes.

Report mean and median latency, p95/p99 for sustained harness workloads with enough samples, throughput, allocated bytes/op, Gen0/1/2 collections, CPU and working set. Capture lock/contention and thread-pool observations for concurrent runs; use appropriate runtime diagnostics and PostgreSQL wait/lock/statement metrics rather than inferring lock time from total latency. Include round-trip counts, rows read/written, commit latency and Scylla timeout/retry counts where relevant. Do not infer a reliable p99 from a handful of BenchmarkDotNet aggregate samples.

Report absolute values, after/before ratios and variability. No improvement is assumed for structural cleanup. Investigate repeatable latency, allocation, throughput or contention regressions; a median regression above 10% triggers focused analysis with equivalent work, not automatic weakening of checks or unconditional benchmark rejection. Record a justified accepted tradeoff explicitly, or leave performance qualification open.

The final timing table must contain one row per workload with context, input size, cache/concurrency state, before, after, ratio, allocation/GC, lock/wait data and correctness result. Values remain pending until measurements are executed; estimates are not substitutes.

## 8 Data and Development safeguards

- Preserve current Development data, event streams, immutable reference bindings, saved Fund configurations, order/fill identities, financial balances and published catalog pointers. Source catalog cleanup does not authorize database recreation or deletion.
- If a genuine physical schema replacement requires destructive recreation under the existing naming convention, treat the required data-preservation/cutover design as a blocker. Do not silently introduce dual-table fallback or drop Development data.
- Keep integrity predicates and transaction boundaries intact while relocating decisions. Enlisted writers must not start, commit or dispose an independent transaction. External provider/reference checks finish before acquiring the persistence transaction.
- Retain latest creation events when they reconstruct current state; use the existing verified full-state snapshot where creation occurs once. Preserve monitoring limits, wider-history requests, tail ordering and stream-head semantics.
- Preserve application clock origins and provider event timestamps independently. No new direct system-clock business timestamps, wall-clock replay decisions or replacement of source timestamps are introduced.
- Preserve independent startup dependencies and indicator timeframes, heartbeat-based quiet-feed readiness, current Options recovery budgets and the immutable option-identity safeguards verified before this plan.
- Keep automatic openings disabled and manual UI lifecycle tests paused. Automated financial/emulator scenarios run in isolated test environments and must not submit Development or IBKR orders.
- Deploy complete consistent managed/native packages with full Development debugging symbols. Use owned-process shutdown, verify deployment hashes, restart the configured API/Rust UI/Rust Market Data/scheduler stack, and leave isolated qualification workers untouched.
- Recovery from a failed slice uses the recorded compatible package/data state. Do not delete accepted events or bindings to make a test or health check pass.

## 9 Completion criteria

Implementation is complete only when:

1. Every context's inventory finding has a code change or a documented, convention-consistent technical classification with supporting tests; moving a violation to another Storage file is not closure.
2. Unit, BDD, integration and source/semantic convention checks pass for all 13 contexts and affected domain owners. Required cases do not disappear behind new skips; optional sustained/provider qualification is identified separately.
3. PostgreSQL latest full-state snapshot tests, historical import reconciliation, raw Scylla boundaries, immutable option identities, financial rollback/concurrency, schedule completion dates and restart idempotency pass.
4. Paired before/after timing evidence covers all 13 contexts through equivalent public paths, with unresolved regressions explicitly reported.
5. Development is restored with a matching server ValueDate/current contract, warm indicators, healthy Futures/Options resources, working Rust eGui and scheduler, and no new unresolved errors attributable to the refactor. Observe across startup/recovery budgets and several refreshes without requiring new option ticks off-hours.
6. The final report lists changes, affected projects, test counts/skips, timing tables, deployment identity, operational verification and any remaining blockers. Manual lifecycle testing and automatic openings remain in their agreed paused/disabled states.

No implementation has begun under this plan. The first execution step is to verify the operational hold gate in section 2.

## 10 Operational gate verification on November 10

All times in this section are America/Toronto. Verification used read-only PostgreSQL queries, persisted MessagePack run/event records, the API health endpoint and the existing read-only native workload probe. No scheduled tasks were manually rerun.

| Check | Verified result |
| --- | --- |
| Scheduled Market Close | Succeeded, exit code 0. Scheduled wrapper run b165065b-b16c-833c-580b-ddcaa5cd2d86 started at 5:01:00 p.m. and finished at 5:01:02.750 p.m. |
| API-owned close execution | Run 95924f27-6c70-46db-9caf-ae8b1db2a1ac reached MarketClosed, exit code 0, at 5:01:02.567 p.m. for ValueDate 2026-11-10. The backend receipt's Manual flag is set by the API implementation; the scheduled wrapper itself has Manual=false. |
| Required close stages | Persisted source events confirm FeedsStopped, then PositionsFinalized with all 5 positions finalized and EOD source commitments confirmed, then successful completion. The completed-EOD projection identifies the same run and November 10 date. |
| Scheduled Market Open | Succeeded, exit code 0. Run 59764195-5eda-f7e3-4b78-f8fdf0fe7bf3 started at 6:00:00.195 p.m. and finished at 6:00:11.464 p.m. Its output confirms the feed-start completion for ValueDate 2026-11-11. |
| Market data session | Both futures datasets and Options report Healthy for ValueDate 2026-11-11. |
| Options workload | Current-generation native resource proofs complete; ReplacementPending=false; all 18 of 18 scopes ready. New option-tick arrival was not required for this readiness check. |
| Application processes | API, Server Manager, scheduler, Rust Market Data Host and Rust eGui remain in the existing owned Development session. No deployment or restart was performed for this verification. |
| Post-open indicator gate | FAILED. At 6:05:24 p.m. the API and indicator health endpoint report Unhealthy, indicator state Failed, with 75 of 76 checks ready. The latest completed audit was at 6:05:12 p.m. |

The unresolved check is ES20261218 Daily RegimeDiscovery inputs. Its required FifteenMinutes Rsi14, Rsi14Slope, Adx14, PlusDi14, MinusDi14, MacdHistogram and Atr14 inputs report Stale. Weekly and Monthly Regime Discovery health checks are ready. This is a required-input readiness failure, rather than an accepted strategy NoTrade decision.

The persisted initialization incident began at 6:00:02.166 p.m.; its five-minute deadline was 6:05:02.166 p.m. After that deadline, Qualified remained false and Notified became true. The failure was present before any work on the DB context refactor.

Outcome: the scheduled-run conditions passed, but the additional healthy post-open condition in section 2 did not. Keep all 13 context changes, baseline/after timings, builds, unit/BDD/integration execution and deployment on hold. Resolve and verify this indicator readiness blocker before releasing the gate; do not weaken freshness checks, mark the input optional or force health green to proceed.

Only this plan's verification/status record was updated. No application code, database data, schema, service state or trading configuration was changed.


## 11 Trading-time prerequisite repair

The November 10 post-open failure was caused by counting the closed 17:00–18:00 exchange interval against the FifteenMinutes observation freshness limit. The existing producer initialization auditor already uses completed exchange-session bars. Regime Discovery instead subtracted the original source timestamp from capture time. It recovered naturally after the first new 15-minute bar at 18:15; that recovery preceded deployment of the repair and is not deployment evidence.

The user clarified that market observation aging must count trading hours continuously across scheduled closures. A shared `MarketTradingTime` implementation uses the injected exchange calendar to count open-session time and calculate forward/backward trading-time boundaries. It retains original source timestamps, UTC future checks and configured age limits. It skips daily closures, weekends and configured holidays/early closes using timezone-aware session boundaries.

| Consumer | Required treatment |
| --- | --- |
| Regime Discovery, all metric/timeframe requirements | Trading-time age and freshness factor, including Daily baseline and ITI observations. |
| Market Condition market observations | Trading-time age and matching expiry for ReferenceQuote, LastTrade, NormalizedMovement and VolatilityChange; retain the completed-minute ATR publication grace. |
| Market Condition VX comparison window | Subtract five trading minutes for the target and comparison window instead of walking into the closed hour. |
| Option-volatility latest observations | Matching trading-time rules for the in-memory repository and PostgreSQL adapter. Preserve publication/source times and append-only identities. |
| FuturesTradeSignal legacy intrinsic-time checks | Convert the recorded local intrinsic timestamp through the injected clock timezone and measure trading-time age. |
| Indicator health and stored-session initialization | Already use exchange-calendar completed-bar windows. Retain those checks. |
| Command/result deadlines, lease TTLs, feed health, processing/recovery watchdogs, receipt latency and financial authority validity | Operational elapsed time. A closed exchange does not suspend a hung request, extend an accepted financial authorization or manufacture a new provider receipt. |
| Executable order/pricing evidence and native quote qualification | Audit separately from bar freshness: explicit quote/receipt/context expiries and current generation/session identity remain mandatory. Do not revive a prior-generation quote or extend a financial validity timestamp as a side effect of the indicator repair. |

Regression qualification includes the 18:00 reopening interval, continued missing bars after reopening, weekends across DST, configured holidays/early closes, positive/negative trading-time movement, source timestamp preservation, Market Condition ATR expiry, and actual isolated PostgreSQL volatility reads.

The DB context implementation remains pending until the consistent candidate package is deployed and Development acceptance is recorded. Before/after timing values are still pending and must not be inferred from the correctness-test durations.


## 12 Prerequisite deployment acceptance and baseline release

On November 10 at approximately 18:44–18:46 America/Toronto, the existing owned Development stack was stopped and consistent Debug API/managed Rust Host packages plus the Development-profile Rust UI were installed. Candidate and installed file hashes were verified. The deployment preserved live History, Logs and data directories; recovery copies contain candidate-matching binaries/resources/configuration only. The native DLL/PDB retained the existing Development profile. Rust eGui remains version 0.1.0 with full source debugging.

At 18:47 the API reports Healthy and all 76 active indicator checks are ready (Warm, zero failed). Futures GLBX.MDP3, Futures XCBF.PITCH and Options GLBX.MDP3 report Healthy for ValueDate 2026-11-11. Native Options resource proofs are complete, all 18 scopes are ready and ReplacementPending=false. The API, Server Manager, scheduler, Rust Host and Rust eGui are running in the owned Development session. The running UI binary matches the verified candidate. This establishes successful post-repair startup, while the explicit reopening regression tests establish the previously failing 18:00–18:15 behavior.

Verification passed: Analytics unit 1,316; Analytics BDD 485; Trade unit 1,751; PostgreSQL volatility integration 2; Rust strategy presentation/workspace/tooltip 13. The Trade suite initially exposed a pre-existing compatibility vector from before the appended Fund BaseContracts field. The updated test verifies the current shape, checks the original pre-append MessagePack and JSON vectors exactly, and deserializes both old formats successfully.

The additional UI request is included: each of the five pipeline operators has a short user-facing decision/reason tooltip. Valid NoTrade outcomes that retain option-chain failure evidence remain NoTrade. Complete technical evidence stays in Details. Compact grid responses retain their existing MessagePack layout and bounded reason arrays; they do not eagerly load full workflow details.

The operational hold gate is released for isolated baseline qualification and the planned context refactor. The earlier section 10 remains the historical record of the failed gate, rather than the current status. No DB-context performance improvement or before/after measurement is claimed yet. Manual lifecycle testing remains paused; no orders were submitted for this verification.


## 13 Context execution journal

The expanded audit, per-context changes, checks and measured results are recorded in [the execution report](DbContext-Conventions-Alignment-Execution-Report-20261110.md). SequenceIdDb, SystemAdminDb and ScheduledTaskDb passed their targeted correctness and timing checks. Securities has five valid timing pairs for its definition paths, with other qualification still open. The execution report distinguishes completed checks from unqualified changes and pending Development deployment.

The full dirty source baseline was captured before context implementation. Its production source is used for the before arm even where measurement execution follows candidate edits. Only the identical benchmark driver is overlaid, and production context hashes are verified. This preserves an actual before comparison rather than reconstructing it from the candidate.

### Later validation checkpoints

- All 13 contexts are covered by 14 source rule groups each, with shared schema, repository ownership and audit self-tests in addition.
- The focused snapshot, configuration, and convention rerun passed 291 of 296 cases. Four validation rule groups remained: EventSource, MarketData, Securities and Trade. The fifth failure was an ordinal test mapping missing for the typed maintenance stream binder; that mapping has been added.
- The broader Storage run reached 719 passes before the runner's per-test deadline aborted the dense 1.2-million-event replay timing case. That case is tracked separately with a longer deadline; the failed or unfinished run is not used as performance evidence.
- Benchmark preflights for EventSource, Configuration, Reference, Trade and TradeTickQuote completed in both versions. Repeated qualification and the corrected MarketData preflight are still required. The execution report contains workload-by-workload measurements and allocation counts.
- The Development readiness endpoints for both API and Rust Market Data Host returned HTTP 200. This was a read-only check of the already deployed version; the current database context candidate has not been deployed.
