# Order Composition live evidence register v1.0

Updated September 8, 2026. Reviewed reference publication and the combined live pricing/recovery path now have executed evidence. Sustained-run results are separate from the broader Stage 4 deployment acceptance matrix.

## Published inputs

See the [publication record](OrderComposition-Reference-Publication-and-Qualification-v1.0.md) for all seven bundle IDs, 4,268 exact definitions, calendar coverage and reproduction. Current profile: `CME-ES-TueThu-202609/v2`. The September 10 canary expiry contains all 726 native definitions, linked to ESU6 publisher 1/instrument 42140870. Later September expiries correctly link to ESZ6 publisher 1/instrument 10252. Every published mapping was read back.

Inputs include European exercise, delivery into the exact future, USD 50 multiplier, native expiry checked against 16:00 Eastern, current CME 358A premium bands, explicit ACT/365F and the finite September exchange calendar. Labor Day is excluded as a separate business trade date. The initial `/v1` is superseded by `/v2`.

Treasury observations come directly from official `daily_treasury_yield_curve`, with `USTreasury` provenance and pinned semiannual-to-continuous conversion. `USTreasury-2026-18ET/v1` is an application allowance, not a provider SLA. See the [official Treasury implementation](OrderComposition-Official-Treasury-Implementation-v1.0.md). FMP remains the economic-calendar provider.

## Executed combined path

The opted-in `Published_reference_live_worker_pricing_durable_handoff_replacement_and_close` case:

1. Reads the published Scylla bundle and obtains a real ESU6 quote.
2. Requests the complete interval containing two adjacent strikes and both rights: four legs. This is a qualification scope, not a strategy-selection algorithm.
3. Starts a standalone native DataBento worker under Production/StrictProduction with unchanged 1000 ms quote-age and 250 ms skew limits.
4. Prepares real Black-76 snapshots for individual Daily, Weekly and Monthly requests, persists immutable Scylla preparations and verifies digest readback through a new repository.
5. Commits a real order event in an isolated PostgreSQL stream, projects and realizes its ownership before releasing discovery.
6. Kills the exact owned child, replaces it, reconstructs the chain from committed ownership, rejects the old generation and obtains a new priced snapshot.
7. Maintains ownership/context refresh throughout the elapsed live interval and records request latency, qualification failures, native ticker/option counters, GC/heap and RSS.
8. Commits position-open/close facts and verifies terminal ownership drains the physical scope. No broker submission occurs.

The first 30-second canary passed in 56 seconds overall: `closure/live-closure.trx`, `closure/first-live-canary.json`. It is initial evidence, not a substitute for the requested sustained interval.

## Failures exposed by longer runs

Two longer attempts terminated when `DatasetWorkerDiagnostics.Validate` rejected an observation while constructing a control response. The actual Windows unhandled exceptions are preserved in `closure/worker-diagnostic-crashes.log`. These are failed runs, not passing soak evidence.

`DatasetWorkerRuntime.GetDiagnostics` now validates inside its protected observation boundary. Invalid evidence returns an explicit unavailable/unhealthy diagnostic, preventing another exception while writing a failure response. Bounded numeric details are retained. The original generic exception did not capture its individual invalid counter, so that counter's cause is not claimed to be established.

The harness now performs the independent health re-probe used in production before retrying reconciliation and records unavailable observations. Recovering requests remain failures; market-event timestamps and freshness thresholds are unchanged. Authenticated worker rejections also retain their bounded reason, with identity/token/sequence/correlation checked first.

A parallel build encountered locked files in the active worker output. Subsequent output was isolated; verification must not rebuild a running standalone worker's directory.

The final live run **passed** in 30 minutes 20 seconds overall: `closure/live-closure-final.trx`. Its measured soak lasted 1800.077 seconds, followed by successful position closure and `ClosedAndDrained`/`ChainUnavailable` evidence in `closure/live-pricing-recovery.json`. No owned worker process remained after shutdown. There were no diagnostic re-probes or native ring overruns in the successful run.

