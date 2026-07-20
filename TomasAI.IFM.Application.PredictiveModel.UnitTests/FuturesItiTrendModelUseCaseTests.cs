using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.PredictiveModel.EventHandlers;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Events;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.PredictiveModel.UnitTests;

public class FuturesItiTrendModelUseCaseTests(MarketDataFixture testFixture)
    : IClassFixture<MarketDataFixture>
{
    MarketDataFixture TestFixture { get; } = testFixture;

    [Fact]
    public void OnFuturesItiTrendDeltaModelTrainedEvent_Ok()
    {
         var useCaseEvents = new FuturesItiTrendModelUseCaseEvents(
             TestFixture.FuturesItiPredictiveTrendModel, 
             TestFixture.FuturesItiTrendEventProducer,
             TestFixture.StatusConsoleWriter);
        FuturesItiTrendDeltaModelTrainedEvent trendModelTrainedEvent = SampleData.FuturesItiTrendDeltaModelTrainedEvent;
        useCaseEvents.ExecuteAsync(trendModelTrainedEvent).Wait();
        TestFixture.ResultEvent.Should().NotBeNull();
        TestFixture.ResultEvent.Should().BeOfType<FuturesItiTrendDeltaModelTrainedCompleteEvent>();
    }

    [Fact]
    public void OnFuturesItiTrendClassModelTrainedEvent_Ok()
    {
        var useCaseEvents = new FuturesItiTrendModelUseCaseEvents(
            TestFixture.FuturesItiPredictiveTrendModel,
            TestFixture.FuturesItiTrendEventProducer,
            TestFixture.StatusConsoleWriter);
        FuturesItiTrendClassModelTrainedEvent trendModelTrainedEvent = SampleData.FuturesItiTrendClassModelTrainedEvent;
        useCaseEvents.ExecuteAsync(trendModelTrainedEvent).Wait();
        TestFixture.ResultEvent.Should().NotBeNull();
        TestFixture.ResultEvent.Should().BeOfType<FuturesItiTrendClassModelTrainedCompleteEvent>();
    }
}

public class MarketDataFixture : IDisposable
{
    public MarketDataFixture()
    {
        SetDevDatabase();
        SetProdDatabase();
    }

    public void Dispose()
    {
        // Do "global" teardown here; Only called once.
    }

    public MarketDataDbContext Database { get; private set; }
    public MarketDataDbContext ProdDatabase { get; private set; }
    public IDbContextFactory ProdDbContextFactory { get; private set; }
    public IFuturesItiPredictiveTrendModel FuturesItiPredictiveTrendModel { get; private set; } 
    public IFuturesItiTrendEventProducer FuturesItiTrendEventProducer { get; private set; }
    public IEvent ResultEvent { get; private set; }
    public IStatusConsoleWriter StatusConsoleWriter { get; private set; }

    void SetDevDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatatestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
        var diContainer = new Dictionary<Type, MarketDataDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var blackboardService = Substitute.For<IBlackboardService>();
        blackboardService.When(_ => { }).Do(_ => { });
        var sequenceIdGenerator = Substitute.For<ISequenceIdGenerator>();
        sequenceIdGenerator.When(_ => { }).Do(_ => { });
        diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory, blackboardService, sequenceIdGenerator, logger));
        Database =  (dbFactory.MarketDataDb as MarketDataDbContext)!;
    }

    void SetProdDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
        var diContainer = new Dictionary<Type, MarketDataDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var blackboardService = Substitute.For<IBlackboardService>();
        blackboardService.When(_ => { }).Do(_ => { });
        var sequenceIdGenerator = Substitute.For<ISequenceIdGenerator>();
        sequenceIdGenerator.When(_ => { }).Do(_ => { });
        diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, dbFactory, blackboardService, sequenceIdGenerator, logger));
        ProdDatabase = (dbFactory.MarketDataDb as MarketDataDbContext)!;
        ProdDbContextFactory = dbFactory;
        var trendQueryApi = Substitute.For<IFuturesItiTrendQueryApi>();
        FuturesItiPredictiveTrendModel = new FuturesItiPredictiveTrendModel(ProdDatabase);
        var eventProducer = Substitute.For<IFuturesItiTrendEventProducer>();
        eventProducer
            .When(e => e.PostEventAsync(Arg.Any<IEvent>()))
            .Do(callInfo => {
                ResultEvent = callInfo.ArgAt<IEvent>(0);
            });
        FuturesItiTrendEventProducer = eventProducer;
        StatusConsoleWriter = Substitute.For<IStatusConsoleWriter>();
    }

}