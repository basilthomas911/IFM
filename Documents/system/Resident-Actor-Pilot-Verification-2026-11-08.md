# Resident actor pilots: implementation and verification

Date: 2026-11-08. Status: first-five implementation, tests, 30-minute soak, measurement rechecks and Development process restoration complete. Actual market-session acceptance is blocked by the existing server ValueDate/replay-window problem described below. The next indicator/function wave remains pending user review of these timings and resolution of that gate.

## Implemented scope

| Pilot | Durable reconstruction | Resident behavior |
|---|---|---|
| FuturesVwapSignalCommandActor | Latest FuturesVwapSignalUpdatedEvent and required suffix | Retains confirmed committed state for eligible updates; recovery invalidates and reloads. |
| MarketOutlookSnapshotCommandActor | Latest MarketOutlookSnapshotInsertedEvent and required suffix | Retains confirmed committed state, preserving revision/session/provenance validation. |
| FuturesTradePlanFunctionActor | Latest FuturesTradePlanUpdatedEvent and required suffix | Keeps serialized committed business state; restores a private state and prepares each new request. |
| VerticalSpreadTradePlanFunctionActor | Latest VerticalSpreadTradePlanUpdatedEvent and required suffix | Same completed-only behavior; retains full plan inputs and thresholds. |
| IronCondorTradePlanFunctionActor | Existing creation-plus-latest IronCondorTradePlanUpdatedEvent query and required suffix | The latest full plan contains all business state used by its reducer; private reconstruction for each request. |

PostgreSQL remains authoritative. Each new eligible Command waits for its own durable save before replying; the original three resident position actors keep their existing behavior. Function failures and unconfirmed commits are not reusable completed state. Required event/projection transactions, command auditing, expected versions, typed Complete/Fail replies and post-commit publication policies remain in force. Queries continue using projected read models.

The new Function base fences ownership through the request's asynchronous execution scope. Timed-out work cannot attach its late load or completion to a later request. A lease stays pinned until abandoned work settles; potentially dirty entries are invalidated. Completed no-write replay and no-event Command decisions validate the authoritative stream head with one indexed query. Cross-writer changes force authoritative recovery.

`ResidentActorPilots` has five independent switches, defaulting off, with a default maximum of 256 keys per actor and five-minute idle timeout. Capacity is a key-count bound, not a byte limit. Idle expiry is lazy on access/admission. Busy entries are pinned; admission falls back safely when all capacity is busy. Metrics expose hits, misses, evictions, invalidations, fallbacks, entries and pinned entries through `TomasAI.IFM.ResidentActors`.

## Test qualification

| Project / qualification | Final result |
|---|---:|
| Shared.UnitTests | 368 passed |
| Domain.Trade.UnitTests | 1,716 passed |
| Domain.Trade.BDDTests | 52 passed |
| Domain.MarketData.Analytics.UnitTests | 1,289 passed |
| Domain.MarketData.Analytics.BDDTests | 481 passed |
| Framework.Telemetry.UnitTests | 9 passed |
| Domain.MarketData.Analytics.IntegrationTests | 48 passed, 1 existing opt-in skip |
| LivePipeline.IntegrationTests | 320 passed, 4 existing opt-in skips |
| Domain.Trade.IntegratedTests | 164 passed, 3 existing opt-in skips |
| Domain.Trade.VerificationTests | 72 passed |
| Storage.IntegrationTests | Initial full run: 879 passed, 1 Scylla comparison timeout; isolated comparison rerun: 2 passed |
| New PostgreSQL pilot cases, included in Storage coverage | 50 passed again after final ownership-fencing changes |
| Fresh OS-process restart probe | 5 passed; committed versions 2 → child 3 → parent 4, no duplicate source events |
| Matched 64-stream fixture verification | All 5 pilot types passed |
| Command / Function / Event / Query / realtime convention audits | 65 / 28 / 31 / 63 / 25 actors passed |

The full Storage run and its clean focused reruns are separate results; there was no single all-green rerun of all 880 Storage cases. Eight opt-in tests remained skipped and are not counted as executed acceptance cases.

New coverage exercises full-state latest snapshot loading and suffix replay, request-local Function reconstruction, duplicates, no-op decisions, changed inputs, stream/configuration isolation, idle/capacity eviction, pinned fallback, failed persistence/atomic rollback, optimistic conflicts, external writers, real NATS request/reply restart, lost replies after commit, cancellation, late work and draining. PostgreSQL and NATS fixtures use disposable isolated infrastructure, not Development financial records.

