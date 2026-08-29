# Market Condition Implementation Plan v1.0

| Item | Value |
|---|---|
| Status | Implemented and qualified; MC-00 through MC-22 complete |
| Created | 2026-08-28 |
| Source design | `MarketCondition-High-Level-Design-v0.1.md` |
| Authoritative specification | `MarketCondition-Specification-v1.0.md` |
| Architecture reference | `Regime-Discovery-Implementation-v1.0.md` and the system FunctionActor conventions |
| Implementation target | .NET 10 / C# actor-based Intrinsic Time Strategy Workflow |

## 1. Objective

Implement Market Condition as the second Intrinsic Time Strategy Workflow stage. The implementation must consume the accepted immutable Regime Discovery result and a sealed ES futures/futures-option market snapshot, return one deterministic Tradeable or NotTradeable completed result or one typed failure, and permit the Strategy Workflow to continue only after it durably accepts a valid, unexpired Tradeable result.

The target is the same completed-only FunctionActor lifecycle proven by Regime Discovery:

```text
Workflow Realtime
  -> ExecuteMarketConditionPipelineCommand over Core NATS request/reply
  -> MarketConditionFunctionActor
  -> frozen snapshot + deterministic model
  -> completed candidate only:
       synchronous Scylla projection
       PostgreSQL completed-only Function state
       direct completed reply
  -> failure/timeout/exception:
       direct non-durable failed reply
  -> Workflow Realtime
  -> CompleteMarketConditionCommand or FailMarketConditionCommand
  -> one authoritative WorkflowStrategyStateUpdatedEvent
```

The plan also implements the accepted normal terminal outcome:

```text
Completed + Tradeable    -> TradeSelection
Completed + NotTradeable -> NoTrade
Failed                   -> PipelineFailed
Expired/timeout          -> TimedOut
```

## 2. Gate execution rules

1. Gates execute in order from MC-00 through MC-22.
2. A later gate may begin only after the prior gate's build and targeted tests are green.
3. Every gate records files changed, tests added/updated, exact pass counts, and unresolved observations.
4. Existing uncommitted work is preserved. Unrelated files are not reformatted, reverted, or folded into a gate.
5. Contract changes are append-only unless the specification explicitly replaces an unused Market Condition placeholder.
6. No gate is complete with skipped, disabled, placeholder, or assertion-free Market Condition tests.
7. Test doubles may replace a not-yet-implemented later pipeline, but may not replace the Market Condition Function, calculation model, workflow command actor, or persistence path in final integration/verification gates.
8. Failures found during a gate are fixed in the owning gate; they are not deferred to final qualification merely to preserve apparent progress.
9. Documentation status changes from Planned to Complete only after the current terminal gate, MC-22, passes.

## 3. Gate sequence

```text
MC-00 Baseline and change isolation
  -> MC-01 Shared contracts, identity, and legacy boundary replacement
  -> MC-02 Versioned parameter model and ConfigurationDb storage
  -> MC-03 Workflow configuration freeze and NoTrade state evolution
  -> MC-04 Immutable snapshot contracts and provider adapters
  -> MC-05 Ordered hard-gate model
  -> MC-06 Classification, scoring, result, and deterministic summary
  -> MC-07 Function actor, completed-only state, projector, and query foundation
  -> MC-08 Atomic Execute extension, timeout, and idempotency
  -> MC-09 Workflow Realtime Function invocation and reply translation
  -> MC-10 Workflow Tradeable, NoTrade, failure, and expiry transitions
  -> MC-11 Projection, queries, observation, and operational telemetry
  -> MC-12 Unit and contract qualification
  -> MC-13 BDD business-flow qualification
  -> MC-14 Storage and runtime integration qualification
  -> MC-15 Basic Market Condition verification suite
  -> MC-16 Full regression, controlled enablement, and documentation closure
  -> MC-17 Input-authority and unused-input audit
  -> MC-18 Full RegimeDiscoveryDecision consumption
  -> MC-19 ITI and futures/options corroboration
  -> MC-20 Advisory output-hint contract and result schema V2
  -> MC-21 Hint derivation, projection, and workflow handoff
  -> MC-22 Pairwise decision qualification and documentation closure
```

## 4. Implementation gates

### MC-00 — Baseline and change isolation

Steps:

1. Inventory the working tree and preserve the completed Regime Discovery and Strategy Workflow changes.
2. Build the solution with serialized MSBuild when the shared DataBento native output requires it.
3. Run and record the current Trade Unit, BDD, Integrated, and Verification baselines.
4. Inventory every reference to `StartMarketConditionPipelineCommand`, `MarketConditionPipelineProcessingEvent`, Market Condition terminal events, and the unconditional `CompleteMarketCondition` transition.
5. Inventory existing ES futures quote, option quote, Securities metadata, session/calendar, health, cache, and broker-readiness providers.
6. Freeze the approved specification defaults, MessagePack keys, enum values, and reason codes.
7. Identify any provider capability that does not yet exist and assign it to MC-04 rather than silently weakening a required gate.

Exit gate:

- baselines and existing dirty changes are recorded;
- all legacy consumers and test probes are identified;
- the implementation has no unresolved architectural decision.

### MC-01 — Shared contracts, identity, and legacy boundary replacement

Steps:

1. Add `MarketConditionExecutionEntityId` as a `readonly record struct`, its formatter, factory, and validation rules.
2. Add `ExecuteMarketConditionPipelineCommand` with the exact Function subject, route, immutable workflow input, parameter payload, FundId, instrument root, horizon, and deadline defined by the specification.
3. Update `MarketConditionPipelineCompletedEvent` and `MarketConditionPipelineFailedEvent` to the exact Function reply schemas.
4. Add Market Condition result/evidence/blocker/failure enums with their fixed numeric values.
5. Append `StrategyWorkflowOutcome.NoTrade = 7` without shifting existing values.
6. Introduce the Function contracts alongside the legacy Start/Processing placeholders so the solution remains buildable. Mark the placeholders as migration-only and prohibit new production use; remove them in MC-09 after every compile-time consumer is migrated.
7. Update routing and bounded-context metadata for `ActorType.Function`.
8. Add MessagePack round-trip, key-order, enum-value, entity equality/hash/format, and default-value rejection tests.
9. Add architecture tests proving no Market Condition Command/Event/Realtime actor or terminal publication route exists.

