using System.Globalization;

namespace TomasAI.IFM.Application.Storage.Backup;

internal enum ProjectionMigrationTarget
{
    Reference,
    Securities,
    Fund,
    Market
}

internal sealed record ProjectionMigrationOptions(
    ProjectionMigrationTarget Target,
    bool ApplySchema,
    int BatchSize,
    int? FundId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTime? StaleOperationCutoffUtc,
    bool WritersDrainedConfirmed)
{
    public string ConnectionEnvironmentVariable => Target switch
    {
        ProjectionMigrationTarget.Reference => "IFM_STORAGE_MIGRATION_REFERENCE_SCYLLA_CONNECTION",
        ProjectionMigrationTarget.Securities => "IFM_STORAGE_MIGRATION_SECURITIES_SCYLLA_CONNECTION",
        ProjectionMigrationTarget.Fund => "IFM_STORAGE_MIGRATION_FUND_SCYLLA_CONNECTION",
        ProjectionMigrationTarget.Market => "IFM_STORAGE_MIGRATION_MARKET_DATA_SCYLLA_CONNECTION",
        _ => throw new ArgumentOutOfRangeException(nameof(Target), Target, null)
    };
}

internal static class ProjectionMigrationCommandLine
{
    internal const string Usage = """
        Usage:
          dotnet run --project TomasAI.IFM.Application.Storage.Backup -- reference [options]
          dotnet run --project TomasAI.IFM.Application.Storage.Backup -- securities [options]
          dotnet run --project TomasAI.IFM.Application.Storage.Backup -- fund --fund-id <id> --start-date <yyyy-MM-dd> --end-date <yyyy-MM-dd> [options]
          dotnet run --project TomasAI.IFM.Application.Storage.Backup -- market [options]

        Options:
          --apply-schema
              Create only the additive projection/state tables used by this migration.
          --batch-size <count>
              Rows per write batch (default: 256; Fund: 500).
          --stale-operation-cutoff-utc <UTC timestamp>
              Recover journaled operations at or before an explicit UTC instant.
          --confirm-writers-drained
              Required with --stale-operation-cutoff-utc. Asserts affected writers are stopped
              and cannot resume. It is rejected when no cutoff is supplied.
          --help

        Fund-only options:
          --fund-id <id>
          --start-date <yyyy-MM-dd>
          --end-date <yyyy-MM-dd>

        Connection-string environment variables (must not contain credentials):
          IFM_STORAGE_MIGRATION_REFERENCE_SCYLLA_CONNECTION
          IFM_STORAGE_MIGRATION_SECURITIES_SCYLLA_CONNECTION
          IFM_STORAGE_MIGRATION_FUND_SCYLLA_CONNECTION
          IFM_STORAGE_MIGRATION_MARKET_DATA_SCYLLA_CONNECTION

        Credentials remain in SCYLLADB_DEV_KEY, SCYLLADB_TEST_KEY,
        SCYLLADB_STAGING_KEY, or SCYLLADB_PROD_KEY as selected by DOTNET_ENVIRONMENT.
        """;

