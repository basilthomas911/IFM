# State repository creation snapshot implementation results

Date: 2026-11-03

Status: Complete. Snapshot-specific verification passes, and the authorized follow-up resolved the broader Trade and Portfolio qualification blockers. See the [follow-up results](State-Repository-Qualification-Blockers-Results-2026-11-03.md) for final counts, timings and the explicitly skipped live/manual gates.

## Implemented behavior

The [resolved catalog](State-Repository-Creation-Snapshot-Catalog-2026-11-03.json) covers every repository in the original audit. The first committed source instance, at **StreamVersion 1**, is the default creation snapshot. The loader replays it and its ordered tail through a head captured by the same PostgreSQL MVCC statement. Repeated Changed/Started/Generated facts cannot replace creation. No source rows, event names, MessagePack keys or aggregate identities are rewritten. No new creation wire contract is necessary: existing atomically committed first domain facts establish creation for both existing and new streams.

The catalog declares 53 ordinary creation-plus-tail repositories, six backup concerns with legacy-prefix compatibility, one authoritative workflow loader, five complete-checkpoint optimizations, three stateless creation/head loaders and four immutable Portfolio operation adapters. Complete checkpoints are explicit for ITI, Trade Signal, TDI, Iron Condor Plan and Forward Price. Their tests compare authoritative state with full replay; request matching, hold transitions, committed versions and pending-event behavior are covered. Incomplete analytics checkpoints fall back to full creation replay.

The three stateless states retain no business fields. A marker constraint and a reflection convention test enforce that restriction. PostgreSQL tests verify two returned rows for a 64-event stream. Dense 100,000-event generic EXPLAIN plans prove that the checkpoint and metadata paths seek endpoints rather than materializing all historical rows.

Normal replay rejects a missing creation fact, a middle gap or an incomplete tail. Validation drains rows even when a consumer ignores or stops reading the sequence. Appends committed during reduction are excluded from the captured head. Unique statement identities prevent prepared-query collisions between loading policies. SQL uses concrete C# parameter records with positional binding.

RSI and ADX replay lifecycle facts from creation while retaining their configured computation windows. Reset/restart markers and RSI accumulator evidence are preserved. MACD full replay recovers legacy observation counts instead of treating a lone last-generated fact as all historical observations. Portfolio completion adapters keep their canonical operation store and original business transaction as the sole authority. Backup prefix versions never become the concern-owned stream's append version.

## Verification

The recorded [qualification results](../../.artifacts/creation-snapshot-implementation-20261103/qualification-results.json) contain exact logs and counts:

| Suite | Passed |
| --- | ---: |
| Reference unit | 270 |
| Trade unit | 1,626 |
| SystemAdmin unit | 422 |
| Portfolio unit | 481 |
| Analytics unit | 1,260 |
| Feed unit | 618 |
| Application unit | 75 |
| Supervisor unit | 71 |
| MarketData unit | 312 |
| Securities unit | 30 |
| **Unit total** | **5,165** |
| Focused PostgreSQL storage / snapshot / transaction checks | 29 |
| Reference integration | 27 |
| Focused Trade replay / serialization / projection integration | 33 (three existing opt-in skips) |
| Paired before/after workload assertions | 3 |
| Portfolio wire verification | 79 |
| Backup domain integration | 1 |

The focused storage suite covers repeated creation-event types, concurrent append, cancellation, empty streams, missing creation, early-stopping reducers, middle gaps, absent checkpoints, metadata endpoints, dense generic query plans, frozen historical catalog identities and atomic trade/workflow/Regime Discovery writes. A convention check accounts for all 72 concrete repositories and the shared backup base. Runtime replay tests accompany that source convention guard.

Additional fixture corrections made qualification executable with current contracts: old snapshot mocks, relocated E: build roots, PostgreSQL transaction coordinators, atomic policy/Portfolio test-store commits, owned-database guards, ReferenceDb provider selection and appended projection payload keys. Production business rules and public wire layouts were not changed by these test updates.