Exit gate:

- shared contracts compile independently;
- Function subjects and identities are deterministic;
- old numeric enum and MessagePack values remain unchanged;
- the new Function contracts have no publication path and legacy placeholders are isolated for removal.

### MC-02 — Versioned parameter model and ConfigurationDb storage

Steps:

1. Add the top-level `MarketConditionParameterSet` and every nested configuration record from the specification.
2. Implement `CreateDefault` for Daily, Weekly, and Monthly using the approved ES futures/options values.
3. Add FluentValidation rules for identities, FundId, ES root, horizon, durations, windows, arrays, ranges, finite decimals, DTE ordering, and all weight-sum invariants.
4. Add `MarketConditionParameterPayload` canonical JSON serialization and SHA-256 calculation.
5. Create `reference_configuration.market_condition_parameter_set` and its composite effective-resolution index.
6. Extend `IConfigurationDbContext` and `ConfigurationDbContext` with draft insert, exact get, and effective resolution by FundId/instrument/horizon.
7. Extend generic Publish and Retire SQL to select the table through a closed `StrategyParameterSetKind` map. Table names must never come from caller strings.
8. Reject zero matches, ambiguous published matches, payload/hash mismatch, typed schema mismatch, and invalid lifecycle transitions.
9. Add PostgreSQL integration tests for insert, immutability, publish, retire, exact read, effective selection, future-effective exclusion, ambiguity, and no-delete behavior.

Exit gate:

- configuration CRUD/resolution is executable against PostgreSQL;
- published payloads cannot be edited or deleted;
- the typed result and canonical hash round-trip exactly;
- one and only one configuration can be selected for a Fund/ES/horizon/effective time.

### MC-03 — Workflow configuration freeze and NoTrade state evolution

Steps:

1. Extend workflow start/input contracts append-only with FundId, Market Condition parameter set, and canonical hash.
2. Extend `IntrinsicTimeStrategyWorkflowView`, legacy/public workflow state, start events, cloning, equality/fingerprinting, repositories, and projectors.
3. Add explicit Outcome to the authoritative immutable workflow view so NoTrade is not inferred as full workflow completion.
4. Resolve Regime and Market Condition parameter sets before the first Started snapshot is committed.
5. Reject missing or ambiguous configuration without committing a Started workflow.
6. Freeze parameter payloads/hashes in the workflow snapshot and never re-resolve them during stage execution or state reconstruction.
7. When Regime completes, initialize MarketCondition stage metadata with its parameter identity/version/hash and input revision.
8. Update existing workflow tests for append-only serialization, reconstruction, stale data, cloning, and projection.

Exit gate:

- revision 1 contains all parameters required by both implemented stages;
- revision 2 supplies the exact frozen Market Condition parameter set;
- NoTrade can survive PostgreSQL reconstruction and Scylla/query projection;
- historical workflows never resolve current configuration.

### MC-04 — Immutable snapshot contracts and provider adapters

Steps:

1. Add immutable snapshot, source observation, ES futures quote, option-chain aggregate, session, event-risk, volatility shock, operational-health, and workflow-eligibility contracts.
2. Add `IMarketConditionSnapshotProvider` with typed success, known-blocked, and failure outcomes.
3. Normalize existing double-based market inputs to finite decimals at the provider boundary.
4. Build the ES futures best bid/ask/size snapshot from the established latest-value feed/cache without rereading after seal.
5. Build the bounded ES futures-option quality universe through authoritative Securities metadata for expiry, option type, strike, and underlying mapping.
6. Calculate quote coverage, median/P90 relative spread, median sizes, expiration count, source ages, and underlying mismatch deterministically.
7. Add session, event-risk, feed/cache health, and IBKR-readiness provider interfaces/adapters.
8. Preserve the accepted distinction: a reliable unhealthy/unavailable state is a business blocker; missing, corrupt, expired, or contradictory mandatory health metadata is a capture failure.
9. Implement bounded revision-stable capture attempts, one evaluation timestamp, defensive copies, canonical ordering, and snapshot SHA-256.
10. Do not persist full order books, complete option chains, credentials, or unrestricted broker payloads.
11. Add unit tests for snapshot sealing, no mid-calculation reread, freshness boundaries, future skew, aggregate math, option-universe filtering, deterministic hash, and provider failure classification.

Exit gate:

- one immutable and diagnosable snapshot represents one market moment;
- every required source has identity, timestamp, sequence/health metadata, availability, and validity;
- known blockers cannot be confused with invalid capture failures.

### MC-05 — Ordered hard-gate model

Steps:

1. Implement sealed stateless evaluators for Workflow Eligibility, Data Fitness, Session, Event Risk, Market Integrity, ES Futures Liquidity, ES Option Liquidity, and Operational Readiness.
2. Execute gates in the exact specification order.
3. Return all ordered blockers/evidence while selecting the first configured blocker as PrimaryReasonCode.
4. Stop opportunity scoring whenever a hard blocker exists.
5. Treat Regime `NoNewTrade`, disabled entries, session closure, stale known data, event lockout, dislocation, insufficient liquidity, and reported unavailable operations as completed blockers.
6. Treat corrupt envelopes, invalid hashes, incompatible identities, invalid numerics, and untrustworthy provider metadata as typed failures.
7. Put every threshold in the parameter set; do not duplicate numeric rules in handlers.
8. Add below/equal/above boundary tests for every hard threshold and every reason code.

Exit gate:

- every hard gate is deterministic and independently testable;
- NotTradeable and Failed semantics are mechanically distinct;
- the same snapshot/parameters always produce the same ordered blocker result.

### MC-06 — Classification, scoring, result, and deterministic summary

Steps:

1. Implement direction mapping and Regime/trigger alignment.
2. Implement phase precedence for initiating, reversing, exhausting, weakening, continuing, confirmed, and undefined.
3. Implement volatility behavior and condition-type precedence.
4. Implement normalized Regime, trigger, futures-liquidity, option-liquidity, data-quality, and entry-timing features.
5. Implement exact strength/confidence weights, penalties, clamp rules, six-decimal rounding, and horizon thresholds.
6. Implement final Tradeable/NotTradeable invariants.
7. Build ordered supporting/conflicting evidence, blockers, reasons, PrimaryReasonCode, and deterministic summary.
8. Validate completed results before they can become completed Function candidates.
9. Add exact golden-vector tests for at least aligned bullish, aligned bearish, range-bound, transition, volatility expansion, volatility contraction, below-strength, and below-confidence scenarios.
10. Prove sequential and thread-pool-parallel calculations serialize identically for fixed inputs.

