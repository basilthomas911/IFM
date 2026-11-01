# Rust MarketData performance implementation plan

Date: 2026-10-31  
Status: Completed on 2026-11-01. All 20 items evaluated: 15 received code improvements (with scope limits below), one was already satisfied, three were deferred with evidence and one is blocked by the pinned SDK. Accepted Release package and combined offline qualification completed.  
Scope: The 20 ranked improvements for `TomasAI.IFM.Application.MarketData.Rust`.

Improve live ingestion latency, option-pricing capacity, historical throughput and retained memory while preserving the existing financial and lifecycle contracts. Begin with a reproducible baseline and small indexing/preparation changes. Advance to scheduling, concurrency and provider changes only when measurements justify them.

This plan supplements the existing MarketData Rust FFI implementation plan and qualification reports. It does not change backend selection, authorize a production cutover, or establish live-provider acceptance. Performance work is confined to the Rust backend and its tests/adapters where required; unrelated application and UI changes stay outside this scope.

## Existing implementation

Retain the optimizations already present: Tokio ownership, bounded queues, fixed quote-buffer pools, latest-quote coalescing, bounded streaming DBN metadata decoding, separate selection/risk pricing paths, cooperative pricing yields and NATS publication batches of up to 32 ready messages. Extend these mechanisms where profiles show a benefit rather than replacing them by default.

The current Release profile already uses optimization level 3, thin LTO and one codegen unit. Compiler work in item 20 is a measured tuning experiment, not simply enabling Release mode.

## Correctness requirements

- Preserve every authoritative trade, order-book contribution and statistic, including repeated same-price trades, replay contributions and corrections. Coalescing remains limited to explicitly replaceable quote/display state.
- Preserve actor publication sequence, generation and manifest identities, including enqueue-failure behavior. Broker round-trip completion remains distinct from durable storage acknowledgment.
- Preserve exact nanounit/decimal representation, CLR timestamp/date conventions, source validity checks and the qualified numerical operation order and tolerances. Do not enable fast-math or weaken convergence settings to improve timings.
- Preserve original absolute deadlines, cancellation, whole-owner cleanup and joined shutdown. A retained reader or queued job must not retain a retired provider session or certify a replacement generation.
- Preserve wire and FFI compatibility. Any new batch ABI must be versioned and retain the existing entry points until its managed adapter is qualified.
- Preserve production capacity limits. Stress fixtures above those limits test algorithms in isolation; they do not increase live limits.

## Baseline and performance gates

Before production edits, capture the current commit, dirty-source patch and hashes, fixture hashes, lockfile, Rust/.NET versions, machine, build flags and deployment configuration. Keep a baseline package and immutable raw results under a uniquely named `.artifacts/rust-marketdata-performance/` run directory. Compare the current working tree, not an older committed implementation, against each candidate.

Add a deterministic replay/performance harness alongside the existing qualification tooling. It must drive actual normalization, books, pricing and publication paths where practical, with separate microbenchmarks for isolated operations. Run Release comparisons and keep Development-profile debugging results separate.

| Workload | Scenarios | Measurements |
| --- | --- | --- |
| Live ingress | Futures and options; 100, 1,000 and production-limit instrument counts; quiet, steady and burst traffic | Records/second, ingress-to-book and ingress-to-queue p50/p95/p99, allocations, CPU, queue age/depth |
| Pricing | European/American; sparse option changes, shared-underlying changes, unchanged input with advancing time, required-risk subsets | Selection/risk completion age, calculations/second, examined/repriced options, solver iterations, allocations, control/I/O latency |
| Publication | Small and large payloads; normal, slow and unavailable local NATS fixture; evidence acknowledgment delay | Encode/compress time, bytes, allocations, batch sizes, broker round trips, queue age and retained bytes |
| References and option caches | Cold preparation, unchanged refresh, one changed reference, broad refresh and chain replacement | Preparation time, decoded objects, digest computations, bytes copied, peak and retained memory |
| API queries | Repeated canonical-contract queries during updates and generation replacement | Query p50/p95/p99, lookup work, lock wait/hold time and unavailable/stale-result parity |
| History and lifecycle | Large deterministic replay, slow managed consumer, cancellation, restart and subscription changes | Records/second, FFI calls/wakeups, memory, cancellation/join latency, replay completeness and recovery duration |

