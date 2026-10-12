namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services.Recovery;

/// <summary>Trusted deployment settings for the independent Development cutover worker.</summary>
public sealed class DevelopmentDatabaseReplacementOptions
{
    public const string SectionName = "DatabaseBackup:DevelopmentReplacement";
    public bool Enabled { get; set; }
    public string Environment { get; set; } = "Development";
    public string RepositoryRoot { get; set; } = "";
    public string PersistentRoot { get; set; } = "";
    public string PostgreSqlRestoreRoot { get; set; } = "";
    public string ScyllaRestoreRoot { get; set; } = "";
    public string NativeRestoreContainer { get; set; } = "ifm-database-backup-development-database-backup-host-1";
    public string PostgreSqlContainer { get; set; } = "ifm_db";
    public string ScyllaContainer { get; set; } = "ifm-scylladb";
    public string RedisContainer { get; set; } = "redis";
    public string ScyllaFreshContainer { get; set; } = "ifm-scylla-restore-validation";
    public string ApiHealthUrl { get; set; } = "http://localhost:22543/health/launch-ready";
    public string[] AllowedPostgreSqlProfiles { get; set; } = ["development-on-demand"];
    public string[] AllowedScyllaProfiles { get; set; } = ["disposable-validation"];
    public string[] AllowedPostgreSqlProtectionSets { get; set; } = ["core-postgresql"];
    public string[] AllowedScyllaProtectionSets { get; set; } = ["read-model-scylla"];

    public void Validate()
    {
        if (!Enabled) return;
        if (!OperatingSystem.IsWindows() || Environment != "Development")
            throw new InvalidOperationException("Development replacement requires an independent Windows Development host.");
        foreach (var path in new[] { RepositoryRoot, PersistentRoot, PostgreSqlRestoreRoot, ScyllaRestoreRoot })
            if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("Recovery roots must be absolute deployment paths.");
        if (!File.Exists(Path.Combine(RepositoryRoot, "scripts", "Development", "Start-IFMDevelopment.ps1")))
            throw new InvalidOperationException("The trusted Development startup script is missing.");
        var health = new Uri(ApiHealthUrl);
        if (!health.IsLoopback) throw new InvalidOperationException("Development recovery health must address the local API.");
    }
}
