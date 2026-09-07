using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>Portfolio-facing deployment choice. Display labels never grant permission.</summary>
public sealed record StrategyDeploymentChoice(CatalogKey Key, string Code, string Name, CatalogLifecycleStatus Status,
    TimeFrameType Horizon, CatalogProduct[] Products, string[] InstrumentClasses, CatalogPipelineParameter[] PipelineParameters,
    string[] VariantNames)
{
    public string SystemKey => Code;
    public string Description => Name;
    public TimeFrameType TimeFrame => Horizon;
    public string Symbol => string.Join(", ", Products.Select(x => x.Symbol).Distinct());
    public string Exchange => string.Join(", ", Products.Select(x => x.Exchange).Distinct());
    public string Currency => string.Join(", ", Products.Select(x => x.Currency).Distinct());
    public long DefinitionVersion => Key.Version;
    public TradeStrategyFamilyReference Reference => new(0, 0) { CatalogDeployment = Key };
}

public sealed record StrategyDeploymentPage(StrategyDeploymentChoice[] Items, string? NextCode);
