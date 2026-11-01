# Rust.eGui performance implementation plan

Date: 2026-10-31  
Status: Complete for the recorded implementation/pilot scope; baseline preserved at commit `1be8410ce4ce2bbc4f740e4370649b6e581d95b1`. Changes are uncommitted.  
Scope: The 20 prospective improvements identified after the typed-model refactor.

Improve interaction latency, publication cost and sustained update handling while preserving trading behavior, exact financial values, backend authority and recovery semantics. Start with bounded render fixes, then address dispatch and incremental publication. Apply larger ownership and decoding changes only where profiles establish a material benefit.

The percentage ranges previously discussed are hypotheses about individual operations. They are not delivery commitments, application-wide speedups or additive savings. Every item receives a measured disposition: implemented, already satisfied, or deferred with evidence.

## Baseline and measurement

Before changing production code, preserve the current working-tree refactor as an identifiable baseline without mixing unrelated changes. Record the commit when available, source and fixture hashes, compiler, dependency lockfile, build profile, feature flags, machine, display scale and window dimensions. Preserve original binaries and named Criterion estimates; never overwrite an earlier baseline.

The latest completed refactor measurements remain historical evidence in `TomasAI.IFM.UI.Rust.eGui/README.md` and `.artifacts/egui-typed-remaining-20261030/`. Capture a fresh baseline for this plan. Existing CPU frame measurements showed mixed results: at 1,000 sparse plans, pointer-frame p95 increased from 3.746 to 4.718 ms. Profile that case before treating faster model conversion as a frame-rendering improvement.

Extend the existing `benches/ui_models.rs`, `tests/trade_blotter.rs`, `tests/portfolio_timing.rs` and timing summary tooling as needed. Add allocation counts/bytes and stage timing where those answer the specific performance question. Separate allocation instrumentation from latency runs when its overhead changes results.

| Scenario | Sizes and conditions | Metrics |
| --- | --- | --- |
| Option-chain rendering and refresh | 100, 1,000 and 10,000 contracts; unchanged repaint and partial quote update | Preparation, visible-frame p50/p95/p99, allocations, examined contracts |
| Trade plans and tabs | 100, 1,000 and 10,000 sparse plans; 1, 10 and 50 loaded trade tabs | Cold preparation, first prepared frame, pointer frames, inactive-document rebuild count, retained memory |
| Charts | 100, 10,000 and 100,000 points; pan, zoom, hover and live append | Preparation, frame latency, point copies, allocation bytes, visual/selection parity |
| Position history | Opening only, 100, 1,000 and 10,000 retained positions | Insertion, ordered access, selected-position lookup and memory |
| Publication bursts | Deterministic 1, 10 and 100 updates/second; additional short overload burst | Source-to-UI age, queue depth, coalesced count, frame p95/p99, terminal outcome delivery |
| Portfolio and supporting views | Existing 1/100-fund fixtures, large scheduler output and multiple loaded views | Publication parity, encoding cost, repaint allocations and memory |
| Runtime and I/O pressure | Idle, delayed reads, rapid navigation, concurrent independent trades and diagnostic bursts | Wakeups, dispatch delay, task/queue bounds, read cancellation, command response latency |

Use the same synthetic data and deterministic arrival schedule on both sides. For short comparisons, collect at least five alternating before/after pairs. Retain Criterion confidence intervals and raw per-run results. Do not run compilers or competing benchmarks during measurement. CPU-only egui tests and real-window/GPU measurements must be reported separately; sustained emulator runs do not establish live-broker latency.

Respect existing production capacity bounds. Larger stress fixtures exercise the relevant algorithm in isolation; they do not authorize increasing subscription, history, request or UI retention limits.

## Delivery sequence

