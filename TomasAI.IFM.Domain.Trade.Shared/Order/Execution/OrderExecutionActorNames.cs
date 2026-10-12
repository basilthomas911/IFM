using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

public static class OrderExecutionActorNames
{
    public const string Command = "OrderExecutionCommand";
    public const string Query = "OrderExecutionQuery";
}
