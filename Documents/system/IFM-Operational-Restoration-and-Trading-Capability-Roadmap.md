# IFM Operational Restoration and Trading Capability Roadmap

**Document type:** System-wide capability roadmap and milestone contract  
**Status:** Active planning baseline; milestone descriptions do not authorize implementation  
**Created:** 2026-08-13  
**Last updated:** 2026-08-18
**Owner:** IFM engineering

## 1. Purpose

This document separates two goals that must not be treated as the same delivery milestone:

1. restoring the complete IFM system and its optimized MVVM-based WinForms UI to the last-known operational capability used for trading more than three years ago; and
2. adding the broker, account, strategy, execution, monitoring, safety, and qualification capabilities required for controlled paper trading and eventual live trading.

The optimized WinForms restoration is a major system milestone. It proves that the modernized actor runtime, storage, NATS messaging, domain behavior, application APIs, event consumers, Models, ViewModels, and legacy WinForms presentation work together as an integrated system with the business behavior that previously existed. It does **not** by itself prove that the new paper-trading capabilities exist.

This document is the authoritative high-level milestone map. Detailed subsystem designs and implementation plans remain separate documents and require review before implementation.

## 2. Terminology and readiness levels

| Term | Meaning |
| --- | --- |
| Legacy operational restoration | Current system behavior is restored to the last-known usable trading-system baseline, with current architecture and optimization improvements |
| Broker foundation | Application-facing broker contracts, emulator, account information, storage, reconciliation, and account actor capabilities exist |
| Manual paper trading | An operator can complete a fully manual order lifecycle against the broker emulator or approved paper broker |
| Automated strategy pipeline | Market state can produce a risk-approved executable strategy proposal through owned actors |
| Automated monitoring and exit | Open strategies are monitored and can produce risk-controlled closing proposals |
| Paper-trading qualification | The complete system passes deterministic, failure, recovery, reconciliation, performance, and extended paper-trading evidence gates |
| Live-trading readiness | A later, separately approved production gate beyond the scope of these milestones |

## 3. Milestone map

| Milestone | Name | Primary outcome |
| --- | --- | --- |
| A | Legacy operational restoration | The optimized WinForms application and existing backend reproduce the last-known operational system behavior and are accepted by the operator |
| B | Trade-broker and account foundation | Broker abstraction, emulator, account queries, account storage, account actor, and reconciliation foundation exist |
| C | Fully manual paper-trading execution | An operator can manually create, approve, submit, monitor, modify, cancel, and reconcile paper orders end to end |
| D | Automated strategy workflow | Regime, market-condition, strategy-selection, order-composition, portfolio-risk, and automated execution capabilities produce controlled broker orders |
| E | Automated trade monitoring and exit | Open strategies are monitored, forward loss is calculated, and exit conditions produce controlled closing orders |
| F | Paper-trading qualification | The combined system passes controlled scenarios and extended paper-trading soak and is approved for paper use |

Milestones are capability dependencies, not estimates or promises of immediate implementation. Work may overlap where contracts are stable, but a later milestone cannot be declared complete while a required earlier capability remains absent.

## 4. Milestone A - legacy operational restoration

### 4.1 Objective

Restore IFM to the functional state last used for trading while retaining the current improvements to actor execution, NATS transport, storage, asynchronous lifecycle, bounded realtime processing, observable ViewModels, error handling, and graceful shutdown.

The active client is the optimized MVVM-based WinForms application. The WinForms views remain legacy adapters over framework-neutral Models and ViewModels.

### 4.2 Capabilities to prove

- application startup, dependency readiness, initialization, and shutdown;
- existing NATS command, query, event, notification, and realtime paths;
- application shell, navigation, status console, and current market state;
- market-data definitions, feeds, futures, options, yield curves, and economic calendar;
- reference-data maintenance;
- fund, transaction, existing order, trade, balance, and profit/loss workflows;
- existing Iron Condor displays, calculations, plans, positions, and legacy order information;
- end-of-day and system-administration workflows;
- current database-backup UI behavior where it is already part of the restored system;
- UI-thread safety, bounded high-rate display processing, lossless business-event handling, lifecycle ownership, diagnostics, and clean process exit; and
- repeated open/close and restart behavior without stale screens, duplicate listeners, detached tasks, or unbounded growth.

### 4.3 Explicit exclusions

Milestone A does not require:

