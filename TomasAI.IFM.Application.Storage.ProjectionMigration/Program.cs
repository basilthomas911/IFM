using TomasAI.IFM.Application.Storage.EventSourceDb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ProjectionMigration;

internal static class Program
{
    const int SuccessExitCode = 0;
    const int FailureExitCode = 1;
    const int UsageExitCode = 2;
    const int ReconciliationMismatchExitCode = 3;
    const int CancelledExitCode = 130;
    const string ScyllaProviderName = "System.Data.ScyllaDb";

    static readonly ILogger<DbProvider> Logger = NullLogger<DbProvider>.Instance;

    static readonly string[] ReferenceProjectionObjects =
    [
        "reference_projection_state",
        "reference_projection_mutation",
        "reference_projection_ownership",
        "scheduled_job_by_name",
        "scheduled_job_write_ownership",
    ];

    static readonly string[] SecuritiesProjectionObjects =
    [
        "futures_contract_by_symbol",
        "futures_option_contract_by_symbol",
        "securities_projection_state",
        "securities_symbol_projection_state",
        "securities_projection_operation",
        "securities_projection_operation_scope"
    ];

    static async Task<int> Main(string[] args)
    {
        if (args is ["--capture-marketdata-development-baseline"])
            return await CaptureMarketDataDevelopmentBaselineAsync().ConfigureAwait(false);
        if (args is ["--capture-securities-baseline", "--confirm-writers-drained"])
            return await CaptureSecuritiesBaselineAsync().ConfigureAwait(false);
        if (args is ["--rebuild-empty-securities-qualification"])
            return await CaptureSecuritiesBaselineAsync(rebuildQualification: true).ConfigureAwait(false);

        if (!ProjectionMigrationCommandLine.TryParse(
            args,
            out var options,
            out var error,
            out var showHelp))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(ProjectionMigrationCommandLine.Usage);
            return UsageExitCode;
        }

        if (showHelp)
        {
            Console.WriteLine(ProjectionMigrationCommandLine.Usage);
            return SuccessExitCode;
        }

        if (options!.StaleOperationCutoffUtc > DateTime.UtcNow)
        {
            Console.Error.WriteLine("--stale-operation-cutoff-utc cannot be in the future.");
            return UsageExitCode;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            WriteStartSummary(options);
            return await RunAsync(options, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("Projection migration cancelled; completion remains disabled where reconciliation did not finish.");
            return CancelledExitCode;
        }
        catch (MigrationConfigurationException exception)
        {
            Console.Error.WriteLine($"Configuration error: {exception.Message}");
            return UsageExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Projection migration failed ({exception.GetType().Name}): {exception.Message}");
            return FailureExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    static async Task<int> CaptureSecuritiesBaselineAsync(bool rebuildQualification = false)
    {
        try
        {
            var connection = Environment.GetEnvironmentVariable("IFM_SECURITIES_BASELINE_CONNECTION")
                ?? throw new InvalidOperationException("Set IFM_SECURITIES_BASELINE_CONNECTION to the isolated or Development PostgreSQL database.");
            var identity = new Npgsql.NpgsqlConnectionStringBuilder(connection);
            var qualification = identity.Database?.StartsWith("ifm_securities_qualification_", StringComparison.Ordinal) == true;
            if (identity.Host is not ("localhost" or "127.0.0.1")
                || (identity.Database != "ifm_application_development" && !qualification)
                || (rebuildQualification && !qualification))
                throw new InvalidOperationException("Securities baseline capture requires local Development or a named Securities qualification database.");
            if (identity.Database == "ifm_application_development")
            {
                foreach (var writer in new[] { "IFMServerManager", "TomasAI.IFM.Application.ServerManager",
                    "TomasAI.IFM.Application.ServerManager.SchedulerHost", "TomasAI.IFM.Application.Api.Server",
                    "TomasAI.IFM.Application.MarketData.Host" })
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName(writer);
                    foreach (var process in processes)
                        process.Dispose();
                    if (processes.Length != 0)
                        throw new InvalidOperationException($"Drain and stop {writer} before capturing the Securities baseline.");
                }
            }
            var settings = new DbConnectionSettings()
                .Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection, "System.Data.Postgres")
                .Add(SecuritiesDbContext.SecuritiesDbConnection, connection, "System.Data.Postgres");
            var transactions = new PostgresEventTransaction(settings);
            if (rebuildQualification)
            {
                var repositories = new Dictionary<Type, object>();
                var database = new SecuritiesDbContext(settings, CreateFactory(repositories), Logger);
                repositories.Add(typeof(IObjectRepository<SecuritiesDbContext>), database);
                var replayed = await SecuritiesCatalogProjection.RebuildEmptyAsync(database, transactions).ConfigureAwait(false);
                Console.WriteLine($"Rebuilt the isolated Securities projections from {replayed} source events.");
                return SuccessExitCode;
            }

            var captured = await new SecuritiesCatalogBaseline(transactions)
                .CaptureAsync(writersDrained: true,
                    expectedMissing: qualification ? null : (41, 6)).ConfigureAwait(false);
            Console.WriteLine(captured is { } result
                ? $"Securities baseline committed with {result.Futures} futures and {result.Options} options without source history."
                : "Securities baseline already exists.");
            return SuccessExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Securities baseline capture failed ({exception.GetType().Name}): {exception.Message}");
            return FailureExitCode;
        }
    }

