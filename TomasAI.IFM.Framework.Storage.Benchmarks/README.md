# Framework.Storage benchmarks

`CommandLogProviderBenchmarks` compares only the database guard operation: the existing authoritative PostgreSQL
`command_log` table (`ON CONFLICT DO NOTHING`, current JSON/text payload) against an isolated ScyllaDB
`command_log_benchmark` table (`IF NOT EXISTS`, MessagePack/blob payload). The Scylla store is not registered in the
application runtime and does not shadow, replace, or dual-write the PostgreSQL command log. First-insert and duplicate
shortcut workloads run at 1, 16, and 32 concurrent requests; serialization is performed once in setup so database
latency and allocation are not conflated with codec cost.

```powershell
$env:DOTNET_ENVIRONMENT = 'Test'
$env:IFM_SCYLLA_TEST_CONNECTION = 'Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db'
$env:SCYLLADB_TEST_KEY = '{"userid":"...","password":"..."}'
$env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=event-source-test-db'
$env:POSTGRES_TEST_KEY = '{"userid":"...","password":"..."}'

dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- `
  --filter '*CommandLogProviderBenchmarks*' `
  --artifacts .test-results/benchmarks-command-log
```

Treat the results as a decision input, not a cutover. PostgreSQL remains authoritative until correctness, operations,
and representative sustained-load evidence support a separate migration decision.

`CommandDuplicateCoordinatorBenchmarks` measures the bounded process-local fast path without database latency:

```powershell
dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- `
  --filter '*CommandDuplicateCoordinatorBenchmarks*' `
  --artifacts .test-results/benchmarks-command-dedup-l1
```

`CompletedIdShortcut` measures IDs already in the 100,000-entry completed cache. `SameIdCoalescing` measures one
local reservation owner followed by same-ID local duplicates; the unit suite separately forces a genuinely concurrent
32-caller in-flight race and verifies one owner. Evicted IDs and independent application processes fall
through to the authoritative PostgreSQL path measured by `CommandLogProviderBenchmarks`.

`ScyllaBulkWriteBenchmarks` compares the former logged-batch write path with the per-call bounded-concurrency
production path. `PostgresBulkWriteBenchmarks` compares the former sequential prepared-command path with bounded
`NpgsqlBatch` chunks. Each benchmark creates one table in its dedicated test database and reserves negative partition
IDs. All benchmark rows are removed after the run.

`ScyllaItiQueryProjectionBenchmarks` is the SWO-05 real-provider before/after gate. It creates isolated canonical and
query-projection tables, seeds identical deterministic ITI keys, and compares the legacy `ALLOW FILTERING`
trend/mode maximum-sequence lookup with direct projection routing. It never reads or writes application tables.

```powershell
$env:DOTNET_ENVIRONMENT = 'Test'
$env:IFM_SCYLLA_TEST_CONNECTION = 'Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db'
$env:SCYLLADB_TEST_KEY = '{"userid":"...","password":"..."}'

dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- `
  --filter '*ScyllaBulkWriteBenchmarks*' `
  --artifacts .test-results/benchmarks-scylla
```

Run the SWO-05 query comparison against the same dedicated keyspace:

```powershell
dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- `
  --filter '*ScyllaItiQueryProjectionBenchmarks*' `
  --artifacts .test-results/benchmarks-swo05-iti
```

The generated `ScyllaItiQueryProjectionComparison.md` reports mean, p50, p95, and p99 workload latency from 100
measured single-query samples with outlier removal disabled, derived
queries/second, allocated bytes, and percentage changes at 4,096 and 32,768 canonical rows. The production migration
must rerun this exact benchmark on the same host and keyspace after the application projections are implemented; the
benchmark legacy CQL remains isolated here and is not an allowed application-storage exception.

The Scylla test keyspace replication must match the test cluster. The example above is normally run against one
local Scylla node, so `fund_test_db` must use replication factor 1. Do not lower a multi-node staging or production
keyspace merely to make a benchmark run: the production provider deliberately uses `LOCAL_QUORUM` and
`LOCAL_SERIAL` so projection state and LWT decisions remain replica-safe.

```powershell
$env:DOTNET_ENVIRONMENT = 'Test'
$env:IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=event-source-test-db'
$env:POSTGRES_TEST_KEY = '{"userid":"...","password":"..."}'

dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- `
  --filter '*PostgresBulkWriteBenchmarks*' `
  --artifacts .test-results/benchmarks-postgres
```

The live database comparison uses BenchmarkDotNet's in-process toolchain. This avoids a network-dependent restore of
an auto-generated child project and ensures both algorithms reuse the same test cluster and process conditions.

BenchmarkDotNet reports mean latency, throughput-equivalent operation time, allocated bytes, and GC collections for
100 and 1,000 rows across one and 32 partitions. The baseline ratio compares each redesigned case with the matching
legacy case under the same database conditions. At the end of a successful run, `ScyllaBulkWriteComparison.md` or
`PostgresBulkWriteComparison.md` is written under the BenchmarkDotNet results directory. It calculates the latency,
throughput, and allocation percentage change for each scenario, plus weighted overall throughput, mean scenario
latency, total allocations, and total GC collection changes. Target-specific global cleanup verifies that the measured
path actually persisted the expected row count before deleting its reserved rows; a silent partial-write run fails.

The ScyllaDB baseline and redesign have different semantics: logged batches are appropriate for small atomic mutation
groups, while ordinary bounded writes preserve token-aware routing and cap cross-partition concurrency. The redesign is
not expected to allocate less than one driver request containing an entire logged batch. Compare it for production
topology safety and sustained multi-partition throughput, not only for single-partition microbenchmark latency.
Each benchmark invocation contains one top-level caller, so this suite does not model aggregate backpressure among
several simultaneous bulk callers after removal of the provider-wide gate; use a separate load test for that capacity
limit. In-process managed-allocation figures include Cassandra/Npgsql background activity and should be treated as
directional alongside the latency and persisted-row checks.