Use identical inputs, clock anchors and arrival schedules. Warm each workload, then collect at least five alternating baseline/candidate pairs. Keep allocation instrumentation separate from latency measurements if its overhead is material. Do not run compilers or other benchmarks concurrently with timing runs. Report localhost transport results separately from provider/network results.

Accept a candidate only when correctness gates pass and the targeted metric improves repeatably beyond measurement noise. Record absolute and relative results, including p99, CPU and memory tradeoffs. Investigate an unexplained regression above 5% in an important untargeted metric; do not accept it automatically because the average throughput improved. These are review gates, not promised speedups.

## Delivery sequence

| Phase | Original item numbers | Deliverable and exit condition |
| --- | --- | --- |
| 0 Baseline | Prerequisite | Reproducible workloads, immutable baseline, profiling and allocation evidence |
| 1 Indexing and prepared inputs | 1, 14, 3, 5, 6 | Lower lookup/allocation/read costs with unchanged pricing and generation behavior |
| 2 Pricing scheduling and synchronization | 2, 8, 7, 9 | Bounded dirty-work processing, fewer notifications and measured contention reduction without lost records |
| 3 Encoding and immutable data | 10, 11, 12, 13, 17 | Compatible publications with fewer temporary objects, copies and repeated hashes |
| 4 Background and bulk throughput | 15, 16, 18 | Incremental maintenance, qualified history batches and tuned ordered publication |
| 5 Conditional architecture and compiler work | 4, 19, 20 | Adopt only experiments that demonstrate benefit and pass lifecycle/provider qualification |
| 6 Combined qualification | All adopted items | Combined before/after report, parity and recovery results, isolated rollback-ready package |

Use one reviewable change per item or tightly coupled dependency. Measure against the preceding accepted implementation and periodically against the original baseline, so individual gains are not incorrectly added together. Reorder conditional work if the baseline exposes a dominant bottleneck.

## Implementation work items

All items start as **Planned**. At completion record one disposition: **Implemented and measured**, **Already satisfied**, **Deferred with evidence**, or **Blocked**, with the relevant result/log links. Conditional items can finish as deferred; they must not be represented as implemented.