    static async Task<int> CaptureMarketDataDevelopmentBaselineAsync()
    {
        try
        {
            var writers = new[]
            {
                "IFMServerManager", "TomasAI.IFM.Application.ServerManager",
                "TomasAI.IFM.Application.ServerManager.SchedulerHost", "TomasAI.IFM.Application.Api.Server"
            };
            foreach (var writer in writers)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(writer);
                try
                {
                    if (processes.Length != 0)
                        throw new InvalidOperationException($"Drain and stop {writer} before capturing the baseline.");
                }
                finally
                {
                    foreach (var process in processes)
                        process.Dispose();
                }
            }

            var connection = Environment.GetEnvironmentVariable("IFM_MARKETDATA_BASELINE_CONNECTION")
                ?? throw new InvalidOperationException("Set IFM_MARKETDATA_BASELINE_CONNECTION to the Development PostgreSQL database.");
            var identity = new Npgsql.NpgsqlConnectionStringBuilder(connection);
            if (identity.Host is not ("localhost" or "127.0.0.1") || identity.Database != "ifm_application_development")
                throw new InvalidOperationException("Baseline capture requires the local Development PostgreSQL database.");

            var settings = new DbConnectionSettings()
                .Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection, "System.Data.Postgres");
            var capture = new MarketDataReconciliationBaseline(new PostgresEventTransaction(settings));
            Console.WriteLine(await capture.CaptureAsync(writersStopped: true).ConfigureAwait(false)
                ? "MarketData Development baseline committed."
                : "MarketData Development baseline already exists.");
            return SuccessExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"MarketData baseline capture failed ({exception.GetType().Name}): {exception.Message}");
            return FailureExitCode;
        }
    }

    static Task<int> RunAsync(
        ProjectionMigrationOptions options,
        CancellationToken cancellationToken)
        => options.Target switch
        {
            ProjectionMigrationTarget.Reference => RunReferenceAsync(options, cancellationToken),
            ProjectionMigrationTarget.Securities => RunSecuritiesAsync(options, cancellationToken),
            ProjectionMigrationTarget.Market => RunMarketAsync(options, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Target), options.Target, null)
        };

    static async Task<int> RunReferenceAsync(
        ProjectionMigrationOptions options,
        CancellationToken cancellationToken)
    {
        var settings = CreateConnectionSettings(
            ReferenceDbContext.ReferenceDbConnection,
            options.ConnectionEnvironmentVariable);
        var repositories = new Dictionary<Type, object>();
        var factory = CreateFactory(repositories);
        var context = new ReferenceDbContext(
            settings,
            factory,
            UnavailableSequenceIdGenerator.Instance,
            Logger);
        repositories.Add(typeof(IObjectRepository<ReferenceDbContext>), context);

        if (options.ApplySchema)
        {
            await ApplySchemaAsync(
                new ReferenceSchemaDb(settings, Logger),
                ReferenceProjectionObjects,
                cancellationToken).ConfigureAwait(false);
        }

        var backfill = await context.BackfillQueryProjectionsV2Async(
            options.BatchSize,
            cancellationToken,
            options.StaleOperationCutoffUtc).ConfigureAwait(false);
        var reconciliation = await context.ReconcileQueryProjectionsV2Async(cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(
            $"Reference backfill: scheduledJobs={backfill.ScheduledJobs}.");
        Console.WriteLine(
            $"Reference reconciliation: scheduled-job source/projected={reconciliation.SourceScheduledJobs}/{reconciliation.ProjectedScheduledJobs}, " +
            $"missing/unexpected/tokenless={reconciliation.MissingScheduledJobs}/{reconciliation.UnexpectedScheduledJobs}/" +
            $"{reconciliation.TokenlessScheduledJobReservations}.");

        return Complete(reconciliation.IsConsistent);
    }

    static async Task<int> RunSecuritiesAsync(
        ProjectionMigrationOptions options,
        CancellationToken cancellationToken)
    {
        var settings = CreateConnectionSettings(
            SecuritiesDbContext.SecuritiesDbConnection,
            options.ConnectionEnvironmentVariable);
        var repositories = new Dictionary<Type, object>();
        var factory = CreateFactory(repositories);
        var context = new SecuritiesDbContext(settings, factory, Logger);
        repositories.Add(typeof(IObjectRepository<SecuritiesDbContext>), context);

        if (options.ApplySchema)
        {
            await ApplySchemaAsync(
                new SecuritiesSchemaDb(settings, Logger),
                SecuritiesProjectionObjects,
                cancellationToken).ConfigureAwait(false);
        }

        var backfill = await context.BackfillSymbolProjectionsAsync(
            options.BatchSize,
            cancellationToken,
            options.StaleOperationCutoffUtc).ConfigureAwait(false);
        var reconciliation = await context.ReconcileSymbolProjectionsAsync(cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(
            $"Securities backfill: futures={backfill.FuturesContractsUpserted}, options={backfill.FuturesOptionContractsUpserted}.");
        Console.WriteLine(
            $"Securities reconciliation: futures source/projected={reconciliation.FuturesContractSourceRows}/{reconciliation.FuturesContractProjectionRows}, " +
            $"missing/unexpected={reconciliation.FuturesContractMissingKeys}/{reconciliation.FuturesContractUnexpectedKeys}; " +
            $"options source/projected={reconciliation.FuturesOptionContractSourceRows}/{reconciliation.FuturesOptionContractProjectionRows}, " +
            $"missing/unexpected={reconciliation.FuturesOptionContractMissingKeys}/{reconciliation.FuturesOptionContractUnexpectedKeys}.");

        return Complete(reconciliation.IsConsistent);
    }

    static async Task<int> RunMarketAsync(
        ProjectionMigrationOptions options,
        CancellationToken cancellationToken)
    {
        var settings = CreateConnectionSettings(
            MarketDataDbContext.MarketDataDbConnection,
            options.ConnectionEnvironmentVariable, "System.Data.Postgres");
        var repositories = new Dictionary<Type, object>();
        var factory = CreateFactory(repositories);
        var context = new MarketDataDbContext(
            settings,
            factory,
            UnavailableBlackboardService.Instance,
            UnavailableSequenceIdGenerator.Instance,
            Logger);
        repositories.Add(typeof(IObjectRepository<MarketDataDbContext>), context);

        if (options.ApplySchema)
        {
            var schema = new MarketDataSchemaDb(settings, Logger);
            await schema.CreateAsync(schema.ManagedObjects, cancellationToken).ConfigureAwait(false);
        }

        var fmpBackfill = await context.BackfillFmpQueryProjectionsAsync(
            options.BatchSize,
            cancellationToken).ConfigureAwait(false);
        var backfill = await context.BackfillQueryProjectionsV2Async(
            options.BatchSize,
            cancellationToken,
            options.StaleOperationCutoffUtc).ConfigureAwait(false);
        var readiness = await context.GetQueryProjectionReadinessAsync(cancellationToken)
            .ConfigureAwait(false);
        FuturesTradeSignalRepairReadModel? tradeSignalRepair = null;
        if (options.RepairFuturesTradeSignals)
        {
            tradeSignalRepair = await context.RepairFuturesTradeSignalLookupAsync(
                    options.BatchSize,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Console.WriteLine(
            $"Market FMP yield reconciliation: rows={fmpBackfill.YieldCurveRowsSource}/{fmpBackfill.YieldCurveRowsProjected}, " +
            $"years={fmpBackfill.YieldCurveYearsSource}/{fmpBackfill.YieldCurveYearsProjected}.");
        Console.WriteLine(
            $"Market FMP yield fingerprints: rows={fmpBackfill.YieldCurveSourceFingerprint}/{fmpBackfill.YieldCurveProjectedFingerprint}, " +
            $"yieldYears={fmpBackfill.YieldCurveYearsSourceFingerprint}/{fmpBackfill.YieldCurveYearsProjectedFingerprint}.");
        Console.WriteLine("Raw tick maintenance is owned by TradeTickQuoteDb and is not evaluated here.");

        Console.WriteLine(
            $"Market EOD reconciliation: source/projected={backfill.FuturesEodRowsSource}/{backfill.FuturesEodRowsProjected}, " +
            $"fingerprints={backfill.FuturesEodSourceFingerprint}/{backfill.FuturesEodProjectedFingerprint}.");
        Console.WriteLine(
            $"Market VIX reconciliation: rows={backfill.VixFuturesEodRowsSource}, contracts source/indexed={backfill.VixContractsSource}/{backfill.VixContractsIndexed}, " +
            $"fingerprints={backfill.VixContractsSourceFingerprint}/{backfill.VixContractsIndexedFingerprint}.");
        Console.WriteLine(
            $"Market ITI reconciliation: source/day/month/trendMode={backfill.FuturesItiSignalsSource}/{backfill.FuturesItiSignalsByDayProjected}/{backfill.FuturesItiSignalsByMonthProjected}/{backfill.FuturesItiSignalsByTrendModeProjected}, " +
            $"fingerprints={backfill.FuturesItiSignalsSourceFingerprint}/{backfill.FuturesItiSignalsByDayFingerprint}/{backfill.FuturesItiSignalsByMonthFingerprint}/{backfill.FuturesItiSignalsByTrendModeFingerprint}.");
        Console.WriteLine(
            $"PostgreSQL readiness: eod={readiness.FuturesEodByMonth}, " +
            $"vix={readiness.VixFuturesContractIndex}, iti={readiness.FuturesItiSignalQueries}, " +
            $"cutoverCompleted={backfill.CutoverCompleted}.");
        if (tradeSignalRepair is { } repair)
        {
            Console.WriteLine(
                $"Market Futures Trade Signal repair: scanned/valid/quarantined={repair.RowsScanned}/{repair.ValidRows}/{repair.QuarantinedRows}, " +
                $"lookupRows={repair.LookupRowsWritten}; malformed canonical rows were retained.");
        }

        return Complete(fmpBackfill.IsReconciled && backfill.IsReconciled &&
            backfill.CutoverCompleted && readiness.IsReady);
    }

    static DbContextFactory CreateFactory(Dictionary<Type, object> repositories)
        => new(new DbContextResolver(type =>
            repositories.TryGetValue(type, out var repository)
                ? repository
                : throw new InvalidOperationException(
                    $"Storage operation unexpectedly requested repository '{type.Name}'.")));

    static DbConnectionSettings CreateConnectionSettings(
        string connectionName,
        string environmentVariable, string providerName = ScyllaProviderName)
    {
        var connectionString = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new MigrationConfigurationException(
                $"Environment variable '{environmentVariable}' is missing or empty.");
        }

        if (providerName == ScyllaProviderName) EnsureCredentialFree(connectionString, environmentVariable);
        var settings = new DbConnectionSettings();
        settings.Add(connectionName, connectionString, providerName);
        return settings;
    }

    static void EnsureCredentialFree(string connectionString, string environmentVariable)
    {
        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = string.Concat(segment[..separator]
                .Where(static character => !char.IsWhiteSpace(character)))
                .ToLowerInvariant();
            if (key is "userid" or "uid" or "username" or "user" or "password" or "pwd" or "passwd")
            {
                throw new MigrationConfigurationException(
                    $"Environment variable '{environmentVariable}' must contain a credential-free Scylla connection string. " +
                    "Put the userid/password JSON in the SCYLLADB key selected by DOTNET_ENVIRONMENT.");
            }
        }
    }

    static async Task ApplySchemaAsync<TSchema>(
        SchemaDbContext<TSchema> schema,
        IReadOnlyCollection<string> objectNames,
        CancellationToken cancellationToken)
        where TSchema : IObjectRepository
    {
        await schema.CreateAsync(objectNames, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Applied {objectNames.Count} additive projection/state schema objects; canonical objects were not changed.");
    }

    static int Complete(bool reconciled)
    {
        if (reconciled)
        {
            Console.WriteLine("Projection migration completed and reconciled.");
            return SuccessExitCode;
        }

        Console.Error.WriteLine("Projection migration completed with a reconciliation mismatch; read cutover is not safe.");
        return ReconciliationMismatchExitCode;
    }

    static void WriteStartSummary(ProjectionMigrationOptions options)
    {
        Console.WriteLine(
            $"Starting {options.Target.ToString().ToLowerInvariant()} projection migration; " +
            $"connection source={options.ConnectionEnvironmentVariable}; applySchema={options.ApplySchema}; batchSize={options.BatchSize}.");
        if (options.StaleOperationCutoffUtc is { } cutoff)
        {
            Console.WriteLine(
                $"Stale-operation recovery enabled through {cutoff:O}; writers-drained assertion accepted.");
        }
    }

    sealed class MigrationConfigurationException(string message) : Exception(message);

    sealed class UnavailableSequenceIdGenerator : ISequenceIdGenerator
    {
        public static UnavailableSequenceIdGenerator Instance { get; } = new();

        public ValueTask<long> GetSequenceIdAsync(
            SequenceName sequenceName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "The projection migration attempted to allocate a new sequence ID.");

        public ValueTask<long> GetHighWatermarkAsync(
            SequenceName sequenceName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "The projection migration attempted to read a sequence high watermark.");
    }

    sealed class UnavailableBlackboardService : IBlackboardService
    {
        public static UnavailableBlackboardService Instance { get; } = new();

        public IEventSourcingBlackboard EventSourcing => Unavailable<IEventSourcingBlackboard>();
        public IMarketDataBlackboard MarketData => Unavailable<IMarketDataBlackboard>();
        public IMarketDataAnalyticsBlackboard MarketDataAnalytics => Unavailable<IMarketDataAnalyticsBlackboard>();
        public IMarketDataFeedBlackboard MarketDataFeed => Unavailable<IMarketDataFeedBlackboard>();
        public IMarketDataSecuritiesBlackboard MarketDataSecurities => Unavailable<IMarketDataSecuritiesBlackboard>();
        public IReferenceBlackboard Reference => Unavailable<IReferenceBlackboard>();
        public ITradeBlackboard Trade => Unavailable<ITradeBlackboard>();

        static T Unavailable<T>()
            => throw new NotSupportedException(
                $"The projection migration attempted to access Blackboard service '{typeof(T).Name}'.");
    }
}
