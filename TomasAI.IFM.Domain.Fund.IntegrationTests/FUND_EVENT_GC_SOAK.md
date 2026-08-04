# Fund event GC soak

`FundEventGcSoakTests` is a manual, production-like event ingress test hosted by
`Actor.IntegrationTests`. It publishes a fixed 4 KB `FundCreatedEvent` through
JetStream for ten minutes by default, waits for the durable event consumer to
ACK every message, and reports throughput, allocations, collections, GC pause,
and post-collection retained heap growth. Reusing one entity keeps actor and
database cardinality stable.

Owned pooled event payloads are the default:

```powershell
$env:IFM_RUN_FUND_EVENT_GC_SOAK='1'
dotnet test TomasAI.IFM.Domain.Fund.IntegrationTests `
  --filter FullyQualifiedName~FundEventGcSoakTests `
  --logger "console;verbosity=detailed"
```

For a controlled legacy comparison, run a separate test process with:

```powershell
$env:IFM_RUN_FUND_EVENT_GC_SOAK='1'
$env:IFM_FUND_EVENT_GC_USE_LEGACY_PAYLOADS='1'
dotnet test TomasAI.IFM.Domain.Fund.IntegrationTests `
  --filter FullyQualifiedName~FundEventGcSoakTests `
  --logger "console;verbosity=detailed"
```

Useful overrides are `IFM_FUND_EVENT_GC_SOAK_SECONDS`,
`IFM_FUND_EVENT_GC_MAX_EVENTS`, `IFM_FUND_EVENT_GC_WARMUP_EVENTS`,
`IFM_FUND_EVENT_GC_PAYLOAD_CHARACTERS`, `IFM_FUND_EVENT_GC_MAX_RETAINED_MB`,
and `IFM_FUND_EVENT_GC_REPORT_PATH`.
