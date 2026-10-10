# Databento Hard and Soft Recovery and Terminal Shutdown Design v1.0

## Current Development recovery contract - 2026-10-08

This section supersedes the downstream qualification gates in the historical sections below for the enabled API recovery pipeline. Production enablement remains a separate qualification.

### Soft actor recovery

`LivePipelineMonitor.CheckOnceAsync` reads a health snapshot once per minute. Required actionable downstream failures get one minute of confirmation, then at most three targeted recovery attempts. ITI uses `Realtime.FuturesItiSignalRealtime`; Market Outlook uses `Realtime.MarketOutlook`. Chart bars and indicator attachments use their registered lifecycle operations.

For disposable realtime actors, `LivePipelineProbe.RecoverDownstreamAsync` calls `ActorSupervisor.RestartAsync`. The supervisor constructs a fresh context, pauses old admission, retires/fences the old generation, discards its queued work, starts the replacement, and resumes admission. It never waits for the retired active handler to drain. This is logical replacement, not forced termination of a pooled thread. Financial execution actors retain their existing durable behavior.

Missing Market Outlook inputs remain unhealthy evidence and can prevent new decisions, but do not restart a working consumer: replacing a consumer cannot manufacture missing source data. Optional checks cannot trigger actor or feed resets. A subsequent healthy check confirms recovery; replacement completion alone does not.

### Hard feed recovery

Both the live pipeline monitor and Databento watchdog call the single `ApiDatabentoRecoveryPipeline` owner. Concurrent requests are rejected immediately as already running. Accepted recovery is independent of caller cancellation and bounded by host lifetime and the overall deadline.

1. Capture the existing in-memory subscriptions and authoritative value date.
2. Fence old dataset generations and reject an uncontained old publisher send.
3. Stop/kill the exact owned workers and confirm containment.
4. Start replacement workers. Startup exclusively owns the control pipe through hello and manifest acceptance; health requests cannot overtake that exchange.
5. Confirm local connection/subscription readiness and fresh provider traffic or heartbeat. Do not require a trade, a quote, or completely empty queues.
6. Ensure the publisher is running.
7. Admit the exact replacement generations and return session ownership to the existing lifecycle.

Steps 3 through 5 get at most three replacement attempts. Startup or local-readiness failure retries after containing the whole previous candidate group. Failed containment, lost publisher isolation, invalid authority/identity, publisher startup failure, or admission failure remains terminal. After essential recovery failure or exhaustion, the existing bounded API shutdown requests exit code 42. There is no recursive recovery loop.

Recovery performs no Redis/PostgreSQL/ScyllaDB probe, Supervisor reconciliation/canary event, candidate tick holding, or test storage write. Those dependencies are observed by normal operational health and their owners. Feed recovery completion does not assert every analytics/storage dependency is healthy; decision admission still uses the actual health snapshot.

After a successful feed reset, clear old component retry counts and observe the rebuilt pipeline for the configured five-minute recovery window, measured from completion. Do not replace actors or escalate from pre-reset observations during that window. Remaining failures are evaluated from subsequent audits.

### Evidence

Once per minute, log every failing check with method, value date, component, scope, required flag, status, reason, observation/progress timestamps, attempt count, recovery state and next deadline. Log targeted attempt start and subsequent confirmed health. Hard actions retain correlation/action/duration/exception records; retries include attempt and failed action. Include Degraded and Unknown reasons in the hard request, not only Unhealthy status. Worker hello rejection reports individual identity/sequence/token-match booleans; token contents are never logged.

### Verification

Regression coverage includes concurrent health polling during three real synthetic worker startups, local readiness with queued records, stale-provider rejection, three bounded hard attempts, transient success on attempt three, containment/admission failures, concurrent request rejection, old retry-count suppression, missing-input suppression, and structured trigger/confirmation fields. Real Databento network recovery requires a separate live run; synthetic tests cannot prove provider availability.


**Status:** Design approved in conversation; implementation pending  
**Date:** 2026-09-30  
**Scope:** API hosted Databento recovery, downstream qualification, supervisor reconciliation, and terminal shutdown

