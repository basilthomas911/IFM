using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

using TomasAI.IFM.Shared.DatabaseBackup.Policies;

namespace TomasAI.IFM.Application.DatabaseBackup.Policies;

public sealed class DatabaseBackupChainPlanner(
    IDatabaseBackupCatalog catalog,
    DatabaseBackupChainPolicy policy,
    TimeProvider? timeProvider = null) : IDatabaseBackupChainPlanner
{
    readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async ValueTask<DatabaseBackupLineage> PlanAsync(
        DatabaseBackupPlanningRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        policy.Validate();
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.ProtectionSetId.Value))
            throw new ArgumentException("A backup operation and protection set are required.", nameof(request));
        DatabaseBackupEnumValidation.RequireDefined(request.Engine, nameof(request.Engine));
        DatabaseBackupEnumValidation.RequireOptionalDefined(request.RequestedMode, nameof(request.RequestedMode));
        var requested = request.RequestedMode == DatabaseBackupMode.None ? DatabaseBackupMode.Full : request.RequestedMode;
        if (requested == DatabaseBackupMode.Full) return Full(request, requested);
        if (!policy.IncrementalEnabled)
            return requested == DatabaseBackupMode.Automatic
                ? Full(request, requested)
                : throw new InvalidOperationException("Incremental backup is disabled.");

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
                .Where(point => point.Manifest.Engine == request.Engine && point.Manifest.ProtectionSetId == request.ProtectionSetId)
                .ToDictionary(static point => point.Entry.RestorePointId);
            common = common is null
                ? eligible
                : common.Where(pair => eligible.TryGetValue(pair.Key, out var other) && Equivalent(pair.Value, other))
                    .ToDictionary(static pair => pair.Key, static pair => pair.Value);
        }

        var parent = common?.Values.MaxBy(static point => point.Manifest.CreatedUtc);
        if (parent is null) return FallbackOrThrow(request, requested, "No common verified parent exists on every required replica.");
        var parentLineage = parent.Manifest.BackupLineage.NormalizeLegacyFull(request.Engine);
        var nextDepth = checked(parentLineage.ChainDepth + 1);
        if (nextDepth > policy.MaximumIncrementalChainDepth)
            return FallbackOrThrow(request, requested, "The configured maximum incremental chain depth was reached.");
        var baseId = parentLineage.BaseRestorePointId ?? parent.Entry.RestorePointId;
        var basePoint = common!.GetValueOrDefault(baseId);
        if (basePoint is null || _timeProvider.GetUtcNow() - basePoint.Manifest.CreatedUtc > policy.MaximumIncrementalBaseAge)
            return FallbackOrThrow(request, requested, "The incremental base is missing or older than the configured limit.");

        var result = new DatabaseBackupLineage
        {
            RequestedMode = requested,
            ResolvedMode = DatabaseBackupMode.Incremental,
            NativeKind = request.Engine == DatabaseEngine.PostgreSql
                ? DatabaseNativeBackupKind.PostgreSqlIncremental
                : DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot,
            BaseRestorePointId = baseId,
            ParentRestorePointId = parent.Entry.RestorePointId,
            ChainDepth = nextDepth,
            NativeIdentity = parentLineage.NativeIdentity
        };
        result.Validate(resolvedRequired: true);
        return result;
    }

    static bool Equivalent(DatabaseCatalogRestorePoint left, DatabaseCatalogRestorePoint right)
        => left.Manifest.ManifestId == right.Manifest.ManifestId
            && left.Manifest.Revision == right.Manifest.Revision
            && left.Manifest.Engine == right.Manifest.Engine
            && left.Manifest.ProtectionSetId == right.Manifest.ProtectionSetId
            && left.Manifest.SafeBoundaryReference == right.Manifest.SafeBoundaryReference
            && left.Manifest.BackupLineage == right.Manifest.BackupLineage
            && left.Manifest.Dependencies.SequenceEqual(right.Manifest.Dependencies);

    static DatabaseBackupLineage FallbackOrThrow(DatabaseBackupPlanningRequest request, DatabaseBackupMode requested, string reason)
        => requested == DatabaseBackupMode.Automatic ? Full(request, requested) : throw new InvalidOperationException(reason);

    static DatabaseBackupLineage Full(DatabaseBackupPlanningRequest request, DatabaseBackupMode requested)
    {
        var result = new DatabaseBackupLineage
        {
            RequestedMode = requested,
            ResolvedMode = DatabaseBackupMode.Full,
            NativeKind = request.Engine == DatabaseEngine.PostgreSql
                ? DatabaseNativeBackupKind.PostgreSqlBase
                : DatabaseNativeBackupKind.ScyllaManagerSnapshot,
            BaseRestorePointId = new DatabaseRestorePointId(request.OperationId.Format()),
            ChainDepth = 0
        };
        result.Validate(resolvedRequired: true);
        return result;
    }
}
