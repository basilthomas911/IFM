# Market Condition Implementation Plan v2.0

> **Strategy catalog direction (2026-09-06):** Reusable strategy-family/structure/variant definitions are planned in ConfigurationDb and are downstream TradeSelection concerns. Current MarketCondition remains market-only for the single ITI-triggering Daily, Weekly or Monthly horizon. Historical family hints and family-scoped rules in superseded designs do not return to the assessment path. Recorded gate evidence is unchanged and does not qualify the new catalog. TradeSelection implementation is on hold. See [ConfigurationDb strategy catalog design](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

| Item | Value |
|---|---|
| Status | Assessment-only code complete; current tests in Gate Evidence v2.0; combined pipeline qualification deferred |
| Revised | 2026-09-06 |
| Source design | [High-Level Design v0.4](MarketCondition-High-Level-Design-v0.4.md) |
| Authoritative specification | [Specification v2.0](MarketCondition-Specification-v2.0.md) |
| Historical implementation record | [Plan v1.0, MC-00 through MC-22](MarketCondition-Implementation-Plan-v1.0.md) |
| Target | One assessment for the ITI trigger's timeframe: Daily, Weekly, or Monthly; no strategy-family selection |
| Change in this revision | Remove earlier evaluator and mode switch; preserve market-only assessment and historical reads |


## Assessment-only revision - 2026-09-06

Market Condition now executes only `ExecuteMarketConditionAssessmentCommand` (`Assess`). The earlier `Execute` evaluator, Function state/projector, option-universe adapters, broker-readiness adapters, snapshot cache/coordinator and legacy decision-reference generator have been removed. No trade strategy family participates in market profile resolution or assessment calculation.

Each new workflow must freeze one published market profile for the triggering Daily, Weekly or Monthly timeframe and the exact matching Regime Discovery parameter ID/version. `ES.Standard` is the default profile name, shared across families; its three timeframe rows still require publication before testing live starts. Missing or mismatched profiles fail explicitly. This revision does not create or publish profiles.

`UseMarketConditionAssessment` has been removed. `Enabled` controls automatic workflow starts; disabling it pauses new starts, without enabling an alternative evaluator. Existing assessment completions remain replayable. An old unbound workflow reaching Market Condition fails with `MC.ASSESSMENT.PROFILE_REQUIRED` and must be started again with an assessment profile. Legacy `Tradeable` completions cannot advance workflows.

Historical MessagePack fields, result DTOs, stored configuration and read-only result/history queries remain for deserialization and inspection. They provide no executable legacy path or fallback. Trade Selection owns fund-authorized family suitability; exact construction and broker readiness remain downstream. This change does not upgrade the Strategy view or run the combined five-stage workflow qualification.


## 1. Objective

Replace the early fund/product opportunity decision with one market-only assessment for the ITI signal's timeframe. Daily, Weekly, and Monthly are supported alternatives, evaluated by separate workflow invocations. MarketCondition reports what the market is doing now for the selected timeframe, whether it can be assessed reliably, and the evidence behind that assessment. Trade Selector owns family/strategy eligibility and suitability.

Preserve the existing completed-only FunctionActor, direct Core NATS request/reply, deterministic snapshots, PostgreSQL authoritative state, Scylla completed projections, deadline fencing, and exactly-once workflow acceptance.

Preserve the existing single-horizon RegimeDiscovery result and trigger-driven workflow routing. Do not add multi-horizon production, move regime calculations into MarketCondition, or retain family hints under another name.

**Timeframe correction:** The previous draft's upstream bundle, three-result collection, and pairwise assessment work are removed. MC-R01 now verifies the existing matching-timeframe handoff; it does not expand RegimeDiscovery.

## 2. Pre-migration baseline and compatibility

This inventory identifies existing implementation seams; it is not new qualification evidence.

| Existing implementation | Required migration |
|---|---|
| RegimeDiscoveryResult has one TargetHorizon | Retain it; verify agreement with the original ITI trigger throughout the pipeline |
| MarketConditionResult schema V2 has one TargetHorizon and OutputHints at key 34 | Introduce a distinct assessment result type; retain old readers |
| Daily/Futures, Weekly/VerticalSpread, Monthly/IronCondor hints | Remove entirely from new mode |
| ES futures and option quality jointly gate tradeability | New mode uses underlying-market observations; product/family suitability moves downstream |
| Parameter resolution includes FundId and TargetHorizon | Add market-profile/root/TargetHorizon resolution; remove fund-based analysis selection and freeze one matching profile |
| Tradeable/NotTradeable controls continuation | Use target data availability/expiry and inherited authoritative restrictions |
| IbkrSession plus UnavailableMarketConditionBrokerReadiness | Remove from new assessment dependencies; readiness belongs at Order Execution |
| Completed-only Function state and Scylla projection | Reuse guarantees with explicit new payload/stream identity |
| Existing decision-reference rows describe decisions and family hints | Add a single-assessment mode/export contract with separate examples for each supported timeframe |

The existing MC-00 through MC-22 pass counts remain historical. None closes a gate below, and the old “core qualified” status must not be used to enable the new mode.

## 3. Execution rules

### Verified DownloadLog dependency and consumer bridge

The MarketData DownloadLog command/query actors, Scylla table, durable projector/recovery, and API clients are implemented. The user's September 5, 21:40 America/Toronto startup used value date September 4 and persisted **104 economic-calendar records in 318 ms** and **one Treasury curve in 229 ms**. Both log projectors completed with zero retries. This is provider-import evidence, not proof that a new MarketCondition assessment completed. See [DownloadLog implementation and live qualification](../../../../../../TomasAI.IFM.Domain.MarketData/Docs/Domain-Actor-Implementation-Details.md) for the authoritative record.

Consumer work implemented before the broader migration:

- `MarketConditionCalendarCoverage` queries the existing DownloadLog query actor for the latest `EconomicCalendar/FMP/ALL` and `/US` attempts on every UTC date touched by the event window. Reads are bounded to at most six queries; no historical-success fallback, source refresh, polling, or added timeframe evaluation occurs.
- `MarketConditionEventRiskAdapter` requires confirmed, fresh coverage before querying event rows. Missing/failed/stale coverage is explicitly unavailable; confirmed empty imports may produce Clear. Query failure/corruption remains a technical capture failure.
- Nullable append-only snapshot evidence preserves actual import timestamps, counts, IDs and hashes independently of the current coverage-check timestamp. Result key 35 retains this evidence through the existing completed-event payload, Scylla projection and restart. The calculator treats known absence as DataUnfit and caps valid result lifetime at coverage expiry. Legacy absent-evidence JSON retains its hash shape.
- Policy `FMP.CalendarCoverage.v1` accepts downloads with positive remaining lifetime under 24 hours; the existing 900-second status-age limit applies to the coverage check, not to daily download cadence. Exact date coverage is always required. This versioned bridge policy is captured in evidence and does not rewrite published legacy parameters.
- Treasury is not a new required MarketCondition input. MarketCondition still evaluates only the ITI trigger's timeframe. Legacy family hints, option/broker requirements, fund-keyed parameters and tradeability semantics remain MC-R migration work.

Qualification: baseline 166 MarketCondition unit tests passed before changes; the updated unit suite has 196 passing tests, including new BDD and Verification categories. The existing MarketCondition BDD suite passed 4 tests and the selected business/decision-combination Verification suites passed 32 tests. A real NATS command -> PostgreSQL event -> durable Scylla projection -> query actor -> production calendar adapter integration scenario passed in an owned temporary keyspace. It verifies missing coverage, Treasury isolation, confirmed empty coverage, actual event classification and a newer failed refresh. The current user API has not yet been restarted with this consumer change.

This bridge is tracked separately as **MC-DL-01**. It closes the immediate empty-calendar/unknown-download gap; it does not close MC-R00 or MC-R04 in full. Terminal logs do not expose in-progress imports or certify an atomic calendar data generation. Preserve this limitation in source-quality documentation.

| MC-DL-01 check | Final result |
|---|---|
| MarketCondition unit suite, including new BDD/Verification categories | 196 passed |
| Combined DownloadLog integration suite, including the new MarketCondition consumer scenario | 14 passed |
| Existing MarketCondition BDD regressions | 4 passed |
| Existing business/decision-combination Verification regressions | 32 passed |
| API Server build | Zero warnings and errors |
| Documentation links and `git diff --check` | Passed |

These results qualify this consumer change, not the assessment-mode migration or a live post-deployment MarketCondition invocation. Current assessment qualification is recorded in the gate table below.

### Migration execution rules

1. Execute MC-R00 through MC-R09 in order. A gate closes only after its required implementation and targeted tests pass.
2. Preserve unrelated uncommitted changes, prior documentation amendments, old persisted contracts, and published parameter versions.
3. Record changed files, test commands/results, compatibility findings, and unresolved dependencies for each gate.
4. Use new wire types and a mode/version discriminator for changed semantics. Never reuse legacy MessagePack fields or enum meanings.
5. No automatic assessment starts with a timeframe mismatch, missing selected profile/provider binding, schema ambiguity, untested consumer, or skipped required qualification.
6. Infrastructure-backed tests run sequentially where they share actor addresses/databases.
7. No automatic retry, hidden market-state query, or healthy placeholder is introduced to make a gate pass.
8. Broker emulator implementation is a separate workstream. Its absence must not prevent market-assessment qualification.

## 4. Gate sequence and exit evidence

### MC-R00 — Baseline, authority, and dependency inventory

Implementation work:

- Record Git status and baseline build/targeted tests before runtime changes.
- Inventory legacy commands, Function identity/state, result schemas, parameters, providers, workflow transitions, queries, UI, and decision-reference exports.
- Inventory the existing single-horizon RegimeDiscovery result and accepted Workflow state; preserve the trigger-driven TargetHorizon boundary.
- Record exact nested DTO/enum manifests and schema discrimination capability; no guessed key reuse.
- Identify all family, option-quality, fund-policy, and broker dependencies in MarketCondition.
- Confirm the selected timeframe's existing upstream profile binding and required market sources; assign any missing market provider work to MC-R03/04. Another timeframe's data/configuration is not an activation prerequisite for this workflow.
- Reuse the verified DownloadLog dependency and MC-DL-01 consumer. Inventory calendar date coverage needed around the event window; identify scheduling gaps without adding refresh actions to MarketCondition.

Exit evidence: a file-level migration inventory, reproducible baseline, explicit assessment execution/historical-data boundary, and identified upstream/consumer prerequisites.

### MC-R01 — Preserve and verify the single-timeframe upstream handoff

Implementation work:

- Retain the existing accepted RegimeDiscoveryResult and its one TargetHorizon.
- Preserve propagation from TriggerEvent.EntityId.TimePeriod into Workflow, RegimeDiscovery, and MarketCondition requests.
- In the assessment path, validate exact agreement among trigger, workflow, accepted regime, selected parameter profile, snapshot, and result.
- Consume the accepted regime envelope/hash from frozen workflow state. Do not add a bundle, fan out RegimeDiscovery, read mutable latest projections, or borrow another workflow's result.
- Distinguish a missing/unaccepted or corrupt upstream contract from a valid accepted result that has become stale.
- Preserve restrictions and the original trigger's timestamp/sequence; do not select another timeframe to bypass them.

Tests:

- Reuse existing matching-timeframe tests and add coverage only where needed for the new path.
- Separate Daily, Weekly, and Monthly workflow fixtures each preserve one matching result and produce one assessment.
- Reject wrong timeframe, wrong workflow/trigger/market/profile, invalid hash, and missing/unaccepted upstream results.
- A Weekly workflow succeeds without requiring Daily or Monthly results or configuration.
- Restart uses the accepted single result and frozen parameters without current-state queries.

Exit evidence: the existing single-timeframe handoff is preserved and qualified for the revised assessment path. No additional upstream horizon production is required.

### MC-R02 — Assessment contracts and compatibility

Implementation work:

- Add ExecuteMarketConditionAssessmentCommand (Assess verb), versioned execution identity, new single-result/assessment types, typed terminal replies, and validators.
- Remove the legacy Execute implementation. Keep historical result DTOs, keys, enums and read-only history compatibility; they cannot authorize continuation.
- Add/validate the envelope payload discriminator and schema, appending fields only where existing envelope contracts evolve.
- Give every nested new record an explicit key manifest and every new enum a sentinel/numeric manifest.
- Validate one assessment and exact TargetHorizon equality plus Available/Unavailable invariants; omit all family/tradeability fields from new calculation DTOs.
- Separate evaluator inputs from the outer WorkflowView so fund/strategy context cannot leak into calculation.

Tests: old/new round trips, true legacy-shaped payloads, default/missing value rejection, cross-mode result rejection, fingerprint conflicts, and architecture checks for forbidden dependencies.

Exit evidence: contract fixtures prove old history remains readable and new semantics cannot be interpreted as legacy tradeability.

### MC-R03 — Market-only configuration and frozen assessment profile

Implementation work:

- Add MarketConditionAssessmentParameterSet, canonical hashing, strict validation, and one profile for the selected TargetHorizon.
- Implement the new ConfigurationDb table/index and closed lifecycle table mapping from specification v2.0.
- Resolve by MarketProfileId/root/TargetHorizon/effective time, including the timeframe in the database index and typed metadata checks. Do not use fund or family to choose analysis parameters.
- Freeze assessment profile, market profile, TargetHorizon, matching upstream binding, full parameter payload, and hashes before workflow start.
- Publish new versions rather than editing old rows. Assessment is the only execution path; `Enabled=false` pauses automatic starts during qualification.
- Freeze calendar dataset/provider/country-scope bindings, download-age policy and status-check freshness separately. Carry forward the bridge's positive-validity and latest-attempt rules; do not treat Treasury as required without a rate feature.

Tests: PostgreSQL draft/publish/retire/read, ambiguity/zero-match rejection, metadata/payload consistency, immutability/no-delete, effective time boundaries, canonical hashes, selected-profile/trigger equality and independence from other timeframe configurations, and inflight freeze.

Exit evidence: deterministic market-only configuration resolution and storage with no change to legacy published configuration.

### MC-R04 — One snapshot and explicit source quality

Implementation work:

- Build one underlying reference-market snapshot for the triggering TargetHorizon and selected profile.
- Reuse configured quote/trade/cache, session/calendar, and economic-event sources with source lineage and timestamps.
- Migrate MC-DL-01 into the new snapshot DTO and retain its frozen download evidence, exact UTC-date coverage, bounded latest-attempt queries, empty-completion handling, failure distinction and coverage-capped expiry. Translate known missing coverage to Unavailable, preserving the new descriptive event semantics.
- Remove option-chain, strategy-family, allocation, sizing, and broker calls from the new path.
- Add typed known-unavailable versus failed capture outcomes; do not treat timeout/exception as a known market report.
- Preserve bounded revision-stable capture, defensive copies, finite normalization, source deduplication, and canonical hashing.
- Use upstream normalized stress observations without recomputing regime indicators.

Tests: one-read sealing, no post-seal I/O, captured-revision consistency, exact age/skew boundaries, required-source outage for the selected assessment, optional unknowns, provider corruption, no-broker/no-option-data success, and stable hashes.

Exit evidence: a production snapshot path capable of supporting one matching-timeframe assessment without early product or family dependencies.

### MC-R05 — Descriptive horizon calculation

Implementation work:

- Implement one deterministic evaluator invoked only for the triggering TargetHorizon; support Daily, Weekly, and Monthly through separate parameterized invocations.
- Preserve matching upstream fields/restrictions, then derive current liquidity/stress, condition, confidence, and source-capped expiry.
- Implement one Available/Unavailable assessment; do not add aggregate horizon coverage or pairwise result contracts.
- Apply the exact classification precedence, confidence formula, rounding, and descriptive thresholds in specification v2.0.
- Remove opportunity-strength acceptance, suitability thresholds, OutputHints, preferred horizons, and family constructability.
- Generate ordered evidence/reasons and deterministic summaries after the typed result is final.

Tests:

- Golden cases for every condition class and below/equal/above descriptive thresholds.
- The matching accepted regime remains authoritative; no other-timeframe fetch, majority vote, or overwritten direction.
- Exact trigger timeframe validation and stale trigger explanation.
- Low confidence, Poor liquidity, elevated event context, and conflicting direction remain descriptive Available assessments when required data is fit.
- Missing data for another timeframe has no effect; required selected-timeframe data unavailability returns one Unavailable assessment.
- Confidence arithmetic, no-positive-validity boundary, optional Unknown, and deterministic concurrent calculation.
- Changing fund/family routing metadata does not change market assessment content.

Exit evidence: all formulas and authority boundaries are proven without a new tradeability decision hidden in the result.

### MC-R06 — Function lifecycle, workflow acceptance, and selector contract

Implementation work:

- Register Assess on the existing Function actor and use the new completed stream namespace.
- Preserve synchronous projection -> completed-only PostgreSQL state -> direct reply ordering.
- Retain hard deadlines, cooperative cancellation, late-worker fencing, and duplicate request behavior.
- Add explicit new-mode reply translation and workflow acceptance of the one matching-timeframe result.
- Continue once when the assessment is available/unexpired and no inherited/global stop applies. Validate the result timeframe before handoff.
- Return normal NoTrade for known target unavailability or authoritative NoNewTrade, and TimedOut for target expiry.
- Update Trade Selector's input contract to consume the single matching assessment, independently apply the frozen fund mandate, honor timeframe/expiry/restrictions, and own family/strategy suitability.
- Preserve independent workflow cancellation/global stops and all downstream order/risk/execution boundaries.

Tests: real Function lifecycle and workflow command transitions; matching target available with no other-timeframe data, target unavailable without timeframe substitution, upstream/global restriction, unfavorable-valid handoff, selector no-strategy response, duplicate/late/expired results, projector failure, persistence failure, and restart.

Exit evidence: exactly one appropriate handoff or terminal state with no premature family decision and no weakened downstream authorization.

This gate includes the selector consumer boundary and its validation; implementing every future trading strategy is not required.

### MC-R07 — Projections, queries, UI, and decision reference

Implementation work:

- Project one result per invocation and make exact/latest/history reads explicit about mode/schema and TargetHorizon.
- Present the selected workflow's one timeframe and assessment with availability, condition, confidence, data ages, inherited restrictions, evidence, and expiry.
- Remove Tradeable/family-hint displays for new mode while retaining clearly labeled legacy history.
- Require the requested timeframe in latest/history resolution so another timeframe's result cannot satisfy the query.
- Update decision-reference query/DTO/CSV paths with explicit assessment mode and separate Daily, Weekly, and Monthly invocation examples.
- Add bounded timeframe/availability/reason telemetry; preserve trace correlation and orphan-row diagnostics.

Tests: projection round trips and idempotency, mixed old/new history, timeframe-filtered latest queries, expired evidence, UI presenter timeframe/labels, typed NATS reference responses, and exported assessment rows without family hints.

Exit evidence: operators and downstream readers see the same typed single-timeframe assessment that Workflow accepted.

### MC-R08 — Integrated qualification and regression

Run through actual NATS, ConfigurationDb/PostgreSQL Function/workflow state, Scylla projection, and the existing single-horizon upstream/workflow handoff.

Minimum end-to-end matrix:

| Scenario | Required outcome |
|---|---|
| Separate Daily, Weekly, and Monthly triggers | Each workflow produces one matching assessment and one selector handoff |
| Weekly trigger with no Daily/Monthly data or configuration | Weekly workflow proceeds using only its required inputs |
| Any trigger/regime/profile/snapshot/result timeframe mismatch | Contract failure; no silent substitution or selector handoff |
| Required selected-timeframe data unavailable | One Unavailable assessment; normal NoTrade |
| Optional observation unavailable | Explicit Unknown, with required-data assessment retained |
| Calendar rows empty, required download missing/failed/stale | Unavailable; never infer Clear from an empty query |
| Confirmed calendar download with zero records | Valid coverage; classify actual event-window rows normally |
| Newer failed calendar refresh after earlier success | Unavailable; do not reuse the older success |
| Event window crosses UTC midnight | Every touched date needs covering calendar evidence |
| Only Treasury/another-country/another-date completion exists | Does not satisfy calendar coverage |
| Download query error/corrupt reply | Technical failure, not known unavailability |
| Valid low-confidence/trigger-conflicting/poor-liquidity market | Descriptive result reaches selector; no MC family recommendation |
| Inherited target NoNewTrade | Restriction persists; workflow stops normally |
| Malformed profile/upstream result/provider response | Typed failure; no completed authority |
| Fresh at capture, expired at acceptance/use | No stale continuation/use |
| No option data, no emulator, no actual broker | Assessment calculation unaffected |
| Matching/conflicting duplicate and host restart | Stable completed result or conflict; no extra capture/dispatch |
| Projection/append failure and late timeout worker | No unauthorized workflow advance; orphan row identifiable |
| Historical unbound workflow | Fails explicitly at Market Condition; no legacy calculation or result can continue it |

Require unit, BDD, integration, and verification evidence. A test receiver may observe a later-stage dispatch, but must not replace the accepted single-horizon upstream handoff, the MarketCondition calculator, Function lifecycle, persistence, or workflow acceptance in final qualification.

Run relevant RegimeDiscovery and Strategy Workflow regressions. Extend dependency testing only for changed or unresolved areas.

Exit evidence: recorded current pass counts, no skipped required cases, and real topology validation through separate invocations for all three supported horizons. Historical counts are not carried forward.

### MC-R09 — Controlled enablement and documentation closure

Implementation work:

- Deploy readers and selector consumers before enabling producers.
- Verify the selected timeframe's upstream profile, required source bindings, published assessment parameters, and new-mode host registration.
- Inspect an actual single-timeframe result through the application observation path; qualify Daily, Weekly, and Monthly through separate workflows.
- Enable new workflow starts only after MC-R00 through MC-R08 pass.
- Verify unbound starts and old Execute requests are rejected; assessment completion replay survives restart.
- Verify `Enabled=false` disables new starts without rewriting assessment state or published parameters; there is no legacy fallback.
- Update design/specification/plan statuses and record exact qualification evidence.
- Leave actual broker integration/emulator readiness claims separate from market-assessment completion.

Exit evidence: controlled end-to-end runtime observation, tested pause/restart and historical reads, synchronized documentation, and no unresolved prerequisite.

## 5. File-level implementation map

The table records the implemented migration areas; exact source manifests and qualification are recorded in [gate evidence](MarketCondition-Gate-Evidence-v2.0.md).

| Repository area | Work |
|---|---|
| TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/RegimeDiscovery/Model/RegimeDiscoveryResults.cs | Preserve the existing single-horizon result and matching-timeframe validation; no bundle contract |
| TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RegimeDiscovery | Retain the existing one-result-per-trigger behavior; qualify the handoff without expanding upstream production |
| Shared workflow input/view contracts and IntrinsicTime Realtime/Command/state/projectors | Freeze assessment profile, TargetHorizon, matching binding, and accepted single regime; validate/route new result |
| Shared Pipeline/Commands, Events, Identity, MarketCondition/Model | Assess command, versioned identity, new result/terminal/serialization contracts |
| Shared Pipeline/Configuration/MarketCondition | New assessment parameters/hash/validation |
| TomasAI.IFM.Application.Storage/ConfigurationDb | New table/lifecycle/effective resolver keyed by market profile/root/TargetHorizon |
| Domain.Trade/.../MarketCondition/Model/MarketConditionAssessmentSnapshotProvider.cs | Sole assessment capture path; legacy cache/provider removed |
| Domain.Trade/.../MarketCondition/Model/MarketConditionEventRiskAdapter.cs | Retained calendar evidence adapter; legacy production adapter bundle removed |
| Domain.Trade/.../MarketCondition/Model/MarketConditionCalendarCoverage.cs | Implemented MC-DL-01; reuse bounded DownloadLog evidence policy in assessment mode |
| Shared Pipeline/MarketCondition/Model/MarketConditionCalendarDownloadEvidence.cs, MarketConditionSnapshot.cs and MarketConditionResult.cs | Implemented append-only calendar provenance in snapshot/result; migrate into new contracts without changing legacy hash shape |
| Domain.MarketData.Shared/ServiceApi/IDownloadLogQueryApi.cs | Existing read-only actor API; no new FMP acquisition from MarketCondition |
| Domain.Trade/.../MarketCondition/Model | Sole deterministic assessment calculator; old evaluator removed |
| Domain.Trade/.../MarketCondition/Function | Assess receive map, state identity, projector, timeout/idempotency |
| Domain.Trade/.../MarketCondition/Query and shared read models | Typed single-assessment projections/queries with TargetHorizon filtering |
| Domain.Trade/.../MarketCondition/Model/MarketConditionDecisionReferenceGenerator.cs and shared export DTO/adapters | Versioned assessment examples and export |
| TradeSelection consumer and Strategy Observation UI/view models | Suitability boundary, new result display and expiry handling |
| API/test host registrations and workflow configuration | Assessment providers only; mode switch and legacy wiring removed |
| Trade Unit/BDD/Integrated/Verification and affected storage/serialization projects | New qualification plus legacy/regime/workflow regressions |

## 6. Expected verification commands

Select filters/project names after the MC-R00 inventory. Representative repository commands:

```powershell
dotnet build TomasAI.IFM.sln --no-restore -m:1
dotnet test TomasAI.IFM.Domain.Trade.UnitTests/TomasAI.IFM.Domain.Trade.UnitTests.csproj --no-build --filter "FullyQualifiedName~MarketCondition|FullyQualifiedName~RegimeDiscovery"
dotnet test TomasAI.IFM.Domain.Trade.BDDTests/TomasAI.IFM.Domain.Trade.BDDTests.csproj --no-build
dotnet test TomasAI.IFM.Domain.Trade.IntegratedTests/TomasAI.IFM.Domain.Trade.IntegratedTests.csproj --no-build
dotnet test TomasAI.IFM.Domain.Trade.VerificationTests/TomasAI.IFM.Domain.Trade.VerificationTests.csproj --no-build --filter "FullyQualifiedName~Strategy.IntrinsicTime.MarketCondition"
git diff --check
```

Build before --no-build test commands. Add focused ConfigurationDb, serialization, query/export, and UI suites appropriate to changed projects. If shared databases/actor routes are used, run those suites serially.

MC-DL-01 qualification commands are recorded below; they do not substitute for the MC-R migration suites:

```powershell
dotnet test TomasAI.IFM.Domain.Trade.UnitTests/TomasAI.IFM.Domain.Trade.UnitTests.csproj --no-restore --filter FullyQualifiedName~MarketCondition
dotnet test TomasAI.IFM.Domain.MarketData.IntegrationTests/TomasAI.IFM.Domain.MarketData.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~MarketConditionDownloadLogIntegrationTests
dotnet build TomasAI.IFM.Application.Api.Server/TomasAI.IFM.Application.Api.Server.csproj --no-restore
```

The integration test requires the local PostgreSQL/Redis/Scylla services and an isolated JetStream broker at `nats://127.0.0.1:14222` (override with `IFM_DOWNLOADLOG_TEST_NATS_URL`). It creates and drops its own `mc_dl_test_<guid>` keyspace and does not seed current-date successes in the application's keyspace.

## 7. Gate record

| Gate | Status | Evidence |
|---|---|---|
| MC-DL-01 | Implemented; targeted qualification passed | Calendar DownloadLog consumer bridge; 196 unit/BDD/verification tests, BDD/Verification regressions and real actor/storage consumer scenario; live consumer restart pending |
| MC-R00 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R01 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R02 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R03 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R04 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R05 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R06 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R07 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R08 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |
| MC-R09 | Complete (controlled qualification) | [Implementation and current evidence](MarketCondition-Gate-Evidence-v2.0.md) |

The old plan retains the original MC-00 through MC-22 and PDR evidence as historical records. Their completion does not qualify removal of family hints, removal of broker requirements, or automatic assessment starts. Existing single-horizon routing remains valid and is rechecked, not replaced.

## 8. Completion boundary

The gates are complete for the controlled qualification recorded above. Runtime evidence shows exactly one assessment for each workflow's triggering timeframe, using its already accepted matching upstream result, with no family/product selection inside MarketCondition.

Actual IBKR remains unimplemented. The IBKR emulator comes first in the separate broker workstream; execution readiness is checked before order submission. Neither an emulator nor an actual broker connection is required to qualify this market-only assessment stage.
