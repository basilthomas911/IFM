using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record RequestBrokerAccountResynchronizationCommand : BrokerAccountCommand
{
    public const string Verb = "RequestBrokerAccountResynchronization";
    [Key(4)] public DateTime RequestedAtUtc { get; init; }
}
