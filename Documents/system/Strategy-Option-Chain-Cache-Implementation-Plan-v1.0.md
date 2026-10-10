# Strategy Option Chain Cache Implementation Plan

Status: Core and authoritative Development provisioning implemented; live acceptance gates outstanding. Version: 1.1. Date: October 9, 2026.

## Objective

Implement the [Strategy Option Chain Cache Design](Strategy-Option-Chain-Cache-and-Order-Composition-Design-v1.0.md) with a single application market-data boundary. Order Composer accesses `IMarketDataApi.OptionChainCache`; it does not reference Databento classes or the framework store directly. The cache supplies a consistent, prepared snapshot immediately or an explicit unavailable outcome. Preserve the current manual option-chain browser and custom-order entry path.

All initial parameters are global. Future fund and portfolio inputs have an explicit reserved contract boundary; no override rules are implemented until that feature is designed.

## Folder and dependency map

| Location | Planned responsibility |
| --- | --- |
| `TomasAI.IFM.Application.MarketData/Contracts/IOptionChainCache.cs` | Provider-neutral read contract consumed through IMarketDataApi. |
| `TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.cs` | Exposes a stable, non-null `IOptionChainCache OptionChainCache { get; }` member. |
| `TomasAI.IFM.Application.MarketData/Contracts/OptionChainCache/` | Application request, snapshot view, outcome and readiness contracts. |
| `TomasAI.IFM.Application.MarketData/OptionChainCache/` | High-level OptionChainCache facade, request validation, resident policy resolution, background preparation and provider adapters. |
| `TomasAI.IFM.Framework.MarketData/OptionChainCache/` | Provider-independent immutable store, publication, indexes, dependency versions and generation fencing. |
| `TomasAI.IFM.Framework.MarketData.DataBento/OptionChain/` | Existing Databento session manager, routes and quote/trade state. |
| `TomasAI.IFM.Application.MarketData/Pricing/` | Reuse qualified contexts, incremental pricing and OptionCalculator integration. |
| `TomasAI.IFM.Domain.MarketData.Shared/OptionChainCache/` | Versioned strategy cache parameter messages and durable read models; catalog references identify strategy structures. |
| `TomasAI.IFM.Domain.MarketData/OptionChainCache/` | Persisted configuration query handling and stored universe source; existing Reference actors manage publication. |
| `TomasAI.IFM.Domain.Trade/.../OrderComposer/Function/Actor/` | Inject IMarketDataApi into function context and consume the cache contract. |
| `TomasAI.IFM.Application.Storage/` | Parameter projections through existing ScyllaDB configuration storage conventions. |
| `TomasAI.IFM.Application.Api.Server/Startup.cs` | Register cache singleton, producer lifecycle and actor/application dependencies. |

Keep strategy-specific configuration in domain contracts. The framework store accepts normalized prepared scopes and observations; it does not resolve fund rules or reference application request types. Application contracts may adapt framework snapshots without exposing provider sessions, native symbols, pipe requests or worker objects.

The new cache facade can be composed into the existing DatabentoMarketDataApi. Its instance lives in the API process and remains stable across dataset epoch replacements. Epoch changes invalidate its data by generation; the API property must not create a new cache or initialize subscriptions when read.

## Public read contract

Proposed shape for implementation:

```csharp
namespace TomasAI.IFM.Application.MarketData.Contracts;

public interface IOptionChainCache
{
    OptionChainSnapshotResult TryGetSnapshot(OptionChainSnapshotRequest request);
    OptionChainCacheStatus GetStatus(OptionChainCacheScope scope);
}

// Additional member on the existing interface:
// IOptionChainCache OptionChainCache { get; }
```

`TryGetSnapshot` is synchronous and bounded. The result distinguishes Ready, Deferred, NoTrade and Rejected, with reason codes, policy identity, chain version and snapshot evidence where available. `GetStatus` reads resident readiness metadata only. Neither method loads definitions, performs database or network I/O, recalculates the universe, subscribes, polls, sleeps or waits for recovery.

Expose publication and lifecycle through separate internal interfaces such as `IOptionChainSnapshotPublisher` and `IOptionChainCacheLifecycle`. They are not members available to Order Composer. Background maintainers use them to publish or fence prepared data.

Related application interfaces:

- `IOptionUniversePlanner`: resolve a normalized bounded subscription plan from published global rows and accepted operational context.
- `IOptionChainBackgroundUpdater`: maintain subscriptions, pricing dependencies and prepared snapshots outside workflow handling.
- `IStrategyOptionChainParameterResolver`: resolve already-loaded published policy versions for reads; background loading uses repositories separately.
- `IOptionChainSnapshotPublisher`: publish immutable validated data into the framework store.

Every public method and constructor receives XML comments describing units, freshness, failure outcomes and thread-safety expectations. Calculations receive formula documentation.

## Stage 1 Parameter schemas and configuration actors

Create versioned global parameter sets with three explicit rows per strategy: Neutral, Bullish and Bearish. Map catalog Balanced to Neutral once. Reference existing catalog leg structure, ratios and credit/debit semantics.

Include DTE range/preference, per-side delta ranges/targets, net delta tolerance, permitted wing widths, liquidity, freshness/skew, candidate bounds, price constraints, subscription coverage and execution defaults as specified in the design. Validate units, ranges, currency applicability and enabled strategy/bias support.

Implement command handlers, domain events, ScyllaDB projections and query actors. Events created by handlers use the originating CommandId and domain-named payload properties. Query actors read persisted models from ScyllaDB. A resident configuration index is fed by published configuration for immediate application reads; it is not a substitute query-actor read store sourced directly from command state.

Acceptance: configuration round trips through persistence; unsupported or contradictory rows cannot publish; all enabled strategy rows resolve deterministically by version. Development examples are labelled and do not become production defaults automatically.

## Stage 2 Framework cache and immutable contracts

Build the provider-independent store in Framework.MarketData/OptionChainCache. Publish immutable arrays and indexes atomically; reads capture one reference without locks or unbounded retries. Include cache instance, chain version, worker generation, policy/reference/context versions, underlying association, timestamps, coverage and readiness.

Fence old generations, reject regressing observations and bound retained versions and total contracts. Avoid mutable dictionaries or shallow wrappers around live worker state. Publish complete consistent views from the background producer.

Acceptance: concurrent reader/writer tests cannot observe partial updates or mixed dependency generations. Read latency and allocations are measured. Stale or missing scopes return immediately.

## Stage 3 Background universe planning and ownership

Implement the host-managed background universe owner and planner. Prepare the union of enabled biases, preferred/fallback expirations and all permitted wing relationships. Resolve DTE and exact expiry timestamps through the appropriate operational date and exchange calendar. Follow the accepted value-date transition after successful EOD processing.

Use volatility-aware coverage with proactive expansion and resource ceilings. Add independent strategy subscription owners. Share physical routes where supported while keeping manual and trade-monitoring owners independent. Prepare replacement coverage before releasing usable coverage.

Acceptance: expiry/delta/wing requirements are covered before signals; changing or closing a manual view cannot release strategy subscriptions; capacity shortages have explicit readiness reasons.

## Stage 4 Background quote and pricing publication

Connect existing Databento OptionChain observations to the application updater. Maintain the API-resident mirror independently of function calls. Worker capture/RPC operations, if needed by the current architecture, run only here in the background.

Reuse qualified reference and rate preparation and OptionCalculator-backed pricing. Recalculate changed options and options affected by underlying, time or context changes. Prepare full required risk valuations for active candidates before composition. Track event and received times separately and keep pricing provenance.

Verify BBO-1s/MBP-1 support and coexistence before enabling those tiers; use the supported bounded path with explicit configuration when necessary. Calibrate publication cadence and freshness against real provider behavior without relaxing execution checks silently.

Acceptance: independent expected-value pricing tests pass; underlying-only and context-only updates invalidate valuations correctly; published lag and burst capacity meet configured limits.

## Stage 5 Application facade and IMarketDataApi integration

Implement high-level `OptionChainCache` in Application.MarketData/OptionChainCache using the framework store, resident parameter resolver and time provider. Return scoped snapshot views from prior-stage inputs with local freshness and coverage validation.

Expose the facade through `IMarketDataApi.OptionChainCache` and inject it into DatabentoMarketDataApi. Update all implementations, fakes, mocks and DI registrations explicitly. If an unsupported provider requires a disabled cache, return a non-null implementation with explicit unavailable results; never fall back to live acquisition.

