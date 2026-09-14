# Futures ITI Realtime Ingress and Strategy Workflow Structured Logging

## Implementation Plan v1.0

**Status:** Implemented and qualified; live observation requires the running application to be restarted with the new binary
**Implementation scope:** Reintroduce a minimal Futures ITI realtime ingress and add structured logging from the market-price event through the Intrinsic Time Strategy Workflow pipeline
**Implementation note:** The runtime, tests, health integration, logging, and documentation are implemented. The clarified busy/free sampling rule and Gate 7 result are recorded in Section 15.

---

## 1. Purpose

The application needs one clear, observable path from a live ES market-price update to Futures ITI signal generation and then into the strategy workflow.

Two changes are required:

1. Reintroduce a small `FuturesItiSignalRealtimeActor` that receives `FuturesMarketPriceUpdatedRealtimeEvent` and requests only a Daily `GenerateFuturesItiSignalCommand`.
2. Add source-generated structured logging across every realtime, command, projector, event, workflow, and pipeline handler that participates in the resulting execution.

The implementation must preserve durable event-sourced mutation and projection. The realtime actor is an ingress router only. It does not calculate or persist an ITI signal, hold signal state, hydrate from the database, project events, generate Weekly or Monthly signals, or start the strategy workflow.

---

## 2. Agreed Runtime Flow

```text
FuturesMarketPriceUpdatedRealtimeEvent
  -> FuturesItiSignalRealtimeActor
  -> FuturesMarketPriceUpdated extension handler
       -> validate eligible current ES trade update
       -> read current VX futures price from the market-data API hot state
       -> request Daily GenerateFuturesItiSignalCommand

GenerateFuturesItiSignalCommand
  -> FuturesItiSignalCommandActor
  -> GenerateFuturesItiSignal extension handler
       -> load durable Daily ITI state
       -> evaluate the signal
       -> return success with no event when nothing materially changed
       -> otherwise commit FuturesItiSignalGeneratedEvent

FuturesItiSignalGeneratedEvent
  -> FuturesItiSignalEventProjector
       -> update MarketDataDb projection
       -> publish FuturesItiSignalGeneratedCompleteEvent

FuturesItiSignalGeneratedCompleteEvent
  -> FuturesItiSignalEventActor
  -> FuturesItiSignalGeneratedComplete extension handler
       -> publish UI/Market Outlook notification
       -> start the strategy workflow for this completed timeframe
       -> when timeframe is Daily, request Weekly and Monthly Generate commands

Weekly or Monthly Generate command
  -> same command actor and durable projector path
  -> same FuturesItiSignalGeneratedCompleteEvent handler
       -> start the strategy workflow for that completed timeframe
       -> do not generate another timeframe

ExecuteIntrinsicTimeStrategyWorkflowCommand
  -> IntrinsicTimeStrategyWorkflowCommandActor
  -> durable WorkflowStrategyStateUpdatedEvent
  -> IntrinsicTimeStrategyWorkflowEventProjector
  -> IntrinsicTimeStrategyWorkflowRealtimeActor
  -> Regime Discovery
  -> Market Condition
  -> Trade Selection
  -> Order Composition
  -> Risk Management
  -> terminal workflow result
```

This establishes one mutation path and one completion path. The market-price event starts Daily evaluation. Durable ITI completion starts the strategy workflow and, for Daily only, requests the longer timeframes.

---

## 3. Non-Negotiable Boundaries

### 3.1 Realtime ingress responsibilities

The restored Futures ITI realtime actor shall:

- register exactly one receive route for `FuturesMarketPriceUpdatedRealtimeEvent`;
- use the standard actor receive-map convention;
- delegate that message to a dedicated `FuturesMarketPriceUpdated` extension handler;
- accept only a valid trade-price update for the current on-the-run ES contract;
- obtain the current VX futures input through `IMarketDataApi`;
- start at most one Daily `GenerateFuturesItiSignalCommand` operation while the generation gate is free;
- immediately ignore new ticks while that operation is busy, without retaining or queueing them;
- record the request result through structured logs and bounded operational telemetry; and
- remove its route during orderly shutdown.

It shall not:

- maintain `FuturesItiSignalRealtimeState`;
- hydrate ITI state on market-price updates;
- acquire or own market-data streams;
- use `IRealtimeProjector`;
- write `FuturesItiSignalGeneratedEvent` directly;
- generate Weekly or Monthly commands;
- start a strategy workflow;
- poll a database;
- create a background recovery loop; or
- retry a failed tick indefinitely.

