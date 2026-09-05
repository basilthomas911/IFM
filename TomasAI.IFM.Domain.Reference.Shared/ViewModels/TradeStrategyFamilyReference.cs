using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

[MessagePackObject]
public sealed record TradeStrategyFamilyReference(
    [property: Key(0)] int TradeStrategyFamilyId,
    [property: Key(1)] long DefinitionVersion)
{
    [IgnoreMember] public bool IsValid => TradeStrategyFamilyId > 0 && DefinitionVersion > 0;
    public static TradeStrategyFamilyReference From(TradeStrategyFamilyReadModel row) => new(row.TradeStrategyFamilyId, row.DefinitionVersion);
}

[MessagePackObject]
public sealed record CreateTradeStrategyFamilyRequest
{
    [Key(0)] public Guid OperationId { get; init; }
    [Key(1)] public TradeStrategyFamilyType Family { get; init; }
    [Key(2)] public TradeStrategyType Strategy { get; init; }
    [Key(3)] public TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType TimeFrame { get; init; }
    [Key(4)] public int TradeStrategySymbolId { get; init; }
    [Key(5)] public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (OperationId == Guid.Empty) errors.Add("OperationId is required.");
        if (TradeStrategySymbolId <= 0) errors.Add("Select a product from the market-data catalog.");
        if (!TradeStrategyTimeFrames.IsAllowed(TimeFrame)) errors.Add("TimeFrame must be Daily, Weekly or Monthly.");
        if (Family == TradeStrategyFamilyType.Futures ? Strategy != TradeStrategyType.Futures :
            Family != TradeStrategyFamilyType.FuturesOption || Strategy is not (TradeStrategyType.IronCondor or TradeStrategyType.VerticalSpread))
            errors.Add("The family/strategy combination is not supported.");
        if (string.IsNullOrWhiteSpace(Description) || Description.Length > 512) errors.Add("Description must contain 1-512 characters.");
        return errors;
    }
}
