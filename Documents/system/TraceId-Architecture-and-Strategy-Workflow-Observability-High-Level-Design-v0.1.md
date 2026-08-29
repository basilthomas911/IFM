# TraceId Architecture and Strategy Workflow Observability

## High-Level Design v0.1

**Status:** Proposed high-level design<br>
**Primary implementation scope:** Futures ITI signal reception through successful creation of a live trade position<br>
**Secondary scope:** Strategy Observation UI and asynchronous LLM-generated workflow summaries<br>
**Intended next step:** Derive a detailed implementation specification, contracts, schemas, and acceptance tests

---

## 1. Purpose

This document defines a high-level TraceId and observability architecture for the deterministic trade strategy workflow. Its first implementation follows every received `FuturesItiSignalGeneratedEvent` through the strategy pipeline and, when approved and filled, to the creation of a live trade position.

The architecture must make every strategy attempt observable, regardless of how far it progresses. An attempt may be rejected before starting, deliberately stop with `NoTrade`, fail inside an actor, be cancelled or time out, be rejected during risk or execution, remain unfilled, or successfully create a live position. None of these paths may disappear from the operational view.

The design also introduces an optional asynchronous LLM summary that explains what the completed deterministic workflow attempted and why it ended. The LLM is observational only and cannot change workflow state, trading parameters, risk decisions, orders, or positions.

---

## 2. Goals

1. Produce one observable strategy attempt for every received futures ITI signal.
2. Trace accepted workflows across asynchronous NATS commands and events.
3. Preserve business identity independently of OpenTelemetry implementation details.
4. Record exactly one terminal observation for every strategy attempt.
5. Display partial, stopped, rejected, failed, and successful attempts in the Strategy Observation UI.
6. Correlate strategy stages, messages, database operations, risk decisions, broker operations, fills, and position creation.
7. Provide an immediate deterministic summary for every terminal attempt.
8. Optionally generate a later natural-language LLM summary from immutable structured facts.
9. Ensure observability and LLM failures never block or alter deterministic trading.
10. Establish conventions that can later extend into position monitoring, exit, reconciliation, security auditing, and client-grade operations.

---

## 3. Non-Goals for This Version

This design does not yet specify:

- Position-monitoring and exit-workflow trace boundaries beyond the hand-off from position creation.
- A single trace spanning the entire lifetime of a position.
- Exact C# types, serialization layouts, database DDL, or NATS subject names.
- OTEL collector deployment topology or final GreptimeDB sizing.
- Complete Keycloak and NATS zero-trust security design.
- Automatic retry of strategy pipeline actors.
- LLM participation in workflow continuation or trade decisions.
- Replacement of authoritative trading records with traces or generated summaries.

---

## 4. Core Design Principles

### 4.1 Business identity and telemetry identity are separate

`StrategyWorkflowId` is a UUIDv7 business identifier. It is not the W3C/OpenTelemetry TraceId. Business records must remain searchable and correct even if tracing is disabled, sampled, unavailable, or later migrated to another telemetry backend.

### 4.2 Every signal creates an attempt

Receipt of a validly deserialized `FuturesItiSignalGeneratedEvent` creates a `StrategyAttemptId` and a new W3C trace context. Rejected starts remain first-class observed attempts even though they never receive a `StrategyWorkflowId`.

### 4.3 Exactly one terminal outcome

Every attempt must produce exactly one terminal observation. Terminal state is enforced by workflow state, not inferred from the absence of additional messages.

### 4.4 No automatic actor retry

The realtime strategy pipeline does not automatically retry actor processing. A major processing problem returns `Failed` and stops the workflow. Any future operator redispatch is a new explicitly recorded operation and must not be hidden within the original attempt.

### 4.5 Completed does not mean continue

An actor `Completed` event means the actor successfully processed its requested stage. The workflow coordinator evaluates the returned result and applies deterministic continuation rules. A completed stage may therefore lead to the next stage, `NoTrade`, or another deliberate stop.

### 4.6 Observability is not authoritative state

PostgreSQL/event records, workflow state, broker reconciliation, and the live-position record remain authoritative. GreptimeDB traces, logs, metrics, UI projections, and LLM summaries explain execution but cannot replace those records.

