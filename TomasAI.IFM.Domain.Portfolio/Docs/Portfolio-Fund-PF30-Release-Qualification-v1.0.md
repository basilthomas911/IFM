# Portfolio/Fund PF-30 Release Qualification v1.0

| Field | Value |
| --- | --- |
| Gate | PF-30 — Operational qualification and release approval |
| Qualification date | 2026-08-30 |
| Result | Complete |
| Baseline commit | `3641d658dba5f9a5c47135f877ba1fd81f44e40d` |
| Runtime topology | Production API actor host, real NATS, PostgreSQL EventSourceDb/SequenceIdDb, ScyllaDB PortfolioDb/ReferenceDb, Redis |

## Release decision

Approve the Portfolio/Reference/Trade Orders pre-execution bounded context for release. Authorization enforcement, bounded telemetry, readiness health, performance, clean restart, deterministic rebuild, and mutation-disable rollback are executable and green. The release retains the hard stop before OrderExecution and has no broker, fill, live TradeDb, live-position, or legacy-write authority.

This is not approval of the complete automated strategy pipeline. PF-13, PF-14, and PF-15 remain explicitly blocked until production TradeSelection, OrderComposition, and RiskManagement actors exist. Their Portfolio-side contracts, validation, idempotency, state transitions, projections, and no-execution fences remain qualified.

## Implemented operational controls

- Additive MessagePack access metadata carries principal and roles without changing keys 0–8.
- Every Portfolio command/query verb maps to one fixed `PortfolioOperation`; actors reject missing, anonymous, or unauthorized identities before mutation.
- Audit events record the asserted caller principal instead of a hard-coded actor name.
- Error `34016` represents authorization denial; `34017` represents an operator-disabled path.
- `Portfolio:Operations` independently controls all Portfolio paths, queries, mutations, and mandatory authorization.
- `/health/ready` reports the effective Portfolio mode alongside actor/runtime health.
- `TomasAI.IFM.Domain.Portfolio` is registered as both an OpenTelemetry ActivitySource and Meter.
- Metrics label only bounded operation/outcome dimensions; principals, workflow/order/trade IDs, hashes, exceptions, and secrets are absent.
- NATS account authentication and subject ACLs are the transport trust boundary for accepting the signed-in application’s asserted principal/roles.

## Captured evidence

| Concern | Result |
| --- | --- |
| BDD/personas | Administrator mutation, workflow continuation, reader query, reader mutation denial, anonymous denial, and absence of execution authority pass |
| Trace capture | Activity listener captured operation and correlation ID; principal was absent |
| Metric capture | Meter listener captured `portfolio.authorization.checks` with only operation/outcome labels |
| Health | Live `/health/ready` was Healthy with 126 registered actor types and Portfolio `{ enabled:true, queriesEnabled:true, mutationsEnabled:true, authorizationRequired:true }` |
| Authorization | Live NATS admin create and reader query passed; reader mutation and anonymous query returned `34016`; authority revision remained 1 |
| Portfolio load | 128 reads, 8 concurrent clients, 334.5 ms total, 42.9 ms p95, zero failures; bound was p95 < 1,000 ms and total < 30 s |
| Reference load | 64 reads, 8 concurrent clients, 533.1 ms total, 155.6 ms p95, exact three-family catalog, zero failures |
| Restart | Portfolio 901 was committed, host stopped cleanly, host restarted, and authority/projection returned revision 1 |
| Rebuild | Representative Portfolio/Fund/policy/order/trade/workflow authority rebuilt twice from PostgreSQL into empty Scylla; both reports and catalog hashes matched |
| Rollback | Host restarted with `Portfolio__Operations__MutationsEnabled=false`; health declared mutations disabled, mutation returned `34017`, Draft query remained available |
| UI | PF-27 rendered STA message-loop acceptance and PF-28 Trade Orders UI journeys remain green and are the operator-facing acceptance evidence |

## Reproduction commands

```powershell
dotnet build TomasAI.IFM.Domain.Portfolio.UnitTests/TomasAI.IFM.Domain.Portfolio.UnitTests.csproj --no-restore -m:1
dotnet test TomasAI.IFM.Domain.Portfolio.UnitTests/TomasAI.IFM.Domain.Portfolio.UnitTests.csproj --no-build --filter "Gate=PF-30"
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests/TomasAI.IFM.Domain.Portfolio.IntegrationTests.csproj --no-build --filter "Gate=PF-30&Category!~PortfolioLiveHost"

# With the production API host online:
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests/TomasAI.IFM.Domain.Portfolio.IntegrationTests.csproj --no-build --filter "Category=PortfolioLiveHostPF30"
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests/TomasAI.IFM.Domain.Portfolio.IntegrationTests.csproj --no-build --filter "Category=PortfolioLiveHostPF29"

# After a clean restart, using the ID printed by the authorization test:
$env:IFM_PORTFOLIO_PF30_ID='901'
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests/TomasAI.IFM.Domain.Portfolio.IntegrationTests.csproj --no-build --filter "Category=PortfolioLiveHostPF30Restart"

# Start the host with Portfolio__Operations__MutationsEnabled=false, then:
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests/TomasAI.IFM.Domain.Portfolio.IntegrationTests.csproj --no-build --filter "Category=PortfolioLiveHostPF30Rollback"
```

## Historical Partial reconciliation

| Historical gate | PF-30 disposition |
| --- | --- |
| PF-10 | Complete through PF-26 typed-route coverage plus PF-30 access/error/restart qualification |
| PF-16 | Complete; superseded and requalified by PF-27 compact Portfolio/Risk Policy UI |
| PF-17 | Complete; obsolete Planned Compositions surface superseded by PF-28 Trade Orders |
| PF-18 | Complete through PF-29 regression and PF-30 operational/live-host evidence |
| PF-19 | Complete through PF-29 legacy isolation, subject/store, and prohibited-dependency audits |
| PF-20 | Complete through production actor authorization, trace/metric capture, health, load, restart, rebuild, and rollback evidence |

## Known deferrals

The implementation-plan deferred register remains authoritative. In particular, broker execution/fills/live positions, TradeDb execution integration, management UI for strategy-family mutations/subtypes, legacy migration/deletion, and high-throughput Scylla identity redesign remain outside PF-30.