## 1. Authority and intent

This specification governs an API process after a hard reset request. It supersedes conflicting recovery rules in the older Databento watchdog, Market Data resiliency, and Application Startup and Databento Recovery designs in this folder. Database reconciliation during hard reset, NATS publication as proof of Databento health, and unlimited hard retries no longer apply. The older documents retain their measurement and historical context. This document states intended behavior, not current implementation status.

Hard recovery proves a fresh and exclusive Databento generation is healthy at the local worker boundary. Soft recovery qualifies NATS, Redis, PostgreSQL, and ScyllaDB, asks the special supervisor context to reconcile actor and projector health, and verifies downstream progress. Three failed hard attempts, or loss of generation exclusivity, make this API process unrecoverable.

## 2. Trigger and coordination

Failure detection precedes hard reset. A five second downstream stall, terminal worker signal, operator request, or supervisor escalation can request it. The executor receives reason and evidence but does not detect or wait for a stall. An ordinary downstream outage with locally healthy Databento should usually request soft recovery; an explicit hard reset remains executable.

One process local coordinator owns each recovery episode. The first request freezes the value date, required datasets, last qualified in memory subscription manifests, current generation ID, reason, and bounded evidence. Concurrent requests join its result and append bounded diagnostics. They cannot queue duplicate resets or restart the attempt count. Requests after an unrecoverable decision are rejected. No actor message or remote service is required to accept or execute the episode.

Missing, stale, or invalid frozen manifests cause an attempt failure with precise evidence. Hard recovery cannot query a database to repair them. Scheduled value date and contract rollover reconciliation is a separate workflow.

## 3. States

Running -> HardRecoveryRequested -> HardRecovering -> DatabentoHealthyDownstreamFenced -> SoftRecoveringInfrastructure -> SoftRecoveringSupervisor -> EndToEndQualifying -> Running.

Finite soft recovery exhaustion yields DatabentoHealthyDownstreamDegraded. Locally healthy Databento stays alive, downstream admission stays fenced, and operational health reports the failed dependency or component.

Three failed hard attempts or a hard safety violation yield HardRecovering -> Unrecoverable -> ShutdownRequested -> GracefulShutdown -> Terminated. A shutdown exception or deadline yields GracefulShutdown -> ForcedExit -> Terminated. Unrecoverable and ShutdownRequested are irreversible within the current process; all recovery initiators become passive.

Every transition records operation, attempt, generation, time, and reason. A successful hard recovery clears its consecutive hard failure count. A later independent incident can start a new episode while the process is Running.

## 4. Hard recovery boundary

Hard recovery may use frozen in memory manifests and value date; Databento credential and network; owned worker executable and native libraries; OS process tree control; local pipes; clocks, cancellation, local generation admission, and direct local logging. It cannot require NATS, Redis, PostgreSQL, ScyllaDB, actors, projectors, status console publication, contract reconciliation, or composite API startup and shutdown orchestration. Startup may share only narrow worker and native primitives that satisfy this boundary.

Each attempt owns fresh worker handles, pipes, native sessions, cancellation and qualification state, and a generation ID. Its stages are:

1. Fence the old generation at the earliest local admission gate, including queued callbacks and stale publication.
2. Detach or quarantine the downstream publisher without awaiting an outstanding NATS send.
3. Stop the exact old worker process trees within a short deadline, force kill survivors, and verify no old worker can supply admitted input.
4. Create fresh attempt resources and start required workers from frozen manifests.
5. Connect and authenticate with Databento; confirm required subscriptions were accepted.
6. Qualify every required dataset at the local worker and pipe boundary using accepted subscriptions, fresh provider traffic or heartbeat, responsive live workers, empty local backlogs, and no terminal status. A market trade or quote is not required to arrive during recovery; an open socket alone is insufficient.
7. Atomically designate the candidate as the authoritative local Databento generation while keeping downstream publication fenced.

