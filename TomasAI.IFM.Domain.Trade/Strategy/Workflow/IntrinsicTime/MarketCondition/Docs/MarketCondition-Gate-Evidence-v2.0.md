# Market Condition gate evidence v2.0

> **Strategy catalog direction (2026-09-06):** Reusable strategy-family/structure/variant definitions are planned in ConfigurationDb and are downstream TradeSelection concerns. Current MarketCondition remains market-only for the single ITI-triggering Daily, Weekly or Monthly horizon. Historical family hints and family-scoped rules in superseded designs do not return to the assessment path. Recorded gate evidence is unchanged and does not qualify the new catalog. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).


## Assessment-only revision - 2026-09-06

Market Condition now executes only `ExecuteMarketConditionAssessmentCommand` (`Assess`). The earlier `Execute` evaluator, Function state/projector, option-universe adapters, broker-readiness adapters, snapshot cache/coordinator and legacy decision-reference generator have been removed. No trade strategy family participates in market profile resolution or assessment calculation.

Each new workflow must freeze one published market profile for the triggering Daily, Weekly or Monthly timeframe and the exact matching Regime Discovery parameter ID/version. `ES.Standard` is the default profile name, shared across families; its three timeframe rows still require publication before testing live starts. Missing or mismatched profiles fail explicitly. This revision does not create or publish profiles.

`UseMarketConditionAssessment` has been removed. `Enabled` controls automatic workflow starts; disabling it pauses new starts, without enabling an alternative evaluator. Existing assessment completions remain replayable. An old unbound workflow reaching Market Condition fails with `MC.ASSESSMENT.PROFILE_REQUIRED` and must be started again with an assessment profile. Legacy `Tradeable` completions cannot advance workflows.

Historical MessagePack fields, result DTOs, stored configuration and read-only result/history queries remain for deserialization and inspection. They provide no executable legacy path or fallback. Trade Selection owns fund-authorized family suitability; exact construction and broker readiness remain downstream. This change does not upgrade the Strategy view or run the combined five-stage workflow qualification.

## Current assessment-only verification

Verified for the 2026-09-06 assessment-only revision:

| Check | Result |
|---|---|
| Full Trade unit suite | 270 passed |
| Full Trade BDD suite | 23 passed |
| MarketAssessmentQualification verification | 7 passed |
| Isolated NATS assessment reference query and CSV | 1 passed |
| Domain Trade, integration and verification project builds | Passed |
| API build using isolated output (running application left untouched) | Passed |
| `git diff --check` | Passed |

The checks include rejection of old Execute verbs before payload parsing, rejection of missing/mismatched workflow profiles, assessment-only continuation, unchanged market results for different workflow funds, actor lifecycle, and retained calendar evidence. NATS uses a temporary isolated JetStream server at localhost:14222. No live ITI triggers or profile publication are part of this verification.

Evidence logs: `.codex-mc-assessment-only-unit.log`, `-bdd.log`, `-verification.log`, `-nats.log`, and `-api-build.log` in the repository root. The full five-stage runtime/restart tests compile but were not rerun in this revision, respecting the requested code-completion-first sequence.

 The historical 465-test count below predates removal of the legacy evaluator and is not the current test count. Obsolete legacy business tests have been removed; calendar, historical serialization and generic workflow tests are retained or migrated to assessment semantics.


## Historical MC-R00 baseline and implementation inventory

Baseline: `dotnet test TomasAI.IFM.Domain.Trade.UnitTests/TomasAI.IFM.Domain.Trade.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MarketCondition|FullyQualifiedName~RegimeDiscovery"` passed **228 tests** before this migration. Existing DownloadLog, calendar-consumer, storage reliability and documentation changes were present and are preserved.

The existing `StrategyStageResultEnvelope` has `ResultType`, `SchemaVersion`, content type and payload SHA-256; no additional envelope discriminator is needed. Its keys 0–7 remain unchanged. RegimeDiscovery already produces one accepted `TargetHorizon`; no upstream fan-out is needed.

