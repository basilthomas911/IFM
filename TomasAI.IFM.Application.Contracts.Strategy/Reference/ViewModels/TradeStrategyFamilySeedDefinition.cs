// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

public sealed record TradeStrategyFamilySeedDefinition(
    string LegacySystemKey, TradeStrategyFamilyType Family, TradeStrategyType Strategy,
    TimeFrameType TimeFrame, string Symbol, string Currency, string Description)
{
    public string SystemKey => TradeStrategyFamilyReadModel.ComposeSystemKey(Family, Strategy);
    public TradeStrategyFamilyReadModel Create(int id, DateTime createdOnUtc, string createdBy, long version = 1) => new()
    {
        TradeStrategyFamilyId = id,
        DefinitionVersion = version,
        SystemKey = SystemKey,
        Family = Family,
        Strategy = Strategy,
        TimeFrame = TimeFrame,
        Symbol = Symbol,
        Currency = Currency,
        Description = Description,
        State = TradeStrategyFamilyState.Active,
        CreatedOnUtc = createdOnUtc,
        CreatedBy = createdBy,
    };
}
