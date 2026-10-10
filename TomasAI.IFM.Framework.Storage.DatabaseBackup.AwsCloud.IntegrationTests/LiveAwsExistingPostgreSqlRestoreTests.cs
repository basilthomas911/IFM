using System.Text.Json;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.S3;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;
namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;
public sealed partial class LiveAwsPublicationAndSigningIntegrationTests
{
    /// <summary>Verifies and boots an existing signed AWS backup in a fresh isolated PostgreSQL target.</summary>
    [Fact]
    [Trait("Category", "LiveAwsExistingRestore")]
    public async Task Existing_running_host_PostgreSql_backup_verifies_and_boots_in_isolated_target()
    {
        if (Environment.GetEnvironmentVariable("IFM_AWS_EXISTING_RESTORE_TESTS") != "1") return;
        var point = new DatabaseRestorePointId(Environment.GetEnvironmentVariable("IFM_AWS_EXISTING_RESTORE_POINT") ?? throw new InvalidOperationException("Explicit restore point required."));
        var evidenceRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("IFM_AWS_EXISTING_RESTORE_ROOT") ?? throw new InvalidOperationException("Fresh evidence root required."));
        var root = Path.Combine(evidenceRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        output.WriteLine("ExistingRestoreEvidenceDirectory={0}", root);
        using var timeout = new CancellationTokenSource(TimeSpan.FromHours(2));
        var options = LiveOptions();
        options.MaximumSignedDocumentBytes = 16 * 1024 * 1024;
        using var s3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var kms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System);
        var signer = new KmsDocumentSignatureService(kms, options, TimeProvider.System);
        var catalog = new S3DatabaseBackupCatalog(s3, store, signer, options);
        var nativeRoot = Path.Combine(root, "native");
        var sink = new ExistingRestoreProgressSink(new LocalDatabaseNativeRestoreArtifactSink(new PostgreSqlBackupOptions { BackupRoot = nativeRoot }, new ScyllaBackupOptions()), root);
        using var recoveryS3 = new AmazonS3Client(RegionEndpoint.CAWest1);
        using var recovery = new AwsRecoveryVaultClient(recoveryS3);
        var source = new S3DatabaseRestoreSourceCapability(s3, catalog, sink, new AwsPostgreSqlWalArchive(s3, store, signer, options, TimeProvider.System), options, recovery, signer, TimeProvider.System);
        var previouslyVerifiedRoot = Environment.GetEnvironmentVariable("IFM_AWS_EXISTING_RESTORE_VERIFIED_ROOT");
        long verifiedBytes;
        int verifiedArtifactCount;
        string stage;
        if (string.IsNullOrWhiteSpace(previouslyVerifiedRoot))
        {
            var prepared = await source.PrepareAsync(new DatabaseRestoreSourceRequest(new DatabaseRecoveryOperationId(Guid.NewGuid()), point, DatabaseEngine.PostgreSql, new DatabaseArtifactReplicaId("aws-primary")), timeout.Token);
            prepared.NativeRestorePointId.Should().Be(point);
            verifiedBytes = prepared.VerifiedBytes;
            verifiedArtifactCount = prepared.VerifiedArtifactCount;
            stage = Path.Combine(nativeRoot, point.Value);
        }
        else
        {
            // Explicit diagnostic rerun: retain prior AWS verification evidence and reverify every native file.
            (await File.ReadAllTextAsync(Path.Combine(previouslyVerifiedRoot, "native-verification.log"), timeout.Token)).Trim().Should().Be("backup successfully verified");
            stage = Path.Combine(previouslyVerifiedRoot, "native", point.Value);
            var files = Directory.GetFiles(stage, "*", SearchOption.AllDirectories);
            verifiedArtifactCount = files.Length;
            verifiedBytes = files.Sum(file => new FileInfo(file).Length);
            var layout = new LocalDatabaseNativeRestoreArtifactSink(new PostgreSqlBackupOptions { BackupRoot = nativeRoot }, new ScyllaBackupOptions());
            await layout.PrepareFreshAsync(DatabaseEngine.PostgreSql, point, timeout.Token);
            foreach (var directory in Directory.GetDirectories(Path.Combine(nativeRoot, point.Value), "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(stage, Path.GetRelativePath(Path.Combine(nativeRoot, point.Value), directory)));
        }
        (await File.ReadAllTextAsync(Path.Combine(stage, "data", "PG_VERSION"), timeout.Token)).Trim().Should().Be("17");
        var target = "ifm-aws-existing-restore-" + Guid.NewGuid().ToString("N");
        var started = false;
        try
        {
            await Gate14DockerAsync("run", "-d", "--rm", "--name", target, "--network", "none", "--label", "ifm.qualification=aws-existing-restore", "--mount", "type=bind,source=" + stage + ",target=/stage,readonly", "--entrypoint", "sleep", "postgres:17.2", "infinity");
            started = true;
            var verify = await Gate14DockerAsync("exec", target, "pg_verifybackup", "/stage/data");
            await File.WriteAllTextAsync(Path.Combine(root, "native-verification.log"), verify);
            await Gate14DockerAsync("exec", target, "bash", "-c", "cp -a /stage/data/. /var/lib/postgresql/data/ && chown -R postgres:postgres /var/lib/postgresql/data && chmod 700 /var/lib/postgresql/data");
            await Gate14DockerAsync("exec", "-u", "postgres", target, "pg_ctl", "-D", "/var/lib/postgresql/data", "-l", "/tmp/restore.log", "-o", "-c listen_addresses='' -c archive_mode=off", "-w", "start");
            var databases = await Gate14DockerAsync("exec", target, "psql", "-U", "postgres", "-d", "postgres", "-At", "-c", "SELECT datname FROM pg_database ORDER BY datname;");
            databases.Should().Contain("postgres");
            await File.WriteAllTextAsync(Path.Combine(root, "completed.json"), JsonSerializer.Serialize(new { restorePointId = point.Value, replica = "aws-primary", VerifiedBytes = verifiedBytes, VerifiedArtifactCount = verifiedArtifactCount, priorAwsVerificationEvidenceRoot = previouslyVerifiedRoot, nativeVerifyBackupPassed = true, isolatedBootPassed = true, databaseNames = databases.Split('\n', StringSplitOptions.RemoveEmptyEntries), verifiedUtc = DateTimeOffset.UtcNow, applicationDatabasesModified = false }, new JsonSerializerOptions { WriteIndented = true }), timeout.Token);
        }
        catch
        {
            if (started) try { await File.WriteAllTextAsync(Path.Combine(root, "restore-server.log"), await Gate14DockerAsync("exec", target, "cat", "/tmp/restore.log")); } catch { }
            throw;
        }
        finally { if (started) await Gate14DockerAsync("rm", "-f", "-v", target); }
    }
    sealed class ExistingRestoreProgressSink(IDatabaseNativeRestoreArtifactSink inner, string root) : IDatabaseNativeRestoreArtifactSink
    {
        int count; long bytes;
        public ValueTask PrepareFreshAsync(DatabaseEngine engine, DatabaseRestorePointId point, CancellationToken ct) => inner.PrepareFreshAsync(engine, point, ct);
        public async ValueTask WriteAsync(DatabaseEngine engine, DatabaseRestorePointId point, string relativePath, Stream source, long expectedLength, string expectedSha256, CancellationToken ct)
        {
            await inner.WriteAsync(engine, point, relativePath, source, expectedLength, expectedSha256, ct);
            bytes += expectedLength; count++;
            if (count % 25 == 0) await File.WriteAllTextAsync(Path.Combine(root, "progress.json"), JsonSerializer.Serialize(new { verifiedArtifactCount = count, verifiedBytes = bytes, updatedUtc = DateTimeOffset.UtcNow }), ct);
        }
    }
}
