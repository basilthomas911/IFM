using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Shared.SystemAdmin.Commands;

/// <summary>
/// Command to initiate a database backup operation for a specified database name, backup type, and timeout.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.SystemAdminBoundedContext"/> with error code 9001.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record BackupDatabaseCommand
    : ICommand<DatabaseBackupId>
{
    public const string Actor = "SystemAdminCommand";
    public const string Verb = "BackupDatabase";
    public const int ErrorId = 9001;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public DatabaseBackupId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Name of the database to back up.</summary>
    [Key(6)]
    public string DatabaseName { get; init; } 

    /// <summary>Type of backup to perform (e.g., Full, Diff).</summary>
    [Key(7)]
    public DatabaseBackupType BackupType { get; init; }

    /// <summary>Timeout in seconds for the backup operation.</summary>
    [Key(8)]
    public int CommandTimeout { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public BackupDatabaseCommand() { }

    /// <summary>
    /// Creates a new command to back up a database.
    /// </summary>
    /// <param name="databaseName">Target database name.</param>
    /// <param name="backupType">Backup type (Full/Diff).</param>
    /// <param name="commandTimeout">Timeout (seconds) for the backup operation.</param>
    public BackupDatabaseCommand(string databaseName, DatabaseBackupType backupType, int commandTimeout)
    {
        DatabaseName = databaseName ?? string.Empty;
        BackupType = backupType;
        CommandTimeout = commandTimeout;

        EntityId = new DatabaseBackupId(DatabaseName);
        RouteTo = BoundedContextName.SystemAdminBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public BackupDatabaseCommand(
        Guid commandId,               // Key(0)
        ActorSubject subject,         // Key(1)
        bool postEvents,              // Key(2)
        DatabaseBackupId entityId,    // Key(3)
        int errorCode,                // Key(4)
        BoundedContextName routeTo,   // Key(5)
        string databaseName,          // Key(6)
        DatabaseBackupType backupType,// Key(7)
        int commandTimeout)           // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        DatabaseName = databaseName;
        BackupType = backupType;
        CommandTimeout = commandTimeout;
    }
}