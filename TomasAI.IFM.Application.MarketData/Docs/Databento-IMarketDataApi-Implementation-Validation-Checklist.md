# DataBento `IMarketDataApi` implementation validation checklist

**Status:** Phase A runtime-complete; Phase B remains deferred for FMP

**Version:** 1.6

**Validation date:** 2026-08-17

**Application contract:**
`TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi`

**Provider implementation project:**
`TomasAI.IFM.Framework.MarketData.DataBento`

## 1. Decision summary

### Current verdict

**Full 17-method implementation: NO-GO.**

**FMP-independent 16-method Phase A implementation: RUNTIME COMPLETE.**

The application interface is sufficiently defined to implement every method.
No missing method parameter or return model was found. Sixteen methods can be
implemented without Financial Modeling Prep. Only
`StartStreamingFuturesOptionChainDataAsync` requires the application to obtain
an FMP-backed Treasury curve and pass a fixed risk-free rate into the DataBento
option-chain session.

The live native adapter now builds and passes its native tests. The bounded live
provider gate is also green: all five integration tests and all nine
non-endurance smoke tests pass against DataBento. This validates the existing
Historical definition/mapping queries, latest-price policies, live ticker
startup, option-chain definitions, and bounded option-chain feed path.

The application orchestration and production-epoch suite now passes 51
deterministic tests. It supplies replaceable fakes for provider queries, lifecycle stages,
publishers, last-price readers, aggregation status, option-route ownership, and
the framework `ITreasuryCurve` contract. The provider live-build,
runtime-connectivity, and pre-implementation harness gates are all resolved.

### Scope decision

- Phase A contains all methods except
  `StartStreamingFuturesOptionChainDataAsync`.
- Phase A must not call FMP, synthesize a zero risk-free rate, or disable
  Greeks validity checks.
- The option-chain start method remains explicitly deferred until an FMP key
  and the FMP `ITreasuryCurve` implementation are available.
- `StopStreamingFuturesOptionChainDataAsync` and the non-rate-dependent chain
  infrastructure may be implemented in Phase A, but end-to-end start/stop
  validation remains deferred with chain start.

## 2. Status legend

- `[x]` — validated successfully with recorded evidence.
- `[ ]` — not yet validated or failed; no checkmark may be claimed.
- `DEFERRED-FMP` — intentionally outside Phase A because it requires the
  unavailable FMP-backed risk-free rate.
- `IMPLEMENTATION TASK` — the design and provider substrate are sufficient,
  but the required production component has not been written yet.

## 3. Validation results snapshot

- [x] The application contract builds with zero warnings and zero errors.
- [x] `IMarketDataApi` is owned only by `Application.MarketData`.
- [x] Provider-neutral last-price contracts exist in
  `Framework.MarketData.Contracts.LastPrice`.
- [x] DataBento exposes ticker-feed, option-chain-feed, contract-query, and
  latest-price primitives.
- [x] DataBento exposes raw quote and trade records with fixed-point prices,
  source sequence, event timestamp, receive timestamp, sizes, and quote counts.
- [x] Futures `TickAggregationService` exists and exposes service/configured/
  running ticker status by domain futures contract ID.
- [x] DataBento synthetic/native unit tests pass: **106 passed, 0 failed,
  0 skipped**.
- [x] A DataBento credential is configured in the validation environment. Its
  value was not read or recorded.
- [x] The live native DLL and native test executable build successfully; native
  tests pass: **1 passed, 0 failed**.
- [x] The managed live integration suite passes: **5 passed, 0 failed,
  0 skipped**.
- [x] The bounded, non-endurance live smoke suite passes: **9 passed, 0 failed,
  0 skipped**.
- [x] The managed integration and smoke projects load the live-enabled DLL;
  live-only Historical and streaming calls complete successfully.
- [x] Windows Historical HTTPS uses Windows certificate-policy validation after
  the OpenSSL handshake; an explicit `SSL_CERT_FILE` remains supported.
- [x] The Application MarketData suite passes: **69 passed, 0 failed,
  0 skipped**, including atomic enriched-reader semantics, concurrent
  independent-dataset catalog/stop barriers, and single-start/single-stop
  ownership of the reference-counted epoch publisher.
