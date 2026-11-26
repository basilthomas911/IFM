# Realtime mailbox v2 execution results — 25 November 2026

Status: component implementation, correctness checks and serialized component measurements recorded. Full-path/session qualification and rollout remain blocked/pending. Development activation is disabled. The full plan is not complete.

Latest follow-up: [latency remediation and final matched rerun](Realtime-Mailbox-Latency-Remediation-2026-11-25.md) supersede the earlier short-load figures. Three first workflow traversals and strict blocked-provider termination pass; operational warm/burst workloads pass. Bulk 3,000-consumer cold creation is explicit test-only stress outside the two-second decision gate; its raw File maximum 5.77 s remains recorded. The full test harness still includes that phase when explicitly invoked; ordinary application startup does not run it. All four runs reconcile exact input/owner hashes and no duplicate effects. Full NATS verification passes 249 tests including 20,000-receipt retention. Startup and consumer/ACK changes are applied in source; no live profile was restarted or activated. The Nov 30/Dec 1 capture jobs remain unarmed, with owner/session/resource gates outstanding. Older measurements below are historical evidence.

## Implemented behavior

- Required realtime publications acquire bounded, flushed local source ownership before their producer advances its watermark. Frozen recipient manifests and original bytes, clocks, provenance and generation identifiers survive retry.
- Each recipient records handler-success evidence before physical JetStream acknowledgement. Source bodies retire only after all required recipient proofs exist. Partial fan-out and broker-stream recreation preserve outstanding ownership.
- Memory and File backends use per-thread durable consumers and finite stream/source/receipt limits. Saturation refuses admission; it does not evict required work. Display-only notifications retain their separate read-repair semantics.
- Ordered grouped source capture, managed ITI WAL preparation/release, native delivery and opted-in actor batching preserve source order. Generation replacement waits for owned work to drain. Missing original causal inputs fail closed.
- Workflow outputs and financial requests freeze before publication. Unknown outcomes reconcile with actual owners instead of repeating financial effects. Existing reference-based approval, expiry and session guards remain.
- Position lifecycle/control, frozen plan inputs, account convergence and monotonic account evidence have durable retry/recovery handling.
- Stream-incarnation fencing prevents a late ACK/NAK for a removed stream from settling a different message after recreation. Shutdown also cancels capture waiting for transport startup.

No live profile restart, transport cutover, paid download or production deployment was performed.

## Evidence and measurement boundaries

The immutable pre-edit baseline, source SHA-256 manifest and previous executable are retained at `.artifacts/realtime-mailboxes/20261125-execution-v2/baseline`.

The exact implementation and correctness evidence is in:

- `.artifacts/realtime-mailboxes/20261125-execution-v2/workflow-results.md`
- `.artifacts/realtime-mailboxes/20261125-safety/destination-v2-results.md`
- `.artifacts/realtime-mailboxes/20261125-safety/producer-v2-results.md`
- `.artifacts/realtime-mailboxes/20261125-execution-v2/managed-iti-batch-results.md`
- `.artifacts/realtime-mailboxes/20261125-execution-v2/native-required-capture-results.md`

Correctness includes actual PostgreSQL workflow/Fund receipts, six actual financial-owner round-trip/exit tests, strict lifecycle retries and broker Memory/File recovery. Component tests and synthetic receivers do not substitute for complete exchange sessions or full-process performance qualification.

### Initial serialized BenchmarkDotNet component results

These short jobs use .NET 10.0.10 / BenchmarkDotNet 0.15.8 on the development Threadripper 1950X host. They ran without competing agent builds/replays, but the existing development services remained running. Error intervals and job metadata are preserved in the raw reports. Do not infer precise production tail latency from five iterations.

| Boundary | Before mean | After mean | Interpretation |
| --- | ---: | ---: | --- |
| ITI durable preparation + release, one command | 24.53 ms | 23.75 ms | Similar single-command cost |
| ITI durable preparation + release, 64 commands | 929.08 ms | 19.41 ms | Grouped fsync, identical original command/release frames; roughly 48× lower batch time |
| Vertical plan processing, no exit request | 28.60 ms | 29.22 ms | Durable frozen request/output adds ownership; error intervals overlap |
| Vertical plan processing, exit request | 44.89 ms | 46.20 ms | Approximately 3% measured additional boundary cost; error intervals overlap |

