# ES Trade Blotter Stage 4 Implementation and Verification v1.0

Date: 2026-09-20  
Status: **Implementation and offline qualification complete; production activation blocked pending owner approval of initial series and consumer policy.**

## Completion summary

Stage 4 implementation and offline qualification satisfy S4.2-S4.7 of [Option Volatility, IV Rank/Percentile and Historical Evidence](Option-Volatility-IV-Rank-Percentile-Stage-4-Requirements.md). API startup now deterministically provisions the Series, Consumer Rules, and Retention defaults as three version-one **Draft** parameter sets for review. They are not published, assigned, or wired to runtime activation. Production activation remains outside this completion record until the S4.1 approvals below are recorded and the reviewed drafts are explicitly published and assigned.

| Stage requirement | Delivered completion |
| --- | --- |
| S4.2 — analytics and contracts | Added immutable, versioned contracts with explicit units, provenance, coverage, freshness and failure states. Rank and Percentile are pure calculations over prior configured exchange sessions: Percentile uses strict-less-than ties, Rank includes current IV in its bounds, and each metric has an independent status. |
| S4.3 — comparable-series construction | Added deterministic ATM selection, call/put combination and constant-tenor interpolation in total-variance space, with explicit expiry transitions and futures-roll continuity by stable series methodology. Daily samples are deduplicated to one canonical observation per value-date/slot; intraday checkpoints and enrichment are bounded. |
| S4.4 — persistence, publication and retrieval | Added immutable PostgreSQL series/policy definitions and ScyllaDB observation history, metric history, sealed snapshots and rebuildable latest pointers. Writes use stable identities for idempotent retries; corrections append superseding revisions. Publication orders source evidence and sealed snapshots before the manifest/latest pointer, fences stale writers, hides incomplete work and supports restart/cache recovery plus bounded paging. |
| S4.5 — typed consumer evidence | Propagated versioned, typed snapshot evidence through TradeSelection, Composer, Risk and Portfolio. Required/optional handling is explicit, fabricated zero cannot enable a strategy, accepted evidence is retained through downstream decisions, and protective exit/cancel behavior remains available. `MarketCondition` is unchanged. |
| S4.6 — operator views and fixtures | Added UI current and bounded history views, exact decision snapshots, and explicitly labeled **as-known** and **restated** history modes. Added deterministic market-closed fixtures with virtual time and simulated roll/correction behavior. |
| S4.7 — verification and performance | Completed formula, series, historical, persistence/recovery, consumer, Portfolio and UI qualification, including the real-Scylla boundary and bounded in-memory API benchmarks summarized below. |

## Verification evidence

| Evidence | Result |
| --- | ---: |
| Analytics unit tests | 55 pass |
| BDD scenarios | 5 pass |
| Trade tests | 8 pass |
| Portfolio tests | 13 pass |
| UI tests | 29 pass |
| Real Scylla integration | 1 pass in 650 ms |
| `git diff --check` | Exit zero; line-ending notices only |

Market-closed qualification covers deterministic full-window data, virtual time, expiry/futures roll, correction and point-in-time behavior. The completed suite also verifies append-only correction, duplicate/idempotent delivery, interrupted publication recovery, latest-pointer fencing, cache reconstruction, as-known/restated separation, daily deduplication, bounded intraday sampling and unchanged `MarketCondition` behavior.

## Benchmark evidence

Environment: .NET 10, Concurrent Workstation GC.

| Operation | Mean | Gen0/1000 ops | Gen1/1000 ops | Gen2/1000 ops | Allocated |
| --- | ---: | ---: | ---: | ---: | ---: |
| Calculate, 32 observations | 9.465 us | 2.5940 | 0 | 0 | 10.61 KB |
| Calculate, 252 observations | 78.083 us | 16.4795 | 0 | 0 | 67.59 KB |
| Bounded history page, 32 rows | 63.25 us | 28.6865 | 0.6104 | 0 | 117.58 KB |
| Bounded history page, 256 rows | 68.09 us | 29.1748 | 2.8076 | 0 | 119.3 KB |

These measurements exercise the in-memory API path. They are not measurements of ScyllaDB network latency.

## Remaining S4.1 activation approvals

Production activation requires the owner to approve and version:

- Target tenor and eligible product family.
- Historical lookback, minimum sample count, minimum coverage, freshness and maximum quote age.
- ATM convention, call/put policy, sampling cadence/slots, session cutoff and material-change boundary.
- Retention/audit horizon and the qualified history source.
- Which strategies treat volatility evidence as required or optional, and any strategy, Composer or Risk thresholds.
- Market-hours behavior and provider/data-source qualification.

No additional architecture or offline-qualification categories are expected after these approvals. Before production activation, the approved parameters must be recorded as immutable, versioned definitions and the complete verification suite must be rerun against those exact versions.
