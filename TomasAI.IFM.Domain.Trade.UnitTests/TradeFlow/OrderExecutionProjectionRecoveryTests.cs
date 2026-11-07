using TomasAI.IFM.Domain.Trade.Order.Execution.Recovery;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class OrderExecutionProjectionRecoveryTests
{
    [Theory]
    [InlineData(OrderExecutionStatus.Pending, false)]
    [InlineData(OrderExecutionStatus.Submitted, false)]
    [InlineData(OrderExecutionStatus.PartiallyFilled, false)]
    [InlineData(OrderExecutionStatus.Rejected, false)]
    [InlineData(OrderExecutionStatus.Filled, true)]
    [InlineData(OrderExecutionStatus.Cancelled, true)]
    public void Only_the_matching_terminal_close_can_enter_settlement_replay(OrderExecutionStatus status, bool admitted)
    {
        var order = new TradeOrderId(101,701,3901);
        var attempt = Guid.NewGuid();
        var source = new OrderExecutionChangedEvent {
            EntityId = new(order, attempt), OrderExecutionDefinition = new() {
                TradeOrderId = order, ExecutionAttemptId = attempt, Status = status,
                PositionType = TradeOrderPositionType.Closing,
                TargetPositionId = new(new(101,701,1901,1501), Guid.NewGuid()),
                Fills = [new() { ExecutionFillId = Guid.NewGuid() }]
            }
        };
        Assert.Equal(admitted, OrderExecutionProjectionRecovery.CanRetry(source, order));
        Assert.False(OrderExecutionProjectionRecovery.CanRetry(source, new(101,701,2001)));
        Assert.False(OrderExecutionProjectionRecovery.CanRetry(source with {
            OrderExecutionDefinition = source.OrderExecutionDefinition with { PositionType = TradeOrderPositionType.Opening }
        }, order));
    }
}