| Phase | Improvements from the original list | Deliverable and exit condition |
| --- | --- | --- |
| 0 — Baseline | Measurement prerequisite | Reproducible baseline, profiles of frame regressions, scenario fixtures and result template |
| 1 — Render and history fixes | 1, 4, 7, 18, 19 | Remove repeated grid, chart, history, tab and log preparation; preserve selection, ordering, layout and accessibility |
| 2 — Dispatch and responsiveness | 8, 11, 15, 20 | Event-driven dispatch, bounded UI intake, cancellation of obsolete reads and buffered diagnostics; no lost outcomes or shutdown hangs |
| 3 — Incremental publication | 5, 6, 13, 3, 9, 12 | Share immutable data, coalesce replaceable snapshots and rebuild only changed/needed sections; generation/scope checks remain intact |
| 4 — Native transport and decode | 2, 14 | Pilot direct typed native delivery and direct contract decoding; retain wire adapters where required and expand only after parity and benefit are demonstrated |
| 5 — Profile-led scaling | 10, 16, 17 | Conditional chart sampling, shorter read locks and bounded CPU workers, selected from remaining measured bottlenecks |
| 6 — Final verification | All implemented items | Combined before/after report, complete compatibility checks, sustained-load verification and documented dispositions for all 20 items |

Use one focused change per improvement where practical. Benchmark against both the immediately preceding implementation and the preserved overall baseline so interactions and regressions remain visible.

## Implementation work packages

The identifiers below retain the original ranking. Phase order determines implementation order.

