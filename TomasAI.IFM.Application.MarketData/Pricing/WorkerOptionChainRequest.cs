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
    [property: Key(7)] string? ExpectedContextDigest = null,
    [property: Key(8)] bool SeparateContractConnection = false,
    [property: Key(9), System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool IndependentChainConnection = false);
