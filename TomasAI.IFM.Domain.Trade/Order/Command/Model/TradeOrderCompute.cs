using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Model;

namespace TomasAI.IFM.Domain.Trade.Order.Command.Model;

/// <summary>An immutable proposed order change; authoritative data remains in command state.</summary>
internal sealed record TradeOrderCompute(bool Accepted, TradeOrderDefinition? TradeOrderDefinition, string RejectionCode, string RejectionReason)
{
    /// <summary>Checks computed domain data before event creation and state application.</summary>
    /// <param name="tradeOrderId">The command's owning order identity.</param>
    /// <returns>True only for an accepted, valid definition for this order.</returns>
    internal bool IsValidFor(TradeOrderId tradeOrderId) => Accepted && TradeOrderDefinition is { } tradeOrderDefinition
        && tradeOrderDefinition.Id == tradeOrderId && tradeOrderDefinition.Validate().Length == 0;

    /// <summary>Returns an accepted proposed definition without modifying state.</summary>
    internal static TradeOrderCompute Accept(TradeOrderDefinition tradeOrderDefinition) => new(true, tradeOrderDefinition, "", "");
    /// <summary>Returns a rejected proposal with its business reason.</summary>
    internal static TradeOrderCompute Reject(string rejectionCode, string rejectionReason) => new(false, null, rejectionCode, rejectionReason);
}

/// <summary>Pure Trade Order computations; no actor state, event application, or persistence.</summary>
internal static class TradeOrderComputation
{
    /// <summary>Computes a new draft definition after checking existence and payload validity.</summary>
    internal static TradeOrderCompute Create(TradeOrderDefinition? tradeOrderDefinition, TradeOrderDefinition proposedTradeOrderDefinition)
        => tradeOrderDefinition switch
        {
            not null => TradeOrderCompute.Reject("TradeOrder.ALREADY_EXISTS", "Trade Order already exists."),
            _ => ValidateDefinition(proposedTradeOrderDefinition)
        };

    /// <summary>Computes a draft amendment preserving identity and advancing revision once.</summary>
    internal static TradeOrderCompute Amend(TradeOrderDefinition? tradeOrderDefinition, TradeOrderDefinition amendedTradeOrderDefinition)
        => tradeOrderDefinition switch
        {
            null => Missing(),
            _ when tradeOrderDefinition.Status != TradeOrderStatus.Draft => InvalidTransition(tradeOrderDefinition, "amend"),
            _ when amendedTradeOrderDefinition.Id != tradeOrderDefinition.Id || amendedTradeOrderDefinition.Revision != tradeOrderDefinition.Revision + 1
                => TradeOrderCompute.Reject("TradeOrder.REVISION", "Replacement must retain identity and increment revision exactly once."),
            _ => ValidateDefinition(amendedTradeOrderDefinition)
        };

    /// <summary>Computes a lifecycle transition from its required source status.</summary>
    internal static TradeOrderCompute Move(TradeOrderDefinition? tradeOrderDefinition, TradeOrderStatus requiredOrderStatus, TradeOrderStatus nextOrderStatus, string orderOperation)
        => tradeOrderDefinition switch
        {
            null => Missing(),
            _ when tradeOrderDefinition.Status != requiredOrderStatus => InvalidTransition(tradeOrderDefinition, orderOperation),
            _ => TradeOrderCompute.Accept(tradeOrderDefinition with { Status = nextOrderStatus })
        };

    /// <summary>Computes the execution binding after checking the attempt and UTC evidence.</summary>
    internal static TradeOrderCompute Bind(TradeOrderDefinition? tradeOrderDefinition, Guid executionAttemptId, ExecutionChannel executionChannel, DateTime effectiveAtUtc)
        => tradeOrderDefinition switch
        {
            null => Missing(),
            _ when tradeOrderDefinition.Status != TradeOrderStatus.Ready => InvalidTransition(tradeOrderDefinition, "bind execution"),
            _ when executionAttemptId == Guid.Empty || effectiveAtUtc.Kind != DateTimeKind.Utc
                => TradeOrderCompute.Reject("TradeOrder.INVALID_EXECUTION_BINDING", "Execution binding requires a non-empty attempt ID and UTC timestamp."),
            _ => TradeOrderCompute.Accept(tradeOrderDefinition with { Status = TradeOrderStatus.Executing,
                BoundExecutionAttemptId = executionAttemptId, BoundExecutionChannel = executionChannel, ExecutionBoundAtUtc = effectiveAtUtc })
        };