ITI values are per whole batch. Destination values follow BenchmarkDotNet's declared operations normalization. Destination Function/financial collaborators are controlled substitutes; actual money invariants are tested separately.

Native disk-only measurements (not BenchmarkDotNet or complete transport): 4,096 entropy-bearing 512-byte sources took median 32,331 ms individually, 5,480 ms in groups of eight, and 2,048 ms in groups of 64 (127 / 747 / 2,000 sources/s).

Initial workflow output capture is an added durability boundary versus a weaker direct receiver. It is not a speedup. The first heavy composition sample allocated 8.2 MB and recorded a Gen 2 collection; safe buffer-copy reductions and final measurements are tracked separately below. No claim of lower live API LOH/Gen 2 is made.

### Final workflow boundary and regression evidence

The compatible journal writer removes intermediate full-body copies and immediate duplicate input decoding. Small and 200 KiB oracle tests compare complete pending/accepted/input files, including checksums, with the exact original writer. The final heavy composition capture allocated **3,794,064 bytes**, versus **8,222,360 bytes** before this allocation reduction (54% less); accepted replay allocated **1,219,608**, versus **1,627,536 bytes** (25% less). Capture mean was 74.73 ms versus 107.30 ms; those five-iteration fsync samples have wide error intervals and do not establish a precise latency speedup.

Final capture means against the weaker direct-receiver baseline: Regime 0.040 → 43.42 ms; Selection 0.027 → 49.50 ms; Composition 0.028 → 74.73 ms; frozen financial request 0.004 → 24.90 ms. These are ownership-boundary costs with synthetic typed outputs and a controlled receiver, not full strategy calculations, PostgreSQL commits or the complete two-second pipeline.

The complete latest Trade `RealtimeDelivery` suite passed **274/274**; focused workflow/runtime/actual-owner/byte-oracle tests passed **27/27**. The fresh API/Core/benchmark graph and NativeHost built with zero warnings/errors. Earlier evidence includes scheduler 297/297, managed ITI 250/250, producer admission 30/30, account 32/32, emulator 23/23, bars 34/34 and six actual financial-owner tests. These suites overlap and must not be summed as distinct test counts. Native library verification was 143 passed, 13 explicitly ignored infrastructure/timing cases, zero failed.

### Healthy backlog scheduling repair

The first transport sweep exposed a 250 ms failure-backoff delay being applied to healthy pending messages. The measured 64-source / 512-byte non-batched Memory case took **16.630 seconds**. The remaining pre-fix sweep was stopped after that measured case; it is not a completed 60-case result. Healthy backlog now reschedules promptly, while actual failures retain backoff and ordering. With the fix, the matching case took **573.48 ms**. Memory/File regressions use a five-second failure delay to prove healthy draining is independent of that delay and a failed head still waits before its successor.

The first complete post-backoff transport sweep executed all **60** cases: Count 1/64, payload 512/8,192/262,144 bytes, actor batching off/on, and Core / serial durable Memory/File / batched capture Memory/File. The source payload tape and receiver behavior are matched; stronger source and handler ownership is deliberately absent from Core.

For 64 sources with actor batching enabled, pre-codec-reduction means were:

| Payload | Core | Serial durable Memory | Batched durable Memory | Serial durable File | Batched durable File |
| --- | ---: | ---: | ---: | ---: | ---: |
| 512 bytes | 15.10 ms | 287.19 ms | 201.89 ms | 272.72 ms | 197.13 ms |
| 8 KiB | 14.99 ms | 279.18 ms | 225.59 ms | 283.35 ms | 284.18 ms |
| 256 KiB | 220.60 ms | 657.96 ms | 673.03 ms | 619.20 ms | 755.00 ms |

