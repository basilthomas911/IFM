# Application Backtesting System Functional Specification

**Version:** 0.1  
**Status:** Requirements draft reflecting agreed scope; implementation plan created; implementation not started  
**Date:** 2026-11-09  
**Design:** [Consolidated design, revision 0.3](Application-Backtesting-System-Draft-Design-v0.1.md)

## 1. Purpose and release gate

The initial system shall run the existing trading application over a configured historical interval using real Databento data and isolated storage. Its primary purpose is to prove functional trading correctness over historical time: expected workflow decisions, defined broker/order states, correct positions and reconciled Fund accounting, with no unexplained application errors.

Profitability outputs remain required. Positive profitability, statistical significance, optimization, and sophisticated financial research are not V1 acceptance conditions. Advanced financial analytics are later work.

The [completed source inventories and shared-code feasibility report](Application-Backtesting-Shared-Code-Inventories-2026-11-09.md) define the current evidence and proposed compatibility boundary. This document defines behavior; the [implementation plan](Application-Backtesting-System-Implementation-Plan-v0.1.md) defines delivery stages and tests. A small isolated proof and live-default regression evidence are required before expanding a shared clock/message migration. Creating these documents does not perform an application refactor, database reset, download, service restart or deployment.

## 2. Scope and isolation

- **ENV-01:** Provide an explicit VS Code **API + eGui Rust UI + Rust MarketData Backtesting** launch profile and prepare/start/stop scripts, with full source-level debugging.
- **ENV-02:** V1 runs Development or Backtesting one at a time. A backtesting launcher must not silently stop, attach to or mutate Development.
- **ENV-03:** Use existing PostgreSQL/ScyllaDB servers with restricted credentials, fresh backtesting PostgreSQL databases and tick-only ScyllaDB keyspaces. Isolate NATS scope, durable streams/consumers, IDs, caches, ports, leases, outboxes, logs and process ownership.
- **ENV-04:** Backtesting cannot select live or IBKR Paper endpoints. Unspecified settings do not fall back to Development. The UI and all run metadata identify the Backtesting environment.
- **ENV-05:** Use E: for configured historical/artifact working storage; preserve existing Docker storage placement. New RAID, AWS automation, Redis/Blackboard or Aspire changes are outside V1.
- **ENV-06:** Exercise existing Intrinsic Time workflows for Daily, Weekly and Monthly, ES, required VX inputs and required ES options. MES/additional strategies are deferred unless a selected configuration requires them.

## 3. Back Testing workspace

### 3.1 Placement

- **UI-01:** The only additions to the current application shell are a **Backtesting** menu item immediately after **System** and, when selected, a **Backtesting tab in the existing center-pane tab host**. Use the same host behavior as loading a trade. Preserve all existing tab placement and ordering, including Strategy/Operations/Actors, and preserve both left- and right-pane views, splitters, graphs and other controls exactly. Do not create a replacement tab strip or regroup current views. Input and Output belong entirely inside the new Backtesting tab. No separate window, modal or full-screen replacement is permitted.
- **UI-02:** The workspace remains available throughout preparation, replay, stopping and result inspection. Switching tabs does not affect the run. While active, tab closure is prevented or requires stopping first.
- **UI-03:** Divide the workspace into a visible **Input** section above a resizable **Output** section. Follow existing calendar/typography rules, baseline alignment and 20 px label-to-control spacing.
- **UI-04:** Every configurable run value is available in Input, including values supplied by a selected profile. Show resolved effective values; do not hide material execution assumptions in scripts. Credentials are never shown.

### 3.2 Input contract