Core hard success does not require tick aggregation, NATS send, database operation, actor processing, projection, analytics, or UI delivery. Qualification records dataset, generation, subscription, last record or heartbeat, and elapsed time.

## 5. Attempt failure and retry limit

Every hard stage has a bounded deadline and exception boundary. The controller returns a structured result with original failed stage and exception, attempt and generation IDs, worker PIDs, subscriptions, qualifying counts and timestamps, termination results, cleanup errors, and duration. No exception escapes the episode controller.

On a safe failure, fence the candidate, cancel local work, close pipes, stop or force kill exact workers, verify isolation, and dispose owned resources within a cleanup deadline. Record cleanup exceptions separately without replacing the original error. Start the next attempt with new resources. A blocked NATS send or publisher stop failure cannot poison the next Databento attempt.

If the fence cannot be proven or an old worker can still supply admitted input, do not create a competing generation. Declare Unrecoverable immediately with the actual attempt count. Otherwise allow at most three sequential attempts, bounded backoff, per stage and cleanup deadlines, and an overall episode deadline. The third failure irrevocably closes hard recovery in this API process. No monitor, timer, queued caller, or actor may start attempt four.

Policy settings must be finite and validated at startup. Production deadlines need live qualification against real Databento behavior. Tests use controlled time and injected failures.

## 6. Soft recovery infrastructure gate

After hard success the feed stays connected locally while downstream admission is fenced. A bounded local buffer may retain recent records. At capacity, discard with counters and rate limited warnings. Memory and replay obligations cannot grow without limit.

Qualify NATS, Redis, every required PostgreSQL logical database, and ScyllaDB concurrently. Each probe has its own exception boundary, identity, duration, result, and deadline. A failed probe does not cancel collection of the others. All mandatory probes must pass before supervisor reconciliation:

- NATS: connect, verify required JetStream capability, and publish/acknowledge or receive a unique bounded probe.
- Redis: authenticate and write, read, compare, then delete or expire a unique short lived key.
- PostgreSQL: connect to each required logical database, execute a query, verify canonical schema identity, and begin/roll back a transaction; add a scoped write probe where immediate writes require it.
- ScyllaDB: verify required keyspace and table availability and perform a bounded read; use a short lived write/read probe with runtime consistency policy where writes are required.

Probe artifacts are isolated from trading data. Per probe, round, backoff, and overall soft episode deadlines are finite. On exhaustion report exact failures, keep downstream fenced, enter DatabentoHealthyDownstreamDegraded, and leave healthy Databento running. A new soft episode needs an explicit bounded policy trigger or operator request. Downstream infrastructure failure does not itself request a Databento hard reset.

## 7. Supervisor reconciliation and admission

After infrastructure qualifies, ask the privileged supervisor context for bounded health reconciliation of all registered non supervisor actors and projectors. It is the sole lifecycle authority. It keeps healthy progressing instances, probes unknown health, applies policy to degraded instances, recycles only unhealthy or stopped instances it owns, and verifies recovery. Actor owned consumers are part of actor health. Projectors have readiness and progress evidence. Results include full actor identity: name, verb, entity ID, and aggregate healthy, degraded, critical, and unknown counts.

Require generation aware end to end progress through necessary publisher, actor, and projector boundaries. Only after all three soft gates pass may downstream admission open and state become Running. A failed gate remains fenced and yields a bounded soft result. It cannot restart healthy Databento.

## 8. Unrecoverable decision and fatal evidence

The process is Unrecoverable after three failed hard attempts, lost exclusive generation ownership, an unfenceable old worker, or lost bounded authoritative control in the hard coordinator. Transition atomically, reject new recovery, retain the downstream fence, and construct one immutable fatal report. Send exactly one direct in process shutdown request. This control path cannot use NATS, Redis, databases, actors, or hosted service messaging.

The report includes original trigger, operation/value date/generation IDs, actual attempt count and each result, final failed stage and exception, worker isolation evidence, cleanup failures, total elapsed time, intended nonzero exit code, and shutdown deadline. An immediate safety failure must not falsely claim three attempts occurred.

