# IFM Databento native dependency

This directory contains the native C++20 ABI, synthetic producer, fixed-slot SPSC ring, portable signal, registered read-buffer ownership, the `LiveBlocking` ticker, option-chain, and one-shot latest-price adapters, and Historical contract-definition queries. It builds without a Databento licence or network connection by default.

**Implementation status:** All six phases are code complete. Credentialed live
confirmations and production-host performance/endurance evidence remain required
for final runtime acceptance; see `Phase6_Implementation.md`.

Ticker feeds also support Databento `statistics` records in the fixed 64-byte
ABI. A nonzero `statistics_replay_start_ns` replays only that schema, marks the
replayed `StatMsg` records, and emits a per-instrument replay-complete control
record before continuing with live statistics. The managed EOD flow uses opening
price and trading-session high/low; quote and trade subscriptions remain live-only.

Live startup stages records encountered during symbol mapping outside the SPSC
ring. Ring publication begins only after the managed drain and its downstream
consumer report ready; the staged records are then released in arrival order.
The default ring holds 131,072 fixed 64-byte records (8 MiB).

## Offline synthetic build

`IFM_DATABENTO_ENABLE_LIVE` defaults to `OFF`, so configuration does not fetch or link the Databento SDK:

```text
cmake -S native/DatabentoFeed.Native -B native/DatabentoFeed.Native/out/build -DIFM_DATABENTO_ENABLE_LIVE=OFF
cmake --build native/DatabentoFeed.Native/out/build --config Release
ctest --test-dir native/DatabentoFeed.Native/out/build -C Release --output-on-failure
```

The Windows build produces `databento_feed_native.dll`; Linux produces `libdatabento_feed_native.so`. The managed project copies an existing configuration-matched Windows DLL into its build output for interop and unit tests.

## Pinned Databento source

- Release: `v0.62.1`
- Annotated tag object: `930624f0987a81bf6e64478055d33e0fbc049af1`
- Source commit: `a37965590f6776ac9659ff496f91fb16c81f76b3`

CMake `FetchContent` retrieves the immutable source commit rather than `main` or the movable tag name. The generated `databento_dependency_metadata.h` embeds the release, source commit, and tag object in the native build.

The first live-enabled configure requires network access. Offline Phase 1/2 builds never enter this dependency path. Later live-enabled configures reuse CMake's populated dependency directory and do not update the pinned source automatically.

## Native prerequisites

Databento `v0.62.1` requires OpenSSL 3 and Zstandard. The checked-in `vcpkg.json` records those dependencies and pins an immutable vcpkg registry commit. That baseline currently resolves OpenSSL 3.6.3 and Zstandard 1.5.7.

After installing CMake 3.24 or later and vcpkg, configure with the vcpkg toolchain. For example:

```text
cmake -S native/DatabentoFeed.Native -B build/databento-native -DCMAKE_TOOLCHAIN_FILE=<vcpkg-root>/scripts/buildsystems/vcpkg.cmake
cmake --build build/databento-native --config Release
```

The native feed target links only to `IFM::DatabentoSdk`, not directly to the vendor target.

## Live Phase 3/4/5 build

The live-enabled build uses `DATABENTO_API_KEY` directly from the native process for both Live and Historical clients. The key is never passed through a managed string or logged. On Windows, cpp-httplib imports certificates from the Windows `ROOT` and `CA` stores into the OpenSSL verification context. If an HTTPS chain requires Windows AIA retrieval, the handshake continues only to cpp-httplib's post-handshake Schannel certificate-policy and hostname validation. `SSL_CERT_FILE` takes precedence as an optional override for an explicitly managed PEM CA bundle, corporate trust roots, containers, or diagnosing host trust configuration. End-to-end TLS verification is never disabled.

On Windows with Visual Studio vcpkg installed:

```powershell
./native/DatabentoFeed.Native/build-native.ps1 -Configuration Release -EnableLive -RunTests
```

An external vcpkg checkout can be selected with `-VcpkgRoot`. The build script keeps registry, download, binary, and installed-package caches below `out/`, which is git-ignored.

Credentialed tests are isolated from the offline unit-test project. Smoke tests
discover only contracts that are current when the test runs:

```powershell
$env:IFM_RUN_DATABENTO_SMOKE_TESTS = '1'
dotnet test ./TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests.csproj -c Release -p:DatabentoEnableLive=true
```

On Windows, the native adapter enables cpp-httplib's Windows system-CA loader
and mandatory Schannel certificate-policy check. This supports chains that need
Windows AIA retrieval while preserving chain and hostname verification.
Set `SSL_CERT_FILE` only when an explicit PEM trust bundle is required:

```powershell
$env:SSL_CERT_FILE = 'C:\path\to\trusted-ca-bundle.pem'
```

`ContractDetailsSmokeTests`, `ContractMappingSmokeTests`, and the definition-only
`OptionChainSmokeTests` use Historical current definitions and can run while the
market is closed. `LiveTickerSmokeTests` also
discovers its future dynamically, but opening the live ticker session still depends
on live-gateway availability and record delivery depends on suitable market hours.
The live option-chain smoke discovers its maturity, underlying, strikes, rights,
raw symbols, instrument IDs, and publisher IDs at runtime, then verifies one live
session reaches running health with one shared managed reader.
`LatestPriceSmokeTests` discovers an activated, unexpired ES future and exercises
last trade, midpoint, bid, and ask through bounded replay or live observation.
`DatabentoOneHourLiveSmokeTests` contains manual ES future and ES futures-option
soak tests. They run for 60 minutes by default and continuously reconcile every
managed tick with the native produced and consumed counters. Run one for a shorter
market-hours verification directly from the command line as follows:

```powershell
dotnet test ./TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests.csproj -c Release -p:DatabentoEnableLive=true -e IFM_RUN_DATABENTO_ONE_HOUR_TESTS=1 -e IFM_DATABENTO_SOAK_MINUTES=5 -e "SSL_CERT_FILE=C:\path\to\trusted-ca-bundle.pem" --filter "FullyQualifiedName~DatabentoOneHourLiveSmokeTests.CurrentEsFutureReceivesEveryTickForConfiguredDuration"
```

Omit `IFM_DATABENTO_SOAK_MINUTES` for the default 60-minute duration. Quote and
trade ticks are subscribed by default. Add `-e IFM_DATABENTO_INCLUDE_MBO=1` only
when the Databento key is entitled to the MBO schema.

For the controlled Monday C++ / Tuesday Rust 3:00-4:00 PM comparison, use the
preflighted launchers and evidence procedure in
[`../native.rust/DatabentoFeed.Rust/docs/market-close-live-comparison.md`](../../native.rust/DatabentoFeed.Rust/docs/market-close-live-comparison.md).

Set `IFM_DATABENTO_TICK_CSV_DIRECTORY` to capture every consumed record in
session order. Each soak test creates a distinct timestamped UTF-8 CSV file and
reports its path, row count, and byte count in the final metrics. CSV capture is
disabled by default because file formatting and I/O are intentionally outside
the production feed's low-latency path:

```powershell
dotnet test ./TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests.csproj -c Release -p:DatabentoEnableLive=true -e IFM_RUN_DATABENTO_ONE_HOUR_TESTS=1 -e IFM_DATABENTO_SOAK_MINUTES=15 -e "IFM_DATABENTO_TICK_CSV_DIRECTORY=C:\DatabentoCaptures" --filter "FullyQualifiedName~DatabentoOneHourLiveSmokeTests.CurrentEsFutureReceivesEveryTickForConfiguredDuration"
```

Prices are written both as the original fixed-point integer and as a decimal
scaled by `1,000,000,000`. Nanosecond timestamps and every quote, trade, and MBO
payload field remain available in raw form for lossless analysis. The soak fails
if the CSV row count does not equal the number of consumed ticks.
That operational confirmation may be run later or during the final all-phases
acceptance pass. The existing smoke asserts authentication, resolution, startup,
running health, and shutdown; final runtime acceptance should separately retain
evidence of current ticker and option records reaching their managed readers. A
failure that exposes a defect reopens the owning phase.

Integration tests always verify a valid connection before exercising fixed,
missing, malformed, or provider-rejected inputs:

```powershell
$env:IFM_RUN_DATABENTO_INTEGRATION_TESTS = '1'
dotnet test ./TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests/TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests.csproj -c Release -p:DatabentoEnableLive=true
```

`IFM_RUN_DATABENTO_LIVE_TESTS=1` remains a compatibility opt-in for both projects.

## Phase 6 RID packaging

Managed resolution is fail-closed and loads the bridge only from
`runtimes/<current-rid>/native`. Windows project builds copy the native bridge and
its live runtime dependencies to `runtimes/win-x64/native`. Linux builds produce
the equivalent `runtimes/linux-x64/native` layout:

```bash
native/DatabentoFeed.Native/build-native.sh --configuration Release --run-tests
```

Add `--enable-live` and set `VCPKG_ROOT` to build the licensed adapter. Both
platform paths use the checked-in vcpkg baseline, immutable Databento commit
`a37965590f6776ac9659ff496f91fb16c81f76b3`, and ABI version 1 metadata.