| Seam | Migration |
|---|---|
| Shared Pipeline/MarketCondition/Assessment | New explicit keyed assessment contracts, enums, parameters, snapshot, result and validators |
| ConfigurationDb | Separate assessment table and immutable lifecycle; resolve by market profile/root/horizon |
| Workflow request/view/state and clones | Append a nullable frozen assessment binding; null retains legacy behavior |
| IntrinsicTimeStrategyWorkflowRealtimeActor | Resolve/freeze selected mode before start; dispatch Assess only for assessment workflows |
| MarketConditionFunctionActor | Add typed Assess handling under the existing mailbox, retaining Execute unchanged |
| Assessment capture/calculator | Underlying quote, exchange session, feed health and calendar evidence; no fund, family, option or broker evaluator dependencies |
| Workflow CompleteMarketCondition / TradeSelection handoff | Validate accepted assessment lineage, availability, expiry and restrictions independently of strategy suitability |
| TradeDb / Query actor / clients / observation UI / exports | Separate assessment payloads and profile/horizon queries; retain legacy readers |
| Qualification | Unit, BDD, real NATS/PostgreSQL/Scylla integration, compatibility, fault and rollback checks |

New mode defaults to disabled for deployment; the new consumer and source bindings are qualified by the tests below. Existing published parameters and persisted results are not rewritten. The live IBKR emulator/broker workstream is separate.

## Historical implemented gate record - 2026-09-05

Qualification date: **2026-09-05 America/Toronto** (runtime artifacts extend into September 6 UTC). All ten gates are implemented and qualified in the controlled local topology described below. Production automatic starts remain an explicit deployment setting; test profiles and test market observations are not production market authority.

| Gate | Implementation and exit evidence |
|---|---|
| MC-R00 | File inventory above; 228-test baseline; legacy keys/types and existing one-horizon RegimeDiscovery retained |
| MC-R01 | Exact trigger/workflow/profile/upstream/snapshot/result horizon validation; separate Daily, Weekly and Monthly real workflows; only the selected horizon is configured before each start; restart uses the frozen request and completed state |
| MC-R02 | Distinct Assess command, completed/failed replies, result schema and AssessmentV2 stream; append-only nullable workflow binding; wire round trips, enum/key manifest verification, invalid contracts and conflicting duplicates tested |
| MC-R03 | PostgreSQL market-profile/root/horizon resolution, immutable publish/retire lifecycle and canonical decimal hashing; 10 storage tests; API and integration hosts bind final configuration before resolving workflow options |
| MC-R04 | Production underlying-market capture, contract-specific managed DataBento health/generation fencing, bounded capture, optional Unknown, CME session and existing FMP DownloadLog adapter; production-provider and managed-cache tests; real calendar actor/storage/adapter qualification |
| MC-R05 | Deterministic classification, strict stress thresholds, liquidity, freshness/confidence, expiry, trigger explanation and inherited restrictions; ordered evidence includes source sequences, thresholds and intermediate confidence factors |
| MC-R06 | Existing Function mailbox handles Assess; synchronous Scylla projection, then completed-only PG append, then direct reply; hard deadline and late-worker fencing; workflow acceptance and selector-side frozen-mandate/family-version validation |
| MC-R07 | Separate exact/history tables and typed queries, profile/horizon latest lookup, reference query and CSV, bounded telemetry and orphan diagnostics; Strategy observation form/presenter distinguishes accepted/current/expired/unavailable/unaccepted projection and legacy results |
| MC-R08 | Actual NATS, PG and Scylla with actual RegimeDiscovery and MarketCondition Functions and workflow acceptance; failures, unavailable inputs, optional absence, poor/closed/elevated but valid market, duplicates and no extra handoff; current regression results below |
| MC-R09 | Configured new-mode starts; simultaneous old/new inflight workflows survive host reconstruction; rollback changes future starts only; exact old/new completions replay without capture or dispatch; actual accepted runtime payloads rendered in the observation form for all three horizons; documentation synchronized |

## Historical qualification - before assessment-only removal

The reproducible runner is [Verify-MarketConditionAssessment.ps1](../../../../../../test-support/Verify-MarketConditionAssessment.ps1). It fails on a failed test/build; there are no environment-based silent passes in the new assessment qualification tests.

| Suite | Passing tests |
|---|---:|
| Trade unit: MarketCondition, RegimeDiscovery, IntrinsicTime workflow and assessment selector | 368 |
| MarketCondition and assessment BDD | 8 |
| Managed dataset current-value/health regression | 15 |
| PostgreSQL assessment configuration lifecycle | 10 |
| Real IntrinsicTime workflow runtime, legacy plus assessment | 17 |
| Real DownloadLog command/projector/query to production calendar adapter | 1 |
| Business/decision and assessment verification | 39 |
| Assessment observation presenter | 4 |
| Actual WinForms observation rendering and Close/message-loop checks | 3 |

