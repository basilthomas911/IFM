# Event-log marker batching: fault and concurrency qualification

## Decision

The next isolated experiment passed: **36/36 final checks**, comprising six scenarios, three repetitions, and two writer variants. The existing per-row marker writer and the batched candidate both preserved the tested durability and retry invariants.

This is a correctness qualification, not another throughput measurement. It does not change the earlier measured speedups, authorize production activation, or justify removing durable command auditing. No production source, settings, table, index, trigger, or running IFM service was changed in this experiment.

## Experiment

Both variants use the actual BinaryCopyEventLogAppender, compressed MessagePack payloads, command auditing, and eight material events per command. Each has a newly created disposable database with the original four event-log indexes and financial fence installed; event_log is renamed to event_log_v2 in both fixtures to hold table naming constant. Only the candidate enables the existing internal benchmark marker-batching switch.

The dedicated PostgreSQL 17.2 container is bound to loopback port 25432. fsync, synchronous_commit, and full_page_writes remain on. The harness rejects the usual application port, remote servers, non-admin database selection, and any server containing another non-template database. Only generated, owned database names reach CREATE/DROP operations.

| Scenario | Deterministic injection | Required result |
|---|---|---|
| Competing stream versions | Two independent appender connections write the same expected version while a third connection holds the stream row lock; both lock waits must be observed | Exactly one command commits; the loser gets ConcurrencyException; no losing command audit or partial events |
| Duplicate command | Two independent appender connections submit the identical audited command while blocked on the stream/audit locks | One commits; the other gets CommandAuditDuplicateException; retry through a fresh writer remains rejected |
| Concurrent checkpoint advance | Seed eight committed events; block the next append in a test-only marker trigger; advance checkpoint from 0 to 8, then release | All eight new events retain Processing/ApplyProjection markers; no skipped new event |
| Pre-commit connection loss | Observe the writer waiting in the marker trigger, then terminate only its uniquely tagged backend in the owned database | Npgsql connection failure; zero durable events, markers, audit records and stream-version advancement; retry commits once |
| Cancellation after admission | Cancel the caller token while the admitted append waits in the marker trigger, then release | The admitted append reports successful durable completion, consistent with the writer contract; no misleading cancellation result |
| Lost COMMIT acknowledgment | A loopback TCP proxy consumes the server's CommandComplete(COMMIT) frame and closes the connection without forwarding it | EventLogCommitOutcomeUnknownException; authoritative database reads prove complete commit; a fresh writer rejects the same command without duplicating it |

The acknowledgment proxy parses PostgreSQL backend message frames, not arbitrary payload contents. Encryption negotiation is disabled only for this isolated proxy connection. The COMMIT completion tag is interpreted using PostgreSQL's documented [message format](https://www.postgresql.org/docs/17/protocol-message-formats.html). A separate direct database read establishes the durable outcome.

Every applicable scenario checks counts, contiguous stream versions, marker-to-event identities, command-audit linkage, and stream counters before/after retries. The final suite also deserializes persisted event payloads and checks order, values, aggregate identity, command identity, and payload content. Sequence-number gaps after rollback are permitted; stream-version gaps are not.

Checkpoint advancement only covers already committed events. The test deliberately does not fabricate a projector checkpoint that claims uncommitted future events have been processed. Existing checkpoint-equal/ahead and marker-conflict semantic checks remain in the earlier benchmark suite.

## Evidence

- Release build: zero warnings and zero errors.
- Initial run: 36/36 passed.
- Final run with additional payload replay, exact marker-count, and audit-link assertions: **36/36 passed**.
- [Final results and case details](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-qualification-final-20260919/results.json)
- [Final environment and harness hash](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-qualification-final-20260919/metadata.json)
- [Generated summary](../../BenchmarkDotNet.Artifacts/event-log-v2/marker-qualification-final-20260919/summary.md)
- [Earlier performance measurements](Event-Log-Marker-Batching-Experiment-2026-09-19.md)

Successful disposable databases were removed; only the postgres administration database remains on the benchmark instance. No application event-log data was removed. Raw results remain on disk.

## What this establishes—and what it does not

The batched marker statement passed the same tested transaction-failure and contention conditions as the original writer. In particular, removing per-event marker round trips did not cause partial durable writes or unsafe duplicate retries in this suite.

The lost-ack result reinforces why in-memory command deduplication alone is insufficient: a fresh writer needs durable evidence to distinguish an already committed command from an uncommitted attempt. Do not replace that evidence with a volatile cache. Normal application handling must reconcile unknown commit outcomes by command identity; the harness's intentional duplicate retry verifies the storage safety net, not the application's complete reconciliation workflow.

Remaining promotion gates:

1. Independent application-process contention and abrupt process death, plus full database restart/crash recovery. Independent connections and pg_terminate_backend are not equivalent to those tests.
2. Longer representative workloads with mixed marker density, queue pressure, and production-sized retained data; include server CPU, I/O and wait sampling.
3. End-to-end projector/checkpoint lifecycle under actor execution. This suite advances a checkpoint directly and checks append semantics, not the entire projector workflow.
4. Controlled activation and rollback switch; production remains on the original path.
5. Benchmark sparse and single-marker batches before deciding whether to keep the per-row statement for the single-marker case.

Recommended next performance experiment: sparse/mixed marker density under sustained load, with the original four-index schema held constant. The earlier marker-heavy wins do not establish the benefit for the application's actual event mix. No schema migration is required to investigate or eventually activate marker batching.

Follow-up completed: the [mixed-density experiment](Event-Log-Mixed-Marker-Experiment-2026-09-19.md)
passed 108/108 samples, including sparse and burst workloads. It establishes benefits under those synthetic
patterns, not queue-saturation or a production-distribution soak; remaining process/crash and rollout gates still apply.

Subsequent follow-up: [process/restart qualification](Event-Log-Process-and-Restart-Qualification-2026-09-19.md)
now covers independent OS-process contention/death and whole-server graceful/SIGKILL recovery, with 30/30
passing cases. The 36-case suite described in this document was rerun successfully as regression verification.

## Reproduction

Use the isolated-container environment variables described in the [benchmark guide](Event-Log-V2-Benchmark-Harness-and-Verification.md). The dedicated server must be empty and running; do not point this at an application instance.

~~~powershell
dotnet build TomasAI.IFM.Framework.Storage.Benchmarks/TomasAI.IFM.Framework.Storage.Benchmarks.csproj -c Release --no-restore
dotnet TomasAI.IFM.Framework.Storage.Benchmarks/bin/Release/net10.0/TomasAI.IFM.Framework.Storage.Benchmarks.dll --event-log-marker-qualification --output=BenchmarkDotNet.Artifacts/event-log-v2/new-marker-qualification
~~~

The output directory must be new/empty. Each run creates fresh fixtures; successful fixtures are dropped and failed fixtures are retained for diagnosis. A subsequent run refuses to use a server with retained fixtures. Fault observation is time-bounded and must occur before the appender's SQL timeout; a failure to observe the intended barrier fails the case rather than counting an unrelated timeout as success.
