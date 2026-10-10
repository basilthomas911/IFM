# PostgreSQL tuning benchmarks

Run from the repository root in PowerShell:

```powershell
scripts/Benchmarks/Compare-PostgresTuning.ps1
# Or select profiles:
scripts/Benchmarks/Compare-PostgresTuning.ps1 -OnlyProfiles Baseline,Memory1GB
```

This standalone BenchmarkDotNet project measures PostgreSQL 18.6 against dedicated Docker databases. It never accepts a development connection string. Windows clients connect only to localhost port 58118; the optional Linux client uses the isolated `pg-tuning` network alias. Each profile has a dedicated labelled container and volume, four database CPUs and 4 GiB memory. Durability remains enabled.

The runner seeds 500,000 one-kilobyte event payloads across 500 streams. Cases reuse this immutable seed within a profile, remove prior benchmark appends, reset stream versions and vacuum/checkpoint outside measurements. Append cases use batches of 1, 8, 32 and 128. Other cases cover latest snapshots, history reads, eight simultaneous commits, reads traversing the working set and a payload sort. Results use eight measured BDN iterations after three warmups; BDN confidence intervals describe iteration means, not individual request tail latency.

Two separate three-minute diagnostics offer 5,000 events per second across eight 32-event writers, force a 30-second checkpoint interval and record actual batch request p50/p95/p99, WAL and checkpoint counters. This checkpoint interval is a stress condition, not a recommended application setting. Sustained diagnostics are not BenchmarkDotNet results.

Profiles execute sequentially and their database volumes are removed after each run. A 3 GiB free-space guard protects Docker's shared filesystem. Cleanup verifies exact run labels; it never prunes unrelated Docker resources. `io_uring` is attempted with normal Docker security and recorded as unavailable if startup fails.

Raw evidence goes to `.artifacts/postgres-tuning/<run-id>/<profile>/`: settings, full BDN JSON/CSV, client and server logs, Docker resource samples, a sort EXPLAIN plan, and sustained diagnostic JSON. Linux client results include a change of client OS and CPU allocation as well as network placement, so they cannot isolate network cost alone.