- [x] Live ES and VX raw-symbol contract hydration passes: **2 passed, 0
  failed, 0 skipped**.
- [x] The accepted UI Development G0 process audit passes all **25** startup,
  initialization, transport, signal, import, rendering, shutdown, and cleanup
  checks, including the correlated market-data feed-stop terminal event.
- [x] Framework MarketData contract tests pass: **46 passed, 0 failed,
  0 skipped**, including the enriched option-reader surface.
- [x] Typed application exceptions define lifecycle, lookup, mapping, price,
  aggregation, route-conflict, chain-conflict, and capacity failures.
- [x] An FMP credential is configured in the validation environment. Its value
  was not read or recorded; Phase B remains separately deferred by scope.

## 4. Method-by-method implementation feasibility

The `Design` column answers whether the accepted interface and current
DataBento record/feed/query capabilities contain enough information to
implement the method. `Runtime` is intentionally stricter and remains pending
until the implementation and its required live tests pass.

| # | `IMarketDataApi` method | Design | FMP dependency | Existing DataBento foundation | Required implementation/validation | Runtime |
| ---: | --- | --- | --- | --- | --- | --- |
| 1 | `StartAsync` | Validated | None | Feed factory, contract queries, ticker feed, option feed, aggregation service | Application epoch/factory, concurrent independent-dataset catalog hydration through bounded operation runners, option service, lifecycle rollback, DI | Phase A runtime gate passed |
| 2 | `StopAsync` | Validated | None | Feed stop/drain and aggregation shutdown exist | Concurrent independent-dataset drain with the five-second actor default, option/chain shutdown, aggregate failures, stale-reader invalidation | Phase A runtime gate passed |
| 3 | `GetFuturesContractAsync` | Validated | None | Exact/bulk details and bidirectional contract/instrument mapping exist | Application resolver/mapper, catalog, bounded sync-call runner, typed miss/ambiguity behavior | Phase A runtime gate passed |
| 4 | `GetFuturesContractsAsync` | Validated | None | Batch contract details exist | Input-order/duplicate preservation, grouped lookup, all-or-nothing mapping | Phase A runtime gate passed |
| 5 | `GetFuturesOptionContractAsync` | Validated | None | Contract mapping and option definitions exist | Application option resolver/mapper and exact underlying/maturity/right/strike checks | Phase A runtime gate passed |
| 6 | `GetFuturesOptionContractsAsync` | Validated | None | Batch details and chain definitions exist | Grouped batch resolution, ordering, duplicate handling, all-or-nothing result | Phase A runtime gate passed |
| 7 | `GetFuturesOptionChainContractsAsync` | Validated | None | `GetChainDefinitions`, both rights, exact maturity and strikes exist | Domain-model hydration/mapping, stable ordering, empty-chain behavior | Phase A runtime gate passed |
| 8 | `GetFuturesPriceAsync` | Validated | None | Futures quote/trade records and aggregation worker exist | Shared hot store update in aggregation path; prefer a fresh last trade, fall back to a fresh valid quote midpoint; typed unavailable exception | Phase A runtime gate passed; VX quote fallback added 2026-08-18 |
| 9 | `GetFuturesOptionPriceAsync` | Validated | None | Option quote records and midpoint fields exist | Option hot-store update; fresh valid two-sided midpoint; null/stale/one-sided behavior | Phase A runtime gate passed |
| 10 | `GetFuturesLastPriceReader` | Validated | None | Framework reader contract and futures record fields exist | DataBento store, epoch-bound reader/provider, coherent lock-free snapshot tests | Phase A runtime gate passed |
| 11 | `GetFuturesOptionLastPriceReader` | Validated | None for raw reads; FMP pricing context for valid Greeks | Framework raw and atomic tick-with-Greeks contracts exist | Multi-asset aggregation slot, exact quote/Greeks coherence, quote-derived trade Greeks, epoch-bound reader | Raw/enriched Phase A gate passed; valid Greeks deferred to Phase B |
| 12 | `StartStreamingFuturesTickDataAsync` | Validated | None | System-wide multi-asset aggregation and contract status exist | Application live-delivery activation set/router and acknowledgement barrier | Phase A runtime gate passed |
| 13 | `StopStreamingFuturesTickDataAsync` | Validated | None | Multi-asset aggregation remains independently running | Router deactivation barrier; persistence must continue after live deactivation | Phase A runtime gate passed |
| 14 | `StartStreamingFuturesOptionTickDataAsync` | Validated | None | Multiplexed ticker feed can carry option quotes/trades | Bounded option streaming worker/publisher, immutable activation snapshot, route ownership | Phase A runtime gate passed |
| 15 | `StopStreamingFuturesOptionTickDataAsync` | Validated | None | Provider feed supports bounded stop/drain | Per-option deactivation barrier without stopping unrelated options | Phase A runtime gate passed |
| 16 | `StartStreamingFuturesOptionChainDataAsync` | Validated structurally | **Required** | Chain definitions, resolved chain subscription, one shared reader, underlying ticker status exist | Session manager, chain tick service, transient publishers/state, Black-76 adapter, underlying hot price, application-supplied FMP Treasury rate | **DEFERRED-FMP** |
| 17 | `StopStreamingFuturesOptionChainDataAsync` | Validated | None to stop | Option-chain feed supports stop and disposal | Session lookup, drain, transient-state removal, dependency lease release | Phase A manager/stop gate passed; end-to-end deferred with #16 |
| 18 | `IsTickDataStreamActive`, `TryGetLastTickPrice`, `TryGetLastOptionTickPrice` | Validated | None for raw trade/quote; existing pricing context for optional Greeks | Multi-asset TickAggregation owner registry and hot cache | Owner-idempotent first/final route transitions, stream-independent decimal snapshots, sequence-aligned optional Greeks, shutdown route cleanup | Unit and cross-component integration gates passed |
| 19 | `TryGetCurrentlyTradedFuturesContract`, `UpdateCurrentlyTradedFuturesContractAsync` | Validated | DataBento definitions when a rollover row is incomplete/due or startup forces provider revalidation | Startup rollover table, persisted futures contracts, atomic runtime registry | Case-insensitive allocation-free lookup, ES/VX registry replacement, due-date persistence, startup provider refresh and blocking validation | Unit, Scylla integration, and realtime ITI route gates passed |