Independently attempt to emit the reason and summary to the configured application/system console logger, OpenTelemetry logging pipeline, and stdout. A failing sink cannot suppress another or exceed its logging budget. Record local emission outcomes when possible. Remote OpenTelemetry delivery cannot be guaranteed during an outage; stdout and stderr are local evidence channels. Duplicate fatal records from console logging and direct stdout are acceptable.

## 9. Final API shutdown

The direct shutdown coordinator atomically enters ShutdownRequested, rejects further recovery, emits fatal evidence, and signals host lifetime to stop. Normal hosted service shutdown gets one finite graceful deadline. Every step and the top level catch exceptions. The process exits with a designated nonzero unrecoverable recovery code even if graceful shutdown succeeds.

A separate deadline guard must remain able to terminate hung host shutdown. If graceful shutdown throws or reaches its deadline, write a minimal fatal reason, exception or timeout, and exit code directly to stderr, attempt only a bounded local flush, then force process exit with the same nonzero code. This final path cannot resolve dependency injection services or await telemetry export, NATS, Redis, databases, actors, or hosted services. A stderr failure is contained. There is no shutdown retry loop.

Normal API shutdown is used only after terminal classification. Hard recovery never borrows general API startup or shutdown methods as recovery primitives.

## 10. Bounded operation and observability

Expose state, operation/attempt/generation IDs, stage and duration, local Databento health, downstream fence, infrastructure results, supervisor counts, next bounded action, and terminal reason through operational health while the API lives. Never report Running on socket connectivity alone.

Configuration validates the fixed maximum of three hard attempts, stage and overall deadlines, cleanup time, backoff, soft probe and round budgets, buffer capacity, fatal log budget, graceful shutdown deadline, and nonzero exit code. Invalid policy is a startup configuration failure; hard recovery never loads replacement policy remotely.

## 11. Verification gates

1. Architecture checks show no hard call path to database reconciliation, NATS/Redis/ScyllaDB, status publication, actor lifecycle, or composite API startup/shutdown.
2. Inject exception and timeout at each hard stage. Safe failures permit attempts two and three with new resources; original and cleanup errors remain visible.
3. Blocked NATS send and publisher stop failure cannot delay or poison hard recovery.
4. Old callbacks, pipes, workers, and queued data fail generation admission. An unfenceable generation causes immediate terminal classification.
5. Simultaneous requests produce one episode. Three failed attempts produce one shutdown request; attempt four is impossible via late timers or callers.
6. Local hard qualification works while NATS, Redis, PostgreSQL, and ScyllaDB are unavailable; test trading and quiet session policies.
7. Infrastructure probes run concurrently, collect independent failures, terminate within budget, and leave Databento running when soft recovery degrades.
8. Supervisor preserves healthy actors/projectors, recovers only unhealthy registered components, and reports full identities and counts.
9. Downstream admission opens only after infrastructure, supervisor, and generation aware end to end qualification.
10. Fatal logging independently attempts console, OpenTelemetry, and stdout. Logger failure cannot stop shutdown.
11. Child API process tests prove nonzero exit after graceful shutdown, direct stderr evidence after exception, forced exit after hung hosted service, and no surviving runaway process.
12. Live supervised qualification during trading hours retains timings and logs before automatic terminal shutdown is enabled in production.

## 12. Implementation stages

1. Add immutable recovery results, states, generation IDs, and a single flight coordinator.
2. Extract a Databento only hard runtime using frozen in memory manifests; remove database and downstream publication from its critical path.
3. Add generation fencing, per attempt ownership, bounded cleanup, local qualification, and three attempt policy.
4. Add direct fatal report and shutdown request, independent log outputs, graceful deadline, and stderr/nonzero exit guard.
5. Add concurrent functional infrastructure qualification and bounded downstream buffering.
6. Add supervisor owned health reconciliation and generation aware end to end admission.
7. Complete fault injection, integration, child process shutdown, and live supervised qualification.

Implementation is incomplete until every stage, including final API shutdown, passes its failure verification gate.