| Input group | Values | Validation / behavior |
| --- | --- | --- |
| Trading interval | From and To inclusive exchange value dates; exchange time zone | Both required; From <= To. Server displays resolved UTC/session boundaries. |
| Portfolio/Fund | Initial capital, account currency, approved Portfolio/Fund baseline/allocation | Capital/currency required; allocations and policy constraints must reconcile. Start with no orders or positions. |
| Configuration | Explicit immutable baseline ID/version/hash, strategy definition and parameter/policy versions | Required per run. No unresolved dynamic “latest” reference at execution time. |
| Strategy scope | Selected configured strategy, Daily/Weekly/Monthly horizons and required instruments | Defaults can reflect the chosen baseline; effective dependencies remain visible. |
| Historical sources | Verified Databento manifest/files; versioned rates/calendar/definition sources | Required source identity and coverage; no silent mixing with operational live captures. |
| Acquisition | Maximum approved data spend and approval reference, where applicable | Default authorization is zero. A positive budget alone does not constitute external spending approval. |
| Execution | Execution-profile ID/version, executable quote rules, combo behavior | Required; conservative executable limits are the V1 model. Diagnostic midpoint profiles must be labeled. |
| Costs | Commission model, currency/unit, entry/exit commissions, exchange/other fees and slippage | Explicit per-run effective values, including a deliberate zero. Units must be shown; do not assume approved numeric defaults. |
| Latency | Place/acknowledgement, Change Limit, Cancel and fill-response delays in simulated milliseconds | Explicit non-negative per-run values or a versioned model resolving those values. |
| Fixed V1 policies | Always fresh reset/import; event-driven replay; Stop retains a partial run; range end marks open positions | Show read-only in Input. No Resume toggle and no optional “keep previous mutable data” switch. |
| Resolved setup | Warm-up interval, exact instrument/schema set, database/keyspace aliases and output root | Server-derived, read-only; validate before admission and show without secrets. |

Initial capital, date range, configuration baseline, costs and latency shall remain configurable for every run. A configuration package supplies initial reference/policy data; it does not contain current Development financial history. Numeric values in the accompanying window draft are illustrative only.

### 3.3 Actions and control state

- **UI-05:** Provide centered **Start** and **Stop** buttons. Input is editable while idle/terminal and locked from accepted Start through terminal completion. Start is disabled during an active run; Stop is enabled during cancellable preparation/replay.
- **UI-06:** Start always requests a fresh run, even when the same dates/files were used previously. It cannot resume previous state or skip reimport because a previous import succeeded.
- **UI-07:** Show a fixed notice beside Start: starting again clears prior mutable backtesting data and prior Output. Reset is part of Start, not a separate optional action.
- **UI-08:** A repeated transport delivery of the same Start operation is idempotent and does not trigger another reset/run. A new deliberate Start uses a new operation ID and generation.
- **UI-09:** Reconnection queries current projected run state and does not start another run. Show an error once per incident, retain its reason inline, and show which stage failed.

## 4. Fresh reset, import and initialization

### 4.1 Start sequence

**RESET-01:** After validating the command/settings and accepting a new Start operation:

1. Clear the previous UI Output and show the new reset/import lifecycle.
2. Fence the prior generation; stop its replay, scheduler, actors and broker workers; close producers/consumers and settle or invalidate their transport ownership.
3. Remove all prior run-owned mutable PostgreSQL databases, ScyllaDB keyspaces, messaging state, IDs, leases, outboxes, emulator state, caches, replay offsets, scheduled timers and mutable run working files. Verify removal before proceeding.
4. Start fresh run workers with no retained/static state. Create and migrate fresh databases/keyspaces and record schema identities.
5. Load the selected immutable configuration baseline and initialize Portfolio/Fund capital through normal owning commands or an approved audited bootstrap.
6. Import the requested historical range plus dependency-derived warm-up data into the fresh working stores. Resolve/download missing source files only under the approved acquisition policy.
7. Initialize clocks at the resolved warm-up boundary, restore reference inputs and seed active indicators from eligible original observations.
8. Qualify readiness, then enable normal trading workflows at the configured trade start boundary.

