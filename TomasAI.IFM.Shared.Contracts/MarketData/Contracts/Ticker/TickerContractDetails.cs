using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

/// <summary>
/// Describes the provider-neutral contract identity cached for an active aggregation epoch.
/// </summary>
public sealed record TickerContractDetails
{
    public required string ContractId { get; init; }
    public required uint InstrumentId { get; init; }
    public required ushort PublisherId { get; init; }
    public required AssetTypeId AssetTypeId { get; init; }
    public required string Dataset { get; init; }
    public required DateOnly DefinitionDate { get; init; }
    public string ProviderContractId { get; init; } = string.Empty;
    public string Ticker { get; init; } = string.Empty;
    public string LocalSymbol { get; init; } = string.Empty;
    public string SecurityType { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string Exchange { get; init; } = string.Empty;
    public decimal ContractMultiplier { get; init; } = 1m;
    /// <summary>The minimum trade-price increment in price units, when supplied by the provider.</summary>
    public decimal? TickSize { get; init; }
    public DateOnly MaturityDate { get; init; }
    public bool IsOnTheRun { get; init; }
    public decimal? StrikePrice { get; init; }
    public string? OptionType { get; init; }
    public string? UnderlyingContractId { get; init; }
}
