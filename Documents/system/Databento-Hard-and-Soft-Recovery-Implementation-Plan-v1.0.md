# Databento Hard and Soft Recovery Implementation Plan v1.0

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


| Item | Value |
| --- | --- |
| Plan ID | DHR-IMP |
| Status | In progress; isolated foundations exist, runtime migration and qualification gates remain open |
| Date | 2026-09-30 |
| Design authority | [Hard and soft recovery design](Databento-Hard-and-Soft-Recovery-and-Terminal-Shutdown-Design-v1.0.md) |
| Scope | API hosted Databento workers, downstream qualification, supervisor reconciliation, terminal shutdown |
| Rollout | Isolated tests, synthetic worker, supervised Development, live Development, production readiness, production enablement |

## 1. Outcome and execution rules

Hard recovery must make Databento healthy at the local worker boundary within three isolated attempts. Soft recovery must qualify NATS, Redis, PostgreSQL, and ScyllaDB; use the special supervisor context to reconcile registered actors and projectors; and prove downstream progress before reopening admission. An unrecoverable hard result must cause one logged, bounded API shutdown with a nonzero exit code. No recovery or shutdown loop may run indefinitely.

Each numbered gate below has a deliverable, tests, and an exit condition. Complete gates in dependency order. Keep automatic terminal shutdown disabled until its child process tests and supervised live gate pass. Preserve unrelated worktree changes. Record implementation evidence and deviations in a separate implementation record. A passing unit test is not a substitute for the required process and live qualifications.

Current code is a migration source, not compliant behavior: `DatabentoMarketDataWatchdogService.RecoverAsync` performs three attempts but calls `RecordAsync` around the attempts and `StartAndQualifyAsync` calls `PrepareContractsAsync`; `SupervisedDatabentoLifecycleRuntime` reconciles contracts, starts/stops the host publisher, and retains `publisherStopFailed`; `RealtimePublicationRecoveryService` can request a new reset after a five second delay; `Program.cs` uses host shutdown and a final Serilog flush but has no dedicated fatal recovery coordinator. These are the primary seams to change.

## 2. Gate map

| Gate | Deliverable | Depends on | Runtime effect |
| --- | --- | --- | --- |
| DHR-00 | Baseline, inventory, test harness, acceptance record | Design | None |
| DHR-01 | Contracts, states, policy, single flight coordinator | DHR-00 | Shadow results only |
| DHR-02 | Frozen manifest and local qualification boundary | DHR-01 | Hard candidate path in tests |
| DHR-03 | Fence, owned worker teardown, isolated attempts | DHR-02 | Synthetic hard recovery |
| DHR-04 | Three attempt terminal decision and fatal report | DHR-03 | Terminal decision in tests |
| DHR-05 | API shutdown coordinator and process guard | DHR-04 | Child process tests only |
| DHR-06 | Infrastructure probes and bounded feed holding | DHR-03 | Soft qualification in tests |
| DHR-07 | Supervisor reconciliation and downstream admission | DHR-06 | Synthetic end to end recovery |
| DHR-08 | Caller migration, observation, operational health | DHR-05, DHR-07 | Development opt in |
| DHR-09 | Fault matrix and full integration qualification | DHR-08 | Development candidate |
| DHR-10 | Supervised live Development and rollout decision | DHR-09 | Development active |
| DHR-11 | Production readiness and enablement | DHR-10 | Separate reviewed production change |

## 3. Proposed ownership and contracts

Keep contracts in the Market Data application layer without introducing a dependency cycle. The API host owns only process lifetime and infrastructure composition. The Databento recovery coordinator owns episode state and hard attempts. Existing watchdogs and monitors remain detectors that submit typed requests. The special supervisor context alone owns actor and projector lifecycle changes.

Proposed contracts, with final names recorded in the implementation record: `HardRecoveryRequest` (reason, source, value date, expected generation, diagnostic snapshot), `RecoveryEpisodeResult`, `HardAttemptResult`, `DatabentoLocalQualification`, `InfrastructureQualificationResult`, `SupervisorReconciliationResult`, `FatalRecoveryReport`, `IHardRecoveryRuntime`, `ISoftRecoveryCoordinator`, and `IApiFatalShutdown`. Results are immutable and have typed outcomes. They include operation/attempt/generation IDs, monotonic durations, wall clock timestamps for logs, failed stage, primary exception summary, cleanup failures, and safety classification. Do not embed unbounded exception text or secrets in metrics labels or status responses.

