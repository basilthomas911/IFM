using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Requests an event-sourced VX front/back leg update.</summary>
[MessagePackObject]
public sealed record UpdateFuturesVxTermStructureSignalCommand
    : ICommand<FuturesVxTermStructureSignalEntityId>
{
    public const string Actor = "FuturesVxTermStructureSignalCommand";
    public const string Verb = "Update";
    public const int ErrorId = 26300;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesVxTermStructureSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesVxTermStructureSignalBoundedContext;
    [Key(6)] public FuturesVxTermStructureLegObservation Observation { get; init; } = new();
    [Key(7)] public FuturesVxTermStructureConfiguration Configuration { get; init; } = FuturesVxTermStructureConfiguration.Standard;
    [IgnoreMember] public string CommandName => nameof(UpdateFuturesVxTermStructureSignalCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}
