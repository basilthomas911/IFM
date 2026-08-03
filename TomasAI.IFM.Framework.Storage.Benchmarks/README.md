# Framework.Storage benchmarks

`ScyllaBulkWriteBenchmarks` compares the former logged-batch write path with the bounded-concurrency production path.
It creates one table in a dedicated test keyspace and reserves negative partition IDs. All benchmark rows are removed
after the run.

```powershell
$env:DOTNET_ENVIRONMENT = 'Test'
$env:IFM_SCYLLA_TEST_CONNECTION = 'Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db'
$env:SCYLLADB_TEST_KEY = '{"userid":"...","password":"..."}'

dotnet run -c Release --project TomasAI.IFM.Framework.Storage.Benchmarks -- `
  --filter '*ScyllaBulkWriteBenchmarks*' `
  --artifacts .test-results/benchmarks
```

The live database comparison uses BenchmarkDotNet's in-process toolchain. This avoids a network-dependent restore of
an auto-generated child project and ensures both algorithms reuse the same test cluster and process conditions.

BenchmarkDotNet reports mean latency, throughput-equivalent operation time, allocated bytes and GC collections for
100 and 1,000 rows across one and 32 partitions. The baseline ratio compares each redesigned case with the matching
legacy case under the same cluster conditions. At the end of a successful run, `ScyllaBulkWriteComparison.md` is
written under the BenchmarkDotNet results directory. It calculates the latency, throughput, and allocation percentage
change for each scenario, plus weighted overall throughput, mean scenario latency, total allocations, and total GC
collection changes.