| Final measured live result | Value |
| --- | ---: |
| Requests / qualified snapshots | 6,071 / 4,205 |
| Incoherent / stale rejections | 1,502 / 364 |
| All-request p95 / p99 | 1.9237 / 2.5752 ms |
| Worker RSS minimum / maximum | 67.55 / 87.03 MB |
| Sampled allocation rate | 5.64 MB/second |
| Sampled Gen 0 / 1 / 2 collections | 2,907 / 3 / 3 |
| Sampled total GC pause | 2,087.42 ms |
| Average / peak 30-second option-record rate | 160.50 / 210.11 per second |

Request latency includes qualified and rejected requests. GC/rate counters cover the recorded progress-sample interval; they are not claimed as exact counters at the final millisecond. The 512-contract controlled run sustained 9,029.46 quotes/second, 42.97 times the observed peak interval rate, exceeding the prescribed two-times criterion. Summary: `closure/final-summary.json`.

## Other verification

| Check | Evidence |
| --- | --- |
| Real Scylla mapping/bundle repeat, restart, conflict and tamper refusal | `closure/reference-storage.trx`: 1 passed |
| Committed two-/four-leg replacement and next-value-date reconstruction | `closure/live-closure-v2.trx`: 4 controlled cases passed; its separate live case failed |
| Repeatable offline runner | `run-d73b683ee9344d60926369db8724363b`: market checks, 1 Scylla case and 8 ownership cases passed |
| Actual supervised recovery | `closure/supervised-recovery-stress.trx`: 100 cycles, including ten kills/replacements; per-cycle JSON retained |
| Frequent diagnostic observation | `closure/diagnostic-stress.trx`: 10,000 actual health reads passed |
| 10,000 leases / 512-option shared scope | Included in 38 focused passes in `closure/final-reference-diagnostics.trx` |
| Maximum-scope elapsed managed load | `closure/managed-load.trx`: passed; 1800.045 seconds, 16,253,440 quotes across 512 contracts, 9,029.46 quotes/second; `closure/managed-load.json` |
| Final full market-data application suite | `closure/market-complete.trx`: 476 passed, 5 explicit opt-in/platform skips; stress/live opt-ins have separate executed records |
| Market-data domain / Trade / Application lifecycle | `domain-market-final.trx`: 195; `trade-final.trx`: 797; `application-final.trx`: 30 passed |
| Final reviewed business keys and diagnostics | `closure/published-business-keys.trx`: 24 passed |
| Actual API composition root | `api-startup-verification.log`: exit 0; no schemas, actors, feeds or HTTP listeners started |

Focused counts overlap broad suites. The controlled load uses real consumer/pricer/snapshot code with generated UTC observations; native synthetic performance-clock timestamps are not relabelled as live market time.

## Earlier machine/source evidence

Approved Windows Time startup/resynchronization and a bounded correction enabled strict event-time testing. A later sample still showed approximately 112-116 ms of lead; the correction does not guarantee permanent clock accuracy. Both native backends reserve bounded working set for concurrent required ring locks. Earlier evidence: C++ required-lock test 1 passed; Rust 7 passed; strict authenticated DataBento quote test 1 passed. FMP endpoint access also passed, but its rate-series lineage is no longer a pricing prerequisite.

Sources: [CME series](https://www.cmegroup.com/articles/faqs/e-mini-s-p-500-tuesday-and-thursday-options-frequently-asked-questions.html), [current 358A rule](https://www.cmegroup.com/rulebook/CME/IV/350/358A/358A.pdf), [trading calendar](https://www.cmegroup.com/trading-hours.html), [2026 Labor Day settlement notice](https://www.cmegroup.com/tools-information/holiday-calendar/files/2026/labor-day-holiday-settlement-times-2026.pdf).

## Deployment acceptance boundary

This evidence does not certify a full relevant trading session, natural overnight/OffTrading/Closed transitions, all deployment profiles, Linux native execution or UI reconnect. Those remain the later OCP-T25/Stage 4 rollout matrix. Controlled date advancement and a bounded soak do not substitute for elapsed overnight/full-session acceptance. Existing live-enablement guards remain. OCP-07 explicitly allows the composer implementation document before that later acceptance matrix; composer algorithms remain OC-01 through OC-08.