| Item | Concrete implementation | Verification and dependencies |
| --- | --- | --- |
| 1 Instrument registration index | In `dataset/live.rs`, build a bounded instrument-to-registration index when realizing a generation. Replace per-market-record vector searches while preserving publisher qualification and unknown-instrument behavior. Keep a separate source-symbol index if mapping-event profiles justify it. | Compare CPU/record at increasing registration counts. Test duplicate registration, unknown IDs, publisher mismatch and generation replacement. |
| 2 Changed-instrument pricing | In `option_chain/coalesced.rs`, maintain deduplicated dirty selection/risk sets and reverse underlying dependencies. Cache membership lists instead of rebuilding full ID vectors for each pass. Keep scheduled time/freshness/expiry and reference-validation work even when no quote changes. | Compare sparse and broad updates; verify fairness, no starvation, required-risk coverage, time decay, stale inputs and generation checks. Depends on 0 and benefits from 6. |
| 3 Reusable pricing workspaces | Introduce bounded reusable scratch storage in `pricing/mod.rs` for American tree and Greek calculations. Size from the unchanged numerical policy; give each concurrent calculation exclusive storage. | Run pricing oracles and cancellation tests. Measure allocation count and p99 per calculation; test workspace reuse after failure and retirement. |
| 4 Bounded pricing workers | Compare current cooperative execution with a tracked CPU executor for expensive calculations. Bound jobs/bytes, check cancellation during numerical loops, join workers on retirement and reject stale completions. Keep fast paths on the existing executor unless measurement supports moving them. | Prove no detached work, no shared-I/O starvation and bounded shutdown. Compare concurrent I/O/control p99 and pricing throughput. Depends on 2, 3 and baseline profiles. |
| 5 Typed prepared trade references | In `messaging/option_pricing.rs` and preparation code, decode immutable reference fields into validated typed data at preparation. Retain required original representations for evidence, hashing and wire compatibility. | Compare actual-trade pricing allocations/time. Test malformed references, frozen-context identity, stale references and byte/economic parity of evidence. |
| 6 Shared underlying reads | Group a pricing pass by underlying and copy each required underlying observation once within the pass's explicit observation boundary. Preserve option-specific input/time validation and generation identity. | Compare reads/locks per chain. Test underlying changes during a pass and the existing stale-completion rejection. Do not claim a globally atomic snapshot across independent books. |
| 7 Shorter and independent book locks | Profile book/query/output lock wait and hold times in `dataset/live.rs` and related stores. First move redundant lookup/preparation outside locks; partition only measured contention hotspots with a documented lock order. | Concurrent readers/ingress/reset tests must preserve coherent per-instrument snapshots and retirement. Reject lock-free or sharded replacements that weaken these guarantees. |
| 8 Coalesced notifications | Signal delivery on empty-to-nonempty transitions or with a pending-work flag. Consumers clear/recheck the flag atomically with the drain protocol. Retain independent lifecycle/error signals. | Lost-wakeup race tests, enqueue during final drain, burst tests and quiet-market cancellation. Measure wakeups/record and queue age. |
| 9 Bounded ingress batches | Drain already-ready decoded work into preallocated batches without waiting for a fill timer. Amortize synchronization where safe; cap records and elapsed work, then check cancellation and yield. | Test record ordering, replay/control boundaries, overflow and saturation. Benchmark throughput and control p99. Avoid cancelling a partially advanced SDK read; batch only at a qualified owned-record boundary. |
| 10 Typed publication encoding | Replace dynamic `rmpv::Value` construction on the hottest event paths with typed/direct MessagePack writers. Keep general dynamic decoding for cold or externally variable inputs where useful. | Byte-level contract fixtures, decimal/date edge cases, schema compatibility, malformed input and existing managed consumers. Profile first to select paths. |
| 11 Encoding buffer reuse | Add generation/worker-owned reusable encoding and LZ4 scratch buffers with explicit retained-capacity limits. Transfer ownership safely while packets remain queued or in flight. | Measure allocations and peak retained bytes. Test slow readers, cancellation, failed encoding and oversize payloads. Depends on ownership choices in 10. |
| 12 Reference digest reuse | Cache canonical digests and immutable encodings by complete semantic identity. Include policy, conventions, date, source, validity and relevant generation dependencies; do not cache a validity decision past its time boundary. | One-field-at-a-time invalidation tests and exact semantic-hash parity. Measure digest count and reference-refresh cost. |
| 13 Shared sealed snapshots | In `messaging/option_cache.rs` and composition code, accept/share immutable sealed snapshots instead of deep-cloning their graphs into a new `Arc`. Audit retained readers for provider ownership. | Compare bytes copied and retained memory; test expiry, replacement and disposal with externally retained snapshots. |
| 14 Canonical contract query index | In `api/owner/prices.rs` and registration owners, build bounded contract-to-binding indexes per immutable generation. Preserve lane/asset distinctions and unavailable versus unmapped results. | Query parity under updates, remapping and retirement; benchmark repeated and mixed-contract lookups. |
| 15 Incremental background planning | Track changed source/reference/scope identities in `dataset/background.rs` and its workers. Reuse unaffected validated groups; publish a complete consistent desired result after each accepted update. | Test add/remove/change, missing underlying prices, recovery, reference expiry and no-change maintenance. Measure preparation CPU and allocation spikes. Depends on 12 where caches overlap. |
| 16 Historical batch delivery | Extend existing history delivery to transfer bounded arrays/chunks and signal available work once per batch. Reuse any existing compatible batch route before introducing an ABI. Preserve every record and backpressure. | Managed/native ABI qualification, order/completeness, partial final batches, slow consumer, cancellation and resumed replay. Compare FFI calls, throughput and memory. |
| 17 Option cache encoding reuse | Reuse compatible immutable encoded snapshot components and hashes across captures. Reassemble the existing complete cache publication; do not introduce a receiver-dependent delta protocol as part of this item. | Full snapshot/chunk/digest parity, changed reference/expiry/generation invalidation and bounded retained memory. Depends on 10, 12 and 13 as applicable. |
| 18 NATS batch tuning | Benchmark existing 32-ready-message batches against bounded count/byte alternatives. Keep first-message delivery immediate, one ordered actor stream, byte reservations through flush completion and separate evidence/control ownership. | Slow/blackholed broker tests, sequence continuity, saturation and receipt handling. Compare round trips, queue age and p99. Do not equate broker flush with durable receipt. |
| 19 Incremental provider membership | First establish supported provider/SDK add/remove semantics and replay/mapping behavior. Prototype only if a tracked update can preserve authority and recovery. If removal requires replacement, retain controlled replacement and record the limitation. | Separate provider qualification, changed membership races, stale mapping rejection and rollback. No live subscription changes during offline implementation. Depends on 15 and explicit provider capability evidence. |
| 20 Compiler tuning | Compare the existing Release build with candidate optimization/codegen settings and representative profile-guided optimization. Keep target CPU compatibility and reproducible build flags. | Repeat numerical, ABI and lifecycle qualification with the exact candidate binary. Retain only repeatable improvements across representative workloads, not training data alone. |

