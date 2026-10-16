using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class TradeCompletionStateTests
{
    [Theory]
    [InlineData(TradeOrderPositionType.Opening, TradeState.Open)]
    [InlineData(TradeOrderPositionType.Closing, TradeState.Closed)]
    public void Complete_fill_waits_for_the_canonical_action_state(TradeOrderPositionType action, TradeState expected)
    {
        var execution = new OrderExecutionDefinition
        {
            Status = OrderExecutionStatus.Filled, OrderQuantity = 2, CumulativeFilledQuantity = 2,
            Order = new() { PositionType = action }
        };
        Assert.Equal(expected, TradeComposerSession.CompletedState(execution));
        // Repeated polls must retain the target while the Fund projection catches up.
        Assert.Equal(expected, TradeComposerSession.CompletedState(execution));
    }

    [Theory]
    [InlineData(OrderExecutionStatus.Submitted, 0, 2)]
    [InlineData(OrderExecutionStatus.PartiallyFilled, 1, 2)]
    [InlineData(OrderExecutionStatus.Cancelled, 1, 2)]
    [InlineData(OrderExecutionStatus.Filled, 1, 2)]
    [InlineData(OrderExecutionStatus.Filled, 0, 0)]
    public void Incomplete_execution_does_not_claim_a_final_state(OrderExecutionStatus status, int filled, int quantity)
    {
        Assert.Null(TradeComposerSession.CompletedState(new()
        {
            Status = status, OrderQuantity = quantity, CumulativeFilledQuantity = filled
        }));
    }
}
