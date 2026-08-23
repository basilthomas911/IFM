namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;

public interface IScyllaSnapshotArtifactTransport
{
    ValueTask<long> ExportAsync(
        string backupLocation,
        string snapshotTag,
        IReadOnlyList<string> artifactReferences,
        string destinationDirectory,
        CancellationToken cancellationToken);

    ValueTask<long> EnsureAvailableAsync(
        string sourceBackupLocation,
        string destinationBackupLocation,
        string snapshotTag,
        string sourceDirectory,
        CancellationToken cancellationToken);
}

internal sealed class ReferenceOnlyScyllaSnapshotArtifactTransport : IScyllaSnapshotArtifactTransport
{
    public ValueTask<long> ExportAsync(
        string backupLocation,
        string snapshotTag,
        IReadOnlyList<string> artifactReferences,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(0L);
    }

    public ValueTask<long> EnsureAvailableAsync(
        string sourceBackupLocation,
        string destinationBackupLocation,
        string snapshotTag,
        string sourceDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(0L);
    }
}