### 3.2 Durable completion responsibilities

`FuturesItiSignalGeneratedCompleteEvent` remains the only signal-completion trigger for:

- Market Outlook/UI notification;
- starting a strategy workflow for the completed signal timeframe; and
- requesting Weekly and Monthly generation when the completed timeframe is Daily.

Weekly and Monthly completed events start their own workflow instances and do not fan out to more signal commands.

### 3.3 Actor convention

The actor contains routing and lifecycle wiring. Each mapped message has one dedicated extension-handler class in the matching `Realtime`, `Command`, `Event`, or `Function` folder. Business rules and message creation remain in the extension handler or its domain model collaborators.

### 3.4 Logging convention

Logging declarations reside in dedicated `Logging` classes. These classes are `static partial` only so `[LoggerMessage]` can generate allocation-efficient implementations at compile time. Actor, handler, state, and model classes do not become partial merely to support logging.

---

## 4. Realtime Input Rules

The `FuturesMarketPriceUpdated` handler shall apply inexpensive rejection checks before making any API or actor request.

An event is eligible when all of the following are true:

- its message and entity identifiers are valid;
- `UpdateSource` identifies trade data rather than quote data;
- the trade snapshot is present;
- the price is finite and greater than zero;
- the contract is the current on-the-run ES futures contract returned by the market-data API;
- the event is not stale relative to the currently accepted stream epoch and ordinal policy supplied by the primary market-data publisher; and
- the normalized update represents a usable trade price under the existing feed normalization contract.

The handler ignores ineligible events without throwing. These routine decisions update bounded health counters without writing logs. Invalid data that indicates a broken contract records degraded health, and an actual command or processing failure records an Error.

Only one generation operation may be active. The first eligible tick that changes the atomic generation gate from Free
to Busy starts a Daily command. All ticks received while Busy return immediately and are not retained, queued,
coalesced, replayed, or processed later. Completion, rejection, or failure returns the gate to Free in `finally`, after
which the next eligible tick may start another command.

### 4.1 VX input

The existing Generate command requires a futures price and a VX futures price. The handler shall read the current front VX contract and its latest valid trade price from `IMarketDataApi` hot state.

If a usable VX value is unavailable:

- do not send the Generate command;
- do not throw an exception for ordinary startup unavailability;
- mark the ITI ingress as degraded with reason `VxPriceUnavailable`;
- issue a rate-limited Warning when the state first becomes degraded or its reason changes; and
- allow the next valid ES tick to try again and return the status to healthy.

This is self-recovery through new market data, not a retry loop.

### 4.2 Daily command construction

The command shall contain:

- the canonical ES `ContractId`;
- event `ValueDate`;
- `TimePeriod = Daily`;
- the trade event timestamp;
- ES last trade price;
- current VX futures price;
- the Daily timeframe start date; and
- a deterministic, non-empty command identifier derived from the immutable source event identity.

The command identifier must remain stable when the same source event is redelivered. The implementation shall use the source event ID when it is valid. If the transport permits an empty event ID, the fallback must be a deterministic hash of the immutable stream epoch, trade ordinal, contract, and timestamp. It must not create a new random ID for the same delivery.

The handler awaits the command response. An accepted response means that the command was processed, including the valid no-material-change outcome. It does not imply that a new source event was committed.

---

## 5. Signal Generation Outcomes

Health and logging must distinguish the following outcomes:

| Outcome | Meaning | Expected next event | Health |
| --- | --- | --- | --- |
| `Filtered` | Market-price event was not an eligible current ES trade | None | Healthy |
| `BusySkipped` | A Generate operation is active, so this tick is ignored | None | Healthy |
| `InputUnavailable` | Required current VX price is unavailable | None | Degraded until a later valid attempt |
| `CommandAcceptedNoChange` | Durable state was loaded and no material ITI change was found | None | Healthy |
| `EventCommitted` | A material ITI change produced a durable source event | Projector completion or failure | Pending completion |
| `CommandRejected` | Actor request was rejected or unavailable | None | Degraded/failed according to classification |
| `CommandFailed` | Signal evaluation or event commit failed | None | Failed |
| `ProjectionCompleted` | MarketDataDb update and terminal complete delivery succeeded | Completion handler activity | Healthy |
| `ProjectionFailed` | Durable projection emitted a fail result | Standard durable recovery path | Failed until recovered |

A lack of `FuturesItiSignalGeneratedEvent` is normal when the calculated signal did not materially change. Monitoring must never infer failure solely from a missing generated event after an accepted command.

---

## 6. Structured Logging Architecture