The direct fatal shutdown interface belongs to the API host and is injected into the recovery coordinator. It cannot route through NATS, a database, Redis, an actor, or a hosted service message. The host must keep a raw stderr/stdout fallback reachable even when logging or dependency injection is failing.

## 4. DHR-00: baseline and harness

Inventory every hard reset caller, watchdog transition, publisher stop/start, worker ownership path, contract reconciliation call, recovery observation write, supervisor API, operational health output, and `Program.cs` shutdown path. Capture the current dependency graph and baseline timing for a successful live/synthetic worker replacement. Characterize current failure cases: PostgreSQL timeout before worker qualification, NATS noncooperative send, publisher stop failure, simultaneous reset callers, failed third attempt, and actor restart after reset.

Create test doubles for Databento connection/subscription/local records, exact worker process ownership, generation admission, monotonic time, each infrastructure probe, the supervisor, and fatal shutdown. The harness injects throw, timeout, cancellation, and delayed completion at every stage. Use a child API process for termination tests rather than executing process exit inside a test runner.

**Exit:** inventory and failing characterization tests are retained; no existing production behavior changes.

## 5. DHR-01: state, policy, and single flight

Introduce explicit episode states and allowed transitions. Implement one atomic process local active episode with a shared completion task. Accept the first request, freeze its immutable inputs, and bound the list of later contributing reasons. Late callers receive the active or already satisfied result. A caller cancellation cancels only that caller's wait unless host shutdown has begun. The accepted episode has its own bounded lifetime token. Once Unrecoverable is committed, all callers and monitors receive a terminal result and no future episode can start in that process.

Configuration validation includes positive finite stage, attempt, cleanup, episode, soft probe, soft round, logging, and shutdown deadlines; bounded buffer capacity and backoff; fixed three hard attempts; and nonzero fatal exit code. The hard controller uses a frozen validated options object and monotonic `TimeProvider` timing. Reject malformed policy at startup. Set values through a dedicated options section, not magic delays in individual callers. Determine production numbers using DHR-00 measurements and DHR-10 live qualification; tests assert finiteness and relationships rather than inventing unqualified production timings.

**Tests:** request race, duplicate cause collection, stale generation, caller cancellation, host cancellation, terminal rejection, state transition legality, zero/negative/unbounded configuration rejection.

**Exit:** coordinator can run in shadow mode and returns deterministic immutable results with no duplicate episode.

## 6. DHR-02: frozen manifests and local Databento proof

Make the existing `DatasetDesiredSubscriptionRegistry` expose a coherent immutable snapshot of value date, required dataset set, manifest revisions, subscriptions, and capture time. Publish it after normal startup/rollover reconciliation has succeeded. A hard attempt validates this snapshot without calling `DatabentoContractAuthority.ReconcileAsync`, securities catalog, or PostgreSQL. An unavailable or wrong date snapshot becomes a precise attempt failure; scheduled rollover remains a distinct operation.

Extract a local worker probe whose evidence comes from worker process, native connection/authentication, subscription acknowledgements, heartbeat, pipe records, and terminal status. Define required datasets by frozen snapshot, not whatever happened to start. In both live and quiet sessions require fresh Databento provider traffic or a heartbeat, accepted subscriptions, responsive workers, and drained local buffers. A trade or quote may not arrive during the recovery deadline and must not be required. Bind every observation to its candidate generation so old records cannot qualify a new attempt.

**Tests:** missing/stale manifest, wrong value date, missing required dataset, partial subscription acknowledgement, old generation record, live no records, quiet valid heartbeat, quiet stale heartbeat, worker alive but pipe stalled, and database unavailable while local qualification succeeds.

**Exit:** a standalone candidate qualifies Databento locally with no database or downstream publisher call.

## 7. DHR-03: exclusive generations and clean attempts

