# Backtesting shared-system qualification

Date: 2026-11-09. Baseline: `2d057bc114318b0ab0a921d6d37e69bdef862b3c` (abbreviated `2d057bc11`). Shared implementation is uncommitted in the working tree. Core backtesting remains deferred.

## Delivered changes

| Area | Implementation |
| --- | --- |
| Clock composition | One explicitly supplied business `TimeProvider` across API service containers. Development defaults to `TimeProvider.System`. `PhysicalTime` is a separate service and cannot accidentally resolve the business clock. Optional clock identity supports future domain/run/generation metadata without enabling a replay mode. |
| Timers and bounded IO | `ClockDeadline` and Function execution policy keep a physical watchdog effective when logical time is frozen. Physical IO cancellation, delivery leases, process ownership, elapsed measurements and diagnostics retain their physical roles. |
| Active business paths | Trading preparation, financial expiry/authorization, pricing qualification, indicator freshness and calculation metadata, value-date/calendar reads and parameter mutation/assignment/startup use the explicit application or transaction clock. Source observation dates are preserved. |
| State staging | The shared state factory binds the instance clock. New events receive provenance before reducer application when clock-bound; replay never invents creation time. Direct unbound state instances defer capture to their owning publication/persistence boundary. |
| Message origin | Immutable validated UTC origin with clock domain and optional run/generation/causation. Weak association avoids adding serialized fields or changing DTO record equality. All 158 computed system-clock origins now read stable metadata. Missing event receipt/error times are filled at the owning boundary; supplied source facts remain intact. |
| Copying and forwarding | Explicit origin preservation on 232 simple record-copy forwarding sites, plus 45 identity-aware command producer/preparation copies and deserialization/envelope adapters. Producer preparation keeps an existing origin only when the command identity stays the same. Newly derived messages have their own origin. Legacy unknown provenance remains explicitly unknown on forwarding, durable retry and replay. |
| NATS | Additive origin headers, independent of tracing and generation headers; actor producers/consumers, replies, owned payload adapters and durable JetStream replay carry provenance. |
| PostgreSQL | Nullable `MessageOriginJson` on event, command-audit and projector-outbox rows; both append modes, replay/snapshot readers and outbox retries preserve it. Existing payload bytes and audit hashes stay authoritative. Accounting-intent events carry an optional command-origin sidecar for reconstruction. |
| Native interoperability | Rust publications, evidence outboxes and control calls carry origin headers. An optional FFI snapshot export carries per-frame origins while the old export remains compatible. Existing eight-key frame bodies remain unchanged. C# snapshot sidecars validate generation/membership before attaching provenance. |
| Rust UI | Actor and market-data control requests create a physical-control origin once per request. UI layout is unchanged. The Development package retains full source-level debugging. |

The [disposition register](backtesting-inventories/2026-11-09/shared-clock-disposition.csv) accounts for the 2,933 original production syntax candidates: 574 original expressions were replaced/removed and 2,359 retained with explicit roles. The final scan contains 2,653 production candidates, including newly explicit seams; these counts are syntax sites, not numbers of bugs. Computed system-clock message origins fell from 158 to zero. The only remaining system-time message-property example is the inactive Actor Client `TestEvent` sample. The scan found zero C# syntax diagnostic files and retained the same 76 `IMarketDataApi` members and 1,529 production message declarations; the final copy benchmark adds a test-only command type.

## Compatibility findings fixed during qualification

1. Generic RPC code attached metadata to decoded values. `Array.Empty<T>()` may return a shared singleton, so a later response falsely appeared to change its origin. Origin now belongs to each RPC request/reply envelope. A real-NATS regression sends repeated empty requests and replies with different clocks and verifies that empty values remain unclaimed.
2. A trade-signal completion can lack an optional event GUID. Passing `Guid.Empty` as causation violated the validated metadata contract and prevented its observational notification. Missing causation is now null; tests cover present and missing identities. The validation rule remains strict.
3. An unbound test state must not silently capture System time before its controlled-clock owner stages it. Factory-bound states capture before reducing; unbound states wait for an explicit owning boundary. Replay keeps the recorded or unknown origin.
4. Full UI compatibility verification found optional draft-label/status/reference fields absent in the older managed fixture. Tests equate absent fields with their defined empty defaults while continuing to compare populated values exactly.