- **RESET-02:** A new namespace is not sufficient if prior mutable run namespaces remain available. Prior-run query endpoints, history lists and UI rows must not expose them after reset.
- **RESET-03:** Reset ownership checks use resolved configured targets and run-generation metadata. Development/production databases, unrelated keyspaces/containers, credentials and canonical archive files are outside the deletion allowlist. This is logical database/keyspace cleanup, not a recursive deletion of arbitrary Docker volumes.
- **RESET-04:** If any producer fails to quiesce or cleanup/import/initialization is incomplete, mark the new attempt Failed/Blocked with details. Do not proceed using stale state.
- **RESET-05:** A minimal current reset-operation receipt survives worker teardown to make retry/recovery safe. It contains operation ID, generation and stage status, not old orders, P&L or prior-run results. A crash during reset resumes that reset transaction, not the old trading run.
- **RESET-06:** Canonical verified Databento files and immutable configuration/reference packages can be reused as source inputs. Working tick imports, database images, aggregate snapshots and projections from a previous run cannot be reused.
- **RESET-07:** Current terminal results remain inspectable until the next accepted Start. V1 has no previous-run history browser. A user-requested read-only export is never an initialization source.

### 4.2 Historical preparation

- **DATA-01:** Input coverage includes futures trades/quotes/definitions/status/statistics, required VX front/back inputs, selected historical option universe and required rates/calendars, with point-in-time metadata and contract multipliers.
- **DATA-02:** Prefer verified local Databento DBN files. Before paid acquisition, expose exact scope, cost estimate and capacity requirement and require explicit authorization. Preserve original compressed files/checksums and version provider corrections.
- **DATA-03:** Validate decoding, session coverage, source/publisher sequences, duplicates, gaps, timestamps, symbol mapping, listing dates, expirations and schema granularity. Unsupported required history blocks readiness; it does not weaken trading rules.
- **DATA-04:** Use inclusive exchange value dates mapped to a half-open UTC replay interval. Respect overnight trading, holidays, early closes and DST. Prehistory is sized by the full dependency chain, including Daily EMA20/50/200 and intraday baselines.
- **DATA-05:** Warm-up cannot place strategy orders. Original source timestamps remain intact. Required non-Databento inputs use historical versions or an explicitly selected and disclosed approximation.
- **DATA-06:** No network/live-history fallback is allowed during replay. Missing needed history is detected in preparation or reported as a run input failure, never filled from today's live feed.

## 5. File-backed IMarketDataApi emulator

**MD-01:** Implement a dedicated Backtesting `IMarketDataApi` backend that presents Databento files as historical live-feed behavior. The API Server and domain consumers retain their normal market-data contracts. Keep this backend in a distinct Backtesting composition root; retain the current live-only native backend guard.

**MD-02:** Reuse Rust DBN decoding, normalization and bounded publication where compatible. The emulator owns file readers, deterministic merge, subscription state, current values and point-in-time discovery. It is not a second strategy engine and does not call a live provider to fill gaps.

| Capability | Required behavior |
| --- | --- |
| Prepare session / current contracts | Resolve historical contracts, roll policy, multipliers and metadata for the simulated session. |
| Start/Stop and backend status | Attach/detach replay coherently; expose generation, state and watermark. |
| Futures/options subscriptions and leases | Support acquire/renew/release/reconciliation used by the application; make duplicate commands idempotent. |
| Last prices and hot read mirror | Expose only observations delivered by the current simulation watermark. |
| Option-chain discovery/composition snapshots | Historical listings and eligible quotes/statistics; coherent per-leg timestamps and ages. |
| Indicator seed history | Return qualified bars/history at or before simulation time; never future records already present later in the same file. |
| Health and diagnostics | Distinguish active quiet subscription, stale quote, session closure, EOF and corruption/failure. No wall-clock false staleness. |
| Recovery | Reconcile current-generation subscription/offset state without duplicate input publication. A new Start always resets rather than resumes. |

**MD-03:** Build and test a complete capability matrix against every `IMarketDataApi` operation reached by the configured trading path. Unneeded operations return explicit unsupported responses; a required unsupported operation fails qualification. Do not assume a tick reader alone implements the full interface.