Place generation checks at the earliest host pipe ingress, cache/aggregation admission, and publisher ingress boundaries. Fence the old generation before any teardown. Detach the old publisher session without awaiting NATS and quarantine its outstanding task/resources for bounded later cleanup. Split `SupervisedDatabentoLifecycleRuntime` so Databento worker ownership is independent of host publisher lifecycle. Remove `publisherStopFailed` as a global barrier to the next local worker attempt. A stopped or stuck publisher may remain quarantined, but cannot regain admission.

Stop exact owned worker process trees within a short deadline, force kill survivors, verify process exit and pipe closure, and only then launch a candidate. Every attempt receives fresh worker IDs, generation, pipes, native sessions, cancellation source, and local state. On a safe failed attempt, fence and clean it under a separate deadline; cleanup exceptions are evidence and do not suppress the next attempt. If an old process or admission route cannot be isolated, classify immediately as unsafe terminal failure and do not launch a competing worker.

**Tests:** force killed worker, kill failure, delayed old pipe callback, queued stale tick, noncooperative NATS send, publisher stop exception, failed cleanup followed by successful second attempt, and all three attempts using distinct resources.

**Exit:** synthetic worker recovery maintains one authoritative generation and second/third attempts survive prior safe failure.

## 8. DHR-04: finite attempts and terminal report

Move the three attempt loop out of watchdog observation and into the episode coordinator. Capture every stage exception as a result, preserve cleanup errors separately, and apply bounded backoff only between safe attempts. An overall episode deadline cannot silently cancel the final result. On attempt three failure, atomically enter Unrecoverable and reject all later requests. On loss of generation isolation, enter Unrecoverable immediately and report the actual attempt count. Successful local qualification ends hard recovery and clears the consecutive failure count for a later independent incident.

Build an immutable `FatalRecoveryReport` with trigger, operation/value date/generation IDs, each attempt and failed stage, primary and cleanup errors, worker PIDs/isolation evidence, elapsed time, exit code, and shutdown deadline. Redact Databento credential and connection secrets at report construction. Keep a small bounded textual fallback available if allocation or structured formatting fails.

**Tests:** first/second attempt success, exactly three failed attempts, terminal safety failure on attempt one, deadline exhaustion, cleanup exception, late request, repeated monitor tick, no fourth attempt, and exactly one fatal report.

**Exit:** a deterministic terminal decision exists without yet stopping the process.

## 9. DHR-05: fatal API shutdown

Add an API host owned `IApiFatalShutdown` implementation with an atomic one shot request. It receives the immutable report directly from DHR-04. Independently attempt structured system/console logging, OpenTelemetry logger submission, and direct stdout; place a bounded budget on each. A remote exporter acknowledgement is never required. Then signal `IHostApplicationLifetime.StopApplication()` and allow hosted service shutdown within a fixed deadline. Ensure the top level `Program.cs` lifetime path returns the designated nonzero code even after successful graceful stop.

The guard must be outside any hosted service that can hang. If `WaitForShutdownAsync`, supervisor actor shutdown, a worker stop, Serilog flush, or another finalizer throws or exceeds the deadline, catch it, write the fatal reason and error/timeout directly to stderr, attempt only a bounded local flush, and force process exit with the same code. Protect the fallback from its own exceptions. Reject new recovery requests as soon as terminal mode is entered. Ordinary operator or scheduled shutdown retains its existing exit semantics.

**Tests:** child API process with normal graceful stop; injected host stop exception; hung hosted service; logging sink exception; OpenTelemetry exporter unavailable; stdout/stderr capture; duplicate fatal requests. Assert exactly one shutdown request, nonzero exit, bounded elapsed time, complete fatal identity, and no surviving child worker tree.

**Exit:** terminal mode is proven in isolated child processes; automatic terminal shutdown remains disabled in live configuration.

## 10. DHR-06: soft infrastructure gate and bounded holding

Create one interface per functional probe and a coordinator that runs NATS, Redis, all required PostgreSQL logical databases, and ScyllaDB concurrently. Each probe owns its deadline and catches exceptions. Collect all results before deciding the round outcome. NATS must prove required JetStream/publish acknowledgement or consume behavior. Redis uses an isolated expiring write/read probe. PostgreSQL validates connection, schema identity, query, and transaction capability in each required database, plus scoped write capability where necessary. Scylla validates keyspace/table and bounded read/write capability using the configured consistency behavior. Probe artifacts cannot collide with trading data.

