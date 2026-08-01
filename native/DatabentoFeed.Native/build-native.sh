#!/usr/bin/env bash
set -euo pipefail

configuration="Release"
enable_live="OFF"
run_tests="OFF"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --configuration)
      configuration="$2"
      shift 2
      ;;
    --enable-live)
      enable_live="ON"
      shift
      ;;
    --run-tests)
      run_tests="ON"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

case "$configuration" in
  Debug|Release) ;;
  *)
    echo "Configuration must be Debug or Release." >&2
    exit 2
    ;;
esac

source_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ "$enable_live" == "ON" ]]; then
  build_directory="$source_directory/out/linux-live-build"
else
  build_directory="$source_directory/out/linux-build"
fi

configure_arguments=(
  -S "$source_directory"
  -B "$build_directory"
  "-DCMAKE_BUILD_TYPE=$configuration"
  "-DIFM_DATABENTO_ENABLE_LIVE=$enable_live"
  -DIFM_DATABENTO_BUILD_TESTS=ON
)

if [[ "$enable_live" == "ON" ]]; then
  if [[ -z "${VCPKG_ROOT:-}" ]]; then
    echo "VCPKG_ROOT is required for a live Databento build." >&2
    exit 2
  fi
  toolchain="$VCPKG_ROOT/scripts/buildsystems/vcpkg.cmake"
  if [[ ! -f "$toolchain" ]]; then
    echo "The vcpkg toolchain was not found at '$toolchain'." >&2
    exit 2
  fi
  configure_arguments+=(
    "-DCMAKE_TOOLCHAIN_FILE=$toolchain"
    "-DVCPKG_INSTALLED_DIR=$source_directory/out/vcpkg-installed-linux"
  )
fi

cmake "${configure_arguments[@]}"
cmake --build "$build_directory" --config "$configuration"

if [[ "$run_tests" == "ON" ]]; then
  ctest --test-dir "$build_directory" --build-config "$configuration" \
    --output-on-failure --timeout 30
fi

runtime_directory="$source_directory/out/package/runtimes/linux-x64/native"
mkdir -p "$runtime_directory"
cp "$build_directory/libdatabento_feed_native.so" "$runtime_directory/"
