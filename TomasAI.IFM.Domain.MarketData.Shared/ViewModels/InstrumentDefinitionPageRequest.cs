using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Dataset) || Dataset.Length > 32 || string.IsNullOrWhiteSpace(Root) || Root.Length > 32
            || PageSize is < 1 or > 200 || Exchange?.Length > 32 || ContinuationToken?.Length > 2048
            || !Enum.IsDefined(Right) || MinimumStrike is <= 0 || MaximumStrike is <= 0
            || MinimumStrike > MaximumStrike)
            throw new ArgumentException("Invalid bounded definition search.");
    }
}
