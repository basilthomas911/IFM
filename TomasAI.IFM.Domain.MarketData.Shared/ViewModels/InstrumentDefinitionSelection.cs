using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Provider facts only. Unknown exercise/settlement conventions are not manufactured here.</summary>
[MessagePackObject]
public sealed record InstrumentDefinitionSelection
{
    [Key(0)] public Guid SnapshotId { get; init; }
    [Key(1)] public string Dataset { get; init; } = "";
    [Key(2)] public ushort PublisherId { get; init; }
    [Key(3)] public uint InstrumentId { get; init; }
    [Key(4)] public string RawSymbol { get; init; } = "";
    [Key(5)] public string Root { get; init; } = "";
    [Key(6)] public string InstrumentClass { get; init; } = "";
    [Key(7)] public string Currency { get; init; } = "";
    [Key(8)] public string Exchange { get; init; } = "";
    [Key(9)] public uint UnderlyingInstrumentId { get; init; }
    [Key(10)] public decimal? Strike { get; init; }
    [Key(11)] public decimal? Multiplier { get; init; }
    [Key(12)] public decimal? TickSize { get; init; }
    [Key(13)] public DateTimeOffset? ExpirationUtc { get; init; }
    [Key(14)] public DateTimeOffset? ActivationUtc { get; init; }
    [Key(15)] public DateTimeOffset DefinitionTimestampUtc { get; init; }
    [Key(16)] public string DefinitionDigest { get; init; } = "";
    [Key(17)] public string RawDefinitionReference { get; init; } = "";
    [Key(18)] public bool Deleted { get; init; }
}

[MessagePackObject]
public sealed record InstrumentDefinitionPage(
    [property: Key(0)] Guid SnapshotId,
    [property: Key(1)] DateTime SnapshotCompletedUtc,
    [property: Key(2)] InstrumentDefinitionSelection[] Items,
    [property: Key(3)] string? ContinuationToken);

[MessagePackObject]
public sealed record InstrumentDefinitionPageRequest : IQueryParameter, IActorEntityId
{
    [Key(0)] public string Dataset { get; init; } = "GLBX.MDP3";
    [Key(1)] public string Root { get; init; } = "";
    [Key(2)] public bool Options { get; init; }
    [Key(3)] public Guid SnapshotId { get; init; }
    [Key(4)] public string? ContinuationToken { get; init; }
    [Key(5)] public int PageSize { get; init; } = 100;
    [Key(6)] public string? Exchange { get; init; }
    [Key(7)] public DateOnly? Expiry { get; init; }
    [Key(8)] public uint? UnderlyingInstrumentId { get; init; }
    [Key(9)] public ReferenceOptionRight Right { get; init; }
    [Key(10)] public decimal? MinimumStrike { get; init; }
    [Key(11)] public decimal? MaximumStrike { get; init; }
    [Key(12)] public bool IncludeExpiredOrDeleted { get; init; }
    [IgnoreMember]
    [System.Text.Json.Serialization.JsonIgnore]
    public string QueryParams => "request=" + Uri.EscapeDataString(
        System.Text.Json.JsonSerializer.Serialize(this));
    public string Format() => "definitions";
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Dataset) || Dataset.Length > 32 || string.IsNullOrWhiteSpace(Root) || Root.Length > 32
            || PageSize is < 1 or > 200 || Exchange?.Length > 32 || ContinuationToken?.Length > 2048
            || !Enum.IsDefined(Right) || MinimumStrike is <= 0 || MaximumStrike is <= 0
            || MinimumStrike > MaximumStrike)
            throw new ArgumentException("Invalid bounded definition search.");
    }
}
