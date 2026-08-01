# IFM Databento native dependency

This directory contains the native C++20 ABI, synthetic producer, fixed-slot SPSC ring, portable signal, registered read-buffer ownership, and native tests for Phases 1 and 2. It builds without a Databento licence or network connection by default.

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

The future native feed target should link only to `IFM::DatabentoSdk`, not directly to the vendor target.