Batching did not improve every payload/backend case. Full tables/error intervals are retained under `.artifacts/realtime-mailboxes/20261125-execution-v2/bdn/transport-before-codec-results`; the partial defect reproduction is `bdn/transport-backoff-before-partial.log`.

The transport probe waits for handler/source completion using `Task.Delay(1)` polling. Windows timer granularity can dominate low-count samples; these are measured probe completion times, not high-resolution individual publication or strategy latency. MemoryDiagnoser bytes are not LOH bytes, and absent Gen 2 columns do not establish an absence of large allocations.

The final codec builds only a bounded metadata header before allocating one exact wire array, and removes the intermediate decoded body array while keeping Capture's independent defensive snapshot. Both 256/512 KiB wire oracles, post-decode caller mutation isolation, malformed envelopes, healthy backlog, retries, and incarnation races passed: **167/167 NATS tests**. No wire version, hash or public contract changed. A final full transport rerun and scoped allocation results are recorded below.

### Final transport and allocation measurements

The final optimized build also completed **all 60 BenchmarkDotNet cases**, with no failed/NA cases. For Count=64 / actor batching enabled:

| Payload | Core | Serial durable Memory | Batched durable Memory | Serial durable File | Batched durable File |
| --- | ---: | ---: | ---: | ---: | ---: |
| 512 bytes | 15.22 ms | 286.24 ms | 197.58 ms | 276.35 ms | 219.91 ms |
| 8 KiB | 21.80 ms | 284.90 ms | 217.22 ms | 274.58 ms | 232.85 ms |
| 256 KiB | 202.29 ms | 627.24 ms | 646.82 ms | 671.98 ms | 702.32 ms |

At 256 KiB / Count=64, batched Memory allocation fell from 134,147.34 KiB to 84,962.77 KiB; File from 134,165.27 KiB to 84,953.66 KiB. This is approximately 37% less allocation. Large-payload batching is not a demonstrated latency win over serial durable delivery. The final non-batched 512-byte serial Memory case was 593.33 ms, versus the defective 16.630-second baseline (the intermediate backoff-only build measured 573.48 ms).

Final raw tables/CSV/HTML are in `bdn/transport/BenchmarkDotNet.Artifacts/results`; the complete final log is `bdn/transport-codec-final.log`. `bdn/binary-hashes-codec-final.json` identifies the exact benchmark/runtime assemblies. Initial and intermediate reports remain separately retained.

Separate EventPipe probes warmed each mode, forced GC before measurement, then marked **512 sources in eight batches of 64**. Every trace has a verified matching start/stop marker and original PID/SHA-256. Exact total allocated bytes and Gen 2 counts come from runtime counter deltas; LOH below means **sampled Large AllocationTick bytes**, not an exact object census. The probes use the benchmark executable's GC configuration, not the live API's configuration.

| Payload / mode | Total allocation MiB, before → after | Sampled LOH MiB, before → after | Gen 2, before → after | Mean 64-source time, before → after |
| --- | ---: | ---: | ---: | ---: |
| 8 KiB / Memory batch | 51.58 → 39.07 | 0 sampled → 0 sampled | 0 → 0 | 266.62 → 254.30 ms |
| 8 KiB / File batch | 51.81 → 39.23 | 0 sampled → 0 sampled | 0 → 0 | 256.24 → 242.30 ms |
| 512 KiB / Memory batch | 2,081.74 → 1,318.52 | 2,048.83 → 1,286.22 | 32 → 21 | 1,329.28 → 1,329.16 ms |
| 512 KiB / File batch | 2,089.69 → 1,314.69 | 2,056.43 → 1,282.47 | 37 → 20 | 1,493.86 → 1,438.22 ms |

The 512 KiB stress is the configured per-source maximum. It deliberately drives large generic source groups; it is not a claim that the native 512 KiB aggregate wire protocol emits 64 maximum-sized records in one native batch. Durable Memory/File still allocate substantially more than the weaker Core path (Core allocated 3.23/5.63 MiB in its before/after 512 KiB reference probes, with no observed Gen 2 collections).

