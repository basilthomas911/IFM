using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests;

public class MarketDataFeedFixture : IDisposable
{
    public DbContextFactory DbFactory { get; private set; }
    public IMarketDataDbContext MarketDataDb { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }
    public IBlackboardService BlackboardService { get; private set; }
    public MarketDataFeedFixture()
    {
        SetSeqIdDatabase();
        SetDbFactory();
    }

    void SetDbFactory()
    {
        var dbConn = new DbConnectionSettings()
             .Add("MarketDataDbConnection",
                 Environment.GetEnvironmentVariable("IFM_TEST_MARKET_DATA_CONNECTION") ?? throw new InvalidOperationException("The assembly integration fixture did not set IFM_TEST_MARKET_DATA_CONNECTION."),
                 "System.Data.Postgres")
             .Add(TradeTickQuoteDbContext.TradeTickQuoteDbConnection,
                 Environment.GetEnvironmentVariable("ConnectionStrings__TradeTickQuoteDbConnection")
                     ?? throw new InvalidOperationException("The assembly integration fixture did not configure the raw tick database."),
                 "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, IObjectRepository>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var marketDataDbLogger = Substitute.For<ILogger<DbProvider>>();
        marketDataDbLogger.When(_ => { }).Do(_ => { });
        var redisCahe = Substitute.For<IRedisCache>();
        redisCahe.When(_ => { }).Do(_ => { });
        var blackboardService = new BlackboardService(redisCahe, new SystemTextJsonSerializer());
        BlackboardService = blackboardService;
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory, blackboardService, SequenceIdGenerator, logger));
        diContainer.Add(typeof(IObjectRepository<TradeTickQuoteDbContext>), new TradeTickQuoteDbContext(dbConn, logger));
        DbFactory = dbFactory;
        MarketDataDb = dbFactory.MarketDataDb as IMarketDataDbContext;
    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection",
                 Environment.GetEnvironmentVariable("IFM_TEST_POSTGRES_CONNECTION") ?? throw new InvalidOperationException("The assembly integration fixture did not set IFM_TEST_POSTGRES_CONNECTION."),
                 "System.Data.Postgres");
        var diContainer = new Dictionary<Type, SequenceIdDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<SequenceIdDbContext>), new SequenceIdDbContext(dbConn, dbFactory, logger));
        SeqIdDatabase = dbFactory.SequenceIdDb as SequenceIdDbContext;
        SequenceIdGenerator = new PostgresSequenceIdGenerator(dbFactory.SequenceIdDb as SequenceIdDbContext);

    }

    /// <summary>Removes one raw EOD observation so immutable-write tests start from a clean identity.</summary>
    public async ValueTask DeleteRawEodObservationAsync(
        string seriesKey,
        string contractId,
        DateOnly valueDate)
        => await MarketDataDb
            .Use(
                "MarketDataDbSql.DeleteRawEodForIntegrationTest",
                "DELETE FROM market_data.futures_eod_observation WHERE seriesKey = $1 AND yearMonth = $2 AND valueDate = $3 AND contractId = $4")
            .SetParameters(new RawEodKey(
                seriesKey,
                checked(valueDate.Year * 100 + valueDate.Month),
                valueDate,
                contractId))
            .ExecuteCommandAsync()
            .ConfigureAwait(false);

    public void Dispose()
    {
    }


    readonly record struct RawEodKey(
        string SeriesKey,
        int YearMonth,
        DateOnly ValueDate,
        string ContractId) : IBindValue
    {
        public object Bind() => new object?[]
        {
            SeriesKey,
            YearMonth,
            ValueDate,
            ContractId
        };
    }
}

