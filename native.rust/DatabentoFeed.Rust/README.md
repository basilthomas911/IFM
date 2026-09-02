# DatabentoFeed.Rust

Windows Rust implementation of the frozen `databento_feed_native` C ABI. The C++ header at
`native/DatabentoFeed.Native/include/databento_feed_native.h` is the canonical interface.
The managed `NativeMethods` declarations are intentionally unchanged.

The crate produces `databento_feed_native.dll` for `x86_64-pc-windows-msvc`. It supports
the deterministic synthetic feed in the default build and the Databento live feed,
historical contract definitions, and latest-price session through the pinned official
Databento Rust SDK when the `live` feature is enabled.

## Build and test

```powershell
./build-native.ps1 -Configuration Release -RunTests
./build-native.ps1 -Configuration Release -RunTests -EnableLive
```

Run deterministic C++/Rust ABI parity tests and the end-to-end BenchmarkDotNet suite:

```powershell
$env:DBF_CPP_DLL = '<repo>\native\DatabentoFeed.Native\out\build\Release\databento_feed_native.dll'
$env:DBF_RUST_DLL = '<repo>\native.rust\DatabentoFeed.Rust\out\build\Release\databento_feed_native.dll'
dotnet test .\dotnet\DatabentoFeed.Native.ComparisonTests -c Release
dotnet run --project .\dotnet\DatabentoFeed.Native.Benchmarks -c Release
```

The comparison tests dynamically load both DLLs in one process. They verify every frozen
export, all fixed structure sizes, invalid/non-live status behavior, and deterministic
synthetic lifecycle, mapping, record, and statistics parity. Synthetic monotonic timestamps
are intentionally excluded from byte-for-byte comparisons because each library owns an
independent clock origin.

Outputs are staged under `out/build/<Configuration>` or
`out/live-build/<Configuration>`. Only one implementation of the canonical DLL name may
be copied to a managed application's runtime directory.

The existing managed projects keep C++ as the default. Select Rust without changing the
P/Invoke declarations:

```powershell
dotnet test ..\..\TomasAI.IFM.Framework.MarketData.DataBento.UnitTests `
  -c Release -p:DatabentoNativeImplementation=Rust

$env:IFM_RUN_DATABENTO_INTEGRATION_TESTS = '1'
dotnet test ..\..\TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests `
  -c Release -p:DatabentoNativeImplementation=Rust -p:DatabentoEnableLive=true
```

The default Cargo feature set intentionally excludes network support so deterministic ABI
tests do not require an API key. The live build reads `DATABENTO_API_KEY` in the same way as
the C++ implementation. Live smoke tests remain opt-in.

## Performance design

- Fixed 64-byte quote, trade, MBO, and statistics records are normalized directly
  into an SPSC ring. Statistics can replay from a configured session-start
  timestamp and emit an explicit replay-complete control record before live updates.
- Records observed during startup mapping are staged outside the ring and released
  in order only after managed consumer readiness; the default managed configuration
  uses a 131,072-record (8 MiB) ring.
- Producer and consumer cursors occupy separate cache lines and use acquire/release
  publication.
- The record path has no per-record heap allocation or mutex.
- Windows virtual memory and auto-reset events preserve the C++ allocation and wakeup
  behavior.
- Tokio is isolated to the native producer thread and synchronous query sessions; it is
  never exposed through P/Invoke.
- Panics are contained at every exported boundary and translated to `DBF_INTERNAL_ERROR`.

Linux is deliberately out of scope for this phase. Platform-specific code is isolated so a
future Linux implementation can add `mmap`, `eventfd`, and Linux affinity without changing
the ABI.

The initial Windows benchmark and its limitations are recorded in
[`docs/windows-benchmark-2026-08-15.md`](docs/windows-benchmark-2026-08-15.md).
The Monday C++ / Tuesday Rust one-hour live comparison procedure is recorded in
[`docs/market-close-live-comparison.md`](docs/market-close-live-comparison.md).
