# Trade-view immediate-mode CPU benchmark

Measured 2026-10-17 on Windows, AMD Ryzen Threadripper 1950X (16 cores / 32 logical processors), rustc 1.97.1, x86_64-pc-windows-msvc, egui/eframe 0.32.3. The running application's packaged executable was byte-identical to the unoptimized debug executable when investigation began.

## Results

Milliseconds per headless CPU frame; median / p95. Each case replays 60 mouse-movement frames after five warmups in the loaded Iron Condor trade view at 1200 × 800 with the indicated number of plan rows. This measures actual egui layout/render preparation plus the egui_kittest accessibility/test harness, **not GPU presentation, Windows input-queue latency, broker response time, or end-to-end native application frame rate**. No build or other test workload ran concurrently with these measurements; existing application processes remained running. These are single-run local development measurements, not statistical confidence intervals or performance guarantees.

| Plan rows | Debug before | Debug after | Optimized before | Optimized after | Optimized median speedup |
|---:|---:|---:|---:|---:|---:|
| 100 | 50.35 / 56.37 | 46.32 / 49.52 | 4.52 / 5.06 | 3.70 / 4.41 | 1.22× |
| 1,000 | 64.83 / 71.52 | 45.69 / 52.36 | 8.31 / 9.35 | 3.91 / 6.00 | 2.12× |
| 10,000 | 230.46 / 250.09 | 46.95 / 50.82 | 55.91 / 62.28 | 4.38 / 4.84 | 12.77× |

The cache removes most history-size-dependent work from repainting. The large remaining debug-build overhead is why interactive startup now defaults to optimized Rust. The fixture uses generic saved plan rows, a 36+ column grid and unavailable-value cells; a live publication containing larger risk documents may cost more to prepare. The harness also scans its fixture rows before drawing, so this is not a pure renderer-only timer. Functional click/drag tests validate the same input/render path, but these timing samples replay mouse movement rather than measure physical click-to-photon latency.

## Publication-arrival profile

After each steady-state case, five publications are explicitly invalidated and prepared through the production worker entry point. Preparation is timed separately from the next GUI frame. These preparation samples execute that entry point synchronously in the test for measurement; in the application it runs on the existing bridge intake worker before enqueueing the publication. The GUI frame consumes the resulting immutable prepared model.

| Plan rows | Preparation CPU median | First GUI frame after preparation, median |
|---:|---:|---:|
| 100 | 0.19 ms | 3.98 ms |
| 1,000 | 2.04 ms | 4.44 ms |
| 10,000 | 22.21 ms | 5.21 ms |

An intermediate profile exposed approximately 16 ms for the first GUI frame at 10,000 rows despite precomputation. Moving destruction of retired histories off the GUI thread reduced the final measured first-frame median to 5.21 ms. The figures in the table are the final run. Preparation time still contributes to data arrival latency; this change protects input responsiveness and does not make large publications free.

## Changes

- Decode and group established trades, positions, limits, realtime snapshots, observations, plans and workspace tabs once per publication. Cache plan display projections, Eastern timestamp overrides, action-reason column presence, chart history points and first-plan-by-date lookups. Table rendering still virtualizes rows and formats visible cells.
- Use a native-only publication identity, not backend generation alone: two deserialized publications at the same generation receive different cache identities. Restarted generations, new tabs, new data and changed row counts cannot reuse an old display model. In-place native fixture edits call `invalidate_render_cache`; production wire publications are replaced, not mutated.
- Prepare immutable display data on the existing bridge intake worker. Mouse-only frames borrow the cached model. Small four-leg summaries still bind on each immediate-mode frame; no local financial calculation or managed risk rule was changed.
- Retire old publications and display models through one cleanup worker with capacity for **two queued items**, plus the item being processed. On saturation/disconnection it falls back to local destruction rather than blocking or retaining an unbounded backlog. This can still cause a transient frame cost under overload. Closing the last trade releases its cached model. Only the active publication is cached by the view; prior trade histories are not retained indefinitely.
- `Build-RustUi.ps1` defaults to optimized Rust, including the normal VS Code build/start dependency. Use `-Release:$false` when an unoptimized host is explicitly required. Managed build configuration and trading state are unchanged.

Caching trades additional resident decoded data for fewer per-frame allocations. Full history preparation remains proportional to publication size; no peak-heap or process-wide allocator claim is made here.

## Validation

40 targeted tests passed across cache lifecycle, realtime value binding, the trade editor/blotter, trade workspace, wire encoding and grid borders. This includes same-generation publication replacement, exact cache reuse, explicit invalidation, worker-model reuse, eventual release after closing, live plan refresh, tab changes, both-axis wheel input, scrollbar clicks/drags and row virtualization. Two cross-language fixture tests remained opt-in; the CPU benchmark was run explicitly despite its ignored-by-default marker.

All-target compilation and the optimized native build are checked separately. The tests contain no broker submissions or live feed mutations. The user's running UI was left intact; restart through the normal build/start workflow to use the changes.

## Reproduction and raw data

From the repository root:

```powershell
$env:IFM_TRADE_FRAME_BENCHMARK = "$PWD/.artifacts/trade-frame-current-release.json"
cargo test --release --manifest-path TomasAI.IFM.UI.Rust/Cargo.toml --test trade_blotter benchmark_trade_view_mouse_frames -- --ignored --nocapture
# Repeat without --release to measure the unoptimized build separately.
cargo test --manifest-path TomasAI.IFM.UI.Rust/Cargo.toml --lib --test trade_blotter --test trade_workspace --test grid_column_borders --test wire
cargo check --manifest-path TomasAI.IFM.UI.Rust/Cargo.toml --all-targets
cargo build --release --manifest-path TomasAI.IFM.UI.Rust/Cargo.toml
```

The before renderer is based on commit `12ef2b4f0` plus the preceding scrollbar fix. Exact pre-cache renderer snapshots are retained as [Iron Condor source](before-iron-condor.rs.txt) and [workspace tabs source](before-workspace-tabs.rs.txt). Iron Condor baseline SHA-256: `E1EC317136E84403E0FBA104164F9B7DCF404E873939FB9F2994C9BE49CF1AC6`.

For a before rerun, use an isolated checkout of the final source, replace those two renderer files with the saved baseline snapshots, and run the same benchmark. The newer cache/preparation helpers are unused by that baseline renderer during timed mouse frames. Do not overwrite the working production source to run the comparison. The separate publication-preparation samples are after-only diagnostics and have no baseline counterpart.

Raw samples summaries: [before debug](before-debug.json), [after debug](after-debug.json), [before optimized](before-release.json), [after optimized](after-release.json). Each JSON includes sample count, median, p95 and maximum; after runs also include preparation and first-frame medians. Source fixture and timing loop: `tests/trade_blotter.rs::benchmark_trade_view_mouse_frames`.