- a trade-broker emulator;
- a complete application-facing trade-broker abstraction;
- full broker account queries or persisted broker account state;
- a broker account actor;
- a new manual broker order-execution workflow;
- automated regime discovery, market-condition classification, strategy selection, order composition, portfolio risk approval, or broker execution;
- automated trade monitoring, forward-loss calculation, or exit-condition evaluation; or
- paper-trading scenarios, simulated fills, or a paper-trading soak.

Existing legacy screens or records that mention orders or trades are validated only for their restored behavior. They must not be mistaken for proof of the new broker-integrated execution capabilities in Milestones B through F.

### 4.4 Exit criteria

- WinForms system-test gates G0 through G4 pass with deterministic evidence or explicitly accepted non-critical limitations.
- Existing operator workflows behave as expected against the current backend.
- Required startup data and integrations for the restored workflows are available.
- Known regressions are fixed or explicitly accepted and documented.
- The operator confirms that the system has returned to the last-known usable operational baseline with the current improvements.
- CommunityToolkit.Mvvm, R3, `IAsyncEnumerable` event listeners, WPF migration, and other presentation refactoring remain deferred until this acceptance unless a separate defect requires a narrowly scoped change.

### 4.5 Current acceptance status

As of 2026-08-18, UI gates G0 and G1 have accepted Development results, and G2-001 through G2-019 are also accepted. G1 proves the initialized shell and status history, current market-outlook state, ES and VX sidebar charts, all five economic-calendar ranges, supported Market Data and Reference catalogs, named-fund and existing trade views, supported System Administration behavior, modal reopen, normal close, and complete process/listener cleanup. The accepted G2 slices prove the non-production mutation policy, dependency probes, a healthy 90-actor backend, reversible typed baseline capture, real WinForms initialization, command listener coverage before mutation, operator-visible market-data feed start/stop whose UI state follows exact correlated terminal events, futures/futures-option add/change/remove with durable query agreement and child-before-parent cleanup, manual yield-curve add/change/remove, and an operator-selected production FMP treasury import through the parameter-only domain flow with durable/UI agreement and public-command baseline restoration. G2-020 through G2-038, G3, G4, and operator acceptance remain open, so Milestone A is not yet complete. Legacy scheduled-task UI remains deferred until that workflow is redesigned; it is not advertised as a supported G1 System Administration destination.

## 5. Milestone B - trade-broker and account foundation

### 5.1 Objective

Create one application-facing broker boundary used consistently by the broker emulator, an approved paper broker, and later real broker adapters. Establish authoritative broker account state, persistence, actors, and reconciliation before order automation is introduced.

### 5.2 Trade-broker contract

The broker boundary must cover:

- connection, authentication/session, readiness, and capability state;
- account discovery and permissions;
- contract/instrument identification and broker identifiers;
- cash, settled cash, buying power, net liquidation value, and margin;
- positions;
- open, working, completed, cancelled, and rejected orders;
- executions, partial fills, complete fills, commissions, and fees;
- order placement, modification, and cancellation;
- broker warnings, errors, and rejection reasons;
- streaming broker updates; and
- reconnect, resubscription, snapshot refresh, and reconciliation.

Broker adapters must not expose provider-specific DTOs directly to domain actors or UI ViewModels. Provider data is normalized at the broker boundary while retaining required provider identifiers and raw diagnostic context.

### 5.3 Trade-broker emulation engine

The emulator must implement the same application-facing contract as an external broker adapter. It should support deterministic scenarios for:

- accepted and rejected orders;
- market, limit, stop, and approved multi-leg option orders;
- partial and complete fills;
- latency, slippage, commissions, and fees;
- modification and cancellation races;
- trading-session and market-state restrictions;
- price movement during submission;
- disconnects, delayed responses, ambiguous outcomes, and reconnect;
- cash, buying-power, margin, position, and P&L effects; and
- repeatable scripted success and failure cases.

The emulator is infrastructure for development, automated testing, and controlled simulation. It must not introduce an emulator-only application workflow that bypasses the real broker contract.

### 5.4 Broker account query and storage

The application must query and persist the account information required for order and risk decisions:

- account identity, type, permissions, and restrictions;
- cash and settled cash;
- buying power and excess liquidity;
- net liquidation value;
- initial and maintenance margin;
- realized and unrealized profit/loss;
- positions and quantities;
- working and completed orders;
- executions, fills, commissions, and fees;
- warnings and broker restrictions;
- last successful synchronization; and
- reconciliation state and discrepancies.

Likely storage projections include broker-account snapshots, balance/margin history, position state/history, broker-order state, fill history, commission history, synchronization checkpoints, and reconciliation results. Broker-reported state and IFM internal trading state remain distinguishable even when correlated.

### 5.5 Broker account actor