Exit gate:

- every formula and classification in the specification is executable;
- exact expected values are committed as tests rather than broad ranges;
- a completed Tradeable result cannot contain an undefined required classification or blocker.

### MC-07 — Function actor, completed-only state, projector, and query foundation

Steps:

1. Add `MarketConditionFunctionActor`, typed context, and DI registration.
2. Implement read-only `_parseMap`, `_validationMap`, and exact-type `_receiveMap` with equal supported request sets.
3. Delegate parsing to `ParseMappedFunction` and exact dispatch to `ResolveMappedFunctionHandler`.
4. Aggregate validation failures into one `CommandValidationException`; visibly validate CommandId first.
5. Add `MarketConditionFunctionState` and repository using expected initial stream version zero.
6. Store completed events only; do not denormalize Function state through the ordinary event projector.
7. Add the synchronous idempotent `MarketConditionFunctionProjector` and completed Scylla read model.
8. Add Market Condition Query actor/context and standardized query `_parseMap`, exact `_receiveMap`, generated `_exceptionMap`, and read-only API.
9. Add architecture/convention tests for actor registration, map parity, exact dispatch, no audit reservation, no terminal publication, and no prohibited actor folders.

Exit gate:

- the Function actor conforms to the generic base lifecycle;
- matching completion state can be reconstructed after restart;
- failed results have no Function state or successful projection;
- queries are read-only and cannot influence continuation.

### MC-08 — Atomic Execute extension, timeout, and idempotency

Steps:

1. Add `ExecuteMarketConditionPipeline` as the command-specific Function extension.
2. Validate the immutable workflow/Regime/trigger/parameter cross-field contract before capture.
3. Derive the Function deadline as the minimum of workflow expiry and configured five-second execution duration.
4. Race the entire capture/evaluation worker against the deadline using `TimeProvider` and cancellable tasks.
5. Recheck deadline before producing a completed candidate.
6. Fence and observe late workers so they cannot project or save completion.
7. Translate known blocker calculations to completed NotTradeable events and calculation/capture failures to typed failed events.
8. Preserve base lifecycle ordering: calculate, project, save completed state, return.
9. Return prior matching completion without repeating capture, calculation, projection, or save.
10. Reject a conflicting request fingerprint without side effects.
11. Add deterministic unit tests for worker-wins, exact-boundary timeout-wins, cancellation, late success, late exception, projection exception, persistence exception, matching retry, and conflict.

Exit gate:

- every invocation returns one logical Completed or Failed result;
- timeout permanently outranks late completion;
- only successfully projected completed candidates enter Function state.

### MC-09 — Workflow Realtime Function invocation and reply translation

Steps:

1. Replace the Market Condition Start-command send path with Function request/reply.
2. Build `MarketConditionExecutionEntityId` and deterministic command identity from the committed workflow view.
3. Pass the exact workflow revision, trigger, FundId, ES root, parameter set/hash, and deadline.
4. Use Core NATS Function request/reply with calculation deadline plus five-second transport-only grace.
5. Translate Completed directly to `CompleteMarketConditionCommand` and Failed directly to `FailMarketConditionCommand`.
6. Do not parse or subscribe to Market Condition terminal events as Realtime messages.
7. Ensure transport timeout cannot produce a successful workflow transition.
8. Update the Regime verification Market Condition probe to intercept the Execute Function request rather than the removed Start command, preserving Regime's boundary-focused tests.
9. Migrate all remaining production and test references, including the generic integration pipeline simulator, then remove `StartMarketConditionPipelineCommand` and `MarketConditionPipelineProcessingEvent`.
10. Add unit tests for command construction, reply translation, transport failure, and stale workflow suppression.

Exit gate:

- the only Market Condition execution path is direct Function request/reply;
- no terminal Function result is published;
- Regime verification remains isolated from the real Market Condition implementation where intended.

### MC-10 — Workflow Tradeable, NoTrade, failure, and expiry transitions

Steps:

1. Replace the unconditional `CompleteMarketCondition` transition with typed payload deserialization and result validation.
2. Validate event/result IDs, envelope hash/schema/type, workflow/entity/fund/horizon, input revision, parameter identity/hash, snapshot identity, and deadlines.
3. For Tradeable, commit revision 3 with MarketCondition Completed/Proceed and TradeSelection Processing.
4. For NotTradeable, commit revision 3 with MarketCondition Completed/Stop, machine Completed, outcome NoTrade, terminal timestamp, and PrimaryReasonCode; do not initialize TradeSelection.
5. For expired completion, commit TimedOut and do not recalculate or continue.
6. For an ordinary Failed result, commit workflow PipelineFailed and preserve safe failure metadata.
7. For typed `FailureCategory.Timeout`, commit MarketCondition and workflow TimedOut. Replace the current Regime-specific numeric timeout check with typed Market Condition classification; string matching is compatibility-only and cannot be primary authority.
8. Ignore stale, duplicate, superseded, wrong-stage, wrong-revision, and late terminal commands without another transition.
9. Preserve lazy expiration of a lost-reply workflow before accepting a later workflow start.
10. Update state application, legacy/public conversion, projectors, and query models to preserve explicit Outcome.
11. Add state-machine unit tests for every transition and precedence boundary.

Exit gate:

- only an accepted Tradeable result selects TradeSelection;
- a valid NotTradeable result is a normal durable NoTrade terminal outcome;
- failure, timeout, expiry, duplicate, and stale data can never select a later pipeline.

### MC-11 — Projection, queries, observation, and operational telemetry

Steps:

1. Implement idempotent Scylla create/update/read/history operations for `MarketConditionReadModel`.
2. Project all structured result fields, serialized payload/hash, parameter/snapshot identities, and timestamps.
3. Extend Strategy Workflow observation, timeline, and current read models for explicit NoTrade and Market Condition details.
4. Add query API methods for exact result, latest Fund/ES/horizon result, and bounded history.
5. Add spans for Function handling, snapshot, gates, calculation, projection, persistence, and continuation.
6. Add bounded-cardinality metrics for outcome, blocker/failure codes, latency, data age, expiry, strength, confidence, and timeout.
7. Add structured transition/error logs without full option chains, credentials, or unbounded evidence logging.
8. Add projection/query integration tests, including idempotent duplicate apply and observational-orphan handling.

