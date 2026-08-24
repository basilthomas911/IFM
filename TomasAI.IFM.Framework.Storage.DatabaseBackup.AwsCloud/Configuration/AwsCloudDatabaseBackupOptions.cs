using System.Text.RegularExpressions;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

public enum AwsBackupEnvironment
{
    Development = 1,
    Staging = 2,
    Production = 3
}

public sealed class AwsCloudDatabaseBackupOptions
{
    public const string SectionName = "DatabaseBackup:Sources:AwsCloud";

    public bool Enabled { get; set; }
    public bool AcceptBackupRequests { get; set; }
    public bool LiveAwsTestsEnabled { get; set; }
    public bool DestructiveTestsEnabled { get; set; }
    public AwsBackupEnvironment Environment { get; set; } = AwsBackupEnvironment.Development;
    public string WorkloadAccountId { get; set; } = string.Empty;
    public string PrimaryVaultAccountId { get; set; } = string.Empty;
    public string RecoveryVaultAccountId { get; set; } = string.Empty;
    public string PrimaryRegion { get; set; } = "ca-central-1";
    public string RecoveryRegion { get; set; } = "ca-west-1";
    public string PrimaryBucketName { get; set; } = string.Empty;
    public string RecoveryBucketName { get; set; } = string.Empty;
    public string JournalTableName { get; set; } = string.Empty;
    public string IdentityRoleArn { get; set; } = string.Empty;
    public string UploadRoleArn { get; set; } = string.Empty;
    public string RecoveryReadRoleArn { get; set; } = string.Empty;
    public string PrimaryEncryptionKeyArn { get; set; } = string.Empty;
    public string RecoveryEncryptionKeyArn { get; set; } = string.Empty;
    public string SigningKeyArn { get; set; } = string.Empty;
    public string ObjectLockMode { get; set; } = "Governance";
    public int DefaultRetentionDays { get; set; } = 35;
    public int MaximumIncrementalChainDepth { get; set; } = 6;
    public int MaximumBaseAgeDays { get; set; } = 7;
    public string[] PostgreSqlProtectionSets { get; set; } = ["postgresql-core"];
    public string[] ScyllaProtectionSets { get; set; } = ["scylla-core"];
    public long MultipartThresholdBytes { get; set; } = 16L * 1024 * 1024;
    public int MultipartPartSizeBytes { get; set; } = 8 * 1024 * 1024;
    public int MaximumSignedDocumentBytes { get; set; } = 4 * 1024 * 1024;
    public TimeSpan StaleMultipartUploadAge { get; set; } = TimeSpan.FromHours(24);
    public string WalSpoolPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "data", "aws-backup", "wal-spool");
    public long MaximumWalSpoolBytes { get; set; } = 8L * 1024 * 1024 * 1024;
    public TimeSpan MaximumWalArchiveLag { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ApiTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaximumSdkRetries { get; set; } = 2;
    public int MaximumConcurrentOperations { get; set; } = 1;
    public long MinimumStagingFreeBytes { get; set; } = 4L * 1024 * 1024 * 1024;
    public long MaximumScyllaProtectionSetBytes { get; set; } = 512L * 1024 * 1024 * 1024;
    public decimal MonthlyCostBudgetUsd { get; set; } = 100m;
    public bool CloudWatchMetricsEnabled { get; set; } = true;
    public string CloudWatchMetricNamespace { get; set; } = "IFM/DatabaseBackup";
    public TimeSpan CloudWatchExportInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int CloudWatchMetricBufferCapacity { get; set; } = 4096;

    public void Validate()
    {
        if (!Enabled) return;
        if (!Enum.IsDefined(Environment)) throw Invalid("environment is unsupported");
        RequireAccount(WorkloadAccountId, nameof(WorkloadAccountId));
        RequireAccount(PrimaryVaultAccountId, nameof(PrimaryVaultAccountId));
        RequireAccount(RecoveryVaultAccountId, nameof(RecoveryVaultAccountId));
        RequireRegion(PrimaryRegion, nameof(PrimaryRegion));
        RequireRegion(RecoveryRegion, nameof(RecoveryRegion));
        RequireBucket(PrimaryBucketName, nameof(PrimaryBucketName));
        RequireBucket(RecoveryBucketName, nameof(RecoveryBucketName));
        if (PrimaryBucketName.Equals(RecoveryBucketName, StringComparison.Ordinal))
            throw Invalid("primary and recovery buckets must be different");
        if (!Regex.IsMatch(JournalTableName, "^[A-Za-z0-9_.-]{3,255}$", RegexOptions.CultureInvariant))
            throw Invalid("journal table name is invalid");
        RequireArn(UploadRoleArn, "iam", WorkloadAccountId, nameof(UploadRoleArn));
        RequireArn(RecoveryReadRoleArn, "iam", RecoveryVaultAccountId, nameof(RecoveryReadRoleArn));
        if (!string.IsNullOrWhiteSpace(IdentityRoleArn)) RequireArn(IdentityRoleArn, "iam", WorkloadAccountId, nameof(IdentityRoleArn));
        RequireArn(PrimaryEncryptionKeyArn, "kms", PrimaryVaultAccountId, nameof(PrimaryEncryptionKeyArn), PrimaryRegion);
        RequireArn(RecoveryEncryptionKeyArn, "kms", RecoveryVaultAccountId, nameof(RecoveryEncryptionKeyArn), RecoveryRegion);
        RequireArn(SigningKeyArn, "kms", WorkloadAccountId, nameof(SigningKeyArn), PrimaryRegion);
        if (Environment == AwsBackupEnvironment.Production)
        {
            if (WorkloadAccountId == PrimaryVaultAccountId || WorkloadAccountId == RecoveryVaultAccountId
                || PrimaryVaultAccountId == RecoveryVaultAccountId)
                throw Invalid("production workload, primary, and recovery accounts must be distinct");
            if (PrimaryRegion.Equals(RecoveryRegion, StringComparison.OrdinalIgnoreCase))
                throw Invalid("production primary and recovery Regions must be distinct");
            if (!ObjectLockMode.Equals("Compliance", StringComparison.OrdinalIgnoreCase))
                throw Invalid("production Object Lock mode must be Compliance");
        }
        else if (!ObjectLockMode.Equals("Governance", StringComparison.OrdinalIgnoreCase)
            && !ObjectLockMode.Equals("Compliance", StringComparison.OrdinalIgnoreCase))
            throw Invalid("Object Lock mode is invalid");
        if (DefaultRetentionDays <= 0 || MaximumIncrementalChainDepth <= 0 || MaximumBaseAgeDays <= 0)
            throw Invalid("retention and incremental-chain bounds must be positive");
        if (PostgreSqlProtectionSets.Concat(ScyllaProtectionSets).Any(static value => string.IsNullOrWhiteSpace(value))
            || PostgreSqlProtectionSets.Intersect(ScyllaProtectionSets, StringComparer.Ordinal).Any())
            throw Invalid("database protection-set engine mappings are empty or ambiguous");
        if (MultipartPartSizeBytes < 5L * 1024 * 1024 || MultipartPartSizeBytes > 512L * 1024 * 1024
            || MultipartThresholdBytes < MultipartPartSizeBytes || MaximumSignedDocumentBytes is <= 0 or > 16 * 1024 * 1024
            || StaleMultipartUploadAge < TimeSpan.FromHours(1))
            throw Invalid("multipart upload and signed-document bounds are unsafe");
        if (string.IsNullOrWhiteSpace(WalSpoolPath) || !Path.IsPathFullyQualified(WalSpoolPath)
            || MaximumWalSpoolBytes < 64L * 1024 * 1024 || MaximumWalArchiveLag <= TimeSpan.Zero)
            throw Invalid("PostgreSQL WAL spool and lag bounds are unsafe");
        if (ApiTimeout <= TimeSpan.Zero || ApiTimeout > TimeSpan.FromMinutes(5) || MaximumSdkRetries is < 0 or > 8)
            throw Invalid("AWS timeout or retry bounds are invalid");
        if (MaximumConcurrentOperations is < 1 or > 4
            || MinimumStagingFreeBytes < 64L * 1024 * 1024
            || MaximumScyllaProtectionSetBytes < MinimumStagingFreeBytes
            || MonthlyCostBudgetUsd <= 0)
            throw Invalid("AWS capacity, concurrency, or cost bounds are unsafe");
        if (CloudWatchMetricsEnabled &&
            (!CloudWatchMetricNamespace.Equals("IFM/DatabaseBackup", StringComparison.Ordinal)
             || CloudWatchExportInterval < TimeSpan.FromSeconds(10)
             || CloudWatchExportInterval > TimeSpan.FromMinutes(5)
             || CloudWatchMetricBufferCapacity is < 1000 or > 10000))
            throw Invalid("CloudWatch metric export settings are unsafe");
        if (DestructiveTestsEnabled && !LiveAwsTestsEnabled)
            throw Invalid("destructive tests require live AWS tests to be explicitly enabled");
        if (AcceptBackupRequests && !LiveAwsTestsEnabled && Environment == AwsBackupEnvironment.Development)
            throw Invalid("development request admission requires an explicitly qualified live AWS profile");
    }

    public override string ToString()
        => $"Environment={Environment}; Enabled={Enabled}; WorkloadAccount={WorkloadAccountId}; "
            + $"PrimaryRegion={PrimaryRegion}; RecoveryRegion={RecoveryRegion}; Requests={AcceptBackupRequests}";

    static void RequireAccount(string value, string name)
    {
        if (!Regex.IsMatch(value ?? string.Empty, "^[0-9]{12}$", RegexOptions.CultureInvariant))
            throw Invalid($"{name} must be a 12-digit account ID");
    }

    static void RequireRegion(string value, string name)
    {
        if (!Regex.IsMatch(value ?? string.Empty, "^[a-z]{2}(?:-gov)?-[a-z]+-[0-9]+$", RegexOptions.CultureInvariant))
            throw Invalid($"{name} is invalid");
    }

    static void RequireBucket(string value, string name)
    {
        if (value.Length is < 3 or > 63 || !Regex.IsMatch(value, "^[a-z0-9][a-z0-9.-]*[a-z0-9]$", RegexOptions.CultureInvariant)
            || value.Contains("..", StringComparison.Ordinal) || System.Net.IPAddress.TryParse(value, out _))
            throw Invalid($"{name} is invalid");
    }

    static void RequireArn(string value, string service, string account, string name, string? region = null)
    {
        var pattern = region is null
            ? $"^arn:aws:{service}::{account}:.+$"
            : $"^arn:aws:{service}:{Regex.Escape(region)}:{account}:.+$";
        if (!Regex.IsMatch(value ?? string.Empty, pattern, RegexOptions.CultureInvariant))
            throw Invalid($"{name} does not match the configured account, Region, or service");
    }

    static InvalidOperationException Invalid(string reason)
        => new($"The AwsCloud database-backup profile is incomplete or unsafe: {reason}.");
}
