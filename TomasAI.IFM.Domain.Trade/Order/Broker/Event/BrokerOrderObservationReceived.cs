using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Event.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Event;

/// <summary>Handles one normalized broker observation.</summary>
public static class BrokerOrderObservationReceived
{
    /// <summary>Sends the observation to the authoritative BrokerOrder command stream.</summary>
    public static async ValueTask ExecuteAsync(this BrokerOrderObservationReceivedEvent received,
        IBrokerOrderEventContext context,
        ILogger<BrokerOrderEventActor> logger)
    {
        try
        {
            var command = new RecordBrokerOrderObservationCommand
            {
                CommandId = received.CommandId,
                Subject = new(ActorType.Command, BrokerOrderCommandActor.ActorName,
                    RecordBrokerOrderObservationCommand.Verb, received.EntityId.Format()),
                EntityId = received.EntityId,
                Observation = received.Observation
            };
            var result = await context.ActorService.SendAsync<RecordBrokerOrderObservationCommand, BrokerOrderId>(
                command, received.EntityId).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(
                    $"BO.OBSERVATION.HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Broker order observation handoff failed for {EventId}, {CommandId}, {EntityId}.",
                received.Id, received.CommandId, received.EntityId);
            throw;
        }
    }
}
