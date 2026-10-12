using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>GenerationId may be empty to read the single currently admitted generation atomically. Pinned universe and quality constraints supplied by the accepted workflow policy.</summary>
public sealed record OptionChainSnapshotRequest(string ScopeId, Guid GenerationId, DateOnly ValueDate,
    string Horizon, DateTimeOffset DeadlineUtc, int MaximumQuoteAgeMilliseconds = 1000,
    int MaximumQuoteSkewMilliseconds = 250)
{
    /// <summary>Accepted catalog structure identity; nonempty identities must match the prepared global policy exactly.</summary>
    public Guid StrategyDefinitionId { get; init; }
    /// <summary>Accepted immutable catalog structure version.</summary>
    public int StrategyDefinitionVersion { get; init; }
    /// <summary>Canonical underlying root from prior pipeline operators.</summary>
    public string InstrumentRoot { get; init; } = "ES";
    /// <summary>Originating workflow identity for diagnostics; never changes resident data.</summary>
    public Guid WorkflowId { get; init; }
    /// <summary>Originating accepted workflow revision.</summary>
    public long WorkflowRevision { get; init; }
    /// <summary>Exact policy identity prepared by the background universe manager.</summary>
    public string ConfigurationDigest { get; init; } = string.Empty;
    /// <summary>Required candidate contracts. All returned ranking observations must still satisfy quality checks.</summary>
    public ImmutableArray<string> RequiredContractIds { get; init; } = [];
    /// <summary>Reserved schema boundary. Nonempty overrides are rejected until the override design is implemented.</summary>
    public string? FundPortfolioParameterSchema { get; init; }
}