Use finite round count, bounded backoff, and one overall soft deadline. Define how a qualified local feed holds only a fixed number of records before counting and dropping excess. Never allow local memory to grow with the outage. On exhaustion enter DatabentoHealthyDownstreamDegraded, keep the publication gate closed, and expose exact failed probes. New soft episodes require an explicit bounded policy trigger or operator request. A probe failure cannot call hard reset.

**Tests:** one and multiple failures, slow/hung probe, concurrent result collection, probe cleanup failure, database schema mismatch, all services unavailable, holding capacity reached, and locally healthy Databento preserved.

**Exit:** all four infrastructure families qualify before any actor recovery is attempted.

## 11. DHR-07: supervisor reconciliation and downstream proof

Add one privileged supervisor operation that inspects all registered non supervisor actors and projectors using existing actor/thread health, consumer readiness, projector backlog/progress, and full actor identity. Keep healthy progressing components; probe unknown; apply policy to degraded; recycle only unhealthy/stopped owned components; verify each recovered component under a deadline. Return per component outcomes and aggregate healthy/degraded/critical/unknown counts. Do not add an unmanaged actor restart path in the recovery coordinator.

After supervisor qualification, verify NATS, actor, and projector progress with a generation correlated control-plane canary for each required dataset, and verify a separate isolated Scylla write/read. If a candidate trade or quote is already held, also prove its exact generation through the real tick-storage actor and durable write. Do not wait for the market to produce a trade or quote. Open publication admission atomically only when the infrastructure, supervisor, and available end to end gates pass. On a later gate failure, retain or restore the fence and return a bounded soft result. A healthy Databento generation is not recycled for NATS, Redis, database, actor, or projector failure.

**Tests:** mixed healthy/unhealthy actors, consumer unavailable, projector stalled, supervisor timeout, supervisor exception, recovered component still unhealthy, old generation downstream acknowledgement, and successful new generation end to end flow.

**Exit:** synthetic soft recovery returns Running only after supervised components and downstream progress qualify.

## 12. DHR-08: caller migration and operational reporting

Change `RealtimePublicationRecoveryService`, `LivePipelineProbe`, the watchdog polling path, operator reset path, and startup failure path to submit typed requests to the same coordinator. Keep failure detection outside hard execution. Remove the publication monitor's independent five second reattempt loop; it should join the active episode and become passive after Unrecoverable. A caller may not infer recovery success merely because an async method returned without throwing: it must inspect the typed hard and soft result.

Separate `DatabentoMarketDataWatchdogService.RecordAsync` and status console publication from core hard recovery. Queue bounded optional observations after local hard success or attempt failure; database/status publication exceptions cannot change a core result. Make startup/rollover contract reconciliation occur before the frozen manifest is committed, not during hard reset. Ensure explicit scheduled stop and process shutdown cannot race a new hard attempt into starting workers.

Expose state, operation/attempt/generation IDs, current stage and elapsed time, Databento local health, downstream fence, infrastructure probe outcomes, supervisor counts, and terminal reason through operational health. Log transitions and failures once with correlation IDs; avoid per tick log amplification. Keep actual credential, subscription secrets, and unbounded exception text out of status responses and OpenTelemetry attributes.

**Tests:** each caller shares the same episode; result handling distinguishes local hard success from full soft success; optional reporting failures do not alter outcome; scheduled stop and shutdown precedence; operational health renders each state.

**Exit:** all reset entry points use one coordinator and return truthful phase results in Development opt in mode.

## 13. DHR-09: fault and integration qualification

Run the complete fault matrix against synthetic supervised workers and the full API composition. Inject throw, timeout, and late completion into fencing, publisher detachment, worker stop/kill, worker start, Databento connect, subscription, local record qualification, candidate admission, each infrastructure probe, supervisor reconciliation, downstream progress, logging, and shutdown. Confirm every safe individual hard failure permits a fresh next attempt; unsafe isolation failure goes terminal immediately. Assert no overlapping admitted generations, no worker leak, no fourth attempt, and no unbounded task or queue.

