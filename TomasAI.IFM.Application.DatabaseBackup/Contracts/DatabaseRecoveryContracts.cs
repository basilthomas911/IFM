using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Application.DatabaseBackup.Contracts;

public sealed record DatabaseExecutionIntent
{
    public required DatabaseBackupEventContract ExecutionEvent { get; init; }

    public DatabaseRecoveryOperationId OperationId => ExecutionEvent.Source.OperationId;
    public BackupSource Source => ExecutionEvent.Source.Source;

    public void Validate()
    {
        ExecutionEvent.Validate();
        DatabaseBackupEnumValidation.RequireConcrete(Source);
        if (ExecutionEvent.GetType().Namespace?.EndsWith(".Events.Execution", StringComparison.Ordinal) != true)
            throw new ArgumentException("Only DatabaseBackup execution events can be admitted.", nameof(ExecutionEvent));
    }
}

public enum DatabaseExecutionAdmissionOutcome
{
    Admitted = 1,
    ExactDuplicate = 2
}

public sealed record DatabaseExecutionAdmission(
    DatabaseRecoveryOperationId OperationId,
    DatabaseExecutionAdmissionOutcome Outcome);

public interface IDatabaseRecoveryProcessor
{
    BackupSource Source { get; }

    ValueTask<DatabaseExecutionAdmission> AdmitAsync(
        DatabaseExecutionIntent intent,
        CancellationToken cancellationToken);
}

public interface IDatabaseRecoveryProcessorRouting
{
    bool CanProcess(DatabaseProtectionSetId protectionSetId);
}

public interface IDatabaseRecoveryProcessorRegistry
{
    IDatabaseRecoveryProcessor GetRequired(BackupSource source);
}

public sealed class UnsupportedDatabaseBackupSourceException(BackupSource source)
    : InvalidOperationException($"Database backup source '{source}' is not registered.")
{
    public BackupSource RequestedSource { get; } = source;
}

public sealed class DatabaseExecutionConflictException(string message) : InvalidOperationException(message);

public sealed class DatabaseLeaseLostException(DatabaseRecoveryOperationId operationId)
    : InvalidOperationException($"The fenced journal lease for operation '{operationId.Format()}' is no longer current.")
{
    public DatabaseRecoveryOperationId OperationId { get; } = operationId;
}
