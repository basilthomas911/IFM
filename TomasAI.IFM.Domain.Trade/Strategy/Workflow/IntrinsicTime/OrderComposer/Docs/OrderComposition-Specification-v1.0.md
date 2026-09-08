# Order Composition Detailed Specification v1.0

| Item | Value |
| --- | --- |
| Date | 2026-09-07 |
| Status | Normative implementation specification; implementation and qualification pending |
| Scope | ES futures and verified European-style ES options on futures; one triggering Daily, Weekly or Monthly horizon |
| Runtime | .NET 10; completed-only mapped Function actor; broker-neutral one-unit candidate |
| Origin | [High-level design v0.1](OrderComposition-High-Level-Design-v0.1.md) |
| Prerequisites | [Prerequisite implementation plan v1.0](OrderComposition-Prerequisite-Implementation-Plan-v1.0.md) |
| Actor authority | [System actor conventions, section 13.3](../../../../../../Documents/system/Actor-Implementation-Conventions.md#133-functionactor-convention) |

SHALL denotes a requirement. Proposed names and wire keys describe work to implement, not existing APIs. This document does not certify production policies, market feeds, builder/risk capabilities or broker integration.

Implementation amendment, 2026-09-08 UTC: prerequisite preparation now has a mapped acceptance command and durable saved Start dispatch (workflow view key 30, legacy state key 26, Start key 17 evidence reference). Preparation schema 2 adds discovery identity at key 7 while retaining historical schema-1 hashes. These implemented keys must be preserved when the proposed Execute Function contract is added. Construction-policy schema 2 pins a finite reviewed `marketData` object (explicit dataset/root/date/scope/options/futures and option pricing-reference policies); version one remains immutable and omits it. The complete proposed composer contract below remains normative future work. See the [implementation record](OrderComposition-Prerequisite-Implementation-Record-v1.0.md) for the tested subset and remaining durable lifecycle work.

## 1. Authority and alignment decisions

The latest dated amendments take precedence over historical body text in these sources:

| Source | Requirement adopted |
| --- | --- |
| [Trade Selection design](../../TradeSelection/Docs/TradeSelection-High-Level-Design-v0.1.md), [specification](../../TradeSelection/Docs/TradeSelection-Specification-v1.0.md), [implementation plan](../../TradeSelection/Docs/TradeSelection-Implementation-Plan-v1.0.md) | Exact authorized deployment/strategy/structure/variant/product; twelve variants on any supported horizon; frozen evidence; committed business-ID reservation before composition; one-unit construction |
| [Market Condition design v0.4](../../MarketCondition/Docs/MarketCondition-High-Level-Design-v0.4.md), [specification v2](../../MarketCondition/Docs/MarketCondition-Specification-v2.0.md), [implementation plan v2](../../MarketCondition/Docs/MarketCondition-Implementation-Plan-v2.0.md) | One descriptive market-only assessment; no family preference or exact option-chain tradeability decision |
| [Regime Discovery design/specification](../../RegimeDiscovery/Docs/Regime-Discovery-Specification-v1.0.md), [implementation](../../RegimeDiscovery/Docs/Regime-Discovery-Implementation-v1.0.md), [atomic workflow plan](../../RegimeDiscovery/Docs/Regime-Discovery-Atomic-Workflow-Implementation-Plan-v1.0.md) | Accepted typed Decision V2, trigger lineage and restrictions; no recalculation or additional horizon request; latest Function amendments supersede earlier topology |
| [ConfigurationDb catalog design](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md), [implementation](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Implementation.md) | Existing normalized graph, exact versions/hashes, deployment-level parameter roles and trusted capabilities |
| [Actor conventions](../../../../../../Documents/system/Actor-Implementation-Conventions.md) | Five frozen maps, list-extension validation, typed context/policy, mapped Complete/Fail handlers, Model calculations, shared boundary serialization |
| [Stage 4 pricing specification](../../../../../../Documents/system/Market-Data-Resiliency-Stage-4-Pricing-Specification-v1.0.md) | Daily FMP Treasury, trading-day tenor buckets without interpolation, verified continuous-rate conversion, contract-specific fractional time, publication freshness and Failed for unusable required pricing inputs |

This specification supersedes the following conflicting Order Composition HLD statements:

1. Daily/Weekly/Monthly do not imply Future/Vertical/Condor respectively. All twelve variants may be deployed on any supported horizon: 36 positive variant/horizon fixtures.
2. Family membership is provenance, not execution permission or an independent policy resolver. Reuse deployment bindings; do not introduce a second family-policy table or arbitrarily choose a source family.
3. Compose exactly one normalized strategy unit. Portfolio Risk Management determines final positive integer units and financial approval. Composition liquidity capacity is only a sizing constraint/hint.
4. Support long/debit and short/credit iron condors, independently balanced/bullish/bearish.
5. Use the shared Function lifecycle. Do not persist/publish Function failures, append Processing events, re-emit terminal events or create a composition Command/Realtime actor chain.
6. Execution deadlines are mandatory and implemented through typed policy dispatch. Cancellation and replay follow the base convention.
7. Consume actual descriptive Market Condition fields. Unavailable IV/skew/term-structure observations are not invented or assigned zero.
8. Admit only verified European-style ES futures options. All option legs must qualify; no American-style approximation or admission of stock/cash-index options. Qualify actual series, not the ES root or workflow horizon.

The 2026-09-07 pricing alignment removes this document's earlier engineering zero-rate interpolation, fixed ACT/365F assumption and missing-pricing NoCandidate treatment. The Stage 4 pricing specification governs those requirements. The prerequisite plan distinguishes readiness to write the full implementation document from offline runtime completion and live-provider qualification.

No upstream calculation receives composition policies. A change to final sizing ownership requires a separate versioned design change.

## 2. Purpose, ownership and invariants

The sequence remains RegimeDiscovery -> MarketCondition -> TradeSelection -> OrderComposition -> RiskManagement -> OrderExecution. Composition answers: which exact one-unit order expresses the already-selected intent within its frozen policy and market evidence?

| Owner | Responsibility |
| --- | --- |
| Workflow Command actor | Durable stage state, dispatch intent, result acceptance, stop/expiry and continuation |
| Workflow Realtime extensions | Bounded preparation, direct Function request/reply and deterministic workflow commands |
| ConfigurationDb / Portfolio | Exact catalog/policy evidence; Fund permission and business-ID reservation |
| Composition Function | Mapped dispatch with the shared completed-only lifecycle |
| Composition Model | Pure parameter resolution, candidate generation, valuation, economics, filtering, ordering and evidence |
| MarketData / Reference | Authoritative definitions and one coherent bounded snapshot |
| Portfolio Risk Management | Current risk, final units, financial limits and atomic risk authorization |
| OrderExecution / adapter | Approved envelope, emulator/account readiness, broker identifiers and submission |

OC-I01: Trigger, workflow, accepted selection, deployment, policy and result SHALL share one horizon. Observation windows, DTE and option-expiration classes are different concepts.

OC-I02: Portfolio/Fund, mandate/assignment revision, deployment, strategy, structure, variant, Side, Bias, PremiumMode and product SHALL equal the accepted Selected intent. No alternate intent search is permitted.

OC-I03: UnitQuantity=1: one futures contract or one combo with ratios 1:1 or 1:1:1:1. No naked, mixed-expiry, ratio-spread or independently submitted option legs in V1.

OC-I04: Identical frozen inputs, model versions and evaluation instant SHALL produce identical economic content, ordered evidence, selected legs and hashes. Duration, retrieval order and current clock do not affect calculation.

OC-I05: NoCandidate is completed evaluation; Failed is technical/contract failure. Neither starts Risk Management. Composed requires authoritative workflow acceptance.

OC-I06: A committed Fund composition reservation allocates one OrderId and one primary TradeId, not capital or broker IDs. Never allocate a TradeId per option leg.

## 3. Existing baseline and required additions

Verified repository boundaries:

- `StartOrderCompositionPipelineCommand` keys 0-16 include AcceptedSelection (14), SelectionBinding (15) and Reservation (16), on the historical Command/Start route.
- Generic Processing/Completed/Failed contracts and workflow Complete/Fail/Timeout handlers exist. Generic `CompleteOrderComposition` currently proceeds to RiskManagement without validating the proposed typed candidate.
- `SelectionConstructionPolicy` schema 1 is a strict JSON reader for existing OrderComposition pipeline parameters: MaximumLegs, DTE bounds, wing bounds, DeltaUnits and MaximumDeltaTolerance only.
- Envelope typed slots are 8 RegimeResult, 9 AssessmentResult and 10 SelectionResult. Shared MessagePack options and measurement support exist.
- The existing custom Black-76 implementation lives in [OptionModel](../../../../../../TomasAI.IFM.Framework.OptionPricer/Black76/OptionModel.cs) and [OptionCalculator](../../../../../../TomasAI.IFM.Framework.OptionPricer/Black76/OptionCalculator.cs).

Required additions: typed composition contracts, complete rules/capability validation, Models, Function/context/state/projector/query, prepared dispatch persistence and guarded acceptance. Scaffolding is not a qualified composer. Preserve historical keys and records; do not silently repoint the old Start subject or activate both routes.

The [prerequisite implementation record](OrderComposition-Prerequisite-Implementation-Record-v1.0.md) documents pricing/reference contracts, explicit-T calculations, qualified temporary worker chains, supervised snapshot transport and immutable Scylla market capture storage. Durable business ownership, the mapped workflow preparation/acceptance transition and the composer are not complete. Follow the [composer implementation plan](OrderComposition-Implementation-Plan-v1.0.md) for OC-01..08, including the application-to-domain snapshot adapter and remaining nested wire manifests. A stored market capture is not an accepted workflow Execute request.

## 4. Catalog and policy binding

### 4.1 Exact identity

Reuse accepted `TradeSelectionResult`, `TradeSelectionBinding`, selected `SelectionCandidateBinding`, `CatalogKey(Kind,Id,int Version)` and original hashes. Verify Deployment.Parent=Strategy; Variant.Parent=Structure; Strategy.Structures includes Structure; Deployment.Variants includes Variant. ProductId/symbol/exchange/currency/horizon match deployment and Fund authority.

Retain every Strategy.Families membership in canonical order without duplicating candidates. Schema-3 assignment TradeTemplateId/Version still mean Deployment GUID/version. AssignmentVersion remains the separate positive long Fund revision. Checked conversions apply where policy versions cross int/long APIs.

Exactly one pipeline binding of kind OrderComposition must match the assignment's composition profile ID/version/hash. Preserve its stored Role; do not assume a fixed role string for that existing binding. Resolve capabilities by trusted role/code/version registration, never display names. All resolution after acceptance uses frozen evidence, not GetLatest or a fresh family lookup.

Different family contexts needing different policies use distinct deployments and exact Fund assignments. No new family-owned policy schema is required.

### 4.2 Preserve the pipeline policy; add detailed catalog rules

Keep `SelectionConstructionPolicy` schema 1, its strict reader and hash unchanged. Do not add fields to its JSON or reinterpret old values. These constraints are the outer limits; futures treat option-only DTE/wing fields as inapplicable.

Add one specialized deployment parameter Role `OrderCompositionRules` (ordinal), pointing to a catalog ParameterSet and exact ParameterSchema parent with existing content hashes. Its typed values implement the new rules schema below. Selector snapshots already retain specialized parameter/schema evidence; add role validation without changing existing hash algorithms.

The rules object contains one complete entry per allowed Variant key. This uses deployment-level binding, not an assumed per-variant pipeline-binding column. Missing entries, duplicates and family/default fallback fail. Shared rule sets may serve deployments only when each exact allowed variant set is fully covered and compatible.

Existing engineering deployments lacking detailed rules remain readable but are not composition-ready. Publish new versions and explicitly update reviewed assignments/activations; do not mutate published data or auto-publish startup defaults.

### 4.3 Detailed rules contract

`OrderCompositionRules` requires SchemaVersion=1, AlgorithmVersion, PricerVersion, SupportedHorizon, InstrumentRoot, Currency, Limits, MarketPolicy, ReferencePolicy, ExecutionPolicy and VariantRules. Identity/version come from the catalog ParameterSet/Schema. Each variant entry requires:

- Exact VariantKey, StructureKey, BaseParameters and HardBounds.
- AdjustmentRules, AllowedWidths, TargetNetDelta, BalanceTolerance, DeltaUnits and RequireSymmetricWings.
- Eligibility, fixed ranking-version identifier, product-discriminated FuturesContractPolicy or OptionExpiryPolicy.
- Fee/slippage reserves and applicable risk-distance requirements.

Unknown properties, incomplete entries, nonfinite numbers, implicit units and conflicting topology fail validation. Option-only fields are absent for futures under a discriminated schema.

`CompositionBinding` retains exact selected graph/provenance, pipeline policy, detailed rules/schema snapshots, builder/validator/data/pricer versions, FrozenAtUtc, ValidUntilUtc and BindingSha256. It references shared selection authority instead of inventing another permission graph. Required references resolve inside the frozen request or a durably pinned immutable artifact, never latest data.

Effective bounds are the intersection of pipeline constraints, exact variant settings and detailed rules. Empty intersection is invalid configuration. Detailed rules cannot broaden any outer constraint. Zero-width option placeholders cannot qualify publication.

## 5. Complete engineering defaults

These explicit values are reproducible test hypotheses, not optimized trading parameters. Authoring functions materialize complete values and identities. Runtime never fills missing fields from this table. Production promotion remains explicit.

| Parameter | Engineering baseline | Units / constraint |
| --- | --- | --- |
| Execution / loading budget | 5000 / 5000 | Milliseconds, each 1..30000 |
| Candidate lifetime | 2000 | Milliseconds; all evidence expiries also apply |
| Quote age / cross-leg skew | 1000 / 250 | Milliseconds; skew includes linked forward |
| MinimumDisplayedSize | 1 | Contracts on each required quote side |
| MaximumParticipationFraction | 0.10 | Capacity=floor(min(size/ratio)*fraction) |
| Underlying / option-leg / combo max spread | 4 / 8 / 16 | Ticks from qualified definition/combo rules |
| Target DTE: Daily / Weekly / Monthly | 30 / 45 / 60 | Elapsed UTC days, not expiry classes |
| Allowed DTE: Daily / Weekly / Monthly | [7,60] / [14,90] / [21,120] | Inclusive; intersect pipeline constraints |
| AllowedWidths | [5,10,15,20] | Index points, on actually listed strikes |
| Vertical target abs delta / tolerance | 0.35 / 0.10 | Purchased leg for debit, sold leg for credit |
| Condor inner put/call abs delta targets | 0.20 / 0.20 | Inner strike positions; side follows variant |
| Net delta target / balance tolerance | Exact variant values | Example 0 or +/-0.15, tolerance 0.05; no fallback |
| MinimumCreditToWidth | 0.10 | Credit only |
| MaximumDebitToWidth | 0.60 | Debit only; condor denominator=min wing width |
| MinimumCreditTicks | 1 | Combo ticks |
| MinimumPayoffRewardToRisk | 0.10 | Bounded payoff after costs, not expected return |
| Midpoint-to-natural fraction | 0.50 | Formula in section 11 |
| MaximumAdverseMoveTicks | 0 | Default execution envelope fixes proposed limit |
| FeePerContract / SlippageTicksPerLeg | USD 2.50 / 1 | Engineering reserves, not exchange fee claims |
| Futures planned / stress distances | 20 / 100 | Index points; not guaranteed stop loss |
| Futures roll buffer | 120 | Hours before last trading instant |
| AdjustmentRules | Empty | No fabricated IV/skew features needed for baseline |

Author MaximumLegs/DeltaUnits/width bounds consistently with the existing exact pipeline policy. Symmetry and bias remain selected variant constraints. Candidate quantity is always one unit; `LiquidityCapacityUnits` is a non-authoritative downstream constraint and must be >=1. At participation 10%, displayed size 1 alone is insufficient; size 10 is needed for a one-unit 1:1 combo. Never round fractional capacity upward.

Composer quote limits may tighten the Stage 4 offline age/skew ceilings (5000/2000 milliseconds). Apply the stricter applicable limit and freeze both policy versions; the Stage 4 qualification wait ceiling of 10000 milliseconds is further bounded by remaining workflow/request lifetime. None of these fixture values is live-provider acceptance. Elapsed-day DTE bounds do not override the pricing support limit of fewer than 90 remaining exchange trading days; validate supported expiry coverage separately.

## 6. Bounded parameter resolution

Model resolution accepts typed immutable inputs only. Operations: Set, Add, Subtract, Multiply, Minimum, Maximum. Predicates use a closed allowlisted grammar of comparison/equality/membership and All/Any groups, bounded to depth 8 and 64 leaves per rule. No scripts, reflection, arbitrary paths, external queries or references to another resolved parameter in V1.

Inputs may include accepted Decision direction/phase/strength/volatility/structure/confidence; assessment ConditionType, VolatilityBehavior, LiquidityCondition, SessionState, EventRiskState, StressState, TriggerAlignment, DataQuality/restrictions; actual trigger evidence; and qualified composition snapshot features. IV/skew features require explicit available source/unit/version evidence and are not presumed to exist in Market Condition.

Optional missing input makes a rule NotApplied(InputUnavailable), never zero. A mandatory feature absent from the contract/schema is Failed.Contract. Correctly represented but unavailable required market/pricing data yields Failed with a structured input-specific cause; it is not a completed economic rejection.

For each parameter: start at explicit base; apply rules by ascending Priority then unique ordinal RuleCode; record input, before/after and reason; clamp to intersected bounds; quantize to the allowed grid without leaving bounds. No grid point means invalid configuration. Nearest ties use ToEven except financial-side rounding in section 11. Side, right, ratios, identity, permission and UnitQuantity are not adaptive.

Record applied/skipped rules, unclamped/resolved values, clamps, units and exact policy keys. Bound to 64 rules and 64 resolved parameters for the selected variant. Unknown schema/operation/field fails before calculation. No safeguard relaxation or second attempt with different policy.

## 7. Snapshot and durable preparation

### 7.1 Capture before Function dispatch

After committed business-ID reservation, a workflow Realtime extension captures one bounded immutable snapshot through proposed `IMarketCompositionSnapshotProvider`. It sends a mapped workflow preparation command containing the proposed complete execution request. Workflow commits the accepted request/fingerprint before Function invocation.

Preparation is a substate (PendingSnapshot, Ready, Stopped), not another decision operator. Acceptance compares WorkflowId, current stage/revision, selected result ID/hash and reservation hash. Exactly one preparation wins. It advances revision once, freezes that new revision as Execute.InputWorkflowRevision, and atomically records the request and durable dispatch intent. The request contains bounded required workflow context, never a recursive copy of state containing itself.

Before preparation acceptance, recovery may recapture: no Function input is yet authorized. Once Ready, every retry uses the identical saved request, snapshot, IDs and timestamps. A race cannot replace accepted input. Before dispatch, verify authoritative workflow state; stopped/stale workflows cannot send new work.

Append nullable preparation/dispatch fields at the next unused workflow view/state keys; current view ends at key 29 SelectionDispatch. Do not reuse keys 27-29. Record both manifests in OC-01. New command IDs use existing deterministic workflow schemes.

### 7.2 Snapshot schema and source quality

`MarketCompositionSnapshot` requires SchemaVersion, SnapshotId, TargetHorizon, ProductId/Root/Exchange/Currency, CapturedAtUtc, EvaluatedAtUtc, ValidUntilUtc, Scope, Completeness, Integrity, Provider/AdapterVersion, SequenceEpoch, FuturesDefinitions, OptionDefinitions, Quotes, ReferenceInputs, OptionalFeatures, Issues and SnapshotSha256.

Scope enumerates requested futures IDs, expirations, rights and strike ranges. Complete coverage of that bounded scope is required; a first page or first N strikes is not a complete chain. A policy-limited region must contain every combination allowed by its qualified coverage algorithm. Overflow fails explicitly, never truncates ranking.

Definitions retain authoritative instrument/provider IDs, raw symbol, root, exchange/currency, class/right/strike, exact expiration/last-trade instant, linked underlying future, multiplier, tick-rule ID, exercise style, settlement/delivery, calendar/day-count mappings and source/version/hash. Combo legs share expiry, underlying, compatible settlement/exercise conventions, multiplier and currency. Admit only verified European-style ES futures options. Exclude known American series with evidence; unknown style never qualifies, and unresolved classification that prevents proving requested-scope completeness is Failed.ContractMetadataUnavailable. A complete classified scope containing no permitted European contracts may yield NoCandidate.NoEligibleEuropeanContract. Explicitly pricing an unsupported contract is Failed.PricingModelUnsupported. Preserve excluded raw definitions; no destructive universe cleanup is implied.

Quotes retain InstrumentId, Bid/Ask, BidSize/AskSize, observed/received UTC times, sequence/epoch, source and status. Required crossed quotes, negative sizes, invalid values and future timestamps produce structured pricing/data failure; locked quotes may pass. Supported option premiums must be positive. Last alone is not executable pricing. Conflicting duplicate IDs or invalid references are contract failures.

A frozen reference snapshot contains applicable calendar/session facts, the daily Treasury snapshot and selected-tenor conversion evidence, fee/tick rules, versions/hashes and validity. Record source-series convention, publication policy, trading-day count, selected tenor, original percentage, continuous annual decimal and FlatSelectedCmtProxy modeling version. DownloadLog completion is provenance only: verify actual Treasury data/rate conventions and calendar coverage. No provider/broker query occurs in the calculation.

Quote age at EvaluatedAtUtc must be in [0,maxAge]; skew=max-min quote time includes the linked forward. Mixed epochs, gaps and incomplete scope remain explicit statuses. Options use their definition-linked underlying futures quote, not blindly the ITI trigger contract. Different candidate expirations may link to different futures; each combo uses one underlying.

Missing/stale/invalid required quotes or reference/pricing inputs, incoherent epochs and incomplete requested chain scope yield Failed with typed causes. A complete trustworthy scope with no eligible contracts, or an authoritatively closed session, may yield NoCandidate. Corrupt request shape/references, absent required payloads and provider execution exceptions also yield Failed. Preparation failures become workflow failures through mapped handlers before Function dispatch; they do not create Function state. No solver/data failure may be silently dropped to select a winner from an incomplete ranking scope.

## 8. Execute contract and identity

Introduce `ExecuteOrderCompositionPipelineCommand : ICommand<OrderCompositionExecutionId>`, ActorType.Function, mailbox OrderCompositionPipelineFunction, verb Execute, existing bounded context, PostEvents=false. Allocate new numeric error IDs using the registry during OC-01; do not reuse unrelated historical errors.

Execution identity combines WorkflowEntityId, WorkflowId(UUIDv7), InputWorkflowRevision; use validated immutable value semantics and canonical route/stream formatting consistent with the selector. Default IDs/nonpositive revisions fail.

Proposed first wire manifest:

| Key | Field |
| ---: | --- |
| 0 | SchemaVersion=1; missing zero invalid |
| 1 | CommandId / InvocationId |
| 2 | Subject |
| 3 | PostEvents=false |
| 4 | EntityId |
| 5 | ErrorCode |
| 6 | RouteTo |
| 7 | InputWorkflowRevision |
| 8 | WorkflowContext |
| 9 | TriggerEvent |
| 10 | CorrelationId |
| 11 | CausationId |
| 12 | RequestedAtUtc |
| 13 | ExpiresAtUtc |
| 14 | EvaluatedAtUtc |
| 15 | AcceptedSelectionEnvelope |
| 16 | SelectionBinding |
| 17 | Reservation |
| 18 | CompositionBinding |
| 19 | MarketSnapshot |
| 20 | InputSha256 |

WorkflowContext contains Portfolio/Fund/version, workflow/entity/stage/revision, original Portfolio snapshot revision/hash, accepted selection source and handoff evidence, workflow deadline and trigger identity. Upstream payloads already retained in selected context are reused; duplicate representations must agree exactly.

InputSha256 covers the complete canonical semantic request with its own field empty, including lineage and frozen times. Arrival diagnostics are excluded. Same InvocationId/different input is a conflict. ResultId and completion event Id equal InvocationId. Snapshot ID is independently frozen during preparation.

Model entry `IOrderComposer.Calculate(CompositionCalculationInput,CancellationToken)` returns typed `CompositionCalculationOutcome`; it has no actor/transport/storage/clock/service-locator dependency. Constructor-inject a qualified deterministic pricer. Product composers and resolver are local Model components, not actors.

## 9. Actor, validation and extension conventions

```text
OrderComposer/
  Function/Actor/OrderCompositionFunctionActor.cs
  Function/Actor/OrderCompositionFunctionContext.cs
  Function/ExecuteOrderCompositionPipeline.cs
  Function/CompleteOrderCompositionPipeline.cs
  Function/FailOrderCompositionPipeline.cs
  Function/ResolveOrderCompositionExecutionPolicy.cs
  Function/State/
  Function/Projector/
  Model/OrderComposer.cs
  Model/CompositionParameterResolver.cs
  Model/EsFuturesOrderComposer.cs
  Model/EsVerticalSpreadOrderComposer.cs
  Model/EsIronCondorOrderComposer.cs
  Query/
  Docs/
```

Messages/results/list-validation contracts belong in Trade.Shared; cross-domain catalog/Portfolio DTOs remain in Strategy.Contracts.Shared. No Storage->Trade->Storage dependency cycle. Calculations stay under Model, not deadline or generic helper classes.

`OrderCompositionFunctionActor` SHALL inherit `BaseEventSourceFunctionActor` with typed request/state/terminal events. Inject `IOrderCompositionFunctionContext`; register it and `IFunctionActorContext<OrderCompositionFunctionActor>` against the same singleton in API/test hosts. Context exposes typed logger, TimeProvider, repository, synchronous projector and IOrderComposer. No Typed(), repeated cast or second instance.

| Map | Contract |
| --- | --- |
| `_parseMap` | Frozen ordinal Execute verb -> one typed decode; ParseMessage only calls ParseMappedFunction |
| `_validationMap` | Frozen exact command Type -> Func<ICommand,List<ValidationError>>; may be instance-owned for capabilities |
| `_receiveMap` | Frozen exact command Type -> Execute extension returning awaited ValueTask FunctionResult |
| `_executionPolicyMap` | Frozen exact command Type -> typed ResolveOrderCompositionExecutionPolicy extension |
| `_eventMap` | Frozen exact completed/failed event Types -> separate Complete/Fail extensions |

Request coverage across parse/validation/receive/policy maps must agree. No assignable fallback, type-name strings, reflection-discovered dispatch, actor switches or direct terminal-handler bypass. Actors contain mappings and base dispatch only.

ValidateAsync performs argument/cancellation checks, calls inherited ValidateMappedCommand, then returns ValueTask.CompletedTask. Ordered list extensions append to and return the same List<ValidationError>:

1. CommandId, route and execution/workflow identity.
2. Schema, required payloads, UTC times and limits.
3. Trigger, accepted regime/assessment/selection and matching horizon.
4. Frozen catalog/policy/schema/capability hashes and variant topology.
5. Portfolio/Fund permission, assignment and committed reservation.
6. Snapshot shape, definition references, statuses and bounds.
7. Cross-input consistency and canonical request fingerprint.

Use FluentValidation through list adapters for structured payloads. Null nested values accumulate errors; no exception-catching wrapper substitutes for rules. Market feasibility remains calculation work. Validation performs no remote reads, pricing, reservation or persistence.

Execute calls the Model and passes typed outcomes to the actor-supplied event dispatcher. It never calls Complete/Fail extensions directly. HandleFunctionEvent only calls DispatchMappedFunctionEvent. Complete constructs/validates typed completion and output bounds; Fail handles calculation/lifecycle/conflict/transport failures, including null request on parse failure.

## 10. Lifecycle, deadline and replay

Base sequence: parse -> validate -> load -> matching replay or execute -> synchronous completed projection -> completed PostgreSQL append -> typed reply. Composed and NoCandidate both persist completed events. No Function Processing/Failed append/publication, command-audit ID reservation, outbox, durable Function projector or terminal re-emission.

The actor policy override is only:

```csharp
protected override FunctionExecutionPolicy ResolveExecutionPolicy(
    ExecuteOrderCompositionPipelineCommand request, FunctionFailureStage stage)
    => DispatchMappedExecutionPolicy(request, stage, _context, _executionPolicyMap);
```

The extension returns the typed domain clock plus Loading=clock.UtcNow+frozen loading budget; Execution/Projection/Persistence=request.ExpiresAtUtc. At preparation, ExpiresAtUtc=min(workflow deadline, reservation expiry, accepted selection/binding validity, RequestedAtUtc+execution budget). The loading budget permits committed replay after expiry, never new work or expired continuation.

Base owns timers, linked cancellation, exact-boundary timeout and observing late workers. No actor deadline arithmetic, GetFunctionDeadline/FunctionTimeProvider override, duplicated timer race or deadline helper. Caller cancellation propagates distinctly; workflow owns durable stop/timeout. An already-started storage write may commit after cancellation; reconcile authority rather than assume rollback.

Base creates Committed/Replayed event-map contexts. Complete observations return the exact original event reference; success telemetry increments on commit, replay separately. Observer exceptions cannot replace durable completion. No actor commit/replay overrides or callback-context construction.

| State / request | Behavior |
| --- | --- |
| Absent, valid current input | Calculate frozen input |
| Absent, invalid input | Typed failure before load/project/save |
| Matching completed request | Return same completion without capture/calculation/project/save |
| Conflicting completed request | OC.INVOCATION.CONFLICT; preserve original |
| Calculation/projection/append failure | Non-durable Function failure; workflow owns recovery |
| Concurrent initial append | Expected version zero fences winners; loser cannot claim an uncommitted result |

Technical recovery uses the identical saved request. NoCandidate or risk rejection does not trigger business recomposition, quantity changes or alternate strategy selection. New economic inputs require a separately authorized new invocation and hash.

## 11. Deterministic construction and pricing

### 11.1 Bounded algorithm

Validate snapshot usability; resolve parameters; enumerate eligible definitions canonically; generate every permitted combination in the qualified bounded scope; value unique pricing inputs once; calculate signed prices/Greeks/payoffs; apply hard gates; rank eligible combinations; choose one or NoCandidate; validate/hash output.

Hard limits: 8 expirations, 512 option definitions/quotes, 16 futures definitions, 4096 generated combinations, 64 rules, 64 resolved parameters, 128 aggregate rejection groups, 256 diagnostic samples and 4 legs per candidate. Overflow is Failed.Limit, never a winner from truncated enumeration. Count all rejections; deterministic diagnostic samples explicitly report omissions. Do not truncate selected evidence or authority.

Invocation-local valuations cache exact instrument/input/model hashes. No actor-held mutable chain. Sequential calculation is baseline; future concurrency requires benchmarks and deterministic ordering. No Task.Run per leg or fire-and-forget work; check cancellation between bounded batches.

### 11.2 Topology and instruments

Validate actual catalog legs/variant overrides against qualified topology. Runtime dispatch uses a frozen capability role/code/version map to Model components, not family display text.

| Variant | Lower to upper strikes / sides |
| --- | --- |
| LongFuture / ShortFuture | Buy / sell one future |
| BullCallDebit | Buy lower call; sell upper call |
| BearCallCredit | Sell lower call; buy upper call |
| BullPutCredit | Buy lower put; sell upper put |
| BearPutDebit | Sell lower put; buy upper put |
| ShortBalanced/Bullish/BearishIronCondor | Buy put K1; sell put K2; sell call K3; buy call K4 |
| LongBalanced/Bullish/BearishIronCondor | Sell put K1; buy put K2; buy call K3; sell call K4 |

Condors require K1<K2<K3<K4. Options share expiry/underlying and ratio 1 per leg. Side is independent of Bias; equal wings do not prove delta balance. Debit vertical target delta refers to its purchased leg, credit vertical to its sold leg. Condor target deltas refer to inner strike positions regardless of long/short sides.

Futures exclude expired/unqualified contracts and those at/inside roll buffer. Default contract ranking is earliest eligible last-trade time then instrument ID ordinal, within exact permitted root/policy and current snapshot eligibility. Elect this single futures contract before general candidate ranking, so spread scoring cannot silently select a later contract. Never guess roll identity from raw symbols.

Options DTE=(ExpirationUtc-EvaluatedAtUtc).TotalSeconds/86400 with inclusive bounds; no integer-day truncation. Candidate validity must end before expiration/last trading. Widths must be explicit allowed values present in real strikes; no synthetic definitions. Settlement, exercise and tick conventions must agree.

### 11.3 Black-76 adapter

Wrap existing code behind IFuturesOptionPricer, with exact model/build version, units and deterministic failure semantics; no new pricing formulas. Pin the verified European exercise style, linked futures instrument/price, strike, exact expiration, product-specific calendar/day-count mapping and positive fractional year value T, annual decimal volatility, continuously compounded annual rate and right. Reuse OptionModel's explicit-T capability and add a compatible explicit-T OptionCalculator path; preserve the legacy DateOnly API. ACT/365F is permitted only through an explicit product mapping, never a global fallback. Reject expired live candidates before the engine's intrinsic-value behavior. Qualify native/managed behavior; no unrecorded engine fallback.

Use valid supplied IV with compatible context/provenance or an explicitly enabled deterministic inversion from frozen midpoint with pinned tolerance/iteration policy. Missing/failed IV, rate, forward or required quote cannot become zero Greeks or NoCandidate. Return a structured Failed pricing cause; unexpected pricer exceptions are Failed.Calculation. Successful valuation outside a configured delta/premium/payoff limit is an economic candidate rejection.

Engineering adapter settings preserve the existing calculator baseline: midpoint inversion enabled, absolute premium residual tolerance 1e-10 points, maximum 100 iterations, maximum accepted IV 4.0 annual decimal, existing no-arbitrage bound checks and no approximate-success fallback on nonconvergence. Normalize finite theoretical/Greek outputs to decimal at 12 fractional digits with ToEven before deterministic comparisons/hashes; retain raw outputs as optional diagnostic evidence only. Solver tolerance and output normalization are distinct policies. Validate adapter support and golden vectors before publication. Freeze the normalization version with PricerVersion; never change numerical behavior in place.

Rate inputs follow the Stage 4 pricing specification: remaining exchange trading days 0..29 select OneMonth, 30..59 TwoMonth, 60..89 ThreeMonth; 90 or more fail TreasuryHorizonUnsupported when pricing is requested. No interpolation, extrapolation or alternate-tenor fallback. Preserve elapsed-day strategy DTE separately from this count and from pricing T. For verified CMT nominal semiannual percentage P, convert with r_cc=2*ln(1+(P/100)/2); record FlatSelectedCmtProxy/v1 rather than claiming a bootstrapped zero rate. Unknown convention, missing required tenor, stale/unobservable curve or failed conversion yields Failed with its typed cause. Publication-aware freshness is mandatory; DownloadLog success or the existing 14-day retrieval window is insufficient evidence.

### 11.4 Signed prices and rounding

Let s_i=+1 buy/-1 sell and ratios r_i=1. SignedDebit is positive cash paid, negative cash received, in index points per normalized unit.

```text
NaturalDebit = sum(s_i*r_i*(ask_i if buy else bid_i))
MidDebit     = sum(s_i*r_i*((bid_i+ask_i)/2))
BestDebit    = sum(s_i*r_i*(bid_i if buy else ask_i))
ComboSpread  = NaturalDebit-BestDebit
RawLimit     = MidDebit + MidpointToNaturalFraction*(NaturalDebit-MidDebit)
LimitDebit   = floor(RawLimit/ComboTick)*ComboTick
WorstDebit   = floor((LimitDebit+AdverseMoveTicks*ComboTick)/ComboTick)*ComboTick
```

Rounding cannot increase cash paid or reduce credit. Reject values outside allowed bounds or with wrong premium sign. Debit requires LimitDebit>0; credit requires LimitDebit<0, Credit=-LimitDebit. Zero premium is ineligible. Require nonnegative spreads, consistent midpoint/natural and WorstDebit<=NaturalDebit; worst price must satisfy every economics/premium gate. Theoretical values never replace quotes.

ComboAction=OpenNormalizedUnit; exact leg sides define exposure. BAG Buy/Sell conventions belong in the broker adapter with sign round-trip tests. Futures use analogous outright prices, buy limits rounded down and sell limits up, with side-specific worst bounds. Negative futures prices are outside this initial ES/Black-76 qualification; never clamp them positive.

## 12. Greeks and one-unit economics

UnderlyingEquivalentDelta=sum(s_i*r_i*delta_i*optionMultiplier_i/referenceFutureMultiplier). Record all other Greek units explicitly: vega per 1.00 annual volatility, theta per year and gamma per price-point change unless a pinned adapter conversion states otherwise. Validate call/put delta signs; never scale percentages twice.

Balanced requires target=0 and abs(netDelta-target)<=tolerance. Bullish additionally requires strictly positive actual net delta and positive target; Bearish strictly negative delta/target. Tolerance cannot admit wrong sign. Futures delta is +1/-1. Long-condor bias must be measured from long-condor legs, not copied from short-condor wing heuristics. Required unavailable Greeks produce Failed; available Greeks outside policy reject that construction economically.

For common multiplier M and worst signed premium D:

```text
Payoff(F) = M * (sum(s_i*r_i*intrinsic_i(F)) - D)
CostReserve = sum(abs(r_i)*FeePerContract_i)
            + sum(abs(r_i)*SlippageTicks_i*LegTick_i*M)
MaxLoss = max(0, -minimum Payoff(F)) + CostReserve
MaxProfit = maximum Payoff(F) - CostReserve
```

Evaluate piecewise-linear payoff at every strike and both asymptotic regions. Reject unbounded option topology; a few stress points do not establish maximum loss. Golden tests compare general payoff to:

| Shape | Premium-only max loss / max profit, points |
| --- | --- |
| Credit vertical, width W, credit C | W-C / C |
| Debit vertical, width W, debit D | D / W-D |
| Short condor, widths Wp/Wc, credit C | max(Wp,Wc)-C / C |
| Long condor, widths Wp/Wc, debit D | D / max(Wp,Wc)-D |

Engineering gates require 0<C<min(Wp,Wc) for short condors and 0<D<min(Wp,Wc) for long condors, preserving intended tail economics. Verticals use W. Costs apply once. MaxLoss and MaxProfit must both be positive. PayoffRewardToRisk=MaxProfit/MaxLoss is not expected return or probability of profit. Do not reuse short-condor formulas for long debit structures.

Futures report signed exposure, absolute notional and planned/stress loss with reserves. MaximumLoss is absent and RiskBound=Unbounded; planned stops do not bound guaranteed loss. Margin estimate is absent unless supplied by a separately pinned qualified source. Current margin/account checks belong downstream.

All values are per one unit. Risk Management selects final units within current limits and creates separate approval identity/hash binding CandidateHash, final units, approved envelope and current authority. This specification does not implement that financial reservation. Final units do not rewrite the one-unit candidate. Contract/strike/side/ratio changes or price outside the envelope require a new candidate and renewed approval.

## 13. Eligibility, ordering and explanations

Gates run in order: snapshot/session; exact definitions/expiry; topology; DTE/width/delta windows; spread/freshness; one-unit size/participation; premium/tick rules; aggregate Greeks/bias; payoff/credit/debit constraints; positive lifetime. No score offsets a hard failure.

V1 ranks eligible combinations lexicographically:

1. Absolute DTE distance to target (zero for futures).
2. Absolute distance to required net-delta target when applicable.
3. Sum of target-leg absolute delta errors.
4. Combo spread in ticks ascending.
5. PayoffRewardToRisk descending for options, not applicable for futures.
6. Canonical candidate key ascending.

Key components: expiry UTC ticks, underlying ID ordinal, then ordered leg tuples of expiry/right/strike/instrument ID/side/ratio. Numeric values compare numerically; GUID/text use explicit ordinal canonical formats. Never lexically sort a numeric strike string as a number. Rules schema 1 fixes objective order; changing it requires a new algorithm/schema version.

Persist the ranking tuple, Generated/Eligible counts and mutually exclusive first-failing-gate counts. Aggregate rejection groups contain stable reason codes; diagnostic samples are deterministically ordered with explicit omitted counts. Selected appears once. NoCandidate has no selected candidate and at least one reason; empty universe is an explained outcome.

## 14. Result, candidate and serialization

### 14.1 Wire manifest

Introduce OrderCompositionResult schema 1 with keys:

| Key | Field |
| ---: | --- |
| 0 | SchemaVersion |
| 1 | ResultId (=InvocationId) |
| 2 | WorkflowId |
| 3 | EntityId (workflow) |
| 4 | InvocationId |
| 5 | InputWorkflowRevision |
| 6 | InputSha256 |
| 7 | EvaluatedAtUtc |
| 8 | ProducedAtUtc (fixed evaluation instant for semantic content) |
| 9 | TargetHorizon |
| 10 | Outcome: Undefined=0, Composed=1, NoCandidate=2 |
| 11 | Candidate; null only for NoCandidate |
| 12 | DecisionContext |
| 13 | ResolvedParameters |
| 14 | CandidateCounts |
| 15 | CandidateDiagnostics |
| 16 | Reasons |
| 17 | ValidUntilUtc; null for NoCandidate |
| 18 | SummaryText |

DecisionContext retains selected result ID/hash, input/binding/snapshot hashes and complete selected authority/definition/rule/pricer evidence. Bulky market evidence may be retained once in the durably pinned request and referenced by immutable ID/hash; result readers resolve exactly that artifact, never latest. No serialization-based copying: defensively copy typed collections.

Candidate keys 0-30, in order: SchemaVersion, CandidateId(InvocationId), OrderId, PrimaryTradeId, PortfolioId, FundId, AssignmentVersion, DeploymentKey, StrategyKey, StructureKey, VariantKey, Product, TargetHorizon, Side, Bias, PremiumMode, Legs, UnitQuantity(1), LiquidityCapacityUnits, Pricing, Greeks, RiskEvidence, ExecutionEnvelope, ParameterResolutionHash, SnapshotHash, BindingHash, PricerVersion, EvaluatedAtUtc, ValidUntilUtc, ApprovalState(Unapproved), CandidateHash.

Leg keys 0-13, in order: InstrumentId, RawSymbol, UnderlyingInstrumentId, InstrumentClass, Side, Ratio, Right(nullable futures), Strike(nullable futures), ExpirationUtc, Multiplier, TickRuleId, Quote, Valuation(nullable futures), DefinitionHash. No broker DTO/conId/order ID.

Nested Pricing/Greeks/Risk/Execution/Quote/Valuation/Rule DTOs require explicit append-only key manifests and golden fixtures before OC-01 closes; sections 5-13 define their required logical content. Integer wire enums reserve zero invalid/unset except deliberately nullable values. Unknown schemas/enums fail closed; keys never derive from reflection order.

### 14.2 Expiry and execution envelope

Composed.ValidUntilUtc=min(workflow/request deadline, accepted selection/authority/binding validity, reservation validity, snapshot/reference validity, EvaluatedAtUtc+candidate lifetime, every used quote's observed time+age limit, applicable last-trade/session cutoffs). Require strictly greater than EvaluatedAtUtc. Zero remaining lifetime yields NoCandidate.NoValidityRemaining. Workflow separately checks current clock at acceptance.

Pricing/data validity and Function deadline checks take precedence: stale required quotes/references and expired requests are Failed, not NoCandidate.NoValidityRemaining. That business reason applies only after trustworthy input validation when construction/session policy leaves no permissible execution window.

ExecutionEnvelope pins schema, order type Limit, TIF=Day for engineering defaults, combo atomicity, proposed/worst allowed price, tick-rule version, permitted price increment, candidate expiry and slippage reserve. It disallows legging, market escalation and side/ratio changes in V1. Day TIF never extends candidate authorization: execution must enforce the earlier approved expiry. Later execution policies may add bounded behavior only through versioned qualification.

### 14.3 Serialization, hashing and limits

Append CompositionResult at envelope key 11 (currently unused), ResultType=OrderCompositionResult, content type application/vnd.ifm.order-composition.v1, empty legacy Payload. Preserve keys 0-10 and historical constructors/readers. Recheck key availability at implementation. No nested encoded result bytes or typeless serialization.

Use shared MessagePackBinarySerializer.Options/serializer at NATS, storage/projection and paging boundaries. Preserve each event repository's owning encoding. Model/Complete do not serialize complete messages. Shared measurement may serialize for byte limits without creating embedded payloads.

Canonical SHA-256 uses lowercase hex over versioned semantic UTF-8 content: explicit fields/schema, ordinal object-property order, meaningful array order, decimal invariant G29, lowercase GUID D, fixed UTC round-trip timestamps. Reject duplicate JSON properties, nonfinite/unrepresentable numbers. Canonicalize once before dispatch. CandidateHash excludes itself/metrics but covers identity/economics/policies/market and parameter hashes/envelope. InputSha256 covers full pinned request with itself empty. Keep original catalog/Portfolio/pipeline algorithms unchanged; semantic hashes are not encoded transport digests.

Limits: rules/binding 262144 uncompressed bytes; snapshot 524288; result 524288; full request/completed event 1048576 uncompressed AND encoded bytes. Check aggregates as well as components before dispatch/project/save. Fail overflow without dropping evidence. Do not globally raise upstream limits.

## 15. Outcomes and failure taxonomy

| Outcome / reason prefix | Examples / behavior |
| --- | --- |
| Composed / OC.COMPOSED | Exactly one valid Unapproved unit candidate |
| NoCandidate / OC.MARKET.* | SESSION_CLOSED, based on authoritative session evidence |
| NoCandidate / OC.CANDIDATE.* | NO_CONTRACT, NO_ELIGIBLE_EUROPEAN_CONTRACT, NO_EXPIRY, NO_STRIKES, DELTA, WIDTH, LIQUIDITY, PREMIUM, PAYOFF, GREEKS_LIMIT, NO_VALIDITY_REMAINING; complete trustworthy evaluation only |
| Failed / OC.PRICING.* | TREASURY_UNAVAILABLE, TENOR_MISSING, TREASURY_STALE, HORIZON_UNSUPPORTED, RATE_CONVENTION_UNSUPPORTED, DAY_COUNT_UNSUPPORTED, MODEL_UNSUPPORTED, GREEKS_CALCULATION_FAILED; preserve the underlying typed cause |
| Failed / OC.MARKET.* | QUOTES_UNAVAILABLE, STALE, CROSSED, SKEW, INCOMPLETE, RECOVERING, REFERENCE_UNAVAILABLE; required data cannot support evaluation |
| Failed / OC.CONTRACT.* | SCHEMA, REQUIRED_FIELD, IDENTITY, HASH, VALUE_RANGE, PAYLOAD_SIZE, UPSTREAM_INVALID, RESERVATION_INVALID |
| Failed / OC.CONFIG.* | MISSING, AMBIGUOUS, CAPABILITY_UNSUPPORTED, PROFILE_MISMATCH, RULE_INVALID, UNSUPPORTED_STRUCTURE |
| Failed / OC.SNAPSHOT.LIMIT | Complete bounded scope cannot be represented |
| Failed / OC.CALCULATION.LIMIT | Candidate/rule/diagnostic structural bounds exceeded |
| Failed / OC.CALCULATION.FAILED | Unexpected model/pricer exception |
| Failed / OC.TIME.EXPIRED | Mandatory Function deadline elapsed |
| Failed / OC.PROJECTION.FAILED or OC.PERSISTENCE.FAILED | Projection or completed append failure |
| Failed / OC.RESULT.INVALID or OC.INVOCATION.CONFLICT | Invalid terminal shape or conflicting input reuse |

Reason records carry code, severity, phase, field/instrument/rule identity, observed/required values with units and source hash. Sort by gate phase, reason code and canonical identity. Summary prose is diagnostic only. Caller cancellation propagates, with workflow-owned durable stop; never save it as a Function failure or call it NoCandidate.

Introduce `OrderCompositionFunctionCompletedEvent` / `OrderCompositionFunctionFailedEvent` without changing historical event meanings. Both retain workflow/entity/invocation/revision, correlation/causation, input fingerprint, timestamps and parameter evidence. Complete carries the typed envelope; Fail carries bounded sanitized structured failure context. Parsing failures may lack decoded identities. Numeric error allocation and event key manifests are OC-01 requirements.

## 16. Persistence, queries and observability

Use IEventSourceFunctionState, its completed-only repository and IFunctionProjector<OrderCompositionFunctionCompletedEvent>. Production composition supplies a projector, although the generic base allows none. Matches compares the complete input fingerprint; TryComplete accepts one matching Composed/NoCandidate event; first append expects version zero.

Project idempotently to Scylla TradeDb before PostgreSQL completed append. Proposed logical tables:

| Table | Partition / clustering | Content |
| --- | --- | --- |
| order_composition_invocation | workflow_id / invocation_id | Result/event/input hashes, Portfolio/Fund, status, times, complete result and exact pinned-input reference |
| order_composition_history | (portfolio_id,fund_id,value_date) / evaluated_at_utc DESC, invocation_id ASC | Summary and exact invocation/result references |

The full prepared request is durably held by workflow state. A query/recovery adapter retrieves that exact accepted request through the workflow repository boundary, validating InputSha256. Do not add another authoritative request table or rebuild input from projections. Result projections carry the immutable request reference, and fail explicitly if required evidence cannot be retrieved.

Physical CQL/DTO manifests and retention belong to the implementation plan, preserving these partitions. ValueDate is the frozen selection RequestedTradeDate under its recorded date policy, not a new receipt date. No ALLOW FILTERING, whole-table scans, daily truncation or automatic deletion of historical/active evidence.

Scylla and PostgreSQL are not one transaction. Partial projection/index writes may exist on failure; failed projection prevents completed append. Append failure after projection leaves orphan evidence. Identical retries upsert equal semantic content, conflicts fail. Completed replay never automatically reprojects; explicit read-model repair is separate. A query row is never workflow/risk authority.

Proposed QueryActor verbs:

- Invocation: WorkflowId, InvocationId; exact record and acceptance evidence.
- Result: same scope plus ResultId; reject mismatch.
- History: PortfolioId, FundId, ValueDate, page size and cursor; maximum 100.
- Decision: latest recorded result for explicit Portfolio/Fund/root/horizon; non-authoritative reference, never execution input.

Use system QueryActor parse/receive/exception maps, Portfolio access scope, shared serializer and bounded cursor. Bind cursor version/filter/scope/page size to continuation; reject malformed/cross-scope reuse. A page is not a complete-history claim. Decision reads use a separately specified bounded partition/index in the implementation plan, not filtering invocation rows; do not enable this optional verb until that access path is implemented.

Read models distinguish FunctionCompleted, WorkflowAccepted, AcceptanceUnknown, NoCandidate and SuspectedOrphan. Acceptance comes from workflow state; no fabricated risk or broker status. New UI work is outside this specification's implementation scope.

Metrics include invocation/outcome/replay counts, phase durations, generated/eligible/rejected counts, quote age/skew and content sizes. Labels use bounded stage/outcome/reason/capability/horizon. Candidate/workflow IDs and arbitrary policy GUID versions belong in persisted evidence/traces/logs, not unbounded metric labels.

## 17. Workflow acceptance and Risk Management handoff

Workflow Realtime translates direct Function results to deterministic CompleteOrderComposition/FailOrderComposition commands. Transport failures use the composition event map. Function/projector never invokes Risk Management directly.

Replace generic completion-to-proceed behavior with:

1. Require current started workflow, same stage/revision/invocation, saved fingerprint/source event and no prior acceptance.
2. Current-time stop/deadline precedence wins. Stale/duplicate/terminal callbacks cause no side effects; late completion cannot reopen a workflow.
3. Validate typed envelope/result hash, upstream lineage, selected winner, exact catalog/policy references and committed one-OrderId/one-PrimaryTradeId reservation.
4. Recompute deterministic composition with the saved input and pinned Model/pricer version. Compare outcome, legs, economics, rule evidence and ranking, excluding processing durations. Never fetch fresh quotes to validate the original decision.
5. Valid NoCandidate records normal completed business stop with reasons and reconciles unused business reservation through Portfolio cancellation/expiry commands. No risk dispatch or automatic reselection.
6. Valid unexpired Composed records acceptance and one durable RiskManagement intent carrying immutable one-unit candidate/hash, source event, authority context and reservation.
7. Invalid result records contract failure; technical failure records workflow failure. Unknown/malformed/expired/cancelled outcomes never dispatch risk.

Recomputation is bounded by remaining workflow deadline. Keep qualified historical Model/pricer versions available for supported recovery, or explicitly refuse continuation; never evaluate an old request with a newer algorithm silently. Persisted metrics remain separate from the semantic decision.

Risk Management determines final units/current risk and separately hashes/authorizes the resulting order. Its later specification must define exact financial reservation/idempotency contracts before integrated execution is enabled. This document does not invent or implement those APIs. Lost replies and notifications reconcile existing IDs/hashes through workflow recovery; allocated business IDs are never reused.

## 18. Compatibility, activation and operational boundaries

New Function ingress replaces historical Command/Start only after preparation and typed acceptance exist. Keep Start keys 0-16 and old events readable; do not reinterpret them as the new Execute request. Historical payloads missing frozen data cannot acquire fresh data/policies by fallback.

Change the current durable Start handoff into preparation then Execute intent under workflow authority. Old inflight starts require an explicit recovery/migration policy; no dual dispatch. Add envelope key 11 readers before typed producers. New handoffs require typed composition schema; opaque historical composition remains read-only.

Register real builder/data/validator capabilities only after qualification. Publication stays blocked when a required risk capability is unavailable: composition completion does not certify Risk Management. Named downstream fixture validators may exist only in isolated tests, never API production registrations.

IBKR emulator precedes live broker connection. Adapter contract/price-sign translation can be tested separately; no broker/account dependency enters composition. No broker connection, submission, automatic production promotion or UI change is authorized by this specification.

## 19. Qualification matrix

These cases are required, not claimed executed. Runtime fixtures must provision current catalog, Portfolio permissions and exact workflow activation so tests reach the Function. Historical host failures are not qualification evidence.

| ID | Required assertion |
| --- | --- |
| OC-C01 | Twelve variants x three horizons = 36 positive constructions; no family/timeframe switch |
| OC-C02 | Wrong horizon/root/Fund/assignment/deployment/variant/hash rejected before load |
| OC-C03 | Multiple family memberships preserve provenance without duplicate candidates/permission |
| OC-C04 | Missing/ambiguous rules, schema mismatch and unknown capabilities fail binding/publication |
| OC-C05 | SelectionConstructionPolicy v1 wire/hash unchanged; extra JSON fields still rejected |
| OC-C06 | Complete variant rules, bound intersection, zero placeholders, invalid units |
| OC-C07 | Every predicate/operation/order/clamp/grid and missing versus unavailable input |
| OC-C08 | Preparation capture race/crash before and after acceptance; no recapture on retry |
| OC-C09 | Chain pagination/incompleteness/overflow never selects truncated winner |
| OC-C10 | Exact freshness/skew boundary, future/crossed/locked quotes and epoch mismatch |
| OC-C11 | Futures rollover buffer and canonical contract selection |
| OC-C12 | Fractional DTE, separate trading-day tenor buckets/year fraction, exact expiry/calendar and definition-linked underlying |
| OC-C13 | Every vertical side/right/premium combination and canonical order |
| OC-C14 | Both condor sides x three biases, unequal wings, wrong-sign delta rejection |
| OC-C15 | European-only series/leg qualification; Black-76 explicit-T golden units/values, IV failure, exact tenor conversion without interpolation, publication freshness and model versions |
| OC-C16 | Signed prices, credit/debit rounding, combo tick and adapter round-trip |
| OC-C17 | General payoff versus closed forms, unequal wings, costs once and futures unbounded risk |
| OC-C18 | UnitQuantity=1, participation floor, zero capacity, no financial authority |
| OC-C19 | Stable ties across shuffled inputs/cultures, lifetime and semantic hash |
| OC-C20 | Composed/NoCandidate/Failed exclusivity; missing/stale/incoherent pricing and solver failure are Failed, complete economic rejection is NoCandidate |
| OC-C21 | Five maps, list-validation order, typed singleton and mapping-only overrides |
| OC-C22 | Real actor ingress, malformed/null/route mismatch, payload release and single typed reply |
| OC-C23 | Timeout/caller cancellation, late fault observation and no later side effects |
| OC-C24 | Projection/index failure, append orphan and concurrent version-zero fence |
| OC-C25 | Committed replay after expiry, no reprice/reproject, conflicting fingerprint |
| OC-C26 | Committed/Replayed phases and observer faults retain original completion |
| OC-C27 | All key manifests, shared transport/content limits, legacy readers and decimal hashes |
| OC-C28 | Workflow rejects altered topology/quantity/economics/selection/reservation |
| OC-C29 | NoCandidate stops normally; stale/expired/cancelled callbacks cannot dispatch risk |
| OC-C30 | Lost acceptance notification produces one logical recoverable risk intent |
| OC-C31 | Query access/paging/cursor tamper and unknown/orphan authority labels |
| OC-C32 | Real NATS/PostgreSQL/Scylla Function completion/replay/failure/restart |
| OC-C33 | Upstream regression and complete current activation fixtures |
| OC-C34 | Feed/pricer/emulator adapter qualification distinct from controlled snapshot tests |

Representative BDD scenarios:

```gherkin
Scenario: Weekly long bullish condor retains the selected meaning
  Given an authorized exact Weekly long debit bullish condor
  And a committed business-ID reservation and complete frozen snapshot
  When the Function evaluates the saved request
  Then it returns one debit four-leg unit with positive policy-compliant net delta
  And it substitutes neither a short condor nor another horizon
  And only workflow acceptance creates a RiskManagement intent

Scenario: No executable combination is available
  Given a selected debit vertical and valid immutable authority
  And a complete snapshot with no quotes in the permitted delta range
  When composition evaluates
  Then the completed outcome is NoCandidate with stable reasons
  And no financial reservation or order submission occurs

Scenario: A completed response is lost
  Given the Function projected and appended a completed result
  When the same saved invocation is retried after its deadline
  Then the original completion returns without new market reads or pricing
  And the workflow refuses expired continuation
```

Unit tests own policy/math/topology/hash/actor conventions; BDD owns business boundaries; integration owns real host/serializer/store/lifecycle; verification owns source-to-accepted-candidate-to-risk-intent evidence. Existing OptionPricer tests own numeric/native compatibility. Controlled fixtures do not establish live feed/emulator readiness.

## 20. Implementation gates and definition of done

| Gate | Deliverable | Required evidence |
| --- | --- | --- |
| OC-01 | Contracts, all nested manifests, identity/error allocation, hashes, envelope | C02/05/19/27; dependency and compatibility builds |
| OC-02 | Rules schema, exact resolver, publication/semantic capability validation | C03/04/06/07; PostgreSQL lifecycle |
| OC-03 | Snapshot adapter, durable preparation and race fencing | C08/09/10/12; restart before/after accepted intent |
| OC-04 | Resolver/composer Models, pricer adapter, economics/ranking | C01/07/11-20; all 36 positive variants/horizons |
| OC-05 | Function/context/maps/policy/state/projector/terminal handlers | C21-27/32; production ingress and stores |
| OC-06 | Typed workflow acceptance/recomputation, business stop and risk intent | C28-30; no generic-success or dual route |
| OC-07 | Scylla queries/evidence/access/observability | C24/31; paging and orphan semantics |
| OC-08 | Integrated regression/readiness record | C32-34; exact commands/counts/revision/dependencies |

All gates are Planned when this document is created. The later implementation plan sequences files, migrations, DTO manifests and fixture provisioning. Numeric tuning may follow in new immutable versions; missing required data or unqualified reference/pricer/capability contracts cannot be replaced by runtime guesses.

Code complete requires the gates' code and owned automated tests, no generic-success bypass, no domain logic in Function actors and no duplicated deadline/serialization mechanics. Operational readiness additionally requires qualified live snapshot/reference/pricer adapters, actual downstream risk capabilities, reviewed published deployments/assignments/activations and emulator qualification. Document completion is neither code completion nor full pipeline qualification.

## 21. Source pointers and revision record

- [Existing Start contract](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/Commands/StartOrderCompositionPipelineCommand.cs)
- [Narrow construction policy](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/TradeSelection/SelectionConstructionPolicy.cs)
- [Existing workflow completion](../../Command/CompleteOrderComposition.cs)
- [Typed envelope](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Model/StrategyStageResultEnvelope.cs)
- [Function base](../../../../../../TomasAI.IFM.Shared/EventModelActor/BaseEventSourceFunctionActor.cs), [typed policy](../../../../../../TomasAI.IFM.Shared/EventModelActor/FunctionExecutionPolicy.cs)
- [Selector evidence and full-workflow limitations](../../TradeSelection/Docs/TradeSelection-Implementation-Evidence-v1.0.md)

2026-09-07: Created specification aligned to current catalog, one-unit selector handoff, all twelve variants/all three horizons, market-only assessment, typed upstream results, five-map Function convention and shared serialization. No production source, schema, policy data or UI changed by this document.
