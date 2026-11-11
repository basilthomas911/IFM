using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventSourcing.Persistence.Command;

/// <summary>Decides whether a matching uncommitted audit reservation may recover inside the command transaction.</summary>
public static class EnlistedCommandAudit
{
    static readonly CommandAuditMessagePackCodec Codec = new();

    extension(EnlistedEventTransaction transaction)
    {
        public async Task ReserveCommandAsync(ICommand command, CancellationToken cancellationToken = default)
        {
            var envelope = CommandAuditPreparation.Create(command, Codec, transaction.BusinessTime);
            var result = await transaction.ReserveCommandAuditAsync(envelope, cancellationToken).ConfigureAwait(false);
            if (result == CommandAuditReservationOutcome.PayloadConflict)
                throw new CommandAuditPayloadConflictException(command.CommandId);
            if (result == CommandAuditReservationOutcome.Accepted) return;

            // Only known recoverable commands may use a verified exact reservation without committed source events.
            if (command is IRecoverableCommandAudit &&
                !await transaction.HasCommittedAuditEventsUnderLockAsync(command.CommandId, cancellationToken).ConfigureAwait(false))
                return;

            var committed = await transaction.HasCommittedCommandEventsAsync(command.CommandId, command.StreamId, cancellationToken)
                .ConfigureAwait(false);
            throw new CommandAuditDuplicateException(command.CommandId, committed);
        }
    }
}