## Test and qualification strategy

Extend the existing tests rather than replacing their expectations. Relevant starting points include `coalesced_pricing`, `committed_book`, `native_book`, `latest_parity`, `native_raw_prices`, `market_price_parity`, `native_price_policy`, `history_batch`, `history_stream_abi`, `native_event_wire`, `native_nats_publication`, `native_evidence_outbox`, `native_evidence_exchange`, `dataset_supervisor`, `availability_supervisor` and `lifecycle`. Confirm exact test targets during implementation.

For each item, run focused correctness tests and its benchmark first. At phase boundaries, run affected managed adapters and the appropriate offline qualification stages. At final acceptance run formatting, strict Clippy, Rust tests/documentation, pricing/metadata/wire oracles, managed ABI tests, and the existing `qualification/Verify-Offline.ps1` workflow. Include isolated NATS/PostgreSQL, sustained load and production-duration deadline fixtures where the changed paths require them; report intentionally omitted gates and their reasons.

Keep local fixtures isolated from the running Host/API, broker scope and databases. Live provider probes and deployment acceptance remain separate from this implementation plan. The existing backend stays selectable for rollback.

## Completion and handoff

Deliver the per-item disposition table, original and combined before/after results, raw measurement files, correctness logs, remaining bottlenecks and an isolated package with source/binary hashes. An item blocked by provider semantics or an incompatible contract must name that dependency and retain the working implementation.

Implementation was authorized on 2026-11-01, including before/after timings. Run evidence is retained under `.artifacts/rust-marketdata-performance/20261101/`. Production backend selection, deployment and commits remain outside this execution scope.

## Execution dispositions — 2026-11-01

The baseline is the actual pre-edit source at commit `1be8410ce4ce2bbc4f740e4370649b6e581d95b1`, archived with its source, Release DLL, executable and rlib. No provider credentials are used by these measurements. All following changes preserve the existing numerical policy, deadlines and wire contract.

