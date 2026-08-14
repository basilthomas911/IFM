using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

/// <summary>
/// Identifies one workflow-owned registration for a transient ticker-data stream.
/// </summary>
public readonly record struct TickerStreamOwner(
    string WorkflowType,
    string WorkflowId,
    string LegId)
{
    /// <summary>
    /// Validates that every component required for deterministic stream ownership is present.
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkflowType);
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(LegId);
    }
}

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
    public DateOnly MaturityDate { get; init; }
    public bool IsCurrentlyTraded { get; init; }
    public decimal? StrikePrice { get; init; }
    public string? OptionType { get; init; }
    public string? UnderlyingContractId { get; init; }
}

/// <summary>
/// Latest trade state for one ticker, expressed entirely in actor-domain values.
/// </summary>
public readonly record struct TickerTradeSnapshot(
    decimal LastPrice,
    uint LastSize,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp);

/// <summary>
/// Latest quote state for one ticker, expressed entirely in actor-domain values.
/// </summary>
public readonly record struct TickerQuoteSnapshot(
    decimal? BidPrice,
    uint BidSize,
    decimal? AskPrice,
    uint AskSize,
    uint BidCount,
    uint AskCount,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp);

/// <summary>
/// Combines the independently advancing latest trade and quote state for one contract.
/// </summary>
public readonly record struct TickerPriceSnapshot(
    string ContractId,
    uint InstrumentId,
    ushort PublisherId,
    AssetTypeId AssetTypeId,
    DateOnly ValueDate,
    TickerQuoteSnapshot? Quote,
    TickerTradeSnapshot? Trade);

/// <summary>
/// Adds optional option valuation state to the common ticker price snapshot.
/// </summary>
public readonly record struct OptionTickerPriceSnapshot(
    TickerPriceSnapshot Price,
    OptionGreeksSnapshot? Greeks);

/// <summary>
