# Strategy Risk to Opening Execution Implementation Plan v1.0

Date: November 9, 2026. Status: backend implemented and deployed; automated qualification passed. Live positive-path and manual UI acceptance remain pending. Automatic opening remains gated off in the running Development package until those rollout gates complete.

## Current financial path clarification

The implementation audit identified a newer schema-2 strategy branch in `ExecuteRiskManagement` that calls `PortfolioOrderCompositionFunction`, the same Portfolio approval boundary used by Rust eGui manual openings. It previously dispatched TradeOrder commands before the strategy decision was committed. New automatic openings will use this Portfolio-owned approval/capacity path and the existing TradeOrder, OrderExecution, BrokerOrder and Development emulator lifecycles. The handoff adds durable post-commit delivery and lifecycle evidence; it does not add another broker engine or consume capacity a second time. Historical Risk-Function authorization records remain readable and inactive. References below to the older reservation/consumption path describe the audited legacy baseline, not a second enabled automatic execution path.

The user confirmed that qualification must precede further backtesting changes. Two independent acceptance gates apply: automatic strategy-workflow opening for each requested timeframe, followed by manual open-position trade lifecycle testing for all three families. Existing Iron Condor results do not qualify Vertical Spread or Futures behavior.

Both gates retain the emulator's existing synthetic cash-account economics recorded in the [E10 verification manifest](Trade-Broker-IBKR-Emulator-E10-Verification-Manifest-v1.0.md). Automatic and manual fills use the same settlement, commission and closing-basis rules. Portfolio capacity records the approved working and filled exposure separately. These tests do not qualify broker-equivalent futures margin or authorize Paper/Live trading.

## 1. Objective and acceptance boundary

Complete the path from an accepted Risk Management decision to a confirmed opening position using the Development broker emulator. Preserve all existing Regime Discovery, Market Condition, Trade Selection, composition, risk and financial restrictions. A NoTrade decision remains a successful business decision and never authorizes execution.

The release boundary includes durable handoff, Portfolio-owned capacity admission and settlement, TradeOrder and OrderExecution creation, broker submission and reconciliation, fills, position establishment, Fund lifecycle updates, accounting, projections, restart recovery and Rust eGui visibility. No IBKR Paper or live brokerage connection, core backtesting, or broad execution-policy redesign is included.

Risk authorization, order submission, broker acceptance, fill confirmation and position opening are distinct outcomes. Report each separately. Overall opening-flow success requires confirmed fills, their corresponding open position, correct financial postings and no duplicate business effects.

## 2. Verified baseline

The five decision Functions and controlled full-workflow tests already exist. `AdvanceRiskFinancialHandoff` currently finishes the strategy workflow when its financial handoff reaches `Authorized`; `ExecuteRiskFinancialHandoff` deliberately returns without consumption or submission. Its recovery model similarly excludes authorized and exploratory historical execution checkpoints. Existing tests assert that boundary explicitly.

The downstream Trade domain already has TradeOrder, OrderExecution, BrokerOrder, reconciliation, fills, established trades and positions. `OrderExecutionEventProjector` routes pending broker executions, accounts for confirmed executions, establishes trades and updates the Fund. `OrderExecutionStateRepository` loads the latest complete `OrderExecutionChangedEvent` and uses PostgreSQL atomic persistence. Extend and qualify these paths rather than introducing a second broker or position engine.

Portfolio also has `ConsumeCapacityReservationCommand`, financial receipts, execution reconciliation contracts and a separate `SubmitEmulatorOrderCommand` financial-emulator path. The latter must not become an additional broker submission or fill/accounting authority for the same order. Existing model helpers named `Consume` or `Submit` are not evidence of an active execution path.

Current Development workflow history does not establish end-to-end opening qualification: there are no Order Composition history rows, and recent workflows stop at earlier business decisions or failures. Existing manual emulator fills do not prove strategy-to-position lineage.

Relevant sources:

- [Risk financial advancement](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/Command/AdvanceRiskFinancialHandoff.cs)
- [Risk financial dispatch](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RiskManager/Realtime/ExecuteRiskFinancialHandoff.cs)
- [Risk recovery](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RiskManager/Model/FinancialWorkflowRecoveryModel.cs)
- [Existing five-stage qualification](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/RiskManager/Docs/RiskManagement-Full-Workflow-Qualification.md)
- [Execution projector](../../TomasAI.IFM.Domain.Trade/Order/Execution/Command/EventProjector/OrderExecutionEventProjector.cs)
- [Execution snapshot repository](../../TomasAI.IFM.Domain.Trade/Order/Execution/Command/State/OrderExecutionStateRepository.cs)
- [Actor implementation conventions](Actor-Implementation-Conventions.md)
- [Actor event modeling conventions](Actor-Event-Modeling-Conventions.md)