| Item | Disposition and delivered scope | Evidence / remaining limitation |
| --- | --- | --- |
| 1 | Implemented: generation-owned instrument index. | Unknown/publisher/generation tests retained. At 2,048 bindings, isolated lookup kernel improves about 91%; this is not a measured 91% improvement in complete ingress. |
| 2 | Implemented membership caching and fair traversal; dirty-only scheduling deferred with evidence. | Membership invalidation tests cover register/remove/risk changes. Unchanged quotes still need time decay, stale-input and reference-expiry validation; suppressing their cadence would change financial behavior. |
| 3 | Implemented bounded calculator scratch reuse. | One retained workspace; concurrent users have exclusive scratch and canceled work returns it. American implied/risk uses one cold tree allocation; subsequent solve reuses it without another tree allocation. Financial oracle coverage retained. |
| 4 | Deferred after measurement: both parallel alternatives rejected; production retains its existing combined cooperative scheduler. | The separate CPU pool roughly doubled throughput but worsened control-timer p99 to about 6.3 ms from 1.0 ms. The actual two-task scheduler improved risk completions from 13 to 25 and selection completions from 554 to 792 per approximately one-second window, but 15 alternating pairs showed median timer p99 worsening from 0.448 ms to 0.559 ms. The parallel-tier implementation and weak admission helper are test-only; no additional pricing tasks or CPU pool ship. |
| 5 | Implemented typed actual-trade reference checks. | Uses already validated convention accessors and generation bytes, avoiding reconstruction/traversal of the immutable dynamic representation. Existing frozen-trade/evidence and managed pricing bridges remain authoritative; no isolated whole-trade speedup is claimed. |
| 6 | Implemented one underlying read per underlying per bounded pricing batch. | Read-count and membership tests; each option still validates its own source/context/time. Independent books are not represented as one atomic market snapshot. |
| 7 | Implemented redundant-lookup removal and unlock before delivery notifications. Further lock partitioning deferred. | Existing flood/query/reset/retirement tests; book update itself was about 72 ns in the isolated probe with no meaningful regression. No evidence justifies changing coherent book ownership or adding lock shards. |
| 8 | Implemented notification only when pending bits change. | Drain/resignal race test and quiet cancellation tests. Duplicate pending notification cost fell about 45%. Lifecycle/error signals retain independent bits. |
| 9 | Deferred with evidence. | The SDK read can be partially advanced and has no qualified nonblocking owned-record drain API. Its buffered decoder already serves ready records without a new scheduled task for each await. No measured ingress bottleneck justifies adding a second record queue or weakening cancellation boundaries. |
| 10 | Implemented direct MessagePack frame, compression-envelope and chunk encoding, including allocation-light CLR GUID formatting. | Exact byte comparisons against the old Value encoders, varied payloads, compression thresholds and 1,024 GUID cases. |
| 11 | Implemented reuse of owned compression input storage and reduced temporary encoding graphs. Additional long-lived compression pools deferred. | Exact compression framing retained. Existing queued/in-flight byte limits remain; extra retained buffers would increase memory without a measured need. |
| 12 | Implemented immutable context, strategy and background-facts hash caches. | Effective-limit changes invalidate context digest cache; validity checks remain live. Exact hash tests and managed semantic/wire oracles. |
| 13 | Implemented owned sealed snapshot publication. | Production moves Snapshot into one Arc instead of deep cloning; borrowed constructor retained for compatibility. Pointer/byte tests and retained-reader lifecycle tests. |
| 14 | Implemented per-lane canonical futures/options binding indexes. | Asset separation, unknown-contract and replacement tests; old registration Arc released on cache replacement. Isolated 2,048-contract kernel improves about 93%, not a whole API latency claim. |
| 15 | Implemented immutable-facts fingerprint caching and one reference-cache snapshot per resolution. Broader delta planner deferred. | Existing date/source caches and unchanged-group maintenance already preserve connections; retained and removal/recovery tests qualify them. No full desired-state protocol rewrite was necessary or claimed. |
| 16 | Already satisfied by existing bounded batch ABI. | Native history accepts up to 4,096 records per call, four bounded readers and bounded four-entry channels. Existing history batch/stream/replay managed/native gates verify exact completeness/backpressure. No incompatible duplicate ABI added. |
| 17 | Implemented streamed snapshot encoding and reuse of exact preflight size, immutable hashes and stored encoded chunks. | Exact full snapshot/chunk bytes and array boundaries tested; no receiver delta protocol. |
| 18 | Implemented measured two-chunk cache pipeline. General publisher retains its existing 32-ready-message policy. | Existing dedicated two-entry/256 KiB cache queue remains bounded. With a fabricated 2 ms PONG delay, cache delivery improves about 39.5%; cancellation, actual PUB order and full-flush completion are tested. Broker flush is not durable acknowledgment. |
| 19 | Blocked by provider capability for incremental removal; controlled replacement retained. | Vendored Databento 0.58.0 `live/client.rs` exposes `subscribe` and `resubscribe`, but no unsubscribe operation. Add-only mutation cannot correctly implement arbitrary desired membership removal. No live-provider subscription experiment performed. |
| 20 | Deferred with evidence prerequisite. | Release already uses opt-level 3, thin LTO and one codegen unit. There is no representative accepted production training profile for PGO while this backend remains opt-in. No CPU-specific target or speculative compiler flag is shipped on microbenchmark evidence alone. |

### Timing interpretation and reproduction

