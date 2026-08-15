# IFM Option Pricer Native (Rust)

This crate provides the Windows x64 Rust implementation of the IFM Black-76 numerical kernel. The managed compatibility
contract and complete version-1 ABI are defined in
`../../TomasAI.IFM.Framework.OptionPricer/Docs/Rust-Black76-Native-ABI-v1.md`.

Stages 2 through 9 provide:

- the `ifm_option_pricer_native` Windows `cdylib` scaffold;
- the frozen ABI version and scalar result structures;
- scalar Black-76 pricing;
- scalar Black-76 price and Greeks; and
- implied-volatility inversion and a fused implied-volatility/Greeks operation;
- zero-allocation structure-of-arrays price and Greeks batches; and
- panic-safe C exports for all implemented operations.

The existing managed API now selects the managed implementation by default. Set
`IFM_OPTION_PRICER_IMPLEMENTATION=Rust`, or set the `TomasAI.IFM.OptionPricer.Implementation` AppContext property to
`Rust`, before the first option-pricer call to select the native implementation. Selection and ABI validation occur
once per process. A missing DLL, unsupported platform, invalid configured value, or ABI mismatch fails explicitly; the
runtime never silently falls back to managed pricing.

The Stage 7-9 gate covers 100,000 randomized scalar contracts, 5,000 randomized implied-volatility cases, 4,096 batch
contracts, finite-difference Greeks, native layout/FFI behavior, steady-state allocation, and direct BenchmarkDotNet
comparison. Managed remains the default because scalar Rust calls still pay P/Invoke overhead. Large native Greeks
batches reach Managed parity, but price batches remain about 8-9% slower on the measured workstation, so there is no
automatic crossover and no `SuppressGCTransition` policy.

## Build and test

Run from PowerShell:

```powershell
.\native.rust\OptionPricer.Rust\build-native.ps1 -Configuration Release -RunTests
```

The release DLL and public header are copied to `out/build/Release`. Generated `target` and `out` directories are not
committed.

The current target is `x86_64-pc-windows-msvc`. Release builds use one code-generation unit, full LTO, optimization
level 3, and unwind-capable panic handling so ABI exports can prevent Rust unwinding across P/Invoke.

## Managed publish layout

Build the native artifact before publishing an application that enables Rust:

```powershell
.\native.rust\OptionPricer.Rust\build-native.ps1 -Configuration Release -RunTests
dotnet publish <application-project> -c Release
```

Verify the publish output contains:

```text
runtimes/win-x64/native/ifm_option_pricer_native.dll
```

Then smoke-test the published process with `IFM_OPTION_PRICER_IMPLEMENTATION=Rust`. Rust selection is Windows x64 only
and fails explicitly for a missing DLL, unsupported process architecture, or ABI mismatch. Linux packaging is outside
the version-1 Windows scope.

Run the managed/native benchmarks with:

```powershell
dotnet run -c Release --project .\TomasAI.IFM.Domain.OptionPricer.Benchmarks -- `
  --filter "*RustOptionPricer*" --join
```