## 3. Ownership and execution contract

### 3.1 Recommended ownership

Add a narrowly scoped, PostgreSQL event-sourced `StrategyExecutionHandoffCommandActor` beneath `Domain.Trade`. It owns acceptance of one authorized strategy opening intent and the durable progress between financial admission and the existing execution lifecycle. It does not calculate Risk again, own the financial ledger, submit directly to IBKR, or create an independent copy of broker execution state.

Risk owns its approved decision and immutable authorization. Portfolio owns accepted capacity, financial obligations, Fund state and accounting. TradeOrder and OrderExecution own the executable order and execution attempt. BrokerOrder and the Development TradeBroker emulator own outbound operations and authoritative broker evidence. Existing strategy trade/position actors own resulting positions. Queries read projected models only.

Keep the existing strategy decision workflow completion compatible and label it as decision completion/Risk authorization. Reuse the existing Trade Orders, Order Fills, Position and Reconcile views for actual execution state. The projected handoff correlates workflow/order/execution identities; each registered Fund trade carries `Strategy:<workflow ID>` in its displayed InstructionReference. Do not silently redefine historical `Completed` workflows as filled positions. The new handoff completes successfully only after its required opening effects are verified.

### 3.2 Immutable authorized intent

Freeze workflow ID, Risk invocation/revision, authorization/reservation IDs and versions, Portfolio/Fund/book/order/trade identities, composition and sized-order hashes, environment, account alias, instrument IDs, leg ratios and sides, strategy units, multiplier/currency, signed net-price convention, limit constraints, validity/session policy and originating message timestamps.

Derive a stable handoff ID and downstream operation IDs from the authorized intent and operation purpose using existing identity conventions. Do not allocate new order IDs, trade IDs, execution attempts, consumption IDs or broker references when replaying a lost reply. A matching duplicate returns its recorded outcome; the same identity with different content is an explicit conflict.

A refreshed optimistic-concurrency version creates a new prepared command attempt ID for that version while retaining the original business and financial operation identities. An unchanged request retains its original command ID. This prevents retry payload conflicts without allocating another order or releasing capacity twice.

Use the shared server TimeProvider for new operation times and timers. Preserve source/broker occurrence times and original message provenance. An expired intent cannot initiate a new external action, but expiry must not prevent reading a committed receipt or reconciling an already submitted order.

### 3.3 Proposed handoff lifecycle and snapshots

| State | Evidence required to advance |
| --- | --- |
| Accepted | Complete immutable authorized intent durably committed |
| FinancialAdmitted | Committed schema-2 Portfolio decision owns the accepted capacity; no second consumption |
| OrderCreated | Original allocated Fund instruction and TradeOrder created idempotently |
| OrderApproved | Original executable order approved |
| OrderReady | Readiness committed together with the immutable binding timestamp |
| ExecutionLinked | Existing execution owner durably accepts the exact order and financial linkage |
| Opened | Confirmed filled quantity and matching position, Fund and accounting evidence verified |
| Rejected, Cancelled or Expired | Authoritative terminal evidence; no unaccounted exposure or unresolved obligation |

Broker pending/partial states remain in the existing execution owner. Reconciliation and opening-effects wait reasons are durable fields on the handoff rather than duplicate broker-state enums.

Partial quantity, working remainder, rejection/cancellation reasons and reconciliation state are separate fields. A partially filled cancelled order can leave an open position; never report it as a zero-exposure cancellation. `Opened` records the actual units and remainder outcome rather than assuming the requested quantity filled.

Define an immutable full-state `StrategyExecutionHandoffStateSnapshottedEvent` carrying the intent, requests, receipts, execution links, known fills, quantities, applied-effect references, reasons and source progress. The creation event is a valid initial snapshot. Save a reconstructible full-state snapshot on every handoff transition and load the latest through the existing single-query checkpoint mechanism. Downstream aggregates retain their own complete snapshots.

## 4. Ordered implementation work

