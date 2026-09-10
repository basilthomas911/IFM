# Order Composition Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Date | 2026-09-08 |
| Status | OC-01..08 code complete; automated qualification passed |
| Authority | [Detailed specification](OrderComposition-Specification-v1.0.md) |
| Prerequisites | [OCP-00..07 plan](OrderComposition-Prerequisite-Implementation-Plan-v1.0.md), [implementation evidence](OrderComposition-Prerequisite-Implementation-Record-v1.0.md) |
| Actors | [System conventions section 13.3](../../../../../../Documents/system/Actor-Implementation-Conventions.md#133-functionactor-convention) |
| Scope | Exact Fund-authorized selected catalog variant on one Daily/Weekly/Monthly trigger; ES futures or European-style ES futures options; one normalized unit |

This document records the composer gate scope. The [composer implementation record](OrderComposition-Implementation-Record-v1.0.md) records actual code and verification separately from the earlier prerequisite record. Automated composer qualification and operational broker/risk readiness are distinct.

## Operator-owned `StartPipelineAsync` correction - 2026-09-10

**Status:** Planned correction to the implemented FunctionActor and prerequisite-handoff baseline.

Order Composition initialization begins after Strategy Workflow has durably entered and projected
the Order Composition stage. The upstream coordinator must not withhold dispatch while it checks
construction policy, business-ID reservation, market preparation, instrument metadata, quotes,
rates, or pricing readiness. `StartPipelineAsync` owns that work and returns either one immutable
composition input or a typed `OrderCompositionPipelineFailedEvent` that Strategy Workflow persists
and Strategy Viewer displays.

```csharp
public sealed record OrderCompositionPipelineInitialization(
    OrderCompositionParameterSet ParameterSet,
    string ParameterPayloadSha256,
    TradeSelectionResult AcceptedSelection,
    OrderCompositionBusinessIdentity BusinessIdentity,
    OrderCompositionMarketSnapshot MarketSnapshot,
    OrderCompositionPricingContext PricingContext);

Task<PipelineStartResult<OrderCompositionPipelineInitialization>> StartPipelineAsync(
    ExecuteOrderCompositionPipelineCommand command,
    CancellationToken cancellationToken);
```

`StartPipelineAsync` owns these Order Composition prerequisites at the workflow's fixed
`RequestedAtUtc`:

1. Validate workflow ID/revision, trigger, ES root, and exact horizon, plus accepted Regime,
   Market Condition, and Trade Selection lineage, hashes, validity, restrictions, and selected
   candidate identity.
2. Resolve the exact published construction profile/policy referenced by the accepted selection;
   validate version, schema, canonical hash, variant signature, capability version, effective time,
   and Fund-authorized deployment/assignment binding.
3. Reserve or reload the deterministic Portfolio/Fund/order/trade business identities through the
   existing durable idempotent reservation path. A retry uses the same reservation identity and
   cannot allocate different IDs for the same workflow invocation.
4. Validate the selected structure, side, bias, premium mode, leg count/roles/ratios, expiration
   groups, product/settlement rules, and construction bounds for the selected one-unit candidate.
5. Accept or rebuild the exact immutable market-preparation evidence required by the policy,
   including reference futures, option universe, contract definitions, quote generations,
   exchange/session state, and snapshot identity/hash.
6. Validate instrument definitions, expirations, strikes, rights, multipliers, underlying links,
   quote presence/order/size/time/skew/staleness, liquidity limits, and configured provider/feed
   quality. Missing or corrupt required evidence is failure; it cannot silently reduce the universe.
7. Resolve and validate the policy's Treasury/rate source and calendar, day-count, volatility and
   pricing-model inputs, fees, tick/rounding rules, and applicable validity limits.
8. Validate snapshot consistency, bounded collection/payload sizes, calculation deadline, and that
   every selected contract is still represented by the frozen evidence.

`ExecuteAsync` invokes `StartPipelineAsync` once and calculates from only the returned immutable
value. A valid search that finds no constructible candidate is the operator's explicit business
outcome when allowed by the specification. Missing policy, authority, identity reservation,
market evidence, instrument definition, quote, rate, or pricing capability is initialization
failure. Risk sizing, capacity reservation, Fund financial authorization, and broker submission
remain downstream responsibilities.

The initialization error includes bounded code/type/message, all reason codes, selected
deployment/variant and construction-profile identity, reservation identity/status, failed market
or pricing dependency, snapshot identity/hash when available, safe diagnostics, and timing.
Strategy Viewer shows `OrderComposition / Initializing`, then `Processing`, `NoTrade`, `Completed`,
or `Failed`, with the exact initialization evidence and reason details.

Implementation and verification order:

1. Add failing tests for invalid upstream lineage, missing/mismatched construction policy,
   reservation conflict, unsupported variant, absent definitions/quotes/rates, stale or inconsistent
   market preparation, payload overflow, and deadline expiry.
2. Add the typed initialization contract and compatible serialization registration.
3. Move prerequisite acceptance, ID reservation, market preparation, policy resolution, and
   pricing readiness into the Function's `StartPipelineAsync` boundary.
4. Preserve idempotent reservation/replay and map every initialization failure to durable workflow
   failure without manufacturing a composed result.
5. Extend workflow projections, queries, notifications, and Strategy Viewer detail.
6. Run real NATS/PostgreSQL/ConfigurationDb/Portfolio/Scylla and market-provider fixtures for all
   twelve variants and three horizons, plus retry, restart, timeout, and fault injection.

**Acceptance:** every workflow reaching Order Composition is visible before prerequisites run;
every readiness failure is attached to that workflow; and calculation receives one sealed,
fully qualified construction input.

## 1. Entry, dependencies and layering

The market-data boundary now has explicit qualification, Treasury conversion, context refresh, Black-76 enrichment and a bounded snapshot assembler connected to supervised worker sources. A mapped workflow acceptance transition commits an evidence-linked Start request before dispatch. The composer now extends this preparation into a frozen Execute Function request and typed workflow acceptance. Concrete committed business-source projection, persisted reconstruction plans, durable startup recovery/context refresh and selected-leg discovery-release receipts are implemented. Reviewed reference publication and an initial combined live pricing/handoff/replacement canary have now passed; see the [publication record](OrderComposition-Reference-Publication-and-Qualification-v1.0.md) and [closure audit](OrderComposition-Closure-Audit-v1.0.md) for sustained qualification and the separate broader Stage 4 acceptance boundary.

| Dependency | Entry requirement | Later acceptance requirement |
| --- | --- | --- |
| Regime Discovery / Market Condition / Trade Selection | Current typed completed results, exact shared catalog binding and same trigger horizon | Real workflow fixture reaches composition without legacy family/horizon fallback |
| Portfolio reservation | Committed OrderId/primary TradeId and exact Fund assignment/version/hash | Replay, expiry, cancellation and no duplicate business IDs |
| ConfigurationDb | Strict SelectionConstructionPolicy v1 remains compatible; v2 additionally pins a bounded reviewed marketData universe. Complete specialized OrderCompositionRules and strict catalog schema are implemented | Reviewed exact published versions; missing role or reviewed universe remains not-ready |
| Market-data prerequisites | Frozen OCP contracts and failure semantics; controlled snapshot fixtures allowed | OCP-01..05 production adapters, worker ownership/recovery and complete snapshots required for actual chain consumption |
| Risk Management | Typed boundary accepting one unapproved unit with limits | Separate sizing/risk reservation and execution authority before any submission |

Do not add a Trade.Shared reference to Application.MarketData or Framework.MarketData: the existing Feed.Shared -> Trade.Shared edge would create a cycle. Define domain composition DTOs in Trade.Shared using existing domain-neutral primitives. A Domain.Trade preparation extension maps the application snapshot to those DTOs explicitly, preserving every required reference/quote/version field and checking its semantic digest. The adapter never serializes to clone or normalizes fingerprints through MessagePack. Pure construction Models do not receive application APIs, repositories, clocks or subscription handles.

The new reference-data mapping access path is `ReferenceDb.option_pricing_convention`, partitioned by exact ContractId and clustered by MappingVersion. It is separate from raw instrument_definition and writes once using IF NOT EXISTS plus read-back conflict verification. No automatic ES mapping seed or production data backfill is assumed.

## 2. OC-01: Contracts, identities and compatibility

**Implement in:** Trade.Shared `Strategy/Workflow/IntrinsicTime/Identity`, `Pipeline/Commands`, new `Pipeline/OrderComposition` and existing result/workflow model folders.

1. Add immutable OrderCompositionExecutionId using the same identity/route format pattern as TradeSelectionExecutionId. Allocate a distinct Execute Function subject; preserve historical StartOrderCompositionPipelineCommand keys/subject.
2. Implement specification section 8's Execute manifest, keys 0..20: SchemaVersion, CommandId, Subject, PostEvents, EntityId, ErrorCode, RouteTo, InputWorkflowRevision, WorkflowContext, TriggerEvent, CorrelationId, CausationId, RequestedAtUtc, ExpiresAtUtc, EvaluatedAtUtc, AcceptedSelectionEnvelope, SelectionBinding, Reservation, CompositionBinding, MarketSnapshot, InputSha256.
3. Append CompositionResult at envelope key 11 after checking the then-current tree; never repurpose slots 8..10. Implement the specification section 14 result/candidate manifests and explicit nested manifests for pricing, Greeks, execution bounds and reference evidence.
4. Freeze nested snapshot domain records by copying the logical OCP fields: exact convention keys 0..24 (schema 2 includes premium tick rules); quote keys 0..8; pricing context fields including publication-policy version; normalized instrument/value/scope fields. These are independent explicit domain contracts, not application CLR type references.
5. Allocate distinct numeric errors for Execute, Complete, Fail, Conflict, Timeout, projection and persistence against the current shared error registry. Allocation is part of this gate's code review; do not reuse selector errors or infer that a number is available from one folder search.
6. Add append-only workflow preparation state and prepared-request reference fields at the next unused keys after existing SelectionDispatch. Pin the final manifests in fixtures before writing events.
7. Use shared MessagePack serializer/measurement at boundaries and versioned semantic hashes from the specification. Decode old envelopes without a composition slot. Zero/unknown schema, malformed nested context and size overflow fail before execution.

**Acceptance:** OC-C02/C05/C19/C27; binary fixtures, normalized decimal hash vectors, shuffled/culture-independent ordering, explicit key uniqueness and dependency-cycle check. No wire claim is complete until every nested manifest has a tested fixture.

## 3. OC-02: Exact catalog rules and capabilities

**Implement in:** existing ConfigurationDb strategy catalog/policy services, Trade.Shared rules contracts/list validation, Domain.Trade `OrderComposer/Model/CompositionParameterResolver`.

- Retain SelectionConstructionPolicy v1's strict JSON/hash behavior. Add Role `OrderCompositionRules` with a new exact ParameterSet/ParameterSchema version through the existing catalog lifecycle.
- Materialize one complete rule entry per deployment VariantKey; resolve exact strategy/structure/variant/product and Fund assignment snapshots. Missing/ambiguous role, duplicate entries, unqualified builder/pricer capabilities and invalid bound intersections fail publication and binding.
- Implement the specification's bounded predicate/operation grammar, deterministic rule order, grid rounding and evidence. No scripting, reflection dispatch, runtime defaults or family-owned policy table.
- Author reproducible complete offline profiles covering all twelve variants on each horizon. Intersect DTE with supported European contracts and the separate <90 exchange-trading-day pricing horizon. Do not mutate published settings or auto-publish at startup.

**Acceptance:** OC-C03/C04/C06/C07, exact PostgreSQL catalog lifecycle/rollback tests, all variants covered and old policy hashes unchanged.

## 4. OC-03: Snapshot preparation and durable dispatch

**Implemented prerequisite subset, 2026-09-08 UTC:** `AcceptOrderCompositionPreparationCommand` verifies Scylla evidence under current workflow revision and commits `CompositionDispatch` before Realtime notification. Existing Start key 17 references the immutable capture. Complete-empty, conflicting/expired capture and identical replay are tested. The final Execute request is now appended at workflow-view key 32 and legacy-state key 28; keys 30/26 retain preparation evidence. Selected-leg handoff uses the existing durable acquire-before-release path, including terminal discovery cleanup. Do not duplicate the accepted transition or repurpose its keys when implementing the Function.

**Implement in:** existing workflow Command/Realtime maps and extensions, workflow event/state transitions, Domain.Trade application-snapshot adapter.

- After committed business reservation, acquire bounded temporary discovery ownership through the qualified Stage 4 API. Capture complete scope through IMarketCompositionSnapshotProvider. OCP failures become mapped workflow failures before Function dispatch.
- Freeze normalized definitions, full source quotes, pricing reference context, scope completeness, fees/ticks/calendar, exact catalog evidence and one evaluation instant into the domain snapshot. Validate required specification fields beyond the normalized OCP assembler result; no source interface is considered production-ready merely because a test supplies pages.
- Send a mapped PrepareOrderComposition command. Compare exact workflow/stage/revision/selection/reservation hashes, advance revision once, and persist the accepted prepared request with the durable dispatch intent before invoking the Function.
- Realtime dispatch rechecks current workflow authority. Before preparation commit recovery may recapture; after commit it reuses identical IDs, bytes/semantic content, context and timestamps. A later quote is a new invocation, never a silent retry replacement.
- Bound total preparation work by request/workflow deadline and OCP snapshot limits; release temporary ownership on business stop/failure. Selected-leg handoff belongs to the joint Stage 4 integration gate and must acquire before releasing discovery.

**Acceptance:** OC-C08/C09/C10/C12; competing captures, crash before/after preparation commit, source generation change between pages, timeout/cancellation and retry with no recapture. Real deployed worker snapshots remain dependent on OCP-04.

## 5. OC-04: Pure construction Models

**Implement in:** `OrderComposer/Model`, behind IOrderComposer and a deterministic qualified pricer adapter.

| Model | Behavior |
| --- | --- |
| EsFuturesOrderComposer | Exact permitted root, qualified contract/roll rules, long or short one future; no Treasury or option dependency |
| EsVerticalSpreadOrderComposer | Bull-call debit, bear-call credit, bull-put credit, bear-put debit; same expiry/underlying, actual strikes and 1:1 legs |
| EsIronCondorOrderComposer | Long/debit or short/credit; balanced/bullish/bearish; four ordered strikes and 1:1:1:1 legs; bias checked using actual signed Greeks |
| CompositionParameterResolver | Explicit baseline plus bounded ordered adjustment rules and constraints; trace every applied/skipped/clamped value |
| OrderComposer | Complete bounded enumeration, unique pricing input valuation, hard filters, deterministic lexicographic ranking and one candidate or NoCandidate |

Use the existing explicit-T Black-76 engine through the adapter. Every option leg must be verified European and share compatible underlying/expiry/settlement within a combo. Pin engine/version and rate conventions. Required pricing failure stops the evaluation with Failed; valid economic rejection contributes a deterministic rejection reason.

Implement signed debit/credit prices, financial-side tick rounding, natural/midpoint execution estimates, size participation floors and one-unit capacity. Apply contract multipliers once. Use the specification's general piecewise payoff algorithm for unequal condor wings, validating against independent closed-form cases. Futures loss is not described as bounded by a planned stop.

**Acceptance:** OC-C01/C07/C11..20, including 36 variant/horizon positive fixtures, put/call/side/bias matrices, unequal wings, negative premium signs, zero liquidity capacity, stable tie ordering and no fabricated Greeks.

## 6. OC-05: Mapped Function actor and completed evidence

**Implement in:** `OrderComposer/Function/Actor`, `Function/State`, `Function/Projector` and separate Execute/Complete/Fail/ResolveExecutionPolicy extension classes.

- Inherit BaseEventSourceFunctionActor. Inject IOrderCompositionFunctionContext; bind it and the generic IFunctionActorContext interface to the same singleton in API and integration hosts.
- Declare all five frozen maps: _parseMap, _validationMap, _receiveMap, _executionPolicyMap and _eventMap. Exact CLR request/event types only. Actor methods perform base mapped dispatch; no deadline arithmetic, calculations, Typed(), direct terminal-handler calls or timer helpers.
- ValidateAsync calls inherited ValidateMappedCommand. Ordered List<ValidationError> extension rules validate identity, schema, upstream lineage, exact catalog/permission/reservation, snapshot bounds and fingerprint without remote reads or pricing.
- Execute extension invokes the Model and dispatches typed outcomes through the event map. Separate Complete and Fail extensions own domain terminal handling. Base lifecycle owns cancellation, deadlines, late-worker observation and committed/replayed phases.
- ResolveExecutionPolicy extension returns the domain clock with Loading=bounded replay-loading budget and other stages=original ExpiresAtUtc. Expired matching completion can replay; expired new work cannot execute or continue workflow.
- Project completed evidence synchronously before appending the completed-only Function event in PostgreSQL. No Function Processing/Failed persistence, failure publication or durable Function projector. Preserve exact terminal event identity for committed/replayed observations.

**Acceptance:** OC-C21..27/C32; real NATS ingress, malformed payload, single typed reply, all failure stages, exact deadline boundary, caller cancellation, late fault, conflicting fingerprint, replay without reprice/reproject and projection/append races.

## 7. OC-06: Workflow acceptance and risk handoff

**Implement in:** mapped CompleteOrderComposition/FailOrderComposition/TimeoutOrderComposition and continuation extensions.

- Replace generic-success continuation with typed result verification: selected deployment/variant/side/bias/premium/product/horizon, exact reservation IDs, candidate fingerprint, one-unit topology, economics and valid lifetime must match frozen input.
- Bounded deterministic recomputation uses the pinned Model/pricer versions and accepted snapshot; never current market data. Reject incompatible historical engine versions rather than silently substituting a new one.
- Record valid Composed acceptance once, then emit one recoverable RiskManagement intent. Risk receives an unapproved one-unit candidate and determines final units/financial authority separately.
- NoCandidate records a normal business stop and reservation cleanup through Portfolio commands. Failed/cancelled/expired callbacks never start risk; late callbacks cannot revive a stopped workflow. Lost notifications reconcile existing invocation/business IDs.
- Activate the new route in one controlled cutover. Preserve old history but do not run both Start and Execute paths for an invocation.

**Acceptance:** OC-C28..30/C33; altered topology/price/hash/reservation rejection, revision races, lost replies, duplicate acceptance and one logical risk intent. Emulator/order execution remains a later integration boundary.

## 8. OC-07: TradeDb projection and bounded query access

**Implement in:** TradeDb schema/context and `OrderComposer/Query` using the established QueryActor maps and access checks.

Implemented additive CQL; the full immutable event is retained in the invocation payload:

```sql
CREATE TABLE IF NOT EXISTS order_composition_invocation (
    workflow_id uuid, invocation_id uuid, result_hash text, input_hash text, payload blob,
    PRIMARY KEY ((workflow_id), invocation_id));
CREATE TABLE IF NOT EXISTS order_composition_history (
    portfolio_id int, fund_id int, value_date date, evaluated_at_utc timestamp,
    invocation_id uuid, workflow_id uuid, event_id uuid, target_horizon smallint,
    outcome tinyint, reason_code text, result_id uuid, result_hash text,
    PRIMARY KEY ((portfolio_id, fund_id, value_date), evaluated_at_utc, invocation_id))
    WITH CLUSTERING ORDER BY (evaluated_at_utc DESC, invocation_id ASC);
```

Match Portfolio/Fund physical types to current owning contracts during schema review; the DTO never coerces business identities without checked compatibility. Validate same-hash idempotence and reject conflicting invocation writes. No daily truncation, ALLOW FILTERING or implicit read-model authority. Full prepared request remains in workflow persistence, retrieved by exact accepted reference/hash.

Invocation/Result queries validate exact scope and identity. History uses the full partition key and cursor bounded to page size 100, filter/scope/version and continuation. Keep optional Decision verb disabled until its dedicated bounded access path is specified and tested; it is not required for composer code completion. Distinguish FunctionCompleted, WorkflowAccepted, AcceptanceUnknown, NoCandidate and SuspectedOrphan.

**Acceptance:** OC-C24/C31/C32; real Scylla/PostgreSQL projection failures, partial/orphan evidence, paging/cursor misuse and access checks. Persist bounded diagnostics; avoid workflow/contract IDs in metric labels.

## 9. OC-08: Qualification, evidence and rollout

Run the owning Trade.Shared, Domain.Trade unit/BDD/verification suites and real NATS/PostgreSQL/Scylla integrated fixtures. Provision current catalog permissions and exact activation so tests reach the new Function. Record precise commands/counts, repository revision and failed/skipped cases in a new composer implementation record.

After isolated gates pass, run the five pipeline actors together one stage at a time using one real triggering horizon. Compare accepted lineage and evidence at each boundary. Complete S4G-08 selected-leg handoff/recovery only after both composer and OCP-04 worker/ownership paths exist. Feed simulations and supplied-leg tests do not establish live-provider readiness.

Live acceptance additionally requires verified exchange/product metadata, current official Treasury source/publication policies, quote limits, native Windows/Linux execution, provider entitlement/capacity, session rollover, sustained recovery/soak and rollback evidence under the existing Stage 3/4 plan. The Treasury source/conversion ambiguity is resolved by the [official provider implementation](OrderComposition-Official-Treasury-Implementation-v1.0.md); its bounded calendar must be renewed before 2027. Do not remove startup guards merely because new unit tests pass. No broker connection or UI work is part of these gates.

## 10. Implementation order and current status

1. OC-01 and OC-02 freeze contracts/catalog rules; OCP-04 continues independently.
2. OC-04 pure Models can run on controlled complete snapshots while OC-03 prepares the workflow integration.
3. OC-05 uses those Models and completed-only repositories.
4. OC-06/OC-07 integrate workflow acceptance and bounded evidence access.
5. OC-08 closes actual integrated tests, then the separate live qualification gates.

All OC gate implementations are now present. See the [composer implementation record](OrderComposition-Implementation-Record-v1.0.md) for qualification results and explicit operational boundaries. Risk approval/sizing, emulator execution, production deployment promotion, and the separately scheduled five-stage live acceptance exercise are not implied by composer completion.
