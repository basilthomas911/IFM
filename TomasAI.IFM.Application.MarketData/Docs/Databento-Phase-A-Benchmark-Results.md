# DataBento IMarketDataApi Phase A benchmark results

**Run date:** 2026-08-10
**Source state:** uncommitted Phase A working tree based on
`26668362af811a734ce139bab9d178019c7fa63a`
**Configuration:** Release, `net10.0`

## Environment

- CPU: AMD Ryzen Threadripper 1950X 16-Core Processor
- Logical processors: 32
- OS: Windows 10.0.19045, win-x64
- .NET runtime: 10.0.10
- .NET SDK: 10.0.302
- Dataset/input shape: deterministic in-memory DataBento normalized quote and
  trade snapshots; two epoch slots; one writer and one non-consuming reader

## Latest-value store and reader

Command:

```powershell
dotnet run --project `
  ./TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks.csproj `
  -c Release -- --last-price --operations=5000000 --strict
```

| Operation | Throughput | Allocation |
| --- | ---: | ---: |
| Quote slot update | 7,410,303 operations/s | 0 B/op |
| Quote reader | 40,765,179 operations/s | 0 B/op |

The strict gate also verified that an existing epoch-bound reader returns a
miss after store invalidation.

## Application futures-price facade

Command:

```powershell
dotnet run --project `
  ./TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks/TomasAI.IFM.Framework.MarketData.DataBento.Benchmarks.csproj `
  -c Release -- --api-price --operations=1000000 --strict
```

| Operation | Throughput | Allocation |
| --- | ---: | ---: |
| `GetFuturesPriceAsync` over hot reader | 9,184,912 calls/s | 80 B/call |

The facade allocation is the completed `Task<decimal>` required by the existing
public contract; the underlying slot update/read paths remain allocation-free.

## Qualification scope

These results are a repeatable Phase A implementation baseline and pass the
local strict gates (at least 1M updates/s, 5M reads/s, zero slot-path allocation,
and at least 1M facade calls/s with no more than 128 B/call). They are not the
30-minute pre-production run, 24-hour soak, or 5M/10M full-pipeline SWO-10
qualification. Those gates require the target deployment host and full live
publisher/storage topology.