Five alternating before/after pairs use warmed batches and separate allocation-count passes. Main probe percentiles are amortized batch operation times, not individual tick latency. The cache/policy probes link the preserved Release library and candidate with the same standalone `rustc -C opt-level=3 -C lto=off` settings. Local transport explicitly adds 2 ms per PONG; it is not production broker latency. The cooperative CPU experiment uses the same numerical cases on a two-worker runtime with comparable-duration control-timer windows.

Raw samples and immutable baseline files are under `.artifacts/rust-marketdata-performance/20261101/`. See [reproduction instructions](../../TomasAI.IFM.Application.MarketData.Rust/qualification/PERFORMANCE.md).

### Final accepted measurements

These are medians across five alternating pairs against the preserved original implementation. The final accepted version retains the original production pricing scheduler. Earlier `phase1`, `phase3` and `final` artifacts are retained experiment history; `accepted-*` files identify delivery evidence.

| Measured operation | Before | Accepted | Reduction |
| --- | ---: | ---: | ---: |
| Frame content, 128-byte payload | 3.477 µs | 0.311 µs | 91.1% |
| Complete frame encoding, 128-byte payload | 4.591 µs | 0.965 µs | 79.0% |
| Complete frame encoding, 4,096-byte payload | 5.239 µs | 1.566 µs | 70.1% |
| Immutable reference digest | 35.067 µs | 0.055 µs | 99.8% |
| Immutable policy hash | 2.169 µs | 0.067 µs | 96.9% |
| Pending notification | 27.8 ns | 15.4 ns | 44.5% |
| Instrument lookup kernel, 2,048 entries | 488.3 ns | 45.0 ns | 90.8% |
| Contract lookup kernel, 2,048 entries | 2,295.3 ns | 153.6 ns | 93.3% |
| Snapshot seal, 1,024 instruments | 9.490 ms | 7.614 ms | 19.8% |
| Cache publication construction, 1,024 instruments | 2.482 ms | 1.196 ms | 51.8% |
| Cache chunk encoding, 1,024 instruments | 144.5 µs | 76.4 µs | 47.1% |
| Local cache delivery with injected 2 ms PONG delay | 78.527 ms | 47.360 ms | 39.7% |

Across 128/512/1,024-instrument fixtures, cache construction improved 51.8–55.7% and snapshot sealing improved 17.0–19.8%. The existing dedicated two-entry/256 KiB cache queue is unchanged; preparing at most two chunks adds bounded temporary staging. No receiver delta protocol or durable-acknowledgment shortcut was introduced.

Pricing and book-update medians were broadly unchanged: the largest median regression among all 26 main scenarios was 1.1%. Allocation benefits remain measurable: a 2,048-option sweep fell from 2,529 to 2,241 allocations; American price/delta and risk batches halved measured allocations; frame-content construction fell from 17 to three allocations per frame. The separate conservative Facts probe reduced repeated fingerprint cost from approximately 1.052 µs to 8.2 ns with current policy hashing in both arms.

The main 31-batch selection measurements showed p99 medians 5.8% higher at 1,000 entries and 5.4% higher at 2,048. These are amortized batch maxima, not individual tick latencies, and individual paired changes varied in direction. A targeted 20-pair follow-up using the same measurement/allocator methods and preserved Release libraries did not reproduce the regression: constant-reader and synthetic locked-reader p99 improved 15–19%. Those standalone executables use opt-level 3 with executable LTO disabled, so their absolute timings must not be substituted for the main thin-LTO results. Both data sets are retained; no whole-application p99 improvement is claimed.

The parallel scheduler was rejected on stronger contrary evidence: 15 actual-scheduler pairs showed control-timer p99 rising from 0.448 to 0.559 ms despite substantially higher throughput. Its code is `cfg(test)` only, and the accepted production pipeline has no diff from the original scheduler.

### Final qualification and delivery

