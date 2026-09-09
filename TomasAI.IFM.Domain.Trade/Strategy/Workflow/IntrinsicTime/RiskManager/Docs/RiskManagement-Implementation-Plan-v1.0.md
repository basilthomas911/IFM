# Risk Manager implementation plan v1.0

Date: 2026-09-09. Baseline: `5d93bf7c` plus the Risk Manager specification documentation. Status: implementation delivered; final qualification is tracked in [delivery and runbook](RiskManagement-Delivery-and-Runbook.md). The sections below retain the original gate acceptance baseline; the delivery record documents the final additive sidecar/storage design.

## 1. Delivery objective

Deliver a fully observable Risk decision workflow using the exact Order Composer result and Portfolio financial authority, ending at a durable **Authorized intent**. Include dedicated Risk history queries, a usable WinForms observation UI, detailed rejection explanations, consistent Fund outcomes and verified recovery. Preserve the existing calculation, reservation and authorization controls.

The owner confirmed this scope after review of [RiskManagement-Specification-v1.0.md](RiskManagement-Specification-v1.0.md). This is an incremental implementation plan: existing Risk/Portfolio code is a baseline to retain and qualify, not a requirement to rewrite it.

## 2. Explicit scope decisions

| ID | Decision | Status |
|---|---|---|
| RM-D01 | End this delivery at Authorized intent; no execution consumption or submission from Risk | Confirmed by owner |
| RM-D02 | Dedicated invocation/result/history queries and Risk observation UI belong in this delivery | Confirmed by owner |
| RM-D03 | Detailed rejection explanations belong in this delivery | Confirmed by owner |
| RM-D04 | Include Fund outcome consistency and recovery verification | Confirmed by owner |
| RM-D05 | Automatic re-sizing after contention | Approved and implemented; see the [implementation record](RiskManagement-Automatic-Resizing-Implementation.md) |
| RM-D06 | Observation feature set | Dedicated observation is included; the baseline feature set is section 9. Optional streaming, export and analytics are not assumed |
| RM-D07 | Legacy history and capital | Retain read-only legacy records; enter development capital separately; no legacy balance conversion |
| RM-D08 | Broker execution, production security and additional risk models | Remain separate deliveries under existing scope |

RM-D05 permits at most three total attempts. Before replacing a pending reservation attempt, reconcile its original operation: an absent receipt is sufficient only at a newer financial revision that permanently excludes the old expected-revision request. Preserve candidate, policy, funding evidence, authority and expiry; refresh financial usage/cash. Changed capacity before reservation can also trigger a new invocation. History, explanation, UI and Fund synchronization implementation is recorded in the delivery document.

Baseline observation includes paged history, exact decision detail, per-quantity constraint explanations, current financial handoff status, Fund synchronization status, evidence age and reconciliation state. It does not include order submission, administrative override, one-click re-sizing, alerts, CSV export or cross-Fund analytics. Those optional features require an explicit scope addition; they do not block this delivery.

## 3. Baseline inventory and known gaps

| Area | Existing implementation | Incremental work |
|---|---|---|
| Preparation | Exact published policy, lineage, original snapshot, coherent authority and immutable request | Preserve; verify all new contracts against saved requests |
| Calculation | Repricing, 36 scenarios, unit risk, whole-unit search, scope limits and fee treatment | Add deterministic explanation records without changing established economic decisions |
| Acceptance | Independent recomputation; NoTrade versus eligible proposal | Version-aware compatibility for explained results; persist terminal synchronization intent |
| Financial handoff | ReservePending -> FundPending -> Authorized | Verify unresolved outcomes, expiry and races with terminal synchronization |
| Fund outcomes | Aggregate supports RiskRejected, Cancelled and Expired; exact financial authorization supports RiskApproved | Connect workflow rejection/failure/expiry through durable, idempotent synchronization |
| Recovery | Replays saved work for Started Risk workflows; excludes terminal workflows and historical execution phases | Recover terminal Fund synchronization and projection work separately |
| Risk persistence | PostgreSQL completed Function events and workflow snapshots; FunctionProjector is null | Dedicated rebuildable history projection, durable projection progress and authoritative exact lookup |
| Query/UI | Order Composition query patterns, Portfolio Admin and StrategyObservationForm exist | Dedicated Risk queries/client/presentation/read-only UI |

