using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

/// <summary>Initializes legacy monitoring limits through the authoritative position command owner.</summary>
[MessagePackObject]
public sealed record InitializeIronCondorMonitoringCommand : ICommand<StrategyPositionId>
{
    public const string Verb = "InitializeIronCondorMonitoring";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public StrategyPositionId EntityId { get; init; }
    [Key(4)] public IronCondorMonitoringInitialization MonitoringInitialization { get; init; } = new();
    [IgnoreMember] public string CommandName => nameof(InitializeIronCondorMonitoringCommand);
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.FuturesIronCondorTradePositionBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Subject.Name}Actor";
    [IgnoreMember] public int ErrorCode => 25107;
}
