using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.BrokerAccount.Event;

/// <summary>Handles one complete or explicitly incomplete broker account snapshot.</summary>
public static class BrokerAccountSnapshotObserved
{
    /// <summary>Sends the immutable snapshot to the authoritative BrokerAccount command stream.</summary>
    public static async ValueTask ExecuteAsync(this BrokerAccountSnapshotObservedEvent observed,
        IBrokerAccountEventContext context,
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
            
            var diagnosticLogger = logger;
            if (diagnosticLogger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
            {
                global::TomasAI.IFM.Domain.BrokerAccount.StorageMigrationDiagnosticLog.Write2(diagnosticLogger, exception, observed.Id, observed.CommandId, observed.EntityId);
            }
        
            throw;
        }
    }
}