The snapshot request carries workflow identity/revision, accepted strategy and bias, pinned global policy version, value date, root, expiry/market constraints, deadline and fund/portfolio identities. Reserve versioned optional override inputs; reject nonempty unsupported overrides rather than ignoring them. Earlier-stage constraints may narrow an eligible scope but cannot demand uncached expansion or silently relax freshness.

Acceptance: reading the property has no side effects; the facade performs zero repository, worker, subscription or context-preparation calls on both Ready and unavailable paths.

## Stage 6 Order Composer function actor integration

Add `IMarketDataApi MarketDataApi` to the function context and resolve through its established injection mechanism. Call `context.MarketDataApi.OptionChainCache.TryGetSnapshot(request)` at the automated composition boundary. Keep the calculation model pure over the returned immutable snapshot.

Refactor the current realtime dispatch/preparation path so it does not call `CompositionMarketPreparation.PrepareAsync` to acquire a chain for automated options. Preserve a clear separation between selecting current market data and durably accepting the exact decision input.

The current command contract carries frozen MarketSnapshot and fingerprint evidence. Extend or version that contract deliberately: a request awaiting its first snapshot is distinct from an accepted frozen execution. Freeze the chosen snapshot identity and selected evidence before durable acceptance; update validation, adapters and command/event fingerprints together. Do not mutate a frozen request or attach a fresh snapshot under the same accepted command identity. Duplicate accepted execution uses its pinned evidence and still observes deadlines; an expired snapshot requires a new normal workflow input, not automatic recapture or replay.

Map unavailable data to a prompt Deferred/NoTrade workflow outcome rather than an acquisition timeout or generic pipeline exception. Distinguish invalid configuration from temporary data unavailability. Do not queue a recovery retry loop or rerun old signals on ChainReady events.

Acceptance: integration tests trace accepted earlier inputs through one cache snapshot to a deterministic candidate or immediate unavailable outcome. Actor event mapping, durable evidence and duplicate execution remain consistent.

## Stage 7 Candidate selection and portfolio handoff

Use indexed expirations and strikes, bounded per-side shortlists and precomputed wing relationships. Rank finalists deterministically. Validate quote age/skew and required full risk values for all selected legs. Calculate conservative executable credit using bids for sells and asks for buys, with qualified tick rounding.

Preserve existing portfolio sizing, fund mandates, financial reservation and broker execution boundaries. A cached one-unit candidate is not permission to spend or bypass order acceptance. Keep optional broker confirmation outside the cache reader and pure composition benchmark.

Acceptance: golden formula/topology tests cover neutral and directional Iron Condors and supported catalog structures; candidate evidence contains exact selected observations and policy versions.

## Stage 8 Lifecycle and operational visibility

Tie background startup/shutdown to API host lifecycle and global market open/close events. Close stops relevant feeds and fences affected scopes. Open validates the accepted value date and resumes background preparation. Every worker replacement immediately fences old quotes, restores desired strategy subscriptions and republishes readiness only after fresh generation data qualifies.

Add readiness transitions and structured metrics with strategy, bias, policy, scope, generation, chain version, quote age, coverage and reason codes. Avoid per-quote logs and repeated identical recovery warnings.

Acceptance: startup, market-close/open and live fault tests return Deferred without waiting, then supply ready data after background recovery. Manual views and open-trade monitoring ownership remain isolated.

## Stage 9 Performance and complete verification

Run BenchmarkDotNet for snapshot read, filtering, finalist verification and bounded composition at representative chain sizes. Include concurrent publication, maximum coverage, stale/missing scopes and generation changes. Targets: snapshot acquisition p99 <= 1 ms, indexed filtering <= 5 ms, finalist calculations <= 10 ms, ranking <= 50 ms and complete pure composition <= 100 ms. Measure percentiles jointly rather than summing independent percentile values.

Use deterministic tests to prove absence of hot-path acquisition calls. Run sustained live readiness, quote-age/skew and recovery tests through the same API contract. Track publication lag, allocations, GC and candidate readiness. Include manual custom-order regression checks. Build the affected projects and solution, fixing integration errors while preserving unrelated work.

Acceptance: all stages pass with measured evidence. Benchmark targets exclude external persistence and broker latency, which are reported separately. Unavailable outcomes are never counted as successful candidate latency.

## Completion checklist