The broker account actor owns application-side account synchronization and must:

- start and refresh account state;
- consume normalized broker account updates;
- maintain balances, positions, buying power, margin, and permissions;
- correlate internal orders with broker orders and executions;
- detect discrepancies and stale state;
- publish durable account and reconciliation facts;
- answer account and risk queries;
- coordinate reconnect and resynchronization; and
- provide current account state to the portfolio risk manager.

### 5.6 Cross-cutting foundation

Milestone B also establishes:

- canonical order, execution, account, and reconciliation identifiers;
- end-to-end correlation, causation, and idempotency rules;
- a formal order/execution state machine;
- trading-session and market-hours services;
- uncertain-submission recovery rules;
- broker versus internal state reconciliation; and
- broker/account metrics, diagnostics, and operator status.

### 5.7 Exit criteria

- the emulator and at least one approved broker adapter contract test use the same application-facing interface;
- account snapshots, positions, orders, executions, balances, and margin can be queried and stored;
- account actor recovery and reconciliation pass deterministic restart and discrepancy tests;
- broker disconnect, delayed response, duplicate update, and ambiguous outcome scenarios are observable and recoverable; and
- no trading order is automated yet merely because the foundation exists.

## 6. Milestone C - fully manual paper-trading execution

### 6.1 Objective

Allow an operator to execute a complete paper-order lifecycle manually through the shared broker foundation before any automated strategy is permitted to submit orders.

### 6.2 Required workflow

```text
Operator input
    -> order validation
    -> order composition
    -> portfolio-risk approval
    -> operator confirmation
    -> broker submission
    -> broker acknowledgement/rejection
    -> partial/complete fills
    -> internal order/trade/position update
    -> modify/cancel/reconcile/close
```

### 6.3 Required behavior

- explicit account and strategy context;
- supported instrument and multi-leg composition;
- quantities, order type, prices, duration, and transmit intent;
- validation before submission;
- account, buying-power, margin, and portfolio-risk approval;
- final operator confirmation showing the exact proposed order;
- idempotent submission with stable internal order identity;
- broker acknowledgement, rejection, partial fill, full fill, cancellation, and modification states;
- timeout and ambiguous-outcome reconciliation before retry;
- correlation between proposal, command, broker order, execution, trade, and position;
- visible errors, broker messages, and terminal outcome;
- operator pause, trading disable, and emergency stop; and
- durable audit history.

### 6.4 UI scope

The UI must support manual order entry, validation, risk presentation, confirmation, working-order monitoring, execution/fill display, modification, cancellation, account/position refresh, and reconciliation status. These are new paper-trading capabilities and are not part of Milestone A restoration.

### 6.5 Exit criteria

- deterministic manual scenarios pass against the emulator;
- approved manual scenarios pass against the paper broker when available;
- duplicate clicks and transport retries cannot create duplicate broker orders;
- partial fill, cancel/replace, rejection, disconnect, and ambiguous submission scenarios reconcile correctly;
- internal orders, broker orders, executions, account balances, and positions agree after recovery; and
- no automated strategy actor has authority to submit an order.

## 7. Milestone D - automated strategy workflow

### 7.1 Objective

Create an actor-owned, explainable pipeline that transforms approved market and portfolio state into a risk-approved executable order and submits it through the same execution foundation proven by Milestone C.

### 7.2 Pipeline

```text
Market and reference data
    -> Regime Discovery Actor
    -> Market Condition Actor
    -> Strategy Selection Actor
    -> Order Composition Actor
    -> Portfolio Risk Manager Actor
    -> Automated Execution Workflow
    -> Trade Broker
```

Each boundary emits a new message with the correct semantic role and transport. Intermediate decisions must be correlated and explainable. Realtime inputs do not enter normal actors directly; they use the documented Command handoff when actor behavior is required.

### 7.3 Regime discovery actor

- consumes approved historical and realtime analytical inputs;
- identifies and versions the broader market regime;
- records input freshness and confidence;
- avoids stale regime publication after supersession; and
- publishes revisioned regime state and diagnostic explanations.

### 7.4 Market condition actor

- evaluates current trend, volatility, liquidity, breadth, session, and other approved features;
- distinguishes current market condition from longer-horizon regime;
- records stale/missing inputs and confidence;
- publishes revisioned actionable condition state; and
- prevents incomplete state from silently authorizing strategy selection.

### 7.5 Strategy selection actor

- evaluates eligible strategies against regime, market condition, account state, existing positions, configuration, and strategy constraints;
- records why strategies were selected or rejected;
- prevents duplicate/conflicting strategy instances; and
- emits a versioned strategy proposal rather than a broker order.

