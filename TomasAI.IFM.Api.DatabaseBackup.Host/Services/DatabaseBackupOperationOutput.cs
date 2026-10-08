using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;
/// <summary>Retains operation output under a host-controlled root; output failure does not change backup success.</summary>
public sealed class DatabaseBackupOperationOutput(IConfiguration configuration, ILogger<DatabaseBackupOperationOutput> logger)
{
    readonly object _gate = new();
    /// <summary>Publishes only explicit safe configuration references for the two source setup tabs.</summary>
    public void PublishSetup()
    {
        var common = new[] { "Host:HostId", "EnvironmentId", "OnlineVault:Root", "OfflineMedia:Enabled", "OfflineMedia:Root", "RestoreWorkspace:Root", "PostgreSql:ToolDirectory", "PostgreSql:ConnectionStringEnvironmentVariable", "Scylla:ManagerApiUrl", "Scylla:ToolDirectory" };
        foreach (var source in new[] { BackupSource.LocalWorkstation, BackupSource.AwsCloud })
        {
            var fields = source == BackupSource.LocalWorkstation
                ? new[] { "Enabled", "DryRun", "PostgreSqlEnabled", "ScyllaEnabled", "IncrementalEnabled", "MaximumIncrementalChainDepth", "MaximumIncrementalBaseAge" }
                : new[] { "Enabled", "AcceptBackupRequests", "Environment", "WorkloadAccountId", "PrimaryVaultAccountId", "RecoveryVaultAccountId", "PrimaryRegion", "RecoveryRegion", "PrimaryBucketName", "RecoveryBucketName", "UploadRoleArn", "RecoveryReadRoleArn", "PrimaryEncryptionKeyArn", "RecoveryEncryptionKeyArn", "SigningKeyArn" };
            var values = common.ToDictionary(name => name, name => configuration["DatabaseBackup:" + name] ?? "Not configured");
            values["PostgreSqlProtectionSets"] = string.Join(",", configuration.GetSection(source == BackupSource.AwsCloud ? "DatabaseBackup:Sources:AwsCloud:PostgreSqlProtectionSets" : "DatabaseBackup:PostgreSql:AllowedProtectionSets").GetChildren().Select(item => item.Value));
            values["ScyllaProtectionSets"] = string.Join(",", configuration.GetSection(source == BackupSource.AwsCloud ? "DatabaseBackup:Sources:AwsCloud:ScyllaProtectionSets" : "DatabaseBackup:Scylla:ProtectionSets").GetChildren().Select(item => source == BackupSource.AwsCloud ? item.Value : item.Key));
            values["PostgreSqlRestoreProfiles"] = string.Join(",", configuration.GetSection("DatabaseBackup:PostgreSql:FreshTargetProfiles").GetChildren().Select(item => item.Key));
            values["ScyllaRestoreProfiles"] = string.Join(",", configuration.GetSection("DatabaseBackup:Scylla:FreshTargetProfiles").GetChildren().Select(item => item.Key));
            foreach (var name in fields) values[name] = configuration[$"DatabaseBackup:Sources:{source}:{name}"] ?? "Not configured";
            try
            {
                var root = configuration["DatabaseBackup:OutputRoot"] ?? Path.Combine(AppContext.BaseDirectory, "backup-output");
                var directory = Path.Combine(root, source.ToString(), "setup"); Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "stdout.log"), System.Text.Json.JsonSerializer.Serialize(new TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels.DatabaseBackupSetupReadModel { Available = true, BackupHostSettings = values }), new UTF8Encoding(false));
            }
            catch (Exception exception) { logger.LogWarning(exception, "Cannot publish safe backup setup metadata for {BackupSource}", source); }
        }
    }
    /// <summary>Appends a timestamped, bounded and redacted output chunk for the identified operation.</summary>
    public void Append(BackupSource source, Guid operationId, string text)
    {
        if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var root = configuration["DatabaseBackup:OutputRoot"] ?? Path.Combine(AppContext.BaseDirectory, "backup-output");
            var directory = Path.Combine(root, source.ToString(), operationId.ToString("N"));
            var safe = Regex.Replace(text, @"(?i)(password|secret|token|access[_-]?key)\s*[:=]\s*[^\s;]+", "$1=[REDACTED]");
            if (safe.Length > 32768) safe = safe[..32768] + "\n[chunk truncated]";
            lock (_gate)
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "stdout.log");
                if (File.Exists(path) && new FileInfo(path).Length >= 16 * 1024 * 1024) return;
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {safe.TrimEnd()}\n", new UTF8Encoding(false));
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "{Component}.{Method} Cannot retain backup output for {BackupSource} {OperationId}", nameof(DatabaseBackupOperationOutput), nameof(Append), source, operationId);
        }
    }
}