| Phase | Work and deliverable | Exit gate |
| --- | --- | --- |
| 1. Contract and accounting map | Trace current TradeOrder creation/approval/binding and BrokerOrder routes. Map Risk authorization, consumed capacity and existing execution accounting to one financial obligation and posting authority. Define contracts, lineage, state transitions and compatibility rules. | One selected broker-emulator path; no double consumption, submission, settlement or new identity allocation on retries. |
| 2. Durable handoff | Implement the new actor, extension handlers, complete snapshots, typed commands/events, projected query models and durable post-commit dispatch. Persist intent acceptance before calling Portfolio. | Replayed or concurrent acceptance produces one handoff and stable downstream requests. |
| 3. Capacity and execution admission | Verify the committed Portfolio decision, materialize/bind the existing TradeOrder and OrderExecution once, and retain the original capacity/authorization lineage. Validate actual order content at admission. | No second consumption or broker submission before committed financial approval and execution ownership; failed creation remains recoverable. |
| 4. Broker operation recovery | Connect through existing BrokerOrder/TradeBroker emulator Place, Change Limit, Cancel and status lookup. Preserve pending and unknown outcomes, broker ID mappings, operation revisions and late callbacks. | Lost replies and restarts recover by lookup; no replacement order appears as a side effect of Reconcile. |
| 5. Fills and opening effects | Reuse/extend fill deduplication, accounting intents, established trades, strategy positions and Fund updates. Record actual partial exposure; retry failed effects from durable evidence. Reconcile financial obligations and release only the proven unfilled remainder. | Filled units, position units, fees, cash/capacity effects and Fund state agree after replay. |
| 6. UI and operations | Keep Risk authorization distinct from actual opening. Reuse Trade Orders, Order Fills, Positions and Rust eGui Reconcile, with workflow lineage in InstructionReference and allocated order/trade IDs. | UI reflects projected broker/financial evidence and never infers Filled/Open from authorization or a request acknowledgement. |
| 7. Qualification and Development rollout | Run the test matrix below, deploy behind a Development-emulator-only switch, execute disposable openings, restart and reconcile, then publish evidence. | All acceptance gates pass; no unresolved exposure, financial mismatch or duplicate effects in the test scope. |

Commands obey current parse/validation/receive maps and dedicated extension-handler conventions. Command producers prepare external inputs; pure domain computations and guards precede state-owned event application. External effects occur after source commit through durable retryable delivery. A failed or ambiguous delivery is not a reason to fabricate success or replace a request identity.

The committed `Authorized` workflow snapshot must enqueue the handoff through durable post-commit delivery, even though the decision workflow is already `Completed`. Recovery must discover undelivered authorized intents through that durable delivery record; the current recovery filter restricted to `Started` workflows cannot be reused unchanged. Enlist the delivery intent with the source event where supported, and make repeated delivery to the new owner idempotent. Apply the rollout boundary so this recovery does not automatically execute historical authorizations.

## 5. Reconciliation and duplicate prevention

Reconcile uses authoritative financial receipts and broker order/execution lookup, together with committed IFM events. A projected read row is useful for discovery but is not proof that an operation never occurred. A lookup timeout or inconclusive “not found” response is not permission to submit again.

Before any permitted retransmission, verify the original operation identity, exact payload, adapter idempotency guarantees and authoritative absence/acceptance evidence. If the emulator cannot safely deduplicate the original client reference, keep the operation reconciliation-required until it can be resolved. Do not claim distributed exactly-once delivery; require duplicate business effects to be prevented by durable identities, uniqueness and idempotent handlers.

Enforce uniqueness for the authorized-intent handoff, consumed reservation operation, execution attempt, logical broker order mapping, broker execution/fill identity and financial posting identity. Match immutable fingerprints on replay. Scope identities by the required environment, broker account and Portfolio/Fund boundaries.

Reconciliation must handle crashes after intent commit, consumption commit, TradeOrder creation, broker acceptance, fill receipt, accounting commit, position creation and Fund update. Reject stale status regressions; process previously unseen valid late fills even after cancellation. Duplicate fills add neither exposure nor commissions. Corrected financial costs use the existing amendment contract rather than inventing another fill.

New submissions stop when the execution switch is disabled. Recovery, status lookup, fill processing and settlement of already accepted orders continue. Recovery has bounded retries/backoff and visible durable errors; timeout never silently releases an obligation with uncertain broker exposure.

## 6. Required automated tests

