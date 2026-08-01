# Databento Phase 4 implementation

Phase 4 adds current option-chain definition discovery and a resolved live option-chain subscription while retaining the Phase 1/2 ring, batching, lifecycle, and synchronous API model.

**Status:** Code complete. Deferred credentialed runtime confirmations are tracked below and do not block implementation of later phases.

## Definition discovery

- `IDatabentoMarketDataQueries.GetChainDefinitions` accepts one dataset, underlying selector, exact maturity, universe policy, and call/put selection.
- `ParentOptionSymbol`, `UnderlyingFuture`, and `ExplicitOptionRoots` are supported.
- Discovery deliberately reuses the Phase 3 Historical current-definition query and opaque result ABI instead of adding a second definition-result format. It asks Databento Metadata for the latest available definition interval and downloads that complete interval, so it works independently of market hours.
- The result contains only outright calls and puts for the exact maturity and requested rights. Spreads and non-option definitions are excluded.
- Underlying-future selection first resolves one exact current outright future and then accepts option definitions whose provider underlying raw symbol or underlying instrument ID matches it.
- Results remove duplicate instrument keys and raw symbols, preserve signed 1e-9 fixed-point strike values as exact managed decimals, and sort by strike, right, then raw symbol.
- The dataset-bound query rejects cross-dataset requests before provider access. Empty valid universes return an empty immutable result.
- `CachedDatabentoMarketDataQueries` passes chain discovery through to the provider. Only the Phase 3 bidirectional contract/instrument mapping operations use Blackboard caching.

## Resolved option-chain feed

- `OptionChainSubscription` identifies one underlying, exact maturity, requested strikes, requested rights, market-data kinds, and the immutable `OptionContractDefinition` values selected from discovery.
- Managed validation rejects an empty or duplicate strike list, invalid rights/data kinds, cross-dataset definitions, mismatched underlying/maturity/strike/right, zero provider keys, duplicate instrument keys, duplicate raw symbols, and overlong UTF-8 symbols before native allocation.
- Definitions are converted to native raw-symbol selections containing the expected instrument ID, publisher ID, and option right.
- Live option chains use one `LiveBlocking` session. Quote, Trade, and MBO selections become schema subscriptions for the same raw-symbol set inside that session.
- The provider's initial raw-symbol mappings must exactly match the discovered instrument and publisher IDs. A stale or conflicting remap faults startup with `DBF_SYMBOL_RESOLUTION_FAILED`.
- All selected instruments share the existing single native SPSC ring, dedicated managed drain, pooled `MarketDataBatch64` transport, and one synchronous reader. Session arrival order is retained.

## Verification

- Offline native tests cover resolved option selection ABI validation and preservation of instrument/publisher mappings.
- Managed unit tests cover exact maturity/right filtering, signed and large strikes, deterministic ordering, duplicate removal, underlying-future matching, cross-dataset rejection, resolved selector validation, and shared-reader ordering.
- `DataBento.SmokeTests` discovers only current ES option contracts at run time. Its definition test is suitable while the market is closed. Its live test selects a current provider underlying group, strikes, rights, symbols, and instrument keys before starting one option-chain session.
- `DataBento.IntegrationTests` verifies a valid Databento connection before exercising invalid Phase 4 request selectors.
- Verification on 2026-08-01 passed 34 Databento unit tests, the offline native CTest target, 5 gated smoke-test methods with their live gates disabled, 4 gated integration-test methods with their live gates disabled, and 5 targeted Blackboard decorator tests. A live-enabled Release native build also compiled against pinned Databento `v0.62.1`, and its native executable passed the synthetic lifecycle, option-mapping, and live DBN normalization fixtures without opening a provider session.

## Deferred runtime confirmations

The following operational checks are intentionally non-blocking and may run when provider conditions are suitable or during the final all-phases acceptance pass:

- Run the complete live-enabled smoke suite with `IFM_RUN_DATABENTO_SMOKE_TESTS=1`, `DATABENTO_API_KEY`, and any required `SSL_CERT_FILE` setting.
- Confirm closed-market `GetChainDefinitions` returns a non-empty, current, sorted ES chain for the dynamically selected maturity.
- Confirm the dynamically resolved live option-chain session authenticates, validates every provider mapping, reaches running health, and shuts down cleanly.
- During market-open or final acceptance, retain evidence that a current option record reaches the shared managed reader for every requested schema under test.

When all phases are code complete, rerun the complete credentialed smoke and integration suites and record their results as final runtime acceptance evidence. A deferred runtime confirmation is not an incomplete Phase 4 code deliverable unless it exposes a defect.
