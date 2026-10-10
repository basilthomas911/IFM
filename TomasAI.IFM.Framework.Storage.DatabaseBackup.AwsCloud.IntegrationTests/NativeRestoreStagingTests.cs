using FluentAssertions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class NativeRestoreStagingTests
{
    [Fact]
    public async Task PostgreSql_file_only_staging_preserves_empty_native_directories_and_rejects_reuse()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-restore-layout-" + Guid.NewGuid().ToString("N"));
        var point = new DatabaseRestorePointId(Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new LocalDatabaseNativeRestoreArtifactSink(new PostgreSqlBackupOptions { BackupRoot = root }, new ScyllaBackupOptions());
            await sink.PrepareFreshAsync(DatabaseEngine.PostgreSql, point, CancellationToken.None);
            string[] required = ["pg_notify", "pg_dynshmem", "pg_replslot", "pg_serial", "pg_snapshots", "pg_stat_tmp", "pg_subtrans", "pg_twophase", "pg_logical/mappings", "pg_logical/snapshots", "pg_multixact/members", "pg_multixact/offsets", "pg_wal/archive_status"];
            foreach (var directory in required)
                Directory.Exists(Path.Combine(root, point.Value, "data", directory)).Should().BeTrue(directory);
            var reuse = async () => await sink.PrepareFreshAsync(DatabaseEngine.PostgreSql, point, CancellationToken.None);
            await reuse.Should().ThrowAsync<InvalidOperationException>();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
