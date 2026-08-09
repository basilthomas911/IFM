using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.FundDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Backup;

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
        "economic_calendar_by_country_month_v2",
        "reference_projection_state_v3",
        "reference_projection_mutation_v3",
        "reference_projection_ownership_v3",
        "scheduled_job_by_name_v3",
        "scheduled_job_write_ownership_v3",
    ];

    static readonly string[] SecuritiesProjectionObjects =
    [
        "futures_contract_by_symbol_v2",
        "futures_option_contract_by_symbol_v2",
        "securities_projection_state_v3",
        "securities_symbol_projection_state_v3",
        "securities_projection_operation_v3",
        "securities_projection_operation_scope_v3"
    ];

    static readonly string[] FundProjectionObjects =
    [
        "fund_order_by_order_id_v3",
        "fund_order_write_ownership_v3",
        "fund_transaction_identity_v4",
        "fund_transaction_timeline_v3",
        "fund_balance_by_status_day_v3",
        "fund_transaction_amount_v3",
        "fund_transaction_projection_state_v3",
        "fund_transaction_projection_mutation_v3",
        "fund_transaction_write_mutation_v3",
        "fund_transaction_write_ownership_v3"
    ];

    static readonly string[] MarketProjectionObjects =
    [
        "futures_tick_data_by_time",
        "futures_eod_data_by_month",
        "vix_futures_contract_index",
        "market_data_projection_month",
        "market_data_projection_state_v2",
        "market_data_projection_mutation",
        "market_data_projection_scope_state_v3",
        "market_data_projection_scope_mutation_v3"
    ];

    static async Task<int> Main(string[] args)
    {
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

    static Task<int> RunAsync(
        ProjectionMigrationOptions options,
        CancellationToken cancellationToken)
        => options.Target switch
        {
            ProjectionMigrationTarget.Reference => RunReferenceAsync(options, cancellationToken),
            ProjectionMigrationTarget.Securities => RunSecuritiesAsync(options, cancellationToken),
            ProjectionMigrationTarget.Fund => RunFundAsync(options, cancellationToken),
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
            $"Reference backfill: calendars={backfill.EconomicCalendars}, scheduledJobs={backfill.ScheduledJobs}.");
        Console.WriteLine(
            $"Reference reconciliation: calendar source/projected={reconciliation.SourceEconomicCalendars}/{reconciliation.ProjectedEconomicCalendars}, " +
            $"missing/unexpected={reconciliation.MissingEconomicCalendars}/{reconciliation.UnexpectedEconomicCalendars}; " +
            $"scheduled-job source/projected={reconciliation.SourceScheduledJobs}/{reconciliation.ProjectedScheduledJobs}, " +
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

    static async Task<int> RunFundAsync(
        ProjectionMigrationOptions options,
        CancellationToken cancellationToken)
    {
        var settings = CreateConnectionSettings(
            FundDbContext.FundDbConnection,
            options.ConnectionEnvironmentVariable);
        var repositories = new Dictionary<Type, object>();
        var factory = CreateFactory(repositories);
        var context = new FundDbContext(
            settings,
            factory,
            UnavailableSequenceIdGenerator.Instance,
            Logger);
        repositories.Add(typeof(IObjectRepository<FundDbContext>), context);

        if (options.ApplySchema)
        {
            await ApplySchemaAsync(
                new FundSchemaDb(settings, Logger),
                FundProjectionObjects,
                cancellationToken).ConfigureAwait(false);
        }

        var orderBackfill = await context.BackfillFundOrderByOrderIdProjectionAsync(
                cancellationToken,
                options.StaleOperationCutoffUtc)
            .ConfigureAwait(false);
        var transactionBackfill = await context.BackfillFundTransactionProjectionsAsync(
            options.FundId!.Value,
            options.StartDate!.Value,
            options.EndDate!.Value,
            options.BatchSize,
            cancellationToken,
            options.StaleOperationCutoffUtc).ConfigureAwait(false);

        Console.WriteLine(
            $"Fund order reconciliation: source/projected={orderBackfill.SourceRows}/{orderBackfill.ProjectedRows}, " +
            $"missing/conflicting/tokenless={orderBackfill.MissingRows}/{orderBackfill.ConflictingRows}/{orderBackfill.TokenlessRows}.");
        Console.WriteLine(
            $"Fund transaction reconciliation: read/projected={transactionBackfill.TransactionsRead}/{transactionBackfill.TransactionsProjected}, " +
            $"timeline/status/amount={transactionBackfill.TimelineRows}/{transactionBackfill.StatusBalanceRows}/{transactionBackfill.TransactionAmountRows}, " +
            $"completedMonths={transactionBackfill.CompletedMonths}/{transactionBackfill.TotalMonths}, batches={transactionBackfill.BatchesExecuted}.");
        Console.WriteLine(
            $"Fund transaction identities: logical/reserved={transactionBackfill.LogicalTransactionKeys}/{transactionBackfill.IdentityRows}, " +
            $"missing/conflicting/duplicateCanonical={transactionBackfill.MissingIdentityRows}/{transactionBackfill.ConflictingIdentityRows}/{transactionBackfill.DuplicateCanonicalRows}.");
        Console.WriteLine(
            $"Fund fingerprints: source={transactionBackfill.SourceFingerprint}, timeline={transactionBackfill.TimelineFingerprint}, " +
            $"status={transactionBackfill.StatusBalanceFingerprint}, amount={transactionBackfill.TransactionAmountFingerprint}.");

        return Complete(orderBackfill.IsReconciled && transactionBackfill.IsReconciled);
    }

    static async Task<int> RunMarketAsync(
        ProjectionMigrationOptions options,
        CancellationToken cancellationToken)
    {
        var settings = CreateConnectionSettings(
            MarketDataDbContext.MarketDataDbConnection,
            options.ConnectionEnvironmentVariable);
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
            await ApplySchemaAsync(
                new MarketDataSchemaDb(settings, Logger),
                MarketProjectionObjects,
                cancellationToken).ConfigureAwait(false);
        }

        var backfill = await context.BackfillQueryProjectionsV2Async(
            options.BatchSize,
            cancellationToken,
            options.StaleOperationCutoffUtc).ConfigureAwait(false);
        var readiness = await context.GetQueryProjectionReadinessAsync(cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(
            $"Market tick reconciliation: source/projected={backfill.FuturesTicksSource}/{backfill.FuturesTicksProjected}, " +
            $"fingerprints={backfill.FuturesTicksSourceFingerprint}/{backfill.FuturesTicksProjectedFingerprint}.");
        Console.WriteLine(
            $"Market EOD reconciliation: source/projected={backfill.FuturesEodRowsSource}/{backfill.FuturesEodRowsProjected}, " +
            $"fingerprints={backfill.FuturesEodSourceFingerprint}/{backfill.FuturesEodProjectedFingerprint}.");
        Console.WriteLine(
            $"Market VIX reconciliation: rows={backfill.VixFuturesEodRowsSource}, contracts source/indexed={backfill.VixContractsSource}/{backfill.VixContractsIndexed}, " +
            $"fingerprints={backfill.VixContractsSourceFingerprint}/{backfill.VixContractsIndexedFingerprint}.");
        Console.WriteLine(
            $"Market readiness: tick={readiness.FuturesTickByTime}, eod={readiness.FuturesEodByMonth}, " +
            $"vix={readiness.VixFuturesContractIndex}, cutoverCompleted={backfill.CutoverCompleted}.");

        return Complete(backfill.IsReconciled && backfill.CutoverCompleted && readiness.IsReady);
    }

    static DbContextFactory CreateFactory(Dictionary<Type, object> repositories)
        => new(new DbContextResolver(type =>
            repositories.TryGetValue(type, out var repository)
                ? repository
                : throw new InvalidOperationException(
                    $"Storage operation unexpectedly requested repository '{type.Name}'.")));

    static DbConnectionSettings CreateConnectionSettings(
        string connectionName,
        string environmentVariable)
    {
        var connectionString = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new MigrationConfigurationException(
                $"Environment variable '{environmentVariable}' is missing or empty.");
        }

        EnsureCredentialFree(connectionString, environmentVariable);
        var settings = new DbConnectionSettings();
        settings.Add(connectionName, connectionString, ScyllaProviderName);
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
        Console.WriteLine($"Applied {objectNames.Count} additive projection/state schema objects; canonical and ITI/RSI objects were not changed.");
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
        if (options.Target == ProjectionMigrationTarget.Fund)
        {
            Console.WriteLine(
                $"Fund scope: fundId={options.FundId}, dates={options.StartDate:yyyy-MM-dd}..{options.EndDate:yyyy-MM-dd}.");
        }
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
        public IFundBlackboard Fund => Unavailable<IFundBlackboard>();
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
