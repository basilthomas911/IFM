# IFM Databento native dependency

This directory contains the native C++20 ABI, synthetic producer, fixed-slot SPSC ring, portable signal, registered read-buffer ownership, the `LiveBlocking` ticker, option-chain, and one-shot latest-price adapters, and Historical contract-definition queries. It builds without a Databento licence or network connection by default.

**Phase 5 status:** Code complete. Credentialed latest-price and earlier live runtime confirmations are deferred and do not block Phase 6; they remain required for final runtime acceptance.

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

Databento `v0.62.1` requires OpenSSL 3 and Zstandard. The checked-in `vcpkg.json` records those dependencies and the same vcpkg baseline used by the pinned Databento release.

After installing CMake 3.24 or later and vcpkg, configure with the vcpkg toolchain. For example:

```text
cmake -S native/DatabentoFeed.Native -B build/databento-native -DCMAKE_TOOLCHAIN_FILE=<vcpkg-root>/scripts/buildsystems/vcpkg.cmake
cmake --build build/databento-native --config Release
```

The native feed target links only to `IFM::DatabentoSdk`, not directly to the vendor target.

## Live Phase 3/4/5 build

The live-enabled build uses `DATABENTO_API_KEY` directly from the native process for both Live and Historical clients. The key is never passed through a managed string or logged. On OpenSSL-based Windows builds, set `SSL_CERT_FILE` to a trusted PEM CA bundle; TLS verification is not disabled when this variable is absent.

On Windows with Visual Studio vcpkg installed:

```powershell
./native/DatabentoFeed.Native/build-native.ps1 -Configuration Release -EnableLive -RunTests
```

An external vcpkg checkout can be selected with `-VcpkgRoot`. The build script keeps registry, download, binary, and installed-package caches below `out/`, which is git-ignored.

Credentialed tests are isolated from the offline unit-test project. Smoke tests
discover only contracts that are current when the test runs:

```powershell
$env:IFM_RUN_DATABENTO_SMOKE_TESTS = '1'
$env:SSL_CERT_FILE = 'C:\path\to\trusted-ca-bundle.pem'
dotnet test ./TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests/TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests.csproj -c Release -p:DatabentoEnableLive=true
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