- [x] Global parameter messages, validation, actor handlers, persistence and resident configuration.
- [x] Provider-independent framework immutable cache.
- [x] Background universe planning and independent subscription ownership.
- [x] Qualified background pricing and API-resident publication; bounded scope capture rather than per-option incremental capture.
- [x] IOptionChainCache contract under Contracts and IMarketDataApi member.
- [x] Function actor integration and versioned snapshot evidence transition; compatible Development deployment activation is now verified.
- [ ] Bounded deterministic composition and portfolio/execution handoff.
- [ ] Host, market-session and recovery lifecycle integration.
- [ ] Benchmarks, live integration evidence and manual regression verification.

Core implementation is present; the unchecked acceptance gates below prevent declaring the whole plan complete. Fund/portfolio override semantics and administration UI are future design work; the implementation establishes their explicit extension boundary only.

## Implementation evidence and remaining gates

Implemented: immutable generation store, `Contracts/IOptionChainCache`, stable `IMarketDataApi.OptionChainCache` integration, global policy validation and ScyllaDB storage, Reference actor publication, reviewed universe planning, qualified background pricing, independent provider ownership, duplicate lease/capture sharing, bounded indexed filtering, schema-3 composition policy, frozen evidence and immediate NoTrade handling. Manual selection retains its existing path. Development defaults use the user-approved rows in the design and never seed Production.

Verified on October 9, 2026:

- API Release build and Development composition-root startup verification passed.
- Application option-chain/preparation/worker suite: 55 passed; one existing sustained reconstruction stress test skipped.
- Isolated storage integration verified publication, version/revision fencing and retirement against Scylla-compatible storage.
- Real Reference command-actor publication test passed against isolated PostgreSQL, NATS, Redis and CQL infrastructure, including duplicate command handling and exact persisted payload. The test waits for asynchronous projection; command acceptance does not mean read-model completion.
- Composer and trade-selection tests cover frozen bias policy, schema-3 acceptance, policy mismatch, NoTrade and legacy regression behavior. Final selected composer/trade-selection suite: 427 passed, zero failures; recorded in `.artifacts/strategy-option-chain-final-trade-tests.log`.

BenchmarkDotNet ShortRun (.NET 10, 128/512/2,048 contracts):

| Operation | 128 | 512 | 2,048 | Allocation |
| --- | --- | --- | --- | --- |
| Validated cache read, mean | 5.799 ?s | 23.201 ?s | 98.995 ?s | 48 B |
| Background candidate filtering, mean | 52.65 ?s | 190.35 ?s | 839.59 ?s | 58.64/236.82/993.65 KB |

Reports are in `.artifacts/strategy-option-chain-cache-final-benchmarks/results/` and `.artifacts/strategy-option-chain-candidate-benchmarks/results/`. Filtering allocations occur in background publication. These are means from three measured iterations, not p99 guarantees. Full prepared-composer benchmarks are recorded below. Joint live latency and p99 measurements remain outstanding.

### Previous activation dependency (resolved by authoritative provisioning update below)

The existing published Development graph pins schema-1 construction profiles, 5?20-point wings and earlier directional net targets. New global 50-point policies are incompatible with that graph. Both vertical structures currently share one construction profile, while schema 3 pins a single structure's global set. Before activation, define and publish compatible graph/rule/construction versions (separate vertical profiles or an explicit multi-structure pin contract), then coordinate workflow activations and existing financial authorization references. Published history cannot be edited in place. The original dependency was resolved through the authoritative versioned migration below; that migration has now been run and verified.

### Remaining acceptance work (deployment implementation superseded by the update below)

- [x] Compatible automated catalog/construction provisioning implemented for Daily futures and Weekly/Monthly options; running database activation verified on October 9, 2026.
- [x] Deterministic cache publication, accepted preparation and successful composer candidate integration using the approved defaults; five synthetic quote fixtures passed. Real provider quote proof remains in the live gate.
- [x] Full pure composer/finalist/ranking BenchmarkDotNet runs; joint live p99 latency remains a live acceptance gate.
- [ ] Sustained live readiness, burst/coverage accuracy, live recovery and manual coexistence acceptance. Friday's closed futures session prevents fresh live quote proof now.

These are explicit outstanding gates; the implementation must not be called fully complete until they pass.

## Authoritative Development provisioning update