Exit gate:

- operators can distinguish Tradeable, NoTrade, Failed, and TimedOut;
- projection/query data is sufficient to reproduce why a decision was made;
- observability cannot alter workflow authority.

### MC-12 — Unit and contract qualification

Steps:

1. Consolidate all gate-level Unit tests under `Strategy/Workflow/IntrinsicTime/MarketCondition`.
2. Cover parameter contracts/defaults/validation/hashing, entity identity, MessagePack compatibility, snapshot math, every hard gate, classification/scoring, evidence ordering, Function lifecycle, timeout/idempotency, and workflow transitions.
3. Add architecture tests for forbidden actor types/routes and map parity.
4. Add regression tests for existing Strategy Workflow and Regime Discovery behavior affected by new frozen fields and explicit Outcome.
5. Run Trade Shared and Trade Unit builds/tests with no skipped Market Condition tests.

Exit gate:

- all deterministic and structural behavior is covered by fast tests;
- every specification boundary and numeric threshold has an executable assertion;
- no test relies on arbitrary wall-clock sleep to choose a timeout winner.

### MC-13 — BDD business-flow qualification

Steps:

Implement at minimum these executable scenarios:

1. Healthy aligned completed result continues exactly once to Trade Selection.
2. Session closed completes normally as NoTrade.
3. Option liquidity below threshold completes normally as NoTrade.
4. Regime `NoNewTrade` restriction completes normally as NoTrade.
5. Direction conflict completes normally as NoTrade/NoOpportunity.
6. Invalid mandatory snapshot metadata fails the workflow.
7. Function timeout terminates the workflow and fences late completion.
8. Completed result expired before acceptance terminates without redispatch.
9. Duplicate completion changes workflow revision at most once.
10. A later start lazily expires an abandoned workflow and may begin a new workflow.

BDD scenarios exercise real command extensions/state transitions rather than mocking the result under test. They use deterministic clocks and immutable typed result builders.

Exit gate:

- business stakeholders can read the expected Tradeable, NoTrade, Failed, and TimedOut behavior directly;
- every non-Tradeable/failure scenario proves that Trade Selection and all order stages remain untouched.

### MC-14 — Storage and runtime integration qualification

Steps:

1. Test PostgreSQL ConfigurationDb lifecycle and effective resolution against the real schema.
2. Test PostgreSQL Function-state append/reconstruction/idempotency and optimistic conflict handling.
3. Test Scylla completed projection, query retrieval, hash equality, and duplicate upsert.
4. Execute real Workflow Realtime -> Market Condition Function -> Workflow Command flow through NATS.
5. Use production actor registration, serializers, ConfigurationDb, PostgreSQL, ScyllaDB, and snapshot interfaces.
6. Replace only Trade Selection with a passive typed probe.
7. Prove Tradeable dispatches one Trade Selection command containing revision 3 and the immutable Market Condition result.
8. Prove NotTradeable projects/saves completion, commits NoTrade, and dispatches nothing later.
9. Prove failure, timeout, projector failure, persistence failure, lost reply, duplicate request, and restart do not advance.
10. Verify the migrated generic pipeline simulator no longer sends removed Market Condition processing/publication events.
11. Verify host startup/shutdown does not hang and fixed actor addresses are isolated through non-parallel collections.

Exit gate:

- the real infrastructure-backed topology matches the specification;
- persistence and workflow views agree for successful outcomes;
- all failure barriers are proven at both storage and dispatch boundaries.

### MC-15 — Basic Market Condition verification suite

Steps:

1. Add `Strategy/IntrinsicTime/MarketCondition` under `TomasAI.IFM.Domain.Trade.VerificationTests`.
2. Add deterministic scenario contracts, builders, assertions, a non-parallel runtime fixture, and a passive Trade Selection probe.
3. Execute production snapshot aggregation, gates, calculation, Function actor, projector/state, workflow transition, and query path.
4. Add exact positive fixtures for Daily, Weekly, and Monthly using healthy ES futures/options inputs.
5. Add at least one exact aligned bullish Directional and one aligned bearish Directional result.
6. Add basic successful RangeBound and Transition classifications.
7. Add basic NoTrade fixtures for session, stale quote, futures liquidity, option liquidity, event risk, operations unavailable, Regime restriction, strength, and confidence.
8. Add basic failure fixtures for invalid parameter hash, corrupt mandatory metadata, projector exception, and hard timeout.
9. Cross-check Scylla projection, PostgreSQL Function state, workflow state/read model, Query API, result payload/hash, parameter version, snapshot hash, and exactly-once continuation.
10. Tag every test `Trait("Category", "Verification")`; add no skipped placeholders.

Exit gate:

- basic V1 Market Condition functionality is proven through production code and infrastructure;
- all supported horizons produce reviewable business outcomes;
- only Tradeable reaches Trade Selection and no scenario reaches Order Composition, Risk Management, or Order Execution.

### MC-16 — Full regression, controlled enablement, and documentation closure

Steps:

1. Run formatting verification and `git diff --check`.
2. Build the complete solution; use `-m:1` if required by the shared DataBento native build output.
3. Run Trade Unit, BDD, Integrated, and Verification suites.
4. Run affected Storage, Actor, Serialization, MarketData Feed, and Analytics suites identified by the actual change map.
5. Repeat focused Market Condition verification to detect state leakage or fixed-address teardown issues.
6. Confirm no legacy Start/Processing/Realtime Market Condition route remains.
7. Confirm Regime Discovery verification remains green after its boundary probe migration.
8. Record pass counts, durations, infrastructure prerequisites, skips, and any unrelated existing warning.
9. Update the specification, high-level design, implementation plan, actor conventions, and workflow implementation documentation to match delivered behavior.
10. Enable Market Condition only after all gates are green; otherwise leave the workflow stopped at the prior controlled boundary.

Exit gate:

- MC-00 through MC-16 have complete execution records for the original V1 closure;
- solution and affected suites pass;
- documentation describes the code that exists;
- the implementation is ready for Trade Selection design without weakening the order-execution safety chain.

