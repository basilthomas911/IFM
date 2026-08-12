using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Application.DatabaseBackup.Console;

/// <summary>
/// Contains one validated database-backup console invocation.
/// </summary>
internal sealed record DatabaseBackupConsoleOptions(
    string Verb,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlySet<string> Flags)
{
    internal string NatsUrl => GetOptional("nats-url")
        ?? Environment.GetEnvironmentVariable("IFM_NATS_URL")
        ?? "nats://localhost:4222";

    internal BackupSource Source => (GetOptional("source") ?? "local")
        .ToLowerInvariant() switch
        {
            "local" or "localworkstation" => BackupSource.LocalWorkstation,
            "aws" or "awscloud" => BackupSource.AwsCloud,
            var value => throw new ArgumentException($"Unsupported backup source '{value}'.")
        };

    internal int PageSize => GetInt32("page-size", 50, 1, DatabaseBackupContractLimits.MaximumPageSize);

    internal static DatabaseBackupConsoleOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args[0] is "help" or "--help" or "-h")
            throw new ArgumentException(Usage);

        var verb = args[0].Trim().ToLowerInvariant();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Count; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
                throw new ArgumentException($"Unexpected argument '{token}'.{Environment.NewLine}{Usage}");

            var option = token[2..];
            var separator = option.IndexOf('=');
            if (separator >= 0)
            {
                AddValue(option[..separator], option[(separator + 1)..]);
                continue;
            }

            if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                AddValue(option, args[++index]);
                continue;
            }

            if (!flags.Add(option))
                throw new ArgumentException($"Option '--{option}' was supplied more than once.");
        }

        return new DatabaseBackupConsoleOptions(verb, values, flags);

        void AddValue(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value) || !values.TryAdd(name, value))
                throw new ArgumentException($"Option '--{name}' requires one non-empty value and may appear only once.");
        }
    }

    internal string Require(string name)
        => GetOptional(name) ?? throw new ArgumentException($"The '--{name}' option is required for '{Verb}'.");

    internal string? GetOptional(string name)
        => Values.TryGetValue(name, out var value) ? value : null;

    internal bool HasFlag(string name) => Flags.Contains(name);

    internal Guid RequireGuid(string name)
        => Guid.TryParse(Require(name), out var value) && value != Guid.Empty
            ? value
            : throw new ArgumentException($"The '--{name}' option must be a non-empty GUID.");

    internal long GetInt64(string name, long defaultValue = 0)
        => GetOptional(name) is not { } raw
            ? defaultValue
            : long.TryParse(raw, out var value) && value >= 0
                ? value
                : throw new ArgumentException($"The '--{name}' option must be a non-negative integer.");

    internal long RequirePositiveInt64(string name)
        => GetOptional(name) is { } raw && long.TryParse(raw, out var value) && value > 0
            ? value
            : throw new ArgumentException($"The '--{name}' option must be a positive integer.");

    internal int GetInt32(string name, int defaultValue, int minimum, int maximum)
        => GetOptional(name) is not { } raw
            ? defaultValue
            : int.TryParse(raw, out var value) && value >= minimum && value <= maximum
                ? value
                : throw new ArgumentException(
                    $"The '--{name}' option must be between {minimum} and {maximum}.");

    internal static string Usage => """
        Usage: database-backup <verb> [options]

        Query verbs:
          status | list-operations | show-operation | list-restore-points | verify | reconcile | follow

        Command verbs:
          backup | cancel | restore | restore-drill | approve-restore | approve-cutover
          retention-evaluate | retention-execute

        Common options:
          --source local|aws  --environment <name>  --caller <identity>
          --authorization <reference>  --nats-url <url>

        Destructive operations require --confirm.
        """;
}

/// <summary>
/// Defines stable process exit codes for database-backup automation.
/// </summary>
internal static class DatabaseBackupConsoleExitCodes
{
    internal const int Success = 0;
    internal const int FollowedOperationFailed = 1;
    internal const int InvalidArguments = 2;
    internal const int CommandRejected = 3;
    internal const int QueryTargetNotFound = 4;
    internal const int ServiceUnavailable = 5;
    internal const int ReconciliationMismatch = 6;
    internal const int Cancelled = 130;
}