Fewer collections did not improve every resource measure. In the maximum-payload Memory probe, CPU increased 9,500 → 10,656 ms and GC pause total increased 45.913 → 51.642 ms; File CPU decreased 10,469 → 9,250 ms and pauses decreased 66.817 → 33.653 ms. Memory's peak process working set also did not fall (setup/attachment-inclusive peak 359.64 → 365.74 MiB), although ending working set fell. These single traced runs do not establish repeatable CPU/pause/tail improvements.

Machine-readable resource comparisons: `.artifacts/realtime-mailboxes/20261125-execution-v2/profiles/comparison.json`. Original scoped traces, exact counters, duration arrays, GC/LOH/contention analysis and before/after NATS container samples are under `profiles/before-codec-r2` and `profiles/after-codec`. NATS RAM/CPU/IO values are point samples, not continuous peaks or isolated workload IO deltas. The first two trace-harness attempts were rejected (redirected CLI attachment detection and EventSource Start/Stop opcode naming); the accepted traces use corrected marker parsing. No empty trace was treated as zero allocation.

Reproduction helper: `scripts/Benchmarks/Measure-RealtimeMailboxQualification.ps1`; parser: `scripts/Benchmarks/TraceMetrics`. The helper is restricted to the dedicated disposable broker on port 45222 and never targets the development broker.

All measurements above are component/probe evidence. They do not close sustained steady/burst/cardinality/retention qualification, the all-family fault matrix, the full two-second decision/handoff SLO, complete sessions, or live API GC/readiness/UI verification.

## Finite budgets and supported failure boundary

Starting budgets remain unqualified: 512 KiB source payload; 64-source protocol batch; 512 KiB encoded native batch / 576 KiB decoded/fetch bound; 4,096 pending sources; 512 MiB source outbox; 128 MiB per actor stream; 4,096 messages per mailbox; 16 delivery workers; 8 GiB per receipt archive. Metadata reservations, original source frames and per-recipient receipts count toward their relevant limits.

Required work has no time-only TTL or DiscardOld eviction. General historical receipt pruning needs an authoritative replay horizon; exhaustion fails closed. Delivery-receipt compaction can remove redundant completed-source proofs only after the full-source completion receipt exists.

Memory-broker restart recovery depends on intact local original-source and handler-proof evidence. This does not promise preservation of provider input that was never durably accepted, arbitrary local-disk loss, or disk-full followed by process death before any original evidence was written.

## Remaining qualification gates

The inventory did not locate two complete normal exchange sessions with matching initial owners/configuration and full realtime source/recipient evidence. Saved ES Nov 16/17 tick files are useful partial inputs; they omit the complete original multi-family arrival tape, books, VX/options/statistics/control events and matching owner snapshots.

VAL3 therefore cannot be honestly completed from the saved ES ticks. It needs acquisition of two complete authoritative session tapes and initial snapshots, a reproducible isolated full-session replay/reconciler, and four matching trials (each session under Memory and File). This includes overnight, cash open/close, EOD and rollover, with only one isolated financial path.

VAL2 also requires sustained representative-rate/cardinality qualification, capacity/retention headroom, full latency/resource tails and every applicable decision staying within two seconds or terminating safely by its deadline. Short transport/component probes establish neither the complete workload nor the whole-pipeline deadline.

VAL4 remains dependent on those gates. Controlled development activation must quiesce all affected producers, inventory/drain or migrate original pending work, switch an explicit epoch without Core/JetStream double execution, test rollback with pending work, then verify the VS Code profile/UI/readiness without weakening checks. No reset, purge or fabricated acceptance may replace this reconciliation.

## Offline restore, evidence and cutover implementation follow-up

The offline restore, evidence-export/comparison and cutover rehearsal tools are implemented and component verified independently of live activation. No registration/rollover barrier or other strategy-decision work is added.