The existing 1,511-pass report qualifies the prior combined development scope. It is a regression baseline, not evidence for the new gates below. No new gate is complete on the strength of that count alone.

## 4. Gate RM-IP01: contracts, compatibility and traceability

**Dependencies:** none. **Output:** reviewed additive wire/storage manifest and fixtures before new events are persisted.

Work in Trade.Shared RiskManagement, workflow model/events/commands, Portfolio shared contracts, existing query/client abstractions and the shared error/route registries.

1. Inventory current numeric MessagePack keys, schema versions, enum values, error IDs and actor subjects. Allocate additions from the actual tree; do not renumber published fields or repurpose historical Consume/Submit states.
2. Define a versioned explanation payload, immutable decision history record, lifecycle observation record, exact query contracts and terminal Fund synchronization checkpoint. Keep financial authority DTOs owned by Portfolio.
3. Separate immutable calculation outcome, workflow acceptance, current financial permission, Fund synchronization and projection completeness. A single “Approved” display flag cannot represent them all.
4. Pin canonical hashing rules for new explained results. Existing schema-1 results must preserve their original validation/hash/replay behavior. Use an explicit result version for new explanation-bearing content; do not recompute historical schema-1 hashes as if new fields existed.
5. Define a version-aware acceptance path: new results are independently recomputed including explanations; old saved invocations/results retain their original semantics. Unsupported schemas fail with a clear reason.
6. Keep existing command/result byte ceilings. Explanation bounds must be validated before dispatch and persistence. If maximum inputs cannot fit, compact the explanation representation instead of silently truncating evidence or increasing limits without review.
7. Freeze query response status semantics: Found, NotFound, Unavailable and incomplete/stale observation metadata. Missing projection is not a missing authoritative decision.

**Acceptance:** old binary fixtures decode; old semantic hashes remain identical; new nested payloads roundtrip; key/error/route uniqueness is verified; mutated nested evidence is rejected; maximum-size explanation vectors fit approved boundaries. All later gates link requirements to this manifest.

## 5. Gate RM-IP02: deterministic rejection explanations

**Dependencies:** RM-IP01. **Primary files:** RiskEvaluator, RiskSizingModel, RiskUnitModel, RiskContracts and RiskAssessmentResult; RiskCalculationTests and RiskAcceptanceTests.

### Explanation model

Represent shared constraint definitions once, keyed by scope kind/key, measure and unit. Record the frozen limit maximum, enabled state and existing Held/Working/Position usage. Each evaluated quantity references these definitions and records proposed requirement, total charge, remaining headroom and pass/fail. Preserve exact decimals and units; presentation rounding must never become decision input.

The payload must identify policy/liquidity quantity ceilings, effective loss budget and its inputs, upstream multiplier and triggering conditions, cash components, quantity-specific funding evidence and the selected quantity. For each larger failed quantity, show all evaluated binding failures, including cash and per-trade loss. For a zero-unit result, retain the bounded search evidence sufficient to explain why no quantity fits.

Keep economic calculations unchanged. Validate the entire required funding grid before sizing. Missing quotes, unknown market evidence and missing limits remain failures, not normal capacity rejection. Market-blocked results carry the exact blocking condition and its source without fabricating calculations that did not run. A liquidity ceiling of zero receives an explicit explanation.

Use deterministic ordering and a documented stable primary-reason selection. Human-readable text belongs in presentation resources, backed by typed reason codes and values. Preserve legacy generic reason interpretation for historical readers. No mutable timestamps or localized strings enter canonical decision hashes.

### Calculation strategy

