using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Reviewed, fully resolved inputs sent to the dataset owner before native allocation.</summary>
[MessagePackObject]
public sealed record WorkerOptionChainRequest(
    [property: Key(0)] string ScopeId,
    [property: Key(1)] Guid LeaseId,
    [property: Key(2)] Guid GenerationId,
    [property: Key(3)] DateOnly ValueDate,
    [property: Key(4)] DateOnly MaturityDate,
    [property: Key(5)] DateTimeOffset LeaseExpiresAtUtc,
    [property: Key(6)] ImmutableArray<WorkerOptionDefinition> Options,
    [property: Key(7)] string? ExpectedContextDigest = null);

[MessagePackObject]
public sealed record WorkerOptionDefinition(
    [property: Key(0)] Framework.MarketData.Contracts.Pricing.OptionPricingContext Pricing,
    [property: Key(1)] decimal Strike,
    [property: Key(2)] bool IsCall);

[MessagePackObject]
public sealed record WorkerOptionChainRelease(
    [property: Key(0)] string ScopeId,
    [property: Key(1)] Guid LeaseId,
    [property: Key(2)] Guid GenerationId,
    [property: Key(3)] WorkerOptionChainOwnership? Ownership = null);

/// <summary>Trusted parent control message derived from committed ownership; business owners have no lease TTL.</summary>
[MessagePackObject]
public sealed record WorkerOptionChainOwnership(
    [property: Key(0)] long Revision,
    [property: Key(1)] string AuthorityScope,
    [property: Key(2)] ImmutableArray<WorkerOptionChainOwner> Owners,
    [property: Key(3)] string ContractSetDigest);

[MessagePackObject]
public sealed record WorkerOptionChainOwner(
    [property: Key(0)] Guid LeaseId,
    [property: Key(1)] ImmutableArray<string> ContractIds);

[MessagePackObject]
public sealed record WorkerOptionChainResult(
    [property: Key(0)] bool Active,
    [property: Key(1)] Framework.MarketData.Contracts.Pricing.OptionPricingFailure? Failure,
    [property: Key(2)] string? ContextDigest = null);