### 6.1 Unit tests

Extend `TomasAI.IFM.Domain.Trade.UnitTests` and `TomasAI.IFM.Domain.Portfolio.UnitTests`:

1. Exact intent mapping for futures, supported two-leg spreads and four-leg Iron Condors, with signed limit price, ratios, quantities, maturity, multiplier and currency preserved.
2. Every legal transition and invalid transition; NoTrade, rejected Risk, foreign Fund/account/environment, mismatched hashes/versions, insufficient capacity, expiry and revoked authority cannot submit.
3. Same-ID/same-payload replay, same-ID/different-payload conflict, concurrent admission, stale callbacks and deterministic downstream identities.
4. Unknown consumption, creation, Place, Change Limit and Cancel replies preserve pending/reconciliation state and original identities.
5. Duplicate/out-of-order fills, two distinct fills at the same timestamp/price, late fill after Cancel, partial fill followed by rejection/cancel and corrected commissions.
6. Financial conservation: reserve/consume/working obligation/filled exposure/released remainder agree; no premature release, duplicate fees, double accounting or opposite position.
7. Source commit failure dispatches nothing; post-commit dispatch failure remains recoverable. Query handlers cannot read command state.
8. Fake-TimeProvider tests for deadlines, expiry, retry scheduling and preserved message origin times; historical receipts remain reconcilable after expiry.
9. Snapshot reconstruction includes deduplication and pending-operation evidence, not merely the most recent displayed status.

### 6.2 BDD tests

Add scenarios to `TomasAI.IFM.Domain.Trade.BDDTests` and the relevant Portfolio BDD project:

| Given / action | Required outcome |
| --- | --- |
| Approved workflow, valid capacity, emulator accepts and fills | One opening order, expected open units, correct Fund and accounting state |
| Workflow ends NoTrade or Risk rejects | No consumption and no broker submission |
| Place accepted but reply lost; restart; Reconcile | Existing order/fill recovered; no duplicate order |
| Consumption commits but reply is lost | Original receipt recovered; capacity consumed once |
| One of two units fills, Change Limit reply lost | First fill preserved; new limit applies only to working remainder |
| One of two units fills, Cancel reply lost | Filled unit remains open; only authoritative unfilled remainder is cancelled/released |
| Last unit fills late after Cancel | Real fill wins over stale status; correct two-unit exposure and accounting |
| Both amendment and cancellation replies lost | Reconcile each original operation; no second submission or lost fill |
| Accounting, position or Fund projection fails after fill | Broker fill remains known; failed opening effects resume once after restart |
| Authorized intent expires before submission | No new broker order; reservation/obligation reconciled before safe release |

### 6.3 Integration tests

Extend the existing `TomasAI.IFM.Domain.Trade.IntegratedTests` project, especially `FullWorkflowRiskTests` and broker/accounting runtime fixtures, plus `TomasAI.IFM.Domain.Portfolio.IntegrationTests`. Use isolated PostgreSQL databases/schemas and NATS, required projection stores, real actor dispatch, production financial persistence and the Development TradeBroker emulator. Do not substitute the separate financial-only emulator for the broker path being qualified.

Run the full five-stage workflow from its admitted start command through a confirmed opening position. Cover Daily/Weekly/Monthly and the supported future/spread/condor scenarios already represented by the full-workflow suite. Positive scenarios use explicitly labelled deterministic market fixtures and test policies; add a negative test proving production NoTrade restrictions remain intact. Test the real ITI admission route separately so direct workflow-start testing is not mistaken for signal-subscription qualification.

Inject crashes at every boundary listed in section 5 and real lost NATS replies at consumption and broker operations. Restart actors/processes and use production status lookup/Reconcile. Repeat reconciliation and replay concurrently, and assert stable unique rows, event identities, broker orders, fills, positions and financial postings.

For PostgreSQL latest snapshot loading, create an initial stream and multiple complete snapshots separated by events, including snapshots at every pending/unknown phase. Restart with no hot cache; verify the newest reconstructible snapshot plus its tail restores all state. Include alias-compatible historical events, a dense history, corrupt/incomplete snapshot refusal and stream resolution in both full-replay and snapshot timing paths. Rebuild projected execution and workflow views from events and confirm Reconcile can discover pending orders after restart.