### Feasibility conclusion

- All 19 method groups are implementable with the accepted architecture.
- Methods 1–15 and 17 require no FMP access and may form the Phase A work.
- Method 16 must not be implemented with a placeholder rate. It remains
  deferred until FMP is available.
- The existing one-shot `IDatabentoLatestPriceClient` is not the implementation
  of methods 8 and 9. Those methods must use the corresponding epoch hot reader
  and must not fall back to a provider query or storage read.

## 5. Required DataBento provider components

These components are implementation work, not additional public
`IMarketDataApi` methods.

### Phase A — no FMP required

- [x] `IDatabentoMarketDataEpochFactory` and owned
  epoch lifecycle.
- [x] Contract resolver and domain read-model mapper.
- [x] Bounded runner for synchronous native query
  calls.
- [x] `IDatabentoLastPriceStore` with bounded slots for
  the configured epoch universe.
- [x] DataBento implementations of
  `IFuturesLastPriceReader` and `IFuturesOptionLastPriceReader`.
- [x] Futures and futures-option hot-slot updates
  inside the asset-neutral `TickAggregationService` before
  batching/publication for every accepted quote or trade.
- [x] Multiplexed individual futures-option streaming
  service and bounded live publisher.
- [x] Atomic option raw and tick-with-Greeks slot
  shapes; enriched reads return `false` until a calculation is available.
- [x] Asset-contract live-delivery activation router.
  It must not control or stop durable multi-asset aggregation.
- [x] Shared option route-ownership registry.
- [x] Option-chain session manager, stop/drain path,
  transient state store, and underlying aggregation dependency lease.
- [x] DataBento DI extension that registers framework
  services only; it must not register application `IMarketDataApi`.
- [x] Application `DatabentoMarketDataApi`, options,
  health, diagnostics, and application DI extension.

### Deferred Phase B — FMP required

- [ ] **DEFERRED-FMP:** FMP implementation of `ITreasuryCurve` and successful
  credentialed query.