Existing message numeric keys remain unchanged except explicitly additive optional sidecars on the accounting-intent and market-data snapshot envelopes. No creation time is backfilled for legacy rows. The Development schema migration added nullable columns to the verified `ifm_application_development` database; application data was not reset.

## Test results

Selected relevant suites passed **9,250 .NET cases across 30 assemblies; zero failures**. Twelve existing opt-in/environment-dependent tests were skipped. The evidence manifest records the selected full passing run for each assembly and adds only the nonoverlapping RPC regression case, excluding repeated runs and historical failures.

| Suite or scenario | Result / test duration |
| --- | --- |
| Shared unit | 388 passed; 9 s |
| NATS unit | 143 passed; approximately 1 s |
| Analytics unit, including missing-causation regression | 1,295 passed; 8 s |
| All selected domain unit and BDD suites | Passed; individual TRX files retained |
| Storage EventSource integration | 275 passed; 13 m 9 s |
| Real-NATS RPC/origin regression class | 3 passed; 612 ms; one new case beyond the 275 above |
| Portfolio integration | 223 passed, 5 existing skips; 2 m 4 s |
| Live-pipeline integration, final rerun | 344 passed; 1 m 15 s |
| Full Market Data unit/lifecycle rerun | 854 passed, 5 existing skips; 10 m 7 s |
| Scheduler portable | 50 passed, 2 existing skips |
| Native Rust library | 106 passed; 11 existing ignored opt-in cases |
| Standalone Rust UI | Full verification passed, including all 58 isolated NATS scenarios, golden fixtures, native journals and contract/schema verification |

The 12 .NET skips cover long-running worker containment/reset stress, the optional live Treasury feed, external Quartz/recovery qualification, and opt-in Production Portfolio host/restart/load tests. New clock/provenance/snapshot/RPC tests were executed. Live IBKR orders were not submitted. Financial correctness and recovery were exercised through existing isolated emulator/managed-journal and Portfolio integration scenarios.

The relevant integration coverage includes real PostgreSQL latest-snapshot loading, origin retention in binary-copy and sequential append paths, known and unknown origin retry, command-audit byte/hash stability, financial intent reconstruction, independent physical cancellation with frozen business time, producer restart, and response clock-domain ownership.

## Before/after measurements

BenchmarkDotNet ran on .NET 10.0.10, Threadripper 1950X, workstation GC, with three warm-up and eight measurement iterations. Baseline source was checked out separately at `2d057bc11`; candidate source used an isolated E: checkout. These are microbenchmarks, not an end-to-end trading latency claim.

| Unchanged serialization case | Baseline mean | Candidate mean | Allocations |
| --- | ---: | ---: | ---: |
| Direct-to-NATS MessagePack, 256-byte payload | 982.1 ns | 730.77 ns | 0 B in both |
| Direct-to-NATS MessagePack, 4,096-byte payload | 2,499.4 ns | 2,077.06 ns | 0 B in both |

The serializer was not optimized in this phase; cross-run differences must not be attributed to the clock change. The same-run provenance comparison measures its actual added cost:

| Payload | Body-only control | Forwarded origin + body | New message + origin + body |
| --- | ---: | ---: | ---: |
| 256 bytes | 586.34 ns / 0 B | 1,272.54 ns / 552 B | 2,817.93 ns / 814 B |
| 4,096 bytes | 1,140.73 ns / 0 B | 1,840.50 ns / 560 B | 3,671.05 ns / 814 B |

Forwarding recorded Gen0 0.1316/0.1335 collections per 1,000 operations for 256/4,096-byte payloads. New-message preparation recorded Gen0 0.1869, Gen1 0.0076 and Gen2 0.0038 per 1,000 operations. No lock contentions were recorded in these single-worker cases; this does not prove system-wide absence of contention. New-message measurements also include creating the benchmark message object. These cases use ordinary System-domain origins with absent run/generation/causation values; additional scope metadata changes header size.

Variability was material in several cases (for example 256-byte forwarded preparation: standard deviation 138.668 ns; new-message preparation: 337.158 ns; 4,096-byte new-message preparation: 798.722 ns). Raw reports include errors and distributions. Origin tracking is a correctness feature with measurable allocation/CPU cost, not a claimed performance improvement.

The final run also measures forwarding copies separately from serialization:

| Copy case | Mean | Allocation | Gen0 / Gen1 / Gen2 per 1,000 operations |
| --- | ---: | ---: | --- |
| Original record copy | 21.63 ns | 72 B | 0.0172 / 0 / 0 |
| Copy preserving an existing origin | 1,299.08 ns | 158 B | 0.0305 / 0.0019 / 0.0019 |
| Producer preparation preserving the same command identity | 1,336.35 ns | 158 B | 0.0305 / 0.0019 / 0.0019 |

No contentions were recorded in the copy cases. Preserving provenance adds approximately 86 B per copy and weak-table/GC work; the report does not characterize forwarding as allocation-free.

Functional baselines: Shared unit 371 cases / 10 s before versus 388 / 9 s after; NATS unit 138 / approximately 1 s before versus 143 / approximately 1 s after. Added cases and workstation load prevent treating suite duration differences as performance gains. Live lifetime-GC snapshots are retained before and after, but process age and startup warm-up differ and those totals are not comparable steady-state rates.

## Development qualification

The final deployment started at **21:06:20 UTC / 16:06:20 Toronto**. The API, Rust Market Data Host, scheduler, Server Manager and Rust eGui run from the Development configuration. Both Rust packages have matching PDB identities, full type records and the expected project source files. All 138 deployed native package files were hash-verified against the candidate.

Observed after startup; the final ten-sample API/native observation ran **2026-11-09T21:08:36.1362178+00:00–2026-11-09T21:13:46.7420764+00:00**, and structured error logs were audited through **2026-11-09T21:15:28.699790+00:00**:

- API readiness Healthy; Rust Host Ready.
- Startup Running: eight satisfied activities, zero failed/skipped.
- All **76** indicator health entries Warm/ready, including the subsequent five-minute audit; zero unready entries.
- Rust eGui connected; no new queued error dialog or failed/rejected backend request in the audited UI process.
- API error/fatal log entries: zero since the final restart; structured Manager/child processing failures: zero in the audited segment. All 10 final observations reported API Healthy and Rust Host Ready.
- Options healthy with complete native resource proofs after planned background provider-registration handoffs. The actual membership comparison excludes clock metadata. There were no monitored trade-leg subscriptions in this run. A strict constant-Options-state monitor caught a brief planned handoff; constant generation is not claimed. API/indicator health and process ownership remained valid.

Six successful UI recovery notices were forwarded from stderr and consequently labelled ERR by Manager's generic stream logging. Their structured severity is `success`; they are not processing exceptions. The audit preserves this distinction and retains the raw evidence. The two earlier deployment attempts that exposed RPC value identity and missing causation were stopped, corrected and retested; their historical errors were not erased.

Qualification establishes the observed Development state, not a guarantee that future provider or trading scenarios can never fail. The planned core runner, isolation/reset, historical feed emulator, simulated-time advancement/barriers, generation fencing, run Input/Output UI, configurable capital/costs/latency and financial result calculations still require their separate authorized implementation phase.

## Evidence and reproduction

Evidence root: `E:\IFM\SharedSystemQualification\20261109`.

- `final-test-results.json`, `after/*.trx`, `qualified-suites.csv`, `acceptance-suites.csv`.
- `benchmarks-baseline-2d057bc11/results` and `benchmarks-acceptance/results`.
- `development-final-samples.json`, `development-error-audit.json`, `development-manager-error-audit.json`, native/UI PDB audits and before/after GC snapshots.
- `rpc-origin-regression.log`, `analytics-final.log`, `live-pipeline-acceptance/integration.log`, `ui-verification-acceptance.log`.
- `Restore-Development.ps1` and package/build logs record the coordinated cutover. Isolated test children were preserved when their executable names overlapped Development guards.

Representative commands:

```powershell
dotnet test TomasAI.IFM.Shared.UnitTests -m:4
dotnet test TomasAI.IFM.Domain.MarketData.Analytics.UnitTests -m:4
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests -m:4 --filter 'FullyQualifiedName~EventSourceDb'
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests -m:4
./scripts/Verification/Test-RedisFreeLivePipeline.ps1
./scripts/UI/Rust/Verify-StandaloneRust.ps1
```

Run benchmarks from an isolated checkout to avoid the repository's artifact/junction project discovery collisions:

```powershell
dotnet run -c Release --project TomasAI.IFM.Framework.Messaging.Nats.Benchmarks -- --filter '*MessageOrigin*Benchmarks*' '*SerializationBenchmarks.SerializeEnvelopeDirectToNats*' --join
```
