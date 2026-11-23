using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

/// <summary>A pure fund decision. It owns no order/trade identifiers and creates no capacity reservation.</summary>
[MessagePackObject]
public sealed record PortfolioFundEntryAssessment(
    [property: Key(0)] int FundId,
    [property: Key(1)] bool Accepted,
    [property: Key(2)] string ReasonCode,
    [property: Key(3)] PortfolioAcceptedCapacityEffect? CapacityEffect);
