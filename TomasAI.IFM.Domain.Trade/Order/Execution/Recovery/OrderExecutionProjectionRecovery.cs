using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Recovery;

/// <summary>Admits only a matching committed terminal close to settlement replay; placement events cannot enter.</summary>
public static class OrderExecutionProjectionRecovery
{
    public static bool CanRetry(IEvent? source, TradeOrderId expected)
        => source is OrderExecutionChangedEvent changed
            && expected.IsValid && changed.EntityId.TradeOrder == expected
            && changed.OrderExecutionDefinition.TradeOrderId == expected
            && changed.OrderExecutionDefinition.PositionType == TradeOrderPositionType.Closing
            && changed.OrderExecutionDefinition.Status is OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled
            && changed.OrderExecutionDefinition.Fills.Length > 0
            && changed.OrderExecutionDefinition.TargetPositionId is { IsValid: true };
}