### 7.6 Order composition actor

- converts an approved strategy proposal into concrete instruments, legs, ratios, quantities, prices, order type, and execution instructions;
- verifies market-data freshness and contract identity;
- calculates maximum loss and required capital inputs;
- records the strategy/configuration version used; and
- emits an explainable order proposal for risk evaluation.

### 7.7 Portfolio risk manager actor

The portfolio risk manager is an independent approval boundary and evaluates:

- current broker account and reconciliation freshness;
- cash, buying power, and margin effect;
- maximum loss;
- underlying and strategy concentration;
- portfolio Greeks and volatility exposure;
- correlated positions;
- open and pending orders;
- daily, strategy, account, and portfolio risk limits;
- duplicate or conflicting proposals;
- stale data or incomplete broker state;
- trading-session and permissions state; and
- pause, disable, and emergency-stop state.

Approval and rejection are explicit durable outcomes with reasons. Risk approval is scoped to a proposal version and may expire when account or market state changes.

### 7.8 Automated execution workflow

- accepts only a current risk-approved proposal;
- applies stable intent and broker-order identities;
- uses the Milestone C execution state machine;
- handles acknowledgement, rejection, partial fills, cancel/replace, timeouts, and ambiguous outcomes;
- reconciles after reconnect before retrying;
- publishes terminal execution facts;
- allows operator pause, intervention, and emergency stop; and
- never bypasses broker account or portfolio risk controls.

### 7.9 Exit criteria

- every automated order is traceable from market inputs through regime, condition, strategy, composition, risk, execution, and broker outcome;
- deterministic scenarios prove selection and rejection behavior;
- stale, missing, duplicated, or contradictory inputs fail safely;
- automation can be enabled per strategy/account and disabled globally;
- manual and automated orders share the same broker execution, reconciliation, storage, and audit foundation; and
- automation is initially qualified only against the emulator and approved paper environment.

## 8. Milestone E - automated trade monitoring and exit

### 8.1 Objective

Create an owned monitoring lifecycle for every open strategy instance and convert approved exit conditions into risk-controlled closing-order proposals.

### 8.2 Pipeline

```text
Position, execution, account, and market updates
    -> Trade Monitor Actor
    -> Forward-Loss Calculation Actor
    -> Trade Exit Condition Actor
    -> Closing Order Composition
    -> Portfolio Risk Manager
    -> Automated Execution Workflow
```

### 8.3 Trade monitor actor

- owns monitoring state for one strategy/trade instance;
- tracks broker and internal position agreement;
- maintains prices, Greeks, P&L, age, time-to-expiry, regime, condition, and data freshness;
- reacts to partial fills and position changes;
- publishes revisioned monitoring state;
- suspends automated decisions when required state is stale or unreconciled; and
- terminates only after the position is closed and reconciliation is complete.

### 8.4 Forward-loss calculation actor

- calculates the approved forward-loss measure;
- identifies the position, market inputs, model/configuration version, and time used;
- detects missing or stale inputs;
- prevents superseded calculations from overwriting newer results;
- publishes revisioned results and diagnostic components; and
- remains a calculation boundary rather than directly initiating broker behavior.

### 8.5 Trade exit condition actor

Exit conditions may include:

- profit target;
- maximum loss;
- forward-loss threshold;
- time-to-expiration;
- time-of-day or market-close rule;
- regime or market-condition change;
- volatility or liquidity deterioration;
- position or broker reconciliation failure;
- manual exit request; and
- emergency portfolio-risk exit.

The actor records which conditions were evaluated, the input revisions used, and why an exit was or was not selected. An exit decision creates a closing-order proposal; it never directly mutates a broker position.

### 8.6 Exit criteria

- monitor recovery reconstructs correct state after restart;
- forward-loss and exit evaluation are deterministic for fixed inputs;
- stale, missing, duplicated, and out-of-order updates cannot produce an unsafe exit order;
- partial fills and broker discrepancies suspend or redirect monitoring safely;
- manual intervention and emergency exits coexist with automated rules;
- closing orders pass through composition, risk, broker execution, reconciliation, and audit; and
- strategy monitoring terminates cleanly after confirmed closure.

## 9. Milestone F - paper-trading qualification

### 9.1 Objective

Qualify the complete system, not merely the UI or broker adapter, for controlled paper trading.

### 9.2 Qualification scope

