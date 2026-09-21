using System.Globalization;

namespace TomasAI.IFM.Application.Storage.ProjectionMigration;

internal enum ProjectionMigrationTarget
{
    Reference,
    Securities,
    Market
}

internal sealed record ProjectionMigrationOptions(
    ProjectionMigrationTarget Target,
    bool ApplySchema,
    int BatchSize,
    DateTime? StaleOperationCutoffUtc,
    bool WritersDrainedConfirmed,
    bool RepairFuturesTradeSignals)
{
    public string ConnectionEnvironmentVariable => Target switch
    {
        ProjectionMigrationTarget.Reference => "IFM_STORAGE_MIGRATION_REFERENCE_SCYLLA_CONNECTION",
        ProjectionMigrationTarget.Securities => "IFM_STORAGE_MIGRATION_SECURITIES_SCYLLA_CONNECTION",
        ProjectionMigrationTarget.Market => "IFM_STORAGE_MIGRATION_MARKET_DATA_SCYLLA_CONNECTION",
        _ => throw new ArgumentOutOfRangeException(nameof(Target), Target, null)
    };
}

internal static class ProjectionMigrationCommandLine
{
    internal const string Usage = """
        Usage:
          dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- reference [options]
          dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- securities [options]
          dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- market [options]

        Options:
          --apply-schema
              Create only the additive projection/state tables used by this migration.
          --batch-size <count>
              Rows per write batch (default: 256).
          --stale-operation-cutoff-utc <UTC timestamp>
              Recover journaled operations at or before an explicit UTC instant.
          --confirm-writers-drained
              Required with --stale-operation-cutoff-utc. Asserts affected writers are stopped
              and cannot resume. It is rejected when no cutoff is supplied.
          --repair-futures-trade-signals
              Market only. Quarantine malformed legacy rows and rebuild valid lookup entries.
              Canonical source rows are retained.
          --help

        Connection-string environment variables (must not contain credentials):
          IFM_STORAGE_MIGRATION_REFERENCE_SCYLLA_CONNECTION
          IFM_STORAGE_MIGRATION_SECURITIES_SCYLLA_CONNECTION
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
        var repairFuturesTradeSignals = false;
        int? batchSize = null;
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
                case "--repair-futures-trade-signals":
                    repairFuturesTradeSignals = true;
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

        if (repairFuturesTradeSignals && target != ProjectionMigrationTarget.Market)
        {
            error = "--repair-futures-trade-signals is valid only for the market command.";
            return false;
        }

        options = new ProjectionMigrationOptions(
            target,
            applySchema,
            batchSize ?? 256,
            staleOperationCutoffUtc,
            confirmWritersDrained,
            repairFuturesTradeSignals);
        return true;
    }

    static bool TryParseTarget(string value, out ProjectionMigrationTarget target)
    {
        target = value.ToLowerInvariant() switch
        {
            "reference" => ProjectionMigrationTarget.Reference,
            "securities" => ProjectionMigrationTarget.Securities,
            "market" => ProjectionMigrationTarget.Market,
            _ => default
        };
        return value.Equals("reference", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("securities", StringComparison.OrdinalIgnoreCase) ||
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