**MD-04:** Quiet off-hours options subscriptions can be ready without ticks. Quote age still uses actual source time against simulation time. EOF stops normal replay; it is not a live connection outage that triggers endless feed replacement.

## 6. Clocks, timestamps and message compatibility

### 6.1 Clock domains

- **TIME-01:** One authoritative run clock supplies .NET `TimeProvider` and an equivalent Rust clock contract. Microsoft DI, SimpleInjector and run worker processes must use the intended same clock domain/generation. Live defaults continue to select the system-backed provider.
- **TIME-02:** Business session dates, quote age, indicator freshness, order expiry, strategy deadlines and due trading schedules use simulation time. Physical transport/database/socket waits, startup/shutdown and replay-stall watchdogs use real monotonic elapsed time.
- **TIME-03:** Preserve `IValueDateProvider` as a run-local server singleton refreshed from the clock on each read. Completed EOD date advancement remains the last successful Market Close step. New earlier-date runs get fresh worker/provider instances, never a rewind of retained singleton state.
- **TIME-04:** Do not use a mutable process-global clock switch. DTO constructors/getters must not locate DI services. Native `Instant`/Tokio usage is classified by meaning before alteration.

### 6.2 Message origin

- **MSG-01:** Application commands, queries, events, replies, errors and status publications must support a stable UTC origin timestamp obtained once from the authoritative injected TimeProvider at creation. Eliminate dynamic getters such as `OriginatedOn => DateTime.UtcNow` from the qualified trading path.
- **MSG-02:** Stamp at producer/factory boundaries and serialize/persist the value. Preserve it on reads, outbox retries, redelivery, durable replay and projection. Record new wall-clock delivery-attempt timestamps separately.
- **MSG-03:** Carry RunId, generation and causal/message identity at the appropriate shared envelope/contract boundary. A current process rejects old-generation messages before they mutate state. Origin time alone is not an identity or deduplication key.
- **MSG-04:** Keep source exchange/receive timestamps, message origin, logical receipt/processing time and wall diagnostic time distinct. Do not overwrite Databento or broker source facts. `ReceivedOn` and `CreatedOn` require an explicit semantic inventory rather than bulk renaming.
- **MSG-05:** Trading-path messages use the run simulation clock. Pre-run administrative messages use a declared control-plane TimeProvider and time-domain metadata. Both eliminate direct system-time expressions in their creation paths; physical diagnostics remain separate.
- **MSG-06:** Append new MessagePack fields only at verified unused slots, or use compatible envelope metadata. Preserve old payload decoding, hashes, request fingerprints, event identities and duplicate semantics. Legacy absent origin is recorded as absent/legacy, not recomputed from today's clock. Add mixed-version, persisted-event and NATS/outbox compatibility fixtures.
- **MSG-07:** Audit common transport defaults as well as concrete messages. `NatsEventProducer` currently fills a missing `ReceivedOn` using wall time; changing a command property alone does not fix that path.

### 6.3 Advancement

- **ORDER-01:** Version a deterministic merge/order policy preserving each source's sequence and availability time, with stable tie-breakers for metadata, market records, due broker responses, scheduled actions and timeouts.
- **ORDER-02:** Advance to the next eligible logical event only after work due now and its required durable projections reach a causal completion watermark. Empty mailboxes alone are insufficient.
- **ORDER-03:** Register future timers/broker responses in the scheduler and allow waiting work to yield. Do not wait for a future timer to fire while the barrier prevents time advancing to it.
- **ORDER-04:** Freeze trading during initialization; no look-ahead or dropped inputs to increase speed. Preserve valid quote/risk/NoTrade restrictions. Record and fail a real elapsed-time stall rather than silently extending market permission.

## 7. Trading and financial behavior

