# Rust MarketData performance verification

The 2026-11-01 baseline, raw timing samples, preserved binaries and source archive are under `.artifacts/rust-marketdata-performance/20261101/` from the repository root. They are local evidence, not deployment assets. The implementation plan and results are in `Documents/system/MarketData-Rust-Performance-Implementation-Plan-2026-10-31.md`.

## Reproduce the main comparison

Before editing, build and copy the original Release probe and DLL to a separate directory. Build the candidate with the same toolchain, Cargo lockfile, target and features. This run used Rust 1.97.1, Release optimization 3, thin LTO, one codegen unit, and the example's dev-dependency feature union in both arms. No fast-math or numerical tolerance changes are enabled.

```powershell
cargo build --locked --release --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --example performance
powershell -NoProfile -File TomasAI.IFM.Application.MarketData.Rust/qualification/Measure-Performance.ps1 -BaselineExecutable <preserved-before.exe> -CandidateExecutable TomasAI.IFM.Application.MarketData.Rust/target/release/examples/performance.exe -OutputDirectory <new-empty-run-directory>
```

Do not run qualification tests, builds or other benchmarks during comparisons. Keep applications and machine power policy consistent. The script writes five alternating pairs and executable hashes. Every scenario warms up, then records 31 batches; allocation instrumentation runs separately. Report medians across pairs. The p95/p99 values are percentiles of **amortized batch operation times**, not individual tick or whole-request latency. Instrument and contract lookup scenarios are isolated kernels; the remaining named operations invoke the actual library paths.

## Component probes

`cache_performance.rs` measures sealing, publication and chunk encoding for 128/512/1024-row fabricated snapshots. `policy_performance.rs` measures the existing immutable strategy fixture. `transport_performance.rs` uses a localhost TCP NATS fixture with an explicit 2 ms PONG delay and verifies publication counts and joined sockets. These are not live-provider or production-broker measurements.

Compile these standalone sources with `rustc --edition=2024 -C opt-level=3 -C lto=off -L dependency=<target/release/deps> --extern ifm_market_data_native=<matching-preserved-rlib> ...`. Supply matching `tokio` and `serde_json` rlibs as required. Add `--cfg optimized` for the owned-cache/two-chunk candidate arms. Do not combine rlibs and dependencies from different builds. The local run's dependency names and raw files are preserved in its artifact directory.

`cpu_performance.rs --multi --cooperative-only` compares the qualified American full-risk calculation on the two-thread runtime. It is an architecture experiment, not the production scheduler. Its separate blocking-worker arm was rejected because it worsened control-timer latency.

The actual scheduler has an explicit ignored test, `actual_pricing_scheduler_before_after_timings`, comparing the production combined loop and the rejected parallel-tier experiment with eight American options. Across 15 alternating pairs the experiment improved throughput but worsened median control-timer p99 from 0.448 ms to 0.559 ms; it is compiled only for tests. Production retains the combined cooperative loop. Compile with `cargo test --release --lib --no-run`, wait for all builds to stop, then run the emitted libtest executable directly with `actual_pricing_scheduler_before_after_timings --ignored --nocapture`. The analogous `immutable_fact_fingerprint_before_after_timings` test compares the actual uncached/cached background computation. These timing tests are deliberately excluded from normal regression runs.

## Correctness and packaging

```powershell
powershell -NoProfile -File TomasAI.IFM.Application.MarketData.Rust/qualification/Verify-Offline.ps1 -NativeProfile release -IsolatedManagedBuilds -IncludeNats -IncludePostgres -IncludeProductionDeadlines -IncludeSustained -OutputDirectory <new-qualification-directory>
powershell -NoProfile -File TomasAI.IFM.Application.MarketData.Rust/scripts/Build-NativeHost.ps1 -Configuration Release -OutputDirectory <isolated-package-directory> -ArtifactsDirectory <isolated-managed-intermediates>
```

Qualification uses fabricated local fixtures, unique NATS subjects and disposable Postgres fixtures. It does not establish live-provider acceptance. Packaging verifies that the copied DLL matches the selected native profile and does not change backend selection or restart an existing application. Use a fresh output directory for every package.