| ID | Implementation and primary code area | Dependencies | Required verification |
| --- | --- | --- | --- |
| P01 | Prepare immutable strike rows and call/put indexes when a market publication changes. Cache nearest-strike and selection lookups separately. Update `trading/trade_workspace/order_layout.rs` and its display preparation. | Phase 0 | Same ordering, duplicate handling, stale-quote marking, selection and click targets. Count full-chain scans during unchanged paint; none should remain. Benchmark refresh and pointer frames separately. |
| P02 | Introduce a typed native publication envelope carrying shared immutable sections. Pilot the monitor path, then broker/portfolio paths if beneficial. Keep MessagePack adapters for existing boundaries and compatibility fixtures. | P03, P13 | Exact managed display parity, ownership/thread safety, publication ordering, native stack budget and retained-memory bounds. Compare end-to-end publication CPU/allocations, not just one eliminated encode. |
| P03 | Replace publication-wide cache invalidation with explicit section revisions or immutable section identities. Separate market, plan, history, account, evidence and selection dependencies in `display_cache.rs`. | P13; P05 coalescing rules | Dependency tests covering every input, stale-scope rejection and unchanged-section reuse. No pointer-only repaint rebuilds. Exercise changes to locale, scale, theme and selection where relevant. |
| P04 | Borrow/shared-own prepared chart points through rendering; remove repeated vector copies in `iron_condor.rs`. Keep mutable live accumulation separate from immutable historical series. Verify the pinned plotting API permits the intended ownership path before choosing an adapter. | Phase 0 | Identical plotted coordinates, live append order, chart clock, hover and selected points. Benchmark allocations/bytes and frames at all chart sizes. |
| P05 | Coalesce only replaceable market/display snapshots before expensive publication construction, keyed by owner/scope/generation. Process required domain/history transitions before replacing their display snapshot. | Phase 0; feed P03 | Burst tests with sequence changes, owner changes, missing data and slow UI. Command outcomes, errors requiring acknowledgement, EOD transitions and recovery receipts must remain observable. Measure freshness and discarded redundant work. |
| P06 | Prepare visible-cell text on data/format changes. Cache decimal/date/percentage/status output in typed presentation sections; invalidate on locale/format changes. | P03; start with one profiled grid | Golden formatting and missing/invalid-value tests. Exact decimal authority stays in backend/command values. Compare allocation counts and frames, including memory cost of cached text. |
| P07 | Maintain ordered history keys as snapshots arrive. Borrow rows and locate a selected position directly instead of cloning/sorting the full collection in `state/monitor_cache.rs`. | Phase 0 | Existing chronological/tie order, replacement, opening row, duplicate suppression, route/sequence fences and EOD precedence. Benchmark insert, rows and selection independently. |
| P08 | Wake `app/runtime.rs` on intents, results, task completion, startup and shutdown. Retain timers only for actual scheduled work and bounded retry needs. Coalesce repaint requests per dispatched batch. | Phase 0 | No missed wakeups, busy loop or channel starvation. Verify backpressure, fair progress and shutdown with empty/full queues. Measure idle wakeups and intent-to-dispatch latency. |
| P09 | Reuse encoded immutable document sections when a wire publication is still required. Key cached bytes by exact section content/revision and format version. Avoid redundant implementation for paths superseded by P02. | P03 | Byte/semantic parity as appropriate, invalidation tests, scope isolation, bounded cache retention and encoding/allocation measurements. |
| P10 | Add a display-only multiresolution chart representation selected by viewport and pixel width. Preserve extrema, boundaries and explicitly selected/important points; zoom resolves to adequate detail. | P04; measured remaining chart bottleneck | Full source data remains available for calculations, commands and exact inspection. Test spikes, gaps, duplicate timestamps, pan/zoom, tooltips and selection. Measure visual fidelity and real-window frame cost. |
| P11 | Bound update intake in `main.rs` by elapsed frame budget or batch size. Leave remaining work queued and request another frame. Keep ordered command outcomes and account for oldest-update age. | P08 | Sustained arrivals must not prevent painting or starve outcomes. Compare p95/p99 frames, queue age and completion delivery; tune budget from baseline rather than fixing an arbitrary message count. |
| P12 | Keep inactive trade state authoritative while deferring expensive display construction. Invalidate hidden sections on changes and build the correct revision when activated. | P03, P05 | Tab isolation, close/reopen, notification processing and pending command visibility. Include first-activation latency, not just background CPU savings, in acceptance. |
| P13 | Replace demonstrated deep snapshot copies with shared immutable ownership across realtime, position, plan and display sections. Keep edit buffers and sealed command evidence independently owned. | Phase 0 | No accidental mutation, stale aliases or unbounded retention. Assert identity/revision behavior; measure copied bytes, allocations and RSS/retained heap across tab open/close cycles. |
| P14 | Pilot direct MessagePack decoding for one high-volume generated read contract, bypassing the temporary generic tree. Extend generation rather than creating a second handwritten ABI definition. | P02 pilot assessment; baseline decode profile | Same positional slots, retired gaps, enum behavior, optional/missing fields, exact decimals, UTC precision, unknown tails and invalid-input errors. Keep raw wire evidence for lossless-edit/sealed-command paths. |
| P15 | Give read-only load groups cancellation and request-generation ownership. Supersede stale hydration when selection/scope changes and release any owned subscriptions/leases correctly. | P08 | Rapid A→B→A selection, close during load, timeout, late replies and cancellation cleanup. Never cancel a submitted write as a substitute for unknown-outcome reconciliation. |
| P16 | Split selected read flows into capture/fetch/apply: capture immutable scope/revision under the lock, perform reads outside it, then validate before applying. Preserve serialization for mutations and commands. | P15; measured lock contention | Concurrent read/write and scope-change tests; results from stale captures cannot replace newer state. Measure lock-wait duration and end-to-end response times with delayed reads. |
| P17 | Move demonstrated heavy CPU work onto a bounded compute executor. Set concurrency/queue limits and cooperative cancellation or stale-result rejection; avoid spawning a task per row/update. | P08, P15; worker-starvation profile | Worker saturation, shutdown and panic/error propagation. Verify no task explosion and no financial calculations move to UI code. Compare service responsiveness and total CPU, including handoff overhead. |
| P18 | Cache tab display text, measured widths and stable control IDs in `workspace_tabs.rs`. Borrow tab records during paint and invalidate geometry on font, DPI, locale or title changes. | Phase 0 | Selection/close hit targets, scrolling, long labels, focus and accessibility. Benchmark many tabs and memory use. |
| P19 | Cache scheduler-output line offsets by immutable output revision. Borrow visible slices instead of collecting all lines during every paint in `operations/scheduled_tasks.rs`. | Phase 0 | Unicode, empty/trailing lines, append/replace, scrolling and changing selected run. Benchmark large output before/after and confirm unchanged rendering. |
| P20 | Introduce an owned bounded diagnostic writer with reusable handles, batched writes and explicit flush/shutdown behavior. Record overload and I/O failures visibly. Restrict this change to diagnostics. | Phase 0; integrate with P08 shutdown | Burst, rotation, disk failure, queue saturation and shutdown tests. Do not weaken command/financial-journal flush, acknowledgement, deduplication or crash-recovery guarantees. Measure producer latency and loss counters. |