### 4.7 Fail-open telemetry, fail-safe trading

Telemetry export and LLM summarization must not block the trading pipeline. Missing mandatory business state, risk data, or broker state must fail safely; missing telemetry export must raise an operational warning without changing an otherwise valid deterministic decision.

---

## 5. Scope of the Initial Strategy Workflow

The initial trace covers:

1. `FuturesItiSignalGeneratedEvent` reception.
2. Strategy start validation.
3. Attempt acceptance or rejection.
4. Regime Discovery.
5. Market Condition evaluation.
6. Trade Selection.
7. Order Composition.
8. Risk Manager evaluation.
9. Order Execution submission and broker acknowledgement.
10. Required fills.
11. Live position creation.
12. Terminal observation projection.
13. Deterministic summary generation.
14. Optional asynchronous LLM summary generation.

The exact strategy actor names and intermediate stages may evolve. The identity, propagation, terminal-outcome, and summary rules remain stable.

---

## 6. Identity Model

| Identifier | Format | Creation point | Lifetime and purpose |
|---|---|---|---|
| `TraceId` | W3C 16-byte trace identifier | ITI signal reception | One technical execution of a strategy attempt |
| `SpanId` | W3C 8-byte span identifier | Each instrumented operation | One actor, message, database, rule, risk, broker, or position operation |
| `StrategyAttemptId` | UUIDv7 | ITI signal reception | Every attempt, including rejected attempts |
| `StrategyWorkflowId` | UUIDv7 | Start accepted | Stable business identity of an accepted workflow |
| `TriggerEventId` | Message identifier | ITI signal creation | Original signal that triggered the attempt |
| `MessageId` | UUIDv7 | Every command/event creation | Unique identity of one message |
| `CausationId` | MessageId | Child message creation | Directly preceding message that caused the child |
| `CorrelationId` | UUIDv7 | Attempt/workflow creation | `StrategyWorkflowId` for accepted workflows; otherwise `StrategyAttemptId` |
| `EntityId` | Domain identifier | Existing domain ownership | Enforces one in-flight workflow per strategy entity |
| `TradeCandidateId` | UUIDv7 | Candidate creation | Identity of a proposed deterministic trade |
| `OrderId` | UUIDv7 | Internal order creation | Stable internal order identity |
| `BrokerOrderId` | Broker-defined | Broker acknowledgement | IBKR order identity |
| `PositionId` | UUIDv7/domain identifier | Position creation | Stable live-position identity and later lifecycle join key |

### 6.1 Identity invariants

- Every attempt has exactly one `StrategyAttemptId`.
- Only accepted attempts have a `StrategyWorkflowId`.
- An accepted attempt retains the same `StrategyWorkflowId` through every strategy actor.
- Every command/event has a new `MessageId`.
- `CausationId` always identifies the direct parent message, not the root signal.
- `TriggerEventId` always identifies the original ITI signal.
- The W3C TraceId never replaces a business identifier in commands, events, persistence, or UI queries.
- A missing or malformed incoming trace header never causes reuse of an invalid TraceId.

---

## 7. Trace Lifecycle

### 7.1 Root trace creation

The component receiving `FuturesItiSignalGeneratedEvent` starts the strategy-attempt trace. The receive span records the signal type, entity, horizon, source timestamp, receive timestamp, and initial validation result.

### 7.2 Accepted start

When start validation succeeds and no workflow is in flight for the entity:

1. Generate `StrategyWorkflowId` as UUIDv7.
2. Set `CorrelationId = StrategyWorkflowId`.
3. Emit the accepted-start event.
4. Dispatch the first strategy-stage command with W3C trace headers.
5. Preserve `StrategyAttemptId`, `StrategyWorkflowId`, `TriggerEventId`, and business correlation metadata.

### 7.3 Rejected start

When the start is rejected:

1. Retain the attempt TraceId.
2. Do not generate a `StrategyWorkflowId`.
3. Set `CorrelationId = StrategyAttemptId`.
4. Record structured rejection reason and any conflicting in-flight workflow identity.
5. Produce a terminal `Rejected` observation and deterministic summary.

