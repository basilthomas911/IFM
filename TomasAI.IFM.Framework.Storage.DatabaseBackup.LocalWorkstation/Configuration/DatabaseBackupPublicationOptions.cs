using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

public sealed class DatabaseBackupPublicationOptions
{
    public const string SectionName = "DatabaseBackup";

    public string EnvironmentId { get; set; } = "local";
    public OnlineVaultOptions OnlineVault { get; set; } = new();
    public OfflineMediaOptions OfflineMedia { get; set; } = new();
    public RestoreWorkspaceOptions RestoreWorkspace { get; set; } = new();
    public DatabaseManifestOptions Manifest { get; set; } = new();
    public DatabaseBackupLimitsOptions Limits { get; set; } = new();

    public void Validate(bool requirePrivateKey)
    {
        _ = new DatabaseProtectionSetId(EnvironmentId);
        OnlineVault.Validate();
        OfflineMedia.Validate();
        RestoreWorkspace.Validate();
        Manifest.Validate(requirePrivateKey);
        Limits.Validate();

        var roots = new[]
        {
            OnlineVault.ResolveRoot(),
            RestoreWorkspace.ResolveRoot(),
            OfflineMedia.Enabled ? OfflineMedia.ResolveRoot() : null
        }.Where(static value => value is not null).Cast<string>().ToArray();
        for (var left = 0; left < roots.Length; left++)
            for (var right = left + 1; right < roots.Length; right++)
                if (IsWithin(roots[left], roots[right]) || IsWithin(roots[right], roots[left]))
                    throw new InvalidOperationException("Database backup publication roots must not overlap.");
        if (!string.IsNullOrWhiteSpace(Manifest.PrivateKeyPemFile))
        {
            var privateKey = Path.GetFullPath(Manifest.PrivateKeyPemFile);
            if (roots.Any(root => IsWithin(privateKey, root)))
                throw new InvalidOperationException("The manifest private signing key cannot reside in a vault or restore workspace.");
        }
    }

    internal static string ResolveRoot(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidOperationException($"A fixed {description} root is required.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    }

    internal static bool IsWithin(string path, string root)
    {
        var candidate = Path.GetFullPath(path);
        var boundary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return string.Equals(candidate, boundary, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DatabaseBackupLimitsOptions
{
    public int MaximumArtifactCount { get; set; } = 1_000_000;
    public int MaximumCatalogEntryCount { get; set; } = 100_000;
    public long MaximumManifestBytes { get; set; } = 64L << 20;

    internal void Validate()
    {
        if (MaximumArtifactCount <= 0 || MaximumCatalogEntryCount <= 0 || MaximumManifestBytes <= 0)
            throw new InvalidOperationException("Database backup artifact, catalog, and manifest limits must be positive.");
        if (MaximumArtifactCount == int.MaxValue || MaximumCatalogEntryCount == int.MaxValue)
            throw new InvalidOperationException("Database backup collection limits must remain bounded.");
    }
}

public sealed class OnlineVaultOptions
{
    public string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI", "IFM", "database-backup", "online-vault");
    public string ReplicaId { get; set; } = "online-vault";
    public string MediaId { get; set; } = "online-local-vault";
    public long MinimumFreeBytes { get; set; } = 1L << 30;
    public string ResolveRoot() => DatabaseBackupPublicationOptions.ResolveRoot(Root, "online vault");

    internal void Validate()
    {
        _ = ResolveRoot();
        _ = new DatabaseArtifactReplicaId(ReplicaId);
        _ = new DatabaseProtectionSetId(MediaId);
        if (MinimumFreeBytes < 0) throw new InvalidOperationException("Online vault free-space reserve cannot be negative.");
    }
}

public sealed class OfflineMediaOptions
{
    public bool Enabled { get; set; }
    public string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI", "IFM", "database-backup", "offline-media");
    public string ReplicaId { get; set; } = "offline-media-a";
    public string ExpectedMediaId { get; set; } = "offline-media-a";
    public string RotationSlot { get; set; } = "A";
    public long MinimumFreeBytes { get; set; } = 1L << 30;

    public string ResolveRoot() => DatabaseBackupPublicationOptions.ResolveRoot(Root, "offline media");

    internal void Validate()
    {
        if (!Enabled) return;
        _ = ResolveRoot();
        _ = new DatabaseArtifactReplicaId(ReplicaId);
        _ = new DatabaseProtectionSetId(ExpectedMediaId);
        _ = new DatabaseProtectionSetId(RotationSlot);
        if (MinimumFreeBytes < 0) throw new InvalidOperationException("Offline-media free-space reserve cannot be negative.");
    }
}

public sealed class RestoreWorkspaceOptions
{
    public string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI", "IFM", "database-restore", "workspace");
    public long MinimumFreeBytes { get; set; } = 1L << 30;

    public string ResolveRoot() => DatabaseBackupPublicationOptions.ResolveRoot(Root, "restore workspace");

    internal void Validate()
    {
        _ = ResolveRoot();
        if (MinimumFreeBytes < 0) throw new InvalidOperationException("Restore-workspace free-space reserve cannot be negative.");
    }
}

public sealed class DatabaseManifestOptions
{
    public string KeyId { get; set; } = "local-manifest-signing-v1";
    public string PrivateKeyPemFile { get; set; } = string.Empty;
    public string PublicKeyPemFile { get; set; } = string.Empty;

    internal void Validate(bool requirePrivateKey)
    {
        _ = new DatabaseProtectionSetId(KeyId);
        if (string.IsNullOrWhiteSpace(PublicKeyPemFile) || !Path.IsPathFullyQualified(PublicKeyPemFile))
            throw new InvalidOperationException("A fixed manifest public-key PEM file is required.");
        if (requirePrivateKey && (string.IsNullOrWhiteSpace(PrivateKeyPemFile) || !Path.IsPathFullyQualified(PrivateKeyPemFile)))
            throw new InvalidOperationException("A fixed manifest private-key PEM file is required for publication.");
        if (!string.IsNullOrWhiteSpace(PrivateKeyPemFile) && !Path.IsPathFullyQualified(PrivateKeyPemFile))
            throw new InvalidOperationException("The manifest private-key PEM path must be fixed.");
    }
}
