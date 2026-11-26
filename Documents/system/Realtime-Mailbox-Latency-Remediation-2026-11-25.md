# Realtime mailbox latency remediation — 25 November 2026

Activation remains disabled. Workflow startup/deadline checks and transport correctness pass. Cold creation of 3,000 unknown consumers is an explicit test-only stress workload, not the two-second decision-pipeline gate. Its raw above-two-second File result is preserved; full-session and rollout qualification remain open.

## Applied changes

- Prepare bounded workflow code, serialization, fingerprint and mapping infrastructure before readiness. The hosted lifecycle barrier completes before any hosted source starts, including concurrent host startup. Preparation invokes no business calculator, accepts no synthetic market observations and makes no financial commitments.
- Enforce a monotonic two-second budget for the complete local decision batch as well as the existing authoritative per-stage deadlines. Keep the 100 ms terminal reserve, interruption semantics, owner acceptance guards and late-result fencing.
- Recover mailbox identities from authoritative frozen pending source manifests, retaining legacy registry validation. Remove redundant per-mailbox registry fsyncs and prepare independent consumers concurrently with installation fenced by the original transport version.
- Group at most 64 original delivery envelopes into one bounded physical broker message for batched recipients. Preserve each original source identity, payload and handler proof. ACK the physical group only after all required member proofs are flushed. Keep one physical message in flight per mailbox.
- Allow independent mailbox ACKs to share a stream read fence; reset/recreation takes an exclusive writer fence and waits for existing readers. Preserve lock ordering and suppress stale-incarnation settlement.
- Use broker delivery metadata for pending counts, retaining INFO refresh after empty pulls, unknown metadata and legacy consumers with multiple outstanding ACKs. Preserve the publication-version lost-wakeup check.
- Maintain a derived per-mailbox ordering index and constant-time proof reservation counters. Remove index entries only after durable proof/full-source completion; reconstruct them from original files on recovery. No TTL deletion or proof-format change.
- Move full-source confirmation bookkeeping to its own bounded pump, enqueue up to 64 validated confirmations together, and re-drain the writer after the enqueue lock is released. Handler proof before broker ACK remains unchanged. Source bodies remain locally owned until the full-source receipt flushes.
- Prepare known realtime infrastructure code/control metadata and actual registered actor streams before source readiness. Create no fabricated entity identifiers, consumers or messages. The cold test still creates all 1,000 unknown entity identities and 3,000 consumers inside its timed phase.

Broker I/O slots increase from 16 to 64; the load fixture still has 16 actor CPU workers and one physical scheduler slot per mailbox. SharedInbox remains the reply default. Direct mode and 128-slot stress experiments were not promoted. Default operation recovers durable pending recipients and creates other consumers on demand; no new explicit required-consumer manifest/barrier is introduced.

## Correctness and decision timing

- Full NATS suite: **249/249 passed**, including the 20,000-receipt retention case, source confirmation restart/retry, partial fan-out, stream incarnation, metadata scheduling races and zero-consumer stream preparation.
- Workflow/runtime/recovery: **322/322 passed**. The obsolete test-only source-journal constructor lookup was corrected without an application behavior change.
- Latest simple setup startup/readiness: **5/5 passed**, including a source registered before preparation, concurrent startup and failed preparation preventing source startup.
- Latest strict close-progress recovery against the optimized outbox: **4/4 passed**.
- Three fresh-process first Daily traversals completed all five stages in **858.375 / 975.137 / 1,039.570 ms**, with the expected authenticated NoTrade from the fixture's empty Fund book. Startup preparation took **5.369 / 4.515 / 4.802 seconds before readiness**.
- A prepared first Weekly workflow with a non-cooperative Market Condition provider became terminal in **1,915.399 ms**. The late result did not alter terminal state or produce an approval.

These are actual production calculation/runtime paths on prepared fixture inputs. They do not qualify an approved financial commit or every eligible options path. Existing options G19/G21 semantics remain unchanged. The runtime test host and full production API startup have different scope; first-traversal evidence is not a claim that every model helper was process-cold.

Raw workflow evidence: `.artifacts/realtime-mailboxes/20261125-execution-v2/workflow-cold-start-qualification.md`. Final simple-setup test logs are under `.artifacts/realtime-mailboxes/20261125-simple-qualification`; earlier transport correctness log: `.artifacts/realtime-mailboxes/20261125-safety/destination-c-startup-preparation-full-nats-including20k.log`.

## Measurement boundaries and separate qualification gates

The workload keeps the original event bytes, origins, generation, three recipients and ordering oracle. It tests 1,000 cold identities, 100 sources/sec for 30 seconds, a 4,000-source burst, 192 completed-source retries and capacity-hold/unrelated-progress/drain/resume. Total batch duration and per-source handoff latency are distinct. A sub-two-second p99 alone does not pass a maximum-two-second requirement.

