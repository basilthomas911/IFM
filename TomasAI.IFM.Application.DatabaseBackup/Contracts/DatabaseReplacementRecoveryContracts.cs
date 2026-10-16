namespace TomasAI.IFM.Application.DatabaseBackup.Contracts;

/// <summary>Persisted outside the databases being replaced; never store database passwords here.</summary>
public sealed record DatabaseReplacementRecoveryRecord(
    DatabaseCutoverRequest Request,
    DatabaseReplacementStage Stage,
    string RollbackReference,
    DatabaseCutoverResult? Result = null);
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1008",Justification="Zero is intentionally invalid in this persisted protocol; existing numeric values must remain unchanged.")]

public enum DatabaseReplacementStage
{
    Prepared = 1,
    StoppingApplication = 2,
    ReplacingData = 3,
    ValidatingDatabase = 4,
    StartingApplicationInRecovery = 5,
    ValidatingApplication = 6,
    ResumingAdmission = 7,
    Completed = 8,
    RollingBack = 9,
    RolledBack = 10,
    ReconciliationRequired = 11
}

/// <summary>A lease owns the deployment's maintenance lock, not just one database operation.</summary>
public interface IDatabaseReplacementRecoveryLease : IAsyncDisposable
{
    ValueTask<DatabaseReplacementRecoveryRecord?> ReadAsync(CancellationToken cancellationToken);
    ValueTask WriteAsync(DatabaseReplacementRecoveryRecord record, CancellationToken cancellationToken);
}

public interface IDatabaseReplacementRecoveryJournal
{
    ValueTask<IDatabaseReplacementRecoveryLease> AcquireAsync(DatabaseCutoverRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Deployment operations run independently of the API. Preparation verifies the completed
/// restore and replacement profile and records rollback data before the application stops.
/// </summary>
public interface IDatabaseReplacementRecoveryPlatform
{
    ValueTask<string> PrepareAsync(DatabaseCutoverRequest request, CancellationToken cancellationToken);
    ValueTask StopApplicationAsync(DatabaseCutoverRequest request, CancellationToken cancellationToken);
    ValueTask ReplaceDataAsync(DatabaseCutoverRequest request, string rollbackReference, CancellationToken cancellationToken);
    ValueTask ValidateDatabaseAsync(DatabaseCutoverRequest request, CancellationToken cancellationToken);
    /// <summary>Starts services with trading, scheduled writes and other mutations fenced.</summary>
    ValueTask StartApplicationInRecoveryAsync(DatabaseCutoverRequest request, CancellationToken cancellationToken);
    /// <summary>Verifies connections and required schemas/data, beyond an HTTP process liveness check.</summary>
    ValueTask ValidateApplicationAsync(DatabaseCutoverRequest request, CancellationToken cancellationToken);
    ValueTask ResumeAdmissionAsync(DatabaseCutoverRequest request, CancellationToken cancellationToken);
    /// <summary>Stops all writers before returning the preserved data to the active endpoints.</summary>
    ValueTask RollbackAsync(DatabaseCutoverRequest request, string rollbackReference, CancellationToken cancellationToken);
    ValueTask ValidateRollbackAsync(DatabaseCutoverRequest request, CancellationToken cancellationToken);
}
