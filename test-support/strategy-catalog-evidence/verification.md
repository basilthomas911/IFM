# Strategy catalog verification

Date: 2026-09-06

277 tests passed; no failures/skips in the listed final runs.

- `dotnet test TomasAI.IFM.Application.Storage.IntegrationTests --no-restore --filter FullyQualifiedName~ConfigurationDb`: 59.
- `dotnet test TomasAI.IFM.Domain.Reference.UnitTests --no-restore`: 51.
- `dotnet test TomasAI.IFM.Domain.Portfolio.UnitTests --no-restore`: 130.
- `dotnet test TomasAI.IFM.Domain.Reference.IntegrationTests --no-restore --filter FullyQualifiedName~ConfigurationStrategyCatalogActor`: 1.
- `dotnet test TomasAI.IFM.UI.Net.SystemTests --no-restore --filter "FullyQualifiedName~ConfigurationStrategyCatalogUi|FullyQualifiedName~TradeFamilyCatalog|FullyQualifiedName~TradeStrategyTimeFrame|FullyQualifiedName~FundAssignment|FullyQualifiedName~PortfolioRiskPolicy"`: 36.

Migration maintenance mode completed against Development configuration: 18 starter definitions, 4 legacy deployment imports, 0 requiring product resolution. Repeat execution verified existing exact definitions without duplicates. No automatic publication or Fund permission changes.

The screenshot is from the actual ReferenceForm hosted on a WinForms STA message loop with fixture APIs. Actor integration uses real PostgreSQL plus a fixture actor context, rather than a live NATS network acceptance run. User visual acceptance remains outstanding before Legacy code deletion.
