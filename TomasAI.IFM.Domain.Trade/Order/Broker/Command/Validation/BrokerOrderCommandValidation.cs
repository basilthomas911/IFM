using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.Validation;

public static class BrokerOrderCommandValidation
{
    /// <summary>Accumulates structural validation errors without accessing state or external services.</summary>
    public static List<ValidationError> Validate(ICommand command) => new List<ValidationError>()
        .ValidateCommandId(command.CommandId, command.CommandName)
        .CaptureCommandValidation(() =>
        {
            if (command is not ICommand<Domain.Trade.Shared.BrokerOrderId> typed || !typed.EntityId.IsValid ||
                command.Subject.EntityId != typed.EntityId.Format())
                throw new ArgumentException("Valid BrokerOrder identity and matching subject are required.");
            switch (command)
            {
                case CreateBrokerOrderCommand create when create.OperationId == Guid.Empty ||
                    create.EffectiveAtUtc.Kind != DateTimeKind.Utc || create.Order.Id != create.EntityId.Execution.TradeOrder:
                    throw new ArgumentException("BrokerOrder creation operation, UTC time and TradeOrder identity are required.");
                case RecordBrokerDispatchCommand dispatch when dispatch.OperationId == Guid.Empty ||
                    dispatch.RecordedAtUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(dispatch.Category):
                    throw new ArgumentException("Broker dispatch operation, UTC time and category are required.");
                case RequestBrokerOrderLimitUpdateCommand update when update.OperationId == Guid.Empty ||
                    update.EffectiveAtUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("Broker limit update operation and UTC time are required.");
                case RequestBrokerOrderCancelCommand cancel when cancel.OperationId == Guid.Empty ||
                    cancel.EffectiveAtUtc.Kind != DateTimeKind.Utc:
                    throw new ArgumentException("Broker cancellation operation and UTC time are required.");
                case RecordBrokerOrderObservationCommand observation when
                    observation.Observation.ObservationId == Guid.Empty ||
                    observation.Observation.Kind == BrokerOrderObservationKind.Unknown ||
                    string.IsNullOrWhiteSpace(observation.Observation.AccountAlias) ||
                    observation.Observation.ComponentId != observation.EntityId.ComponentId ||
                    observation.Observation.SourceEpoch <= 0 || observation.Observation.SourceSequence <= 0 ||
                    observation.Observation.OccurredAtUtc.Kind != DateTimeKind.Utc ||
                    string.IsNullOrWhiteSpace(observation.Observation.ContentHash):
                    throw new ArgumentException("A correlated, sourced UTC broker observation is required.");
                case RecordBrokerOrderObservationCommand observation when
                    observation.Observation.Kind == BrokerOrderObservationKind.Execution &&
                    (observation.Observation.LegId == Guid.Empty ||
                     string.IsNullOrWhiteSpace(observation.Observation.ContractId) ||
                     string.IsNullOrWhiteSpace(observation.Observation.ExternalExecutionId) ||
                     observation.Observation.SignedQuantity == 0 || observation.Observation.Price <= 0):
                    throw new ArgumentException("Execution observations require exact leg, contract, execution, quantity and price evidence.");
                case RecordBrokerOrderObservationCommand observation when
                    observation.Observation.Kind == BrokerOrderObservationKind.Commission &&
                    (string.IsNullOrWhiteSpace(observation.Observation.ExternalExecutionId) ||
                     observation.Observation.Commission < 0):
                    throw new ArgumentException("Commission observations require an execution identity and non-negative amount.");
            }
        });
}
