using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Api.DatabaseBackup.Host.Services;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.IntegrationTests;

/// <summary>Verifies bounded retained output and safe source-specific setup metadata.</summary>
public sealed class DatabaseBackupOutputTests
{
    [Fact]
    public async Task Output_is_redacted_paged_and_isolated_by_source_and_operation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-backup-output-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseBackup:OutputRoot"] = root,
                ["DatabaseBackup:Sources:LocalWorkstation:Enabled"] = "true",
                ["DatabaseBackup:PostgreSql:Password"] = "hidden-setup-secret"
            }).Build();
            var writer = new DatabaseBackupOperationOutput(configuration, NullLogger<DatabaseBackupOperationOutput>.Instance);
            var operationId = Guid.NewGuid();
            writer.Append(BackupSource.LocalWorkstation, operationId, "password=hidden-runtime-secret phase=Verifying");
            writer.Append(BackupSource.LocalWorkstation, operationId, new string('x', 32000));
            writer.Append(BackupSource.LocalWorkstation, operationId, new string('y', 32000));
            writer.Append(BackupSource.LocalWorkstation, operationId, new string('z', 32000));
            writer.PublishSetup();
            var reader = new DatabaseBackupOutputReader(root);
            var first = await reader.ReadAsync(BackupSource.LocalWorkstation, operationId, 0, CancellationToken.None);
            Assert.True(first.OutputAvailable);
            Assert.Contains("[REDACTED]", first.Output);
            Assert.DoesNotContain("hidden-runtime-secret", first.Output);
            Assert.False(first.EndOfOutput);
            var second = await reader.ReadAsync(BackupSource.LocalWorkstation, operationId, first.NextOutputOffset, CancellationToken.None);
            Assert.True(second.EndOfOutput);
            Assert.True(second.NextOutputOffset > first.NextOutputOffset);
            Assert.False((await reader.ReadAsync(BackupSource.AwsCloud, operationId, 0, CancellationToken.None)).OutputAvailable);
            Assert.False((await reader.ReadAsync(BackupSource.LocalWorkstation, Guid.NewGuid(), 0, CancellationToken.None)).OutputAvailable);
            var setup = await reader.ReadSetupAsync(BackupSource.LocalWorkstation, CancellationToken.None);
            Assert.True(setup.Available);
            Assert.Equal("true", setup.BackupHostSettings["Enabled"]);
            Assert.DoesNotContain("hidden-setup-secret", string.Join(";", setup.BackupHostSettings.Values));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
