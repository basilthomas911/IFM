using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Broker;

public static class BrokerOrderActorNames
{
    public const string Command = "BrokerOrderCommand";
    public const string Event = "BrokerOrderEvent";
    public const string Query = "BrokerOrderQuery";
}
