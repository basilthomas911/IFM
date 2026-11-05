using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Commands;

/// <summary>Applies an acquired and validated import through its owning command actor.</summary>
[MessagePackObject]
public sealed record ApplyYieldCurveRatesImportCommand : ICommand<YieldCurveRateEntityId>
{
    public const string Verb = "ApplyImport";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public YieldCurveRateEntityId EntityId { get; init; } = default!;
    [Key(3)] public YieldCurveRatesImportedCompleteEvent Accepted { get; init; } = default!;
    [IgnoreMember] public bool PostEvents => false;
    [IgnoreMember] public int ErrorCode => ImportYieldCurveRatesCommand.ErrorId;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.YieldCurveRateBoundedContext;
    [IgnoreMember] public string CommandName => nameof(ApplyYieldCurveRatesImportCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => "YieldCurveRateCommandActor";
}