Retain descending bounded enumeration up to 100 units. Record all failed quantities above the selected quantity and the selected feasible quantity; lower quantities need not be evaluated after selection. Rejected sizing records all searched quantities. Clearly label “not evaluated” rather than implying feasibility for untested quantities.

Financial admission failures after calculation are separate lifecycle explanations. Show, for example, “calculation eligible; reservation not admitted because financial revision changed.” Do not rewrite an immutable approved result into a rejected calculation. Transport uncertainty is displayed as unresolved, without inventing a financial refusal.

**Acceptance:** independent expected values for cash/loss/Greek/quantity limits; several simultaneous failures; equality boundaries; zero liquidity; 0.5 multiplier; disabled/missing limits; nonlinear margin; fee shortfall; deterministic ordering/culture/hash tests; actual maximum serialization measurements. Recomputed explained acceptance rejects altered limits, quantities and reason values.

## 6. Gate RM-IP03: Fund outcome consistency

**Dependencies:** RM-IP01. **Primary areas:** CompleteRiskManagement, FailRiskManagement, TimeoutRiskManagement, workflow state/events, PortfolioFundCompositionCommandHandler and aggregate; Portfolio command client and authoritative receipts.

### Target behavior

| Workflow fact | Fund target | Synchronization rule |
|---|---|---|
| Verified Risk rejection | RiskRejected | Reference the exact committed result and Composition hash |
| Risk failure before authorization | Cancelled with structured failure reason | Do not describe a technical failure as an evaluated rejection |
| Risk expiry before authorization | Expired | Preserve workflow deadline evidence and reconcile any reservation |
| Exact financial grant accepted by Fund | RiskApproved | Continue the existing exact authorization route |
| Fund already accepted authorization, but workflow handoff is interrupted | Reconciliation required until original intent is resolved | Never overwrite RiskApproved with a conflicting rejection/cancellation |
| Unknown reservation/Fund commit result | Pending reconciliation | Do not infer absence or release from a missing acknowledgement |

Persist a synchronization checkpoint with the workflow terminal decision, including original command ID, outcome source event/hash, Portfolio/Fund/order/workflow identities, expected Fund version and the intended transition. Dispatch only after this checkpoint commits. Read authoritative acceptance before marking synchronization complete.

Existing Cancel/Expire transitions do not accept RiskApproved. Preserve that guard. Existing RecordRiskOutcome validates result validity at acceptance time: delayed recording of a historically valid rejection therefore needs an explicitly designed evidence-based synchronization path. It must verify the original committed decision and its decision time, allow delayed observation without granting new trading authority, and never refresh expiry to force the old command through.

Choose a dedicated evidence-bound terminal synchronization command where existing semantics cannot meet these rules. Reuse the conventional mapped Command architecture, Fund version fence and operation/source receipts. Preserve `CandidateSha256`'s existing Composition-result-hash meaning; do not substitute the unit-candidate hash.

On version conflict, reread and reconcile exact prior effects. A harmless version advance may permit a new durable reconciliation command with its own identity and the original decision reference; never edit the payload under a previously committed command ID. A conflicting terminal decision remains explicit and requires repair rather than last-writer-wins.

Workflow business outcome and synchronization completion are separate. NoTrade remains visible promptly while the UI shows “Fund update pending” until confirmed. Extend recovery to find terminal workflows with pending synchronization; the existing Started-only Risk recovery cannot do this work.

Release composition discovery ownership through the existing ownership lifecycle when appropriate. Any capacity release must go through Portfolio authority after checking reservation/consumption facts. A terminal workflow does not itself release money. Historical consumed/submitted checkpoints remain visible and excluded from invented execution cleanup.

**Acceptance:** rejection/failure/expiry converge to the correct Fund outcome; delayed sync beyond result expiry; crash before/after workflow append, Fund commit and acknowledgement; duplicate and conflicting commands; version contention; authorization racing timeout; pending reservation reconciliation; no inappropriate release; terminal-workflow recovery after process restart.