- **TRADE-01:** Reuse current indicator, ITI, Regime Discovery, Market Condition, Trade Selection, Order Composition, Risk Management, execution and position/exit paths. Preserve horizon-specific dependencies and inactive optional-input policy.
- **TRADE-02:** Use the broker emulator with a simulation-clock-backed `IEmulatorClock`, explicit per-run costs/latency and historical executable quotes. Do not alter live/Paper registrations or financial command/event semantics to make a test pass.
- **TRADE-03:** Preserve Place/Change Limit/Cancel pending states, rejection, reconciliation, partial fills and late fills. All outcomes remain known or explicitly recoverable; no duplicate financial execution is acceptable.
- **TRADE-04:** Reuse Portfolio/Fund/order/position aggregates; no second authoritative backtest ledger. Only execution/settlement facts affect fills and financial position.
- **TRADE-05:** Marketable limits use qualified executable bid/ask evidence; spreads use atomic combo semantics for V1. Do not assume independent favorable leg fills or Net Mid execution. Missing supported expiration/assignment behavior must block an affected range or be supported explicitly.
- **TRADE-06:** Expected NoTrade and rejected-order business outcomes are retained with reasons. NoTrade is not a failed run, and an unknown input cannot be relabeled as a successful trading signal.

## 8. Output section

All results below shall be accessible inside Output, using read-only projected data and detail views. Output is empty for a new run until current-generation evidence arrives; it cannot show previous results as a placeholder.

| Output view | Required content |
| --- | --- |
| Progress / run health | RunId, state, reset/import/warm-up steps, simulated UTC/local time, value date, range progress, errors and readiness. |
| Summary | Daily, monthly and whole-run views split by Long and Short: trades, wins/losses, win rate, points, gross gains/losses, gross/net realized P&L, commissions/fees, average win/loss, drawdown, unrealized P&L and open quantity/exposure. |
| Trades and Fills | Entry/exit dates, side, quantity, contract/legs, entry/exit/fill prices, points, original/revised limits, state/status, partials, commissions and P&L; order/fill drill-down. |
| Decisions | ITI/workflow counts by horizon, stage results and input evidence, Selected/NoTrade reasons, rejected orders and errors; distinguish expected restrictions from defects. |
| Data and Performance | Coverage/warm-up, gaps, stale/invalid observations, missing definitions, source/import counts, replay throughput, simulated and wall duration, queue pressure, barrier/stage timings and storage usage. |
| Settlement | Initial/effective capital, Fund allocations, cash/reservations, realized/unrealized totals, commissions, position/quantity reconciliation and open or unsettled exposure. |
| Run Details | Frozen effective inputs, source/config/code/calendar hashes, execution/ordering policies, UTC boundaries and schema/generation identities. |

- **OUT-01:** Derive results from the same authoritative financial ledger conventions as trading. Costs are counted once; margin/reservations are not trading losses. Expose reconciliation failures.
- **OUT-02:** Points retain contract/multiplier/quantity units and direction. Do not combine unrelated instrument points or replace Long/Short views with net points. Preserve trade/strategy-side classification rather than counting each option leg as a separate trade.
- **OUT-03:** Mark open positions at qualified range-end executable evidence. Missing marks remain unavailable; do not assume zero unrealized P&L. Identify which costs/marks are modeled.
- **OUT-04:** Profitability is descriptive output for functional qualification. More sophisticated attribution, statistical comparisons, optimization, scenario analysis and research metrics are deferred.
- **OUT-05:** Do not fabricate sample results in the real UI. The accompanying visual draft uses empty result placeholders and clearly labeled example settings.

## 9. Lifecycle and stop

`Created -> Validating -> Resetting -> Importing -> Initializing -> Ready -> Running -> Draining -> Completed`

Preparation can wait for explicit data-cost authorization. Stop transitions active work through Stopping to Stopped; unrecoverable errors record Failed. Each accepted attempt has one terminal outcome and an idempotent operation receipt.

Stop blocks new replay admission and freezes at a consistent watermark, settles/records already-admitted work within real-time cleanup bounds and preserves a partial current result. It does not fabricate rejection, settlement or fills. Normal range end marks remaining open positions without forced liquidation; report unrealized/settlement status explicitly. A subsequent accepted Start clears the entire current mutable run and imports anew. Whole-run resume is deferred.

