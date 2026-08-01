# Databento Phases 1 and 2 implementation

Phases 1 and 2 are implemented against the licence-free synthetic producer. Live Databento connectivity remains disabled by default and is reserved for Phase 3.

## Phase 1

- Versioned public C ABI with fixed-width status, configuration, lifecycle, mapping, wait, batch and statistics structures.
- Exact 64-byte quote, trade, MBO and discriminated market records with native and managed layout assertions.
- Page-backed fixed-capacity native SPSC ring with acquire/release publication.
- Windows event and Linux `eventfd`/`poll` wait signalling.
- Fail-closed two-millisecond native ring-full deadline.
- Synthetic ticker and option-contract producers.
- Registered native read-buffer allocation, validation and release.
- Synchronous start, consumer-setup handshake, wait, batch read, stop and destroy lifecycle.
- Windows/Linux base-page, memory-lock, explicit NUMA, producer affinity and priority paths.
- Offline native CTest coverage and .NET interop lifecycle coverage.

## Phase 2

- Source-generated .NET 10 `LibraryImport` declarations and `SafeHandle` ownership.
- Immutable deployment-profile configuration and synchronous factory validation.
- Dedicated managed drain thread with one reusable unmanaged native read buffer.
- Preallocated fixed-slot synchronous managed batch channels.
- Per-instrument ticker readers and one shared option-chain reader.
- Fixed batch pools with channel slots plus writer and reader leases.
- Full backpressure, one outstanding consumer lease and deterministic lease return.
- Monotonic public timeouts and bounded stop/join handling.
- Process-wide GC latency coordinator.
- Automatic/explicit affinity, stable process CPU reservations, Windows worker CPU-set exclusion and thread-priority configuration.
- Health snapshots covering native ring, managed batches, channel pressure, pool misses and post-warm-up drain allocations.
- Deterministic synthetic ordering, ABI, lifecycle, option-chain, backpressure, completion and configuration tests.

## Build and test

On Windows:

```powershell
./native/DatabentoFeed.Native/build-native.ps1 -Configuration Release -RunTests
dotnet test ./TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj -c Release
```

The unit-test project automatically invokes the offline native build on Windows. Linux uses the CMake commands documented in `native/DatabentoFeed.Native/README.md`, followed by the same `dotnet test` command.

## Deferred until a licence is available

- Databento `LiveBlocking` connection, authentication and subscriptions.
- Provider DBN decoding and real symbol mappings.
- Definition discovery, live option-chain subscriptions and latest-price queries.
- Provider heartbeat, slow-reader and replay/recovery integration.

Those items begin in Phase 3 and do not block synthetic Phase 1/2 development or testing.
