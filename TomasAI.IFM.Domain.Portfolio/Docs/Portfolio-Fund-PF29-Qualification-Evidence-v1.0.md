# Portfolio/Fund PF-29 Qualification Evidence v1.0

| Field | Value |
| --- | --- |
| Gate | PF-29 — Cross-pipeline qualification and regression |
| Qualification date | 2026-08-30 |
| Result | Complete |
| Runtime topology | Real NATS, PostgreSQL EventSourceDb, ScyllaDB PortfolioDb/ReferenceDb, production API actor host |

## Exit decision

PF-29 is complete. The revised Portfolio/Reference/Trade Orders surface has green five-layer evidence, the production actor path is qualified, bounded concurrent typed queries retain exact results, least-privilege outcomes are exhaustive for every representable Portfolio operation, and the bounded context has no execution or legacy-write dependency.

Authenticated NATS identity enforcement, operational telemetry/health capture, sustained performance baselines, feature rollback, and operator release approval remain PF-30 obligations. PF-13 through PF-15 remain separately blocked on the future production TradeSelection, OrderComposition, and RiskManagement actors; PF-29 does not waive or simulate those actors.

## PF-21 through PF-29 traceability

| Gate | Requirement represented | Executable evidence |
| --- | --- | --- |
| PF-21 | Revised identities/contracts and obsolete composition surface removal | `PortfolioAdministrationUiSystemTests`, `PortfolioTradeOrdersUiSystemTests` |
| PF-22 | Exact three-family catalog, repeatable bootstrap, read-only query/UI | `TradeStrategyFamilyBootstrapProcessIntegrationTests`, `PortfolioLiveHostEndToEndTests`, `TradeStrategyFamilyReferenceUiSystemTests` |
| PF-23 | Versioned policy validation, family/global limits, fail-closed capacity | `PortfolioFinancialPolicyScenarios`, `PortfolioFinancialPolicyAggregateTests`, `PortfolioFinancialPolicyCatalogVerificationTests`, `PortfolioAdministrationUiSystemTests` |
| PF-24 | Policy lifecycle, atomic replacement, recovery, concurrency | `PortfolioFinancialPolicyScenarios`, `PortfolioFinancialPolicyActorRecoveryTests`, `PortfolioEventStoreIntegrationTests`, `PortfolioFinancialPolicyAggregateTests` |
| PF-25 | Durable policy persistence, tombstones, delayed delivery, rebuild/hash equivalence | `PortfolioDbIntegrationTests`, `PortfolioEventStoreIntegrationTests`, `PortfolioRealProjectionIntegrationTests`, `PortfolioProjectionHandlerTests` |
| PF-26 | Typed Reference/policy APIs and frozen identity/version/cap propagation | `PortfolioLiveHostEndToEndTests`, `PortfolioFinancialPolicyCatalogVerificationTests` |
| PF-27 | Compact administration/Risk Policy UI, rendered message loop, dirty close, unauthorized controls | `PortfolioRiskPolicyMessageLoopAcceptanceTests`, `PortfolioAdministrationUiSystemTests`, plus policy BDD/unit/integration/verification traits |
| PF-28 | Unified manual/automated Trade Orders, integer allocation, stale-response fencing | `PortfolioCompositionScenarios`, `PortfolioFundCommandClientTests`, `PortfolioLiveHostEndToEndTests`, `PortfolioWorkflowTests`, `PortfolioCompositionCatalogVerificationTests`, `PortfolioTradeOrdersUiSystemTests` |
| PF-29 | Cross-layer authorization, concurrency/load, exact representative catalogs, regression and prohibited effects | `PortfolioAuthorizationScenarios`, `PortfolioOperationalPolicyTests`, `PortfolioPf29LiveQualificationTests`, `PortfolioCompositionCatalogVerificationTests`, `PortfolioLegacyIsolationTests`, PF-29-tagged UI tests |

## Focused PF-29 evidence

| Layer | Passed | Failed | Skipped | Coverage |
| --- | ---: | ---: | ---: | --- |
| BDD | 7 | 0 | 0 | Four documented personas, global/family policy outcomes, canonical manual Draft behavior |
| Unit | 44 | 0 | 0 | All 36 operation/persona pairs, policy caps, manual/automated boundaries, execution absence, legacy isolation |
| Integration | 2 | 0 | 0 | Production pipeline journey and bounded concurrent production NATS query load |
| Verification | 10 | 0 | 0 | Three-family/global/envelope catalogs, manual/automated identity, no execution dependency |
| UI system | 6 | 0 | 0 | Read-only Reference, rendered Risk Policy/unauthorized/dirty-close, and Portfolio-first Trade Orders/stale-response journeys |

The production load case issued 64 typed Reference queries through eight concurrent NATS clients. Every response returned the exact ordered Active catalog (`FUTURES`, `VERTICAL_SPREAD`, `IRON_CONDOR`). The run completed in 597.2 ms with 204.6 ms p95 request latency and no failure before the 30-second qualification bound.

## Full regression evidence

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Portfolio unit | 93 | 0 | 0 |
| Portfolio BDD | 22 | 0 | 0 |
| Portfolio integration, isolated responder topology | 29 | 0 | 0 |
| Portfolio verification | 28 | 0 | 0 |
| Portfolio UI system | 17 | 0 | 0 |
| Portfolio UI presentation | 4 | 0 | 0 |
| Reference unit + integration | 24 | 0 | 0 |
| NATS unit + integrated | 132 | 0 | 0 |
| Trade pipeline unit + BDD + focused integrated + verification | 451 | 0 | 0 |

The Reference BDD project currently discovers no tests and is not counted as evidence or a skip. Reference behavior is covered by its unit/integration suites and the Portfolio production-host Reference tests.

## Topology and repeatability notes

Production-host tests and isolated real-NATS responder tests are deliberately separate:

1. Start the production API host for `PortfolioLiveHostPipeline` and `PortfolioLiveHostPF29`.
2. Stop the production host before running the default `Category=Portfolio` integration suite because `PortfolioRealNatsRouteTests` owns an isolated responder on the same command subject.
3. A combined attempt correctly exposed two responders. After restoring the declared single-responder topology, all 29 default integration tests passed. This is explained environment interference, not a product failure or accepted flake.

The production pipeline journey proved configuration, Reference family, policy activation/assignment, Portfolio/Fund resolution, idempotent integer reservation, composition reference, Risk outcome reference, authoritative PostgreSQL state, and Scylla projections. The journey stops before OrderExecution.

## Prohibited-effect proof

- `PortfolioOperationalPolicy` contains no execution operation or role.
- Portfolio production assemblies have no broker, OrderExecution, live TradeDb, or live-position dependency.
- `FundLegacyDbContext` exposes no mutation interface.
- New Portfolio command/query, projection, and UI paths do not reference legacy Fund write services.
- Manual and StrategyWorkflow records remain pre-execution composition records distinguished by origin.

## Reproduction commands

```powershell
dotnet test TomasAI.IFM.Domain.Portfolio.UnitTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.Domain.Portfolio.BDDTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.Domain.Portfolio.VerificationTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.UI.Net.SystemTests --filter "Category=Portfolio"
dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests --filter "Category=Portfolio"

# Run with the production API actor host online.
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests --filter "Category=PortfolioLiveHostPipeline"
dotnet test TomasAI.IFM.Domain.Portfolio.IntegrationTests --filter "Category=PortfolioLiveHostPF29"
```