The latest user-approved profiles replace earlier option engineering defaults. Daily deployments contain Futures only; Weekly and Monthly contain Vertical Spreads and Iron Condors only. Both option horizons use the same strategy-specific DTE ranges: Iron Condor 30?45/preferred45; Verticals 5?10/preferred5. Width is always50 ES points. Neutral targets are put/call0.16/0.16; Bullish0.20/0.10; Bearish0.10/0.20.

Provisioning authors new immutable variant, composition-rule and deployment versions2; construction version2 uses schema3 for option horizons and retains schema1 for Daily futures. Activation version3 replaces the previous activation graph. A schema3 construction profile can pin up to eight exact structure policies; Weekly/Monthly pin the approved Iron Condor, Call Vertical and Put Vertical global sets. Runtime resolution selects only the exact selected structure and validates its hash. There is no fallback to another structure or current policy version.

The original reserved Development permissions migrate through financial-policy and Fund command APIs. New financial-policy and Fund mandate versions replace their exact deployment permissions; allocation and risk-envelope references are versioned without changing monetary values. Financial authority is refreshed after reference migration. Automatic migration recognizes only the exact original reserved manifest; unrelated operator configurations require review. Earlier published versions remain available as history.

The earlier single-structure activation design blocker is resolved by this mapping. The actual Development database migration and repeated provisioning check passed on October 9, 2026. Live readiness and complete workflow performance acceptance remain separate gates.


## Offline verification gates completed ? October 9, 2026

The user authorized gates 1?3 now and deferred gate 4 until trading opens.

1. **Development migration and activation:** startup successfully published the new catalog versions, migrated reserved financial permissions and mandates, and verified all three exact published activation references. Independent PostgreSQL reads show activation version 3 with deployment counts 1/2/2 for Daily/Weekly/Monthly. Repeated provisioning retained Portfolio 101, Funds 701/702/703, five deployments, financial authority ready, and the existing 1,000,000 USD capital. No new capital posting was needed. Three authoritative global option profiles are published in ScyllaDB, with exact payload hashes checked against the approved defaults. Startup creates the two additive parameter tables before background readers; the seeder waits for actor readiness, recognizes empty query state, and skips only published profiles. Historical published catalog versions remain available; they are excluded from the new activation, rather than deleted.
2. **Cache-to-composer integration:** five deterministic synthetic, model-priced quote cases cover Neutral/Bullish/Bearish Iron Condors and call/put verticals. They use production candidate preparation, the resident cache, exact schema-3 bindings, accepted input freezing, valuation reuse and the full composer. Every case produces a candidate with identical hashes on repeated composition. A fenced cache generation still resolves the already committed duplicate input; the provider is never called. This is offline integration evidence, not live Databento coverage proof.
3. **Complete prepared composition benchmarks:** BenchmarkDotNet 0.15.8, .NET 10 Release, one launch, one warmup and three measured iterations. All five setup cases require a successful candidate. Measured work includes validation, enumeration, risk filtering, ranking and evidence hashes; fixture IO, broker execution and accepted-input persistence are excluded.

| Strategy | Mean | Allocated per composition |
| --- | ---: | ---: |
| Put vertical | 21.56 ms | 4.32 MB |
| Call vertical | 18.78 ms | 4.33 MB |
| Neutral Iron Condor | 23.00 ms | 5.06 MB |
| Bearish Iron Condor | 23.82 ms | 5.01 MB |
| Bullish Iron Condor | 28.51 ms | 5.08 MB |

These ShortRun means are not p99 guarantees. Allocation volume remains a live-load observation item. The local successful calculation is comfortably below the 2?5 second composition objective; no end-to-end live SLA is established by these measurements.

Evidence:

- `.artifacts/option-cache-development-idempotence.log`: exact Scylla profile verification and successful repeated provisioning.
- `.artifacts/option-cache-persisted-activations.log`: independent PostgreSQL activation query.
- `.artifacts/option-cache-composer-integration.log`: 5/5 cache-to-candidate tests.
- `.artifacts/option-cache-final-composer-regression.log`: 451/451 composer, selection and Development regression tests.
- `.artifacts/option-cache-api-verification-build.log`: API Release build, zero warnings/errors.
- `.artifacts/option-cache-full-composer-benchmarks/results/`: complete five-case BenchmarkDotNet reports.

The verification API host shuts down after checks. Gate 4 remains open: fresh live quotes, readiness under sustained/burst load, joint latency, recovery and manual coexistence during trading hours. No live recovery fault was injected for these offline gates.
