# Live pipeline minute audit

The API owns `LivePipelineMonitor`. It audits immediately and then every minute. The existing session reconciliation, five-second operations observer, and Databento watchdog remain independent. Health HTTP requests only read the last audit; they never start recovery.

`GET /api/market-data/live-health` returns component/scope/status/reason, observation and progress times, recovery attempts, and next recovery time. `/health/ready` includes `live_pipeline`. The Operations Health dialog also includes these checks. Missing evidence is `Unknown`, not a successful observation. Cached audits expire after 90 seconds.

## Checks

| Boundary | Evidence |
| --- | --- |
| Session and contracts | Valid session, future next boundary, matching active feed date, authoritative ES/VX assignments |
| Feed and native delivery | Existing live feed readiness, per-dataset readiness, produced/consumed counters, buffered work without downstream progress |
| Workers and processing | Existing supervised worker diagnostics, aggregation progress, independent observer freshness |
| Actor routing | Supervisor readiness, actual ITI routing-table membership |
| Messaging | Bounded PING/PONG on the shared NATS connection used by the pipeline; absent connection is unverified |
| Prices and publication | Current-contract accepted cache timestamps and publication progress relative to accepted updates |
| Tick persistence | Successful durable tick projection compared with durable publication progress |
| Chart bars | Process-local timer registration/liveness; current-contract, current-date latest durable bar read; 45-second bar freshness |
| Analytics | Configured RSI/ATR/ADX/MACD attachments for every activation timeframe; consumed closed-observation watermarks; enabled analytics/output telemetry |
| ITI | Eligible trade evaluation, missing VX prerequisite, projection failures, successful daily/weekly/monthly evaluation including no-signal results |
| Market Outlook | Processor readiness/pending work, required input completeness, publication versus ES trade progress, latest snapshot persistence |
| UI | Server-timed heartbeat, value date and contract agreement, received and rendered output timestamps for ES, VX and Outlook |
| Health monitor | Audit timestamp, timeout/failure evidence, disagreement between watchdog and feed runtime |

## Verified tick path boundary and pending correction

Production uses two different parts of the historical Futures Tick area, and they must not be
removed as one unit:

1. `StartFuturesTickDataStreamingCommand` and its matching stop/reset lifecycle remain in use. API
   startup calls the start command for each configured futures contract, and the resulting lifecycle
   event activates the application-owned stream. This control path is still required until a
   replacement ownership/control command is implemented and qualified.
2. `InsertFuturesTickDataCommand` and `FuturesTickDataEventProjector` are not the active Databento
   ES/VX persistence path. No production live-feed caller was found submitting the insert command.
   Client API methods and public contracts still exist, so removal requires a complete external
   caller and compatibility inventory rather than source-reference deletion alone.

The active data path is:

```text
Databento dataset worker
  -> TickAggregationService
  -> TickAggregationEventPublisher
  -> FuturesTickTradeDataChangedEvent | FuturesTickQuoteDataChangedEvent
  -> TickAggregationRealtimeActor
  -> TickAggregationRealtimeProjector
  -> MarketDataDb.InsertTickTradeDataAsync | InsertTickQuoteDataAsync
```

`TickAggregationService.LastDurableTickPublishedAtUtc` currently means that the normalized event
was accepted by the realtime publisher. It is upstream transport progress and is not confirmation
that MarketDataDb committed the row.

The minute audit's `Tick storage` evidence was added to `FuturesTickDataEventProjector`, which only
observes the older command/event-sourced insert path. The active
`TickAggregationRealtimeProjector` does not publish that evidence. Consequently the current live
pipeline can persist ticks successfully while both ES and VX remain `Tick storage = Unknown`.

The pending correction is:

1. Record storage success in `TickAggregationRealtimeProjector` only after
   `InsertTickTradeDataAsync` or `InsertTickQuoteDataAsync` completes successfully.
2. Record failure through the same actor/projector metric boundary when the database write fails;
   a publish timestamp must never overwrite a storage failure.
3. Key evidence by the actual contract ID from the inserted event. The minute probe continues to
   resolve authoritative current ES and VX contracts and queries the corresponding contract keys,
   so the same implementation covers both products and rollover contracts.
4. Record event ID, source event time, database completion time, projection kind, and bounded
   failure classification. Health freshness uses database completion/progress time and compares it
   with upstream publication progress.
