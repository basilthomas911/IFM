# IFM System-Wide Telemetry and Distributed Tracing Design

**Document type:** Target design and implementation contract

**Status:** Proposed for review; development/paper-trading summary may precede full application tracing

**Version:** 0.2

**Created:** 2026-08-20

**Owner:** IFM engineering

## 1. Purpose

Define a system-wide observability model for logs, metrics, and distributed traces, including a bounded telemetry
summary in Server Manager. This document establishes trace identity and propagation before tracing is added throughout
the application.

The design must make it possible to start a trace at a UI action and follow the resulting command or query through
local actors, NATS, PostgreSQL or ScyllaDB, emitted events, and a terminal UI outcome. It must also support scheduled
tasks and future trade-strategy and trade-monitor workflows without making observability state part of business
correctness.

This document does not approve a production Grafana stack, make Aspire a production dependency, or require complete
application instrumentation before Server Manager gains a useful summary view.

The Server Manager telemetry summary is intentionally scoped to development, integration testing, and paper trading.
It is not the approved production observability surface. Production readiness will be handled as a separate future
milestone that completes Aspire integration and deploys an approved durable telemetry collection, storage, dashboard,
alerting, security, and retention topology.

## 2. Decision summary

1. Use OpenTelemetry and the W3C Trace Context format end to end. In .NET, IFM instrumentation uses
   `System.Diagnostics.ActivitySource` and `Activity`.
2. Keep `CommandId` as the immutable business command and idempotency identifier. A `CommandId` is recorded on spans
   and logs, but it is **not** converted into or substituted for the W3C `TraceId`.
3. Add an immutable `QueryId` to the query contract and preserve it through the request and response path.
4. Use a separate business `CorrelationId` plus specific durable workflow identifiers for work that outlives one
   trace. Use `CausationId` to identify the immediate business predecessor where durable causation is required.
5. Propagate `traceparent`, optional `tracestate`, and tightly allowlisted baggage in NATS message headers. Do not put
   trace transport fields in MessagePack domain payloads.
6. Create spans at UI operations, command/query send and receive, actor processing, repository operations, external
   providers, scheduled runs, and bounded trade-workflow steps. Do not create a span for every market-data tick.
7. Instrument PostgreSQL and ScyllaDB through their centralized repository-provider boundaries. Do not record bind
   values, connection strings, credentials, or unsanitized dynamic SQL.
8. Start a new trace for replay, recovery, and later workflow cycles. Link it to prior trace context when useful rather
   than constructing traces that remain open for hours or days.
9. Build a bounded, non-authoritative Server Manager summary for recent logs, metrics, and trace completions during
   development and paper trading. Continue independent OTLP export so Aspire can be used in development and a durable
   production telemetry stack can be added later.
10. Telemetry failure, backpressure, or absence must never reject, delay, duplicate, or change a business operation.

## 3. Current-state assessment

The repository currently has a metrics foundation but not an end-to-end trace pipeline:

- `TomasAI.IFM.Framework.Telemetry` registers `AddIfmMetrics` and exports selected IFM, .NET runtime, ASP.NET Core,
  Kestrel, HTTP, actor, NATS, projector, and market-data meters through OTLP.
- The API server registers that metrics pipeline. Tracing is not registered with `WithTracing`, and IFM has no shared
  application `ActivitySource` catalog.
- `ICommand.CommandId` is propagated into `IEvent.CommandId` and is used for duplicate-command protection.
- `IQuery` has no query identifier or general correlation metadata.
- NATS command, event, query, request/reply, Core, and JetStream paths do not consistently inject and extract W3C
  trace context.
- PostgreSQL and ScyllaDB repository providers centralize most database work and are suitable instrumentation seams,
  but they do not currently create IFM database spans.
- Server Manager can supervise process stdout and stderr. The proposed Scheduler Host specification adds scheduled-run
  logs and metrics, but no common trace-summary feed exists yet.

These gaps mean that current `CommandId` searches can correlate some business evidence but cannot produce a complete
parent/child timing graph or naturally cover queries.

