using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Immediate provider-neutral access to prepared strategy option snapshots. No operation acquires data.</summary>
public interface IOptionChainCache
{
    /// <summary>Reads a single current-generation snapshot or returns an explicit unavailable result immediately.</summary>
    OptionChainSnapshotResult TryGetSnapshot(OptionChainSnapshotRequest request);
    /// <summary>Reads resident readiness without refreshing or acquiring a scope.</summary>
    OptionChainCacheStatus GetStatus(OptionChainCacheScope scope);
}

/// <summary>Explicit outcomes; unavailability is not permission to use stale quotes.</summary>
public enum OptionChainSnapshotOutcome { Ready, Deferred, NoTrade, Rejected }

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

/// <summary>One immutable prepared version and provenance; reason codes describe unavailable outcomes.</summary>
public sealed record OptionChainSnapshotResult(OptionChainSnapshotOutcome Outcome, string ReasonCode,
    MarketCompositionSnapshot? Snapshot = null, long ChainVersion = 0)
{
    /// <summary>Whether the returned snapshot is eligible for composition at the instant of reading.</summary>
    public bool IsReady => Outcome == OptionChainSnapshotOutcome.Ready;
}

/// <summary>Identity of an already prepared scope.</summary>
public sealed record OptionChainCacheScope(string ScopeId, Guid GenerationId);

/// <summary>Resident publication status; Ready still requires request-specific quality validation.</summary>
public sealed record OptionChainCacheStatus(bool Published, long ChainVersion, DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? ValidUntilUtc, int ContractCount, string ReasonCode);

/// <summary>Explicit cache unavailability for providers that do not support prepared strategy chains.</summary>
public sealed class UnavailableOptionChainCache : IOptionChainCache
{
    /// <summary>Shared stateless unavailable implementation.</summary>
    public static UnavailableOptionChainCache Instance { get; } = new();
    private UnavailableOptionChainCache() { }
    /// <inheritdoc />
    public OptionChainCacheStatus GetStatus(OptionChainCacheScope scope) => new(false, 0, null, null, 0, "CacheNotConfigured");
    /// <inheritdoc />
    public OptionChainSnapshotResult TryGetSnapshot(OptionChainSnapshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(OptionChainSnapshotOutcome.Deferred, "CacheNotConfigured");
    }
}