Run full Trade unit, BDD, Integrated and Verification suites; Portfolio unit, BDD, Integration and Verification suites; relevant BrokerAccount/TradeBroker emulator suites; actor/query convention audits; and Rust codec/controller/NATS/UI tests for changed contracts and status displays. Record skips and external blockers explicitly. Automated tests must not contact a Paper or live broker.

## 7. Development emulator acceptance

1. Capture baseline process identities, configuration, test counts and existing order/position counts. Enable only the Development emulator route and use a disposable, separately identifiable Portfolio/Fund and unique run correlation.
2. Use the actual Development API, NATS, Rust Market Data Host and Rust eGui. Select valid current market inputs; off-hours may use the existing explicitly labelled Development historical-pricing policy. Preserve the actual source time and freshness/eligibility rules. Do not relax live trading policy to create a happy path.
3. Observe an eligible strategy-triggered opening through all five operators, authorization, consumption and broker execution. For deterministic failure cases, use the isolated emulator controls; do not hand-inject downstream success events and call that full-workflow evidence.
4. Verify the order and position in Rust eGui, including units, sides, fill prices, fees, Fund status and linked workflow. Treat an ordinary NoTrade as valid but insufficient positive-path evidence.
5. Repeat missing Place reply, partial fill with missing Change Limit reply, partial fill with missing Cancel reply and a late fill after cancellation. Restart Development and operate the existing Reconcile button against each original order.
6. Read back PostgreSQL source events, financial receipts and projections plus emulator order/execution records. Repeat Reconcile and restart again: all identifiers, quantities, fill prices, fees and postings must remain unchanged.
7. Confirm API, market-data, scheduler and UI recover normally; check logs for unresolved handoffs, repeated modal errors, orphaned obligations and duplicate-prevention conflicts. Restore the normal Development configuration. Retain verification evidence; any cleanup is restricted to the disposable test scope.

Failure to obtain a genuinely eligible live Development signal is a live-verification blocker, not permission to weaken restrictions. Controlled end-to-end integration evidence remains separately reportable.

## 8. Measurements and release safeguards

Capture elapsed time and retry counts for authorization-to-handoff, consumption, broker acceptance, first/final fill and fill-to-position/Fund completion. Also measure restart-to-discovery and Reconcile-to-convergence, queue depth, pending-age and duplicate-effect counters. Separate emulator-configured fill latency from application processing time. The current path stops at authorization, so report missing baseline stages as not applicable rather than claiming a fabricated before/after improvement.

Compare latest-snapshot loading with full replay using identical histories and stream-resolution work on both paths; retain raw samples and environment details. Use failure correctness and eventual convergence as acceptance criteria, with concrete bounded deadlines taken from reviewed configuration and deterministic test fixtures rather than an unexplained global timeout.

Roll out disabled by default. Require an explicit Development environment, emulator account/adapter and execution-enabled setting. The admission switch must fail closed for Paper, Production and unknown environments. Protect existing manual order paths and existing fills; do not automatically execute historical Authorized/Completed workflows on deployment. Identify old pending obligations in a read-only migration report and reconcile them individually before any explicit adoption. Version contracts additively and preserve snapshot compatibility.

Rollback disables new strategy execution admission while retaining the compatible runtime needed to reconcile accepted orders, process late fills and finish financial/position effects. Do not roll back to a binary that cannot read new accepted execution states while those states remain active.

Final delivery requires a test report, fault-injection matrix, source-to-position lineage, snapshot/replay evidence, UI verification, timing results, duplicate-count assertions and a list of any unresolved blockers. No real brokerage orders are part of this acceptance.

### Qualification evidence recorded November 9, 2026

The implementation now uses a durable, source-verified schema-2 opening handoff with stable workflow/order/execution identities, complete latest-state snapshots, post-commit dispatch, financial capacity settlement and bounded recovery. Genuine expired unsubmitted approvals release capacity through a source-proven Fund command without placing an order. Historical authorizations remain inactive. New admission is disabled by default and restricted to Development plus the emulator when enabled; recovery of already accepted work remains available.

Seven isolated PostgreSQL/NATS/real-emulator integration scenarios passed: normal Daily futures, Weekly vertical spread and Monthly Iron Condor openings; the same three openings with lost broker replies followed by a new host and repeated Reconcile; and expired approval with no submission. These use labelled deterministic market fixtures, not current market eligibility. Original broker orders, execution IDs, fill prices, position quantity, Fund cash and financial receipts survived host replacement and repeated reconciliation without duplicate effects.

