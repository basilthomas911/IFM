using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Model;
/// <summary>The proposed execution definition, establishment evidence, or business rejection.</summary>
internal sealed record OrderExecutionCompute(bool Accepted, OrderExecutionDefinition? OrderExecutionDefinition, string RejectionCode, string RejectionReason, EstablishedTradeDefinition[] CreatedTrades, PositionCloseExecution[] ClosedPositions)
{
    /// <summary>Checks execution ownership and required business identity before event creation.</summary>
    public bool IsValidFor(OrderExecutionId executionId) => Accepted && OrderExecutionDefinition is { } execution && executionId.IsValid && execution.Id == executionId && execution.TradeOrderId.IsValid;
    /// <summary>Accepts computed business data and optional establishment evidence.</summary>
    public static OrderExecutionCompute Accept(OrderExecutionDefinition execution, EstablishedTradeDefinition[]? createdTrades = null, PositionCloseExecution[]? closedPositions = null) => new(true, execution, string.Empty, string.Empty, createdTrades ?? [], closedPositions ?? []);
    /// <summary>Preserves the business rejection classification and detail.</summary>
    public static OrderExecutionCompute Reject(string rejectionCode, string rejectionReason) => new(false, null, rejectionCode, rejectionReason, [], []);
}
