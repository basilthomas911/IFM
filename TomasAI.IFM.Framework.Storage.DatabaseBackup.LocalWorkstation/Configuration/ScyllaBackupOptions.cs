using System.Text.RegularExpressions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

public sealed partial class ScyllaBackupOptions
{
    public const string SectionName = "DatabaseBackup:Scylla";

    public string ToolDirectory { get; set; } = string.Empty;
    public string BackupRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI", "IFM", "database-backup", "scylla");
    public string RestoreRoot { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI", "IFM", "database-restore", "scylla");
    public string ManagerApiUrl { get; set; } = "http://127.0.0.1:5080/api/v1";
    public string ManagerApiCertificateFile { get; set; } = string.Empty;
    public string ManagerApiKeyFile { get; set; } = string.Empty;
    public Dictionary<string, ScyllaProtectionSetOptions> ProtectionSets { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ScyllaFreshTargetProfileOptions> FreshTargetProfiles { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public string[] ProtectedDataRoots { get; set; } = [];
    public int MinimumManagerMajorVersion { get; set; } = 3;
    public int MaximumManagerMajorVersion { get; set; } = 4;
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public bool RequirePersistentBackupRoot { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ToolDirectory) || !Path.IsPathFullyQualified(ToolDirectory))
            throw new InvalidOperationException("A fixed Scylla Manager tool directory is required.");
        if (!Uri.TryCreate(ManagerApiUrl, UriKind.Absolute, out var managerUri)
            || (managerUri.Scheme != Uri.UriSchemeHttp && managerUri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(managerUri.UserInfo) || !string.IsNullOrEmpty(managerUri.Query)
            || !string.IsNullOrEmpty(managerUri.Fragment))
            throw new InvalidOperationException("The Scylla Manager API URL is invalid or contains credentials.");
        if (string.IsNullOrWhiteSpace(ManagerApiCertificateFile) != string.IsNullOrWhiteSpace(ManagerApiKeyFile))
            throw new InvalidOperationException("Scylla Manager client certificate and key references must be configured together.");
        if (!string.IsNullOrWhiteSpace(ManagerApiCertificateFile)
            && (!Path.IsPathFullyQualified(ManagerApiCertificateFile) || !Path.IsPathFullyQualified(ManagerApiKeyFile)))
            throw new InvalidOperationException("Scylla Manager client certificate and key references must be fixed paths.");
        if (MinimumManagerMajorVersion < 3 || MaximumManagerMajorVersion < MinimumManagerMajorVersion)
            throw new InvalidOperationException("The Scylla Manager compatibility range is invalid.");
        if (OperationTimeout <= TimeSpan.Zero || PollInterval <= TimeSpan.Zero || PollInterval >= OperationTimeout)
            throw new InvalidOperationException("The Scylla Manager operation timing is invalid.");
        if (ProtectionSets.Count == 0)
            throw new InvalidOperationException("At least one Scylla protection set must be configured.");
        foreach (var (name, protectionSet) in ProtectionSets)
        {
            _ = new DatabaseProtectionSetId(name);
            protectionSet.Validate();
        }
        if (FreshTargetProfiles.Count == 0)
            throw new InvalidOperationException("At least one fresh Scylla target profile must be configured.");
        foreach (var (name, target) in FreshTargetProfiles)
        {
            _ = new DatabaseProtectionSetId(name);
            target.Validate();
        }

        var backupRoot = ResolveRoot(BackupRoot, nameof(BackupRoot));
        var restoreRoot = ResolveRoot(RestoreRoot, nameof(RestoreRoot));
        if (PostgreSqlBackupOptions.IsWithin(backupRoot, restoreRoot)
            || PostgreSqlBackupOptions.IsWithin(restoreRoot, backupRoot))
            throw new InvalidOperationException("Scylla backup and restore roots must not overlap.");
        if (RequirePersistentBackupRoot && PostgreSqlBackupOptions.IsWithin(backupRoot, AppContext.BaseDirectory))
            throw new InvalidOperationException("The Scylla backup root cannot use the disposable application directory.");
        foreach (var value in ProtectedDataRoots.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            var protectedRoot = Path.GetFullPath(value);
            if (PostgreSqlBackupOptions.IsWithin(backupRoot, protectedRoot)
                || PostgreSqlBackupOptions.IsWithin(restoreRoot, protectedRoot))
                throw new InvalidOperationException("Scylla backup and restore roots cannot reside in protected data roots.");
        }
    }

    public string ResolveBackupRoot() => ResolveRoot(BackupRoot, nameof(BackupRoot));
    public string ResolveRestoreRoot() => ResolveRoot(RestoreRoot, nameof(RestoreRoot));

    static string ResolveRoot(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidOperationException($"A fixed Scylla {name} is required.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    }

    internal static void ValidateManagerIdentifier(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !ManagerIdentifierPattern().IsMatch(value))
            throw new InvalidOperationException($"The Scylla {name} contains unsupported characters.");
    }

    internal static void ValidateLocation(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !BackupLocationPattern().IsMatch(value))
            throw new InvalidOperationException("The Scylla Manager backup location is invalid.");
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.:/-]{0,254}$", RegexOptions.CultureInvariant)]
    private static partial Regex ManagerIdentifierPattern();

    [GeneratedRegex(@"^(?:[A-Za-z0-9_.-]+:)?(?:localstorage|s3|gcs|azure):[A-Za-z0-9][A-Za-z0-9.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupLocationPattern();
}

public sealed class ScyllaProtectionSetOptions
{
    public string ManagerCluster { get; set; } = string.Empty;
    public string BackupLocation { get; set; } = string.Empty;
    public string[] Keyspaces { get; set; } = [];
    public int RequiredLiveNodes { get; set; } = 1;

    public void Validate()
    {
        ScyllaBackupOptions.ValidateManagerIdentifier(ManagerCluster, "Manager cluster");
        ScyllaBackupOptions.ValidateLocation(BackupLocation);
        if (Keyspaces.Length == 0)
            throw new InvalidOperationException("At least one Scylla keyspace selection is required.");
        foreach (var keyspace in Keyspaces)
            if (string.IsNullOrWhiteSpace(keyspace) || keyspace.Any(static character => !(char.IsLetterOrDigit(character) || "_.*?!,-".Contains(character))))
                throw new InvalidOperationException("A Scylla keyspace selection contains unsupported characters.");
        if (RequiredLiveNodes <= 0)
            throw new InvalidOperationException("The required Scylla live-node count must be positive.");
    }
}

public sealed class ScyllaFreshTargetProfileOptions
{
    public string ManagerCluster { get; set; } = string.Empty;
    public string[] AllowedLogicalTargets { get; set; } = [];
    public int RequiredLiveNodes { get; set; } = 1;

    public void Validate()
    {
        ScyllaBackupOptions.ValidateManagerIdentifier(ManagerCluster, "fresh-target Manager cluster");
        if (AllowedLogicalTargets.Length == 0)
            throw new InvalidOperationException("At least one logical fresh Scylla target must be allowlisted.");
        foreach (var target in AllowedLogicalTargets) _ = new DatabaseProtectionSetId(target);
        if (RequiredLiveNodes <= 0)
            throw new InvalidOperationException("The fresh Scylla target live-node count must be positive.");
    }
}