## 10. Required tests and functional acceptance

| Test group | Minimum acceptance evidence |
| --- | --- |
| Isolation/reset | Start twice, including the same interval; prove the second run cannot read first-run rows/messages/orders/snapshots/caches, imports occur again and Development/immutable source checksums are unchanged. Test interrupted reset, wrong ownership and late old-generation messages. |
| Configuration/Input | Every material per-run setting is visible, validated, locked during execution and frozen in the manifest. Reject unresolved baseline versions and inconsistent capital allocations/cost units. |
| Clocks/messages | Stable origins under repeated reads, round-trip serialization, persisted/outbox retries and legacy payloads; original provider timestamps preserved. Test date rollover/DST, order expiry, virtual timers and real watchdogs in .NET and Rust. |
| IMarketDataApi emulator | Contract capability tests, real DBN decoding, subscriptions/quiet readiness, point-in-time chains, no future seed history, coherent snapshot ages, EOF/closure/fault distinctions and explicit unsupported methods. |
| Normal application paths | Real isolated PostgreSQL, ScyllaDB and NATS; all active indicators initialized; actual historical ITI drives Daily/Weekly/Monthly workflows without synthetic market triggers. Preserve expected NoTrade. |
| Trading lifecycle | Place/change/cancel, lost reply/reconcile, reject, partial fill/cancel, late fill, open/close positions, fees and Fund settlement; verify restart within the current run where supported and no duplicates. |
| Output/UI | Input/Output sections, menu placement, persistent center tab, Start/Stop/reconnect, current-generation result isolation, Long/Short period views, drill-down and single incident notifications. |
| Live regression | Unchanged live composition defaults, live feed/backend guards, broker registration, existing financial/state tests, persisted-message compatibility and a controlled Development smoke test after shared changes. |
| Repeatability | Identical inputs and declared ordering/models yield equivalent normalized decisions, fills and financial state; exclude random operational IDs and wall logs from business equivalence. |

Unit, BDD and integration tests are required. Initial functional qualification uses a short complete real historical fixture with enough eligible events to exercise the intended transitions. Absence of a trigger is not proof of workflow coverage; report the unexercised path or select another available fixture. Fault-injection scenarios supplement real-data replay; they are labeled tests, not historical market facts.

**Initial pass condition:** no unexplained application errors, no lost required replay input, no indeterminate broker/order outcome, no duplicate order/fill, no leakage across runs and reconciled accounting. Data problems must be diagnosed and surfaced with the correct restriction/failure, not ignored. A run may pass functional checks while producing losses or valid NoTrade decisions. Noncritical known limitations must be explicitly listed; they do not qualify a broken critical trading path.

## 11. Feasibility proof and implementation gates

The source review identifies useful existing seams but does not prove a risk-free small change. The implementation plan now incorporates design section 17, the shared message/clock inventory, the `IMarketDataApi` capability matrix, native timer classification and causal completion requirements. Per-site classification and the isolated proof remain implementation gates.

The first implementation proof, once authorized, should validate those seams in an isolated disposable run and demonstrate fresh reset/import twice. Shared clock/message changes need a separate compatible migration boundary and live-default regression evidence. If meeting the requirements requires broad trading-rule or actor-runtime redesign, return to design review before expanding implementation.

## 12. Traceability

This specification incorporates the 13 accepted recommendations plus the user's refinements: all run values in Input; all agreed results in Output; mandatory fresh mutable-state reset/import on every Start; TimeProvider-based stable message origins; dedicated historical file-backed `IMarketDataApi`; functional trading correctness first with profitability outputs retained; and feasibility/live-impact review before implementation planning.

The [consolidated design](Application-Backtesting-System-Draft-Design-v0.1.md) is the architecture discussion entry point. Older backtesting/archive documents remain provenance references, not competing V1 implementation mandates.
