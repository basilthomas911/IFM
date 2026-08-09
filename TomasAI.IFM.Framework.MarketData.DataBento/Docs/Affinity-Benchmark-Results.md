# Databento affinity verification and benchmark results

## Implementation behavior

- The native C++ producer and dedicated managed drain are assigned different physical cores on the same NUMA node.
- Intel hybrid processors are detected with the Intel vendor identifier, CPUID hybrid feature bit, and per-logical-processor CPUID leaf `0x1A` core type. Only verified Core type `0x40` processors are selected; Atom/E-core type `0x20` processors are excluded.
- Windows can additionally use distinct CPU-set `EfficiencyClass` values as an OS-provided performance classification.
- Linux enumerates the process-allowed affinity mask and reads package, physical-core and NUMA topology from sysfs before applying the same Intel CPUID probe.
- Homogeneous CPUs and systems where performance-core classification is unavailable use distinct-core affinity and report `AffinityFallback`. This is the default because `AllowAffinityFallback` is true.
- Both endpoints read affinity back immediately after applying it. A mismatch faults startup rather than silently continuing.
- Optional residency diagnostics sample the current processor after every produced and drained record. They report sample count, unique processors, migrations, and samples outside the primary assignment. Tracking is disabled by default, so it does not add processor-query work to the production hot path.
- The synthetic-only forced-migration diagnostic alternates each endpoint between two different physical cores and verifies every change. It exists to prove the residency detector and to stress migration behavior; it is not a model of normal OS scheduling.
- `FeedCpuAffinityOptions.PinFeedThreads` is the default-on operational switch. Setting it to `false` leaves both dedicated feed threads under normal OS scheduling while retaining the affinity policy for later re-enablement. The benchmark's `--unpinned` mode exercises this switch directly.

## 2026-08-09 Windows benchmark

Host: AMD Ryzen Threadripper 1950X, 16 physical cores and 32 logical processors.

The host is homogeneous and therefore correctly selected `AffinityFallback`. Five fresh-process samples of 20,000,000 synthetic records were collected per mode. Processor residency was sampled for every record for the full duration of both threads.

| Mode | Median records/s | Mean records/s | Minimum | Maximum |
|---|---:|---:|---:|---:|
| Naturally unpinned | 9,415,769 | 8,699,295 | 5,864,987 | 10,787,685 |
| Pinned for full duration | 5,742,945 | 5,885,092 | 5,472,874 | 6,332,490 |
| Forced migration, randomized interval per run | 5,465,035 | 6,347,102 | 5,389,198 | 9,254,119 |

Every pinned sample resolved and continuously remained on the same placements:

- Native producer: processor group 0, logical processor 0, verified true.
- Managed drain: processor group 0, logical processor 2, verified true.
- Each endpoint: 20,000,000 samples, one unique processor, zero migrations, and zero off-assignment samples in every run.

The naturally unpinned runs recorded three native-producer migrations and fourteen managed-drain migrations in total. Four of five runs migrated at least one endpoint, so this is now a real full-duration scheduler comparison rather than an initial affinity read-back comparison.

The forced-migration intervals ranged from 27,975 to 82,953 records. Each endpoint made exactly the expected number of verified migrations in every run, for 1,852 migrations per endpoint across the five samples, and observed both assigned physical cores. This independently verifies that continuous residency accounting detects actual movement. Its throughput includes affinity-system-call and deliberate cache-migration costs and must not be interpreted as ordinary unpinned throughput.

The pinned median was 39.0% lower than the naturally unpinned median on this idle host, but its range was much tighter. That result does not support pinning as a maximum-throughput optimization on this older homogeneous Threadripper. Its potential production value is predictable placement and isolation under competing load, which requires a separate loaded-host benchmark. Intel hybrid P-core behavior must also be rerun on the target Intel host because this AMD system cannot exercise CPUID hybrid core types.

## Deferred busy-market acceptance test

The pinning recommendation remains provisional. Repeat the pinned/unpinned comparison during a representative high-volume window approximately 30 minutes before the futures market close, with the complete application workload running. Capture feed throughput, end-to-end p50/p95/p99/p99.9 latency, ring/channel high-water marks, backpressure and dropped-record counts, native and managed CPU utilization, processor migrations, GC pauses, ThreadPool queue length, and strategy/actor latency. Prefer simultaneous equivalent feeds when licensing and session limits permit; otherwise repeat matched windows on comparable active days. Retain pinning only if tail latency or loss behavior improves without moving contention elsewhere.

## Reproduction

Run every sample in a fresh process:

```powershell
dotnet run --project ./TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks -c Release -- --unpinned --records=20000000
dotnet run --project ./TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks -c Release -- --pinned --records=20000000
dotnet run --project ./TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks -c Release -- --forced-migration --records=20000000
```

The forced mode selects one interval from 10,000 through 100,000 records per process. Pass `--migration-interval=50000` when a fixed migration count is needed for controlled comparisons.

## Linux verification

The native library and synthetic test executable were compiled with the installed Ubuntu 20.04 WSL `g++` toolchain. All native tests passed, including `test_native_producer_affinity_is_verified`, which applies `pthread_setaffinity_np` and verifies the resulting singleton CPU mask with `pthread_getaffinity_np`.

The managed project also compiled successfully for `linux-x64`. A managed WSL test run was not available because that distribution contains a .NET runtime but no .NET SDK; the Linux-specific topology parser and affinity selection are covered by platform-neutral unit tests, while an actual managed Linux affinity run remains an acceptance check for the deployment host.