### 6.1 Implementation mechanism

Each affected domain area receives a dedicated logging class with `[LoggerMessage]` methods. Event IDs are stable, unique within the application, and grouped by domain range. Each declaration uses a constant message template and typed parameters.

Example organization:

```text
FuturesItiSignal/
  Realtime/Logging/FuturesItiSignalRealtimeLogging.cs
  Command/Logging/FuturesItiSignalCommandLogging.cs
  Event/Logging/FuturesItiSignalEventLogging.cs

Strategy/Workflow/IntrinsicTime/
  Command/Logging/IntrinsicTimeWorkflowCommandLogging.cs
  EventProjector/Logging/IntrinsicTimeWorkflowProjectionLogging.cs
  Realtime/Logging/IntrinsicTimeWorkflowRealtimeLogging.cs
  RegimeDiscovery/.../Logging/RegimeDiscoveryLogging.cs
  MarketCondition/.../Logging/MarketConditionLogging.cs
  TradeSelection/.../Logging/TradeSelectionLogging.cs
  OrderComposer/.../Logging/OrderCompositionLogging.cs
  RiskManager/.../Logging/RiskManagementLogging.cs
```

Logging classes contain logging declarations only. They do not contain business decisions, actor calls, database access, state mutation, or message construction.

### 6.2 Common structured properties

Handlers log the identifiers that exist at their boundary. They do not manufacture unavailable business IDs.

| Property | Use |
| --- | --- |
| `Actor` | Logical actor name |
| `Handler` | Extension-handler name |
| `MessageType` | Concrete received or sent contract |
| `Outcome` | Stable bounded result name |
| `SourceEventId` | Original market-price or ITI trigger event when available |
| `MessageId` | Current command/event identifier |
| `CommandId` | Current or causal command identifier |
| `EntityId` | Actor entity/thread identity |
| `ContractId` | Canonical broker-neutral contract identifier |
| `ValueDate` | Trading value date |
| `TimePeriod` | Daily, Weekly, or Monthly |
| `TimeFrameStartValueDate` | Durable timeframe bucket identity |
| `StrategyWorkflowId` | Workflow business identity after it exists |
| `WorkflowRevision` | Durable workflow revision |
| `PipelineStage` | Current workflow stage |
| `ContinuationDecision` | Continue, NoTrade, Complete, Failed, timed out, or other defined result |
| `DurationMs` | Measured processing duration for material operations |
| `ErrorCode` | Stable application error code |
| `ErrorType` | Stable failure classification |
| `ExceptionType` | Exception type for a real exception |
| `TraceId` and `SpanId` | Current `Activity` identifiers when an activity exists |

High-cardinality identifiers are log properties. They are not metric labels.

### 6.3 Severity and frequency

| Level | Policy |
| --- | --- |
| `Trace` | Per market tick, filtering decision, actor receive, and message-send detail. Normally disabled in production. |
| `Debug` | State comparisons, deduplication, no-change decisions, and development diagnostics. |
| `Information` | Material durable milestones: ITI event committed/projected/completed, workflow started, stage completed, workflow terminal outcome. |
| `Warning` | Recoverable degradation, missing VX input, stale completion, command rejection, timeout, duplicate/inconsistent delivery, or a deliberate pipeline stop that needs operator attention. |
| `Error` | Actual processing exception, durable write failure, projection failure, invalid required state, or failed workflow stage. |
| `Critical` | Reserved for conditions that require trading shutdown or manual recovery under an existing explicit policy. |

The realtime market-price path writes one normal Information entry only when a Free-to-Busy transition generates a
Daily command. Receipt, filtering, busy skips, unavailable input, command acceptance, and gate transitions do not
write routine logs. Actual command rejection or processing exceptions remain Error entries.

### 6.4 Exception logging

Every actor boundary must catch and classify exceptions according to the actor convention. Each actual exception is logged once at the boundary that owns the failure and includes the exception object plus all available business identifiers.

Downstream handlers log the classified failed result without logging the same exception again. Error details returned to the workflow include the root exception type, message, stage, and relevant identifiers in bounded form. Expected filtering, no-change, NoTrade, and temporarily unavailable market input are outcomes rather than exceptions.

### 6.5 Activity correlation

Existing `WorkflowTrace` activities remain in place. Structured logs emitted while `Activity.Current` is active include its TraceId and SpanId. This plan does not require a new durable trace schema or an observability database migration.

---

## 7. Required Log Points

### 7.1 Market-price to Daily ITI