## 4. Identity model

Observability and business identities solve different problems and must remain separate.

| Identifier | Owner and lifetime | Purpose | Required propagation |
| --- | --- | --- | --- |
| `TraceId` | OpenTelemetry; one bounded causal execution | Joins spans for timing and dependency analysis | W3C trace context |
| `SpanId` | OpenTelemetry; one operation within a trace | Identifies a node in the trace graph | W3C parent context |
| `CommandId` | Domain/application; immutable for one command intent | Idempotency, duplicate rejection, command/event lookup | Command and resulting events; span/log attribute |
| `QueryId` | Application; immutable for one query request | Query/reply correlation and diagnostics | Query and reply; span/log attribute |
| `CorrelationId` | Domain/workflow; durable across related operations | Joins a business operation that may cross traces | Explicit business metadata |
| `CausationId` | Domain/workflow; immediate predecessor | Reconstructs durable cause without relying on telemetry | Explicit business metadata where required |
| `Event.Id` | Event contract; one event instance | Event identity | Event and event store |
| Workflow IDs | Domain-specific; potentially long lived | Strategy, monitor, trade, order, and scheduled-run lookup | Their owning domain contracts |

### 4.1 Why `CommandId` is not the trace ID

Both identifiers may be represented by 16 bytes, but equivalence would create incorrect semantics:

- a retried delivery of the same command keeps its `CommandId`, while each delivery attempt may need a separate span;
- one UI operation can submit multiple commands and queries within one trace;
- one command may trigger later replay, projection, or reconciliation work in a new trace;
- commands started outside the UI still require valid trace roots;
- a long-running strategy can issue many commands across many short traces; and
- sampling, privacy boundaries, and external trace-context validation belong to the tracing system, not the duplicate
  guard.

The standard attribute is `ifm.command.id`. The trace summary may make this field searchable and visually prominent,
which gives the desired command-centric operator workflow without corrupting the trace model.

### 4.2 Query identity

Add `Guid QueryId { get; init; }` to the common query model through a compatibility-safe contract migration. The ID is
created at the first public boundary—normally the UI or API—and is never regenerated by routing, retries, actors,
repositories, or response mapping.

The query response contract or transport reply metadata must return the same `QueryId`. An absent ID received from an
older client is assigned once at the compatibility boundary and recorded with `ifm.query.id.generated=true` during
the migration period.

### 4.3 Durable workflow identity

Future trading workflows require explicit business identifiers such as:

- `StrategyInstanceId` for the configured and running strategy instance;
- `StrategyEvaluationId` for one evaluation cycle;
- `TradeWorkflowId` for a proposal-to-terminal-trade lifecycle;
- `OrderIntentId` for the durable intent before broker submission;
- `BrokerOrderId` for broker reconciliation; and
- `ScheduledRunId` for one Scheduler Host execution attempt.

These fields join separately sampled traces and authoritative state. They must not be encoded only in baggage or
telemetry.

## 5. Trace model and boundaries

### 5.1 General rule

A trace represents one bounded causal operation with a clear start and terminal outcome. Most traces should last
milliseconds to seconds. A trace may last for a bounded UI wait or scheduled-task run, but must not be kept open merely
to represent a durable business lifecycle.

Use the current `Activity` context implicitly across `async` calls. Do not add trace parameters to domain method
signatures. At a transport boundary, inject or extract the context explicitly.

### 5.2 UI-rooted operations

The WPF UI creates a root activity for a user intent when no parent activity exists:

```text
ui.operation ExecuteTradeAction
  command create SubmitTrade
  publish <command-subject>
    process <command-subject>
      actor.process SubmitTrade
        INSERT command_log
        event create TradeSubmitted
        publish <event-subject>
          process <event-subject>
            actor.process TradeSubmitted
              SELECT/INSERT projection
  ui.await_terminal TradeSubmitted
```