### 7.4 Trace completion

The logical strategy trace ends after one terminal outcome is recorded. For a successful trade this occurs only after the required broker fills have been reconciled sufficiently to create the live position record and emit the position-live event.

The trace need not rely on one continuously open in-memory root `Activity`. Trace context can be persisted and propagated across messages, allowing child spans to be created after asynchronous waits or process boundaries.

---

## 8. NATS Context Propagation

### 8.1 Transport headers

NATS messages propagate standard W3C headers:

- `traceparent`
- `tracestate` when present
- `baggage` only for a small approved set of non-sensitive values

Business identifiers remain in the versioned message envelope or typed message contract rather than depending exclusively on transport headers.

### 8.2 Producer behavior

For every published command or event, the producer:

1. Creates a producer/send span.
2. Creates a new `MessageId`.
3. Sets `CausationId` to the message currently being processed.
4. Injects W3C context into NATS headers.
5. Records destination subject and message type using bounded-cardinality attributes.
6. Never places credentials, tokens, account secrets, full order payloads, or unrestricted text in trace headers.

### 8.3 Consumer behavior

For every received command or event, the consumer:

1. Extracts and validates the W3C context.
2. Creates a consumer/process span.
3. Records message and business identifiers as span attributes.
4. Executes the actor stage within the consumer span or a child processing span.
5. Emits exactly one stage `Completed` or `Failed` terminal event for that invocation.

### 8.4 Missing or malformed context

If W3C headers are absent or malformed:

- Start a new trace context.
- Preserve all valid business identifiers.
- record `trace.propagation.status = missing|invalid`.
- emit an operational metric and warning.
- continue only when the business message itself is otherwise valid.

### 8.5 Duplicate delivery

JetStream may deliver messages at least once. Duplicate consumption must not create a second business transition or second terminal observation. The duplicate may create a diagnostic span marked as duplicate, while idempotency is enforced using `MessageId`, actor invocation identity, and workflow state.

---

## 9. Strategy Trace Model

### 9.1 Representative successful trace

```text
StrategyAttempt
├── FuturesItiSignal.Receive
├── StrategyWorkflow.Start
├── RegimeDiscovery.Process
│   ├── TrendRegime.Process
│   ├── VolatilityRegime.Process
│   └── FusionRegime.Process
├── StrategyWorkflow.ApplyRegimeRules
├── MarketCondition.Process
├── StrategyWorkflow.ApplyMarketRules
├── TradeSelection.Process
├── StrategyWorkflow.ApplySelectionRules
├── OrderComposition.Process
├── StrategyWorkflow.ApplyCompositionRules
├── RiskManager.Process
├── StrategyWorkflow.ApplyRiskRules
├── OrderExecution.Submit
├── Broker.OrderAcknowledged
├── Broker.OrderFilled
├── Position.Create
└── StrategyAttempt.PositionLive
```

### 9.2 Representative no-trade trace

```text
StrategyAttempt
├── FuturesItiSignal.Receive
├── StrategyWorkflow.Start
├── RegimeDiscovery.Process
├── StrategyWorkflow.ApplyRegimeRules
└── StrategyAttempt.NoTrade
```

### 9.3 Representative failed trace

```text
StrategyAttempt
├── FuturesItiSignal.Receive
├── StrategyWorkflow.Start
├── RegimeDiscovery.Process [Error]
└── StrategyAttempt.Failed
```

### 9.4 Actor completion versus continuation

Each actor processing span records:

- Processing terminal status: `Completed` or `Failed`.
- Structured result reference.
- Workflow continuation decision: `Continue`, `NoTrade`, `Stop`, or not evaluated.
- Reason code.
- Configuration/parameter snapshot version.
- Processing and queue durations.

The workflow coordinator, not the processing actor, owns the continuation-decision span.

---

## 10. Terminal Outcome Model

