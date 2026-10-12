using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
[Union(0, typeof(RecordBrokerAccountSnapshotCommand))]
[Union(1, typeof(SubmitAccountQualificationEvidenceCommand))]
[Union(2, typeof(AcceptAccountQualificationCommand))]
[Union(3, typeof(RevokeAccountQualificationCommand))]
[Union(4, typeof(SetManualTradingHoldCommand))]
[Union(5, typeof(ReleaseManualTradingHoldCommand))]
[Union(6, typeof(RequestBrokerAccountResynchronizationCommand))]
public abstract record BrokerAccountCommand : ICommand<BrokerAccountId>
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public BrokerAccountId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.BrokerAccountBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{BrokerAccountActorNames.Command}Actor";
    [IgnoreMember] public int ErrorCode => 25104;
}
