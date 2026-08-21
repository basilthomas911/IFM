using TomasAI.IFM.Application.ServerManager.Contracts;
using System.Security.Cryptography;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerHostOptions
{
    public string Environment { get; set; } = "Development";

    public string SchedulerName { get; set; } = "IFM-Scheduler";

    public string PipeName { get; set; } = "IFM.ServerManager.Scheduler.v1";

    public string TaskRunRoot { get; set; } = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
        "TomasAI",
        "IFM",
        "ServerManager",
        "TaskRuns");

    public string DeploymentRoot { get; set; } = "C:\\TomasAI\\IFMAppDir";

    public int MaximumConcurrentProcesses { get; set; } = 2;

    public int ShutdownTimeoutSeconds { get; set; } = 45;

    public int RecentRunLimit { get; set; } = 200;

    public int MaximumOutputLineCharacters { get; set; } = 65_536;

    public long MaximumOutputBytesPerStream { get; set; } = 100L * 1024 * 1024;

    public int SuccessfulRunRetentionDays { get; set; } = 30;

    public int FailedRunRetentionDays { get; set; } = 180;

    public Dictionary<string, string> DependencyEndpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> AllowedOperatorGroups { get; set; } = [];

    public bool UseOperatorGroupPipeAcl { get; set; }

    public long MinimumFreeDiskBytes { get; set; } = 1024L * 1024 * 1024;

    public int HealthProbeIntervalSeconds { get; set; } = 10;

    public List<ScheduledTaskCatalogDefinition> TaskCatalog { get; set; } = [];

    public List<InitialScheduleDefinition> InitialSchedules { get; set; } = [];

    public bool SeedInitialSchedules { get; set; } = true;

    public TimeSpan ShutdownTimeout => TimeSpan.FromSeconds(ShutdownTimeoutSeconds);

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(SchedulerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(PipeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(TaskRunRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeploymentRoot);
        if (MaximumConcurrentProcesses <= 0 || ShutdownTimeoutSeconds <= 0 || RecentRunLimit <= 0
            || MaximumOutputLineCharacters <= 0 || MaximumOutputBytesPerStream <= 0
            || SuccessfulRunRetentionDays <= 0 || FailedRunRetentionDays <= 0
            || MinimumFreeDiskBytes <= 0 || HealthProbeIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("Scheduler concurrency, shutdown, output, retention, and recent-run limits must be positive.");
        }

        if (SuccessfulRunRetentionDays > FailedRunRetentionDays)
        {
            throw new InvalidOperationException("Successful-run retention cannot exceed failed-run retention.");
        }

        if (UseOperatorGroupPipeAcl
            && AllowedOperatorGroups.Count == 0)
        {
            throw new InvalidOperationException("Operator-group pipe ACL mode requires at least one explicit Scheduler Host operator group.");
        }

        var duplicate = TaskCatalog
            .GroupBy(task => task.TaskKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Task catalog key '{duplicate}' is duplicated.");
        }

        foreach (var task in TaskCatalog)
        {
            task.Validate(this);
            foreach (var endpoint in task.RequiredEndpoints)
            {
                if (!DependencyEndpoints.ContainsKey(endpoint))
                {
                    throw new InvalidOperationException($"Task '{task.TaskKey}' references undefined dependency endpoint '{endpoint}'.");
                }
            }
        }

        var duplicateScheduleId = InitialSchedules.GroupBy(value => value.ScheduleDefinitionId)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateScheduleId is not null)
        {
            throw new InvalidOperationException($"Initial schedule ID '{duplicateScheduleId}' is duplicated.");
        }

        if (InitialSchedules.Any(value => value.Enabled))
        {
            throw new InvalidOperationException("Configuration-seeded schedules must always start disabled.");
        }
    }
}

