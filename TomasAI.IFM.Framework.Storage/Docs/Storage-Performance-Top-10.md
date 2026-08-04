# Framework.Storage performance review: top ten remaining issues

This review starts after commit `a751edd`, so it does not count the reflection-free ordinal result mapping, positional
ScyllaDB binding, deferred parameter sources, paged streaming, PostgreSQL queued batching, or the first bounded ScyllaDB
bulk writer as unresolved work. The following were the ten highest-impact issues still present in the PostgreSQL and
ScyllaDB providers.

| # | Provider | Area | Finding | Resolution |
| ---: | --- | --- | --- | --- |
| 1 | PostgreSQL | Latency | Ordinary multi-row commands still made one prepared-command round trip per row. | Stream values into bounded `NpgsqlBatch` chunks. `POSTGRES_BULK_BATCH_SIZE` defaults to 256 and is validated from 1 through 4,096. |
| 2 | PostgreSQL | Memory/GC | `ExecuteCommandAsync` accessed the compatibility list, eagerly materializing deferred input. | Consume `ReadParameterValues()` once and retain only the current batch. |
| 3 | PostgreSQL | Cancellation/threading | Command execution did not propagate cancellation into open, transaction, prepare, or execute operations. | Add the cancellable overload and pass its token through the complete asynchronous path. |
| 4 | PostgreSQL | Locking/latency | Queued execution opened an explicit transaction even when `useTransaction` was false; single statements also paid redundant transaction work. | Respect the queue flag and rely on PostgreSQL statement atomicity for single commands. Explicit transactions remain for requested multi-row all-or-nothing writes. |
| 5 | PostgreSQL | Threading/GC | Materialized reads used synchronous `Read` and synchronous disposal; immutable results allocated a growing `List<T>`. | Use asynchronous reads/disposal and a pooled temporary builder, then return an exactly sized caller-owned array without imposing a hidden disposal contract. |
| 6 | PostgreSQL | Connection latency/memory | Every repository operation rebuilt a credential-bearing connection string and bypassed an owned data-source cache. | Cache one thread-safe, lazily constructed `NpgsqlDataSource` per credential-free connection string and create logical pooled connections from it. |
| 7 | ScyllaDB | Locking/threading | A provider-wide semaphore could queue unrelated callers and retain cancelled-request leases. | Remove the shared gate. `SCYLLADB_BULK_MAX_CONCURRENCY` now limits workers per call, while the driver pool and ScyllaDB apply system-wide backpressure. |
| 8 | ScyllaDB | Latency/threading | Prepared statements used synchronous preparation and concurrent cache misses could prepare the same CQL more than once. | Cache `Lazy<Task<PreparedStatement>>`, prepare asynchronously once, and evict a failed preparation for retry. |
| 9 | ScyllaDB | Memory/threading | Provider cache contention could construct multiple clusters even though only one provider won `GetOrAdd`. | Cache `Lazy<IObjectRepositoryProvider>` with execution-and-publication semantics. |
| 10 | ScyllaDB | Memory/resources | Single-row and scalar reads did not deterministically dispose `RowSet`; cancellation could let a caller abandon an uncancellable driver request without transferring ownership of its eventual result. | Dispose result sets and transfer a cancelled request to a background drain that observes the driver task and disposes its eventual result. |

## Live benchmark evidence

The final semaphore-free BenchmarkDotNet reruns used the configured localhost test databases on August 4, 2026.
Across 100 and 1,000 PostgreSQL rows over one and 32 logical partitions, bounded `NpgsqlBatch` reduced mean scenario
latency from 322.823 to 84.763 ms (-73.74%), increased weighted throughput from 1,704 to 6,489 rows/second
(+280.85%), and reduced total managed allocation by 25.72%. No measured PostgreSQL scenario triggered a garbage
collection.

The historical binding microbenchmarks also remain positive. Positional single-row binding measured about 41–42 ns
versus 746–809 ns for cached reflection, with 264 versus 368 allocated bytes. Positional batch binding measured
3.93 microseconds versus 77.13 microseconds for 100 rows and 53.87 microseconds versus 768.15 microseconds for 1,000
rows, while reducing allocation by about 28%.

The ScyllaDB comparison is a semantic tradeoff, not an allocation win. The final selected-scheduler run used one local
Scylla node, test-keyspace replication factor 1, and the same `LOCAL_QUORUM` settings on both implementations. The old
path sends all rows as one logged request; the production path sends independently routed requests through a bounded
worker set. The logged request was therefore faster in this single-node test: at 1,000 rows it measured 44.565 versus
50.650 ms for one partition and 54.035 versus 57.276 ms for 32 partitions. Across the four scenarios, weighted
throughput changed from 20,989 to 17,970 rows/second (-14.39%) and mean latency changed from 26.204 to 30.607 ms
(+16.80%). Independent requests increased measured allocation from 3,856,208 to 21,261,568 bytes (+451.36%) and caused
three Gen0 and one Gen1 collections; the logged-batch cases caused none.

Most of that ScyllaDB allocation slope is driver request, protocol, task, continuation, and result state for one request
per row. Recovering logged-batch allocation numbers would require fewer requests. The generic provider does not know
partition-key boundaries, so automatically regrouping rows would reintroduce unsafe cross-partition batching and
coordinator hot spots. Explicit queued logged batches remain available for small mutation groups that genuinely require
atomicity and are designed to stay partition-local.

The earlier isolated semaphore-removal run remains the best gate-specific comparison: removing only the shared gate
changed mean scenario latency from 27.373 to 26.364 ms (-3.68%) and total allocation from 20,652,008 to 20,562,224 bytes
(-0.43%). Later binding and scheduling changes mean those absolute values are not directly comparable with the final
legacy-versus-production run. The current path preserves token-aware routing and a bounded per-call worker set while
simultaneous callers are no longer globally serialized.

Benchmark results depend on server topology, network, durability settings, and load. Rerun the included benchmarks
against the deployment being tuned rather than treating these localhost numbers as capacity targets. BenchmarkDotNet
also warned that these five-iteration localhost cases were shorter than its recommended 100 ms minimum, so the exact
latency percentages are directional; the allocation and one-request-versus-many-request tradeoff is consistent.