Broader suites exposed stale test expectations unrelated to residency: actor-base inventory, E: child-process/repository-root resolution, registration reflection arity, an outdated frozen identity count, declared additive position wire defaults and a Risk fixture clock preceding its accepted composition. These expectations/fixtures were corrected without loosening production freshness checks or changing historical fixture bytes. The Scylla comparison timeout passed in isolation; no Scylla production change was made.

An intermediate PostgreSQL pilot rerun also failed fixture setup after credentials were added to the provider-bound base connection string. The fixture binding was corrected, with credentials applied only to its direct test connection; the subsequent full 50-case pilot reruns passed, including after the final ownership-fencing changes. Intermediate failed TRX files remain preserved alongside the final results.

## Benchmark method and evidence

The primary BenchmarkDotNet matrix executed 70 cases: 35 matched before/after pairs, .NET 10.0.10, Server GC, two launches, three warmups and ten measured iterations. Both sides use the same compiled implementation: residency disabled versus enabled. The original Git source archive is retained, but these are not separate old-executable versus new-executable measurements.

Cold timings include stream resolution and actual snapshot reconstruction on both sides. The actor is cold; process/type-identity caches are warm. Fresh OS-process correctness is tested separately. Timing includes direct actor handling, calculation, audit and required PostgreSQL persistence/projection work. Fixture setup, ActorRuntime mailbox scheduling and NATS network transport are excluded; real NATS behavior is covered separately by integration tests. Resource-probe request percentiles are direct actor/DB invocation latency, not network/API/UI latency. The independent-stream concurrency matrix exercises supported bounded calls; it does not establish that production callers routinely achieve the same parallelism.

Warm workloads use 1, 4, 16 or 64 independent streams with two awaited commands per stream. Concurrent callers are bounded at 16, including the 64-stream case. Replay has eight duplicate/completed requests. Churn has 32 requests across 16 streams with capacity four. Batch elapsed time is not individual request latency; the separate resource probe measures individual p50/p95/p99.

- [All 35 paired timings, uncertainty and allocation values](Resident-Actor-Pilot-Results-2026-11-08/resident-pilot-timings.md)
- [All BDN timing, allocation, GC, locking and completed-work metrics](Resident-Actor-Pilot-Results-2026-11-08/resident-pilot-paired.csv)
- [Separate CPU, memory, GC pauses, contention and request latency analysis](Resident-Actor-Pilot-Results-2026-11-08/resident-resources.md)
- [All 30 original resource samples](Resident-Actor-Pilot-Results-2026-11-08/resident-resources.csv)

The primary warm 64-stream means improved 18.4–31.0%. Completed Function replay improved 59.8–66.0%; Command duplicate replay improved only 0.6–2.4%. Warm single-stream Iron Condor showed no established mean benefit. Cold work and churn retain loading costs and incur cache/capture overhead; neither is a promised speedup.

An initial VWAP churn allocation diagnostic was unusually high, and the initial cold VWAP/Iron Condor means were 7.0%/5.1% slower with overlapping uncertainty. The six-case focused repeat completed: cold VWAP 33.892 ± 1.398 → 35.050 ± 0.903 ms (3.4% slower); cold Iron Condor 20.565 ± 0.388 → 19.783 ± 0.270 ms (3.8% faster); VWAP churn 979.731 ± 2.821 → 981.626 ± 2.864 ms (0.2% slower). A repeatable cold regression above 5% was not established. VWAP churn allocation was 95.02 → 95.93 KiB/command in the repeat; its original 974.58 KiB candidate diagnostic did not recur. The original measurements remain published, including that outlier.

- [Focused timing and allocation repeat](Resident-Actor-Pilot-Results-2026-11-08/resident-pilot-recheck.md)
- [All repeated resource samples and validity analysis](Resident-Actor-Pilot-Results-2026-11-08/resident-resources-repeat.md)

Iron Condor's original p99 varied widely and averaged 80.13 → 89.55 ms. The repeated p99 averaged 58.73 → 51.29 ms; no reliable p99 regression or universal tail benefit is established. CPU and GC pause averages fell for all pilots in both resource runs. Monitor contention did not fall consistently. These process-wide counts include provider and fixture/mock-context infrastructure; they do not isolate cache-only locks or measure asynchronous semaphore waits.

