using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Validation;
namespace TomasAI.IFM.Domain.Trade.Order.Command.Validation;

/// <summary>Accumulates payload and identity errors before state loading.</summary>
public static class TradeOrderCommandValidation
{
    /// <summary>Checks the command payload and its routing identity without throwing.</summary>
    public static List<ValidationError> ValidateTradeOrderCommand(this List<ValidationError> errors, ICommand<TradeOrderId> command)
    {
        var order = command switch
        {
            CreateTradeOrderCommand create => create.Order,
            AmendTradeOrderCommand amend => amend.Order,
            _ => null
        };
        if (command is CreateTradeOrderCommand or AmendTradeOrderCommand)
        {
            errors.ValidateTradeOrderDefinition(order);
            if (order is not null && order.Id != command.EntityId)
                errors.Add(new("Order.Id must match EntityId."));
        }
        switch (command)
        {
            case BindTradeOrderExecutionCommand bind:
                if (bind.ExecutionAttemptId == Guid.Empty) errors.Add(new("ExecutionAttemptId is required."));
                if (!Enum.IsDefined(bind.ExecutionChannel)) errors.Add(new("ExecutionChannel is invalid."));
                if (bind.EffectiveAtUtc.Kind != DateTimeKind.Utc) errors.Add(new("EffectiveAtUtc must be UTC."));
                break;
            case ReleaseTradeOrderExecutionCommand release:
                if (release.ExecutionAttemptId == Guid.Empty) errors.Add(new("ExecutionAttemptId is required."));
                if (release.EffectiveAtUtc.Kind != DateTimeKind.Utc) errors.Add(new("EffectiveAtUtc must be UTC."));
                break;
            case ExpireTradeOrderCommand expire:
                if (expire.EffectiveAtUtc.Kind != DateTimeKind.Utc) errors.Add(new("EffectiveAtUtc must be UTC."));
                break;
        }

        if (command.Subject.EntityId != command.EntityId.Format()) errors.Add(new("Subject.EntityId must match EntityId."));
        return errors;
    }
}
