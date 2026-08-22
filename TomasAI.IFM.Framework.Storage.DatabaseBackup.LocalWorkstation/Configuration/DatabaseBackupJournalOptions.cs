using TomasAI.IFM.Application.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

public sealed class DatabaseBackupJournalOptions
{
    public const string SectionName = "DatabaseBackup:Journal";

    public string Path { get; set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TomasAI", "IFM", "database-backup", "execution-journal.db");
    public bool EnableWriteAheadLog { get; set; } = true;
    public int BusyTimeoutMilliseconds { get; set; } = 5_000;
    public bool RequirePersistentPath { get; set; } = true;
    public string[] ProtectedDataRoots { get; set; } = [];

    public string ValidateAndResolvePath()
    {
        if (string.IsNullOrWhiteSpace(Path))
            throw new InvalidOperationException("A fixed DatabaseBackup journal path is required.");
        if (BusyTimeoutMilliseconds <= 0)
            throw new InvalidOperationException("The DatabaseBackup journal busy timeout must be positive.");

        var resolved = System.IO.Path.GetFullPath(Path);
        if (!string.Equals(System.IO.Path.GetExtension(resolved), ".db", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The DatabaseBackup journal path must identify a .db file.");
        if (RequirePersistentPath && IsWithin(resolved, AppContext.BaseDirectory))
            throw new InvalidOperationException("The DatabaseBackup journal cannot reside in the disposable application directory.");
        foreach (var protectedRoot in ProtectedDataRoots.Where(static value => !string.IsNullOrWhiteSpace(value)))
            if (IsWithin(resolved, System.IO.Path.GetFullPath(protectedRoot)))
                throw new InvalidOperationException("The DatabaseBackup journal cannot reside inside a protected database data directory.");
        return resolved;
    }

    static bool IsWithin(string path, string root)
    {
        var normalizedRoot = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root));
        var normalizedPath = System.IO.Path.GetFullPath(path);
        return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

[Obsolete("Use source-neutral DatabaseBackupHostOptions.")]
public sealed class LocalWorkstationDatabaseBackupOptions : DatabaseBackupHostOptions;