### MC-17 through MC-22 — Maximum input use and advisory output hints

These gates are an append-only upgrade to the completed V1 actor topology.

| Gate | Required implementation and exit evidence |
|---|---|
| MC-17 | Audit every Regime Discovery, trigger, futures, option, session, event-risk, health, and workflow field. Record primary authority, corroboration, scoring, gate, evidence-only, or unavailable semantics. No populated decision field may be silently ignored. |
| MC-18 | Make `RegimeDiscoveryDecision` the primary source of market direction, phase, volatility behavior, structure, breakout, restrictions, and decision quality. Retain specialist fallbacks only for schema-V1-shaped upstream payload compatibility. |
| MC-19 | Keep the exact ITI event as directional/timing corroboration and the frozen futures/options snapshot as independent tradeability, liquidity, data-quality, and hint-quality evidence. A conflict is explicit and deterministic. |
| MC-20 | Add append-only `MarketConditionResult` schema V2 `OutputHints[]` with typed trade family, timeframe, suitability, confidence, reason, and an explicit advisory marker. |
| MC-21 | Emit exactly one minimum hint for the evaluated horizon: `Futures/Daily`, `VerticalSpread/Weekly`, or `IronCondor/Monthly`. Derive it after the primary result; blocked results emit `Avoid`. Preserve it in the Function envelope and projected result payload. |
| MC-22 | Qualify unit/contract, BDD, runtime integration, and a 12-case pairwise verification matrix; update high-level design, specification, implementation plan, exact counts, and schema language. |

Direct implementation rule: **inputs determine the Market Condition decision; hints describe possible downstream use.**
Hints may be changed, reranked, ignored, or augmented by Trade Selection. They cannot make a blocked market tradeable,
erase evidence, override a Regime restriction, or narrow the market language Market Condition is permitted to emit.

## 5. File-level implementation map

| Area | Change |
|---|---|
| `Domain.Trade.Shared/.../Identity/MarketConditionExecutionEntityId.cs` | Add execution identity and validation |
| `Domain.Trade.Shared/.../Pipeline/Commands` | Replace Start placeholder with Execute Function request |
| `Domain.Trade.Shared/.../Pipeline/Events` | Convert Completed/Failed to direct Function contracts; remove Processing |
| `Domain.Trade.Shared/.../Pipeline/Configuration/MarketCondition` | Add typed parameter records, defaults, validation, canonical payload/hash |
| `Domain.Trade.Shared/.../Pipeline/MarketCondition/Model` | Add enums, evidence, blockers, result, snapshot/query/read-model contracts |
| `Application.Storage/ConfigurationDb` | Add Market Condition table, CRUD, lifecycle, and effective resolution |
| `Application.Storage/TradeDb` | Add completed Function-state storage/reconstruction if generic repository wiring requires it |
| `Domain.Trade/.../MarketCondition/Model` | Add snapshot aggregation, gates, classification, scoring, and summary models |
| `Domain.Trade/.../MarketCondition/Function` | Add actor, context, Execute extension, projector, state, and repository |
| `Domain.Trade/.../MarketCondition/Query` | Add standardized Query actor/context/extensions |
| `Domain.Trade/.../IntrinsicTime/Realtime` | Replace Start send with Function request/reply and workflow command translation |
| `Domain.Trade/.../IntrinsicTime/Command` | Add Tradeable/NoTrade/expiry branching and explicit Outcome persistence |
| `Domain.Trade/.../IntrinsicTime/Command/EventProjector` | Preserve NoTrade and Market Condition observation fields |
| `Domain.Trade.UnitTests/.../MarketCondition` | Add deterministic contracts, gates, formulas, Function, and transition tests |
| `Domain.Trade.BDDTests/.../MarketCondition` | Add readable business-flow scenarios |
| `Domain.Trade.IntegratedTests/.../MarketCondition` | Add real topology and persistence tests; replace legacy simulator seams |
| `Domain.Trade.VerificationTests/.../MarketCondition` | Add basic V1 business-verification suite |
| Regime Discovery verification fixture | Replace Start-command Market Condition probe with Execute Function boundary probe |
| `Documents/system/Actor-Implementation-Conventions.md` | Mark Market Condition as the second conforming FunctionActor implementation |

Exact repository files must be re-inventoried at MC-00 because the working tree may evolve before implementation begins.

## 6. Mandatory conventions and constraints

### 6.1 Actor and messaging

- Function request uses `ActorType.Function` and Core NATS request/reply only.
- The request remains `ICommand<MarketConditionExecutionEntityId>`.
- `_parseMap` uses the exact command verb and `ParseMappedFunction`.
- `_validationMap` and `_receiveMap` use exact CLR `Type`; assignable fallback, type-name strings, reflection discovery, and dispatch switches are prohibited.
- Parse, validation, and receive supported request sets must be equal.
- Function attempts use structured attempt logging, not CommandActor audit reservation.
- Function terminal results are returned, never published to Core NATS or JetStream.
- No Market Condition Command, Event, or Realtime actor is introduced.

### 6.2 Validation and serialization

- Every command validation visibly begins with CommandId validation.
- Validation accumulates all deterministic errors into one `CommandValidationException`.
- Reference payloads use colocated FluentValidation rules; entity validation follows its identity definition.
- MessagePack keys and enum numeric values are append-only.
- Serialization constructors include every keyed member in key order.
- Required decimals must be finite and bounded; source doubles are normalized at the boundary.
- Arrays are defensively copied and deterministically ordered.

### 6.3 Determinism

- Calculation models are sealed, stateless, and receive immutable inputs.
- Wall-clock access occurs through `TimeProvider` outside pure formula methods.
- GUID/time generation is injected or fixed in deterministic tests.
- All thresholds and weights come from the frozen parameter set.
- Decimal rounding uses six places and `MidpointRounding.AwayFromZero` where specified.
- Evidence, blockers, conflicts, and reasons use explicit stable ordering.
- No LLM, random source, current configuration lookup, or live reread participates in authority.

### 6.4 Persistence and atomicity

- ConfigurationDb is authoritative for immutable versioned parameters; rows are never deleted.
- Function state persists completed results only and exists for idempotency.
- Failed/timeout results are not stored in Function state and are not projected.
- Synchronous completed projection finishes before completed Function-state append.
- The cross-database sequence is a semantic commit protocol, not distributed ACID.
- An observational Scylla orphan after PostgreSQL failure cannot advance workflow state.
- Strategy Workflow's committed snapshot is the sole continuation authority.

