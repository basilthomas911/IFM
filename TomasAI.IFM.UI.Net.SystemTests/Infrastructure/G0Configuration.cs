using System.Globalization;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed record G0Endpoint(string Name, string Host, int Port);

public sealed class G0Configuration
{
    const string ProductionAdapter = "Production";
    const string DeterministicAdapter = "Deterministic";

    public required string RunId { get; init; }
    public required string EnvironmentName { get; init; }
    public required string RepositoryRoot { get; init; }
    public required string ApiExecutable { get; init; }
    public required string DesktopExecutable { get; init; }
    public required string ResultsRoot { get; init; }
    public required Uri ApiReadyUri { get; init; }
    public required Uri NatsUri { get; init; }
    public required G0Endpoint PostgreSql { get; init; }
    public required G0Endpoint ScyllaDb { get; init; }
    public required G0Endpoint Redis { get; init; }
    public required string FmpAdapter { get; init; }
    public required bool FmpCredentialPresent { get; init; }
    public required bool DeterministicAdapterApproved { get; init; }
    public required TimeSpan ReadinessTimeout { get; init; }
    public required TimeSpan StartupTimeout { get; init; }
    public required TimeSpan ShutdownTimeout { get; init; }
    public required TimeSpan AuditTimeout { get; init; }
    public int ExpectedActorTypeCount { get; init; } = 93;

    public bool UsesProductionFmp => string.Equals(FmpAdapter, ProductionAdapter, StringComparison.OrdinalIgnoreCase);

    public static bool LiveRunEnabled
        => string.Equals(Environment.GetEnvironmentVariable("IFM_RUN_UI_G0"), "1", StringComparison.Ordinal);

    public static bool G1LiveRunEnabled
        => string.Equals(Environment.GetEnvironmentVariable("IFM_RUN_UI_G1"), "1", StringComparison.Ordinal);

    public static bool G2StartupLiveRunEnabled
        => string.Equals(Environment.GetEnvironmentVariable("IFM_RUN_UI_G2_STARTUP"), "1", StringComparison.Ordinal);

    public static G0Configuration Load()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = Read("IFM_G0_CONFIGURATION", "Debug");
        var runId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var natsUri = new Uri(Read("IFM_G0_NATS_URL", "nats://localhost:4222"));
        var apiBaseUri = new Uri(Read("IFM_G0_API_URL", "http://localhost:22543"));

        return new G0Configuration
        {
            RunId = runId,
            EnvironmentName = Read("IFM_G0_ENVIRONMENT", "Development"),
            RepositoryRoot = repositoryRoot,
            ApiExecutable = Path.GetFullPath(Read(
                "IFM_G0_API_EXECUTABLE",
                Path.Combine(repositoryRoot, "TomasAI.IFM.Application.Api.Server", "bin", configuration, "net10.0", "TomasAI.IFM.Application.Api.Server.exe"))),
            DesktopExecutable = Path.GetFullPath(Read(
                "IFM_G0_UI_EXECUTABLE",
                Path.Combine(repositoryRoot, "TomasAI.IFM.UI.Net", "bin", configuration, "net10.0-windows10.0.17763.0", "TomasAI.IFM.UI.Net.exe"))),
            ResultsRoot = Path.GetFullPath(Read(
                "IFM_G0_RESULTS_ROOT",
                Path.Combine(repositoryRoot, "TomasAI.IFM.UI.Net.SystemTests", "TestResults", "Runs"))),
            ApiReadyUri = new Uri(apiBaseUri, "/health/ready"),
            NatsUri = natsUri,
            PostgreSql = ReadEndpoint("PostgreSQL", "IFM_G0_POSTGRES", "localhost", 5432),
            ScyllaDb = ReadEndpoint("ScyllaDB", "IFM_G0_SCYLLA", "localhost", 9042),
            Redis = ReadEndpoint("Redis", "IFM_G0_REDIS", "localhost", 6379),
            FmpAdapter = Read("IFM_G0_FMP_ADAPTER", ProductionAdapter),
            FmpCredentialPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FMP_API_KEY")),
            DeterministicAdapterApproved = string.Equals(
                Environment.GetEnvironmentVariable("IFM_G0_APPROVED_ADAPTER"), "1", StringComparison.Ordinal),
            ReadinessTimeout = ReadTimeout("IFM_G0_READINESS_TIMEOUT_SECONDS", 90),
            StartupTimeout = ReadTimeout("IFM_G0_STARTUP_TIMEOUT_SECONDS", 120),
            ShutdownTimeout = ReadTimeout("IFM_G0_SHUTDOWN_TIMEOUT_SECONDS", 45),
            AuditTimeout = ReadTimeout("IFM_G0_AUDIT_TIMEOUT_SECONDS", 1800),
            ExpectedActorTypeCount = ReadInt("IFM_G0_ACTOR_TYPE_COUNT", 93)
        };
    }

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (!OperatingSystem.IsWindows())
            errors.Add("G0 desktop process automation requires Windows.");
        if (!string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
            errors.Add($"G0 only owns the Development environment; configured value is '{EnvironmentName}'.");
        if (!File.Exists(ApiExecutable))
            errors.Add($"API executable does not exist: {ApiExecutable}");
        if (!File.Exists(DesktopExecutable))
            errors.Add($"Desktop executable does not exist: {DesktopExecutable}");
        if (ExpectedActorTypeCount <= 0)
            errors.Add("Expected actor-type count must be positive.");
        if (AuditTimeout <= TimeSpan.Zero)
            errors.Add("Audit timeout must be positive.");

        if (UsesProductionFmp && !FmpCredentialPresent)
            errors.Add("FMP_API_KEY is required for the production FMP adapter.");
        else if (!UsesProductionFmp
                 && (!string.Equals(FmpAdapter, DeterministicAdapter, StringComparison.OrdinalIgnoreCase)
                     || !DeterministicAdapterApproved))
            errors.Add("A non-production FMP adapter must be the explicitly approved Deterministic adapter.");

        return errors;
    }

    static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not find TomasAI.IFM.sln from the current directory or test output.");
    }

    static G0Endpoint ReadEndpoint(string name, string variable, string defaultHost, int defaultPort)
    {
        var value = Read(variable, $"{defaultHost}:{defaultPort}");
        var separator = value.LastIndexOf(':');
        if (separator < 1 || !int.TryParse(value[(separator + 1)..], CultureInfo.InvariantCulture, out var port))
            throw new FormatException($"{variable} must use host:port format.");
        return new G0Endpoint(name, value[..separator], port);
    }

    static TimeSpan ReadTimeout(string variable, int defaultSeconds)
        => TimeSpan.FromSeconds(ReadInt(variable, defaultSeconds));

    static int ReadInt(string variable, int defaultValue)
        => int.TryParse(Environment.GetEnvironmentVariable(variable), CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;

    static string Read(string variable, string defaultValue)
        => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable))
            ? defaultValue
            : Environment.GetEnvironmentVariable(variable)!;
}