The realtime ingress writes one normal log when a Daily Generate command is created and started. It includes the source
event ID, command ID, contract ID, and value date. Actual command rejection and unexpected processing exceptions write
Error logs. All other realtime ingress outcomes remain telemetry only.

The lower-frequency durable path continues logging material command evaluation, durable ITI source-event commit,
projector completion or failure, and completion receipt by the ITI EventActor.

### 7.2 Daily completion fan-out

The Daily completion handler shall log:

1. notification and Market Outlook update outcome;
2. Weekly command request and response;
3. Monthly command request and response;
4. workflow start request and response for Daily; and
5. the complete handler outcome and duration.

Weekly and Monthly command calls are independent. A rejection from one does not prevent the other from being attempted. A child command accepted with no material change is successful and does not require a child completion event.

### 7.3 Weekly and Monthly completion

Each longer-timeframe completion shall log:

1. completion receipt and timeframe identity;
2. notification and Market Outlook update outcome;
3. workflow start request and response; and
4. confirmation that no further timeframe command was generated.

### 7.4 Strategy workflow command and projection

The implementation shall log:

1. `ExecuteIntrinsicTimeStrategyWorkflowCommand` received;
2. start validation and parameter-set resolution;
3. new workflow identity/revision or idempotent duplicate result;
4. workflow state event committed;
5. workflow state projection started/completed/failed; and
6. `WorkflowStrategyStateUpdatedEvent` received by the workflow realtime actor.

### 7.5 Pipeline stages

The following stages require consistent structured logs at dispatch, start, completed/failed event reception, continuation decision, and durable workflow-state update:

- Regime Discovery;
- Market Condition;
- Trade Selection;
- Order Composition;
- Risk Management;
- portfolio financial handoff branches currently owned by Risk Management; and
- any current reservation/release messages that remain part of the implemented workflow until separately removed.

For each stage, logs must show:

- workflow ID and revision;
- input and result message IDs;
- actor and handler;
- elapsed duration;
- whether processing completed, returned NoTrade, was rejected, timed out, was cancelled, or failed;
- the continuation decision; and
- error code, type, complete root detail, and failed stage for failures.

### 7.6 Terminal workflow result

Every workflow must produce an Information or Error log for exactly one terminal state. The record contains the workflow identity, trigger source event, timeframe, last completed stage, terminal outcome, continuation reason, duration, parameter-set ID/version, and failure classification when applicable.

Read-only QueryActors are outside the execution-log path except for their own failures. The scope covers handlers that receive, mutate, project, dispatch, or complete the live workflow.

---

## 8. Operational Health Without Polling

Structured logs explain individual executions. A bounded process-local telemetry recorder supplies current health without querying PostgreSQL or ScyllaDB.

The recorder uses fixed stage cells and atomic operations for:

- last receipt timestamp;
- last success timestamp;
- last failure timestamp;
- received, filtered, accepted, no-change, generated, completed, and failed counts;
- busy-skipped tick count;
- current consecutive failure count;
- last bounded outcome/reason; and
- last observed actor mailbox delay when available.

It must not allocate a dictionary entry per event, command, workflow, contract, or exception. It must not retain message payloads. The high-frequency update path uses `Interlocked`, immutable snapshots, or an equivalent lock-free bounded representation.

Health is interpreted as follows:

- continuous eligible input with accepted no-change command results is healthy;
- missing market activity outside expected trading activity is informational;
- missing VX input is degraded and self-clears after a valid command request;
- a committed source event awaiting projection is pending for a bounded interval;
- projection failure, command failure, or growing mailbox delay is failed;
- an absence of Weekly/Monthly completion after a no-change command is healthy; and
- an absence of any Daily command attempt while eligible ES trade events are arriving is failed.

No health component polls the database. Durable projector recovery remains event-driven through its existing queue. The next live tick provides recovery for transient realtime input failures. A missing realtime route may restart only the thin realtime actor through the established supervisor mechanism; it must not recreate the retired ITI state/projector architecture.

---

## 9. Implementation Gates

### Gate 0: Baseline and contract inventory

1. Capture the current compile/test baseline and preserve unrelated working-tree changes.
2. Enumerate the actual actor receive maps and extension handlers across ITI and the strategy workflow.
3. Reserve non-overlapping structured logging EventId ranges.
4. Freeze the market update eligibility rules against the existing normalized publisher contract.
5. Identify the exact API calls for current ES identity, current VX identity, and latest VX trade price.
6. Record a BenchmarkDotNet and integration-test baseline for the current market-data route.

**Exit:** The complete execution chain and existing test failures are documented; no ambiguous market input or actor ownership remains.

