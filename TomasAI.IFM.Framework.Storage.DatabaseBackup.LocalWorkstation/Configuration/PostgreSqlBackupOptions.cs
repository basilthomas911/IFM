using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

public sealed class LocalWorkstationSourceOptions
{
    public const string SectionName = "DatabaseBackup:Sources:LocalWorkstation";

    public bool Enabled { get; set; }
    public bool DryRun { get; set; } = true;
    public bool PostgreSqlEnabled { get; set; } = true;
    public bool ScyllaEnabled { get; set; } = true;
    public bool IncrementalEnabled { get; set; }
    public int MaximumIncrementalChainDepth { get; set; } = 6;
    public TimeSpan MaximumIncrementalBaseAge { get; set; } = TimeSpan.FromDays(7);

    public void Validate()
    {
        if (Enabled && !DryRun && !PostgreSqlEnabled && !ScyllaEnabled)
            throw new InvalidOperationException(
                "At least one native database engine must be enabled when LocalWorkstation dry-run mode is disabled.");
        if (MaximumIncrementalChainDepth <= 0)
            throw new InvalidOperationException("The maximum incremental chain depth must be positive.");
        if (MaximumIncrementalBaseAge <= TimeSpan.Zero)
            throw new InvalidOperationException("The maximum incremental base age must be positive.");
    }

    public bool IsEngineEnabled(DatabaseEngine engine)
        => !Enabled || DryRun || engine switch
        {
            DatabaseEngine.PostgreSql => PostgreSqlEnabled,
            DatabaseEngine.ScyllaDb => ScyllaEnabled,
            _ => false
        };
}

public sealed class PostgreSqlBackupOptions
{
    public const string SectionName = "DatabaseBackup:PostgreSql";

    public string ToolDirectory { get; set; } = string.Empty;
    public string BackupRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI", "IFM", "database-backup", "postgresql");
    public string RestoreRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI", "IFM", "database-restore", "postgresql");
    public string ConnectionStringEnvironmentVariable { get; set; } = "IFM_POSTGRES_BACKUP_CONNECTION";
    public string[] AllowedProtectionSets { get; set; } = [];
    public Dictionary<string, PostgreSqlFreshTargetProfileOptions> FreshTargetProfiles { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public string[] ProtectedDataRoots { get; set; } = [];
    public int MinimumMajorVersion { get; set; } = 15;
    public int MaximumMajorVersion { get; set; } = 18;
    public TimeSpan ProcessTimeout { get; set; } = TimeSpan.FromHours(4);
    public bool RequirePersistentBackupRoot { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ToolDirectory) || !Path.IsPathFullyQualified(ToolDirectory))
            throw new InvalidOperationException("A fixed PostgreSQL tool directory is required.");
        if (string.IsNullOrWhiteSpace(ConnectionStringEnvironmentVariable)
            || ConnectionStringEnvironmentVariable.Any(static character => !(char.IsLetterOrDigit(character) || character == '_')))
            throw new InvalidOperationException("The PostgreSQL connection-string secret reference is invalid.");
        if (MinimumMajorVersion < 12 || MaximumMajorVersion < MinimumMajorVersion)
            throw new InvalidOperationException("The PostgreSQL native version range is invalid.");
        if (ProcessTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("The PostgreSQL native process timeout must be positive.");
        if (AllowedProtectionSets.Length == 0)
            throw new InvalidOperationException("At least one PostgreSQL protection set must be allowlisted.");
        foreach (var protectionSet in AllowedProtectionSets) _ = new DatabaseProtectionSetId(protectionSet);
        if (FreshTargetProfiles.Count == 0)
            throw new InvalidOperationException("At least one fresh PostgreSQL target profile must be configured.");
        foreach (var (profile, target) in FreshTargetProfiles)
        {
            _ = new DatabaseProtectionSetId(profile);
            target.Validate();
        }

        var backupRoot = ResolveRoot(BackupRoot, nameof(BackupRoot));
        var restoreRoot = ResolveRoot(RestoreRoot, nameof(RestoreRoot));
        if (IsWithin(backupRoot, restoreRoot) || IsWithin(restoreRoot, backupRoot))
            throw new InvalidOperationException("PostgreSQL backup and restore roots must not overlap.");
        if (RequirePersistentBackupRoot && IsWithin(backupRoot, AppContext.BaseDirectory))
            throw new InvalidOperationException("The PostgreSQL backup root cannot use the disposable application directory.");
        foreach (var protectedRootValue in ProtectedDataRoots.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            var protectedRoot = Path.GetFullPath(protectedRootValue);
            if (IsWithin(backupRoot, protectedRoot) || IsWithin(restoreRoot, protectedRoot))
                throw new InvalidOperationException("PostgreSQL backup and restore roots cannot reside in protected data roots.");
        }
    }

    public string ResolveBackupRoot() => ResolveRoot(BackupRoot, nameof(BackupRoot));
    public string ResolveRestoreRoot() => ResolveRoot(RestoreRoot, nameof(RestoreRoot));

    static string ResolveRoot(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidOperationException($"A fixed PostgreSQL {name} is required.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    }

    internal static bool IsWithin(string path, string root)
    {
        var normalizedPath = Path.GetFullPath(path);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class PostgreSqlFreshTargetProfileOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; }
    public string Database { get; set; } = "postgres";
    public string[] AllowedLogicalTargets { get; set; } = [];
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public void Validate()
    {
        if (!string.Equals(Host, "127.0.0.1", StringComparison.Ordinal)
            && !string.Equals(Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Host, "::1", StringComparison.Ordinal))
            throw new InvalidOperationException("Fresh PostgreSQL validation targets must be loopback-only.");
        if (Port is < 1024 or > 65535)
            throw new InvalidOperationException("A non-privileged PostgreSQL validation port is required.");
        _ = new DatabaseProtectionSetId(Database);
        if (AllowedLogicalTargets.Length == 0)
            throw new InvalidOperationException("At least one logical fresh target must be allowlisted.");
        foreach (var target in AllowedLogicalTargets) _ = new DatabaseProtectionSetId(target);
        if (StartupTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("The PostgreSQL validation startup timeout must be positive.");
    }
}