| Outcome | Classification | Meaning |
|---|---|---|
| `Rejected` | Controlled terminal | Start validation or concurrency policy rejected the attempt |
| `NoTrade` | Successful terminal | All required processing succeeded; deterministic rules selected no trade |
| `Stopped` | Controlled terminal | A defined business rule deliberately stopped processing |
| `Failed` | Technical terminal | An actor or required dependency could not process the workflow |
| `Cancelled` | Operator terminal | An authorized operator cancelled the workflow |
| `TimedOut` | Technical/control terminal | Optional actor or workflow timeout expired |
| `RiskRejected` | Controlled terminal | Risk Manager denied the candidate |
| `OrderRejected` | Execution terminal | Broker or execution validation rejected the order |
| `OrderUnfilled` | Execution terminal | Order ended without the required fill |
| `PositionLive` | Successful terminal | Required fills completed and a live position was created |

The detailed specification may normalize these into `OutcomeCategory`, `OutcomeCode`, `LastCompletedStage`, and `ReasonCode` rather than using one large enumeration. The UI must distinguish expected business outcomes from technical failures.

---

## 11. Span Naming and Required Attributes

### 11.1 Naming convention

Use stable low-cardinality span names based on operation, never IDs or values:

```text
strategy.signal.receive
strategy.workflow.start
strategy.stage.process
strategy.rules.evaluate
messaging.nats.publish
messaging.nats.consume
db.postgresql.query
db.scylla.write
risk.evaluate
broker.order.submit
broker.order.callback
position.create
strategy.attempt.terminal
strategy.summary.generate
```

Actor and stage names are stored as attributes.

### 11.2 Core attributes

```text
strategy.attempt.id
strategy.workflow.id
strategy.entity.id
strategy.horizon
strategy.stage
strategy.outcome.category
strategy.outcome.code
strategy.reason.code
strategy.continuation.decision
strategy.parameter_set.id
strategy.parameter_set.version
iti.signal.type
iti.signal.event_id
message.id
message.correlation_id
message.causation_id
message.type
messaging.system
messaging.destination.name
trade.candidate.id
order.id
broker.order.id
trade.position.id
enduser.id
auth.client.id
auth.role
```

Identifiers with high cardinality must be stored where necessary for trace lookup but must not become unbounded metric labels.

### 11.3 Error recording

Technical exceptions are recorded using standard OTEL exception conventions. User-facing and business-facing explanations use bounded reason codes. Stack traces and sensitive payloads are not copied into metric labels or ordinary business summaries.

---

## 12. Broker and Order Execution Tracing

Broker callbacks may arrive asynchronously without an active in-memory parent context. The order-execution component must persist the relevant trace context with `OrderId` when submitting the order.

On a callback:

1. Resolve `OrderId` and saved context using the broker order identity.
2. Continue the existing trace when it is semantically part of the same open-position attempt and valid context is available.
3. Otherwise create a new callback trace and add an OTEL span link to the order-submission context.
4. Always retain business joins through `StrategyWorkflowId`, `OrderId`, `BrokerOrderId`, and eventual `PositionId`.

The implementation specification must define maximum workflow duration, context persistence, late fills, partial fills, cancel/replace behavior, and callbacks received after a workflow has already reached a terminal state.

---

## 13. Observation Data Architecture

### 13.1 Separate operational projection from raw traces

The Strategy Observation UI should not reconstruct workflow truth by querying raw spans. A dedicated observation projection consumes workflow events and maintains one stable row per `StrategyAttemptId`.

```text
Authoritative workflow events ──> Strategy Observation Projection ──> UI
             │
             └──────────────────> OTEL Collector ──> GreptimeDB
```

The projection supplies predictable business queries. GreptimeDB supplies trace, metric, and log drill-down.

### 13.2 Attempt projection

The high-level attempt projection contains:

- Attempt, workflow, trace, trigger, correlation, and entity identifiers.
- Signal type, direction, horizon, and timestamps.
- Accepted/rejected state.
- Current and last-completed stage.
- Per-stage start, completion, status, result reference, duration, and reason.
- Continuation decisions.
- Candidate, order, broker order, and position identifiers when available.
- Terminal outcome and terminal timestamp.
- Deterministic summary and reason codes.
- LLM summary state and generated content reference.
- Trace availability and propagation-warning indicators.