- [ ] **DEFERRED-FMP:** application no-look-ahead curve selection and DTE
  ceiling-tenor selection.
- [ ] **DEFERRED-FMP:** immutable `OptionChainRiskFreeRate` passed by value to
  the chain session.
- [ ] **DEFERRED-FMP:** Black-76 Greeks enrichment for transient option-chain
  quote/trade messages.
- [ ] **DEFERRED-FMP:** publish the same atomic quote/trade-with-Greeks state
  through `IFuturesOptionLastPriceReader`.
- [ ] **DEFERRED-FMP:** complete implementation and end-to-end validation of
  `StartStreamingFuturesOptionChainDataAsync`.

## 6. Mandatory pre-implementation gates

The user requested that implementation begin only after every applicable
pre-implementation checkmark succeeds. Phase A therefore remains closed until
all items in this section are checked. Section 7 is the subsequent runtime
acceptance checklist and is completed while implementing the methods.

### G1 — contract and architecture

- [x] Exact application interface reviewed method by method.
- [x] Canonical domain IDs are strings; provider IDs remain internal.
- [x] Application owns `IMarketDataApi`; framework vendor projects implement
  provider-neutral services.
- [x] Option-chain data is transient and never persisted.
- [x] Durable tick persistence remains exclusive to futures
  `TickAggregationService`.
- [x] Risk-free-rate access is isolated to option-chain start/Greeks.

### G2 — native DataBento live build

- [x] Resolve the Windows Winsock header conflict in the live native test
  target. `WIN32_LEAN_AND_MEAN` is defined before `<Windows.h>` so legacy
  `winsock.h` declarations are excluded.
- [x] Run the live native build and native tests successfully:

  ```powershell
  ./native/DatabentoFeed.Native/build-native.ps1 `
      -Configuration Debug `
      -EnableLive `
      -RunTests
  ```

- [x] Confirm the managed test output loads the live-enabled DLL rather than
  the offline DLL from `out/build`.

### G3 — existing provider integration

- [x] Pass all five bounded DataBento integration tests with the live build:

  ```powershell
  $env:IFM_RUN_DATABENTO_INTEGRATION_TESTS = '1'
  dotnet test `
      ./TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests.csproj `
      --no-restore `
      -p:DatabentoEnableLive=true
  ```

- [x] Pass current contract-details and contract-mapping smoke tests.
- [x] Pass current option-chain-definition smoke test.
- [x] Pass a bounded current futures ticker smoke test during suitable market
  conditions.
- [x] Pass a bounded current option-chain feed smoke test during suitable
  market conditions.

The one-hour endurance tests are a production-acceptance gate, not a coding
start gate.

### G4 — implementation test harness

- [x] Add an `Application.MarketData` unit-test project or equivalent test
  location for the application orchestration implementation.
- [x] Provide deterministic fake framework services for contract queries,
  feed lifecycle, last-price readers, aggregation status, publishers, and
  option routes.
- [x] Provide a fake `ITreasuryCurve` only for architecture/unit testing. It
  must not be used to claim production readiness or to enable method 16 in
  production.
- [x] Define typed exceptions and test the public null/false/throw semantics
  before method implementation begins.
- [x] Define concurrency tests for duplicate start, stop-during-start,
  per-contract activation, reader-after-stop, and chain/individual route
  conflict.

Gate 4 evidence: `TomasAI.IFM.Application.MarketData.UnitTests` contains a
test-only executable semantic model. The production implementation can replace
that SUT while retaining the same fake controls and assertions. The harness is
included in `TomasAI.IFM.sln` and passes **50/50** tests.

## 7. Method acceptance checklist

Each method receives a runtime checkmark only after its implementation passes
all applicable items below.

### Lifecycle

- [x] Same-date `StartAsync` is idempotent.
- [x] Different-date start is rejected until explicit stop.
- [x] Partial-start failure drains every acquired resource in reverse order.
- [x] `StopAsync` is idempotent, date-safe, and aggregates cleanup failures.
- [x] Stop leaves no worker, native handle, reader, batch, route, or transient
  chain state alive.

### Contract queries

