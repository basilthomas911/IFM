using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Validation;
namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.Validation;

/// <summary>Accumulates payload and identity errors before state loading.</summary>
public static class BrokerOrderCommandValidation
{
    /// <summary>Checks the command payload and its routing identity without throwing.</summary>
    public static List<ValidationError> ValidateBrokerOrderCommand(this List<ValidationError> errors, ICommand<BrokerOrderId> command)
    {

        if (command is CreateBrokerOrderCommand create && (create.OperationId == Guid.Empty ||
            create.EffectiveAtUtc.Kind != DateTimeKind.Utc || create.Order is null || create.Order.Id != create.EntityId.Execution.TradeOrder))
            errors.Add(new("BrokerOrder creation operation, UTC time and TradeOrder identity are required."));
        if (command is RecordBrokerDispatchCommand dispatch && (dispatch.OperationId == Guid.Empty ||
            dispatch.RecordedAtUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(dispatch.Category)))
            errors.Add(new("Broker dispatch operation, UTC time and category are required."));
        if (command is RequestBrokerOrderLimitUpdateCommand update && (update.OperationId == Guid.Empty ||
            update.EffectiveAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("Broker limit update operation and UTC time are required."));
        if (command is RequestBrokerOrderCancelCommand cancel && (cancel.OperationId == Guid.Empty ||
            cancel.EffectiveAtUtc.Kind != DateTimeKind.Utc))
            errors.Add(new("Broker cancellation operation and UTC time are required."));
        if (command is RecordBrokerOrderObservationCommand observation)
        {
            errors.ValidateBrokerObservation(observation.Observation);
            if (observation.Observation is not null && observation.Observation.ComponentId != observation.EntityId.ComponentId)
                errors.Add(new("BrokerOrder.Observation.ComponentId must match EntityId.ComponentId."));
        }
        if (command is RecordBrokerDispatchCommand recorded && !Enum.IsDefined(recorded.Outcome))
            errors.Add(new("BrokerOrder.Dispatch.Outcome is invalid."));

        if (command is CreateBrokerOrderCommand createOrder) errors.ValidateTradeOrderDefinition(createOrder.Order);

        if (command.Subject.EntityId != command.EntityId.Format()) errors.Add(new("Subject.EntityId must match EntityId."));
        return errors;
    }
}
