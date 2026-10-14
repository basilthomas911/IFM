# Development ES reference restoration

This maintenance tool restores missing Development lookup choices and ES option drafts through the normal reference command APIs. Existing contracts are preserved. It refuses a database other than `ifm_application_development`.

```powershell
$env:DOTNET_ENVIRONMENT='Development'
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project tools/DevelopmentReferenceRestore -- --plan
dotnet run --project tools/DevelopmentReferenceRestore -- --apply --fresh
```

`--plan` inspects the published stored catalog without writes. `--apply --fresh` requests only `ES.FUT` and `ES.OPT` from Databento using the existing credential configuration. It stores the raw definitions under a new snapshot and verifies provider identity, digest and exact decimal strike after each missing contract is projected. It does not replace the global published catalog.

The completed run on 2026-10-13 stored 8,816 raw records, selected 2,903 current definitions, added seven missing lookup values and 2,882 option drafts, and preserved all existing futures. Evidence: `.artifacts/rust-ui/tests/databento-reference-restoration.json`.

Drafts are not reviewed trading references. Exercise, settlement, exchange-calendar and variable tick conventions still require review. Existing legacy underlying contracts are preserved and need their own provider-evidence review before qualification. The price-point multiplier mapper currently verifies ES/USD/IPNT economics; unsupported products remain unknown.