Original results used Workstation GC and a broker writable container layer. The API explicitly uses Server GC; development NATS mounts its data on a persistent `/data` volume. The final comparison therefore reruns both the preserved executable and the new executable with Server GC and fresh disposable `/data` volumes. Original/intermediate results are retained and must not be mixed with that comparison. The preserved executable uses its original 16 broker slots; the optimized default uses 64. Input hashes and final deterministic owner witnesses must match across all four runs.

The 3,000-consumer cold diagnostic is explicit test-only stress and outside the two-second prepared-decision gate. It is not full qualification. Known stream/code preparation before readiness is reported separately; it does not pre-create the unknown consumers or move their timer boundary. Raw File cold stress diagnostics still exceeded two seconds, including after matching the broker volume layout; this is retained as stress-capacity evidence. No volatile-consumer substitution, skipped fsync, relaxed limit or pre-provisioning of the test's unknown identities was used to hide that failure.

Two complete session tapes, matching authoritative owner/source-clock snapshots, actual production-owner replay/resource reconciliation and controlled rollout remain separate open gates. The Nov 30 / Dec 1 jobs remain PreparedNotArmed. No live profile restart, transport activation, source repair, capture watcher, real order or paid data request was performed.

## Final matched load rerun

Both executables ran serially with Server GC, fresh disposable NATS 2.12.0 `/data` volumes and local source journals under the same C: workspace. The preserved before executable uses its original 16 broker I/O slots; after uses 64. Actor CPU workers remain 16. No Direct reply mode, volatile File consumer substitution, skipped flush or preprovisioning of unknown identities was used. Startup code/metadata and actual registered actor streams are prepared before readiness; all 3,000 unknown consumers remain inside the cold timer.

| Workload | Memory max before → after | File max before → after |
| --- | ---: | ---: |
| Stress only: cold 1,000 identities / 3,000 recipients | 16272.92 → 1842.38 ms | 17979.82 → 5771.67 ms |
| 100 sources/sec, 30 seconds | 90.12 → 41.77 ms | 124.04 → 55.06 ms |
| 4,000-source burst / 12,000 effects | 7383.17 → 608.60 ms | 6148.63 → 551.80 ms |

All four runs produced the same exact owner witness and original input hashes. Each main run has 24,000 effects; completed-source retries produce zero additional effects; saturation retains refused input, permits unrelated progress and resumes correctly. These are transport/scheduler measurements with a deterministic CPU-only owner, not production financial-owner or complete-session qualification. Raw phase JSON includes p50/p95/p99, maximum, over-budget counts, startup, managed allocations, Gen 2, aggregate control waits and durable writer flushes. Aggregate waits overlap across tasks and are not additive wall time.

**Bulk cold consumer creation is stress only, not the two-second decision-pipeline gate.** Its raw File maximum still exceeds two seconds and is not hidden. No explicit required-consumer barrier is being added. Full-session resource budgets, matching owner snapshots and session reconciliation remain open; activation remains disabled.


## Actual live footprint observed after rollover

A read-only sample at 2026-11-25 23:45 UTC found 217 registered/running actors. The **25 Realtime actors retained 869 resident mailbox identities**, all admission-open/Running, with **zero processing and zero queued** at the observation. This is not 869 active trades, required consumers or a startup/rollover peak. The running configuration retains 128 idle mailboxes per actor; newly-idle high-cardinality queues beyond that bound are retired and disappear from metrics. Five quote/order fanout actor types each retained 128 older identities. The exact identity/per-actor snapshot and date/last-receive analysis are preserved in `.artifacts/realtime-mailboxes/20261125-safety/live-realtime-mailbox-identities.json` and `live-realtime-mailbox-counts-analysis.json`.

The existing native read-only observation RPC at 23:52 UTC reported trading date **Nov 26**, two primary datasets and three exact futures contracts: GLBX.MDP3/ES20261218, XCBF.PITCH/VX20261216 and VX20270120. It also reported 12 ready option scopes, but the 12 option-chain publication records and empty rotating option-observation window do not establish a complete option-contract or mailbox manifest. Source evidence is `live-native-source-footprint-summary.json`; raw original reply bytes are retained beside it.

The profile started on Nov 24 before this audit. **Actual historical startup counts and the 6 p.m. before/after rollover peak were not captured**; the post-rollover resident sample cannot reconstruct them. No live restart, activation or feed mutation was performed to manufacture such evidence. Current readiness 503 reports an unresolved ITI source failure and missing ES Daily projection after rollover; this unactivated build is not claimed to have caused or repaired those live failures.

The full qualification harness still includes its cold phase when explicitly invoked. No new opt-in branch or default-off harness switch was added; the stress workload does not run in ordinary application startup.

Latest simple-setup first Daily process IDs were 162128/158932/180000. All five stages completed with the expected authenticated empty-Fund NoTrade; no financial approval is claimed. The explicit required-consumer/rollover barrier was reverted; prior code/serialization preparation and the whole-batch two-second cap remain. Activation stays disabled.
