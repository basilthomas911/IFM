# IFM Option Pricer Native (Rust)

This crate provides the Windows x64 Rust implementation of the IFM Black-76 numerical kernel. The managed compatibility
contract and complete version-1 ABI are defined in
`../../TomasAI.IFM.Framework.OptionPricer/Docs/Rust-Black76-Native-ABI-v1.md`.

Stages 2 and 3 currently provide:

- the `ifm_option_pricer_native` Windows `cdylib` scaffold;
- the frozen ABI version and scalar result structures;
- scalar Black-76 pricing;
- scalar Black-76 price and Greeks; and
- panic-safe C exports for those scalar operations.

Implied volatility, the fused calculator operation, batch exports, managed P/Invoke integration, differential testing,
and BenchmarkDotNet comparison remain later stages.

## Build and test

Run from PowerShell:

```powershell
.\native.rust\OptionPricer.Rust\build-native.ps1 -Configuration Release -RunTests
```

The release DLL and public header are copied to `out/build/Release`. Generated `target` and `out` directories are not
committed.

The current target is `x86_64-pc-windows-msvc`. Release builds use one code-generation unit, full LTO, optimization
level 3, and unwind-capable panic handling so ABI exports can prevent Rust unwinding across P/Invoke.
