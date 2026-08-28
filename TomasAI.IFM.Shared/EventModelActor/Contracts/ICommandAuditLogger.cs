using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Atomically writes the durable command audit record and reserves its command identifier.
/// </summary>
public interface ICommandAuditLogger
{
    /// <summary>
    /// Audits the command and returns whether this caller owns the durable command reservation.
    /// A duplicate command is already audited and must not be processed again.
    /// </summary>
    ValueTask<CommandAuditReservation> TryReserveAsync(
        ICommand command,
        CancellationToken cancellationToken = default);
}