The 17 runtime tests include seven new assessment cases: one three-horizon scenario, known unavailable inputs, four injected failures (capture, projection, append, timeout), and mixed-mode restart/rollback. The configuration-driven three-horizon scenario and expanded mixed-inflight restart case were also rerun separately after their final changes. These additional executions are not added to the distinct test count.

The final runner completed successfully: **465 tests passed, zero failed, zero skipped**. API Server and UI.Net each built with **zero warnings and zero errors**. Documentation links and `git diff --check` passed. Detailed local output is `.codex-mc-final-qualification.log`; runtime payloads and rendered views are in `.codex-mc-evidence`. These local artifacts are not application data or production market reports.

### What was real, and what was controlled

- Infrastructure: local PostgreSQL, Scylla and Redis; an owned JetStream broker on `127.0.0.1:14222`. Workflow and Function authority used `event-source-test-db`; projections used `trade_test_db`. Unique configuration profile IDs isolate tests from published application profiles.
- Upstream: the actual single-horizon RegimeDiscovery Function calculated its result from typed controlled signal inputs. Workflow accepted that result before dispatching the actual assessment Function. No fabricated upstream terminal reply replaced this handoff.
- Assessment: controlled market observations exercise the real calculator, Function lifecycle, projection, persistence and workflow continuation. A separate production-provider test exercises the actual DataBento/cache/calendar capture implementation. The actual calendar adapter is also exercised against real DownloadLog actors and storage in an owned temporary Scylla keyspace.
- Later stages: a test receiver observes dispatch; it does not replace RegimeDiscovery, MarketCondition, storage or workflow acceptance. The real selector consumer boundary independently validates the accepted assessment and the caller-supplied frozen mandate and filters exact family versions. Implementing every future strategy or a complete TradeSelection actor remains outside this MarketCondition plan's stated boundary.
- UI: tests load exact workflow/assessment bytes returned by the real NATS query path and render them through the production form and presenter under a WinForms message loop. Query responses are replayed at the UI test boundary. All three frames label old results expired, show the matching accepted projection and expose evidence. The remote Windows automation kernel could not initialize; rendering and Close were verified by the native test harness instead.

The tests do not claim a live provider-funded trading session or a broker connection. They do establish new-mode market assessment without options or a broker requirement.

## Runtime behavior and compatibility findings

- Missing or stale required evidence produces a completed **Unavailable** assessment and normal workflow **NoTrade**. Technical source/query, projection and storage failures produce typed failures, not invented market reports.
- Poor liquidity, a closed exchange session, elevated event context, optional stress-data absence and low/conflicting descriptive context are not new MarketCondition suitability vetoes. A valid result reaches the selector unless expired or carrying an authoritative restriction such as NoNewTrade.
- Scylla projection precedes the PG authoritative completion. A failed append can leave an orphan projection. Observation identifies it as unaccepted; no selector handoff occurs. A partially successful two-table projection is likewise not workflow authority.
- Deadline expiry cancels cooperative work and fences later writes/continuation when an uncooperative capture eventually returns. A completed request remains replayable after market expiry; workflow and selector independently reject stale authority. An in-flight storage commit can outlive client cancellation, so reconciliation uses the persisted completion and never assumes a timeout proves no commit.
- Snapshot/parameter and request hashes normalize decimal scale. This fixes hash changes after PostgreSQL JSON round trips. Array copies are defensive; a serializable getter does not recursively invoke MessagePack serialization.
- Managed DataBento health is selected for the reference contract's dataset. An unrelated optional VX dataset cannot make a healthy ES dataset unavailable. Feed-generation changes during capture force a bounded recapture.
- Calendar expiry uses the newest covering attempt per date, including across ALL/US scopes. An older scope does not shorten a newer covering success; a newer failure still invalidates coverage. The original import time is never replaced with the coverage-check time.
- Legacy Execute, result keys/enums, fund/product requirements and published parameters retain their meanings. The assessment path has no family hint, Tradeability, IbkrSession or option-chain requirement.

## Current deployment controls

`AppSettings:IntrinsicTimeStrategyWorkflow:Enabled` controls automatic starts. `MarketConditionAssessmentProfileId` defaults to `ES.Standard`. There is no mode flag or earlier execution path. Publish the matching Regime Discovery and market assessment profile for each intended timeframe before starting qualification. Profile publication, combined workflow testing and further Strategy UI work remain separate from this cleanup.