Two repeated process-wide lifetime allocation deltas were negative (Market Outlook enabled and Vertical Spread disabled), so those aggregate allocation values are explicitly invalid. Raw negative values remain preserved; they are not clamped, silently omitted or interpreted as savings. The runtime-counter discontinuity's cause has not been established. Other resource allocation totals also varied, so the long probes do not establish universal allocation percentages. BDN allocation diagnostics are separate, including the original VWAP churn outlier and its clean repeat. CPU/latency/GC measurements from those rows are retained with their separate definitions.

A separate quiet allocation control used the same accepted binary with Server GC and `DOTNET_GCDynamicAdaptationMode=0` only in that measurement process. All 30 deltas were positive. Warm KiB/request before → after: VWAP 131.82 → 66.34 (49.7% less), Outlook 120.39 → 59.85 (50.3%), Futures Plan 146.70 → 97.05 (33.8%), Vertical Spread 151.48 → 103.28 (31.8%), Iron Condor 239.71 → 176.12 (26.5%). This supports a warm allocation reduction and is consistent with adaptive-GC-sensitive accounting, without proving the exact counter defect. It does not retroactively repair default-policy samples or change Development's GC policy. Its pause/locking results are mixed, including higher Iron Condor pause time; zero collections in a short sample do not mean the path is GC-free.

- [Fixed-heap allocation control and all resource metrics](Resident-Actor-Pilot-Results-2026-11-08/resident-resources-fixed-heaps.md)

Raw evidence root: `E:\IFM\BenchmarkArtifacts\ResidentActors\20261108`. The accepted primary run is `bdn-corrected`; `bdn-final` failed generated compilation before executing benchmarks and is excluded from performance results. Source hashes, patches, fixture/restart logs, TRX, raw BDN JSON, counters and process resource records are retained. Original HEAD: `3da7aa14b337d8d04b4342e9cfa5b49118be561b`. Baseline archive SHA256: `4BFF51872B83A2BCC3DEC25ECB63DB76D7CFA1ED9977E61DE1CE474CAC89C325`.

## Soak, Development and disposition

The [30-minute burst/idle soak](Resident-Actor-Pilot-Results-2026-11-08/soak-summary.md) completed in 1,800.148 seconds: 28,160 successful commands, all 80 source stream counts matching successful requests, five caches each at 16 entries, zero pinned entries and zero in-flight work between bursts. Post-GC managed heap ranged 11.27–13.13 MiB and ended at 13.13 MiB; sampled process working set peaked at 205.27 MiB. The probe forces full collections for retained-memory observations and retains 88 measurement records. This is fixed-key workload evidence, not a hard byte bound or proof of no long-term growth.

All five policies were enabled one at a time in Development, with API launch readiness and Rust Host readiness HTTP 200 after each planned restart. Existing owned processes were stopped/drained before changing settings. No source/event migration was required. Non-Development defaults remain disabled.

Final Debug restoration (17:39 Toronto): API PID 71976, Server Manager PID 103560, Rust Host PID 37604, scheduler PID 88960, eGui PID 11524. API build passed with zero warnings/errors, current deployment-manifest hashes match, 216 actors are registered and monitored healthy, and startup has eight satisfied activities with none failed/skipped. Rust native manifest and PDB hashes match its Debug/development package, configured with full source debugging level 2. The unchanged Rust UI also retains its PDB.

eGui responds with title `IFM - eGui 0.1.0`, has established connections to Development NATS port 4222 and API port 22543, and successful Daily/Weekly/Monthly Strategy Updates grid queries were observed. Scheduler read-only pipe health reports database/Quartz available and scheduling started. Its initial pre-API NoResponders registration attempt recovered on the bounded retry. Authoritative Quartz triggers remain WAITING for November 8 18:00 and November 9 17:01 Toronto (Market Open / Market Close). The dashboard schedule list remains empty as it was in the pre-change baseline; authoritative triggers were verified independently.

The authoritative session was initially ScheduledStopped. Full `/health/ready` was HTTP 503 because live_pipeline was Inactive; `/health/launch-ready` was HTTP 200. The pre-existing optional warning remained that Market Outlook lacked its in-memory snapshot for ES20261218 / November 6 after historical warm-up reported AlreadyCurrent. These stopped-session observations matched the baseline and were not treated as live-market acceptance. The scheduled opening was then observed as detailed below.

The exact read-only financial comparison is unchanged across current position payload, all history payloads, Fund order/trade hashes, financial postings, allocation versions and close mappings. Position 1901:1501 remains closed, order 701/trade 4202 remain Closed, with 18 history rows and four unique close fills. Corrected multiplier 50 still gives realized loss $347.50, total commissions $5.20 and net loss $352.70. No order or financial reconciliation write was submitted by this optimization. Disposable pilot PostgreSQL/NATS/Redis containers were disposed after measurements; unrelated infrastructure was preserved.

