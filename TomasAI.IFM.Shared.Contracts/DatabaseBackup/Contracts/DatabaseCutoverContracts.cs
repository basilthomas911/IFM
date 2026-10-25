using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Shared.DatabaseBackup.Contracts;

/// <summary>An approved replacement of active database data, retaining its endpoints.</summary>
public sealed record DatabaseCutoverRequest(
    DatabaseRecoveryOperationId OperationId,
    DatabaseProtectionSetId ProtectionSetId,
    DatabaseEngine Engine,
    DatabaseRestorePointId RestorePointId,
    DatabaseFreshTargetDescriptor FreshTarget,
    long ValidationRevision,
    long FencingToken);

/// <summary>Success requires native health, application health and resumed admission.</summary>
public sealed record DatabaseCutoverResult(
    bool DatabaseHealthy,
    bool ApplicationHealthy,
    bool AdmissionResumed,
    string SafeEvidenceReference)
{
    public bool Succeeded => DatabaseHealthy && ApplicationHealthy && AdmissionResumed;
}

public interface IDatabaseCutoverCapability
{
    /// <summary>
    /// Executes under an external durable recovery journal, outside the API and databases
    /// being replaced. Interrupted replacements must reconcile before further mutation.
    /// </summary>
    ValueTask<DatabaseCutoverResult> RecoverAsync(
        DatabaseCutoverRequest request,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
}
