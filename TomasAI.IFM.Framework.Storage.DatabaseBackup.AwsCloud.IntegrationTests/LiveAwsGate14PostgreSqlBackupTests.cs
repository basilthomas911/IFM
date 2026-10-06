using System.Diagnostics;
using System.Text.Json;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Amazon.SecurityToken;
using FluentAssertions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed partial class LiveAwsPublicationAndSigningIntegrationTests
{
    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    [Trait("Category", "Gate14RealBackup")]
    public async Task Dedicated_non_evidence_PostgreSql_backups_restore_from_both_vaults()
    {
        if (Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS") != "1"
            || Environment.GetEnvironmentVariable("IFM_GATE14_BACKUP_LIVE_TESTS") != "1")
            return;
        var evidenceRoot = Environment.GetEnvironmentVariable("IFM_GATE14_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("Persistent evidence directory required.");
        var run = Guid.NewGuid().ToString("N");
        var root = Path.Combine(evidenceRoot, run);
        Directory.CreateDirectory(root);
        output.WriteLine("Gate14EvidenceDirectory={0}", root);
        var source = "ifm-gate14-pg-source-" + run;
        var containers = new List<string>();
        var observations = new List<object>();
        var options = LiveOptions();
        using var s3 = new AmazonS3Client(RegionEndpoint.CACentral1);
        using var kms = new AmazonKeyManagementServiceClient(RegionEndpoint.CACentral1);
        var store = new S3ImmutableObjectStore(s3, options, TimeProvider.System);
        var signer = new KmsDocumentSignatureService(kms, options, TimeProvider.System);
        var catalog = new S3DatabaseBackupCatalog(s3, store, signer, options);
        var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials(new AmazonSecurityTokenServiceConfig { RegionEndpoint = RegionEndpoint.CACentral1 });
        using var recoveryCredentials = new AssumeRoleAWSCredentials(credentials, options.RecoveryReadRoleArn,
            "gate14-" + run, new AssumeRoleAWSCredentialsOptions { ExternalId = "ifm-database-backup-development" });
        using var recoveryS3 = new AmazonS3Client(recoveryCredentials, RegionEndpoint.CAWest1);
        using var recoveryVault = new AwsRecoveryVaultClient(recoveryS3);
        try
        {
            await Gate14DockerAsync("run", "-d", "--name", source, "--label", "ifm.qualification=gate14-non-evidence",
                "-e", "POSTGRES_HOST_AUTH_METHOD=trust", "postgres:17.2");
            containers.Add(source);
            await Gate14WaitAsync(source);
            await Gate14DockerAsync("exec", source, "psql", "-U", "postgres", "-v", "ON_ERROR_STOP=1", "-c",
                "CREATE TABLE gate14_orders (id integer PRIMARY KEY, amount numeric NOT NULL); INSERT INTO gate14_orders SELECT i, i*1.25 FROM generate_series(1,100) i;");
            foreach (var role in new[] { "candidate", "keep" })
            {
                if (role == "keep") await Gate14DockerAsync("exec", source, "psql", "-U", "postgres", "-c", "INSERT INTO gate14_orders VALUES (101,126.25);");
                var local = Path.Combine(root, role);
                Directory.CreateDirectory(local);
                await Gate14DockerAsync("exec", "-u", "postgres", source, "pg_basebackup", "-h", "127.0.0.1", "-U", "postgres",
                    "-D", "/tmp/gate14-" + role, "-Ft", "-X", "stream", "--checkpoint=fast");
                await Gate14DockerAsync("cp", source + ":/tmp/gate14-" + role + "/.", local);
                var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
                var point = new DatabaseRestorePointId(operation.Format());
                var publisher = new S3DatabaseBackupPublicationCapability(new Gate14DirectorySource(local), store, signer, options,
                    new DatabaseBackupHostOptions { HostId = "gate14-non-evidence-" + run }, TimeProvider.System);
                var published = await publisher.PublishAsync(new DatabaseBackupPublicationRequest(operation,
                    new DatabaseProtectionSetId("postgresql-gate14-disposable-" + run), DatabaseEngine.PostgreSql,
                    "gate14-non-evidence-" + role,
                    [new DatabaseLogicalDestination("aws-primary", true), new DatabaseLogicalDestination("aws-recovery", true)],
                    Dependencies: [], BackupLineage: new DatabaseBackupLineage
                    { RequestedMode = DatabaseBackupMode.Full, ResolvedMode = DatabaseBackupMode.Full,
                      NativeKind = DatabaseNativeBackupKind.PostgreSqlBase, BaseRestorePointId = point,
                      NativeIdentity = "gate14-isolated-postgresql-17.2-" + run, ChainDepth = 0 }), CancellationToken.None);
                observations.Add(new { role, operationId = operation.Format(), restorePointId = point.Value, publication = published });
                await SaveAsync("progress.json");
                foreach (var replica in new[] { "aws-primary", "aws-recovery" })
                {
                    var sink = new InMemoryRestoreArtifactSink();
                    var restore = new S3DatabaseRestoreSourceCapability(s3, catalog, sink,
                        new AwsPostgreSqlWalArchive(s3, store, signer, options, TimeProvider.System), options, recoveryVault, signer, TimeProvider.System);
                    var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
                    while (true)
                    {
                        try
                        {
                            await restore.PrepareAsync(new DatabaseRestoreSourceRequest(new DatabaseRecoveryOperationId(Guid.NewGuid()),
                                point, DatabaseEngine.PostgreSql, new DatabaseArtifactReplicaId(replica)), CancellationToken.None);
                            break;
                        }
                        catch (AmazonServiceException e) when (e.ErrorCode is "AccessDenied" or "AccessDeniedException") { throw; }
                        catch (Exception e) when (DateTimeOffset.UtcNow < deadline && (e is FileNotFoundException or InvalidDataException or AmazonS3Exception))
                        { sink.Artifacts.Clear(); await Task.Delay(5000); }
                    }
                    var stage = Path.Combine(root, role + "-" + replica);
                    Directory.CreateDirectory(stage);
                    foreach (var artifact in sink.Artifacts[point])
                        await File.WriteAllBytesAsync(Path.Combine(stage, Path.GetFileName(artifact.Key)), artifact.Value);
                    var target = "ifm-gate14-pg-" + role + "-" + replica + "-" + run;
                    await Gate14DockerAsync("run", "-d", "--name", target, "--network", "none", "--label", "ifm.qualification=gate14-restore",
                        "--entrypoint", "sleep", "postgres:17.2", "infinity");
                    containers.Add(target);
                    await Gate14DockerAsync("cp", stage + "/.", target + ":/tmp/");
                    await Gate14DockerAsync("exec", target, "bash", "-c",
                        "cd /var/lib/postgresql/data && tar xf /tmp/base.tar && tar xf /tmp/pg_wal.tar -C pg_wal && chown -R postgres:postgres . && chmod 700 .");
                    await Gate14DockerAsync("exec", "-u", "postgres", target, "pg_ctl", "-D", "/var/lib/postgresql/data", "-l", "/tmp/restore.log", "-w", "start");
                    var rows = await Gate14DockerAsync("exec", target, "psql", "-U", "postgres", "-At", "-c", "SELECT count(*) || ':' || sum(amount) FROM gate14_orders;");
                    rows.Trim().Should().Be(role == "candidate" ? "100:6312.50" : "101:6438.75");
                    observations.Add(new { role, replica, restorePointId = point.Value, independentlyRestored = true, rows = rows.Trim() });
                    await SaveAsync("progress.json");
                }
            }
            await SaveAsync("completed.json");
        }
        finally
        {
            foreach (var container in containers) await Gate14DockerAsync("rm", "-f", "-v", container);
        }
        Task SaveAsync(string file) => File.WriteAllTextAsync(Path.Combine(root, file), JsonSerializer.Serialize(new
        { run, purpose = "Non-evidence retention-drill database backups", deletionAuthorized = false, observations }, new JsonSerializerOptions { WriteIndented = true }));
    }
    static async Task Gate14WaitAsync(string container)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try { await Gate14DockerAsync("exec", container, "pg_isready", "-U", "postgres"); return; }
            catch (InvalidOperationException) { await Task.Delay(1000); }
        }
        throw new TimeoutException("Isolated PostgreSQL did not become ready.");
    }
    static async Task<string> Gate14DockerAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker could not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException("Docker failed: " + await stderr);
        return await stdout;
    }
    sealed class Gate14DirectorySource(string root) : IDatabaseNativeArtifactSource
    {
        public ValueTask<IReadOnlyList<DatabaseNativeArtifactDescriptor>> DescribeAsync(DatabaseEngine engine, DatabaseRecoveryOperationId operationId, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<DatabaseNativeArtifactDescriptor>>(Directory.GetFiles(root).Select(path => new DatabaseNativeArtifactDescriptor(Path.GetFileName(path), new FileInfo(path).Length)).ToArray());
        public ValueTask<Stream> OpenReadAsync(DatabaseEngine engine, DatabaseRecoveryOperationId operationId, string requestedRelativePath, CancellationToken cancellationToken)
        {
            if (Path.GetFileName(requestedRelativePath) != requestedRelativePath) throw new InvalidDataException("Invalid artifact name.");
            return ValueTask.FromResult<Stream>(File.OpenRead(Path.Combine(root, requestedRelativePath)));
        }
    }
}