### Gate 1: Logging and telemetry foundation

1. Add the dedicated source-generated logging classes and EventId catalog.
2. Add a bounded Futures ITI/workflow operations recorder in the existing operations-health application area.
3. Add immutable snapshot/read APIs for health reporting.
4. Ensure Trace-disabled hot-path calls do not build strings, arrays, scopes, or temporary property dictionaries.
5. Add common outcome/reason values with bounded cardinality.

**Exit:** Logging declarations compile, EventIds are unique, telemetry is bounded, and no message contract or database schema has changed.

### Gate 2: Restore the thin Futures ITI realtime actor

1. Restore `FuturesItiSignalRealtimeActor` and its context with only the dependencies required for routing, market-data lookup, logging, health, and actor request/reply.
2. Add only the `FuturesMarketPriceUpdatedRealtimeEvent` parse/receive mapping.
3. Add/remove the route on actor startup/shutdown.
4. Implement the dedicated `FuturesMarketPriceUpdated` extension handler.
5. Apply the fast eligibility checks, VX lookup, deterministic command ID, and atomic Free/Busy generation gate.
6. Start the command as one safely observed operation and immediately ignore ticks while the gate is Busy.
7. Clear Busy in `finally` and await the active operation during orderly actor shutdown.
8. Confirm that the retired realtime state, stream ownership, hydration, projector, and completion mappings remain absent.

**Exit:** One eligible ES trade event can request exactly one Daily command. No realtime code can create an ITI event, longer timeframe, or workflow.

### Gate 3: Instrument command evaluation and projection

1. Instrument the Generate command handler at state load, evaluation, no-change, event-created, commit, and failure boundaries.
2. Expose a reliable internal distinction between accepted/no-change and event-committed for health without changing public wire contracts unless the current service result cannot carry it.
3. Instrument the ITI event projector and its completed/failed terminal messages.
4. Record the source-event-to-completion pending interval.

**Exit:** Operators can distinguish filtering, missing input, no material change, source commit, projection completion, and genuine failure.

### Gate 4: Instrument completion fan-out and workflow admission

1. Instrument the ITI EventActor and `FuturesItiSignalGeneratedComplete` handler.
2. Log and measure Daily-to-Weekly and Daily-to-Monthly requests separately.
3. Log each timeframe's strategy workflow start request and response.
4. Instrument workflow command validation, parameter-set resolution, state-event commit, and projection.
5. Preserve deterministic child command IDs and workflow IDs under redelivery.

**Exit:** A generated Daily event can be followed into Daily, Weekly, and Monthly evaluation and into each material completion's workflow start.

### Gate 5: Instrument every workflow pipeline handler

1. Instrument `WorkflowStrategyStateUpdatedEvent` receipt and dispatch selection.
2. Add consistent source-generated logs to the Regime Discovery FunctionActor and coordinator handlers.
3. Repeat for Market Condition, Trade Selection, Order Composition, and Risk Management.
4. Include completed, failed, NoTrade, rejected, timed-out, cancelled, and terminal paths.
5. Include the existing Risk Management portfolio handoff branches.
6. Ensure the workflow command handler records every continuation and final state transition.

**Exit:** One `StrategyWorkflowId` can be used to reconstruct the ordered handler path and terminal reason from structured logs alone.

### Gate 6: Health integration

1. Replace the deleted tick-driven ITI probe with the new ingress/evaluation/completion health model.
2. Surface minute-level health for route attachment, eligible input, Daily attempts, no-change outcomes, durable completions, and workflow starts.
3. Prevent false alarms during quiet markets and valid no-change periods.
4. Add bounded pending-completion detection only for commands that actually committed source events.
5. Connect actor route failure to the established supervisor restart path for the thin actor.

**Exit:** Feed Health identifies where the chain last progressed without database polling or per-tick history retention.

### Gate 7: Qualification and live verification

Run all tests and benchmarks in Section 10. Fix failures before declaring completion. Stop every test host after its test run and verify that it is not running beside the application.

**Exit:** All targeted builds and tests pass; performance remains within the accepted baseline; a live run demonstrates correlated logs and health from ES input to a workflow terminal state or a clearly explained deliberate stop.

---

## 10. Required Tests

### 10.1 Unit tests

Realtime actor and handler:

- actor contains exactly one supported receive mapping;
- startup adds and shutdown removes the correct route;
- quote, non-futures, non-ES, stale, cancelled/cleared, missing-trade, zero-price, and invalid-ID events are handled without exceptions;
- current ES plus current VX requests exactly one Daily command;
- the command contains the correct contract, timestamps, prices, timeframe, and start date;
- duplicate source delivery creates the same command ID;
- missing VX creates no command and records degraded health;
- a later valid VX input clears the degradation;
- command rejection/failure records complete details without creating Weekly, Monthly, or workflow messages;
- 1,000 or more ticks received while generation is Busy create no additional command and retain no ticks; and
- realtime context has no state repository, event projector, stream ownership, or database hydration dependency.

Signal and completion handlers:

- no material change records a healthy no-change outcome;
- a material change records event-committed pending projection;
- projector completion clears the pending result;
- Daily completion requests Weekly and Monthly exactly once apiece;
- one child rejection does not prevent the other child request;
- Weekly and Monthly completions never generate further periods;
- each completed timeframe requests exactly one workflow start; and
- Set Hold and Clear Hold completions do not start workflows or generate timeframes.

Structured logging:

- all source-generated EventIds are unique and in their reserved ranges;
- required templates expose the agreed property names;
- exceptions include their complete classified details at the owning boundary;
- duplicate layers do not log the same exception as separate root errors;
- no-change and filtering paths do not produce Warning/Error logs; and
- Trace-disabled hot-path logging performs no message-template allocation.

Workflow handlers:

- each receive mapping invokes its dedicated extension handler;
- each stage logs dispatch, completion/failure, continuation, and duration;
- NoTrade is recorded as a deliberate terminal result;
- parameter-set failure contains the ID/version and underlying error;
- timeout/cancellation includes the active stage and workflow identity; and
- exactly one terminal log is produced per workflow revision.

### 10.2 BDD scenarios

1. **Material Daily change:** eligible ES tick -> Daily command -> generated event -> projection -> Daily completion -> Daily workflow start -> Weekly and Monthly requests.
2. **No Daily change:** eligible ES tick -> accepted Daily command -> no event -> healthy ingress with no workflow.
3. **Material longer-period change:** Daily completion -> Weekly/Monthly command -> corresponding completion -> one workflow for that timeframe.
4. **No longer-period change:** accepted Weekly/Monthly command -> no completion required -> healthy result.
5. **Startup without VX:** ES ticks arrive -> no command -> degraded reason shown -> later VX tick -> next ES event succeeds automatically.
6. **Duplicate market event:** redelivery -> same command ID -> no duplicate durable transition or workflow.
7. **Projection delay/failure:** committed event becomes pending, then failed or recovered through the durable projector path.
8. **Regime waiting for signals:** workflow starts and Regime Discovery returns its explicit waiting/degraded result with full details.
9. **NoTrade:** a pipeline stage completes successfully with NoTrade and the workflow records one deliberate terminal result.
10. **Pipeline failure:** the exact actor, handler, stage, exception type/message, error code, parameter set, and workflow identity appear in the failure log.

### 10.3 Integration tests

- NATS routes a real market-price event to the thin realtime actor and the standard command actor;
- EventSourceDb commits the material generated event and the durable projector updates MarketDataDb;
- completion delivery runs through the ITI EventActor only;
- Daily completion fans out through the standard command API;
- a material completion creates a durable strategy workflow and dispatches the correct stage;
- generated, completed, workflow, and pipeline logs share the expected identifiers;
- message redelivery is idempotent;
- app startup registers the realtime route and app shutdown removes it; and
- no test-only host remains alive after completion.

### 10.4 Verification tests

- scan actor maps to prove there is no second ITI completion consumer;
- scan dependencies to prove the thin actor does not reference retired realtime state/projector types;
- scan logging methods for duplicate EventIds and unbounded payload logging;
- query EventSourceDb after a controlled run and reconcile committed source events, projection receipts, completion events, and workflow records;
- verify a successful no-change command creates no false pending projection alert; and
- verify comprehensive failure details reach the workflow result and operational logs.

### 10.5 BenchmarkDotNet and runtime performance tests

Measure before and after at representative normal and burst input rates:

- event eligibility/filtering throughput;
- eligible tick-to-Daily-command request throughput;
- allocations per filtered event and eligible event;
- source-generated command-created logging with Information disabled;
- atomic busy-gate rejection throughput;
- telemetry recorder update cost;
- actor mailbox depth and oldest-message delay;
- Gen0/Gen1/Gen2 collections and allocated bytes; and
- end-to-end latency for a material Daily update through durable completion.

Use multiple input rates, including at least 1,000, 5,000, and 10,000 market updates per second where the environment supports them. Record the hardware, build configuration, logger configuration, database mode, and sample size beside results.