## 7. Gate RM-IP04: dedicated Risk history storage and projection

**Dependencies:** RM-IP01; accommodate RM-IP02/03 payloads. **Areas:** Storage TradeDb/schema, Risk projection/recovery, API composition root and existing event journal patterns.

Implement additive Scylla read models consistent with existing TradeDb conventions. Proposed logical tables are:

| Table | Partition / clustering | Purpose |
|---|---|---|
| risk_management_invocation | WorkflowId / InvocationId | Exact immutable invocation/result evidence and source event references |
| risk_management_history | PortfolioId, FundId, UTC evaluation date / evaluated UTC descending, InvocationId | Bounded Fund decision history |
| risk_management_lifecycle | WorkflowId / InvocationId, source revision | Append-only acceptance, handoff and Fund synchronization observations |

Pin physical names, types and nested payloads in RM-IP01 before implementation. Do not duplicate every snapshot in each history summary. Details must resolve to immutable evidence by identity/hash. Preparation failures before a calculation result must still appear as failed lifecycle entries; no fake Risk result is created for them.

Project only verified committed source events. The existing completed Function projector hook alone is insufficient for preparation failures, workflow acceptance and subsequent financial lifecycle. Integrate the appropriate sources and durable progress/receipts, using existing repository recovery conventions. Avoid circular project references.

Identical source/content replay is a no-op; same identity with conflicting content is an integrity failure. Workflow revisions govern update order, not arrival timestamps. Delayed lower revisions must not rewind displayed current state. A crash between exact-row and history-row writes must be repairable without duplicating a decision.

Recovery must discover late commits behind an apparent high-water mark; use existing proven journal/receipt patterns instead of assuming allocated IDs commit in order. Projection outages must not grant authority or roll back valid financial effects. Detail queries reconcile authoritative PostgreSQL state and clearly expose projection lag.

Backfill existing committed Risk completions and workflow snapshots with bounded, resumable reads. Preserve original hashes and label explanations unavailable for legacy schema-1 content. Do not rerun old decisions with current inputs or auto-populate historical explanations. No truncation, silent deletion, TTL or application-data backfill at startup. Provide an explicit resumable maintenance command and a generated-scope qualification before operator use.

**Acceptance:** real PostgreSQL/Scylla replay and backfill tests; missing and half-written projections; conflicting content; out-of-order and late commits; restart at each checkpoint; old schema records; failed preparation; no duplicate history entries; no ALLOW FILTERING or unbounded scans.

## 8. Gate RM-IP05: dedicated queries and client API

**Dependencies:** RM-IP01/04. **Areas:** Trade.Shared queries, Domain.Trade Risk Query actor/context/handlers, API.Nats.Client, API Startup; mirror established OrderComposition query architecture.

Provide typed exact invocation and result queries plus paged Portfolio/Fund Risk history and lifecycle detail. Freeze actor subjects and payload manifests before exposing them. Exact detail must support authoritative lookup when Scylla is delayed and must report unavailable authority distinctly from not found.

History uses required Portfolio/Fund and a UTC date partition, deterministic descending order with identity tie-breaker, default page size 25 and maximum 100. Bind versioned opaque cursors to scope, filters, date and page size; reject malformed or cross-scope reuse. Baseline server filters must be supported by keyed access; do not implement status/horizon filtering by silently discarding rows from arbitrary storage pages. The first delivery can expose these values as columns and exact selections without promising a global filtered search.

Enforce server-side Portfolio/Fund read authorization using existing scoped access conventions on every request, including exact IDs, cached reads and cursor continuation. Do not trust UI visibility as authorization. Production identity hardening remains a separate delivery; current scoped checks are still mandatory.

Return historical decision facts separately from current workflow/reservation/Fund state and evidence timestamps. A previous Authorized checkpoint must not appear as currently executable after expiry/revocation. If live authority cannot be verified, return history with current authority Unavailable rather than a green current-approval flag.