### 6.5 Market data and security

- Snapshot capture reads each required source once and then seals the snapshot.
- Full option chains/order books are not stored in workflow or Function events.
- Contract metadata comes from Securities/reference data, not display-symbol parsing in the evaluator.
- Credentials, broker payloads, and unrestricted vendor messages never enter results or logs.
- Workflow/entity/invocation IDs are trace fields and never metric labels.

### 6.6 Safety

- Completed NotTradeable is a successful result and maps to NoTrade.
- Invalid mandatory inputs are failures and cannot be disguised as NotTradeable.
- Only a valid, accepted, unexpired Tradeable result may initialize Trade Selection.
- Timeout and expiry outrank completion at the boundary.
- Late, stale, duplicate, conflicting, or superseded results cannot advance workflow revision.
- No automatic retry, business replay, resume, or stage skip is introduced.

## 7. Basic test ownership and minimum matrix

| Behavior | Unit | BDD | Integration | Verification |
|---|:---:|:---:|:---:|:---:|
| Contract keys, enum values, identity, validation | Required |  | Required round-trip |  |
| Parameter defaults/hash/lifecycle | Required |  | Required PostgreSQL | Required identity/version |
| Snapshot sealing and option aggregate math | Required |  | Provider adapters | Required production path |
| Every hard gate and threshold boundary | Required | Representative | Representative | Basic blocker set |
| Classification/strength/confidence formulas | Exact golden | Representative |  | Exact positive scenarios |
| Completed-only Function lifecycle | Required |  | Required | Required |
| Tradeable -> Trade Selection exactly once | Required | Required | Required | Required |
| NotTradeable -> NoTrade and no next stage | Required | Required | Required | Required |
| Failure -> PipelineFailed and no projection/next | Required | Required | Required | Required |
| Timeout/expiry/late result fencing | Required | Required | Required | Required |
| Matching/conflicting duplicate | Required | Required | Required | Required |
| Restart/reconstruction | Required state apply |  | Required | Required cross-check |
| Daily/Weekly/Monthly | Parameter tests | At least one | At least one | All three |

Minimum BDD and integration coverage is part of implementation, not optional follow-up work.

## 8. Expected test commands

```powershell
dotnet build TomasAI.IFM.sln --no-restore -m:1
dotnet test TomasAI.IFM.Domain.Trade.UnitTests/TomasAI.IFM.Domain.Trade.UnitTests.csproj --no-build
dotnet test TomasAI.IFM.Domain.Trade.BDDTests/TomasAI.IFM.Domain.Trade.BDDTests.csproj --no-build
dotnet test TomasAI.IFM.Domain.Trade.IntegratedTests/TomasAI.IFM.Domain.Trade.IntegratedTests.csproj --no-build
dotnet test TomasAI.IFM.Domain.Trade.VerificationTests/TomasAI.IFM.Domain.Trade.VerificationTests.csproj --no-build --filter "FullyQualifiedName~Strategy.IntrinsicTime.MarketCondition"
```

ConfigurationDb and other affected storage projects receive focused commands determined at MC-00. Infrastructure-backed test projects run sequentially when they share fixed actor addresses or databases.

## 9. Known failure scenarios and required result

| Scenario | Function result | Durable workflow result | Projection | Later-stage dispatch |
|---|---|---|---|---|
| Healthy aligned opportunity | Completed Tradeable | Started / TradeSelection | Completed row | Exactly one Trade Selection |
| Session/event/liquidity/operations blocker | Completed NotTradeable | Completed / NoTrade | Completed row | None |
| Below strength/confidence | Completed NotTradeable | Completed / NoTrade | Completed row | None |
| Regime NoNewTrade or direction conflict | Completed NotTradeable | Completed / NoTrade | Completed row | None |
| Invalid command/config/hash | Failed | PipelineFailed when workflow command can be correlated | None | None |
| Corrupt mandatory provider metadata | Failed | PipelineFailed | None | None |
| Calculation invariant/exception | Failed | PipelineFailed | None | None |
| Projector exception | Failed | PipelineFailed | No successful completed row | None |
| PostgreSQL completion append exception | Failed | PipelineFailed | Possible idempotent observational orphan | None |
| Function hard timeout | Failed with `FailureCategory.Timeout` | TimedOut | None | None |
| Completed result expires before acceptance | Completed reply rejected | TimedOut | Completed Function row may exist | None |
| Lost Function reply | No workflow command received | Remains Started until lazy hard expiry | Function state depends on execution outcome | None |
| Matching duplicate after completion | Original Completed | At most one revision change | No duplicate side effects | At most one |
| Conflicting duplicate | Failed conflict | No valid continuation | None beyond original | None beyond original |
| Late result after timeout/supersession | Ignored/rejected | Existing terminal/new workflow unchanged | None from late worker | None |

## 10. Explicit non-goals

- Implementing Trade Selection, Order Composition, Risk Management, or Order Execution.
- Calibrating V1 defaults for profitability or production capital deployment.
- Adding a Configuration UI; PostgreSQL CRUD and tests are sufficient for this implementation.
- Adding ML/LLM authority or nondeterministic market classification.
- Adding manual Market Condition cancellation before a separate authorization/state-machine design.
- Redesigning all strategy parameter storage beyond the minimum reusable ConfigurationDb extension.
- Converting unrelated actor identities or refactoring unrelated market-data actors.
- Persisting full market snapshots where bounded evidence and hashes are sufficient.

## 11. Definition of done

Market Condition V1 is complete only when:

1. MC-00 through MC-22 are recorded Complete.
2. The FunctionActor topology is the only executable Market Condition topology.
3. Parameter configuration is immutable, versioned, stored in its own ConfigurationDb table, and frozen in workflow state.
4. ES futures and futures-option inputs are captured in one immutable snapshot.
5. Hard gates, classification, formulas, defaults, and reason codes match the specification exactly.
6. Completed-only projection/state ordering and idempotency match Regime Discovery conventions.
7. Tradeable continues exactly once to Trade Selection.
8. NotTradeable commits a normal NoTrade outcome and dispatches nothing later.
9. Failure, timeout, expiry, duplicate, restart, and late-result scenarios are fail-closed.
10. Unit, BDD, integration, and basic verification suites contain no skipped Market Condition placeholders and all pass.
11. Regime Discovery and Strategy Workflow regression suites remain green.
12. Documentation, actor conventions, query/read models, and observation behavior match the implementation.

