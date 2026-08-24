using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public static class AwsScyllaProtectionSetPolicy
{
    public static void ValidatePublication(
        IReadOnlyCollection<DatabaseRestorePointId> dependencies,
        DatabaseBackupLineage? lineage,
        ScyllaTopologyEvidence? topology,
        ScyllaSnapshotEvidence? snapshot)
    {
        if (dependencies.Count != 0)
            throw new InvalidDataException(
                "A Scylla Manager protection set is logically complete and cannot declare an IFM artifact dependency chain.");
        if (lineage is null || lineage.NativeKind is not (
                DatabaseNativeBackupKind.ScyllaManagerSnapshot or
                DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot))
            throw new InvalidDataException("The Scylla publication has no valid Manager lineage.");
        Validate(topology, snapshot);
    }

    public static ScyllaRecoveryExpectation CreateRecoveryExpectation(AwsPublicationRecord publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (publication.Engine != DatabaseEngine.ScyllaDb || publication.Dependencies.Length != 0)
            throw new InvalidDataException("The selected publication is not a logically complete Scylla protection set.");
        Validate(publication.ScyllaTopology, publication.ScyllaSnapshot);
        return new ScyllaRecoveryExpectation(publication.ScyllaTopology!, publication.ScyllaSnapshot!);
    }

    static void Validate(ScyllaTopologyEvidence? topology, ScyllaSnapshotEvidence? snapshot)
    {
        if (topology is null || string.IsNullOrWhiteSpace(topology.ClusterName)
            || topology.LiveNodeCount <= 0 || topology.TokenRangeCount <= 0 || !topology.SchemaAgreement)
            throw new InvalidDataException("The Scylla publication does not prove complete, schema-agreed topology coverage.");
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.SnapshotTag)
            || string.IsNullOrWhiteSpace(snapshot.ManagerTaskReference)
            || snapshot.SchemaSha256?.Length != 64 || snapshot.NativeManifestSha256?.Length != 64
            || snapshot.KeyspaceCount <= 0 || snapshot.TableCount <= 0 || snapshot.NodeCount <= 0
            || snapshot.NodeCount != topology.LiveNodeCount || snapshot.ArtifactCount <= 0
            || string.IsNullOrWhiteSpace(snapshot.ScyllaVersion) || string.IsNullOrWhiteSpace(snapshot.ManagerVersion))
            throw new InvalidDataException("The Scylla publication does not contain complete Manager snapshot evidence.");
    }
}