### 13.3 Consistency

The observation projection is eventually consistent and non-authoritative. The UI should show projection freshness and may link to authoritative workflow details when needed. Projection failure cannot change workflow execution.

---

## 14. Strategy Observation UI

### 14.1 Main grid

Every attempt appears as one row with:

- Received/start time.
- Entity and strategy horizon.
- ITI signal type/direction.
- Accepted or rejected.
- Current or last stage.
- Terminal outcome.
- Total elapsed duration.
- Deterministic one-line summary.
- LLM-summary availability.
- Warning/error indicator.

### 14.2 Detail view

Selecting an attempt displays:

1. Original ITI trigger.
2. Workflow parameter snapshot.
3. Ordered stage timeline.
4. Actor results and continuation decisions.
5. Reason codes and warnings.
6. Candidate and proposed order when reached.
7. Risk decision.
8. Broker acknowledgement and fills.
9. Live-position identity when created.
10. Deterministic summary.
11. LLM summary and provenance.
12. Links to correlated logs, metrics, and full trace.

### 14.3 Live updates

The UI receives asynchronous stage and terminal updates through NATS. It must tolerate duplicate and out-of-order projection updates by applying workflow sequence/version rules rather than UI arrival order.

### 14.4 Visual status

- Green: completed successful outcome, including `PositionLive`.
- Neutral/blue: expected `NoTrade` or controlled stop.
- Yellow: rejected, unfilled, degraded telemetry, or operational warning.
- Red: failed, timed out, unauthorized, or inconsistent terminal state.

Exact colors remain a UI design decision; meaning must not depend on color alone.

---

## 15. Deterministic Workflow Summary

Every terminal attempt immediately generates a deterministic summary from structured stage results and reason codes. This summary is mandatory and available even when the LLM service is unavailable.

Example:

```text
Outcome: NoTrade
Last stage: MarketCondition
Reason: VOLATILITY_EXTREME
Summary: Strategy processing completed successfully. Entry stopped because
the volatility regime was Extreme and new positions are prohibited.
```

The deterministic summary must:

- Be derived solely from authoritative structured results.
- Identify terminal outcome, last stage, primary reason, and whether a position opened.
- Avoid speculative explanations.
- Remain versioned so later template changes do not silently rewrite history.

---

## 16. LLM Strategy Workflow Update Summary

### 16.1 Purpose

The LLM summary provides a concise natural-language explanation of what the strategy attempted, what it observed, how far it progressed, why it stopped or continued, and whether it created a live position.

### 16.2 Architectural boundary

The LLM:

- Runs only after the attempt reaches a terminal outcome.
- Is invoked asynchronously.
- Cannot delay or block strategy execution.
- Cannot modify workflow state or records.
- Cannot create or alter a trade candidate, order, risk decision, or position.
- Cannot replace deterministic reason codes or the mandatory deterministic summary.
- Is optional during initial implementation and may be disabled without affecting trading.

### 16.3 Summary flow

```text
Strategy terminal event
    ├──> Deterministic summary ──> Observation projection/UI
    └──> LLM summary requested
             └──> Structured snapshot loaded
                      └──> LLM generates schema-bound response
                               └──> Validate and persist
                                        └──> UI summary updated
```

### 16.4 Input snapshot

The summarizer receives an immutable, size-bounded structured snapshot containing:

- Attempt and workflow identities.
- ITI signal facts.
- Strategy horizon and entity.
- Parameter/configuration snapshot reference and selected safe values.
- Ordered stage names and statuses.
- Regime and market-condition classifications and scores.
- Continuation decisions and reason codes.
- Candidate structure when produced.
- Risk result and reasons.
- Execution status, acknowledgement, and fill summary.
- Final outcome and last reached stage.
- Stage durations and operational warnings.
- Deterministic summary.

Raw unrestricted logs, secrets, authentication tokens, account details, and arbitrary message payloads must not be supplied.

### 16.5 Output contract

The LLM output should be schema constrained:

```json
{
  "attemptSummary": "The upward ES direction-change signal started a monthly strategy evaluation.",
  "stageReached": "RiskManager",
  "finalOutcome": "NoTrade",
  "primaryReason": "Portfolio delta exposure exceeded the configured entry limit.",
  "supportingFactors": [
    "Trend regime was moderately bullish",
    "Volatility regime was high",
    "The candidate passed composition validation"
  ],
  "operationalWarnings": [],
  "positionOpened": false
}
```

The validator rejects outputs with invalid enumerations, unexpected fields, excessive length, missing provenance, or claims that contradict the structured source snapshot.

### 16.6 Provenance

Persist alongside the summary:

- `StrategyAttemptId` and `StrategyWorkflowId` when present.
- Source observation version/hash.
- Model provider, model name, and model version.
- Prompt-template version.
- Output-schema version.
- Generation timestamp and latency.
- Summary status: `Pending`, `Completed`, `Failed`, `Unavailable`, or `RejectedByValidation`.
- Validation result and failure reason.

### 16.7 Failure handling

There is no retry loop in the realtime strategy workflow. LLM summarization is a separate observational process. A bounded retry policy may later be considered for transient summary-service failures, but it must be independently configured, finite, observable, and incapable of redispatching the trading workflow.

Until such a policy is specified, a failed generation records `SummaryFailed` and leaves the deterministic summary in place.

---

## 17. Security and Privacy

Keycloak identity should be attached to manual or operator-triggered operations using safe identifiers and roles, not access tokens. Service-to-service spans identify the authenticated client/service principal where useful.

The design prohibits recording:

- Keycloak tokens or credentials.
- Broker credentials.
- Database connection secrets.
- Full account numbers.
- Personally identifying client data.
- Unbounded order or message payloads.
- Arbitrary exception data without sanitization.

NATS authorization and Keycloak authorization remain independently enforced. A UI permission does not grant NATS subject permission.

LLM inputs must be built from an allowlisted summary DTO, preventing accidental transmission of secrets and limiting prompt-injection surfaces.

---

## 18. Metrics

Metrics must use bounded labels. Recommended metrics include:

- Strategy attempts received.
- Starts accepted and rejected.
- Attempts by terminal outcome.
- Attempts by last stage.
- Stage processing duration histograms.
- Queue/wait duration histograms.
- End-to-end attempt duration.
- Actor completed/failed counts.
- Missing/invalid trace-context counts.
- Duplicate-message counts.
- Terminal-observation invariant violations.
- Broker acknowledgement and fill latency.
- Observation projection lag.
- LLM summary requested/completed/failed counts.
- LLM generation latency and validation rejection counts.

`TraceId`, workflow IDs, order IDs, and position IDs must never be metric labels.

---

## 19. Sampling and Retention

The first small-production implementation should favor complete strategy-attempt tracing because expected attempt volume is manageable and every decision is operationally valuable.

Minimum retention policy principles:

- Retain all failed, timed-out, cancelled, unauthorized, risk-rejected, broker-rejected, and inconsistent attempts.
- Retain all attempts that submit a real or paper order.
- Retain all attempts that create a position.
- Initially retain all `NoTrade` and rejected attempts; revisit sampling only after measuring volume.
- Metrics remain unsampled.
- Business observation records follow business/audit retention, independently of trace retention.
- LLM summaries must not outlive the source business observation unless explicitly required.

Tail-based sampling may later be introduced at the collector, but sampling must never control whether the deterministic observation record is created.

---

## 20. Performance and Reliability

### 20.1 Hot-path constraints

- Use OTEL `ActivitySource` and standard propagation with minimal allocations.
- Avoid serializing large payloads into span attributes.
- Export asynchronously through an OTEL Collector.
- Never synchronously query GreptimeDB from a strategy actor.
- Never invoke the LLM from the strategy execution thread.
- Use bounded telemetry queues and explicit drop metrics.

### 20.2 Failure isolation

- Collector unavailable: buffer within configured bounds, warn, and continue deterministic operation.
- GreptimeDB unavailable: collector buffers/drops according to policy; trading continues.
- Observation projection unavailable: authoritative workflow continues; lag and recovery are observable.
- LLM unavailable: deterministic summary remains final visible explanation.
- Malformed trace context: create a new trace and warn.
- Missing mandatory business identity: fail the affected workflow safely.