## Appendix A — Gate deliverables summary

| Gates | Primary deliverable |
|---|---|
| MC-00 | Safe baseline and complete change inventory |
| MC-01–03 | Contracts, configuration storage, and immutable workflow inputs/outcomes |
| MC-04–06 | Frozen market snapshot, hard gates, and deterministic business result |
| MC-07–08 | Atomic completed-only Function implementation |
| MC-09–10 | Direct workflow handoff and Tradeable/NoTrade/failure transitions |
| MC-11 | Projection, queries, UI observation, and telemetry |
| MC-12–15 | Unit, BDD, integration, and basic verification qualification |
| MC-16 | Full regression and controlled completion |
| MC-17–19 | Input audit, full Regime decision consumption, and independent trigger/futures/options corroboration |
| MC-20–21 | Result schema V2 and advisory horizon/trade-family hints |
| MC-22 | Pairwise decision qualification and documentation closure |

## Appendix B — Gate execution record (2026-08-28)

This audit records implementation evidence against the gates as written. `Partial` means executable work exists,
but at least one mandatory step or exit condition remains unsatisfied. Market Condition must not be treated as
production-enabled while any gate is Partial or Blocked.

| Gate | Status | Evidence and remaining work |
|---|---|---|
| MC-00 | Complete | Dirty-tree isolation and legacy boundary inventory recorded. Baseline: solution build green; Trade Unit 156 passed; BDD 8 passed; Integrated 40 passed, 1 failed, 2 skipped; Verification 33 passed. |
| MC-01 | Complete | Execute Function identity/contracts added; legacy Start command and Processing event removed; C# legacy-reference scan is empty. |
| MC-02 | Complete | All nested V1 parameter records/defaults, defensive array copies, bounded validation, scale-independent canonical hashing, typed metadata/hash checks, and closed lifecycle table mapping are implemented. PostgreSQL enforces append-only content, no-delete, legal Draft-to-Published-to-Retired transitions, effective/future/retired selection, and ambiguity failure. The focused MC-02 matrix passes 18 unit and 8 PostgreSQL integration tests. |
| MC-03 | Complete | Fund/config/hash are frozen in workflow state and `NoTrade` is an explicit append-only outcome. |
| MC-04 | Complete | Live capture now falls back from deterministic seeded snapshots to a one-read production coordinator. Concrete adapters consume the registered current ES/VX contracts and hot quote/trade caches, exact one-minute ATR lineage, Securities option metadata joined to eligible hot quotes, the CME holiday/DST/early-close calendar, US economic-calendar rows, persisted five-minute VX history, Databento feed/cache health, and a typed IBKR readiness boundary. Because no IBKR connection authority exists in this repository, its registered default reports reliable `Unavailable` and therefore blocks trading without misclassifying missing authority as healthy. Startup wiring is present in both hosts; finite normalization, partial quote coverage, canonical aggregation/sealing/hash, direct-provider fallback, holiday/early-close, event classification, unknown-source failure, and fail-closed broker tests are green. |
| MC-05 | Complete | All eight hard gates execute in specification order; invalid provider contracts fail separately from reliable blockers; hard blockers prevent opportunity scoring; and below/equal/above tests cover every numeric threshold plus categorical blockers and reason ordering. |
| MC-06 | Complete | Classification, phase/volatility precedence, exact normalized features and contributions, penalties, six-decimal rounding, result invariants, stable evidence/reasons, exact bullish/bearish/range/transition/expansion/contraction vectors, threshold boundaries, and sequential/parallel byte equality are executable and tested. |
| MC-07 | Complete | Completed-only Function actor/state/repository/projector and exact/latest/history query foundation are implemented. |
| MC-08 | Complete | Effective deadline is the minimum of command, workflow, and frozen execution deadlines; exact-boundary timeout wins; caller cancellation remains distinct; late workers are cancelled and observed; and matching/conflicting duplicate, projection exception, persistence exception, and completed-only ordering are covered through unit and real actor lifecycle tests. |
| MC-09 | Complete | Workflow Realtime invokes the Function directly over request/reply and translates typed terminal replies. |
| MC-10 | Complete | Tradeable advances once; NotTradeable commits `NoTrade`; failure, timeout, expiry, duplicate, and late terminal commands fail closed in unit/BDD/runtime coverage. |
| MC-11 | Complete | Scylla exact/latest/history projection and bounded result payloads are exposed through read-only queries. Workflow observation correlates the accepted Market Condition terminal, flags projection/state notification orphans, and preserves NoTrade/failure/timeout detail. Function, snapshot, gate/calculation, projection, persistence, and continuation spans plus bounded outcome/reason/latency/source-age/expiry/strength/confidence/timeout metrics are registered for OTLP; identity values are excluded from metric labels. |
| MC-12 | Complete | Trade Unit is green at 315/315 with exhaustive snapshot aggregate, production-adapter boundaries, every hard-threshold boundary, exact golden vectors, result invariants, Function deadline/lifecycle, telemetry, query observation, architecture, serialization, and workflow regression coverage. No Market Condition test is skipped. |
| MC-13 | Complete | Trade BDD is green at 14/14, including Tradeable, NoTrade, and typed timeout flows. |
| MC-14 | Complete | The registered Workflow Realtime -> Market Condition Function -> Workflow Command topology passes through NATS, PostgreSQL event sourcing, ConfigurationDb, and Scylla. Daily/Weekly/Monthly success cross-checks payload/hash, Function result identity, workflow revision, reconstructed state, query observation, and exactly-once continuation. NoTrade is projected as explicit `NoTrade` and never dispatches Trade Selection; timeout is unprojected and terminal; injected projector and Function-state persistence failures prove both storage/dispatch barriers and observable notification-orphan detection. A matching retry returns the same completion without recapture, and a new host reconstructs that Function completion from PostgreSQL without capture or redispatch. The generic pipeline simulator remains on typed Execute requests and the collection is non-parallel. |
| MC-15 | Complete | `Strategy/IntrinsicTime/MarketCondition` now contains 19 deterministic business cases plus three `Verification`-tagged qualification cases: production snapshot aggregation/sealing, infrastructure-backed Daily/Weekly/Monthly success, and the combined NoTrade/timeout/projection/persistence/lost-notification/retry/restart matrix. The Verification assembly reuses the Integration scenario implementation so NATS, PostgreSQL Function state, ConfigurationDb, Scylla projection, workflow reconstruction, query payload/hash, and exactly-once assertions cannot drift. Full Verification is green at 55/55 with no skips. |
| MC-16 | Complete | Full build, core regressions, repeated focused verification, affected Storage/actor/serialization/MarketData suites, legacy-route scans, and actor-convention gates are complete. Changed-file formatting and `git diff --check` pass. The production API host's controlled live-trigger setting is enabled; the registered unavailable broker-readiness authority remains fail-closed and cannot authorize continuation. |
| MC-17 | Complete | Input ownership was re-audited after RD20–25. `RegimeDiscoveryDecision` is primary; exact ITI, frozen futures/options, session, event, health, and workflow observations retain independent corroboration/gate/scoring roles. |
| MC-18 | Complete | Direction, phase, decision quality, volatility change, structure, breakout, restrictions, conviction, agreement, and trend strength consume the expanded Decision contract with explicit evidence and safe legacy specialist fallback. |
| MC-19 | Complete | Trigger conflict remains explicit; futures/options quality independently gates and scores the opportunity and contributes to hint confidence. Hints cannot bypass any hard blocker. |
| MC-20 | Complete | `MarketConditionResult` schema V2 appends typed `OutputHints[]` at MessagePack key 34 without changing keys 0–33. Invariants require a bounded advisory hint on every new result. |
| MC-21 | Complete | Daily/Futures, Weekly/VerticalSpread, and Monthly/IronCondor mappings emit Preferred/Eligible/Avoid suitability after the primary decision. Function-envelope and live projected payload assertions use schema V2. |
| MC-22 | Complete | Unit, BDD, integration, and 12-case minimum pairwise verification cover all reasonable initial market-language/hint combinations. Design, specification, plan, and qualification evidence are synchronized. |

