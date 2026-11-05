using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.BrokerAccount.Event;

/// <summary>Handles one complete or explicitly incomplete broker account snapshot.</summary>
public static class BrokerAccountSnapshotObserved
{

    public static string ServiceId => nameof(LogSourceType.BrokerAccountSnapshotObserved);

    extension(BrokerAccountSnapshotObservedEvent observed)
    {
        /// <summary>Sends the immutable snapshot to the authoritative BrokerAccount command stream.</summary>
        public async ValueTask ExecuteAsync(IBrokerAccountEventContext context,
            ILogger<BrokerAccountEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(observed);
            try
            {
                var command = new RecordBrokerAccountSnapshotCommand
                {
                    // Derive the command ID from the immutable snapshot so legacy generation-only events
                    // drain safely after restart and exact event redelivery remains idempotent.
                    CommandId = BrokerAccountSnapshotIdentity.Create(observed.Environment, observed.Snapshot),
                    Subject = new(ActorType.Command, BrokerAccountCommandActor.ActorName,
                        RecordBrokerAccountSnapshotCommand.Verb, observed.EntityId.Format()),
                    EntityId = observed.EntityId,
                    Environment = observed.Environment,
                    Snapshot = observed.Snapshot
                };
                var result = await context.ActorService.SendAsync<RecordBrokerAccountSnapshotCommand, BrokerAccountId>(
                    command, observed.EntityId).ConfigureAwait(false);
                if (!result.Success)
                    throw new InvalidOperationException(
                        $"BA.SNAPSHOT.HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
            }
            catch (Exception exception)
            {

                logger.LogErrorEvent(ServiceId, exception,
                    "Broker account observation handoff failed. EventId={EventId} CommandId={CommandId} AccountId={AccountId}",
                    observed.Id, observed.CommandId, observed.EntityId);

                throw;
            }
        }
    }
}
