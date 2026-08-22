using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Application.DatabaseBackup.Policies;

public sealed record DatabaseBackupChainPolicy(
    bool IncrementalEnabled,
    int MaximumIncrementalChainDepth,
    TimeSpan MaximumIncrementalBaseAge)
{
    public void Validate()
    {
        if (MaximumIncrementalChainDepth <= 0)
            throw new InvalidOperationException("The maximum incremental chain depth must be positive.");
        if (MaximumIncrementalBaseAge <= TimeSpan.Zero)
            throw new InvalidOperationException("The maximum incremental base age must be positive.");
    }
}

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

public static class DatabaseBackupManifestPolicy
{
    public static void Validate(DatabaseBackupManifest manifest, BackupSource? requiredSource = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion is not (1 or 2) || manifest.Revision <= 0)
            throw new InvalidDataException("The database backup manifest schema or revision is unsupported.");
        _ = new DatabaseProtectionSetId(manifest.ManifestId);
        if (manifest.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(manifest.RestorePointId.Value))
            throw new InvalidDataException("The database backup manifest identity is invalid.");
        DatabaseBackupEnumValidation.RequireConcrete(manifest.Source);
        DatabaseBackupEnumValidation.RequireDefined(manifest.Engine, nameof(manifest.Engine));
        if (requiredSource is { } expected && manifest.Source != expected)
            throw new InvalidDataException($"The manifest source must be '{expected}'.");
        var lineage = manifest.BackupLineage.NormalizeLegacyFull(manifest.Engine);
        lineage.Validate(resolvedRequired: true);
        if (manifest.SchemaVersion == 2 && manifest.BackupLineage.ResolvedMode == DatabaseBackupMode.None)
            throw new InvalidDataException("A version 2 manifest requires resolved backup lineage.");
        if (manifest.SchemaVersion == 2 && lineage.ResolvedMode == DatabaseBackupMode.Full && lineage.BaseRestorePointId != manifest.RestorePointId)
            throw new InvalidDataException("A version 2 full manifest must identify itself as the chain base.");
        if (lineage.NativeKind is DatabaseNativeBackupKind.PostgreSqlBase or DatabaseNativeBackupKind.PostgreSqlIncremental
            && manifest.Engine != DatabaseEngine.PostgreSql)
            throw new InvalidDataException("The manifest native kind conflicts with its database engine.");
        if (lineage.NativeKind is DatabaseNativeBackupKind.ScyllaManagerSnapshot or DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot
            && manifest.Engine != DatabaseEngine.ScyllaDb)
            throw new InvalidDataException("The manifest native kind conflicts with its database engine.");
        if (manifest.CreatedUtc == default || manifest.CreatedUtc.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Manifest creation time must be UTC.");
        if (string.IsNullOrWhiteSpace(manifest.SafeBoundaryReference) || manifest.SafeBoundaryReference.Any(char.IsControl))
            throw new InvalidDataException("The manifest boundary reference is invalid.");
        if (manifest.Artifacts.Length == 0 || manifest.Replicas.Length == 0)
            throw new InvalidDataException("The manifest must contain artifacts and replicas.");
        if (manifest.Artifacts.Select(static value => value.RelativePath).Distinct(StringComparer.Ordinal).Count() != manifest.Artifacts.Length)
            throw new InvalidDataException("The manifest contains duplicate artifact paths.");
        if (manifest.Dependencies.Contains(manifest.RestorePointId) || manifest.Dependencies.Distinct().Count() != manifest.Dependencies.Length)
            throw new InvalidDataException("The manifest dependency graph contains a self-reference or duplicate.");
        if (lineage.NativeKind == DatabaseNativeBackupKind.PostgreSqlIncremental
            && (manifest.Dependencies.Length != 1 || manifest.Dependencies[0] != lineage.ParentRestorePointId))
            throw new InvalidDataException("A PostgreSQL incremental manifest requires its direct parent dependency.");
        if (lineage.NativeKind != DatabaseNativeBackupKind.PostgreSqlIncremental && manifest.Dependencies.Length != 0)
            throw new InvalidDataException("Only PostgreSQL incremental manifests may declare restore-chain dependencies.");
        foreach (var artifact in manifest.Artifacts)
            if (artifact.Length < 0 || artifact.Sha256.Length != 64 || !artifact.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("A manifest artifact digest is invalid.");
    }
}

public static class DatabaseBackupCanonicalJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public static T Deserialize<T>(ReadOnlySpan<byte> value)
    {
        RejectDuplicateProperties(value);
        return JsonSerializer.Deserialize<T>(value, Options)
            ?? throw new InvalidDataException($"The signed {typeof(T).Name} document is empty.");
    }

    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new StrongIdJsonConverter<DatabaseRecoveryOperationId>(
            static value => new DatabaseRecoveryOperationId(Guid.ParseExact(value, "N")), static value => value.Format()));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseRetentionPlanId>(
            static value => new DatabaseRetentionPlanId(Guid.ParseExact(value, "N")), static value => value.Value.ToString("N")));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseProtectionSetId>(
            static value => new DatabaseProtectionSetId(value), static value => value.Value));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseRestorePointId>(
            static value => new DatabaseRestorePointId(value), static value => value.Value));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseArtifactId>(
            static value => new DatabaseArtifactId(value), static value => value.Value));
        options.Converters.Add(new StrongIdJsonConverter<DatabaseArtifactReplicaId>(
            static value => new DatabaseArtifactReplicaId(value), static value => value.Value));
        return options;
    }

    static void RejectDuplicateProperties(ReadOnlySpan<byte> value)
    {
        var reader = new Utf8JsonReader(value);
        var stack = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject) stack.Push(new(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) _ = stack.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && stack.Count > 0
                && !stack.Peek().Add(reader.GetString()!))
                throw new JsonException("Canonical JSON cannot contain duplicate properties.");
        }
    }

    sealed class StrongIdJsonConverter<T>(Func<string, T> parse, Func<T, string> format) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => parse(reader.GetString() ?? throw new JsonException("A strong identity cannot be null."));
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(format(value));
    }
}