Recommended UI roots are `ui.command.submit`, `ui.query.execute`, and `ui.operation`. Tags identify the view-model type,
action name, safety mode, and non-sensitive outcome. Raw form data, account numbers, symbols, and order parameters are
not recorded by default.

The UI waiting activity ends on terminal success, terminal failure, cancellation, or timeout. A timeout is the UI's
observed outcome and does not assert that the business operation failed. The span records `ifm.outcome=unknown` when
the authoritative result is not known.

Commands created by scheduled tasks, HTTP endpoints, or internal workflows create an equivalent root when there is no
valid incoming parent.

### 5.3 Commands and events

For live processing, a command send span is followed by a consumer processing span. Resulting event creation and send
spans remain in the active trace where the work is causally synchronous or directly queued.

Required searchable attributes include:

- `ifm.command.id` and `ifm.command.name`;
- `ifm.event.id`, `ifm.event.name`, and `ifm.event.type` where applicable;
- `ifm.correlation.id` and `ifm.causation.id` when present;
- bounded actor type, route kind, owning host, and outcome; and
- duplicate, overload, retry, redelivery, and durable-ack outcomes.

Duplicate detection remains authoritative in the command log. A duplicate command span records
`ifm.command.duplicate=true`, the guard backend, and the shortcut outcome, then ends without processing the handler.
It must not include the serialized command body.

### 5.4 Queries and replies

A query trace begins at the UI/API caller or at the internal caller when no parent exists. It includes:

- query creation/send;
- NATS request and consumer processing when remotely routed;
- actor mailbox wait and query handling;
- PostgreSQL/ScyllaDB calls;
- reply send and client receive; and
- UI result mapping/render readiness where useful.

`ifm.query.id` is mandatory on query send, process, and reply spans. Query text and query parameters are not telemetry
attributes. Use the query contract name and a low-cardinality repository operation summary.

### 5.5 Local actor calls

Same-process actor dispatch keeps `Activity.Current`; it does not serialize trace headers. Create separate internal
spans for mailbox enqueue/wait and handler processing only when they add diagnostic value. Existing metrics remain the
primary always-on signal for high-volume mailbox behavior.

### 5.6 NATS Core, request/reply, and JetStream

All IFM NATS publishers inject the active context into `NatsHeaders`:

- `traceparent`;
- `tracestate` when present and allowed; and
- `baggage` only after IFM allowlist and size validation.

Consumers extract the context before deserialization and start a `CONSUMER` processing activity. Publishers create a
`PRODUCER` send activity. Span names and attributes follow the applicable OpenTelemetry messaging semantic convention,
using low-cardinality subject templates where a raw subject embeds an entity ID.

This applies to commands, events, queries, request/reply inboxes, routed messages, durable JetStream delivery,
redelivery, settlement, and dead-letter/recovery paths. Owned NATS message wrappers must preserve source headers until
extraction completes.

Malformed, oversized, or disallowed trace headers are ignored and counted. They never fail message processing. At
external trust boundaries, IFM may start a new trace and link to validated incoming context rather than accepting an
untrusted caller's sampling decision.

The MessagePack payload remains unchanged by trace propagation. This prevents trace implementation details from
becoming part of domain serialization compatibility.

### 5.7 Durable events, replay, and projections

An event stored during a live trace keeps its normal durable business identifiers. If origin trace context is retained
for diagnostics, store only immutable `OriginTraceId` and `OriginSpanId` metadata; never treat that metadata as a
required event-store field or an active parent after restart.

Replay, projector recovery, backfill, and reconciliation start new traces. Add an `ActivityLink` to the origin context
when it is available and useful. The new trace records the replay/recovery reason, range/checkpoint, and current
workflow identifiers. This preserves causal discovery without producing a misleading hours- or days-long trace.

## 6. Database tracing

### 6.1 Instrumentation seam

Prefer supported client instrumentation when it produces safe, stable spans. Add manual activities at IFM's logical
repository boundary when provider instrumentation is absent, incomplete, or too low level.

The initial manual seams are:

- `PostgresObjectDataRepositoryProvider` execute, query, scalar, stream, bulk, batch, and transaction operations; and
- `ScyllaDbObjectDataRepositoryProvider` execute, query, scalar, stream/page, prepared-statement, and batch operations.

Do not double-instrument the same logical operation. If a client library later emits adequate child spans, retain the
IFM repository span only when it describes a distinct logical operation.

### 6.2 Span contents

Database spans use `ActivityKind.Client` and current OpenTelemetry database conventions. At minimum record:

- `db.system.name`: `postgresql` or `cassandra` for ScyllaDB-compatible CQL instrumentation;
- `db.namespace` when safe and known;
- `db.operation.name` such as `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `BATCH`, or a bounded logical operation;
- `db.collection.name` when one known table is involved;
- `db.query.summary`, preferably an explicit stable operation/table fingerprint;
- safe server address/port according to deployment policy;
- batch size, returned/affected row count when cheaply available;
- retry/page count as `ifm.db.*` attributes; and
- error status and exception class on failure.

Span duration covers the logical call seen by the repository caller, including an internal client retry. A streaming
span covers acquisition and active enumeration only when lifecycle ownership is reliable; otherwise split query start
from page-fetch spans and keep a metric for total rows/duration.

### 6.3 Data protection

The following are prohibited by default:

- connection strings, credentials, tokens, or certificates;
- bind/parameter values;
- raw command, event, or query payloads;
- unsanitized dynamic SQL or CQL;
- account numbers, order details, prompts, and personal data; and
- high-cardinality row, partition, entity, symbol, or message identifiers as metric labels.

Parameterized query text may be considered later only after a sanitizer and explicit security review. Initial rollout
uses `db.query.summary` and named repository operations. Do not inject trace context into SQL comments.

## 7. Trade strategy and monitor workflows

Trade workflows are the highest-value tracing target and the strongest reason not to equate a trace with a command or
long-running strategy.

### 7.1 Trace units

Create a separate bounded trace for:

- one strategy evaluation cycle;
- one signal-to-proposal decision;
- one risk evaluation or approval attempt;
- one order submission attempt;
- one broker acknowledgement/rejection/timeout reconciliation;
- one monitor evaluation;
- one modify/cancel/exit decision; and
- one recovery or operator intervention.

Connect them through `StrategyInstanceId`, `TradeWorkflowId`, `OrderIntentId`, and broker identifiers. Add span links
from a later trace to the decision or submission trace that caused it when the context is retained.

### 7.2 Suggested trace shape

```text
strategy.evaluate
  query market/context state
  strategy.compute
  risk.precheck
  command create ProposeTrade
  publish/process
  persist proposal

order.submit                         [linked to proposal trace]
  risk.authorize
  broker.request
  persist broker correlation
  process broker acknowledgement

trade.monitor.evaluate               [linked through TradeWorkflowId]
  query position/market state
  monitor.compute
  command create ModifyOrExitTrade