---

## 21. Data Ownership

| Information | Authoritative owner |
|---|---|
| Workflow state and terminal business outcome | Strategy workflow/event persistence |
| Candidate and risk decisions | Relevant deterministic domain records |
| Order and fill state | Internal order records reconciled with IBKR |
| Live position | Position domain; broker remains external book of truth |
| Attempt observation projection | Derived operational view |
| Trace, span, log, and metric data | GreptimeDB observability store |
| Deterministic summary | Versioned derived business observation |
| LLM summary | Versioned non-authoritative derived observation |

---

## 22. Suggested Component Responsibilities

### ITI signal receiver

- Create `StrategyAttemptId` and root trace context.
- Record signal reception.
- Dispatch strategy-start command.

### Strategy workflow coordinator

- Enforce one in-flight workflow per entity.
- Accept or reject attempts.
- Create `StrategyWorkflowId` on acceptance.
- Apply continuation rules after completed stages.
- Enforce one stage terminal event per invocation.
- Emit exactly one attempt terminal outcome.

### Strategy actors

- Extract context and create processing spans.
- Record bounded structured facts and result references.
- Emit exactly one `Completed` or `Failed` result per invocation.
- Never determine global terminal-state ownership independently of the workflow coordinator, except for defined failure notification behavior.

### Order Execution

- Trace validation, submission, acknowledgement, fills, rejection, cancel/replace, and errors.
- Persist context needed to correlate asynchronous broker callbacks.
- Emit structured execution results.

### Position domain

- Create the live position only after required execution conditions are satisfied.
- Return `PositionId` to the strategy workflow terminal observation.
- Establish the future hand-off to position-monitoring observability.

### Observation projector

- Maintain one ordered attempt view per `StrategyAttemptId`.
- Enforce idempotent projection updates.
- Produce deterministic summaries.
- Request optional LLM summaries after terminal state.

### LLM summary service

- Load only approved immutable summary snapshots.
- Generate schema-bound explanatory text.
- Validate output against source facts.
- Persist provenance and status.
- Never publish commands to trading subjects.

---

## 23. High-Level Event Sequence

```text
FuturesItiSignalGeneratedEvent received
  -> StrategyAttemptStarted
  -> StartStrategyWorkflowCommand
     -> StartAcceptedEvent
        -> StartRegimeDiscoveryCommand
        <- RegimeDiscoveryCompleted|Failed
        -> continuation decision
        -> StartMarketConditionCommand
        <- MarketConditionCompleted|Failed
        -> continuation decision
        -> StartTradeSelectionCommand
        <- TradeSelectionCompleted|Failed
        -> continuation decision
        -> StartOrderCompositionCommand
        <- OrderCompositionCompleted|Failed
        -> continuation decision
        -> StartRiskManagerCommand
        <- RiskManagerCompleted|Failed
        -> continuation decision
        -> SubmitOrderCommand
        <- OrderAcknowledged|Rejected
        <- OrderFilled|OrderUnfilled
        -> CreatePositionCommand
        <- PositionCreated
        -> StrategyAttemptTerminal(PositionLive)
     OR -> StrategyAttemptTerminal(NoTrade|Stopped|Failed|...)
     -> DeterministicStrategySummaryCreated
     -> LlmStrategySummaryRequested (optional)
     <- LlmStrategySummaryCompleted|Failed
```

The exact commands and events will be finalized in the implementation specification.

---

## 24. Required Invariants

1. Every received ITI signal has one `StrategyAttemptId`.
2. Every accepted start has exactly one `StrategyWorkflowId`.
3. At most one accepted workflow is in flight per `EntityId`.
4. Every actor invocation emits exactly one `Completed` or `Failed` terminal result.
5. Every strategy attempt emits exactly one terminal observation.
6. A `PositionLive` terminal outcome requires a persisted `PositionId`.
7. A rejected attempt cannot have a newly created `StrategyWorkflowId`.
8. LLM summary status cannot alter attempt terminal outcome.
9. Absence of OTEL export cannot alter the deterministic result.
10. Every child message has a unique `MessageId` and correct direct `CausationId`.
11. Duplicate message delivery cannot create duplicate business transitions.
12. `StrategyWorkflowId` is never used as a substitute W3C TraceId.