- `scripts/RealtimeMailboxSessions/session_restore.py` performs an actual PostgreSQL custom-dump restore, Scylla schema/SSTable restore into new table UUIDs, automatic secondary-index rebuilding, and exact flushed owner-file copying. It accepts only fresh run-labelled isolated Docker targets and matching original image IDs. Five tests pass, including actual PostgreSQL/Scylla values and indexed queries. It emits `RestoredNotReplayed`, never a completed-session certificate.
- `scripts/RealtimeMailboxSessions/cutover_tool.py` validates stopped original journal ownership and a fully drained source/recipient inventory, rehearses an isolated configuration switch, restores the exact original bytes and verifies unchanged journals. Nine tests pass. Actual live quiescence, activation and rollback remain unqualified.
- `scripts/RealtimeMailboxSessions/outcome_evidence.py` exports actual PostgreSQL rows, paged Scylla trade/quote partitions, emulator checkpoint records and handler/source receipts into bounded canonical ledgers. It preserves exact decimals, original IDs/timestamps and opaque bytes, detects per-record differences, and refuses incomplete bundles. Thirteen tests pass including actual PostgreSQL/Scylla, a 0.00000001 cash change, quote UDT mutation, paging and cleanup failure. It never certifies a complete session by itself. Existing session-tool checks also pass11/11.

**Superseded by the historical replay implementation below.** At the earlier audit, the production-handler session runner was blocked and unimplemented. The existing EventLog qualification host forces synthetic providers, incompatible storage mappings and a candidate schema migration. It cannot be used unchanged for this replay. A dedicated restored-store host needs the original certified native registration/book/mapper checkpoint, a historical business clock with real monotonic deadlines, and complete original input/session evidence. Saved scalar snapshots and generated assertions cannot replace these inputs.

Detailed audit: `.artifacts/realtime-mailboxes/20261125-simple-qualification/production-handler-replay-feasibility.md`. Restore evidence: `.artifacts/realtime-mailboxes/20261125-simple-qualification/restore-tool-results.json`. The prepared capture jobs remain unarmed and all activation flags remain disabled.


## Historical host and original native checkpoint implementation

The missing implementation is now present in a dedicated Test-only host and explicit original-session runner; EventLogQualification retains its earlier separate meaning. See [Historical replay operation](../../scripts/RealtimeMailboxSessions/Historical-Replay.md).

The real native checkpoint captures private Futures/Options books, original registrations/catalogs, mapper and required-publication state, original wire/IDs, pricing/evidence work, raw/source sequence floors and the binary backend read snapshot under one opt-in native mutation fence. Import validates typed state and restores public queries without provider acquisition. It preserves original timestamps and denies readiness/publication proofs in read-only mode. Full-file prefix-copy SHA evidence is separately bound to the atomic source/raw sidecar. All restore directories are isolated from original owners.

The actual API host uses certified loopback stores and real owners; a historical business clock is separate from real monotonic deadlines. The archive records original physical native framing, option-cache and raw-storage inputs, exact backend exchanges, original origins and v4 admission times. The runner requires those originals, preserves source identities, verifies real ingress receipts and refuses unknown input types. Derived financial output is regenerated and must still be reconciled; no synthetic owner is substituted.

Verified so far: native177 passed/13 intentional skips, NATS250 passed, recorded-input/mirror29 passed, prefix-tool12 passed, actual PG/Scylla restore5 passed. The latest replay runner builds with zero warnings/errors. The latest actual child-host financial-owner acceptance passed, including actual recorder DI, real reservations/duplicates/composition/risk and drained graceful shutdown. Managed/native host48/48, actual native ABI, historical/timer/authority20/20, monitoring16/16 and latest NATS/archive252/252 checks passed. These finite checks do not constitute two complete original-session qualification.

Activation remains disabled. Future capture jobs remain unarmed; no live profile was restarted and no broker orders or paid provider downloads were made.

Final implementation detail and remaining session gates: [historical replay results](Realtime-Actor-JetStream-Mailbox-Historical-Replay-Results-2026-11-25.md). The previously missing host/native implementation is no longer listed as an unimplemented blocker.

Final full isolated MarketData regression: **957 passed, zero failed, five intentional skips**. Test broker defaults were corrected to require an explicit disposable loopback endpoint; the successful rerun used port45222. See the consolidated historical replay results for the initial-run audit and exact evidence. Owned fixtures and the disposable broker were removed afterward.
