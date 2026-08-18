using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class LocalBackupChainPlannerTests
{
    static readonly DatabaseArtifactReplicaId Online = new("online-vault");
    static readonly DatabaseArtifactReplicaId Offline = new("offline-media-a");

    [Fact]
    public async Task Automatic_without_a_parent_resolves_to_full()
    {
        var catalog = Substitute.For<IDatabaseBackupCatalog>();
        catalog.EnumerateAsync(Online, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([]));
        var planner = CreatePlanner(catalog);

        var result = await planner.PlanAsync(Request(DatabaseBackupMode.Automatic), CancellationToken.None);

        result.RequestedMode.Should().Be(DatabaseBackupMode.Automatic);
        result.ResolvedMode.Should().Be(DatabaseBackupMode.Full);
        result.NativeKind.Should().Be(DatabaseNativeBackupKind.PostgreSqlBase);
        result.ParentRestorePointId.Should().BeNull();
        result.ChainDepth.Should().Be(0);
    }

    [Fact]
    public async Task Explicit_incremental_without_a_parent_is_rejected()
    {
        var catalog = Substitute.For<IDatabaseBackupCatalog>();
        catalog.EnumerateAsync(Online, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([]));
        var planner = CreatePlanner(catalog);

        var action = async () => await planner.PlanAsync(
            Request(DatabaseBackupMode.Incremental), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*parent*");
    }

    [Fact]
    public async Task Common_verified_parent_on_every_replica_creates_incremental_lineage()
    {
        var parent = Point(DatabaseEngine.PostgreSql, depth: 0);
        var catalog = Substitute.For<IDatabaseBackupCatalog>();
        catalog.EnumerateAsync(Online, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([parent]));
        catalog.EnumerateAsync(Offline, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([parent]));
        var planner = CreatePlanner(catalog);

        var result = await planner.PlanAsync(
            Request(DatabaseBackupMode.Automatic, includeOffline: true), CancellationToken.None);

        result.ResolvedMode.Should().Be(DatabaseBackupMode.Incremental);
        result.NativeKind.Should().Be(DatabaseNativeBackupKind.PostgreSqlIncremental);
        result.BaseRestorePointId.Should().Be(parent.Entry.RestorePointId);
        result.ParentRestorePointId.Should().Be(parent.Entry.RestorePointId);
        result.ChainDepth.Should().Be(1);
        result.NativeIdentity.Should().Be("postgres-system-1");
    }

    [Fact]
    public async Task Parent_missing_from_one_required_replica_forces_automatic_full()
    {
        var parent = Point(DatabaseEngine.PostgreSql, depth: 0);
        var catalog = Substitute.For<IDatabaseBackupCatalog>();
        catalog.EnumerateAsync(Online, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([parent]));
        catalog.EnumerateAsync(Offline, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([]));
        var planner = CreatePlanner(catalog);

        var result = await planner.PlanAsync(
            Request(DatabaseBackupMode.Automatic, includeOffline: true), CancellationToken.None);

        result.ResolvedMode.Should().Be(DatabaseBackupMode.Full);
    }

    [Fact]
    public async Task Parent_with_different_signed_content_across_required_replicas_forces_automatic_full()
    {
        var parent = Point(DatabaseEngine.PostgreSql, depth: 0);
        var inconsistent = parent with
        {
            Manifest = parent.Manifest with { SafeBoundaryReference = "different-boundary" }
        };
        var catalog = Substitute.For<IDatabaseBackupCatalog>();
        catalog.EnumerateAsync(Online, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([parent]));
        catalog.EnumerateAsync(Offline, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([inconsistent]));
        var planner = CreatePlanner(catalog);

        var result = await planner.PlanAsync(
            Request(DatabaseBackupMode.Automatic, includeOffline: true), CancellationToken.None);

        result.ResolvedMode.Should().Be(DatabaseBackupMode.Full);
    }

    [Fact]
    public async Task Maximum_chain_depth_forces_a_new_automatic_full()
    {
        var parent = Point(DatabaseEngine.PostgreSql, depth: 2);
        var catalog = Substitute.For<IDatabaseBackupCatalog>();
        catalog.EnumerateAsync(Online, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([parent]));
        var planner = CreatePlanner(catalog, maximumDepth: 2);

        var result = await planner.PlanAsync(Request(DatabaseBackupMode.Automatic), CancellationToken.None);

        result.ResolvedMode.Should().Be(DatabaseBackupMode.Full);
        result.NativeKind.Should().Be(DatabaseNativeBackupKind.PostgreSqlBase);
        result.ParentRestorePointId.Should().BeNull();
    }

    [Fact]
    public async Task Expired_base_forces_a_new_automatic_full()
    {
        var parent = Point(DatabaseEngine.PostgreSql, depth: 0);
        parent = parent with
        {
            Manifest = parent.Manifest with { CreatedUtc = DateTimeOffset.UtcNow.AddDays(-8) }
        };
        var catalog = Substitute.For<IDatabaseBackupCatalog>();
        catalog.EnumerateAsync(Online, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([parent]));
        var planner = CreatePlanner(catalog);

        var result = await planner.PlanAsync(Request(DatabaseBackupMode.Automatic), CancellationToken.None);

        result.ResolvedMode.Should().Be(DatabaseBackupMode.Full);
    }

    [Fact]
    public async Task Scylla_incremental_is_recorded_as_a_deduplicated_manager_snapshot()
    {
        var parent = Point(DatabaseEngine.ScyllaDb, depth: 0);
        var catalog = Substitute.For<IDatabaseBackupCatalog>();
        catalog.EnumerateAsync(Online, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<DatabaseCatalogRestorePoint>>([parent]));
        var planner = CreatePlanner(catalog);

        var request = Request(DatabaseBackupMode.Incremental) with { Engine = DatabaseEngine.ScyllaDb };
        var result = await planner.PlanAsync(request, CancellationToken.None);

        result.ResolvedMode.Should().Be(DatabaseBackupMode.Incremental);
        result.NativeKind.Should().Be(DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot);
    }

    static LocalBackupChainPlanner CreatePlanner(IDatabaseBackupCatalog catalog, int maximumDepth = 6)
        => new(catalog, new LocalWorkstationSourceOptions
        {
            IncrementalEnabled = true,
            MaximumIncrementalChainDepth = maximumDepth,
            MaximumIncrementalBaseAge = TimeSpan.FromDays(7)
        });

    static DatabaseBackupPlanningRequest Request(DatabaseBackupMode mode, bool includeOffline = false)
        => new(
            new DatabaseRecoveryOperationId(Guid.NewGuid()),
            new DatabaseProtectionSetId("core-postgresql"),
            DatabaseEngine.PostgreSql,
            mode,
            includeOffline
                ? [new(Online.Value, true), new(Offline.Value, true)]
                : [new(Online.Value, true)]);

    static DatabaseCatalogRestorePoint Point(DatabaseEngine engine, int depth)
    {
        var id = new DatabaseRestorePointId(Guid.NewGuid().ToString("N"));
        var nativeKind = engine == DatabaseEngine.PostgreSql
            ? (depth == 0 ? DatabaseNativeBackupKind.PostgreSqlBase : DatabaseNativeBackupKind.PostgreSqlIncremental)
            : (depth == 0 ? DatabaseNativeBackupKind.ScyllaManagerSnapshot : DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot);
        var manifest = new DatabaseBackupManifest
        {
            ManifestId = "manifest-" + id.Value,
            OperationId = new DatabaseRecoveryOperationId(Guid.NewGuid()),
            RestorePointId = id,
            Engine = engine,
            ProtectionSetId = new DatabaseProtectionSetId(engine == DatabaseEngine.PostgreSql
                ? "core-postgresql"
                : "core-postgresql"),
            SafeBoundaryReference = "boundary",
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            Artifacts = [new("artifact", 1, new string('a', 64))],
            Replicas = [Online],
            BackupLineage = new DatabaseBackupLineage
            {
                RequestedMode = depth == 0 ? DatabaseBackupMode.Full : DatabaseBackupMode.Incremental,
                ResolvedMode = depth == 0 ? DatabaseBackupMode.Full : DatabaseBackupMode.Incremental,
                NativeKind = nativeKind,
                BaseRestorePointId = id,
                ParentRestorePointId = depth == 0 ? null : id,
                ChainDepth = depth,
                NativeIdentity = engine == DatabaseEngine.PostgreSql ? "postgres-system-1" : "scylla-cluster-1"
            }
        };
        var entry = new DatabaseCatalogEntry(
            1, id, manifest.ManifestId, 1, engine, manifest.ProtectionSetId,
            Online, "manifest", "commit", manifest.CreatedUtc);
        return new DatabaseCatalogRestorePoint(entry, manifest, 1, 1);
    }
}