## Before/after measurements

Raw initial baselines are in `before-load-timings.jsonl`; the final comparison runs both loaders on identical stored rows in a single isolated PostgreSQL fixture, with six measurements per variant (first load plus five warm loads). The old path reads the committed head separately; the new path captures it in its source query. Each run verifies the exact number of facts replayed and the final committed version. These are source hydration/replay measurements with valid repeated lifecycle facts, not indicator calculation or end-user UI latency. First-load timings are not a controlled cold-cache experiment; p95 is the maximum of only five warm observations. Build-and-test wall times are retained separately and are not application performance benchmarks.

| Source facts | Before warm p50 | After warm p50 | Change | Before warm max/p95 | After warm max/p95 |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1,000 | 16.49 ms | 18.57 ms | +12.6% | 21.65 ms | 21.17 ms |
| 100,000 | 537.85 ms | 617.03 ms | +14.7% | 591.39 ms | 679.63 ms |
| 1,200,000 | 7,494.84 ms | 7,745.90 ms | +3.3% | 10,993.32 ms | 8,509.63 ms |

Full creation replay is **not a universal speed improvement**. The paired warm medians show the cost of capturing/validating a stable head, checking stream continuity and allocating independent row models. At 1.2 million facts, warm median allocations increased from about 940.83 MB to 998.42 MB (about 6.1%); this prevents callbacks that retain rows from observing one repeatedly mutated row object. The validated checkpoint and stateless paths avoid superseded replay entirely: the dense query test returns creation and the latest checkpoint/head as two rows instead of 100,000. Full tail replay remains necessary where later events are incomplete snapshots or carry independent state.

Both variants passed all three workload assertions; the final paired run completed successfully in 2.39 minutes. Raw values, including first-load measurements and all allocation samples, are in [paired-load-timings.jsonl](../../.artifacts/creation-snapshot-implementation-20261103/paired-load-timings.jsonl).

## Broader qualification follow-up

The initial broader Portfolio run had 202 passes, six failures and five opt-in skips; the initial Trade run was cancelled after recording 28 failures and a stall. The user subsequently authorized resolving these blockers. Their original [failure inventory](../../.artifacts/creation-snapshot-implementation-20261103/broader-blockers.json) and logs remain available as baseline evidence.

The completed follow-up has **208 Portfolio integration passes and 163 Trade integration passes, zero failures, and eight existing live/manual opt-in skips**. Both full suites exited successfully, including teardown. It also passed 2,810 relevant unit tests, 79 Portfolio wire checks and 20 PostgreSQL Trade storage/infrastructure regressions, including preparation of all 99 migrated legacy SQL statements.

The follow-up corrected native PostgreSQL SQL bindings, removed duplicate domain query SQL, supplied child-process deployment manifests, validated catalog products through published Reference snapshots, resolved the Portfolio batch-100 timeout, and fixed additional audit/retry, history-revision, test-host teardown and timing-instrumentation gaps uncovered by full verification. The unchanged two-second batch deadline now passes: the final measured batch median is 1,233.12 ms and maximum 1,498.07 ms. The original creation-replay measurements above remain unchanged.

See the [qualification follow-up report](State-Repository-Qualification-Blockers-Results-2026-11-03.md) and [final qualification inventory](../../.artifacts/qualification-blockers-20261103/qualification-results.json). No required qualification blocker remains. The skipped gates require explicit external orchestration, live-feed credentials/evidence or a dedicated disposable PostgreSQL audit database; they are not counted as passes.

Disposable fixture infrastructure was cleaned up. Development services were not stopped or redeployed; builds use isolated E: outputs. No production database write, historical event rewrite, commit or push was performed.

## Evidence locations

- [Implementation plan and resolved convention](State-Repository-Creation-Snapshot-Implementation-Plan-2026-11-03.md)
- [72-repository catalog](State-Repository-Creation-Snapshot-Catalog-2026-11-03.json)
- [Verification and timing artifacts](../../.artifacts/creation-snapshot-implementation-20261103)