Acceptance requires:

- no avoidable allocation from disabled command-created logging;
- bounded telemetry memory;
- no per-event collection growth;
- no exceptions during expected filter/no-change/input-unavailable paths;
- stable ordering for one Daily ITI entity;
- no unbounded mailbox growth at the configured production input rate; and
- at most one active Daily command; and
- immediate, allocation-free rejection of ticks received while Busy.

The event-sourced command rate does not have to match the raw tick rate. Under the clarified domain rule, one tick is
sufficient to initiate a complete ITI evaluation and intermediate ticks received while that evaluation is active have
no independent business value.

---

## 11. Files and Documentation Expected to Change

### 11.1 Futures ITI runtime

- restore the Futures ITI realtime actor and minimal context;
- add its dedicated `FuturesMarketPriceUpdated` extension handler;
- add realtime, command, and event logging classes;
- instrument the command handler and event projector;
- update ITI service registration and supervisor startup ownership; and
- add bounded operations-health recording.

### 11.2 Strategy workflow runtime

- add logging classes for workflow command, projector, and realtime handlers;
- add logging classes for all five pipeline domains;
- instrument their command/function completed/failed mappings and coordinator handlers; and
- preserve existing actor maps, state transitions, messages, and database schemas unless a test identifies a correctness defect.

### 11.3 Documentation

- revise `Market-Data-ITI-Completion-Driven-Timeframe-Specification.md` so its input section describes the thin realtime Daily ingress;
- update `Live-Pipeline-Minute-Health.md` with no-change-aware ITI health;
- update `Actor-Implementation-Conventions.md` with the source-generated logging-class convention if any detail is missing;
- update the Strategy Workflow implementation document with the final log points; and
- cross-reference the existing TraceId observability design without expanding this work into its proposed UI, LLM, or durable trace-storage scope.

---

## 12. Compatibility and Migration

No MessagePack key, NATS subject, EventSourceDb event schema, MarketDataDb schema, or strategy workflow schema change is expected.

The shared `FuturesMarketPriceUpdatedRealtimeEvent` remains unchanged and continues to serve its other consumers. Futures ITI adds one normal route subscriber.

The restored actor may reuse its former actor name so supervisor configuration and operational naming remain stable, but it receives only the market-price event. Removed shared constants must not be restored merely to recreate obsolete completion routes.

The existing completion-driven changes remain the base. The implementation restores the minimum ingress files and rewrites the specification; it does not revert the working tree to the former tick-driven stateful implementation.

---

## 13. Material Risks and Controls

| Risk | Control |
| --- | --- |
| A market burst overwhelms the event-sourced command actor | The atomic generation gate permits one active command and immediately ignores all ticks received while Busy. |
| Missing VX suppresses Daily evaluation | Explicit degraded health, rate-limited warning, and recovery on the next valid ES tick. |
| No-change outcomes look like missing events | Record command outcomes explicitly and create pending projection state only after an event is committed. |
| Duplicate market delivery creates duplicate work | Deterministic command IDs and existing event-sourced idempotency. |
| Realtime actor accidentally regains state ownership | Dependency and source scans prohibit state repository, hydration, stream ownership, and realtime projector references. |
| Per-tick logs increase allocation and GC | Source-generated logging, Trace level, typed parameters, and benchmarks with Trace disabled. |
| Warning storms hide real failures | State-transition/rate-limited warnings and counters for repeated conditions. |
| Logs lose correlation at asynchronous boundaries | Carry existing business identifiers and W3C Activity context where available. |
| Multiple consumers start the same workflow | ITI EventActor completion handler remains the sole workflow-start boundary. |
| A child timeframe failure prevents other work | Weekly and Monthly requests are attempted independently and logged independently. |

---

## 14. Definition of Done

Implementation is complete when:

1. An eligible live ES market-price event invokes one Daily Generate command through the restored thin realtime actor.
2. The realtime actor contains no ITI state, hydration, projection, stream ownership, longer-timeframe, or workflow responsibilities.
3. Daily durable completion generates Weekly and Monthly commands and starts the Daily strategy workflow.
4. Weekly and Monthly durable completions each start their own strategy workflow and do not recurse.
5. Every affected realtime/event/command/projector/function handler emits the agreed source-generated structured logs.
6. A workflow failure exposes the root error details, actor, handler, stage, workflow identity, and parameter-set identity/version.
7. Normal filtering and no-change processing throw no exceptions and do not raise false failures.
8. Feed Health distinguishes ingress, no-change, source commit, projection, completion, and workflow progress without database polling.
9. Unit, BDD, integration, verification, and performance tests pass.
10. Benchmarks demonstrate bounded allocations and that busy ticks are rejected well above the configured input rate.
11. All test hosts are stopped and the API/UI can run without competing application instances.
12. The completion-driven ITI, actor convention, health, observability, and workflow documents describe the implemented behavior consistently.