5. Remove the misplaced evidence write from `FuturesTickDataEventProjector` when the active path is
   verified. If the legacy insert API is retained for compatibility, expose it under a distinct
   legacy metric name so it cannot satisfy live Databento health.
6. Inventory external HTTP/NATS consumers, persisted event compatibility, startup/reset callers,
   and test fixtures before retiring `InsertFuturesTickDataCommand` and its insert projector. Keep
   the still-used streaming lifecycle commands unless they receive a separately qualified
   replacement.

Acceptance requires real ES and VX realtime events, successful reads of the written trade/quote
rows, `Tick storage = Healthy` for each authoritative contract, injected write failures producing
`Unhealthy`, publication without storage completion remaining unconfirmed, and rollover proving
that evidence follows the new contract key.

Checks use upstream/downstream evidence instead of demanding price changes or a signal on every cycle. Long-frame analytics compare against previously published closed observations, not a one-minute output cadence. Quote-only updates do not require ITI signals. Off-hours source freshness follows the existing feed policy. UI acknowledgement allows 90 seconds to account for the independent minute sampling cycles.

Connected clients report through `POST /api/market-data/live-health/ui`. The report cannot change backend evidence or request a restart. Chart acknowledgements are sent only after the chart control successfully updates; caught rendering failures no longer count as success. Disconnected/unobserved clients remain separately visible with `Required=false`; they do not disable otherwise independent backend operation. Reports are bounded to 32 clients and three streams per client, and expire as current evidence after two minutes.

## Recovery and operational behaviour

- Only one audit/recovery runs at a time, with a 40-second deadline.
- Definite upstream faults take precedence. The existing serialized watchdog owns feed recovery.
- Missing/faulted chart timers are rearmed through the actor command API. Timer ownership remains idempotent.
- Missing analytics attachments are restored through their typed start commands. A missing ITI route is restored through the supervisor's deduplicated routing table.
- Recovery has at most three attempts per continuously failing component/scope, with one- and two-minute backoff between attempts. Successful progress resets the incident budget.
- An accepted restart command never turns a component green. A later observation must confirm recovery.
- Other failures remain explicit operator-required incidents; the audit does not invent replay semantics or restart shared storage. Existing persistence retries continue to own storage recovery.
- The UI can reconnect its bar consumer and reload the current snapshot, with at most three attempts until verified healthy.
- Changed failures are written to the status console, and recovery/diagnostics are logged.
- Market-condition assessments require a recent successful backend audit before treating feed evidence as available for new decisions. UI-only faults do not gate independent backend decisions; exit/risk-control paths are unchanged.

The `AlreadySatisfied` startup path explicitly ensures tick routes and chart streaming. A running Databento epoch is not evidence that these process-local downstream components were started.

The VX term-structure actor also tolerates actor registration before a live epoch exists: it retains its price router and defers stream acquisition until the first routed price update. Only `MarketDataApiNotRunningException` is deferred; other acquisition errors still propagate. Regression coverage verifies subsequent acquisition of both leases, idempotence and shutdown release.

## Future system design: queryable metrics for every actor

The current health implementation gathers evidence from several component-specific static
registries and optional recorders. A later delivery will introduce one uniform actor metrics system
so operations and health code can query every actor and projector through the same contract.

### Design decision

Actor metrics are written to an independent `IActorMetricsRegistry`, not queried synchronously by
sending a request through the actor's own mailbox. An overloaded, stopped, or faulted actor may be
unable to answer its mailbox, which is exactly when its metrics are needed. The registry exposes an
actor-scoped query API while actors remain the source of their own lifecycle and processing
measurements.

The registry key is the stable actor identity:

```text
ActorType + ActorName + BoundedContext + ProcessBootId
```

Entity-level detail is a bounded drill-down keyed by actor identity plus a normalized entity scope.
High-cardinality entity IDs are not emitted as ordinary metric labels. The registry retains a
bounded recent set and stores detailed failures separately by diagnostic ID.

### Common actor snapshot

Every actor exposes the following current-process facts through one versioned
`ActorMetricsSnapshot`:

| Group | Required measurements |
| --- | --- |
| Identity | Actor type/name, bounded context, process boot ID, instance ID, schema version |
| Lifecycle | Registered, starting, running, stopping, stopped, faulted, intake open, timestamps, restart count |
| Mailbox | Capacity, current depth, high-water depth, received, dequeued, rejected, coalesced, duplicates |
| Processing | In-flight count, completed, failed, cancelled, timed out, last receive/start/complete/fail UTC |
| Latency | Current, maximum and bounded histogram/percentiles for queue wait, handler, persistence and total time |
| Routing | Required subjects/routes, attached state, last message per route, delivery failure count |
| Persistence | Append/read attempts and failures, last committed revision/event, last successful UTC, duration |
| Projection | Accepted/completed/failed writes, backlog/replay depth, last projected event/source time, lag |
| Function stages | Initialization attempts/success/failure, calculation attempts/success/failure, terminal result |
| Diagnostics | Bounded reason code/type/message reference, diagnostic ID, correlation/causation IDs and phase |

Actor-specific metrics extend this common snapshot with a versioned bounded payload. Examples are
Databento produced/consumed records, tick rows committed per contract, workflow admission results,
and pipeline initialization reason counts. The common fields remain sufficient for generic health
and Operations UI rendering.

### Collection ownership

- Base actor lifecycle records registration, state, mailbox, handler, timeout, cancellation and
  terminal counters automatically.
- Actor producers record send/admission/rejection and transport latency.
- command/function state repositories record durable load/append and optimistic-concurrency facts.
- conventional and realtime projector bases record write, replay, backlog and failure facts.
- operator code records domain phases such as `StartPipelineAsync` and calculation outcomes through
  bounded typed extensions.
- Recording is nonblocking and cannot change actor business results. A recorder failure increments
  a separate telemetry fault and never reports the actor operation as successful or failed.

### Storage and query

The process registry owns the latest snapshot and monotonic counters for immediate health queries.
A background exporter periodically writes bounded time-series samples to the configured operations
metrics store. Detailed actor failure observations use a retention-limited diagnostic store linked
by diagnostic ID. Business event stores remain authoritative for workflow and trading state;
metrics do not grant trading authority or reconstruct missing business events.

Provide bounded queries by actor identity, status, process boot ID and observation window, plus an
exact actor query that returns common and actor-specific metrics. HTTP and NATS query adapters read
the independent registry/store. Operations Health and the minute audit consume the same query
contract, preventing separate evidence implementations from assigning conflicting states.

### Status policy

Generic actor health is derived from expected lifecycle state, current intake, mailbox progress,
failure recency, persistence/projection progress and route requirements. Quiet actors can be healthy
when no work is expected. A message accepted without later phase progress becomes degraded after
the actor-specific deadline. Missing required evidence is `Unknown`; recovery activity does not
become `Healthy` until new downstream progress is observed.

### Delivery plan for later work

1. Inventory every Actor, CommandActor, EventActor, RealtimeActor, QueryActor, FunctionActor,
   projector and repository; freeze metric names, units, cardinality budgets and schema.
2. Add the registry/query contracts and instrument shared base lifecycles first.
3. Instrument producer, repository, conventional projector and realtime projector bases.
4. Migrate current market-data, workflow and pipeline evidence into actor-specific extensions,
   including ES/VX tick storage confirmation at the active realtime projector.
5. Add bounded durable sampling, query actors/endpoints and Operations UI actor drill-down.
6. Replace component-specific health reads only after parity tests prove identical or more accurate
   status and reason output.
7. Remove superseded evidence registries and unused legacy tick insertion contracts only after
   production caller inventory and compatibility gates pass.

Verification includes actor stopped/faulted/overloaded cases, mailbox stall, handler failure,
persistence and projection failure, duplicate/replay, restart with a new process boot ID, metrics
export outage, bounded-cardinality stress, concurrent snapshot reads, and proof that health remains
queryable when the target actor cannot process messages.

## Verification

`TomasAI.IFM.LivePipeline.IntegrationTests` exercises the real minute coordinator, real HTTP endpoints and UI HTTP client, real bar timer, and real probe with controlled feed/storage dependencies. It covers missing timers behind healthy feeds, stale bars, storage failure isolation, UI receipt versus rendering, invalid reports, stale audits, bounded retries, non-overlapping minute scheduling, and the already-running startup regression.

Additional regression suites cover existing chart/tick handlers, shell/operations-health presentation, and the Scylla/NATS-backed ITI realtime pipeline. Tests use separate output directories and integration test keyspaces. They do not replace the running desktop/API processes.

Activation requires rebuilding and restarting the API and desktop together. A backend cannot prove UI rendering when an older desktop does not send acknowledgements; that evidence remains unverified.