| Family | Authorization to confirmed opening | Host restart and repeated Reconcile | Lost-reply restart and convergence | Median full replay / latest snapshot |
| --- | ---: | ---: | ---: | ---: |
| Daily futures | 3,263.182 ms | 1,836.032 ms | 4,551.211 ms | 19.458 / 7.270 ms |
| Weekly vertical spread | 3,483.376 ms | 1,713.811 ms | 4,517.514 ms | 20.3202 / 7.7764 ms |
| Monthly Iron Condor | 4,592.837 ms | 1,830.470 ms | 5,670.530 ms | 24.484 / 6.4712 ms |

Snapshot comparison uses five samples from the same normal-opening history and includes stream resolution in both paths. These are local full-debug correctness measurements, not throughput or broker-latency benchmarks. The old workflow ended at authorization, so there is no valid before measurement for opening completion. Raw samples and assertions are in `E:\IFM\StrategyOpeningExecution\20261109\trade-integrated-release-gate.trx`.

The separate Trade unit suite passed 1,743 tests; Portfolio unit/BDD/integration/verification passed 516/35/223/79 tests respectively; Trade BDD and verification passed 62/72; TradeBroker unit passed 43. Full Trade integration passed 174 tests with three explicit skips. Portfolio integration has five explicitly skipped external qualification cases. The final expiry retry change passed its focused PostgreSQL integration rerun. All command, query, event, Function and realtime actor convention scripts passed. The selected Rust codec, broker, strategy-workspace and typed-contract suites passed 30 tests, including additive workflow-lineage compatibility.

Broker qualification fixtures now use fresh account/ledger identities. Opening qualification stops the assembly-shared actors while its separate runtime owns the shared-database outboxes, then verifies those actors restart. This prevents an unrelated emulator adapter from claiming the test's orders and avoids reusing exhausted synthetic balances.

Development cutover applied the two additive tables and pending-handoff index, installed the hash-verified debug API package, and restored API, Rust Market Data Host, scheduler, Server Manager and eGui 0.1.0. Readiness is Healthy and no new API Error/Fatal startup entries were found. Before/after counts are unchanged: 28 TradeOrders, 28 executions, 68 fills, 14 established trades and 14 current positions. Historical authorizations were not executed. The magenta Other ITI Event legend/markers were separately confirmed visually by the user.

The existing E10 emulator synthetic cash-account convention is retained for all three families, including futures. These results do not qualify actual broker margin, Paper trading or live trading. The normal Development query confirms schema-2 Daily/Weekly bindings. Before cutover, Daily was blocked by Unclassified condition/Poor liquidity and Weekly by Unknown stress evidence. A new Daily run at 21:56 local time failed Regime Discovery with missing VX and intraday observations during startup; readiness became Healthy afterward, but a fresh post-warm-up pass remains unverified. Monthly has not supplied a fresh eligible positive case; its last failed run predates this restart. Live positive-path qualification requires genuinely eligible inputs and must not weaken these restrictions. The three-family manual lifecycle gate below remains separate and uncompleted. Desktop automation can read eGui but returns `coordinate input geometry is unavailable` when clicking, so actual manual UI operation requires the workstation user. Core backtesting has not started.

## 9. Manual lifecycle gate before backtesting

| Timeframe | Strategy family | Required manual Development emulator qualification |
| --- | --- | --- |
| Monthly | Iron Condor | Open and verify the four-leg position, Fund and accounting outcomes |
| Weekly | Vertical Spread | Open and verify the two-leg position, Fund and accounting outcomes |
| Daily | Futures Outright | Open and verify futures quantity, entry price, multiplier, fees and approved capacity exposure |

For each family, test normal fills, partial fills and remainder handling, lost Place/Change Limit/Cancel replies, authoritative rejection, late fills, restart and Rust eGui Reconcile. Read back original order/fill identities, quantities, prices, commissions, Fund state and positions after repeated reconciliation; verify no duplicates or unresolved financial obligations. Keep manual and automatic workflow evidence separate and label deterministic fixture prices versus current market prices. Backtesting implementation resumes only after both gates have passed or an explicit blocker has been reported.

### Manual command-path qualification, November 10, 2026