Bound response bytes and query time; support cancellation; include typed correlation and source references. Paginate history rather than returning full snapshots for every row. Avoid one unbounded authoritative lookup per history item: expose bounded summaries and fetch authoritative detail for selected items, with clear freshness semantics.

**Acceptance:** real NATS request/reply; cross-Fund denial; missing/unavailable/projecting distinctions; cursor scope tampering; stable paging under concurrent inserts; duplicate timestamps; maximum page/payload; cancellation; current-state reconciliation; no projection-based authorization.

## 9. Gate RM-IP06: Risk history and observation UI

**Dependencies:** RM-IP02/03/05. **Areas:** UI.Net.Services, UI.Net.Views/Strategy/StrategyObservationForm, PortfolioAdministrationForm, presentation and WinForms system tests.

Provide a reusable read-only Risk detail view reachable from the existing strategy observation screen and the selected Fund in Portfolio Admin. Use existing UI styling and service conventions. The Portfolio entry point opens Fund history; strategy observation opens exact workflow/invocation detail. It must remain useful for failed workflows and historical results with no active book.

### Baseline screens

| View | Required content |
|---|---|
| Fund history | UTC date selection, paged evaluation time, workflow/order identity, horizon/variant, calculation outcome, units and primary reason; loading/empty/unavailable states |
| Decision summary | Exact Composer identity, policy/version, input timestamps, original result, accepted or unaccepted status, current authority checked-at time |
| Sizing explanation | Quantity ceiling and chosen size, cash/margin/fee/variation components, loss budget and market multiplier, failed quantities and binding constraints with units |
| Lifecycle | Prepared, calculated, accepted, reservation pending/committed, Fund pending/accepted, Authorized, rejected/failed/expired; separate Fund synchronization progress |
| Evidence | Expandable IDs/hashes, source versions, funding source, original expiry, projection freshness and linked Portfolio financial receipts |

Show plain-language explanations such as “3 units require $X; available cash at evaluation was $Y.” Keep technical IDs in expandable evidence. Display evaluated values as historical, and current authority as a separate observation. Do not substitute current cash into the old decision explanation.

Distinguish calculation Approved from Authorized intent and from expired authority. Clearly label emulator funding. Show historical results without detailed explanations as “Explanation not recorded for this version.” Unknown commit, failed query and empty history must be visually distinct.

Use asynchronous cancellation-aware loading, bounded paging and an explicit refresh action. Discard responses for a previously selected Fund/workflow; cancel on close. No unbounded timer polling or synchronous UI-thread queries. Preserve full precision in evidence while formatting readable money and units in summaries.

No UI control may submit, consume, release, authorize, edit an old result or trigger automatic re-sizing in this delivery. Existing separate Portfolio administration controls retain their own authorization and workflows.

**Acceptance:** presentation tests with meaningful data states and stale-response races; rendered STA/message-loop tests for history, detail, explanations, failed/expired/pending states and navigation; visual review at supported scaling/window sizes; keyboard navigation and long-text layouts; actual API read integration.

## 10. Gate RM-IP07: recovery and concurrency qualification

**Dependencies:** RM-IP02–06. Verify the complete delivered behavior, not only pure model equivalence.

| Boundary | Required fault/race evidence |
|---|---|
| Preparation/Function | Lost dispatch, completed-event replay, changed duplicate payload, cancellation and fixed deadline |
| Acceptance | Rehashed explanation tampering, late result, stale workflow revision, duplicate completion |
| Capacity | Shared Portfolio contention, withdrawal race, revocation, lost reply and unknown COMMIT using original identity |
| Fund success | Crash after grant before Fund command, after Fund commit before acknowledgement, before Authorized checkpoint |
| Fund terminal outcomes | Rejection/failure/expiry synchronization pending across restart; delayed historical rejection; competing terminal and authorization transitions |
| Projection | Partial writes, event arrival reversal, late source commit, prolonged outage, rebuild and legacy-schema replay |
| Query/UI | Authoritative storage unavailable while history remains readable; stale projection; unauthorized scope; selection changes during request |
| Execution boundary | No consume/submit calls from current or historical checkpoints; no fabricated fill/cancel or release |