- [x] Single confirmed miss returns `null`.
- [x] Batch empty input returns `[]` without provider access.
- [x] Batch results preserve order and duplicates.
- [x] Batch missing/ambiguous results fail the whole call.
- [x] Futures and futures-option kinds cannot cross-map.
- [x] Provider fixed-point strikes map through decimal and are rejected if the
  existing double domain property cannot round-trip without precision loss.
- [x] Option-chain definitions include calls and puts for the exact underlying
  and maturity and are stably ordered.

### Prices and readers

- [x] Futures price returns a fresh last trade only.
- [x] Missing/stale futures trade throws the typed unavailable exception and
  never returns zero.
- [x] Futures-option price returns a fresh valid two-sided midpoint.
- [x] Missing/stale/one-sided option quote returns `null`.
- [x] Crossed quote or identity conflict throws.
- [x] No price method calls the one-shot latest-price provider client,
  persistence, actor APIs, Blackboard, or Redis.
- [x] Reader handles are stable per contract/epoch and perform no subscription.
- [x] Readers return `false` after epoch stop and never attach to another epoch.
- [x] Quote/trade snapshots are coherent under concurrent update/read stress.
- [x] Option quote-with-Greeks reads atomically match the quote source sequence
  used by the calculation.
- [x] Option trade-with-Greeks reads carry the latest quote-derived Greeks
  state available when the trade was processed.
- [x] Enriched-read availability is independent of `IsValid`; failures carry a
  typed reason and nullable values rather than zero sentinels.
- [x] A newer raw option tick clears or replaces older enriched state so a
  stale tick/Greeks pair cannot be returned as current.

### Futures streaming

- [x] Start/stop returns `true` only for a state transition and `false` for an
  identical existing state.
- [x] Live activation does not start, stop, or suppress durable aggregation.
- [x] Deactivation completion forms a visible routing barrier.
- [x] `GetTickerStatus` correctly reports service/configured/running state.

### Individual option streaming

- [x] One bounded multiplexed option worker services the configured universe.
- [x] Activation and deactivation are linearizable per option contract.
- [x] Quote/trade messages use domain contract/value-date identity.
- [x] Individual option live activation causes no separate persistence;
  admitted raw option ticks persist only through `TickAggregationService`.
- [x] Individual and chain routes cannot own the same option simultaneously.

### Option-chain stop and non-rate infrastructure

- [x] Chain admission validates the underlying aggregation service and ticker
  before allocating provider resources.
- [x] Chain definitions validate underlying, maturity, rights, strikes, raw
  symbols, and instrument keys.
- [x] Dependency loss drains and stops/faults the chain.
- [x] Chain stop removes transient state and publishes no later live delta.
- [x] Chain quote/trade messages remain separate from tick-aggregation events.

### Deferred option-chain start/Greeks

- [ ] **DEFERRED-FMP:** latest curve date is never after the epoch value date.
- [ ] **DEFERRED-FMP:** shortest Treasury tenor covering DTE is selected.
- [ ] **DEFERRED-FMP:** no zero/default rate is synthesized.
- [ ] **DEFERRED-FMP:** current underlying midpoint, then last trade, is selected
  without provider/storage calls on the hot path.
- [ ] **DEFERRED-FMP:** Black-76 result validity and input timestamps are carried
  on transient quote/trade messages.

## 8. Required implementation order after Phase A gate approval

1. **Completed:** fix and validate the live native DataBento build, existing
   integration suite, and bounded non-endurance smoke suite.
2. Implement DataBento last-price store/readers and integrate futures and
   futures-option raw/enriched slot updates into `TickAggregationService`.
3. Implement the application contract resolver/mapper and bounded query runner.
4. Implement the individual option streaming service, option slots, and route
   ownership.
5. Implement the epoch factory/lifecycle and application `StartAsync`/
   `StopAsync`.
6. Implement contract and option-chain definition query methods.
7. Implement price and reader acquisition methods.
8. Implement futures and individual-option activation methods.
9. Implement non-rate option-chain session management and the stop path.
10. Register DataBento framework services and the application API separately in
    DI.
11. Complete Phase A unit, integration, live smoke, concurrency, and shutdown
    validation.