Run isolation tests with NATS, Redis, PostgreSQL, and ScyllaDB unavailable during hard recovery. Run soft tests where each dependency returns and one where it remains unavailable. Run the actor/projector integration suite through supervisor reconciliation. Capture duration, allocation, thread pool and lock contention, worker PIDs, source generation and downstream generation evidence, and log volume. Compare to DHR-00 baseline. Keep the existing market data and API integration suites green or record a specific unrelated failure with reproduction.

**Exit:** all architecture, fault injection, synthetic process, API integration, and child shutdown tests pass with retained evidence.

## 14. DHR-10: supervised live Development qualification

Run with operator observation during an active trading session. Record the frozen manifests and required datasets first. Exercise a controlled downstream NATS failure and confirm it does not make local Databento qualification depend on NATS. Exercise one controlled worker loss and verify a fresh generation; do not deliberately exhaust three live hard attempts on an active trading host. Verify infrastructure probes, supervisor per actor results, end to end progress, bounded record holding/drops, and restoration latency. Examine logs for secret leakage, duplicate episodes, extra worker trees, false Running, and unbounded retries.

Qualify the terminal path separately in an isolated API child process with synthetic workers and production shaped host shutdown. Preserve stdout, stderr, structured logs, OpenTelemetry local submission evidence, exit code, and elapsed time. The operator can stop the live qualification immediately; that stop has precedence over starting further attempts.

**Exit:** live hard and soft success is measured and accepted; isolated terminal process behavior is measured and accepted. Record any policy timing changes and rerun affected tests.

## 15. DHR-11: production readiness and enablement

Publish an as built design and implementation record containing exact code owners, validated deadline values, fault matrix results, live timing, shutdown exit code, health/status sample, and remaining limits. Verify service manager behavior on the nonzero exit code and ensure its restart policy cannot create an uncontrolled crash loop. Verify deployed stdout/stderr and OpenTelemetry collection retain the fatal reason. Rehearse turning off the new recovery coordinator in Development without losing worker containment.

Keep production automatic terminal shutdown disabled until the production configuration change has been reviewed against this evidence. Enable it in a separate deployment with monitoring of recovery episode count, local qualification latency, soft degradation, worker tree count, and API restarts. A regression rolls back the enablement/configuration, not the generation safety fence.

**Exit:** production qualification and explicit enablement are recorded. The plan is complete only after all gates pass and the as built record matches the running code.

## 16. Cross gate failure policy

| Failure | Required action |
| --- | --- |
| Core hard stage throws or times out but isolation is proven | Record failed stage, clean candidate within budget, start next attempt if fewer than three |
| Cleanup throws but generation is fenced and old workers cannot feed admission | Preserve primary and cleanup errors; continue next clean attempt |
| Old generation cannot be fenced or worker remains capable of admitted input | Immediate Unrecoverable; never launch a competing generation |
| Third hard attempt fails | One terminal report and one API shutdown request; reject attempt four |
| Fatal logger or OpenTelemetry path fails | Independently attempt remaining sinks and continue shutdown |
| Graceful API shutdown throws or hangs | Direct stderr fallback and forced nonzero process exit within deadline |
| Infrastructure, supervisor, or end to end soft gate fails | Keep downstream fenced, report Degraded after finite budget, keep locally healthy Databento running |
| API stop arrives during recovery | Fence admission, cancel work under bounded cleanup, follow host stop precedence |

## 17. Completion checklist

- Hard reset uses no PostgreSQL, NATS, Redis, ScyllaDB, or actor call before local Databento success.
- Exactly one coordinator owns every reset caller and at most one generation is authoritative.
- Three safe failed attempts or an immediate isolation failure lead to exactly one terminal shutdown.
- Fatal reason reaches independently attempted console logger, OpenTelemetry, and stdout; stderr covers shutdown exception/deadline.
- Graceful and forced paths exit nonzero within validated bounds.
- Soft recovery functionally qualifies all four infrastructure families before supervisor reconciliation.
- Supervisor retains healthy actors/projectors and recovers only unhealthy registered components.
- Downstream admission opens only after generation correlated end to end qualification.
- Every wait, retry, buffer, background monitor, and exit is bounded.
- Synthetic, child process, integration, supervised live, and production rollout evidence is retained.