    public static bool TryParse(
        string[] args,
        out ProjectionMigrationOptions? options,
        out string? error,
        out bool showHelp)
    {
        ArgumentNullException.ThrowIfNull(args);
        options = null;
        error = null;
        showHelp = args.Length == 0 || args.Any(static argument => argument is "--help" or "-h");
        if (showHelp)
            return true;

        if (!TryParseTarget(args[0], out var target))
        {
            error = $"Unknown migration command '{args[0]}'.";
            return false;
        }

        var seenOptions = new HashSet<string>(StringComparer.Ordinal);
        var applySchema = false;
        var confirmWritersDrained = false;
        int? batchSize = null;
        int? fundId = null;
        DateOnly? startDate = null;
        DateOnly? endDate = null;
        DateTime? staleOperationCutoffUtc = null;

        for (var index = 1; index < args.Length; index++)
        {
            var optionName = args[index];
            if (!seenOptions.Add(optionName))
            {
                error = $"Option '{optionName}' was supplied more than once.";
                return false;
            }

            switch (optionName)
            {
                case "--apply-schema":
                    applySchema = true;
                    break;
                case "--confirm-writers-drained":
                    confirmWritersDrained = true;
                    break;
                case "--batch-size":
                    if (!TryReadValue(args, ref index, optionName, out var rawBatchSize, out error))
                        return false;
                    if (!int.TryParse(rawBatchSize, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedBatchSize) ||
                        parsedBatchSize < 1)
                    {
                        error = "--batch-size must be a positive integer.";
                        return false;
                    }
                    batchSize = parsedBatchSize;
                    break;
                case "--fund-id":
                    if (!TryReadValue(args, ref index, optionName, out var rawFundId, out error))
                        return false;
                    if (!int.TryParse(rawFundId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFundId) ||
                        parsedFundId < 1)
                    {
                        error = "--fund-id must be a positive integer.";
                        return false;
                    }
                    fundId = parsedFundId;
                    break;
                case "--start-date":
                    if (!TryReadValue(args, ref index, optionName, out var rawStartDate, out error))
                        return false;
                    if (!DateOnly.TryParseExact(
                        rawStartDate,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedStartDate))
                    {
                        error = "--start-date must use yyyy-MM-dd.";
                        return false;
                    }
                    startDate = parsedStartDate;
                    break;
                case "--end-date":
                    if (!TryReadValue(args, ref index, optionName, out var rawEndDate, out error))
                        return false;
                    if (!DateOnly.TryParseExact(
                        rawEndDate,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedEndDate))
                    {
                        error = "--end-date must use yyyy-MM-dd.";
                        return false;
                    }
                    endDate = parsedEndDate;
                    break;
                case "--stale-operation-cutoff-utc":
                    if (!TryReadValue(args, ref index, optionName, out var rawCutoff, out error))
                        return false;
                    if (!TryParseExplicitUtc(rawCutoff, out var parsedCutoff))
                    {
                        error = "--stale-operation-cutoff-utc must be an ISO-8601 timestamp with an explicit UTC offset (Z or +00:00).";
                        return false;
                    }
                    staleOperationCutoffUtc = parsedCutoff;
                    break;
                default:
                    error = $"Unknown option '{optionName}'.";
                    return false;
            }
        }

        if (staleOperationCutoffUtc.HasValue != confirmWritersDrained)
        {
            error = staleOperationCutoffUtc.HasValue
                ? "--stale-operation-cutoff-utc requires --confirm-writers-drained."
                : "--confirm-writers-drained is valid only with --stale-operation-cutoff-utc.";
            return false;
        }

        if (target == ProjectionMigrationTarget.Fund)
        {
            if (!fundId.HasValue || !startDate.HasValue || !endDate.HasValue)
            {
                error = "The fund command requires --fund-id, --start-date, and --end-date.";
                return false;
            }
            if (endDate < startDate)
            {
                error = "--end-date cannot precede --start-date.";
                return false;
            }
        }
        else if (fundId.HasValue || startDate.HasValue || endDate.HasValue)
        {
            error = "--fund-id, --start-date, and --end-date are valid only for the fund command.";
            return false;
        }

        options = new ProjectionMigrationOptions(
            target,
            applySchema,
            batchSize ?? (target == ProjectionMigrationTarget.Fund ? 500 : 256),
            fundId,
            startDate,
            endDate,
            staleOperationCutoffUtc,
            confirmWritersDrained);
        return true;
    }

    static bool TryParseTarget(string value, out ProjectionMigrationTarget target)
    {
        target = value.ToLowerInvariant() switch
        {
            "reference" => ProjectionMigrationTarget.Reference,
            "securities" => ProjectionMigrationTarget.Securities,
            "fund" => ProjectionMigrationTarget.Fund,
            "market" => ProjectionMigrationTarget.Market,
            _ => default
        };
        return value.Equals("reference", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("securities", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("fund", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("market", StringComparison.OrdinalIgnoreCase);
    }

    static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out string value,
        out string? error)
    {
        if (++index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            error = $"Option '{optionName}' requires a value.";
            return false;
        }

        value = args[index];
        error = null;
        return true;
    }

    static bool TryParseExplicitUtc(string value, out DateTime utc)
    {
        var hasUtcDesignator = value.EndsWith('Z') ||
            value.EndsWith("+00:00", StringComparison.Ordinal);
        if (!hasUtcDesignator ||
            !DateTimeOffset.TryParseExact(
                value,
                [
                    "yyyy-MM-dd'T'HH:mm:ss'Z'",
                    "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
                    "yyyy-MM-dd'T'HH:mm:sszzz",
                    "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
                ],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed) ||
            parsed.Offset != TimeSpan.Zero)
        {
            utc = default;
            return false;
        }

        utc = parsed.UtcDateTime;
        return true;
    }
}
