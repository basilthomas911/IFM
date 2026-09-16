using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Validation;

public static class OrderExecutionCommandValidation
{
    /// <summary>Accumulates structural execution-command failures before state is loaded.</summary>
    public static List<ValidationError> Validate(ICommand command) => new List<ValidationError>()
        .ValidateCommandId(command.CommandId, command.CommandName)
        .CaptureCommandValidation(() =>
        {
            if (command is not ICommand<OrderExecutionId> typed || !typed.EntityId.IsValid ||
                command.Subject.EntityId != typed.EntityId.Format())
                throw new ArgumentException("Valid execution identity and matching subject are required.");
            if (command is StartOrderExecutionCommand start &&
                (start.EntityId.TradeOrder != start.Order.Id ||
                 start.EntityId.ExecutionAttemptId != start.ExecutionAttemptId ||
                 start.EffectiveAtUtc.Kind != DateTimeKind.Utc))
                throw new ArgumentException("Matching order, attempt and UTC effective time are required.");
            if (command is AddOrderExecutionFillCommand fill && fill.Fill.ExecutionFillId == Guid.Empty)
                throw new ArgumentException("Execution fill identity is required.");
            if (command is UpdateOrderExecutionFillCostCommand cost &&
                (string.IsNullOrWhiteSpace(cost.ExternalExecutionId) || cost.Commission < 0))
                throw new ArgumentException("Execution identity and non-negative commission are required.");
            if (command is AcceptOrderExecutionCommand accept && accept.EffectiveAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("UTC completion time is required.");
        });
}