| Pilot | Disposition after tests and benchmarks |
|---|---|
| VWAP | Enabled for Development; warm read reduction and multi-stream gains, small cold/churn costs, little duplicate-path gain. |
| Market Outlook | Enabled for Development; warm read reduction and multi-stream gains, little duplicate-path gain. |
| Futures Plan | Enabled for Development; warm and completed-replay gains; cold capture adds allocation. |
| Vertical Spread Plan | Enabled for Development; warm and completed-replay gains; cold capture adds allocation. |
| Iron Condor Plan | Enabled for Development; larger-stream/replay gains; no established single-stream mean gain, noisy tail and higher checkpoint allocation. |

Production rollout is not enabled or certified by this controlled pilot. The exact resource-counter defect is unresolved; invalid measurements and fixed-heap controls are disclosed above. No paid data acquisition was required.

## Live acceptance blocker found at 18:00

The scheduled Market Open fired on November 8 at 18:00 Toronto. Its run-start event was recorded and `MarketDataFeedCommand.Start.current` succeeded at 18:00:01.330. The opening log then explicitly admitted the native owner using API **ValueDate November 6, 2026**. This predates the current Sunday-evening session.

The unchanged Rust replay code (`futures_bootstrap.rs`, `session_start`, line 395) derives a session start at 18:00 New York on the preceding date. November 6 therefore requests November 5 at 23:00 UTC. Databento rejected the futures starts with terminal provider error code 5:

- GLBX.MDP3: `Invalid start time. Must be 2026-11-07T08:15:00Z or later, or 0`.
- XCBF.PITCH: `Invalid start time. Must be 2026-11-08T00:00:00Z or later, or 0`.

Workers repeatedly recover/recreate with that same stale session basis. Launch readiness remains HTTP 200 and all five Development processes remain running, but full readiness is HTTP 503 and the live futures feed is not qualified. **This is not a successful live-market acceptance run.** No MarketDataFeed, ValueDate provider or Rust replay code was changed by this resident-actor implementation.

All 65 existing pilot stream/version pairs were unchanged before versus after the attempted opening. A separate indexed event-head check again found 65 streams and zero metadata/source-head mismatches. The upstream feed failure prevented a qualifying live hot-path workload; no live cache-hit or throughput result is claimed. The diagnostic collector was stopped after the blocker was established, without treating an incomplete capture as successful evidence. Controlled actor/PostgreSQL/NATS tests and soak remain the completed functional qualification.

The required follow-up is to establish why the previous Market Close/ValueDate transition did not leave the server on the current trading session, reconcile that lifecycle through its owning commands while preserving EOD/financial ordering, and then start the correct session and rerun live acceptance. The historical-data/replay boundary also needs a valid bridge when its required start is outside the provider's allowed range. This optimization did not manually advance the trading date, fabricate market inputs, switch to live-only start `0` to hide a historical gap, or run financial reconciliation commands.

Raw opening logs and health responses are retained under the evidence root as `development-market-open.log`, `development-market-open-launch-ready.json`, and `development-market-open-ready.json`. The exact financial comparison remained unchanged after the attempted opening.

## Subsequent ValueDate correction

Following the user's instruction to check operational date on every call for a long-running singleton, `FuturesValueDateProvider` now reconciles the session calendar on each date read. Sunday November 8 at 18:00 resolves November 9 without restarting the instance or calling an explicit initializer. Successful EOD advancement during maintenance is retained; older EOD records and earlier clock observations cannot rewind the operational date. This does not fabricate an EOD completion or settle historical financial work.

Regression coverage includes weekday/Sunday rollover, both DST transitions, weeks of uptime, repeated reads, late completion restoration and feed preparation using the same provider across opening. Market Data unit tests passed 319, Feed 619, Application 91 and SystemAdmin 435: 1,464 passes. The Development API was rebuilt in Debug with zero warnings/errors. Evidence is under `E:\IFM\BenchmarkArtifacts\ValueDateRefresh\20261108`.

IFM remains stopped at the user's request. The corrected build has not undergone another live opening, so the historical failure above is retained and live acceptance remains unexecuted for this correction. The resident-pilot timing results describe their recorded source/GC configurations; this later date correction is not a new benchmark run.

The next actors/functions remain deferred until these results have been presented and reviewed.