---

## 25. Implementation Phases

### Phase 1 — Identity and propagation

- Standardize message envelope identifiers.
- Implement W3C NATS inject/extract helpers.
- Establish `ActivitySource`, naming, and attribute conventions.
- Implement malformed/missing-context behavior.

### Phase 2 — Strategy workflow tracing

- Instrument signal reception, coordinator, actors, continuation decisions, and terminal outcomes.
- Enforce terminal invariants and idempotency.
- Trace order execution and position creation.

### Phase 3 — Observation projection and UI

- Build attempt projection.
- Populate the Strategy Observation grid and stage timeline.
- Add trace/log/metric drill-down.
- Generate deterministic summaries.

### Phase 4 — LLM summaries

- Define immutable summary snapshot and output schema.
- Implement asynchronous request/completion events.
- Add validation, provenance, security allowlist, and UI presentation.

### Phase 5 — Production hardening

- Define collector buffering and failure behavior.
- Establish retention and tail-sampling policy.
- Add alerting, trace completeness metrics, load testing, and recovery testing.

### Phase 6 — Later lifecycle expansion

- Position monitoring and exit traces.
- Cross-trace span links from opening strategy to position evaluations.
- Reconciliation and month-end processing.
- Security audit correlation using Keycloak identity.

---

## 26. Acceptance Criteria for the Later Specification

The detailed specification derived from this design must demonstrate:

1. A rejected concurrent start appears in the UI with a terminal trace and reason.
2. A successful Regime Discovery followed by `NoTrade` is shown as a successful controlled outcome, not an error.
3. A failed actor produces one failure event and one attempt terminal observation without retry.
4. A successful order can be traced through broker acknowledgement, fills, and position creation.
5. Duplicate JetStream delivery does not duplicate a stage transition or position.
6. Missing trace headers create a new trace and warning without losing valid business correlation.
7. GreptimeDB unavailability does not stop trading.
8. LLM unavailability does not delay the terminal workflow view.
9. An invalid or contradictory LLM response is rejected and the deterministic summary remains visible.
10. Searching by `StrategyAttemptId`, `StrategyWorkflowId`, `OrderId`, or `PositionId` locates the appropriate observation and diagnostic traces.
11. No secret, token, full account number, or unrestricted payload appears in telemetry or LLM input.
12. Every terminal attempt has a deterministic summary and provenance version.

---

## 27. Decisions Deferred to the Detailed Specification

- Exact command, event, DTO, and NATS header names.
- Whether `StrategyAttemptStarted` is persisted as a domain event or only as an observation event.
- Trace-context persistence mechanism for long broker waits and process restarts.
- Root-span representation across asynchronous workflow duration.
- Stage sequence numbering and projection optimistic concurrency.
- Exact timeout and manual cancellation semantics.
- Partial fill and multi-leg completion rules.
- Storage selection and retention duration for the observation projection and summary payloads.
- GreptimeDB schema, indexes, partitioning, and dashboards.
- LLM model/runtime selection and maximum summary latency.
- Bounded retry policy, if any, for the separate LLM summary service.
- Exact Keycloak claim-to-telemetry allowlist.
- Final transition from strategy trace to position-monitoring traces.

---

## 28. Final Architecture Statement

The central model is:

> **One received futures ITI signal creates one strategy attempt and one technical trace. An accepted attempt creates one UUIDv7 StrategyWorkflowId. The deterministic workflow progresses through zero or more strategy stages and produces exactly one terminal observation. Every terminal observation immediately receives a deterministic summary and may later receive a non-authoritative asynchronous LLM summary.**

This architecture gives the Strategy Observation UI a complete, searchable explanation of what the strategy attempted, how far it progressed, why it stopped or failed, and which live position it created. It also preserves the strict boundary between deterministic trading, business identity, telemetry, and AI-generated interpretation.
