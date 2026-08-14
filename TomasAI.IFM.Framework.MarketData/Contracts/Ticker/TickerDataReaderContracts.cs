using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

/// <summary>
/// Identifies the workflow-owned use of one transient ticker reader.
/// </summary>
public readonly record struct TickerReaderOwner(
    string WorkflowType,
    string WorkflowId,
    string LegId)
{
    /// <summary>
    /// Validates that every component required for deterministic lease ownership is present.
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkflowType);
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(LegId);
    }
}

/// <summary>
/// Identifies one active lease over a TickAggregation-owned contract stream.
/// </summary>
public readonly record struct TickerStreamLease(
    Guid LeaseId,
    string ContractId,
    TickerReaderOwner Owner,
    long StreamGeneration);

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
/// Describes why a ticker lease can no longer authorize an aggregation read.
/// </summary>
public enum TickerLeaseFailureReason
{
    Unknown = 0,
    ServiceNotRunning = 1,
    ContractNotConfigured = 2,
    LeaseNotFound = 3,
    LeaseReleased = 4,
    ContractMismatch = 5,
    StaleGeneration = 6
}

/// <summary>
/// Raised when TickAggregation cannot confirm that a reader lease is currently active.
/// </summary>
public sealed class TickerLeaseNotActiveException : InvalidOperationException
{
    public TickerLeaseNotActiveException(
        TickerStreamLease lease,
        TickerLeaseFailureReason reason)
        : base($"Ticker lease '{lease.LeaseId}' for contract '{lease.ContractId}' is not active ({reason}).")
    {
        Lease = lease;
        Reason = reason;
    }

    public TickerStreamLease Lease { get; }
    public TickerLeaseFailureReason Reason { get; }
}

/// <summary>
/// A transient workflow-owned capability for reading TickAggregation state.
/// </summary>
public interface ITickerDataReader : IAsyncDisposable
{
    string ContractId { get; }
    TickerReaderOwner Owner { get; }
    TickerStreamLease Lease { get; }
    TickerContractDetails GetContractDetails();
    bool TryGetPrice(out TickerPriceSnapshot snapshot);
    bool TryGetOptionPrice(out OptionTickerPriceSnapshot snapshot);
}

/// <summary>
/// Creates idempotent owner-scoped ticker readers and maintains their shared contract leases.
/// </summary>
public interface ITickerDataReaderFactory
{
    ValueTask<ITickerDataReader> CreateAsync(
        TickerReaderOwner owner,
        string contractId,
        CancellationToken cancellationToken = default);
}
