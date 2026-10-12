using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Actor names for the singleton logical broker account.</summary>
public static class BrokerAccountActorNames
{
    public const string Command = "BrokerAccountCommand";
    public const string Event = "BrokerAccountEvent";
    public const string Query = "BrokerAccountQuery";
}
