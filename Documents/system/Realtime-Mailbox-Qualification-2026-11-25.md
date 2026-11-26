# Realtime mailbox qualification — 25 November 2026

Activation remains disabled. Component correctness and finite-retention checks pass; this is not a completed full-session or universal two-second qualification.

Superseded latency follow-up: see [the final remediation report](Realtime-Mailbox-Latency-Remediation-2026-11-25.md). Workflow cold startup, strict termination, Memory cold and both warm/burst runs now pass in their declared fixtures; File cold creation remains failed at 5.77 s maximum. The earlier measurements below are retained historical evidence, not the latest results. Activation and complete-session qualification remain gated.

## Matched load results

Final source-to-handler p99 measurements on disposable NATS, with the real scheduler and a deterministic CPU-only owner:

| Workload | Memory | File | Two-second handoff gate |
| --- | ---: | ---: | --- |
| 100 sources/sec for 30 seconds | 45.94 ms | 42.95 ms | Passed for this short warm workload |
| Cold creation of 1,000 source identities / 3,000 recipients | 16.88 s | 19.80 s | Failed |
| Burst of 4,000 sources / 12,000 effects | 8.03 s | 8.33 s | Failed |

Every source's original bytes, ordering and final deterministic owner hash match across Memory/File. Main runs have 24,000 effects each; replaying 192 completed sources creates zero duplicate effects. A held mailbox, finite source-budget refusal, unrelated mailbox progress and drain/resume all pass. These checks do not qualify production financial owners or complete exchange sessions.

The burst p99 improved from 10.93 → 8.03 seconds for Memory and 11.95 → 8.33 seconds for File. Burst managed allocations fell from 625.24 → 344.13 MiB and 623.81 → 346.79 MiB respectively; each burst still recorded one Gen 2 collection. The warm workloads recorded zero Gen 2 collections. These are isolated probe totals, not live API LOH measurements. Full raw metrics and exact boundaries are in `.artifacts/realtime-mailboxes/20261125-qualification-v3/load-results.md` and the `load-*-r4` directories.

The final BenchmarkDotNet ITI durable preparation/release rerun measured 20.65 → 20.31 ms for one command and 898.94 → 19.17 ms for a batch of 64, serial versus grouped durable preparation. These are whole-batch fsync-bound component measurements, not full-pipeline latency; five iterations give broad error intervals. Reports are retained in `.artifacts/realtime-mailboxes/20261125-qualification-v3/bdn-final`.

All 16 final workflow-boundary BenchmarkDotNet cases also completed:

| Output capture | Volatile reference mean | Durable final mean | Durable allocated bytes |
| --- | ---: | ---: | ---: |
| Regime | 0.055 ms | 35.377 ms | 83,216 |
| Selection | 0.036 ms | 48.991 ms | 598,872 |
| Composition | 0.039 ms | 64.363 ms | 2,931,832 |
| Frozen financial request | 0.006 ms | 19.483 ms | 74,664 |

The volatile receiver provides weaker ownership: these additional costs buy durable frozen output, not a performance improvement over volatile dispatch. The cases use saved real source sizes and controlled typed output/owner collaborators; they do not time actual strategy calculations or database financial commits. Raw reports include accepted-replay cases, error intervals and allocation metadata.

## Decision boundary

Prepared production calculations read published inputs, with no decision-time database/provider reads or retries. The actual Daily five-stage runner completed in 350.262 ms (323.973 ms with concurrent timeframes). A deliberately non-cooperative Market Condition calculation reached a fenced terminal result in 1,907.617 ms; completing its late response did not alter the terminal workflow. An internal 100 ms terminal reserve does not extend the authoritative two-second acceptance deadline.

