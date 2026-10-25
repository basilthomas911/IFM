using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

[MessagePackObject]
[Union(0, typeof(OpenIronCondorPositionCommand))]
[Union(1, typeof(UpdateIronCondorPositionLegMarketPriceCommand))]
[Union(2, typeof(EndOfDayIronCondorPositionCommand))]
[Union(3, typeof(CloseIronCondorPositionCommand))]
[Union(4, typeof(CorrectIronCondorPositionBasisCommand))]
[Union(5, typeof(SnapshotIronCondorPositionCommand))]
[Union(6, typeof(OpenVerticalSpreadPositionCommand))]
[Union(7, typeof(UpdateVerticalSpreadPositionLegMarketPriceCommand))]
[Union(8, typeof(EndOfDayVerticalSpreadPositionCommand))]
[Union(9, typeof(CloseVerticalSpreadPositionCommand))]
[Union(10, typeof(CorrectVerticalSpreadPositionBasisCommand))]
[Union(11, typeof(SnapshotVerticalSpreadPositionCommand))]
public abstract record PositionCommand : ICommand<StrategyPositionId>
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public StrategyPositionId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public abstract BoundedContextName RouteTo { get; }
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Subject.Name}Actor";
    [IgnoreMember] public int ErrorCode => 25104;
}