public sealed class InitialScheduleDefinition
{
    public Guid ScheduleDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TaskKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public ScheduleKind Kind { get; set; }
    public string ScheduleExpression { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "UTC";
    public SchedulerMisfirePolicy MisfirePolicy { get; set; } = SchedulerMisfirePolicy.DoNothing;
    public int? MaximumRuntimeSeconds { get; set; }
    public int SuccessfulRetentionDays { get; set; } = 30;
    public int FailedRetentionDays { get; set; } = 180;

    public ScheduleDefinitionInputDto ToInput()
        => new(
            ScheduleDefinitionId,
            Name,
            Description,
            TaskKey,
            Kind,
            ScheduleExpression,
            TimeZoneId,
            MisfirePolicy,
            MaximumRuntimeSeconds,
            SuccessfulRetentionDays,
            FailedRetentionDays);
}

public sealed class ScheduledTaskCatalogDefinition
{
    public string TaskKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public List<string> DefaultArguments { get; set; } = [];

    public List<string> EnvironmentAllowlist { get; set; } = [];

    public string RequiredEnvironment { get; set; } = "Development";

    public List<int> SuccessExitCodes { get; set; } = [0];

    public ScheduledTaskStopMode GracefulStopMode { get; set; }

    public string? ShutdownInput { get; set; }

    public bool RequiresApi { get; set; }

    public List<string> RequiredEndpoints { get; set; } = [];

    public int MaximumRuntimeSeconds { get; set; } = 1800;

    public SchedulerRiskClassification RiskClassification { get; set; }

    public string ManifestVersion { get; set; } = "1";

    public string? FileHash { get; set; }

    public string ResolveWorkingDirectory(SchedulerHostOptions host)
        => ResolveBelowRoot(host.DeploymentRoot, WorkingDirectory);

    public string ResolveExecutablePath(SchedulerHostOptions host)
        => Path.IsPathRooted(ExecutablePath)
            ? EnsureBelowRoot(host.DeploymentRoot, ExecutablePath)
            : EnsureBelowRoot(host.DeploymentRoot, Path.Combine(ResolveWorkingDirectory(host), ExecutablePath));

    public void Validate(SchedulerHostOptions host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TaskKey);
        if (TaskKey.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new InvalidOperationException(
                $"Task key '{TaskKey}' may contain only letters, digits, hyphens, underscores, and periods.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(RequiredEnvironment);
        ArgumentException.ThrowIfNullOrWhiteSpace(ManifestVersion);
        if (MaximumRuntimeSeconds <= 0 || SuccessExitCodes.Count == 0)
        {
            throw new InvalidOperationException($"Task '{TaskKey}' requires a positive runtime and success exit code.");
        }

        if (GracefulStopMode == ScheduledTaskStopMode.StandardInput && string.IsNullOrWhiteSpace(ShutdownInput))
        {
            throw new InvalidOperationException($"Task '{TaskKey}' requires ShutdownInput for StandardInput stop mode.");
        }

        _ = ResolveWorkingDirectory(host);
        _ = ResolveExecutablePath(host);
    }

    public bool IsExecutableAvailable(SchedulerHostOptions host)
    {
        var path = ResolveExecutablePath(host);
        if (!File.Exists(path))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FileHash))
        {
            return true;
        }

        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return string.Equals(actual, FileHash.Replace("-", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveBelowRoot(string root, string value)
        => Path.IsPathRooted(value)
            ? EnsureBelowRoot(root, value)
            : EnsureBelowRoot(root, Path.Combine(root, value));

    private static string EnsureBelowRoot(string root, string value)
    {
        var canonicalRootWithoutSeparator = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var canonicalRoot = canonicalRootWithoutSeparator + Path.DirectorySeparatorChar;
        var canonicalValue = Path.GetFullPath(value);
        if (!string.Equals(canonicalValue, canonicalRootWithoutSeparator, StringComparison.OrdinalIgnoreCase)
            && !canonicalValue.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{canonicalValue}' escapes deployment root '{canonicalRoot}'.");
        }

        return canonicalValue;
    }
}