    /// <summary>Computes release only for the bound execution with confirmed zero exposure.</summary>
    internal static TradeOrderCompute Release(TradeOrderDefinition? tradeOrderDefinition, Guid executionAttemptId, bool zeroExposureConfirmed, DateTime effectiveAtUtc)
        => tradeOrderDefinition switch
        {
            null => Missing(),
            _ when tradeOrderDefinition.Status != TradeOrderStatus.Executing => InvalidTransition(tradeOrderDefinition, "release execution"),
            _ when !zeroExposureConfirmed || executionAttemptId == Guid.Empty || executionAttemptId != tradeOrderDefinition.BoundExecutionAttemptId || effectiveAtUtc.Kind != DateTimeKind.Utc
                => TradeOrderCompute.Reject("TradeOrder.EXECUTION_RELEASE_NOT_PROVEN", "Release requires the bound execution attempt, UTC evidence time, and proven zero exposure."),
            _ => TradeOrderCompute.Accept(tradeOrderDefinition with { Status = TradeOrderStatus.Ready,
                BoundExecutionAttemptId = null, BoundExecutionChannel = null, ExecutionBoundAtUtc = null })
        };

    /// <summary>Computes cancellation for an order that is not already terminal.</summary>
    internal static TradeOrderCompute Cancel(TradeOrderDefinition? tradeOrderDefinition)
        => tradeOrderDefinition switch
        {
            null => Missing(),
            _ when tradeOrderDefinition.Status is TradeOrderStatus.Completed or TradeOrderStatus.Cancelled or TradeOrderStatus.Expired => InvalidTransition(tradeOrderDefinition, "cancel"),
            _ => TradeOrderCompute.Accept(tradeOrderDefinition with { Status = TradeOrderStatus.Cancelled })
        };

    /// <summary>Computes expiry after validity ends, excluding executing or completed orders.</summary>
    internal static TradeOrderCompute Expire(TradeOrderDefinition? tradeOrderDefinition, DateTime effectiveAtUtc)
        => tradeOrderDefinition switch
        {
            null => Missing(),
            _ when effectiveAtUtc.Kind != DateTimeKind.Utc || effectiveAtUtc < tradeOrderDefinition.ValidUntilUtc
                => TradeOrderCompute.Reject("TradeOrder.NOT_EXPIRED", "Order validity has not expired."),
            _ when tradeOrderDefinition.Status is TradeOrderStatus.Executing or TradeOrderStatus.Completed => InvalidTransition(tradeOrderDefinition, "expire"),
            _ => TradeOrderCompute.Accept(tradeOrderDefinition with { Status = TradeOrderStatus.Expired })
        };

    /// <summary>Validates proposed domain data and computes its draft representation.</summary>
    private static TradeOrderCompute ValidateDefinition(TradeOrderDefinition proposedTradeOrderDefinition)
    {
        var tradeOrderErrors = proposedTradeOrderDefinition.Validate();
        return tradeOrderErrors.Length > 0 ? TradeOrderCompute.Reject("TradeOrder.INVALID", string.Join(" | ", tradeOrderErrors))
            : TradeOrderCompute.Accept(proposedTradeOrderDefinition with { Status = TradeOrderStatus.Draft });
    }
    /// <summary>Returns the domain rejection for a missing order.</summary>
    private static TradeOrderCompute Missing() => TradeOrderCompute.Reject("TradeOrder.NOT_FOUND", "Trade Order does not exist.");
    /// <summary>Returns the domain rejection for an invalid lifecycle transition.</summary>
    private static TradeOrderCompute InvalidTransition(TradeOrderDefinition tradeOrderDefinition, string orderOperation)
        => TradeOrderCompute.Reject("TradeOrder.INVALID_TRANSITION", $"Cannot {orderOperation} an order in {tradeOrderDefinition.Status} state.");
}
