using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

/// <summary>
/// Identifies one workflow-owned registration for a transient ticker-data stream.
/// </summary>
/// <param name="WorkflowType">The type of workflow that owns the stream.</param>
/// <param name="WorkflowId">The identifier of the workflow that owns the stream.</param>
/// <param name="LegId">The identifier of the strategy leg that owns the stream.</param>
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
    public bool IsOnTheRun { get; init; }
    public decimal? StrikePrice { get; init; }
    public string? OptionType { get; init; }
    public string? UnderlyingContractId { get; init; }
}

/// <summary>
/// Latest trade state for one ticker, expressed entirely in actor-domain values.
/// </summary>
/// <param name="LastPrice">The last accepted trade price.</param>
/// <param name="LastSize">The quantity of the last accepted trade.</param>
/// <param name="SourceSequence">The provider sequence number for the source record.</param>
/// <param name="EventTimestamp">The timestamp of the source market event.</param>
/// <param name="ReceiveTimestamp">The timestamp when the record was received.</param>
public readonly record struct TickerTradeSnapshot(
    decimal LastPrice,
    uint LastSize,
    long SourceSequence,
    DateTimeOffset EventTimestamp,
    DateTimeOffset ReceiveTimestamp);

/// <summary>
/// Latest quote state for one ticker, expressed entirely in actor-domain values.
/// </summary>
/// <param name="BidPrice">The best bid price.</param>
/// <param name="BidSize">The quantity available at the best bid.</param>
/// <param name="AskPrice">The best ask price.</param>
/// <param name="AskSize">The quantity available at the best ask.</param>
/// <param name="BidCount">The number of orders at the best bid.</param>
/// <param name="AskCount">The number of orders at the best ask.</param>
/// <param name="SourceSequence">The provider sequence number for the source record.</param>
/// <param name="EventTimestamp">The timestamp of the source market event.</param>
/// <param name="ReceiveTimestamp">The timestamp when the record was received.</param>
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
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="InstrumentId">The provider instrument identifier.</param>
/// <param name="PublisherId">The provider publisher identifier.</param>
/// <param name="AssetTypeId">The asset type identifying futures or futures options.</param>
/// <param name="ValueDate">The trading value date associated with the data.</param>
/// <param name="Quote">The bid and ask snapshot, when available.</param>
/// <param name="Trade">The last trade snapshot, when available.</param>
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
/// <param name="Price">The price snapshot or record price represented by this value.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
public readonly record struct OptionTickerPriceSnapshot(
    TickerPriceSnapshot Price,
    OptionGreeksSnapshot? Greeks);
