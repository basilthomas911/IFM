using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.DevelopmentData;

/// <summary>Resets and seeds disposable development-only storage datasets.</summary>
internal static class Program
{
    const string ConnectionEnvironmentVariable = "IFM_STORAGE_DEVELOPMENT_MARKET_DATA_SCYLLA_CONNECTION";
    const string DefaultDevelopmentConnection =
        "Contact Points=localhost;Port=9042;Default Keyspace=market_data_test_db";
    const string ScyllaProviderName = "System.Data.ScyllaDb";
    static readonly string[] FuturesItiObjects =
    [
        "futures_iti_signal",
        "futures_iti_signal_by_contract_day",
        "futures_iti_signal_by_contract_month",
        "futures_iti_signal_by_trend_mode_month",
        "futures_iti_timeframe_state",
        "futures_iti_signal_index"
    ];

    static async Task<int> Main(string[] args)
    {
        if (!TryParse(args, out var contractId, out var valueDate, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(
                "Usage: dotnet run --project TomasAI.IFM.Application.Storage.DevelopmentData -- " +
                "--contract-id <id> --value-date <yyyy-MM-dd> --confirm-reset");
            return 2;
        }

        try
        {
            EnsureDevelopmentEnvironment();
            var connection = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable)
                ?? DefaultDevelopmentConnection;
            EnsureDevelopmentKeyspace(connection);

            var settings = new DbConnectionSettings();
            settings.Add(
                MarketDataDbContext.MarketDataDbConnection,
                connection,
                ScyllaProviderName);
            var repositories = new Dictionary<Type, object>();
            var factory = new DbContextFactory(new DbContextResolver(type =>
                repositories.TryGetValue(type, out var repository)
                    ? repository
                    : throw new InvalidOperationException(
                        $"Development seed requested unavailable repository '{type.Name}'.")));
            var context = new MarketDataDbContext(
                settings,
                factory,
                UnavailableBlackboardService.Instance,
                UnavailableSequenceIdGenerator.Instance,
                NullLogger<DbProvider>.Instance);
            repositories.Add(typeof(IObjectRepository<MarketDataDbContext>), context);

            var schema = new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance);
            Console.WriteLine(
                $"Resetting Futures ITI development data for contract {contractId}, " +
                $"display value date {valueDate:yyyy-MM-dd}.");
            await schema.RecreateAsync(FuturesItiObjects).ConfigureAwait(false);

            var rows = CreateHistory(contractId!, valueDate);
            foreach (var row in rows)
                await context.InsertFuturesItiSignalAsync(row).ConfigureAwait(false);

            var window = FuturesItiSignalHistoryWindow.Resolve(valueDate, TimeFrameType.Monthly);
            var persisted = await context.GetFuturesItiSignalsForContractAsync(
                contractId!,
                window.StartValueDate,
                window.EndValueDate).ConfigureAwait(false);
            Console.WriteLine(
                $"Seeded {persisted.Count} Futures ITI rows: " +
                string.Join(", ", persisted
                    .GroupBy(static row => row.TimePeriod)
                    .OrderBy(static group => group.Key)
                    .Select(group => $"{group.Key}={group.Count()}")));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Development Futures ITI reset/seed failed ({exception.GetType().Name}): {exception.Message}");
            return 1;
        }
    }

    static IReadOnlyList<FuturesItiSignalV2ReadModel> CreateHistory(
        string contractId,
        DateOnly valueDate)
    {
        var weekly = FuturesItiSignalHistoryWindow.Resolve(valueDate, TimeFrameType.Weekly);
        var monthly = FuturesItiSignalHistoryWindow.Resolve(valueDate, TimeFrameType.Monthly);
        List<FuturesItiSignalV2ReadModel> rows = [];
        long sequence = 9_100_000;

        var dailyTimes = new[]
        {
            valueDate.AddDays(-1).ToDateTime(new TimeOnly(18, 0)),
            valueDate.AddDays(-1).ToDateTime(new TimeOnly(21, 30)),
            valueDate.ToDateTime(new TimeOnly(1, 15)),
            valueDate.ToDateTime(new TimeOnly(7, 45)),
            valueDate.ToDateTime(new TimeOnly(10, 0)),
            valueDate.ToDateTime(new TimeOnly(12, 30)),
            valueDate.ToDateTime(new TimeOnly(15, 10)),
            valueDate.ToDateTime(new TimeOnly(16, 55))
        };
        AddSeries(rows, contractId, valueDate, TimeFrameType.Daily,
            valueDate, dailyTimes, 6492, ref sequence);

        var weeklyDates = Enumerable.Range(
                0,
                weekly.EndValueDate.DayNumber - weekly.StartValueDate.DayNumber + 1)
            .Select(weekly.StartValueDate.AddDays)
            .Where(static date => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            .ToArray();
        var weeklyTimes = weeklyDates
            .SelectMany(date => new[]
            {
                date.ToDateTime(new TimeOnly(9, 35)),
                date.ToDateTime(new TimeOnly(15, 40))
            })
            .ToArray();
        AddSeries(rows, contractId, valueDate, TimeFrameType.Weekly,
            weekly.StartValueDate, weeklyTimes, 6465, ref sequence);

        var monthlyDates = Enumerable.Range(
                0,
                monthly.EndValueDate.DayNumber - monthly.StartValueDate.DayNumber + 1)
            .Select(monthly.StartValueDate.AddDays)
            .Where(static date => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            .ToArray();
        var monthlyTimes = monthlyDates
            .SelectMany((date, index) => index % 3 == 0
                ? new[]
                {
                    date.ToDateTime(new TimeOnly(9, 45)),
                    date.ToDateTime(new TimeOnly(15, 20))
                }
                : new[] { date.ToDateTime(new TimeOnly(13, 0)) })
            .ToArray();
        AddSeries(rows, contractId, valueDate, TimeFrameType.Monthly,
            monthly.StartValueDate, monthlyTimes, 6380, ref sequence);
        return rows;
    }

    static void AddSeries(
        ICollection<FuturesItiSignalV2ReadModel> rows,
        string contractId,
        DateOnly displayValueDate,
        TimeFrameType period,
        DateOnly frameStart,
        IReadOnlyList<DateTime> marketTimes,
        double basePrice,
        ref long sequence)
    {
        var modes = new[]
        {
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.Trending,
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.Trending,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.HoldTradeChanged,
            IntrinsicTimeModeType.InTradeChanged
        };
        var direction = IntrinsicTimeTrendType.UpTrend;
        var directionPrice = basePrice;
        var extreme = basePrice;

        for (var index = 0; index < marketTimes.Count; index++)
        {
            var mode = modes[index % modes.Length];
            if (mode == IntrinsicTimeModeType.TrendDirectionChanged && index > 0)
                direction = direction == IntrinsicTimeTrendType.UpTrend
                    ? IntrinsicTimeTrendType.DownTrend
                    : IntrinsicTimeTrendType.UpTrend;
            var wave = Math.Sin(index * 0.72) * 13;
            var drift = direction == IntrinsicTimeTrendType.UpTrend ? index * 1.8 : -index * 1.2;
            var price = Math.Round(basePrice + wave + drift, 2);
            if (mode == IntrinsicTimeModeType.TrendDirectionChanged)
                directionPrice = price;
            extreme = direction == IntrinsicTimeTrendType.UpTrend
                ? Math.Max(extreme, price)
                : Math.Min(extreme, price);
            var threshold = 18d;
            var bandLevel = direction == IntrinsicTimeTrendType.UpTrend
                ? (price - directionPrice) / threshold
                : (directionPrice - price) / threshold;
            var establishedMove = Math.Abs(extreme - directionPrice);
            var reversalLevel = establishedMove == 0
                ? 0
                : direction == IntrinsicTimeTrendType.UpTrend
                    ? Math.Max(0, (extreme - price) / establishedMove)
                    : Math.Max(0, (price - extreme) / establishedMove);
            var marketTime = DateTime.SpecifyKind(marketTimes[index], DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(marketTime, FuturesTradingValueDate.MarketTimeZone);
            var rowValueDate = period == TimeFrameType.Daily
                ? displayValueDate
                : DateOnly.FromDateTime(marketTime);

            rows.Add(new FuturesItiSignalV2ReadModel(
                contractId,
                rowValueDate,
                period,
                sequence++,
                utc,
                intrinsicTimeGroupId: index + 1,
                intrinsicTimeLength: index + 1,
                intrinsicPrice: price,
                intrinsicTimeTrend: direction,
                intrinsicTimeMode: mode,
                trendPrice: directionPrice,
                trendExtreme: extreme,
                trendReversal: direction == IntrinsicTimeTrendType.UpTrend
                    ? extreme - threshold
                    : extreme + threshold,
                trendDelta: price - directionPrice,
                targetDelta: threshold,
                lambda: 0.75,
                tradingDays: period switch
                {
                    TimeFrameType.Daily => 1,
                    TimeFrameType.Weekly => 5,
                    _ => 20
                },
                threshold: threshold,
                upTrendTrigger: directionPrice + threshold,
                downTrendTrigger: directionPrice - threshold,
                tradeState: mode == IntrinsicTimeModeType.HoldTradeChanged
                    ? IntrinsicTimeTradeState.Hold
                    : IntrinsicTimeTradeState.Ready,
                timeFrameStartValueDate: frameStart,
                bandAnchorPrice: directionPrice,
                bandPercentage: 0.1,
                bandSize: threshold * 0.1,
                bandLevel: bandLevel,
                reversalLevel: reversalLevel));
        }
    }

    static bool TryParse(
        IReadOnlyList<string> args,
        out string? contractId,
        out DateOnly valueDate,
        out string? error)
    {
        contractId = null;
        valueDate = default;
        error = null;
        var confirmed = false;
        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--contract-id" when ++index < args.Count:
                    contractId = args[index];
                    break;
                case "--value-date" when ++index < args.Count:
                    if (!DateOnly.TryParseExact(args[index], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out valueDate))
                    {
                        error = "--value-date must use yyyy-MM-dd.";
                        return false;
                    }
                    break;
                case "--confirm-reset":
                    confirmed = true;
                    break;
                default:
                    error = $"Unknown or incomplete option '{args[index]}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(contractId) || valueDate == default || !confirmed)
        {
            error = "--contract-id, --value-date, and --confirm-reset are required.";
            return false;
        }
        return true;
    }

    static void EnsureDevelopmentEnvironment()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The development data tool requires DOTNET_ENVIRONMENT=Development.");
        }
    }

    static void EnsureDevelopmentKeyspace(string connection)
    {
        var keyspace = connection.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(static parts => parts.Length == 2)
            .Where(static parts => parts[0].Replace(" ", string.Empty, StringComparison.Ordinal)
                .Equals("DefaultKeyspace", StringComparison.OrdinalIgnoreCase))
            .Select(static parts => parts[1])
            .SingleOrDefault();
        if (!string.Equals(keyspace, "market_data_test_db", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing destructive reset for keyspace '{keyspace ?? "<missing>"}'. " +
                "Only market_data_test_db is permitted.");
        }
    }

    sealed class UnavailableSequenceIdGenerator : ISequenceIdGenerator
    {
        public static UnavailableSequenceIdGenerator Instance { get; } = new();
        public ValueTask<long> GetSequenceIdAsync(SequenceName sequenceName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Development seed rows must provide deterministic sequence IDs.");
        public ValueTask<long> GetHighWatermarkAsync(SequenceName sequenceName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Development seeding does not inspect sequence high watermarks.");
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
        static T Unavailable<T>() => throw new NotSupportedException(
            $"Development seeding attempted to access Blackboard service '{typeof(T).Name}'.");
    }
}
