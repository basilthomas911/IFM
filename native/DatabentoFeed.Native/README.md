# IFM Databento native dependency

This directory contains the native CMake foundation for the Databento market-data bridge. The feed itself is intentionally not implemented yet.

## Pinned Databento source

- Release: `v0.62.1`
- Annotated tag object: `930624f0987a81bf6e64478055d33e0fbc049af1`
- Source commit: `a37965590f6776ac9659ff496f91fb16c81f76b3`

CMake `FetchContent` retrieves the immutable source commit rather than `main` or the movable tag name. The generated `databento_dependency_metadata.h` embeds the release, source commit, and tag object in the native build.

The first configure requires network access. Later configures reuse CMake's populated dependency directory and do not update the pinned source automatically.

## Native prerequisites

Databento `v0.62.1` requires OpenSSL 3 and Zstandard. The checked-in `vcpkg.json` records those dependencies and the same vcpkg baseline used by the pinned Databento release.

After installing CMake 3.24 or later and vcpkg, configure with the vcpkg toolchain. For example:

```text
cmake -S native/DatabentoFeed.Native -B build/databento-native -DCMAKE_TOOLCHAIN_FILE=<vcpkg-root>/scripts/buildsystems/vcpkg.cmake
cmake --build build/databento-native --config Release
```

The future native feed target should link only to `IFM::DatabentoSdk`, not directly to the vendor target.
