using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record GetBrokerAccountQuery : IQuery<BrokerAccountDefinition>
{
    public const string Actor = BrokerAccountActorNames.Query;
    public const string Verb = "GetBrokerAccount";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public BrokerAccountId BrokerAccountId { get; init; }
    [IgnoreMember] public int ErrorCode => 25212;
    [IgnoreMember] public string? QueryParams => null;
}