- **466 Rust tests passed**, zero failures, across 105 targets; 21 timing/conditional cases remain excluded from the standard run. Four real-duration Rust deadline cases and the explicit performance probes were run separately as described above.
- Formatting, strict Clippy with warnings denied and Rust documentation gates passed on the accepted source.
- **All 61 accepted offline qualification stages passed**, including 70 managed recovery tests, managed/native numerical and wire oracles, history/replay/live adapters, actual local NATS publication, disposable Postgres recovery/publication fixtures and 1,000 joined option-owner replacements.
- Exact differential checks include 1,520 C# pricing outcomes, 104 application MessagePack/digest cases, 10,000 quote-qualification cases and 80 actual-trade DTO/digest cases. Historical qualification matched 50,000 rows across 28 range/batch/cancellation cases.
- Accepted owner replacements measured p50 31.511 ms, p99 32.167 ms and maximum 43.308 ms. Socket joining, stale-generation rejection and stable-reader recovery passed; these are qualification timings, not a comparative speedup claim.
- Real deadlines completed at approximately Host 120.004 s, SDK 120.015 s, DNS 120.011 s, metadata 120.011 s and pricing 120.045 s, with required socket cleanup and no renewed budget or false cleanup proof.
- One existing C# compatibility test assumed nine fields although the existing status DTO already had ten. Its fixture now serializes the actual eight-field legacy DTO and verifies ownership defaults, appended recovery-audit roundtrip and legacy-reader compatibility. Production C# contracts were not changed. Backend Rust formatting was normalized to satisfy the existing gate; Rust.eGui and the legacy backend were outside this formatting scope.
- The isolated accepted Host package passed its own executable startup, readiness, scoped NATS status and joined-shutdown check. Its native DLL matches the exact standalone Release DLL used by accepted qualification.

Accepted native DLL SHA-256: `036a421a862ec9ed257a3bdeb93e565d4e35354ab72fe37e971b3d8434ffa83c`.

Evidence and handoff:

- [Accepted main timing summary](../../.artifacts/rust-marketdata-performance/20261101/accepted-summary.json), [component timing summary](../../.artifacts/rust-marketdata-performance/20261101/accepted-component-summary.json) and [selection-tail investigation](../../.artifacts/rust-marketdata-performance/20261101/selection-p99-summary.json).
- [Accepted qualification report](../../.artifacts/rust-marketdata-performance/20261101/accepted-qualification/results.json) and [exact packaged Host check](../../.artifacts/rust-marketdata-performance/20261101/accepted-package-host-qualification.log).
- [Accepted package manifest](../../.artifacts/rust-marketdata-performance/20261101/accepted-package/native-package.json), [build provenance](../../.artifacts/rust-marketdata-performance/20261101/accepted-package/performance-build.json) and [source manifest](../../.artifacts/rust-marketdata-performance/20261101/accepted-source-manifest.json). Original and accepted source archives and immutable binaries remain beside them.
- [Rejected actual-scheduler measurements](../../.artifacts/rust-marketdata-performance/20261101/actual-scheduler-summary.json). The older `package/` directory is explicitly marked as a rejected experiment; delivery is `accepted-package/`.

The deliverable is an isolated, opt-in package. No application was deployed or restarted, no backend selection was changed, and no commit or push was performed. Offline qualification does not establish live-provider or complete trading-application acceptance. Remaining provider and conditional limitations are the explicit dispositions above, not unfinished accepted code changes.

## Repository references

- [Existing Rust FFI implementation plan](MarketData-Rust-FFI-Implementation-Plan-v0.1.md)
- [Backend implementation status](../../TomasAI.IFM.Application.MarketData.Rust/docs/IMPLEMENTATION-STATUS.md)
- [Backend qualification report](../../TomasAI.IFM.Application.MarketData.Rust/docs/QUALIFICATION-REPORT.md)
- [Live processing](../../TomasAI.IFM.Application.MarketData.Rust/src/dataset/live.rs)
- [Coalesced pricing](../../TomasAI.IFM.Application.MarketData.Rust/src/option_chain/coalesced.rs)
- [Pricing engines](../../TomasAI.IFM.Application.MarketData.Rust/src/pricing/mod.rs)
- [Publication codec](../../TomasAI.IFM.Application.MarketData.Rust/src/messaging/codec.rs)
- [Ordered publisher](../../TomasAI.IFM.Application.MarketData.Rust/src/messaging/publisher.rs)
- [Offline qualification runner](../../TomasAI.IFM.Application.MarketData.Rust/qualification/Verify-Offline.ps1)