### Qualification evidence

| Command/suite | Result |
|---|---|
| `dotnet build TomasAI.IFM.sln --no-restore -m:1` | Passed on the final post-edit rerun; 0 warnings, 0 errors; 5 m 02.99 s |
| Trade Unit | 332 passed; 0 failed; 0 skipped; includes true schema-V1-shaped payload compatibility and schema-V2 hint invariants |
| Trade BDD | 22 passed; 0 failed; 0 skipped; includes four MC17–22 business scenarios |
| Trade Integrated | 46 passed; 0 failed; 2 unrelated pre-existing TradePlan skips; infrastructure actor suites run sequentially |
| Trade Verification | 79 passed; 0 failed; 0 skipped; 49 s on final rerun |
| Focused Market Condition Verification | Existing 22 cases plus the 12-case minimum pairwise decision/hint matrix; 0 failed; 0 skipped |
| Focused MC-02 Unit | 18 passed; defaults, canonical scale-independent hash, defensive arrays, and complete nested validation boundaries |
| Focused MC-02 ConfigurationDb PostgreSQL Integration | 8 passed; insert/exact round trip, publish, effective boundary, future exclusion, retire, ambiguity, invalid transitions, immutable payload, no-delete, corrupt hash/schema/identity, and closed table map |
| Focused Market Condition Storage Integration | 1 passed; exact/latest/history, duplicate upsert, payload/hash preservation |
| Broad Application.Storage Integration | 385 passed; 0 failed; 0 skipped; 7 m 48 s |
| Serialization | Framework Serialization Unit 11 passed; 0 failed; 0 skipped |
| MarketData Feed | Unit 489 and BDD 314 passed. Integration: 46 passed and four existing skips; the two former Futures EOD timeouts were fixed by using an isolated, deterministic trading-date scenario, then passed in three consecutive focused runs. |
| MarketData Analytics | Unit 944, BDD 464, Integration 48 passed; 0 failed; 0 skipped |
| MarketData/Securities dependencies | Securities Unit 11, BDD 2, Integration 14; MarketData Unit 102 and Integration 21; Framework MarketData Unit 46; DataBento Unit 123; Application MarketData Unit 82 all passed |
| Actor/transport dependencies | Domain Application Actor Unit 5, BDD 1, Integrated 1, NATS Unit 78, and NATS Integrated 54 passed. The former SPSC timeout was fixed with a full-fence waiter handshake and small-capacity lost-wakeup regression, then both concurrency cases passed in 20 consecutive runs. The Application Actor project is a host assembly with no discoverable tests. |
| Actor convention gates | Realtime 16, Command 36, Query 33, and Event 31 domain actors passed; stale expected inventories and Market Condition query helper parameter names were corrected |
| Legacy C# boundary scan | No `StartMarketConditionPipelineCommand` or `MarketConditionPipelineProcessingEvent` references |
| `git diff --check` | Passed; only existing LF-to-CRLF notices |
| Formatting verification | Changed-file `dotnet format --verify-no-changes` passed. Repository-wide verification reproduced extensive pre-existing whitespace/end-of-line findings in untouched files; no unrelated files were modified. |

### Readiness decision

MC-00 through MC-22 are closed. `TomasAI.IFM.Application.Api.Server` enables the qualified live ITI-trigger route,
while test hosts remain disabled unless a scenario opts in. This is controlled workflow enablement, not trading
authority: the registered IBKR readiness source deliberately remains fail-closed until a real broker connection
authority replaces it, and later trade-selection/order stages retain their own independent safety boundaries.
## PDR-01 through PDR-08 decision-reference amendment

Market Condition now supports the shared Pipeline Decision Reference design. Its Query actor handles
`GetMarketConditionDecisionReferenceQuery`; the deterministic generator preserves the twelve MC-22 pairwise anchors
and calculates every row with `MarketConditionCalculationModel`; the NATS client returns typed DTO arrays; and the
typed `MarketConditionDecisionReferenceCsvAdapter` exports those rows in the caller process with overwrite enabled by
default. Unit/contract, BDD, live-NATS integration, verification, CSV, and documentation gates are executable. The
shared authoritative record is `../../Docs/Pipeline-Decision-Reference-Queries-v1.0.md`.
