using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog;

public sealed record StrategyCatalogMigrationReport(int StarterDefinitions, int ImportedDeployments, int DeploymentsRequiringProductResolution);
