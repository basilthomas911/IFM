using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>Requests the latest projected VX term-structure signal for a stream.</summary>
[MessagePackObject]
public sealed record GetLatestFuturesVxTermStructureSignalQuery
    : IQuery<FuturesVxTermStructureSignalReadModel?>
{
    public const string Actor = "FuturesVxTermStructureSignalQuery";
    public const string Verb = "GetLatest";
    public const int ErrorId = 26310;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = default!;
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public string ConfigurationId { get; init; } = string.Empty;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}
