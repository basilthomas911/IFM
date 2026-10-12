using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Durable identity for one configured broker account alias.</summary>
[MessagePackObject]
public readonly record struct BrokerAccountId([property: Key(0)] string AccountAlias) : IActorEntityId
{
    [IgnoreMember] public bool IsValid => !string.IsNullOrWhiteSpace(AccountAlias);

    /// <summary>Returns the exact configured account alias.</summary>
    public string Format() => AccountAlias;
}