The 27 focused cases and consolidated 317-case workflow/runtime regression passed. Current Weekly/Monthly production paths correctly stop after Selection with existing G19/G21 evidence rejections. Independent valid frozen Composition/Risk cases pass, but they do not prove a fully eligible options workflow. Cold Daily observations of 2,369.646 and 2,034.168 ms remain failed deadline evidence; a later faster cold observation does not erase them. Startup preparation and eligible options coverage remain open gates.

Raw detail: `.artifacts/realtime-mailboxes/20261125-execution-v2/workflow-two-second-qualification.md` and `workflow-v3-qualification-results.json`.

## Retention and recovery

New ownership refuses when a finite budget is exhausted; already owned work retains completion headroom. There is no TTL deletion of required evidence. Tests cover pinned source frames, completed active-segment reclamation, cold over-cap refusal, concurrent reservations, exact receipt saturation, collision-safe cold receipt lookup, drain/resume and restart. One test writes and settles 20,000 actual source receipts rather than pre-seeding synthetic receipt bytes.

The source receipt lookup now rebuilds a derived on-disk index from authoritative receipts: an 8 MiB bucket table plus 48 bytes per receipt. These bytes are additional to receipt/source budgets and must be included in deployment disk sizing. It does not load one managed object per retained receipt. Receipt bytes and original proof formats remain unchanged. Capture-only archives retain their original source bodies and must not be opened by actor mailboxes.

Verified suites (overlapping; do not sum): native 154 passed / 13 intentional exclusions; shared actors 301/301; ITI/TDI 418/418; Futures/FuturesOption routing 32/32; final NATS 185/185 plus the separately run 20,000-receipt retention case; native capture ingress 23/23; API/DI 5/5; session tools 11/11. All reported tests have zero failures. Native source-prefix capture explicitly does not prove mapper or RequiredCaptureIntent checkpoint ownership.

## Complete-session qualification

The independent Development-only recorder is implemented and disabled by default. It archives original native protocol-2 members, managed Realtime messages and actual JetStream account observations before forwarding. Recording uses exact original bytes, origin, generation, trace and frozen recipient manifests. Its flushed archive receipt proves source ownership, not successful business-handler execution. Recording failures propagate rather than producing a false success. Prefix export fixes committed append-only lengths under a short fence and copies/hashes outside that fence; it does not freeze financial owners.

Two jobs are prepared for Nov 30 and Dec 1, including EOD and the following 6 p.m. ET rollover. They remain **PreparedNotArmed**, with owner, clock and source-floor hashes null until actually acquired. Memory and File must restore the same initial owner snapshot independently and carry session-one state into session two. Overlapping capture envelopes must deduplicate original identities.

Arming currently fails for concrete reasons: the unchanged live API reports a Daily ES20261218 source gap (352759 → 352761, missing 352760) and readiness 503; the sampled legacy PG account mirror and candidate emulator head do not establish a positive matching revision; no authoritative matching owner/source-clock snapshot has been acquired; protected capture credentials and full-session capacity qualification are absent. The candidate emulator head's effective use is unproved. Queue emptiness, a native forwarding pause, SourceSequence zero or a JSON declaration cannot substitute for that evidence.

Final read-only preflight at 2:22 p.m. ET confirmed those blockers again; native readiness was 200. Evidence: `.artifacts/realtime-mailboxes/20261125-qualification-v3/final-session-preflight.json`. Preflight exited blocked and started no capture. The final full API/Core/benchmark build passed with zero warnings/errors, and `git diff --check` found no whitespace errors.

The source recorder and snapshot/export tools are implemented; a complete isolated production-owner session runner, a demonstrated clean matching baseline/restore and two actual complete tapes are still outstanding. Neither a deterministic load-test owner nor a Core observer is a replacement. No recorder, watcher, native pause, live restart, financial order or activation was started.

Prepared job: `scripts/RealtimeMailboxSessions/nov30-dec1.capture-job.json`. Detailed setup and remaining prerequisites: `Documents/system/Realtime-Mailbox-Session-Capture-Setup-2026-11-25.md`.