```

Metrics capture every cycle and latency distribution. Detailed spans are sampled. Error, rejection, ambiguous broker
outcome, risk denial, forced stop, recovery, and operator intervention traces receive retention priority.

### 7.3 Market-data boundary

Do not create a span for every quote, tick, bar, indicator update, or actor hot-path operation in normal mode. Use
metrics and structured diagnostic counters for continuous flow. A time-bounded diagnostic mode may trace a sampled
subset keyed by a safe feed or workflow category, with a hard expiry and operator-visible overhead warning.

## 8. Logs, metrics, and trace correlation

### 8.1 Logs

Structured application logs emitted while an activity is active include `TraceId` and `SpanId`. IFM logging enrichers
also include available business identifiers such as `CommandId`, `QueryId`, `CorrelationId`, and `ScheduledRunId`.

Stdout and stderr remain useful process evidence but are not authoritative business state. Server Manager displays
them even if structured OpenTelemetry log export is disabled.

### 8.2 Metrics

Existing low-cardinality metrics remain always-on and are not replaced with spans. Metric dimensions may include
bounded operation, actor type, route, service, result class, database system, and workflow stage. They must not include
trace, command, query, event, entity, order, account, or symbol IDs.

Where supported by the eventual backend, exemplars can attach a sampled `TraceId` to a latency or error metric without
turning the ID into a metric dimension.

### 8.3 Resource attributes

Every executable host emits consistent resource attributes:

- `service.name`;
- `service.instance.id`;
- `service.version`;
- `deployment.environment.name`;
- host/process attributes allowed by policy; and
- an IFM capability/role attribute when it adds information beyond `service.name`.

Custom attributes use the `ifm.*` namespace. Attribute names and allowed values are versioned in a shared telemetry
contract rather than invented independently by each host.

## 9. Server Manager telemetry summary

### 9.1 Goal

Add one operator view that answers:

- Are the API, UI, Scheduler Host, databases, and messaging dependencies healthy?
- Are warning/error rates, actor backlog, NATS failures, or process resource use changing?
- What recent operations were slow or failed?
- Can an operator pivot from a `CommandId`, `QueryId`, or workflow ID to its trace and related logs?

The summary is deliberately smaller than Aspire or Grafana. It is a local development and paper-trading operational
overview and troubleshooting entry point, not a durable observability database or production monitoring commitment.

### 9.2 View layout

The proposed `Telemetry Summary` page contains:

1. **Health strip** — API, UI, Scheduler Host, PostgreSQL, ScyllaDB, NATS, and collector status with last update.
2. **Process cards** — state, uptime, CPU, working set, thread count, restart count, and recent stdout/stderr warning and
   error counts.
3. **System metrics** — actor utilization/mailbox depth, overload/duplicate totals, NATS publish/redelivery/failure
   rates, projector lag, database latency/failure rate, and scheduled-task outcomes.
4. **Recent traces** — UTC start, duration, status, root operation, originating service/view, participating services,
   span/error counts, and available command/query/workflow IDs.
5. **Trace detail** — a compact waterfall/tree, span duration/status, safe attributes, and correlated log lines.
6. **Search and filters** — time range, status, service, root operation, `CommandId`, `QueryId`, `CorrelationId`,
   `TradeWorkflowId`, and `ScheduledRunId`.

An unsampled operation may appear only in metrics/logs and business lookup results; the UI must say “trace not
retained,” not “operation not found.”

### 9.3 Data-source design

Use an `IObservabilityQueryClient` abstraction so the view does not depend on Aspire Dashboard internals:

```text
Processes/health -------------------------------> Server Manager view
Host metric/span summaries --- bounded IPC ---> local trace aggregator and in-memory ring
OTel logs/metrics/traces -------- OTLP ---------> Aspire now / Collector and Grafana later
Authoritative business lookup ---- API/DB ------> command/query/workflow status
```

For the first summary version:

- retain the existing supervised stdout/stderr feed;
- collect process metrics and health directly from supervised processes/endpoints;
- add an optional bounded `ServerManagerSummaryExporter` to the shared OpenTelemetry pipeline;
- send only selected metric snapshots and completed span summaries over an authenticated local named pipe;
- group the received spans by `TraceId` in Server Manager to build a best-effort recent trace summary;
- store 15–60 minutes in bounded in-memory rings; and
- deep-link to the configured full backend when available.

The exporter uses a bounded non-blocking queue, fixed message-size limits, drop counters, reconnect with jitter, and
no persistence in application hosts. If Server Manager is absent or slow, summaries are dropped; OTLP export and the
business operation continue independently.

The summary pipe is a local operational transport exception, not a general telemetry bus. General cross-host
telemetry still uses OTLP and never uses NATS.

### 9.4 Backend progression

| Stage | Full telemetry view | Server Manager role |
| --- | --- | --- |
| Current/initial | OTLP optional; stdout/stderr and metrics foundation | Local summary and process supervision |
| Development | Standalone Aspire Dashboard or Aspire AppHost dashboard | Summary plus deep link to full trace |
| Paper trading | Collector with retained logs/metrics/traces | Operational summary and high-value pivots |
| Production | Future full Aspire integration plus approved durable collection, storage, dashboards, and alerting | Server Manager summary is optional and not the production authority |

Aspire Dashboard is suitable for local development visualization, including logs, metrics, and traces, but its
standalone telemetry storage is bounded/in-memory and it is not the production monitoring system.

The future production milestone may use Aspire AppHost and Service Defaults to compose and consistently configure the
production observability topology. Production readiness still requires durable stores, secured ingestion, retention,
alerting, capacity validation, backup/recovery where applicable, and operator runbooks beyond the standalone Aspire
Dashboard.

## 10. Sampling and retention

### 10.1 Policy

- **Development:** sample all bounded application traces while testing, subject to local memory/export limits.
- **Automated tests:** deterministic sampler and in-memory exporter; no network exporter unless the test requires it.
- **Paper trading:** parent-based head sampling for normal work plus Collector tail policies that retain errors, slow
  traces, trade/order/risk decisions, operator actions, recovery, and scheduled-task failures.
- **Production:** rates and tail policies require an explicit capacity, privacy, and cost review before activation.

Routine strategy monitoring and market-data-adjacent work use lower rates. Duplicate-command shortcuts, ambiguous
outcomes, safety interlocks, risk denials, broker errors, and forced termination are high-value categories.

### 10.2 Limits and controls

Configuration includes:

```text
Telemetry:Tracing:Enabled
Telemetry:Tracing:OtlpEndpoint
Telemetry:Tracing:Sampler
Telemetry:Tracing:SampleRatio
Telemetry:Tracing:Sources:Ui
Telemetry:Tracing:Sources:Messaging
Telemetry:Tracing:Sources:Actors
Telemetry:Tracing:Sources:Storage
Telemetry:Tracing:Sources:Strategies
Telemetry:Summary:Enabled
Telemetry:Summary:PipeName
Telemetry:Summary:QueueCapacity
Telemetry:Summary:RetentionMinutes
```

Each source has a kill switch. Disabling tracing must leave metrics and business behavior intact. The tracing pipeline
must bound span attributes, events, links, baggage, queue sizes, batch sizes, export timeout, and shutdown flush time.

## 11. Error, cancellation, and outcome semantics

- Set span status to error for an operation that fails according to that operation's contract.
- Record the exception type and sanitized message/stack according to the logging policy; never attach serialized
  commands or secrets.
- Expected business rejection is represented by a bounded `ifm.outcome` and does not automatically mean infrastructure
  failure. Safety/risk rejection remains highly searchable.
- Cancellation requested by the caller records `ifm.outcome=canceled` and the responsible boundary.
- A timeout records `ifm.outcome=timeout`; if durable completion is unknown, also record
  `ifm.authoritative_outcome=unknown`.
- NATS redelivery is a new processing attempt span with redelivery attributes. It keeps the business ID and links or
  parents according to the retained creation context.
- Duplicate suppression is a successful guard decision but a shortcut business outcome, not a handler success.

## 12. Security and privacy

1. Trace context is untrusted input at HTTP, NATS, plugin, broker, and external-provider boundaries.
2. Validate length and format before extraction. Invalid context is discarded without rejecting the business message.
3. Baggage is disabled by default until an allowlist is defined. It never contains account, order, credential,
   strategy parameter, prompt, or personal data.
4. Raw subjects containing entity values are converted to stable templates for span names and metric tags.
5. High-cardinality business identifiers are permitted only on sampled spans and structured logs under the retention
   policy; metric tags remain bounded.
6. OTLP endpoints use authentication and TLS outside an explicitly loopback-only development setup.
7. Aspire standalone OTLP ingestion must not be exposed without its documented authentication and network controls.
8. The Server Manager summary pipe uses local ACLs, authenticated framing, fixed payload limits, and redaction before
   transmission.
9. Observability access is an operator capability and is audited where it exposes business identifiers.

## 13. Shared implementation architecture

Evolve `TomasAI.IFM.Framework.Telemetry` from metrics-only registration to a single host registration surface such as
`AddIfmObservability`. It owns:

- resource identity;
- the existing metrics sources and OTLP metrics exporter;
- approved IFM `ActivitySource` names;
- ASP.NET Core and HTTP-client instrumentation where applicable;
- OTLP trace export;
- structured logging correlation/export when enabled;
- sampling and redaction policy;
- the optional Server Manager summary exporter; and
- pipeline self-metrics for accepted, dropped, queued, exported, and failed telemetry.

Each executable host registers one intended OpenTelemetry pipeline. Libraries define static source/meter names and
emit instrumentation but do not create exporters or depend on Aspire.

Proposed source names are stable and coarse:

```text
TomasAI.IFM.UI
TomasAI.IFM.Framework.Messaging.Nats
TomasAI.IFM.Shared.EventModelActor
TomasAI.IFM.Framework.Storage.Postgres
TomasAI.IFM.Framework.Storage.ScyllaDb
TomasAI.IFM.Application.Scheduler
TomasAI.IFM.Domain.Trading
```

## 14. Delivery sequence

### T0 — Contract and baseline

- Approve this identity model and attribute allowlist.
- Capture current metrics/export cost and hot-path benchmarks.
- Inventory every NATS publish, request/reply, receive, owned-message, and JetStream settlement path.

### T1 — Shared observability pipeline

- Add shared resource configuration and tracing registration while preserving `AddIfmMetrics` compatibility.
- Add in-memory exporter test support and pipeline self-metrics.
- Correlate structured logs with `TraceId`/`SpanId`.

### T2 — UI, queries, and propagation

- Add UI operation roots.
- Add `QueryId` with MessagePack compatibility tests.
- Inject/extract W3C context through every NATS header path and query reply.

### T3 — actors and databases

- Add sampled actor processing spans around existing measured stages.
- Instrument PostgreSQL and ScyllaDB logical operations at centralized providers.
- Validate SQL/CQL redaction and hot-path overhead.

### T4 — Server Manager summary

- Add the bounded summary exporter and local query model.
- Build health, metric, recent-trace, trace-detail, and correlation search panels.
- Add Aspire/OTLP backend status and configurable deep links without an Aspire API dependency.

### T5 — scheduled tasks and trading workflows

- Create scheduled-run roots and child-process/external-operation spans.
- Instrument strategy evaluation, risk, order submission, broker acknowledgement, monitoring, and recovery units.
- Add tail-retention rules for high-value trading outcomes.

### T6 — paper-trading validation and production observability design

- Run production-like load, fault, privacy, and retention tests.
- Tune sampling and budgets from measured evidence.
- Use paper-trading evidence to design the later full Aspire-integrated production telemetry topology.
- Treat durable collection, storage, dashboards, alerting, security, retention, and operational recovery as a separate
  production-readiness milestone rather than extending the Server Manager summary implicitly.

Each tranche is independently reviewable and reversible. Adding the summary view does not require completing T5 or
selecting the final production backend.

## 15. Testing and verification

### 15.1 Unit and contract tests

- valid/missing/invalid/oversized `traceparent`, `tracestate`, and baggage extraction;
- propagation through Core NATS, request/reply, owned messages, and JetStream;
- MessagePack compatibility before and after `QueryId` introduction;
- `QueryId` round trip and compatibility-boundary generation;
- command/event `CommandId` preservation while `TraceId` remains independent;
- duplicate-command shortcut span outcome;
- replay/recovery uses a new trace and an `ActivityLink` rather than a remote parent;
- safe subject templating and attribute cardinality;
- database summaries contain no bind values, credentials, raw entity values, or forbidden SQL/CQL;
- error, cancellation, timeout, unknown outcome, retry, and redelivery status;
- structured logs receive active trace/span IDs;
- summary exporter queue drop/reconnect/shutdown behavior; and
- disabled tracing does not change results.

### 15.2 Integration tests

- UI root through command, NATS, actor, both command-log backends, event, and terminal UI outcome;
- UI query through NATS request/reply and PostgreSQL/ScyllaDB repository calls;
- mixed local and remote actor routing preserves the graph;
- JetStream redelivery and replay produce the intended attempt/link structure;
- PostgreSQL and ScyllaDB failures produce safe database spans;
- Scheduler Host run correlates process stdout/stderr and scheduled-run identity;
- OTLP export is readable by standalone Aspire Dashboard or the test Collector; and
- Server Manager continues operating when summary IPC or the OTLP collector is unavailable.

### 15.3 Performance gates

- Benchmark unsampled, sampled-without-export, and sampled-with-export paths separately.
- Measure allocations and p50/p95/p99 at command dispatch, actor handling, NATS, and storage boundaries.
- Verify no per-tick activity creation in normal market-data operation.
- Saturate the summary queue and collector while proving bounded memory and unchanged business admission behavior.
- Record the approved overhead budget before enabling tracing in paper or production environments.

## 16. Acceptance criteria

The initial tracing foundation is accepted when:

1. `CommandId`, `QueryId`, correlation/workflow IDs, `TraceId`, and `SpanId` have documented, tested, non-overlapping
   semantics.
2. A UI-started sampled command can be followed through NATS/local actors, duplicate guard, storage, event, and
   terminal outcome.
3. A sampled query can be followed through request/reply and PostgreSQL or ScyllaDB.
4. Every NATS transport path propagates valid context without changing MessagePack payload compatibility.
5. Database spans reveal operation and latency without parameters, credentials, or sensitive dynamic text.
6. Replay and long-lived workflow activity use new traces plus durable IDs/links.
7. Metrics remain low cardinality and always-on; high-volume market-data work does not emit a span per item.
8. During development and paper trading, Server Manager shows bounded recent log, metric, and trace summaries and
   remains responsive under exporter failure.
9. Aspire can display development traces through normal OTLP export, but no runtime component requires Aspire.
10. Failure or disabling of any telemetry component has no effect on business correctness, command deduplication,
    scheduling, or trading safety.

## 17. Deferred decisions

- Final production Collector, trace store, log store, metrics store, Grafana topology, and retention period.
- Full production Aspire integration, dashboards, alerts, access control, capacity, and operational runbooks.
- Whether sanitized parameterized SQL/CQL text is ever enabled.
- Exact head/tail sampling rates after paper-trading evidence.
- Whether origin trace IDs are persisted beside durable events or only in correlated logs.
- Cross-machine Server Manager observability access and authorization.
- Alert routing and external notifications.
- Full strategy/monitor domain contracts and their workflow identifier names.

## 18. References

- [W3C Trace Context Recommendation](https://www.w3.org/TR/trace-context/)
- [OpenTelemetry context propagation](https://opentelemetry.io/docs/concepts/context-propagation/)
- [OpenTelemetry messaging span conventions](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/)
- [OpenTelemetry database client span conventions](https://opentelemetry.io/docs/specs/semconv/db/database-spans/)
- [OpenTelemetry .NET traces](https://opentelemetry.io/docs/languages/dotnet/traces/)
- [Aspire dashboard overview](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/overview)
- [Aspire dashboard security considerations](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/security-considerations)
- `Documents/system/System-Wide-Optimization-Results.md`
- `Documents/system/Aspire migration overview.md`
- `TomasAI.IFM.Application.ServerManager/Docs/ServerManager-Scheduled-Task-Supervision-Specification.md`

## 19. Revision history

| Version | Date | Summary |
| --- | --- | --- |
| 0.2 | 2026-08-20 | Limited the Server Manager summary to development through paper trading and deferred production observability to a full Aspire-integrated, durable production-readiness milestone. |
| 0.1 | 2026-08-20 | Defined identity, UI/query/NATS/database/workflow tracing, sampling and security policy, a bounded Server Manager telemetry summary, phased delivery, and acceptance gates. |
