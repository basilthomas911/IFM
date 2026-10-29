# Managed strike-window oracle

This qualification tool executes the unchanged copied C# selectors without
opening a provider, broker, storage connection or native runtime. The committed
Zstandard fixture retains 1,200 deterministic cases in about 800 KiB. Rust tests
compare ordered displayed/buffered membership, numeric Decimal bounds and labels.
Exact Decimal arithmetic bits have a separate CLR oracle.

Regenerate from the repository root in PowerShell:

```powershell
dotnet run --project TomasAI.IFM.Application.MarketData.Rust/qualification/WindowOracle -- C:/repos/IFM/.artifacts/rust-marketdata-offline/window-oracle.json
$env:IFM_WINDOW_ORACLE_JSON='C:/repos/IFM/.artifacts/rust-marketdata-offline/window-oracle.json'
cargo test --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --test browser_window refresh_managed_fixture -- --ignored
cargo test --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --test browser_window
```

Fixture regeneration is explicitly ignored in ordinary regression runs because
it overwrites the checked-in oracle. Browser Host wiring remains a separate gate.
