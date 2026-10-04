using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Validation;
namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Validation;

/// <summary>Accumulates payload and identity errors before state loading.</summary>
public static class OrderExecutionCommandValidation
{
    /// <summary>Checks the command payload and its routing identity without throwing.</summary>
    public static List<ValidationError> ValidateOrderExecutionCommand(this List<ValidationError> errors, ICommand<OrderExecutionId> command)
    {
        if (command is StartOrderExecutionCommand start &&
                (start.Order is null || start.EntityId.TradeOrder != start.Order.Id ||
                 start.EntityId.ExecutionAttemptId != start.ExecutionAttemptId ||
                 start.EffectiveAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("Matching order, attempt and UTC effective time are required."));
        if (command is AddOrderExecutionFillCommand fill && (fill.Fill is null || fill.Fill.ExecutionFillId == Guid.Empty))
            errors.Add(new("Execution fill identity is required."));
        if (command is UpdateOrderExecutionFillCostCommand cost &&
                (string.IsNullOrWhiteSpace(cost.ExternalExecutionId) || cost.Commission < 0))
            errors.Add(new("Execution identity and non-negative commission are required."));
        if (command is TimedOrderExecutionCommand timed && timed.EffectiveAtUtc.Kind != DateTimeKind.Utc)
            errors.Add(new("UTC completion time is required."));

        if (command is StartOrderExecutionCommand startOrder) errors.ValidateTradeOrderDefinition(startOrder.Order);
        if (command is AddOrderExecutionFillCommand fillCommand) errors.ValidateExecutionFill(fillCommand.Fill);

        if (command.Subject.EntityId != command.EntityId.Format()) errors.Add(new("Subject.EntityId must match EntityId."));
        return errors;
    }
}