Extend the five-stage matrix to verify explained results and exact observation identity across all 12 variants and Daily/Weekly/Monthly. Add real orchestrated workflow-to-Portfolio/Fund handoff cases through registered actors and clients, including one rejection and each terminal synchronization path; retain the existing store-boundary matrix and label its scope accurately.

Recovery integration must include stopped-and-restarted services/processes and durable queries, not merely direct handler calls. Do not run schema-mutating suites concurrently. Use owned generated test identities and a separate NATS broker. Keep test adapters and external-source fixtures explicitly labelled.

Measure explained-calculation runtime, allocation and encoded size at the bounded maximum and normal cases. Verify cancellation within existing stage budgets; document measured distributions and the environment. Do not assert a production SLO from a local passing timeout test.

**Acceptance:** exact new test counts, zero unexplained skips/failures, reproducible failure/recovery evidence, no financial overcommit or duplicate terminal effect, and no delivery path beyond Authorized intent.

## 11. Gate RM-IP08: composition root, release evidence and runbook

**Dependencies:** all prior gates.

Register new query actors/clients, projection services, terminal synchronization recovery and UI services in the existing composition root. Startup verification must validate registrations without running data backfill or financial mutations. Keep projection/query failures independent of the authority gate.

Extend the sequential qualification runner with new query, projection, compatibility, Fund synchronization, UI and integrated cases. Build changed projects and API; run relevant suites and final startup verification. Generate a gate report with exact commands, source revision, test counts, failure/skip status, artifacts, rendered screenshots and fixture boundaries.

Publish a runbook covering schema installation, legacy Risk backfill/resume, projection repair, pending Fund synchronization, unknown commit reconciliation and current-authority outage. Repairs use mapped commands and original evidence; no manual financial row edits or deletion to force completion.

Describe rollout and rollback compatibility explicitly. Old readers must tolerate additive records; old writers must not process new explained/synchronization states if they cannot preserve semantics. A binary rollback must not resume historical execution checkpoints. Choose a documented compatible-version range during RM-IP01 and verify it before release.

Update the specification to distinguish the new implemented behavior from baseline behavior. Link every acceptance requirement to executable evidence. Separately list operational capital/retention/authority setup; do not execute application-data cutover or enter capital as a side effect of software qualification.

**Acceptance:** all new gates have linked evidence, no unresolved delivery-scope defect, deployment/startup composition verified and operator procedures concrete. Push/deployment remain subject to the existing session's approval block; documentation completion does not bypass it.

## 12. Work order and review units

1. RM-IP01 contract manifest, compatibility strategy and decision record.
2. RM-IP02 explained calculations and RM-IP03 terminal Fund synchronization, each as independently reviewable changes after contracts are settled.
3. RM-IP04 committed-source history and resumable projection.
4. RM-IP05 typed queries and client.
5. RM-IP06 UI and rendered verification.
6. RM-IP07 complete recovery/concurrency qualification.
7. RM-IP08 startup, evidence and runbook closure.

Each review unit must state changed behavior, compatibility implications and its relevant tests. Gates with shared schemas run sequentially during verification even where coding work is independent. Preserve existing user changes and keep generated data/artifacts outside source commits.

## 13. Definition of completion

The delivery is complete when a user can find a Risk decision, inspect its exact Composer/Portfolio inputs and calculation explanation, distinguish eligibility from current financial authorization, see Fund synchronization and recovery status, and trace all facts to committed evidence. Rejected/failed/expired workflows must converge safely with Fund state or show an explicit unresolved conflict with recoverable evidence.

Approved workflows must end at one durable Authorized intent with exact reservation/Fund references. History and UI must never create authority. All new gates must pass with reproducible evidence. Automatic re-sizing must retain immutable earlier attempts, prove the absence of an admissible prior reservation, enforce the three-attempt ceiling and never renew expiry.