- market-open initialization and account reconciliation;
- realtime market-data and analytics operation;
- regime and market-condition changes;
- strategy selection and rejection;
- order composition and risk decisions;
- manual order entry and confirmation;
- automated paper-order execution;
- acknowledgements, rejection, partial fills, complete fills, modification, and cancellation;
- position, account, margin, and P&L reconciliation;
- trade monitoring, forward loss, and exit decisions;
- controlled closing orders;
- market close and end-of-day workflows;
- process restart at every significant workflow state;
- NATS, storage, broker, and feed disconnect/reconnect;
- ambiguous submission and duplicate delivery;
- overload, backpressure, latency, memory, and recovery; and
- operator pause, disable, intervention, and emergency stop.

### 9.3 Evidence

- deterministic unit, actor, contract, and integration suites;
- broker emulator scenario suite;
- end-to-end system tests using production composition;
- fault-injection and recovery tests;
- account/order/position reconciliation reports;
- audit trace from decision inputs to terminal broker outcome;
- performance baseline with p95/p99 latency, peak event lag, CPU, memory, GC, queue depth, and recovery time;
- extended paper-trading soak over agreed market sessions; and
- operator-reviewed workflow and usability evidence.

### 9.4 Exit criteria

- Milestones A through E are complete;
- exact paper environment, accounts, instruments, strategies, risk limits, fill assumptions, soak duration, stop conditions, and rollback are approved;
- no unexplained order, execution, position, cash, margin, or reconciliation discrepancy remains;
- no duplicate broker order is produced by retry, reconnect, redelivery, repeated input, or process restart;
- no unrecovered consumer, actor, feed, broker, storage, or UI failure remains;
- memory, event lag, backlog, and UI responsiveness remain bounded during the agreed soak;
- all automated decisions are explainable and auditable;
- emergency controls and runbooks are exercised; and
- the operator explicitly approves controlled paper-trading use.

Milestone F does not authorize live trading. Live-trading activation requires a separate production readiness, deployment, risk, operational, and rollback gate.

## 10. Cross-cutting architectural requirements

All milestones must preserve:

- immutable durable event history;
- ActorType-selected Core NATS or JetStream transport;
- commands as the boundary for requesting actor behavior;
- durable events for workflow facts and external participants;
- idempotent at-least-once event handling;
- explicit correlation and causation;
- formal lifecycle ownership and graceful shutdown;
- bounded queues and visible overload/backpressure;
- lossless order, execution, account, risk, and audit paths;
- projection queries as the source of current large state;
- broker versus internal state separation and reconciliation;
- safe recovery after restart and disconnect;
- operator-visible errors and decision explanations; and
- no live or paper order authority from a capability that has not passed its milestone gate.

## 11. Presentation technology and deferred refactoring

The roadmap does not require a WPF migration before broker or paper-trading capability work. After Milestone A, presentation priorities may be selected independently:

- retain the accepted optimized WinForms client while broker and trading capabilities are built;
- incrementally adopt CommunityToolkit.Mvvm or R3 after a reviewed implementation plan;
- add the designed `IAsyncEnumerable` listener API after messaging contracts are approved for implementation; or
- begin WPF migration using the stable shared Models, ViewModels, event consumers, and lifecycle contracts.

These tools can improve maintainability and high-rate presentation processing, but none substitutes for Milestones B through F. The paper-trading readiness decision is based on complete trading-system behavior and evidence, not the desktop framework.

## 12. Required future design documents

Before implementing the corresponding milestone, create and review detailed designs for:

1. broker abstraction and provider adapter contract;
2. deterministic broker emulator;
3. broker account schema, projections, and reconciliation;
4. broker account actor;
5. canonical order and execution state machine;
6. manual order-execution workflow and UI;
7. regime discovery and market-condition contracts;
8. strategy selection and order composition;
9. portfolio risk manager;
10. automated execution and uncertain-outcome recovery;
11. trade monitor lifecycle;
12. forward-loss model and actor;
13. exit-condition policy and closing workflow;
14. paper-trading scenarios, risk limits, soak, evidence, and rollback; and
15. live-trading production gate, only after paper qualification.

## 13. Summary

Milestone A restores and proves the existing system with its optimized MVVM-based WinForms client. That achievement demonstrates that the modernized IFM platform again works as the previously used trading system, but with current architectural improvements.

Milestones B through E add the broker account, manual execution, automated strategy, portfolio risk, automated broker execution, and trade-monitoring capabilities that do not yet exist as a complete qualified workflow. Milestone F qualifies the combined system for controlled paper trading.

Keeping these milestones separate prevents UI restoration from being overstated and makes the true paper-trading dependencies explicit.
