# Databento Phase 3 implementation

Phase 3 adds the licensed live ticker path while preserving the Phase 1/2 ABI, ring, managed batching, and synchronous ownership model. Offline synthetic builds remain the default.

**Status:** Code complete. Deferred runtime smoke confirmations are tracked below and do not block implementation of later phases.

## Native live session

- Uses the immutable Databento C++ `v0.62.1` source commit recorded by CMake.
- Constructs `LiveBlocking` with `DATABENTO_API_KEY` via `SetKeyFromEnv`; the key never enters managed memory.
- Applies the configured dataset, integral heartbeat interval, connection/authentication deadline, and `SlowReaderBehavior::Warn`.
- Groups stable raw-symbol and instrument-ID tickers by input symbology and selected schema, producing the minimum MBP-1, trades, and MBO subscription requests within one session.
- Requires all subscription acknowledgements and initial symbol mappings before entering `ConsumerSetup`.
- Buffers market-data records received during mapping setup through the normal lossless native ring.
- Rejects partial/unresolved symbols, conflicting remaps, unexpected post-start mappings, provider errors, skipped/slow-reader conditions, and ring overrun without concealing loss.
- Maps the pinned SDK's heartbeat timeout to `DBF_CONNECTION_HUNG` and connection-limit/symbol-resolution failures to their typed ABI statuses.

## Record normalization

- DBN MBP-1 maps to `dbf_quote_record64`.
- DBN trades map to `dbf_trade_record64`.
- DBN MBO maps to `dbf_mbo_record64`.
- Provider integer prices, event/receive timestamps, publisher/instrument IDs, sequence numbers, actions, sides, flags, depth, channel, and ingress delta are retained.
- Undefined prices become zero with `DBF_RECORD_FLAG_UNDEFINED_PRICE`; snapshot state is retained with `DBF_RECORD_FLAG_SNAPSHOT`.
- Unsupported control or schema records are handled on the cold control path and never enter the market-record ring.

## Managed runtime

- `Development` may explicitly opt into `DatabentoLive`; paper-trading and production require it; synthetic CI rejects it.
- Live option-chain creation and resolved-definition validation are implemented by Phase 4.
- Identical ticker requests coalesce while conflicting duplicates fail before native allocation.
- Registrations are immutable and sorted by requested symbol and instrument key.
- `FeedHealthSnapshot.TransportReady` reports the completed live handshake; `TradingReady` additionally requires an initial quote or MBO baseline for every subscription that needs one.

## Closed-market contract details

- `DatabentoFeedFactory.CreateMarketDataQueries` creates a dataset-bound synchronous query service.
- `GetContractDetail(fullContractName)` returns a future or option definition, or `null` when a valid raw symbol does not resolve.
- `GetContractDetails(ticker)` returns all outright futures, calls, and puts found under the ticker's Databento `.FUT` and `.OPT` parents.
- `GetContractDetails(fullContractNames)` preserves input order and returns a nullable slot for each requested name.
- `ContractIdToInstrumentId` supports `SYMBOLyyyyMMdd` futures and `SYMBOLyyyyMMdd[C|P]strike` futures options, resolves only current definitions, and requires a unique match.
- `InstrumentIdToContractId` resolves the current definition by Databento instrument-ID symbology, creates the canonical application ID, and verifies the reverse mapping.
- Contract dates use the UTC date of the provider expiration timestamp; option strikes are exact 1e-9 fixed-point conversions with trailing zeroes removed on output.
- Mapping failures are always `DatabentoContractMappingException` with direction, requested identifier, and provider or ambiguity detail. Databento instrument IDs remain day-scoped and are not persisted as permanent identifiers.
- `TomasAI.IFM.Application.Blackboard` supplies the optional `ICachedDatabentoMarketDataQueries` decorator and the DI-facing `IDatabentoContractMappingCache`. Successful live mappings are stored under both contract-ID and instrument-ID keys; misses and provider errors are never cached.
- Mapping-cache keys include the Databento dataset and current UTC definition date. Entries have a 24-hour hard expiration and a 15-minute sliding Redis TTL, renewed in both directions without crossing the hard expiration. Conflicting pairs are evicted and throw a detailed `DatabentoContractMappingException`; Redis infrastructure failures fall back to the provider.
- `IRedisCache.Set`/`SetAsync` overloads accepting `(absoluteExpiry, ttl)` select the earlier Redis deadline. They use an exact Redis absolute expiration when the hard limit is nearer and a relative TTL otherwise, allowing renewal without extending the hard limit.
- `IDatabentoContractMappingCache.ClearMapping(dataset, contractId)` and `ClearMapping(dataset, instrumentId)` remove both directional keys when the cached pair is available. `ClearCurrentMappings(dataset)` removes only that dataset's current UTC definition-date partition.
- Current-partition clearing uses `IRedisCache.RemoveByPrefix`, which performs incremental Redis key scans and deletes only matching literal-prefix keys. It never calls `FLUSHDB`, and existing Blackboard cache namespaces are unaffected.
- Concurrent misses for the same identifier and timeout are coalesced within a decorator instance, preventing duplicate provider requests. Contract-detail queries remain uncached pass-through operations.
- Queries use the latest available Historical `definition` interval, so they work when the live market and live definition replay are unavailable.
- Fixed-point values remain in Databento's 1e-9 integer units; timestamp values remain Unix nanoseconds; undefined provider sentinels become `null`.
- The native opaque result owns numeric records, UTF-8 strings, and error text until the managed wrapper copies and releases it.
- `DATABENTO_API_KEY` remains native-only. OpenSSL Windows builds use the trusted PEM file named by `SSL_CERT_FILE`; certificate verification is never disabled.

## Verification

- Offline native and managed lifecycle tests remain supported with no provider dependency.
- Live-enabled native tests compile against the pinned SDK and include golden DBN quote/trade/MBO normalization fixtures.
- `DataBento.UnitTests` contains no credentialed tests and runs against the offline native build.
- `DataBento.SmokeTests` is opt-in through `IFM_RUN_DATABENTO_SMOKE_TESTS=1` (or the compatibility switch `IFM_RUN_DATABENTO_LIVE_TESTS=1`). Every test discovers current contracts at runtime; no fixed or invalid symbol is used. It covers current definitions, future/option mapping round trips, a current live ticker session, and Phase 4 option-chain discovery/subscription.
- `DataBento.IntegrationTests` is opt-in through `IFM_RUN_DATABENTO_INTEGRATION_TESTS=1` (or the compatibility switch). Each test first verifies a valid Databento connection, then covers provider-rejected tickers, unknown instrument IDs, or malformed application IDs.

## Deferred runtime confirmations

The following operational confirmation is intentionally non-blocking and may be completed when suitable market conditions are available or during the final all-phases acceptance pass:

- Run the existing `LiveTickerSmokeTests` against dynamically discovered current contracts while CME live market data is available, and confirm authentication, resolution, subscription startup/running health, and clean shutdown.
- During market-open or final all-phases runtime acceptance, capture evidence that a current-contract live record reaches the managed feed reader. This operational observation is deferred even though native normalization and managed delivery paths already have deterministic coverage.

When all phases are code complete, rerun the complete credentialed `DataBento.SmokeTests` and `DataBento.IntegrationTests` suites and record their results as final runtime acceptance evidence. A deferred runtime confirmation is not an incomplete Phase 3 code deliverable unless it exposes a defect.
