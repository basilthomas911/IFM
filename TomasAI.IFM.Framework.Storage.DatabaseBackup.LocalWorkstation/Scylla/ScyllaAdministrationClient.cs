using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

internal sealed record ScyllaNativeCapture(
    string ManagerTaskReference,
    string SnapshotTag,
    ScyllaTopologyEvidence Topology,
    string SchemaSha256,
    string NativeManifestSha256,
    string[] ArtifactReferences,
    int KeyspaceCount,
    int TableCount,
    long SourceBytes,
    string ScyllaVersion,
    string ManagerVersion,
    TimeSpan Elapsed);

internal sealed record ScyllaNativeVerification(
    bool Succeeded,
    ScyllaTopologyEvidence Topology,
    string NativeManifestSha256,
    long SourceBytes,
    TimeSpan Elapsed);

internal sealed record ScyllaNativeRestoreValidation(
    bool Succeeded,
    string RestoredClusterName,
    ScyllaTopologyEvidence Topology,
    long ValidationRevision,
    long RestoredBytes,
    TimeSpan Elapsed);

internal interface IScyllaAdministrationClient
{
    ValueTask ValidateAsync(CancellationToken cancellationToken);

    ValueTask<ScyllaNativeCapture> CaptureAsync(
        DatabaseRecoveryOperationId operationId,
        ScyllaProtectionSetOptions protectionSet,
        string nativeDirectory,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);

    ValueTask<ScyllaNativeVerification> VerifyAsync(
        ScyllaProtectionSetOptions protectionSet,
        ScyllaNativeCapture capture,
        string nativeDirectory,
        CancellationToken cancellationToken);

    ValueTask<ScyllaNativeRestoreValidation> RestoreAsync(
        DatabaseRecoveryOperationId operationId,
        ScyllaProtectionSetOptions source,
        ScyllaFreshTargetProfileOptions target,
        ScyllaNativeCapture capture,
        string sourceNativeDirectory,
        string restoreWorkspace,
        IProgress<DatabaseNativeProgress> progress,
        CancellationToken cancellationToken);
}