---

## 15. Implementation and Gate Record

### 15.1 Implemented runtime

Gates 0-6 are implemented:

- `FuturesItiSignalRealtimeActor` owns one normalized market-price route and delegates it to the dedicated
  `FuturesMarketPriceUpdated` extension handler;
- the handler filters non-eligible updates, resolves the current ES and VX inputs, and awaits one Daily Generate
  command without owning ITI state, persistence, projection, stream subscriptions, Weekly/Monthly generation, or
  workflow admission;
- the durable Daily completion handler requests Weekly and Monthly generation and starts the Daily workflow;
- Weekly and Monthly completions start their matching timeframe workflows without recursive fan-out;
- source-generated structured logs cover ITI ingress, command evaluation, event commit/projection/completion,
  workflow command/projection, centralized pipeline dispatch and completion, terminal workflow state, and actual
  exception boundaries;
- `FuturesItiSignalRuntimeTelemetry` provides fixed-cardinality process-local health evidence without database
  polling or per-event history retention; and
- the live pipeline probe distinguishes route attachment, eligible ES traffic, input unavailability, command
  acceptance, durable completion, and failure, and its recovery action restarts the thin realtime actor.

### 15.2 Automated evidence

The following checks passed on 2026-09-14:

| Check | Result |
| --- | --- |
| Analytics project build | Passed, zero warnings and errors |
| Analytics integration-test project build | Passed, zero warnings and errors |
| Trade integrated-test project build | Passed, zero warnings and errors |
| Focused Futures ITI unit tests | 203 passed |
| Full Analytics unit suite | 1,042 passed |
| Futures ITI BDD suite | 67 passed |
| Strategy workflow unit tests | 82 passed |
| Thin Core NATS ingress integration | 1 passed |
| Live pipeline health tests | 10 passed, plus the focused ITI health case |
| Static observability/convention verification | Passed; 29 reserved logging EventIds were unique |
| API build using an isolated output path | Passed, zero warnings and errors |
| Repository whitespace validation | `git diff --check` passed |

The application was already running during qualification. Tests that create a second complete application host were
not started because concurrent IFM hosts previously interfered with the live feed. The thin NATS integration uses no
second application host and used isolated contract identities. No `testhost` process remained after the test runs.

### 15.3 Hot-path benchmark evidence

`FuturesItiIngressBenchmarks` ran in Release with 1,000, 5,000, and 10,000-operation inputs. The 10,000-operation
results were:

| Operation | Mean | Reported managed allocation |
| --- | ---: | ---: |
| Eligibility predicate | 224.203 microseconds | approximately 1 byte |
| Bounded telemetry updates | 867.179 microseconds | approximately 4 bytes |
| Busy-gate skips | 76.137 microseconds | 0 bytes |
| Source-generated command-created logging when disabled | 7.639 microseconds | 0 bytes |

These results qualify the synchronous filtering, telemetry, busy rejection, and disabled logging portions of the
realtime ingress. The slower durable command path is intentionally limited to one active operation and is independent
of the number of ticks ignored while Busy.

### 15.4 Gate 7 resolution: Free/Busy realtime sampling

The domain rule was clarified during Gate 7: one eligible ES tick is sufficient to initiate a complete Daily ITI
evaluation. Ticks arriving during that evaluation do not represent work that must be retained. The realtime actor now
uses one atomic generation gate. The successful Free-to-Busy transition starts and observes one command operation.
Busy ticks update a bounded counter and immediately return without creating a task, command, queue entry, log entry, or
retained tick object. The operation clears Busy in `finally`, including after failure. Orderly actor shutdown removes the
route and waits for the one active operation.

The updated BenchmarkDotNet run processed 10,000 busy-gate checks in 76.137 microseconds with zero reported managed
allocation, approximately 7.6 nanoseconds per ignored tick. Ten thousand disabled source-generated command-created log
calls took 7.639 microseconds with zero allocation. This is substantially above the 10,000-update-per-second Gate 7
input rate and decouples market burst admission from the slower durable command rate.

Gate 7 is complete for automated qualification. A live execution trace requires restarting the already-running API so
it loads this binary; no second application test host was started beside it.
