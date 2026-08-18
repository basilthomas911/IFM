using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

public sealed class LocalBackupChainPlanner(
    IDatabaseBackupCatalog catalog,
    LocalWorkstationSourceOptions sourceOptions) : IDatabaseBackupChainPlanner
{
    public async ValueTask<DatabaseBackupLineage> PlanAsync(
        DatabaseBackupPlanningRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.ProtectionSetId.Value))
            throw new ArgumentException("A backup operation and protection set are required.", nameof(request));
        DatabaseBackupEnumValidation.RequireDefined(request.Engine, nameof(request.Engine));
        DatabaseBackupEnumValidation.RequireOptionalDefined(request.RequestedMode, nameof(request.RequestedMode));
        var requested = request.RequestedMode == DatabaseBackupMode.None
            ? DatabaseBackupMode.Full
            : request.RequestedMode;
        if (requested == DatabaseBackupMode.Full)
            return Full(request, requested);
        if (!sourceOptions.IncrementalEnabled)
            return requested == DatabaseBackupMode.Automatic
                ? Full(request, requested)
                : throw new InvalidOperationException("Local-workstation incremental backup is disabled.");

        var replicas = request.RequiredDestinations
            .Where(static destination => destination.Required)
            .Select(static destination => new DatabaseArtifactReplicaId(destination.Name))
            .Distinct()
            .ToArray();
        if (replicas.Length == 0)
            throw new InvalidOperationException("Incremental planning requires at least one required replica.");

        Dictionary<DatabaseRestorePointId, DatabaseCatalogRestorePoint>? common = null;
        foreach (var replica in replicas)
        {
            var eligible = (await catalog.EnumerateAsync(replica, cancellationToken).ConfigureAwait(false))
                .Where(point => point.Manifest.Engine == request.Engine
                    && point.Manifest.ProtectionSetId == request.ProtectionSetId)
                .ToDictionary(static point => point.Entry.RestorePointId);
            if (common is null)
            {
                common = eligible;
                continue;
            }
            common = common
                .Where(pair => eligible.TryGetValue(pair.Key, out var other)
                    && EquivalentAcrossReplicas(pair.Value, other))
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);
        }

        var parent = common?.Values.MaxBy(static point => point.Manifest.CreatedUtc);
        if (parent is null)
            return FallbackOrThrow(request, requested, "No common verified parent exists on every required replica.");

        var parentLineage = parent.Manifest.BackupLineage.NormalizeLegacyFull(request.Engine);
        var nextDepth = checked(parentLineage.ChainDepth + 1);
        if (nextDepth > sourceOptions.MaximumIncrementalChainDepth)
            return FallbackOrThrow(request, requested, "The configured maximum incremental chain depth was reached.");
        var baseId = parentLineage.BaseRestorePointId ?? parent.Entry.RestorePointId;
        var basePoint = common!.GetValueOrDefault(baseId);
        if (basePoint is null
            || DateTimeOffset.UtcNow - basePoint.Manifest.CreatedUtc > sourceOptions.MaximumIncrementalBaseAge)
            return FallbackOrThrow(request, requested, "The incremental base is missing or older than the configured limit.");

        var result = new DatabaseBackupLineage
        {
            RequestedMode = requested,
            ResolvedMode = DatabaseBackupMode.Incremental,
            NativeKind = request.Engine switch
            {
                DatabaseEngine.PostgreSql => DatabaseNativeBackupKind.PostgreSqlIncremental,
                DatabaseEngine.ScyllaDb => DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot,
                _ => throw new ArgumentOutOfRangeException(nameof(request.Engine))
            },
            BaseRestorePointId = baseId,
            ParentRestorePointId = parent.Entry.RestorePointId,
            ChainDepth = nextDepth,
            NativeIdentity = parentLineage.NativeIdentity
        };
        result.Validate(resolvedRequired: true);
        return result;
    }

    static bool EquivalentAcrossReplicas(
        DatabaseCatalogRestorePoint left,
        DatabaseCatalogRestorePoint right)
        => string.Equals(left.Manifest.ManifestId, right.Manifest.ManifestId, StringComparison.Ordinal)
            && left.Manifest.Revision == right.Manifest.Revision
            && left.Manifest.Engine == right.Manifest.Engine
            && left.Manifest.ProtectionSetId == right.Manifest.ProtectionSetId
            && string.Equals(left.Manifest.SafeBoundaryReference, right.Manifest.SafeBoundaryReference, StringComparison.Ordinal)
            && left.Manifest.BackupLineage == right.Manifest.BackupLineage
            && left.Manifest.Dependencies.SequenceEqual(right.Manifest.Dependencies);

    static DatabaseBackupLineage FallbackOrThrow(
        DatabaseBackupPlanningRequest request,
        DatabaseBackupMode requested,
        string reason)
        => requested == DatabaseBackupMode.Automatic
            ? Full(request, requested)
            : throw new InvalidOperationException(reason);

    static DatabaseBackupLineage Full(
        DatabaseBackupPlanningRequest request,
        DatabaseBackupMode requested)
    {
        var result = new DatabaseBackupLineage
        {
            RequestedMode = requested,
            ResolvedMode = DatabaseBackupMode.Full,
            NativeKind = request.Engine switch
            {
                DatabaseEngine.PostgreSql => DatabaseNativeBackupKind.PostgreSqlBase,
                DatabaseEngine.ScyllaDb => DatabaseNativeBackupKind.ScyllaManagerSnapshot,
                _ => throw new ArgumentOutOfRangeException(nameof(request.Engine))
            },
            BaseRestorePointId = new DatabaseRestorePointId(request.OperationId.Format()),
            ChainDepth = 0
        };
        result.Validate(resolvedRequired: true);
        return result;
    }
}