## Acceptance and compatibility

Correctness is a hard gate. Preserve exact decimal text and identifiers, backend financial authority, immutable command receipts, unknown-outcome reconciliation, EOD behavior, stale-generation rejection, subscription ownership and managed interoperability. Do not introduce Aspire, Keycloak/OAuth changes, provider/API changes or database migrations into this performance work.

For each item, define the primary metric and target before editing. A useful initial target is at least a 15% repeatable reduction in the affected CPU/allocation stage, or a material reduction in measured p95/p99 latency, wakeups or queue age. This is a screening target, not a guaranteed result. A change below that target may still be justified by a demonstrated allocation, scalability or responsiveness benefit; record the reason.

Treat a repeated greater-than-5% regression in relevant frame p95/p99, first-activation latency, overall load latency or retained memory as requiring investigation. Compare absolute changes and measurement spread before deciding; do not hide regressions behind faster microbenchmarks. Revert or defer an optimisation when its benefit does not justify complexity or it compromises correctness. Add performance regression checks only where the environment and signal make them reliable; avoid brittle absolute timing assertions in normal unit tests.

Use focused unit tests for changed ordering, invalidation, scheduling, cancellation and ownership behavior. Extend existing synthetic C#/Rust fixture parity tests instead of maintaining duplicate expected contracts. Run the affected integration scenarios after each phase; run the complete native gate, isolated backend suite, console-intake verification, cross-language journal checks, generator checks and original-hybrid ownership audit at final acceptance. Discover the current test counts from the runner rather than hardcoding historical counts.

## Decisions resolved by measurement

| Decision | Default approach | Evidence required to change or proceed |
| --- | --- | --- |
| Direct typed native delivery versus encoded section reuse | Pilot typed delivery on monitors; reuse encoded sections only at remaining wire boundaries | End-to-end CPU/allocation gain, manageable retained memory and unchanged compatibility |
| Cache ownership and invalidation | Shared immutable sections with explicit revisions; keep mutable edit state separate | Dependency tests plus no retained-snapshot leak across repeated tab cycles |
| Coalescing and frame budget | Replace only display snapshots; deliver outcomes in order | Burst freshness, bounded queues and outcome delivery with a slow UI |
| Chart sampling | Full fidelity until profiles establish a plotting bottleneck | Significant rendering cost reduction with spike/selection/zoom fidelity |
| Lock splitting and CPU workers | Preserve existing behavior until contention/starvation is demonstrated | Measured wait time or service latency and safe capture/apply or bounded handoff design |

## Completion report

Keep a checklist for P01–P20 with its disposition, implementation commit where available, before/after artifact paths, correctness checks, primary metric, regressions and remaining limits. Report phase-level and overall measurements separately. Link the final summary from the Rust.eGui README and retain reproducible benchmark commands and raw evidence.

Begin implementation with Phase 0, then P01, P04 and P07, followed by P18/P19 and the dispatch work in Phase 2. Reassess the remaining order after those measurements; all 20 retain a recorded disposition even when a conditional item is deferred.

## Implementation dispositions

All 20 items have a final disposition: 15 implemented changes/pilots, four existing
capabilities retained, and one evidence-based deferral. The [results report](Rust-eGui-Performance-Results-2026-10-31.md)
records measured gains, the small-input merge cost, original and follow-up frame
results, complete native/protocol/journal verification and measurement limits.

The preserved baseline is commit `1be8410ce4ce2bbc4f740e4370649b6e581d95b1`.
Source/archive hashes and run artifacts are under `.artifacts/egui-performance-plan-20261031/`.
The following dispositions passed the reported correctness gates. Performance evidence and its limits are recorded in the results report.

