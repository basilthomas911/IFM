# Approved trade-limit recovery

Use this backend tool for an existing Iron Condor whose monitoring limits are missing. It reads the original Portfolio acceptance and the exact stored opening fills. It never substitutes current cash, quotes, or default risk amounts.

Build and restart the API with the approved-limit changes first. No additional connection settings are required when running from this repository.

The tool automatically reads the API's `ConnectionStrings:TradeDbConnection`, the UI's `AppSettings:NatsServerUri`, and the existing API database credentials through the shared credential resolver. It loads `appsettings.json` and the selected environment's settings, followed by environment-variable overrides. The environment comes from `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT`, defaulting to Development.

`IFM_TRADE_RECOVERY_POSTGRES` and `IFM_UI_NATS_URL` remain optional overrides for a deliberately different endpoint; they are not required for normal use.

Preview:

```powershell
dotnet run --project tools/TradeLimitRecovery -- 101.701.1901.1501
```

Apply the verified calculation:

```powershell
dotnet run --project tools/TradeLimitRecovery -- 101.701.1901.1501 --apply
```

The command owner checks the original approval and stored trade again inside its transaction. Recovery appends an audited monitoring initialization event and its limit rows atomically. Existing limits are preserved. Missing/ambiguous approvals, mismatched identities, missing fills and conflicting evidence fail explicitly. The tool never places or retries a trade.