The new `ManualTradeRoundTripTests` exercise CreateManualOrder and AddManualTrade as separate real API operations, with a NewTrade opening matching Rust eGui's request. They then use the manual Portfolio order-composition Function, Create/Approve/Ready/Bind commands, the real Development broker emulator, actual matched market quotes, Fund lifecycle handoffs and PostgreSQL accounting. After adding a separate closing setup, the close reverses the original position and settles before the host is destroyed. A new host reads the same durable emulator ledger and reconciles both original orders twice. No synthetic fill or position-success event is injected. The generic Testing host explicitly receives the existing trusted Development-emulator policy; production financial gates are unchanged.

| Family | Create/add/open/add close/settle | Restart and repeated Reconcile | Execution orders | Leg fills | Positions |
| --- | ---: | ---: | ---: | ---: | ---: |
| Monthly iron condor | 7,022.982 ms | 1,733.321 ms | 2 | 8 | 1, closed |
| Weekly call debit spread | 5,675.822 ms | 1,520.650 ms | 2 | 4 | 1, closed |
| Daily long futures | 8,874.702 ms | 2,107.122 ms | 2 | 2 | 1, closed |

These are isolated full-debug correctness timings, including host setup and labelled fixture authority, not live order latency or a performance comparison. Raw results are in `E:\IFM\ManualTradeLifecycle\20261110\manual-roundtrip-final.trx`. The test validates cash after actual prices and commissions, released broker reservations, flat broker exposure, Completed execution orders, the Closed Fund order and position, and unchanged execution hashes, broker orders, fills and ledger postings after restart and Reconcile. Duplicate create, evaluation and bind requests are also exercised.

The Add Trade failure on Development order 4001 came from a UI selection gap: selecting a Fund implicitly selected its first order but cleared its trades. PostgreSQL already contained primary NewTrade 4301, while the UI inferred another primary from the empty cache. Order refresh now loads its selected trades atomically. Preparing or submitting Add Trade refreshes canonical records before deciding primary/closing, and Add Trade is unavailable while an existing opening draft still needs placement. The domain compatibility guard remains intact. Closing futures draft action now reverses the opening Buy/Sell action. The existing trade is preserved; it is not deleted or replaced to bypass the guard.

Rust protocol tests cover default Fund/order selection, failed-read atomicity, the deliberately cleared stale cache, no additional mutation, lost acknowledgements and exact manual request metadata. Creating an order now selects that newly created empty order; a controller test performs create-then-add-opening for each family. A separate concurrency guard prevents an opening dialog from silently becoming a close when another client changes the primary trade. Payload tests cover Monthly iron condors, Weekly spreads and both futures closing directions. Three new Fund BDD cases cover opening/closing transitions, premature and duplicate opening rejection, and restoration of Closed state.

The full Trade integration suite passed 177 tests, with three existing explicit skips. Trade/Portfolio unit suites passed 1,743/516, their BDD suites passed 62/38, and broker unit tests passed 43: 2,579 distinct .NET passes. The final focused three-family rerun passed the additional execution-hash, reservation-release and terminal-order assertions. Suite counts and individual round-trip evidence are recorded in `E:\IFM\ManualTradeLifecycle\20261110\manual-lifecycle-report.json`.

Native Rust verification passed 31 distinct tests, and the final nine protocol scenarios passed, including the three-family create-order/add-opening sequence. Protocol error/timeout messages are deliberate fixture fault injections. The installed full-debug eGui package has SHA-256 `AED94A3D61C7D2D4F48F0E269A828931E39F19FA543E3946288F25FA380A38A7`; PDB symbols are present. Development financial counts remain 28 execution orders, 28 executions and 68 fills.

At the final Development check (05:11 EST), API readiness was Healthy; startup had eight satisfied activities with zero failures, indicators were Warm, both futures datasets and the Options dataset were Healthy with no reported dataset error, and API, Rust Market Data Host, scheduler, Server Manager and eGui were running.

The updated full-debug eGui 0.1.0 is installed in Development. Manual screen operation is still distinct from this automated application-path coverage: desktop accessibility reads the running UI, but clicking returns `coordinate input geometry is unavailable`. The three-family operator-driven UI gate remains deferred. Automatic workflow openings remain disabled, valid NoTrade restrictions are unchanged, and no Paper/live broker or core backtesting work was started.

Fresh Daily/Weekly workflows at 04:25 EST and Monthly at 04:10 EST on November 10 passed Regime Discovery with restored version-2 sets and completed specialist evaluation, ending in valid Trade Selection NoTrade. This supersedes the earlier unverified post-warm-up status recorded above.