| Item | Current disposition | Implementation or evidence |
| --- | --- | --- |
| P01 | Implemented; verified | Publication-owned strike/call/put indexes and nearest strike; indexed contract-ID merging preserves first-match behavior. |
| P02 | Native monitor-plan pilot implemented | Shared typed plan payloads reach display preparation directly; compatibility bytes are materialized lazily. The pilot measures 6.22× faster plan publication/display preparation. Further broker/portfolio boundary expansion remains conditional on a separate profile; this delivery retains the planned pilot scope. |
| P03 | Section reuse implemented; verified | Payload-owned caches retain declared concrete market/index, chart, plan, history, account, execution/evidence and selected-leg records. Replaced payloads invalidate independently; context and field overrides still rebuild for each publication. |
| P04 | Implemented; verified | Cached PlotPoint buffers are borrowed by both points and lines; pointer-only frames do not rebuild chart arrays. |
| P05 | Existing behavior retained | Monitor events already process state/history before a 50 ms dirty-publication flush, with latest-only watch delivery. Command outcomes remain separate. |
| P06 | Implemented for option cells | Only requested cells are formatted, then reused. The cache is bounded to 1,024 contracts and discarded with its immutable publication. Off-screen chains are not eagerly formatted. |
| P07 | Implemented; verified | Ordered history keys maintained during acceptance; row reads borrow snapshots and selection clones only its selected snapshot. |
| P08 | Implemented; verified | Notify-backed bounded action channels; dispatch selects startup, task results, sidebar and wake notifications. Full-queue retry wakes when the UI drains; monitor coalescing timers poll only while dirty. |
| P09 | Implemented for monitor sections | Plans materialize compatibility bytes lazily; unchanged charts reuse encoded bytes by content revision. Other monitor rows reuse identical immutable payloads after byte comparison. All producer cache entries are weak. |
| P10 | Additional sampling deferred | Existing six-hour clipping and latest-sample-per-minute policy bounds the spread plot to at most 361 points. A dense-input regression verifies that bound. Further sampling would change display semantics without an established remaining point-count problem. |
| P11 | Implemented; verified | UI intake budgets two milliseconds or 32 updates per frame, allows at least one update to progress and schedules another repaint when needed. |
| P12 | Existing behavior retained | Hub.publish appends detailed data only for the active owner; inactive owners retain their authoritative state and contribute tab metadata. |
| P13 | Implemented for publications | Wire-row clones share immutable storage. Native history plans omit the same heavy fields as the existing history wire format, then share compact typed payloads and prepared display records. Record/index/chart caches release with their publications; producer weak references do not retain inactive payloads. |
| P14 | Streaming option-chain pilot implemented | Two readers generated from the existing contract metadata reuse scalar validation and stream contract arrays; framing, depth, size, query deadlines and error auditing remain bounded. |
| P15 | Existing monitor cancellation retained | Replaced background slots abort prior tasks; owner disposal aborts its jobs. Incarnation/detail/chart generation checks reject late results after close/reopen or navigation. |
| P16 | Existing monitor capture/fetch/apply retained | Chart/detail hydration captures state under short locks, awaits reads outside locks and validates scope/generation before applying. Command controller serialization is retained. |
| P17 | Implemented for large option decodes | Expanded responses of at least 256 KiB use a two-job compute pool. Cancelled waiters discard results; running jobs retain permits until completion. Smaller decodes stay inline. |
| P18 | Implemented; verified | Prepared tab rows/IDs/captions are borrowed during paint. egui's existing font/DPI-aware galley cache remains responsible for text measurement. |
| P19 | Implemented; verified | Scheduler output retains Unicode-safe line ranges; visible rows borrow slices rather than splitting the entire output each frame. |
| P20 | Implemented for non-durable timing diagnostics | Bounded writer reuses buffered file handles, rotates files and flushes/joins on shutdown. Rejection and I/O failures are visible; synchronous durable error logs and financial journals are unchanged. |

The row cache accepts only an explicit sealed list of concrete presentation schemas.
Its internal type check prevents reusing one schema as another. It owns at most one
prepared record per wire payload, and raw bytes remain the compatibility source.
Presentation normalization does not alter command or journal authority.
