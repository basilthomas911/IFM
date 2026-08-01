# Databento Phase 6 implementation

Phase 6 is code complete as of 2026-08-01. Runtime qualification evidence that
requires a production-like host, live credentials, or a long-running soak is
tracked separately and does not block later code completion.

## Recovery and readiness

- `DatabentoRecoveryOrchestrator` closes the entry gate on `Suspect` and keeps it
  closed through `Recovering` and `Faulted`.
- Recoverable connection, pressure, and continuity faults use the exact
  1/2/5/10/30-second schedule, at most five stop/dispose/recreate/start attempts,
  and a 30-second budget for each attempt.
- Authentication, invalid-request, provider, and unresolved-symbol faults fail
  immediately without retry.
- Readiness requires authentication, every subscription acknowledgement, replay
  completion, verified continuity, required baselines, and a complete immutable
  definition set when definitions are recovered.
- `TimestampReplayCursor` implements trade/MBP-1 replay from the saved timestamp
  while discarding earlier records and the saved count of records at the exact
  timestamp.
- `MboRecoveryBaseline` enforces reset, ordered snapshot reconstruction, snapshot
  completion, and the first continuous live boundary before readiness.

## Cold-path monitoring

- `DatabentoFeedMonitor` runs on a normal-priority background thread. It polls
  `GetHealth()` every second and exports aggregate snapshots every five seconds;
  neither exporter nor alert sink runs on the producer or drain thread.
- A deterministic `PollOnce()` path and injected `TimeProvider` support fake-clock
  tests without sleeping.
- Thresholds implement native-ring warning/critical at 50/75 percent, managed
  channel warning at 75 percent for two polls and critical at 90 percent, pool
  warning at two free batches and critical at zero, all channel-full episodes,
  channel-full waits over 100 ms, and four consecutive drain pass-limit polls.
- Fault state or terminal status immediately marks the feed `Suspect` and closes
  the entry gate. Unchanged alert conditions are emitted at most once per minute.

## Deployment

- The managed module resolves `databento_feed_native` only from
  `runtimes/<current-rid>/native`; unsupported OS/architecture combinations and
  missing RID assets fail with detailed exceptions.
- Windows build targets copy the bridge and dependencies to
  `runtimes/win-x64/native`. `build-native.sh` creates the matching
  `runtimes/linux-x64/native` package.
- The dependency remains pinned to Databento `v0.62.1`, immutable source commit
  `a37965590f6776ac9659ff496f91fb16c81f76b3`, the checked-in vcpkg baseline, and
  native ABI version 1.

## Qualification gates

`DatabentoSyntheticQualificationProbe` collects native-to-managed latency,
throughput, sequence-integrity, duration, and post-warm-up allocation evidence.
`DatabentoQualificationGate` provides deterministic pass/fail evaluation for:

- 1M records/s: p50 <= 50 us, p99 <= 250 us, p99.9 <= 1 ms;
- 5M records/s: p99 <= 500 us, p99.9 <= 2 ms;
- 10M records/s burst: p99 <= 1 ms, p99.9 <= 5 ms;
- zero post-warm-up allocation, record loss, ordering errors, and handle growth;
- throughput regression no greater than 10 percent and p99 regression no greater
  than 20 percent;
- 30-minute pre-production and 24-hour production soak durations.

## Verification and deferred evidence

The deterministic managed suite passes 64 tests, including Phase 6 recovery,
readiness, replay, monitoring cadence/threshold/rate-limit, RID path, endurance,
and regression-gate tests. The Windows synthetic native target is rebuilt as part
of that run and the existing lifecycle/ABI coverage remains green.

The following evidence remains intentionally deferred until the required host and
runtime window are available:

1. Windows Release and Linux Release package execution on their target hosts.
2. 1M/5M sustained, 10M 10-second burst, and 2x replay-load measurements on the
   production hardware profile.
3. The 30-minute strict run and 24-hour production soak with allocation, handle,
   loss, ordering, and latency reports retained as artifacts.
4. Credentialed live recovery and smoke confirmation. Use
   `IFM_RUN_DATABENTO_SMOKE_TESTS=1` with `DATABENTO_API_KEY`; use
   `IFM_RUN_DATABENTO_INTEGRATION_TESTS=1` for provider-connected negative cases.

These are release-acceptance evidence items, not unfinished Phase 6 code.
