using System.Security.Cryptography;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

public sealed class LocalBackupRepository :
    IDatabaseBackupPublicationCapability,
    IDatabaseRestoreSourceCapability,
    ILocalBackupVault,
    IOfflineBackupMediaProvider,
    IRestoreWorkspace,
    IDatabaseBackupCatalog
{
    readonly DatabaseBackupPublicationOptions _options;
    readonly PostgreSqlBackupOptions _postgreSql;
    readonly ScyllaBackupOptions _scylla;
    readonly IBackupPathPolicy _paths;
    readonly IArtifactChecksumService _checksums;
    readonly IManifestSignatureService _signatures;
    readonly IDatabaseBackupManifestWriter _manifestWriter;
    readonly IDatabaseBackupManifestReader _manifestReader;
    readonly ILocalBackupCapacityReader _capacity;

    public LocalBackupRepository(
        DatabaseBackupPublicationOptions options,
        PostgreSqlBackupOptions postgreSql,
        ScyllaBackupOptions scylla,
        IBackupPathPolicy paths,
        IArtifactChecksumService checksums,
        IManifestSignatureService signatures,
        IDatabaseBackupManifestWriter manifestWriter,
        IDatabaseBackupManifestReader manifestReader,
        ILocalBackupCapacityReader capacity)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _postgreSql = postgreSql ?? throw new ArgumentNullException(nameof(postgreSql));
        _scylla = scylla ?? throw new ArgumentNullException(nameof(scylla));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _checksums = checksums ?? throw new ArgumentNullException(nameof(checksums));
        _signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
        _manifestWriter = manifestWriter ?? throw new ArgumentNullException(nameof(manifestWriter));
        _manifestReader = manifestReader ?? throw new ArgumentNullException(nameof(manifestReader));
        _capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
        _options.Validate(requirePrivateKey: false);
    }

    public async ValueTask<DatabaseBackupPublicationResult> PublishAsync(
        DatabaseBackupPublicationRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var sourceRoot = NativeRestorePointRoot(request.Engine, request.OperationId.Format());
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException("The verified native backup artifact set is unavailable.");
        ValidateTree(sourceRoot);

        var requestedReplicas = ResolveRequestedReplicas(request.RequiredDestinations);
        var manifestId = $"manifest-{request.OperationId.Value:N}";
        var restorePointId = new DatabaseRestorePointId(request.OperationId.Format());
        var existing = await TryResolveExistingPublicationAsync(
            request, restorePointId, manifestId, requestedReplicas, sourceRoot, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return existing;
        var artifactPrefix = ArtifactPrefix(request.ProtectionSetId, request.OperationId, request.Engine, manifestId);
        var artifacts = await DescribeArtifactsAsync(sourceRoot, artifactPrefix, cancellationToken).ConfigureAwait(false);
        var lineage = request.BackupLineage?.NormalizeLegacyFull(request.Engine)
            ?? new DatabaseBackupLineage
            {
                RequestedMode = DatabaseBackupMode.Full,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = request.Engine == DatabaseEngine.PostgreSql
                    ? DatabaseNativeBackupKind.PostgreSqlBase
                    : DatabaseNativeBackupKind.ScyllaManagerSnapshot,
                BaseRestorePointId = restorePointId
            };
        lineage.Validate(resolvedRequired: true);
        var manifest = new DatabaseBackupManifest
        {
            ManifestId = manifestId,
            OperationId = request.OperationId,
            RestorePointId = restorePointId,
            Engine = request.Engine,
            ProtectionSetId = request.ProtectionSetId,
            SafeBoundaryReference = request.SafeBoundaryReference,
            CreatedUtc = DateTimeOffset.UtcNow,
            Dependencies = request.Dependencies ?? [],
            Artifacts = artifacts,
            Replicas = requestedReplicas,
            Statistics = request.Statistics,
            BackupLineage = lineage
        };
        LocalBackupManifestStore.Validate(manifest);

        var published = new List<DatabaseArtifactReplicaDescriptor>(requestedReplicas.Length);
        foreach (var replica in requestedReplicas)
        {
            var result = await PublishReplicaAsync(
                new DatabaseReplicaPublicationRequest(manifest, request.OperationId, replica),
                sourceRoot,
                cancellationToken).ConfigureAwait(false);
            published.Add(result.Replica);
        }
        return new DatabaseBackupPublicationResult(restorePointId, manifestId, manifest.Revision, [.. published]);
    }

    public async ValueTask ValidateAsync(
        DatabaseBackupPublicationPreflightRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DatabaseBackupEnumValidation.RequireDefined(request.Engine, nameof(request.Engine));
        if (string.IsNullOrWhiteSpace(request.ProtectionSetId.Value))
            throw new ArgumentException("A publication protection set is required.", nameof(request));
        var replicas = ResolveRequestedReplicas(request.RequiredDestinations);
        foreach (var replica in replicas)
        {
            await ValidateIdentityAsync(replica, cancellationToken).ConfigureAwait(false);
            var reserve = replica == new DatabaseArtifactReplicaId(_options.OnlineVault.ReplicaId)
                ? _options.OnlineVault.MinimumFreeBytes
                : _options.OfflineMedia.MinimumFreeBytes;
            EnsureCapacity(RootForReplica(replica), requiredBytes: 0, reserve);
        }
        Directory.CreateDirectory(_options.RestoreWorkspace.ResolveRoot());
        ValidateTree(_options.RestoreWorkspace.ResolveRoot());
        EnsureCapacity(_options.RestoreWorkspace.ResolveRoot(), requiredBytes: 0,
            _options.RestoreWorkspace.MinimumFreeBytes);
    }

    public async ValueTask<DatabasePreparedRestoreSource> PrepareAsync(
        DatabaseRestoreSourceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OperationId.Value == Guid.Empty || string.IsNullOrWhiteSpace(request.RestorePointId.Value))
            throw new ArgumentException("A restore operation and restore point are required.", nameof(request));
        DatabaseBackupEnumValidation.RequireDefined(request.Engine, nameof(request.Engine));

        var candidates = request.PreferredReplicaId is { } preferred
            ? [preferred]
            : CandidateReplicas();
        List<Exception>? failures = null;
        foreach (var replica in candidates)
        {
            try
            {
                var available = await EnumerateAsync(replica, cancellationToken).ConfigureAwait(false);
                var restorePoint = available.SingleOrDefault(value => value.Entry.RestorePointId == request.RestorePointId)
                    ?? throw new FileNotFoundException("The restore point is not catalog-visible on the requested replica.");
                if (restorePoint.Manifest.Engine != request.Engine)
                    throw new InvalidDataException("The restore point engine does not match the requested engine.");
                var dependencies = ResolveDependencyOrder(restorePoint, available);
                foreach (var dependency in dependencies)
                {
                    var dependencyStaged = await StageAsync(request.OperationId, dependency, cancellationToken)
                        .ConfigureAwait(false);
                    await MaterializeNativeRestorePointAsync(
                        dependencyStaged, dependency.Manifest, cancellationToken).ConfigureAwait(false);
                }
                var staged = await StageAsync(request.OperationId, restorePoint, cancellationToken).ConfigureAwait(false);
                await MaterializeNativeRestorePointAsync(staged, restorePoint.Manifest, cancellationToken)
                    .ConfigureAwait(false);
                return new DatabasePreparedRestoreSource(
                    request.RestorePointId,
                    replica,
                    restorePoint.Manifest.ManifestId,
                    restorePoint.Manifest.Revision,
                    restorePoint.VerifiedBytes,
                    restorePoint.VerifiedArtifactCount,
                    dependencies.Select(static point => point.Entry.RestorePointId).ToArray());
            }
            catch (Exception exception) when (request.PreferredReplicaId is null
                && exception is IOException or UnauthorizedAccessException or CryptographicException or InvalidDataException)
            {
                (failures ??= []).Add(exception);
            }
        }
        throw new AggregateException("No eligible local backup replica could prepare the restore point.", failures ?? []);
    }

    ValueTask<DatabaseMediaIdentity> ILocalBackupVault.EnrollAsync(
        DatabaseMediaEnrollmentRequest request,
        CancellationToken cancellationToken)
        => EnrollAsync(request, online: true, cancellationToken);

    ValueTask<DatabaseMediaIdentity> IOfflineBackupMediaProvider.EnrollAsync(
        DatabaseMediaEnrollmentRequest request,
        CancellationToken cancellationToken)
        => EnrollAsync(request, online: false, cancellationToken);

    public ValueTask<DatabaseMediaIdentity> ValidateAttachedMediaAsync(CancellationToken cancellationToken)
        => ReadIdentityAsync(offline: true, cancellationToken);

    ValueTask<DatabaseReplicaPublicationResult> ILocalBackupVault.PublishAsync(
        DatabaseReplicaPublicationRequest request,
        CancellationToken cancellationToken)
        => PublishReplicaAsync(request, NativeRestorePointRoot(
            request.Manifest.Engine, request.NativeArtifactOperationId.Format()), cancellationToken);

    ValueTask<DatabaseReplicaPublicationResult> IOfflineBackupMediaProvider.PublishAsync(
        DatabaseReplicaPublicationRequest request,
        CancellationToken cancellationToken)
        => PublishReplicaAsync(request, NativeRestorePointRoot(
            request.Manifest.Engine, request.NativeArtifactOperationId.Format()), cancellationToken);

    public async ValueTask<string> StageAsync(
        DatabaseRecoveryOperationId operationId,
        DatabaseCatalogRestorePoint restorePoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        var workspaceRoot = _options.RestoreWorkspace.ResolveRoot();
        var requiredBytes = restorePoint.Manifest.Artifacts.Sum(static value => value.Length);
        EnsureCapacity(workspaceRoot, requiredBytes, _options.RestoreWorkspace.MinimumFreeBytes);
        Directory.CreateDirectory(workspaceRoot);
        ValidateTree(workspaceRoot);

        var sourceRoot = RootForReplica(restorePoint.Entry.ReplicaId);
        await ValidateIdentityAsync(restorePoint.Entry.ReplicaId, cancellationToken).ConfigureAwait(false);
        var incomingRelative = $"incoming/{operationId.Format()}/{restorePoint.Manifest.RestorePointId.Value}.inprogress";
        var finalRelative = $"staged/{operationId.Format()}/{restorePoint.Manifest.RestorePointId.Value}";
        var incoming = ResolvePath(workspaceRoot, incomingRelative);
        var final = ResolvePath(workspaceRoot, finalRelative);
        if (Directory.Exists(incoming) || Directory.Exists(final))
            throw new IOException("The immutable restore workspace identity already exists.");
        Directory.CreateDirectory(incoming);

        var prefix = ArtifactPrefix(restorePoint.Manifest.ProtectionSetId, restorePoint.Manifest.OperationId,
            restorePoint.Manifest.Engine, restorePoint.Manifest.ManifestId) + "/";
        foreach (var artifact in restorePoint.Manifest.Artifacts)
        {
            if (!artifact.RelativePath.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("A manifest artifact is outside its declared artifact version.");
            var nativeRelative = artifact.RelativePath[prefix.Length..];
            var source = ResolvePath(sourceRoot, artifact.RelativePath);
            var destination = ResolvePath(incoming, nativeRelative);
            await CopyCreateNewAndVerifyAsync(source, destination, artifact, cancellationToken).ConfigureAwait(false);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(final)!);
        Directory.Move(incoming, final);
        return final;
    }

    public async ValueTask<DatabaseCatalogRestorePoint> ResolveAsync(
        DatabaseRestorePointId restorePointId,
        DatabaseArtifactReplicaId replicaId,
        CancellationToken cancellationToken)
    {
        var entries = await EnumerateAsync(replicaId, cancellationToken).ConfigureAwait(false);
        return entries.SingleOrDefault(value => value.Entry.RestorePointId == restorePointId)
            ?? throw new FileNotFoundException("The restore point is not catalog-visible on the requested replica.");
    }

    public async ValueTask<IReadOnlyList<DatabaseCatalogRestorePoint>> EnumerateAsync(
        DatabaseArtifactReplicaId replicaId,
        CancellationToken cancellationToken)
    {
        await ValidateIdentityAsync(replicaId, cancellationToken).ConfigureAwait(false);
        var root = RootForReplica(replicaId);
        var catalogRoot = ResolvePath(root, CatalogRoot());
        if (!Directory.Exists(catalogRoot)) return [];
        ValidateTree(catalogRoot);
        var results = new List<DatabaseCatalogRestorePoint>();
        var entryPaths = Directory.EnumerateFiles(catalogRoot, "*.json", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith(".sig", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Take(_options.Limits.MaximumCatalogEntryCount + 1)
            .ToArray();
        if (entryPaths.Length > _options.Limits.MaximumCatalogEntryCount)
            throw new InvalidDataException("The local backup catalog exceeds its configured entry limit.");
        foreach (var entryPath in entryPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = await LocalBackupJson.ReadSignedAsync<DatabaseCatalogEntry>(
                entryPath, _signatures, cancellationToken).ConfigureAwait(false);
            if (entry.SchemaVersion != 1 || entry.ReplicaId != replicaId)
                throw new InvalidDataException("A catalog entry has an invalid schema or replica identity.");
            var manifest = await _manifestReader.ReadAndVerifyAsync(
                Approve(root), entry.ManifestRelativePath, cancellationToken).ConfigureAwait(false);
            if (manifest.RestorePointId != entry.RestorePointId
                || manifest.ManifestId != entry.ManifestId
                || manifest.Revision != entry.ManifestRevision
                || manifest.Engine != entry.Engine
                || manifest.ProtectionSetId != entry.ProtectionSetId
                || !manifest.Replicas.Contains(replicaId))
                throw new InvalidDataException("A catalog entry conflicts with its signed manifest.");
            await ValidateCommitAsync(root, entry, manifest, cancellationToken).ConfigureAwait(false);
            if (replicaId == new DatabaseArtifactReplicaId(_options.OfflineMedia.ReplicaId))
                await ValidateMediaSealAsync(root, entry, manifest, cancellationToken).ConfigureAwait(false);
            long bytes = 0;
            foreach (var expected in manifest.Artifacts)
            {
                var actual = await CalculateChecksumAsync(root, expected.RelativePath, cancellationToken)
                    .ConfigureAwait(false);
                if (actual.Length != expected.Length
                    || !string.Equals(actual.Sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new CryptographicException("A published backup artifact failed digest verification.");
                bytes = checked(bytes + actual.Length);
            }
            results.Add(new DatabaseCatalogRestorePoint(entry, manifest, bytes, manifest.Artifacts.Length));
        }
        ValidateDependencyGraph(results);
        return results;
    }

    static void ValidateDependencyGraph(IReadOnlyList<DatabaseCatalogRestorePoint> points)
    {
        var byId = points.ToDictionary(static point => point.Entry.RestorePointId);
        foreach (var point in points)
        {
            var visiting = new HashSet<DatabaseRestorePointId>();
            var visited = new HashSet<DatabaseRestorePointId>();
            Visit(point.Entry.RestorePointId, byId, visiting, visited);
        }

        static void Visit(
            DatabaseRestorePointId id,
            IReadOnlyDictionary<DatabaseRestorePointId, DatabaseCatalogRestorePoint> byId,
            HashSet<DatabaseRestorePointId> visiting,
            HashSet<DatabaseRestorePointId> visited)
        {
            if (visited.Contains(id)) return;
            if (!byId.TryGetValue(id, out var point))
                throw new InvalidDataException("A restore point dependency is missing from this replica.");
            if (!visiting.Add(id))
                throw new InvalidDataException("The restore point dependency graph contains a cycle.");
            foreach (var dependency in point.Manifest.Dependencies)
                Visit(dependency, byId, visiting, visited);
            visiting.Remove(id);
            visited.Add(id);
        }
    }

    static DatabaseCatalogRestorePoint[] ResolveDependencyOrder(
        DatabaseCatalogRestorePoint target,
        IReadOnlyList<DatabaseCatalogRestorePoint> available)
    {
        var byId = available.ToDictionary(static point => point.Entry.RestorePointId);
        var ordered = new List<DatabaseCatalogRestorePoint>();
        var visited = new HashSet<DatabaseRestorePointId>();
        Visit(target);
        return [.. ordered];

        void Visit(DatabaseCatalogRestorePoint point)
        {
            foreach (var dependencyId in point.Manifest.Dependencies)
            {
                if (!byId.TryGetValue(dependencyId, out var dependency))
                    throw new InvalidDataException("A restore dependency is missing from the selected replica.");
                if (!visited.Add(dependencyId)) continue;
                Visit(dependency);
                ordered.Add(dependency);
            }
        }
    }

    async ValueTask<DatabaseMediaIdentity> EnrollAsync(
        DatabaseMediaEnrollmentRequest request,
        bool online,
        CancellationToken cancellationToken)
    {
        var expectedReplica = new DatabaseArtifactReplicaId(online
            ? _options.OnlineVault.ReplicaId : _options.OfflineMedia.ReplicaId);
        var expectedMedia = online ? _options.OnlineVault.MediaId : _options.OfflineMedia.ExpectedMediaId;
        var expectedSlot = online ? "online" : _options.OfflineMedia.RotationSlot;
        if (request.ReplicaId != expectedReplica
            || !string.Equals(request.MediaId, expectedMedia, StringComparison.Ordinal)
            || !string.Equals(request.EnvironmentId, _options.EnvironmentId, StringComparison.Ordinal)
            || !string.Equals(request.RotationSlot, expectedSlot, StringComparison.Ordinal)
            || request.EnrolledUtc == default || request.EnrolledUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("The media enrollment request does not match the configured identity.");
        if (!online && !_options.OfflineMedia.Enabled)
            throw new InvalidOperationException("Offline media is disabled.");
        var root = online ? _options.OnlineVault.ResolveRoot() : _options.OfflineMedia.ResolveRoot();
        Directory.CreateDirectory(root);
        ValidateTree(root);
        var publicKey = await File.ReadAllBytesAsync(
            _options.Manifest.PublicKeyPemFile, cancellationToken).ConfigureAwait(false);
        var trustBundleSha256 = Convert.ToHexStringLower(SHA256.HashData(publicKey));
        var identity = new DatabaseMediaIdentity(
            1, request.MediaId, request.ReplicaId, request.EnvironmentId,
            request.RotationSlot, request.EnrolledUtc, _signatures.KeyId, trustBundleSha256);
        await LocalBackupJson.WriteCreateNewAsync(
            ResolvePath(root, TrustBundleRelativePath), publicKey, cancellationToken).ConfigureAwait(false);
        var identityPath = ResolvePath(root, EnrollmentRelativePath);
        await LocalBackupJson.WriteSignedCreateNewAsync(
            identityPath, identity, _signatures, cancellationToken).ConfigureAwait(false);
        return await ReadIdentityAsync(!online, cancellationToken).ConfigureAwait(false);
    }

    async ValueTask<DatabaseMediaIdentity> ReadIdentityAsync(bool offline, CancellationToken cancellationToken)
    {
        if (offline && !_options.OfflineMedia.Enabled)
            throw new InvalidOperationException("Offline media is disabled.");
        var root = offline ? _options.OfflineMedia.ResolveRoot() : _options.OnlineVault.ResolveRoot();
        var path = ResolvePath(root, EnrollmentRelativePath);
        var identity = await LocalBackupJson.ReadSignedAsync<DatabaseMediaIdentity>(
            path, _signatures, cancellationToken).ConfigureAwait(false);
        var expectedReplica = new DatabaseArtifactReplicaId(offline
            ? _options.OfflineMedia.ReplicaId : _options.OnlineVault.ReplicaId);
        var expectedMedia = offline ? _options.OfflineMedia.ExpectedMediaId : _options.OnlineVault.MediaId;
        var expectedSlot = offline ? _options.OfflineMedia.RotationSlot : "online";
        var trustBundle = await File.ReadAllBytesAsync(
            ResolvePath(root, TrustBundleRelativePath), cancellationToken).ConfigureAwait(false);
        var trustBundleSha256 = Convert.ToHexStringLower(SHA256.HashData(trustBundle));
        var mismatch = identity.SchemaVersion != 1 ? "schema"
            : identity.ReplicaId != expectedReplica ? "replica"
            : !string.Equals(identity.MediaId, expectedMedia, StringComparison.Ordinal) ? "media"
            : !string.Equals(identity.EnvironmentId, _options.EnvironmentId, StringComparison.Ordinal) ? "environment"
            : !string.Equals(identity.RotationSlot, expectedSlot, StringComparison.Ordinal) ? "rotation-slot"
            : !string.Equals(identity.SigningKeyId, _signatures.KeyId, StringComparison.Ordinal) ? "signing-key"
            : !string.Equals(identity.TrustBundleSha256, trustBundleSha256, StringComparison.OrdinalIgnoreCase) ? "trust-bundle"
            : null;
        if (mismatch is not null)
            throw new InvalidDataException($"The attached vault/media identity is wrong or untrusted ({mismatch}).");
        return identity;
    }

    async ValueTask ValidateIdentityAsync(
        DatabaseArtifactReplicaId replicaId,
        CancellationToken cancellationToken)
    {
        var online = new DatabaseArtifactReplicaId(_options.OnlineVault.ReplicaId);
        if (replicaId == online)
        {
            _ = await ReadIdentityAsync(offline: false, cancellationToken).ConfigureAwait(false);
            return;
        }
        var offline = new DatabaseArtifactReplicaId(_options.OfflineMedia.ReplicaId);
        if (_options.OfflineMedia.Enabled && replicaId == offline)
        {
            _ = await ReadIdentityAsync(offline: true, cancellationToken).ConfigureAwait(false);
            return;
        }
        throw new InvalidOperationException("The requested local backup replica is not configured.");
    }

    async ValueTask<DatabaseReplicaPublicationResult> PublishReplicaAsync(
        DatabaseReplicaPublicationRequest request,
        string sourceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        LocalBackupManifestStore.Validate(request.Manifest);
        if (!request.Manifest.Replicas.Contains(request.ReplicaId))
            throw new InvalidOperationException("The manifest does not declare the requested replica.");
        if (request.NativeArtifactOperationId != request.Manifest.OperationId)
            throw new InvalidOperationException("The native artifact operation does not match the manifest.");
        await ValidateIdentityAsync(request.ReplicaId, cancellationToken).ConfigureAwait(false);
        if (request.ReplicaId == new DatabaseArtifactReplicaId(_options.OfflineMedia.ReplicaId))
            foreach (var dependency in request.Manifest.Dependencies)
                _ = await ResolveAsync(dependency, request.ReplicaId, cancellationToken).ConfigureAwait(false);
        var root = RootForReplica(request.ReplicaId);
        ValidateTree(root);
        ValidateTree(sourceRoot);
        var requiredBytes = request.Manifest.Artifacts.Sum(static value => value.Length);
        var reserve = request.ReplicaId == new DatabaseArtifactReplicaId(_options.OnlineVault.ReplicaId)
            ? _options.OnlineVault.MinimumFreeBytes
            : _options.OfflineMedia.MinimumFreeBytes;
        EnsureCapacity(root, requiredBytes, reserve);

        var artifactPrefix = ArtifactPrefix(request.Manifest.ProtectionSetId, request.Manifest.OperationId,
            request.Manifest.Engine, request.Manifest.ManifestId);
        var incomingRelative = $"incoming/{request.Manifest.OperationId.Format()}/{request.Manifest.ManifestId}";
        var incomingRoot = ResolvePath(root, incomingRelative);
        var finalArtifactRoot = ResolvePath(root, artifactPrefix);
        if (Directory.Exists(incomingRoot) || Directory.Exists(finalArtifactRoot))
            throw new IOException("The immutable backup artifact version already exists.");
        Directory.CreateDirectory(incomingRoot);

        foreach (var artifact in request.Manifest.Artifacts)
        {
            var prefix = artifactPrefix + "/";
            if (!artifact.RelativePath.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("A manifest artifact is outside its declared artifact version.");
            var nativeRelative = artifact.RelativePath[prefix.Length..];
            var source = ResolvePath(sourceRoot, nativeRelative);
            var destination = ResolvePath(incomingRoot, nativeRelative);
            await CopyCreateNewAndVerifyAsync(source, destination, artifact, cancellationToken).ConfigureAwait(false);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(finalArtifactRoot)!);
        Directory.Move(incomingRoot, finalArtifactRoot);

        var manifestRelative = ManifestRelativePath(request.Manifest);
        await _manifestWriter.WriteSignedAsync(Approve(root), manifestRelative, request.Manifest, cancellationToken)
            .ConfigureAwait(false);
        var manifestBytes = await File.ReadAllBytesAsync(ResolvePath(root, manifestRelative), cancellationToken)
            .ConfigureAwait(false);
        var commitRelative = CommitRelativePath(request.Manifest);
        var commit = new LocalPublicationCommit(
            1,
            request.Manifest.RestorePointId,
            request.Manifest.ManifestId,
            request.Manifest.Revision,
            manifestRelative,
            Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
            request.ReplicaId,
            DateTimeOffset.UtcNow);
        await LocalBackupJson.WriteSignedCreateNewAsync(
            ResolvePath(root, commitRelative), commit, _signatures, cancellationToken).ConfigureAwait(false);

        var catalogRelative = CatalogEntryRelativePath(request.Manifest, request.ReplicaId);
        var entry = new DatabaseCatalogEntry(
            1,
            request.Manifest.RestorePointId,
            request.Manifest.ManifestId,
            request.Manifest.Revision,
            request.Manifest.Engine,
            request.Manifest.ProtectionSetId,
            request.ReplicaId,
            manifestRelative,
            commitRelative,
            DateTimeOffset.UtcNow);
        await LocalBackupJson.WriteSignedCreateNewAsync(
            ResolvePath(root, catalogRelative), entry, _signatures, cancellationToken).ConfigureAwait(false);

        if (request.ReplicaId == new DatabaseArtifactReplicaId(_options.OfflineMedia.ReplicaId))
        {
            var seal = new LocalMediaSeal(
                1,
                _options.OfflineMedia.ExpectedMediaId,
                _options.OfflineMedia.RotationSlot,
                1,
                request.Manifest.RestorePointId,
                request.Manifest.ManifestId,
                request.Manifest.Artifacts.Sum(static artifact => artifact.Length),
                request.Manifest.Artifacts.Length,
                DependencyComplete: true,
                VerificationSucceeded: true,
                DateTimeOffset.UtcNow);
            await LocalBackupJson.WriteSignedCreateNewAsync(
                ResolvePath(root, MediaSealRelativePath(request.Manifest.ManifestId)),
                seal,
                _signatures,
                cancellationToken).ConfigureAwait(false);
        }

        _ = await ResolveAsync(request.Manifest.RestorePointId, request.ReplicaId, cancellationToken)
            .ConfigureAwait(false);
        var descriptor = new DatabaseArtifactReplicaDescriptor
        {
            ArtifactId = new DatabaseArtifactId($"artifact-{request.Manifest.OperationId.Value:N}"),
            ReplicaId = request.ReplicaId,
            Engine = request.Manifest.Engine,
            State = DatabaseArtifactReplicaState.Published,
            SafeDestinationReference = $"{request.ReplicaId.Value}:{request.Manifest.RestorePointId.Value}",
            Bytes = requiredBytes
        };
        return new DatabaseReplicaPublicationResult(descriptor, manifestRelative, commitRelative);
    }

    async ValueTask ValidateCommitAsync(
        string root,
        DatabaseCatalogEntry entry,
        DatabaseBackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var commit = await LocalBackupJson.ReadSignedAsync<LocalPublicationCommit>(
            ResolvePath(root, entry.CommitRelativePath), _signatures, cancellationToken).ConfigureAwait(false);
        var manifestContent = await File.ReadAllBytesAsync(
            ResolvePath(root, entry.ManifestRelativePath), cancellationToken).ConfigureAwait(false);
        if (commit.SchemaVersion != 1
            || commit.RestorePointId != entry.RestorePointId
            || commit.ManifestId != manifest.ManifestId
            || commit.ManifestRevision != manifest.Revision
            || commit.ManifestRelativePath != entry.ManifestRelativePath
            || commit.ReplicaId != entry.ReplicaId
            || !string.Equals(commit.ManifestSha256,
                Convert.ToHexStringLower(SHA256.HashData(manifestContent)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The signed publication commit does not match its catalog entry.");
    }

    async ValueTask ValidateMediaSealAsync(
        string root,
        DatabaseCatalogEntry entry,
        DatabaseBackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var seal = await LocalBackupJson.ReadSignedAsync<LocalMediaSeal>(
            ResolvePath(root, MediaSealRelativePath(manifest.ManifestId)),
            _signatures,
            cancellationToken).ConfigureAwait(false);
        if (seal.SchemaVersion != 1
            || !string.Equals(seal.MediaId, _options.OfflineMedia.ExpectedMediaId, StringComparison.Ordinal)
            || !string.Equals(seal.RotationSlot, _options.OfflineMedia.RotationSlot, StringComparison.Ordinal)
            || seal.SealRevision != 1
            || seal.RestorePointId != entry.RestorePointId
            || seal.ManifestId != manifest.ManifestId
            || seal.Bytes != manifest.Artifacts.Sum(static artifact => artifact.Length)
            || seal.FileCount != manifest.Artifacts.Length
            || !seal.DependencyComplete
            || !seal.VerificationSucceeded)
            throw new InvalidDataException("The signed offline media seal is incomplete or conflicts with its manifest.");
    }

    async ValueTask MaterializeNativeRestorePointAsync(
        string stagedRoot,
        DatabaseBackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var destination = NativeRestorePointRoot(manifest.Engine, manifest.RestorePointId.Value);
        var prefix = ArtifactPrefix(manifest.ProtectionSetId, manifest.OperationId,
            manifest.Engine, manifest.ManifestId) + "/";
        if (Directory.Exists(destination))
        {
            ValidateTree(destination);
            var expectedNativePaths = manifest.Artifacts
                .Select(artifact => artifact.RelativePath[prefix.Length..])
                .ToHashSet(StringComparer.Ordinal);
            var actualNativePaths = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(destination, path).Replace('\\', '/'))
                .ToHashSet(StringComparer.Ordinal);
            if (!actualNativePaths.SetEquals(expectedNativePaths))
                throw new InvalidDataException("An existing native restore point contains unexpected or missing artifacts.");
            foreach (var artifact in manifest.Artifacts)
            {
                var nativeRelative = artifact.RelativePath[prefix.Length..];
                var actual = await CalculateChecksumAsync(destination, nativeRelative, cancellationToken)
                    .ConfigureAwait(false);
                if (actual.Length != artifact.Length
                    || !string.Equals(actual.Sha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new CryptographicException("An existing native restore point conflicts with the signed manifest.");
            }
            return;
        }

        var nativeRoot = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(nativeRoot);
        var incoming = destination + ".restore-inprogress";
        if (Directory.Exists(incoming))
            throw new IOException("The native restore materialization identity already exists.");
        Directory.CreateDirectory(incoming);
        foreach (var artifact in manifest.Artifacts)
        {
            var nativeRelative = artifact.RelativePath[prefix.Length..];
            await CopyCreateNewAndVerifyAsync(
                ResolvePath(stagedRoot, nativeRelative),
                ResolvePath(incoming, nativeRelative),
                artifact,
                cancellationToken).ConfigureAwait(false);
        }
        Directory.Move(incoming, destination);
    }

    async ValueTask<DatabaseArtifactDigest[]> DescribeArtifactsAsync(
        string sourceRoot,
        string artifactPrefix,
        CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Take(_options.Limits.MaximumArtifactCount + 1)
            .ToArray();
        if (files.Length == 0) throw new InvalidDataException("The native backup artifact set is empty.");
        if (files.Length > _options.Limits.MaximumArtifactCount)
            throw new InvalidDataException("The native backup artifact set exceeds its configured file-count limit.");
        var result = new DatabaseArtifactDigest[files.Length];
        for (var index = 0; index < files.Length; index++)
        {
            var nativeRelative = Path.GetRelativePath(sourceRoot, files[index]).Replace('\\', '/');
            var digest = await CalculateChecksumAsync(sourceRoot, nativeRelative, cancellationToken)
                .ConfigureAwait(false);
            result[index] = digest with { RelativePath = $"{artifactPrefix}/{nativeRelative}" };
        }
        return result;
    }

    async ValueTask<DatabaseBackupPublicationResult?> TryResolveExistingPublicationAsync(
        DatabaseBackupPublicationRequest request,
        DatabaseRestorePointId restorePointId,
        string manifestId,
        DatabaseArtifactReplicaId[] replicas,
        string sourceRoot,
        CancellationToken cancellationToken)
    {
        var descriptors = new List<DatabaseArtifactReplicaDescriptor>(replicas.Length);
        var missing = new List<DatabaseArtifactReplicaId>();
        DatabaseBackupManifest? canonical = null;
        foreach (var replica in replicas)
        {
            DatabaseCatalogRestorePoint existing;
            try
            {
                existing = await ResolveAsync(restorePointId, replica, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                missing.Add(replica);
                continue;
            }
            var manifest = existing.Manifest;
            if (manifest.ManifestId != manifestId
                || manifest.OperationId != request.OperationId
                || manifest.Engine != request.Engine
                || manifest.ProtectionSetId != request.ProtectionSetId
                || !string.Equals(manifest.SafeBoundaryReference, request.SafeBoundaryReference, StringComparison.Ordinal)
                || !manifest.Replicas.SequenceEqual(replicas)
                || !manifest.Dependencies.SequenceEqual(request.Dependencies ?? [])
                || manifest.BackupLineage != (request.BackupLineage?.NormalizeLegacyFull(request.Engine)
                    ?? manifest.BackupLineage))
                throw new InvalidDataException("An existing immutable publication conflicts with this operation.");
            if (canonical is not null
                && !LocalBackupJson.Serialize(canonical).AsSpan().SequenceEqual(LocalBackupJson.Serialize(manifest)))
                throw new InvalidDataException("Required replicas contain conflicting signed manifests.");
            canonical = manifest;
            descriptors.Add(new DatabaseArtifactReplicaDescriptor
            {
                ArtifactId = new DatabaseArtifactId($"artifact-{request.OperationId.Value:N}"),
                ReplicaId = replica,
                Engine = request.Engine,
                State = DatabaseArtifactReplicaState.Published,
                SafeDestinationReference = $"{replica.Value}:{restorePointId.Value}",
                Bytes = existing.VerifiedBytes
            });
        }
        if (canonical is null) return null;
        foreach (var replica in missing)
        {
            var published = await PublishReplicaAsync(
                new DatabaseReplicaPublicationRequest(canonical, request.OperationId, replica),
                sourceRoot,
                cancellationToken).ConfigureAwait(false);
            descriptors.Add(published.Replica);
        }
        return new DatabaseBackupPublicationResult(
            restorePointId, manifestId, canonical.Revision,
            [.. descriptors.OrderBy(descriptor => Array.IndexOf(replicas, descriptor.ReplicaId))]);
    }

    async ValueTask CopyCreateNewAndVerifyAsync(
        string source,
        string destination,
        DatabaseArtifactDigest expected,
        CancellationToken cancellationToken)
    {
        LocalBackupPathPolicy.RejectLink(new FileInfo(source));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            128 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
        }
        var destinationRoot = FindContainingRoot(destination);
        var relative = Path.GetRelativePath(destinationRoot, destination).Replace('\\', '/');
        var actual = await CalculateChecksumAsync(destinationRoot, relative, cancellationToken).ConfigureAwait(false);
        if (actual.Length != expected.Length
            || !string.Equals(actual.Sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("A copied backup artifact failed durable read-back verification.");
    }

    string FindContainingRoot(string path)
    {
        var roots = new[]
        {
            _options.OnlineVault.ResolveRoot(),
            _options.OfflineMedia.Enabled ? _options.OfflineMedia.ResolveRoot() : null,
            _options.RestoreWorkspace.ResolveRoot(),
            _postgreSql.ResolveBackupRoot(),
            _scylla.ResolveBackupRoot()
        }.Where(static value => value is not null).Cast<string>()
            .Where(root => DatabaseBackupPublicationOptions.IsWithin(path, root))
            .OrderByDescending(static root => root.Length)
            .FirstOrDefault();
        return roots ?? throw new InvalidOperationException("A copy destination is outside all approved roots.");
    }

    DatabaseArtifactReplicaId[] ResolveRequestedReplicas(DatabaseLogicalDestination[] destinations)
    {
        if (destinations.Length == 0) throw new InvalidOperationException("At least one destination is required.");
        var online = new DatabaseArtifactReplicaId(_options.OnlineVault.ReplicaId);
        var offline = new DatabaseArtifactReplicaId(_options.OfflineMedia.ReplicaId);
        var replicas = new List<DatabaseArtifactReplicaId>();
        foreach (var destination in destinations)
        {
            var replica = new DatabaseArtifactReplicaId(destination.Name);
            if (replica == online) replicas.Add(replica);
            else if (_options.OfflineMedia.Enabled && replica == offline) replicas.Add(replica);
            else if (destination.Required)
                throw new InvalidOperationException($"Required local destination '{destination.Name}' is unavailable.");
        }
        if (!replicas.Contains(online))
            throw new InvalidOperationException("The online vault is required for local publication.");
        return [.. replicas.Distinct()];
    }

    DatabaseArtifactReplicaId[] CandidateReplicas()
    {
        var values = new List<DatabaseArtifactReplicaId>
        {
            new(_options.OnlineVault.ReplicaId)
        };
        if (_options.OfflineMedia.Enabled) values.Add(new(_options.OfflineMedia.ReplicaId));
        return [.. values];
    }

    static DatabaseApprovedStorageRoot Approve(string fullPath)
        => new("framework-internal", Path.GetFullPath(fullPath));

    string ResolvePath(string approvedRoot, string relativePath)
        => _paths.Resolve(Approve(approvedRoot), relativePath);

    void ValidateTree(string approvedRoot)
        => _paths.ValidateTree(Approve(approvedRoot));

    ValueTask<DatabaseArtifactDigest> CalculateChecksumAsync(
        string approvedRoot,
        string relativePath,
        CancellationToken cancellationToken)
        => _checksums.CalculateAsync(Approve(approvedRoot), relativePath, cancellationToken);

    void EnsureCapacity(string approvedRoot, long requiredBytes, long reserveBytes)
        => _capacity.EnsureCapacity(Approve(approvedRoot), requiredBytes, reserveBytes);

    string RootForReplica(DatabaseArtifactReplicaId replicaId)
    {
        if (replicaId == new DatabaseArtifactReplicaId(_options.OnlineVault.ReplicaId))
            return _options.OnlineVault.ResolveRoot();
        if (_options.OfflineMedia.Enabled
            && replicaId == new DatabaseArtifactReplicaId(_options.OfflineMedia.ReplicaId))
            return _options.OfflineMedia.ResolveRoot();
        throw new InvalidOperationException("The requested local backup replica is not configured.");
    }

    string NativeRestorePointRoot(DatabaseEngine engine, string restorePointId)
        => ResolvePath(engine switch
        {
            DatabaseEngine.PostgreSql => _postgreSql.ResolveBackupRoot(),
            DatabaseEngine.ScyllaDb => _scylla.ResolveBackupRoot(),
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        }, restorePointId);

    string EnvironmentPrefix()
        => $"vault/schema-v1/environments/{_options.EnvironmentId}";

    string OperationPrefix(DatabaseProtectionSetId protectionSetId, DatabaseRecoveryOperationId operationId)
        => $"{EnvironmentPrefix()}/protection-sets/{protectionSetId.Value}/operations/{operationId.Format()}";

    string ArtifactPrefix(
        DatabaseProtectionSetId protectionSetId,
        DatabaseRecoveryOperationId operationId,
        DatabaseEngine engine,
        string manifestId)
        => $"{OperationPrefix(protectionSetId, operationId)}/engines/{EngineName(engine)}/artifacts/{manifestId}";

    string ManifestRelativePath(DatabaseBackupManifest manifest)
        => $"{OperationPrefix(manifest.ProtectionSetId, manifest.OperationId)}/engines/{EngineName(manifest.Engine)}/ifm-engine-manifest/{manifest.ManifestId}.json";

    string CommitRelativePath(DatabaseBackupManifest manifest)
        => $"{OperationPrefix(manifest.ProtectionSetId, manifest.OperationId)}/publication/{manifest.ManifestId}/commit.json";

    string CatalogRoot() => $"{EnvironmentPrefix()}/catalog/entries";

    string CatalogEntryRelativePath(DatabaseBackupManifest manifest, DatabaseArtifactReplicaId replicaId)
        => $"{CatalogRoot()}/{manifest.CreatedUtc:yyyyMMdd}/{manifest.RestorePointId.Value}/{manifest.ManifestId}-{replicaId.Value}.json";

    string MediaSealRelativePath(string manifestId)
        => $"{EnvironmentPrefix()}/media-seals/{manifestId}/1.json";

    static string EngineName(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.PostgreSql => "postgresql",
        DatabaseEngine.ScyllaDb => "scylla",
        _ => throw new ArgumentOutOfRangeException(nameof(engine))
    };

    static void Validate(DatabaseBackupPublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId.Value == Guid.Empty)
            throw new ArgumentException("A publication operation identity is required.", nameof(request));
        DatabaseBackupEnumValidation.RequireDefined(request.Engine, nameof(request.Engine));
        if (string.IsNullOrWhiteSpace(request.SafeBoundaryReference)
            || request.SafeBoundaryReference.Any(char.IsControl))
            throw new ArgumentException("A bounded native consistency reference is required.", nameof(request));
    }

    const string EnrollmentRelativePath = "vault/enrollment/media.json";
    string TrustBundleRelativePath => $"vault/enrollment/trust/{_signatures.KeyId}.public.pem";

    sealed record LocalPublicationCommit(
        int SchemaVersion,
        DatabaseRestorePointId RestorePointId,
        string ManifestId,
        long ManifestRevision,
        string ManifestRelativePath,
        string ManifestSha256,
        DatabaseArtifactReplicaId ReplicaId,
        DateTimeOffset PublishedUtc);

    sealed record LocalMediaSeal(
        int SchemaVersion,
        string MediaId,
        string RotationSlot,
        long SealRevision,
        DatabaseRestorePointId RestorePointId,
        string ManifestId,
        long Bytes,
        int FileCount,
        bool DependencyComplete,
        bool VerificationSucceeded,
        DateTimeOffset VerifiedUtc);
}
