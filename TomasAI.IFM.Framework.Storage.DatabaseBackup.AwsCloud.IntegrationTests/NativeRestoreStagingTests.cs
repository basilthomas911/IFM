using FluentAssertions;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class NativeRestoreStagingTests
{
    [Fact]
    public async Task PostgreSql_staging_preserves_empty_directories_and_verifies_existing_artifacts_without_overwrite()
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
            await sink.PrepareFreshAsync(DatabaseEngine.PostgreSql, point, CancellationToken.None);
            byte[] original = [1, 2, 3, 4];
            var hash = Convert.ToHexString(SHA256.HashData(original));
            await sink.WriteAsync(DatabaseEngine.PostgreSql, point, "data/PG_VERSION", new MemoryStream(original), original.Length, hash, CancellationToken.None);
            await sink.WriteAsync(DatabaseEngine.PostgreSql, point, "data/PG_VERSION", new MemoryStream(original), original.Length, hash, CancellationToken.None);
            var corrupt = async () => await sink.WriteAsync(DatabaseEngine.PostgreSql, point, "data/PG_VERSION", new MemoryStream([5, 6]), 2,
                Convert.ToHexString(SHA256.HashData(new byte[] { 5, 6 })), CancellationToken.None);
            await corrupt.Should().ThrowAsync<InvalidDataException>();
            (await File.ReadAllBytesAsync(Path.Combine(root, point.Value, "data", "PG_VERSION"))).Should().Equal(original);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
