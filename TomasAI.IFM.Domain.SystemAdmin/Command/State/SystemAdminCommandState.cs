using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Commands;
using TomasAI.IFM.Shared.SystemAdmin.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Command.State;

/// <summary>
/// Represents the event-sourced state of System Admin commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for System Admin operations by applying domain events
/// such as <see cref="DatabaseBackupEvent"/>. It mirrors the logic of
/// <see cref="SystemAdmin.SystemAdminBoundedContextState"/>.</remarks>
public class SystemAdminCommandState
    : BaseEventSourceActorState<SystemAdminCommandState>, IEventSourceActorState<SystemAdminCommandState>
{
    string _databaseName = string.Empty;
    DatabaseBackupType _backupType;
    int _commandTimeout;

    /// <summary>
    /// Gets or sets the unique identifier for the actor thread associated with this state.
    /// </summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Applies the specified domain event to update the state of the current object.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply. Must be of a supported type.</param>
    /// <returns><see langword="true"/> if the domain event was successfully applied; otherwise, <see langword="false"/>.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                DatabaseBackupEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;

        bool On(DatabaseBackupEvent e)
        {
            _databaseName = e.DatabaseName;
            _backupType = e.BackupType;
            _commandTimeout = e.CommandTimeout;
            return true;
        }
    }

    /// <summary>
    /// Applies a database backup command by producing and applying a <see cref="DatabaseBackupEvent"/>.
    /// </summary>
    /// <param name="e">The backup database command.</param>
    /// <returns><see langword="true"/> if the event was successfully applied; otherwise, <see langword="false"/>.</returns>
    internal bool ApplyDatabaseBackupEvent(BackupDatabaseCommand e)
        => Apply(new DatabaseBackupEvent
        {
            CommandId = e.CommandId,
            DatabaseName = e.DatabaseName,
            BackupType = e.BackupType,
            CommandTimeout = e.CommandTimeout,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        }.RoutedFrom(e), true);

    /// <summary>
    /// Gets the name of the database targeted by the last backup command.
    /// </summary>
    public string DatabaseName => _databaseName;

    /// <summary>
    /// Gets the backup type of the last backup command.
    /// </summary>
    public DatabaseBackupType BackupType => _backupType;

    /// <summary>
    /// Gets the command timeout of the last backup command.
    /// </summary>
    public int CommandTimeout => _commandTimeout;
}