12. When FMP is available, implement the risk-free-rate selection, chain start,
    and Greeks enrichment as Phase B.

## 9. Commands and evidence recorded for this report

### Successful

```powershell
dotnet test `
    ./TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj `
    --no-restore
```

Result: **97 passed, 0 failed, 0 skipped**.

```powershell
dotnet build `
    ./TomasAI.IFM.Application.MarketData/TomasAI.IFM.Application.MarketData.csproj `
    --no-restore
```

Result: **build succeeded, 0 warnings, 0 errors**.

```powershell
./native/DatabentoFeed.Native/build-native.ps1 `
    -Configuration Debug `
    -EnableLive `
    -RunTests
```

Result: **live native build succeeded; 1 native test passed, 0 failed**.

```powershell
$env:IFM_RUN_DATABENTO_INTEGRATION_TESTS = '1'
dotnet test `
    ./TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests.csproj `
    --no-restore `
    -p:DatabentoEnableLive=true
```

Result: **5 passed, 0 failed, 0 skipped** in approximately 2 minutes 4
seconds.

```powershell
$env:IFM_RUN_DATABENTO_SMOKE_TESTS = '1'
dotnet test `
    ./TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests.csproj `
    --no-restore `
    -p:DatabentoEnableLive=true `
    --filter "Category!=LongRunning"
```

Result: **9 passed, 0 failed, 0 skipped** in approximately 2 minutes 41
seconds. The one-hour endurance category was explicitly excluded.

```powershell
dotnet test `
    ./TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj `
    --no-restore
```

Result: **52 passed, 0 failed, 0 skipped**. This covers the application method
surface, typed exceptions, lifecycle rollback/idempotency, query and price
semantics, epoch-bound readers, aggregation admission, activation concurrency,
individual/chain option-route ownership, and atomic quote/trade-with-Greeks
reads.

```powershell
dotnet test `
    ./TomasAI.IFM.Framework.MarketData.UnitTests/TomasAI.IFM.Framework.MarketData.UnitTests.csproj `
    --no-restore
```

Result: **46 passed, 0 failed, 0 skipped**, including the provider-neutral
Greeks snapshot and enriched option-reader contract tests.

```powershell
dotnet run --project `
    ./TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks.csproj `
    -c Release -- --last-price --operations=5000000 --strict
```

Result: **7,410,303 quote updates/s**, **40,765,179 quote reads/s**, and
**0 B/op** for both hot-store paths. Post-stop reader invalidation passed.

```powershell
dotnet run --project `
    ./TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks.csproj `
    -c Release -- --api-price --operations=1000000 --strict
```

Result: **9,184,912 futures-price calls/s** and **80 B/call** for the
contract-required completed `Task<decimal>`. Full environment evidence is in
`Databento-Phase-A-Benchmark-Results.md`.

### Resolved readiness failures

An integration run against the offline DLL reached five managed tests, and all
failed because Historical support was compiled out. This was an expected
offline-build limitation, not a credential failure.

The first live native test build failed with Windows Winsock type redefinitions;
`WIN32_LEAN_AND_MEAN` corrected the header boundary. The first live Historical
calls then exposed a Windows/OpenSSL trust-chain mismatch. The adapter now lets
cpp-httplib complete its Windows Schannel certificate-policy check after the
OpenSSL handshake, while retaining the explicit PEM override. The successful
integration and smoke results above validate both corrections.

## 10. Gate approval record

- Phase A approved to start: **Yes**
- Phase A runtime completion approved: **Yes — methods 1-15 and 17**
- Full API implementation approved to start: **No**
- DataBento live-provider gates G2/G3: **Approved**
- Application implementation-harness gate G4: **Approved**
- Immediate owner/action: retain the Phase A runtime and benchmark gates;
  schedule the still-deferred Phase B implementation independently now that an
  FMP credential is available.
- FMP-dependent method: explicitly deferred; it does not prevent completing
  the Phase A design and implementation after its independent gates pass.

Do not approve the Phase A implementation start until every applicable checkbox
in section 6 has objective evidence. Do not approve Phase A runtime completion
until its applicable section 7 checks pass. Full 17-method completion also
requires every `DEFERRED-FMP` check.
