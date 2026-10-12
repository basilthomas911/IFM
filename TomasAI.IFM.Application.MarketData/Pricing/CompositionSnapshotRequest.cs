using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record CompositionSnapshotRequest([property: Key(0)] Guid SnapshotId, [property: Key(1)] string ScopeId, [property: Key(2)] string Horizon,
    [property: Key(3)] Guid GenerationId, [property: Key(4)] DateTimeOffset EvaluatedAtUtc, [property: Key(5)] DateTimeOffset DeadlineUtc,
    [property: Key(6)] bool IncludeOptions, [property: Key(7)] int MaximumContracts = 2048, [property: Key(8)] int MaximumQuoteAgeMilliseconds = 1000,
    [property: Key(9)] int MaximumQuoteSkewMilliseconds = 250,
    ImmutableArray<CompositionFutureDefinition> Futures = default)
{
    [Key(10)] public ImmutableArray<CompositionFutureDefinition> Futures { get; init; } = Futures.IsDefault ? [] : Futures;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    [Key(11)] public bool SelectionOnly { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    [Key(12)] public bool AllowMissingOptionQuotes { get; init; }
    /// <summary>Browsing captures calculate full risk values only for these selected contracts.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serializer contract requires its existing array shape.")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Key(13)] public string[] RiskContractIds { get; init; } = [];
}
