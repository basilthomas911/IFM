# In-Memory Event-Sourced Command Actor Verification

**Version:** 1.0  
**Date:** 2026-09-12  
**Scope:** Command audit prerequisite, atomic event persistence, generic resident actor, and OptionTrade pilot

## Implemented result

- New command-audit rows store versioned, uncompressed MessagePack plus SHA-256 identity. Existing JSON rows and
  columns remain intact and readable.
- Standard CommandActors use an event-driven bounded PostgreSQL writer. The writer sleeps on its channel and performs
  no database polling.
- The completed and in-flight command-ID coordinator rejects equal duplicates and explicitly rejects payload conflicts.
- Resident persistence commits the command audit and its complete event group in the same PostgreSQL transaction.
- Same-stream preparation and physical batching preserve exact expected stream-version order, including batches that
  cross the configured physical event limit.
- `BaseInMemoryEventSourceCommandActor<TActor,TState>` provides bounded resident state, commit-before-reply,
  failure eviction, shutdown drain, a 64-command admission barrier, and a switch back to the standard path.
- Only the exact `ChangeOptionTradeLegDataCommand` type and verb use the resident path. Every other OptionTrade command
  waits for pending resident persistence, evicts the resident slot, and uses the existing load/save path.
- Actor metrics expose resident hits, misses, count, evictions, pending persistence, completions, and failures.
- The pilot also corrects two defects exposed by its end-to-end test: position creation now receives the command's
  component spread type, and snapshot-range replay retains the stored stream version used by optimistic persistence.

## Schema compatibility

The migration adds nullable `CommandPayload`, `CommandPayloadFormat`, `CommandPayloadVersion`, and
`CommandPayloadSha256` columns to `command_log`. It does not rewrite immutable definitions or historical command rows.
The `event_log` schema is unchanged.

## BenchmarkDotNet evidence

Environment: AMD Ryzen Threadripper 1950X, local PostgreSQL Docker database, .NET 10.0.10, concurrent workstation GC.
Atomic results include command MessagePack serialization, audit insertion, event serialization, stream locking, event
ID reservation, binary COPY, and transaction commit. Each reported operation is one logical command from a group of 64
same-stream commands.

| Physical transaction size | Event LZ4 | Mean per command | Approx. commands/sec | Allocation/command |
| ---: | :---: | ---: | ---: | ---: |
| 1 | Off | 52.579 ms | 19 | 20.10 KB |
| 1 | On | 52.515 ms | 19 | 20.11 KB |
| 64 | Off | 0.875 ms | 1,143 | 3.36 KB |
| 64 | On | 0.868 ms | 1,151 | 3.35 KB |

The retained 64-event atomic batch is about 60 times faster than one atomic transaction per command and exceeds the
1,000 committed same-stream commands/second initial target. LZ4 did not produce a measurable penalty for this payload;
the production compression requirement can remain enabled.

The command serialization benchmark measured Newtonsoft JSON at 3,701.1 ns and 5,209 B versus uncompressed MessagePack
at 774.8 ns and 568 B: about 4.8 times faster with about 89% less managed allocation.

Raw reports:

- `BenchmarkDotNet.Artifacts/command-log-messagepack-gates/results/TomasAI.IFM.Application.Storage.Benchmarks.AtomicCommandEventPostgresBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/command-log-messagepack-gates/results/TomasAI.IFM.Application.Storage.Benchmarks.CommandAuditPostgresBenchmarks-report-github.md`
- `BenchmarkDotNet.Artifacts/command-log-messagepack-gates/results/TomasAI.IFM.Application.Storage.Benchmarks.CommandAuditSerializationBenchmarks-report-github.md`

## Automated verification

| Gate evidence | Result |
| --- | --- |
| Shared resident-state and durable-window focused tests | 11 passed |
| Shared full unit suite | 264 passed; 1 unrelated pre-existing path-dependent CSV test failed |
| Command duplicate coordinator | 8 passed |
| Trade unit suite | 1,072 passed |
| Command-audit PostgreSQL migration, compatibility, duplicate, and conflict tests | 6 passed |
| Atomic two-command, 64-command cross-batch ordering, and in-flight duplicate tests | Passed |
| OptionTrade application-host integration suite, including two consecutive resident leg updates | 11 passed |
| Storage integration project build | Clean, zero warnings/errors |
| Application API production-host build | Clean, zero warnings/errors |

The unrelated Shared test expects a file under
`C:\TomasAI\Projects\IFM\TomasAI.InvestmentFundManager\...` instead of resolving test data from the current checkout.
It does not execute the changed actor or persistence code.

## Gate disposition

- Gates 0-2: complete. Compatible MessagePack storage, event-driven command audit, deterministic deduplication, tests,
  and before/after benchmarks are present.
- Gates 3-7: complete for the development pilot. Resident state, barriers, atomic persistence, OptionTrade wiring,
  application-host integration, ordering, and throughput evidence are present.
- Gate 8: automated rollback, persistence failure eviction, duplicate, restart-replay storage, and exact ordering cases
  are covered. Operating-system process termination at each transaction phase remains part of the operational soak.
- Gate 9: MessagePack and 64-command atomic window optimizations are retained from measured allocation results. More
  pooling is deferred because current measurements show no Gen1 or Gen2 collections in the atomic benchmark.
- Gate 10: pending. A 30-minute burst/idle soak and a full trading-session-duration run cannot be truthfully replaced by
  the short development test suite. The feature switch permits those runs with immediate fallback to standard
  OptionTrade event sourcing and requires no data migration.

## Retained configuration

- Command audit: windowed uncompressed MessagePack, maximum 64 commands, 256 KiB, one-shot 1 ms oldest-request age.
- Atomic event persistence: maximum 256 events and 1 MiB globally; the resident actor stops admission at 64 pending
  commands per stream.
- Resident cache: maximum 4,096 OptionTrade streams.
- Pilot switch: `InMemoryEventSourceActor.OptionTradeLegDataEnabled`.
- Production event compression remains governed independently by `EventLogPersistence.UseLz4Compression`.
