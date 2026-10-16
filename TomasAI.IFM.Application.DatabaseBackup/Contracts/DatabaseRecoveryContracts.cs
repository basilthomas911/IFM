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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1008",Justification="Zero is intentionally invalid in this persisted protocol; existing numeric values must remain unchanged.")]

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

public interface IDatabaseRecoveryEngineSelector
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1716",Justification="Preserve the established public interface member used by existing consumers.")]
    DatabaseEngine Select(DatabaseProtectionSetId protectionSetId);
    bool CanSelect(DatabaseProtectionSetId protectionSetId);
}

public interface IDatabaseRecoveryProcessorRegistry
{
    IDatabaseRecoveryProcessor GetRequired(BackupSource source);
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1032",Justification="This domain exception requires structured identity or reason data; retain its intentional constructors.")]

public sealed class UnsupportedDatabaseBackupSourceException(BackupSource source)
    : InvalidOperationException($"Database backup source '{source}' is not registered.")
{
    public BackupSource RequestedSource { get; } = source;
}
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1032",Justification="This domain exception requires structured identity or reason data; retain its intentional constructors.")]

public sealed class DatabaseExecutionConflictException(string message) : InvalidOperationException(message);
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1032",Justification="This domain exception requires structured identity or reason data; retain its intentional constructors.")]

public sealed class DatabaseLeaseLostException(DatabaseRecoveryOperationId operationId)
    : InvalidOperationException($"The fenced journal lease for operation '{operationId.Format()}' is no longer current.")
{
    public DatabaseRecoveryOperationId OperationId { get; } = operationId;
}
